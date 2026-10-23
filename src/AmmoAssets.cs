using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jotunn.Managers;
using UnityEngine;

namespace AmmoVariants
{
    internal static class AmmoAssets
    {
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, AudioClip> Sounds = new Dictionary<int, AudioClip>();
        private static readonly Dictionary<GameObject, GameObject> ReleaseSounds = new Dictionary<GameObject, GameObject>();
        private static readonly Dictionary<EffectList, EffectList> QuietEffects = new Dictionary<EffectList, EffectList>();
        private static readonly MethodInfo CloneEffect = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static AssetBundle bundle;
        internal static Shader TrailShader;

        internal static byte[] Bytes(string suffix)
        {
            var assembly = typeof(Plugin).Assembly;
            string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
        }

        internal static void Load()
        {
            if (TrailShader != null) return;
            bundle = AssetBundle.LoadFromMemory(Bytes("ammo_visuals"));
            TrailShader = bundle.LoadAsset<Shader>("Assets/AmmoTrail.shader");
            if (TrailShader == null) throw new InvalidOperationException("ammo trail shader unavailable");
            foreach (string baseName in Items.BoltBases.Concat(Items.ArrowBases))
                foreach (string kind in new[] { "Scatter", "Piercing" }) LoadIcon(baseName + kind);
            foreach (string baseName in Items.EnergyBases) LoadIcon(baseName + "Energy");
            string[] soundNames = { "scatter_bow", "scatter_crossbow", "piercing_bow", "piercing_crossbow", "energy_bow", "energy_crossbow" };
            for (int i = 0; i < soundNames.Length; i++) Sounds.Add(i + 2, LoadWave(soundNames[i]));
            Plugin.Log.LogInfo("loaded 42 ammo icons, six firing sounds and transparent trail shader");
        }

        private static void LoadIcon(string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
            var loader = conversion.GetMethods().First(m =>
            {
                try { var p = m.GetParameters(); return m.Name == "LoadImage" && p.Length == 3 && p[1].ParameterType == typeof(byte[]); }
                catch (TypeLoadException) { return false; }
            });
            if (!(bool)loader.Invoke(null, new object[] { texture, Bytes(name + ".png"), false })) throw new InvalidOperationException("invalid icon " + name);
            texture.name = name;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            Icons.Add(name, sprite);
        }

        internal static Sprite Icon(string name) { return Icons[name]; }

        internal static void RegisterReleaseSounds()
        {
            foreach (string name in new[] { "sfx_bow_fire", "sfx_arbalest_fire" })
            {
                var source = PrefabManager.Instance.GetPrefab(name);
                if (source == null) { Plugin.Log.LogWarning("missing native release sound " + name); continue; }
                if (ReleaseSounds.ContainsKey(source)) continue;
                var quiet = PrefabManager.Instance.CreateClonedPrefab("AmmoVariants_" + name, source);
                foreach (var sound in quiet.GetComponentsInChildren<ZSFX>(true))
                {
                    sound.m_minVol *= Plugin.NativeReleaseVolume.Value;
                    sound.m_maxVol *= Plugin.NativeReleaseVolume.Value;
                }
                foreach (var sound in quiet.GetComponentsInChildren<AudioSource>(true)) sound.volume *= Plugin.NativeReleaseVolume.Value;
                PrefabManager.Instance.AddPrefab(quiet);
                ReleaseSounds.Add(source, quiet);
            }
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"registered {ReleaseSounds.Count} native release sound copies at volume {Plugin.NativeReleaseVolume.Value:F2}, ammo accents {Plugin.FiringSoundVolume.Value:F2}");
        }

        internal static EffectList ReleaseEffects(EffectList effects, bool variant)
        {
            if (!variant || effects == null) return effects;
            if (QuietEffects.TryGetValue(effects, out var cached)) return cached;
            int replaced = 0;
            var copy = new EffectList { m_effectPrefabs = effects.m_effectPrefabs.Select(effect =>
            {
                if (effect == null || effect.m_prefab == null || !ReleaseSounds.TryGetValue(effect.m_prefab, out var quiet)) return effect;
                var entry = (EffectList.EffectData)CloneEffect.Invoke(effect, null);
                entry.m_prefab = quiet;
                replaced++;
                return entry;
            }).ToArray() };
            QuietEffects.Add(effects, copy);
            if (Plugin.LogCounters.Value && replaced > 0) Plugin.Log.LogInfo("variant native release effects replaced " + replaced);
            return copy;
        }

        internal static GameObject[] CreateRelease(EffectList effects, Vector3 basePos, Quaternion baseRot, Transform baseParent,
            float scale, int variant, ZDOID gamepadEffectsExclusiveToPlayer, Attack attack)
        {
            return ReleaseEffects(effects, Shots.IsVariantAttack(attack)).Create(basePos, baseRot, baseParent, scale, variant, gamepadEffectsExclusiveToPlayer);
        }

        private static AudioClip LoadWave(string name)
        {
            using (var reader = new BinaryReader(new MemoryStream(Bytes(name + ".wav"))))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException(name);
                reader.ReadInt32(); reader.ReadChars(4);
                int channels = 0, rate = 0, bits = 0;
                byte[] data = null;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunk = new string(reader.ReadChars(4));
                    int length = reader.ReadInt32();
                    long next = reader.BaseStream.Position + length + (length & 1);
                    if (chunk == "fmt ")
                    {
                        if (reader.ReadInt16() != 1) throw new InvalidDataException(name);
                        channels = reader.ReadInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                    }
                    else if (chunk == "data") data = reader.ReadBytes(length);
                    reader.BaseStream.Position = next;
                }
                if (data == null || bits != 16 || channels != 1) throw new InvalidDataException(name);
                var samples = new float[data.Length / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
                var clip = AudioClip.Create(name, samples.Length, channels, rate, false);
                var setter = typeof(AudioClip).GetMethods().First(m =>
                {
                    try { var p = m.GetParameters(); return m.Name == "SetData" && p.Length == 2 && p[0].ParameterType == typeof(float[]); }
                    catch (TypeLoadException) { return false; }
                });
                if (!(bool)setter.Invoke(clip, new object[] { samples, 0 })) throw new InvalidDataException(name);
                return clip;
            }
        }

        internal static void Play(int code, Vector3 position)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!Sounds.TryGetValue(code, out var clip) || Plugin.FiringSoundVolume.Value <= 0f) return;
            string prefabName = (code & 1) == 0 ? "sfx_bow_fire" : "sfx_arbalest_fire";
            var prefab = PrefabManager.Instance.GetPrefab(prefabName) ?? PrefabManager.Instance.GetPrefab("sfx_bow_fire");
            var template = prefab != null ? prefab.GetComponentInChildren<AudioSource>(true) : null;
            if (template == null || template.outputAudioMixerGroup == null)
            {
                Plugin.Log.LogWarning("ammo firing sound skipped because the game SFX mixer was unavailable");
                return;
            }
            var holder = new GameObject("AmmoFiringSound");
            holder.transform.position = position;
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.outputAudioMixerGroup = template.outputAudioMixerGroup;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 40f;
            source.volume = Plugin.FiringSoundVolume.Value;
            source.Play();
            UnityEngine.Object.Destroy(holder, clip.length + 0.1f);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("ammo firing sound " + clip.name);
        }
    }

    public sealed class AmmoFlight : MonoBehaviour
    {
        private ZNetView view;
        private float waited;

        internal void Played() { enabled = false; }

        private void Awake() { view = GetComponent<ZNetView>(); }

        private void Update()
        {
            waited += Time.deltaTime;
            if (waited > 2f) { enabled = false; return; }
            if (view == null || !view.IsValid()) return;
            int code = view.GetZDO().GetInt(Shots.SoundKey, 0);
            if (code == 0) return;
            if (code >= 2 && waited < 0.5f) AmmoAssets.Play(code, view.GetZDO().GetVec3(Shots.SoundKey + ".Origin", transform.position));
            enabled = false;
        }
    }
}
