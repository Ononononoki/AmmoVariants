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
        private static readonly Dictionary<int, AudioClip[]> Sounds = new Dictionary<int, AudioClip[]>();
        internal const int ExplosiveBowSounds = 4;
        internal const int ExplosiveCrossbowSounds = 6;
        internal const int ExplosiveEarlySounds = 3;
        internal const int ExplosivePlainsSounds = 3;
        internal static AudioClip BlastArrow;
        internal static AudioClip BlastBolt;
        internal static AudioClip EarlyBlastArrow;
        internal static AudioClip EarlyBlastBolt;
        internal static AudioClip PlainsBlastArrow;
        internal static AudioClip PlainsBlastBolt;
        private static AudioClip fuse;
        private static readonly Dictionary<GameObject, GameObject> ReleaseSounds = new Dictionary<GameObject, GameObject>();
        private static readonly Dictionary<EffectList, EffectList> QuietEffects = new Dictionary<EffectList, EffectList>();
        private static readonly MethodInfo CloneEffect = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static AssetBundle bundle;
        internal static Shader TrailShader;
        internal static Shader HeatShader;
        internal static Shader SootShader;

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
            HeatShader = bundle.LoadAsset<Shader>("Assets/AmmoShaftHeat.shader");
            SootShader = bundle.LoadAsset<Shader>("Assets/AmmoShaftSoot.shader");
            if (HeatShader == null || SootShader == null || !HeatShader.isSupported || !SootShader.isSupported)
                Plugin.Log.LogWarning("shaft heat shaders unavailable, explosive ammo keeps the plain shaft");
            foreach (string baseName in Items.BoltBases.Concat(Items.ArrowBases))
                foreach (string kind in new[] { "Scatter", "Piercing" }) LoadIcon(baseName + kind);
            foreach (string baseName in Items.EnergyBases) LoadIcon(baseName + "Energy");
            foreach (string baseName in Items.ExplosiveBases) LoadIcon(baseName + "Explosive");
            string[] soundNames = { "scatter_bow", "scatter_crossbow", "piercing_bow", "piercing_crossbow", "energy_bow", "energy_crossbow" };
            for (int i = 0; i < soundNames.Length; i++) Sounds.Add(i + 2, new[] { LoadWave(soundNames[i]) });
            // explosive firing picks one of several mechanical variations at random, codes follow Shots.ConsumeSound
            Sounds.Add(2 + ((int)Items.AmmoKind.Explosive - 1) * 2, Enumerable.Range(1, ExplosiveBowSounds).Select(i => LoadWave("explosive_bow_" + i)).ToArray());
            Sounds.Add(3 + ((int)Items.AmmoKind.Explosive - 1) * 2, Enumerable.Range(1, ExplosiveCrossbowSounds).Select(i => LoadWave("explosive_crossbow_" + i)).ToArray());
            // early explosive firing sits two codes above the charred one, see Shots.OnSetup
            Sounds.Add(4 + ((int)Items.AmmoKind.Explosive - 1) * 2, Enumerable.Range(1, ExplosiveEarlySounds).Select(i => LoadWave("explosive_early_bow_" + i)).ToArray());
            Sounds.Add(5 + ((int)Items.AmmoKind.Explosive - 1) * 2, Enumerable.Range(1, ExplosiveEarlySounds).Select(i => LoadWave("explosive_early_crossbow_" + i)).ToArray());
            // plains explosive firing sits four codes above the charred one, between the charred and the early weight
            Sounds.Add(6 + ((int)Items.AmmoKind.Explosive - 1) * 2, Enumerable.Range(1, ExplosivePlainsSounds).Select(i => LoadWave("explosive_plains_bow_" + i)).ToArray());
            Sounds.Add(7 + ((int)Items.AmmoKind.Explosive - 1) * 2, Enumerable.Range(1, ExplosivePlainsSounds).Select(i => LoadWave("explosive_plains_crossbow_" + i)).ToArray());
            BlastArrow = LoadWave("explosive_blast_arrow");
            BlastBolt = LoadWave("explosive_blast_bolt");
            EarlyBlastArrow = LoadWave("explosive_early_blast_arrow");
            EarlyBlastBolt = LoadWave("explosive_early_blast_bolt");
            PlainsBlastArrow = LoadWave("explosive_plains_blast_arrow");
            PlainsBlastBolt = LoadWave("explosive_plains_blast_bolt");
            fuse = LoadWave("explosive_fuse");
            Plugin.Log.LogInfo($"loaded {Icons.Count} ammo icons, {Sounds.Values.Sum(s => s.Length)} firing sounds, explosive blast and fuse sounds and transparent trail shader");
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
            if (!Sounds.TryGetValue(code, out var clips) || Plugin.FiringSoundVolume.Value <= 0f) return;
            var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            var holder = Sound("AmmoFiringSound", clip, (code & 1) == 0 ? "sfx_bow_fire" : "sfx_arbalest_fire");
            if (holder == null) return;
            holder.transform.position = position;
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("ammo firing sound " + clip.name);
        }

        // the fuse hiss rides on the stuck shot, the blast removes it with the shot.
        internal static void PlayFuse(Transform shot)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || fuse == null) return;
            var holder = Sound("AmmoFuseSound", fuse, "sfx_bow_fire");
            if (holder == null) return;
            holder.transform.SetParent(shot, false);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("explosive fuse sound on " + shot.name);
        }

        // an audio source on the game's SFX mixer, removed after the clip.
        private static GameObject Sound(string name, AudioClip clip, string prefabName)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName) ?? PrefabManager.Instance.GetPrefab("sfx_bow_fire");
            var template = prefab != null ? prefab.GetComponentInChildren<AudioSource>(true) : null;
            if (template == null || template.outputAudioMixerGroup == null)
            {
                Plugin.Log.LogWarning("ammo sound skipped because the game SFX mixer was unavailable");
                return null;
            }
            var holder = new GameObject(name);
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
            return holder;
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
