using System.Linq;
using UnityEngine;

namespace AmmoVariants
{
    internal static class AmmoDisplayEffects
    {
        private const float RadiusShare = 0.035f;
        private const float ScatterSizeShare = 0.42f;
        private const float MinimumRadius = 0.008f;
        private const float MaximumRadius = 0.045f;
        private const float ParticleLifetime = 0.22f;
        private const int ParticleLimit = 12;
        private const float ParticleRate = 14f;
        private const float TrailLifetime = 0.08f;
        private const int GlowTextureSize = 32;
        private const float GlowOpacity = 0.8f;
        private const float GlowSizeShare = 2.2f;
        private static Material auraMaterial;

        internal static void Add(GameObject projectile, Transform shaft, Vector3 tip, float length, bool scatter)
        {
            bool energy = Items.EnergyProjectiles.Contains(projectile);
            bool frost = projectile.name.StartsWith("ArrowFrost");
            var sources = projectile.GetComponentsInChildren<ParticleSystem>(true);
            if (!energy && sources.Length == 0) return;
            var holder = new GameObject("AmmoTipEffects"); holder.SetActive(false);
            holder.transform.SetParent(shaft,false);
            holder.transform.localPosition = tip;
            holder.transform.localScale = new Vector3(1f/shaft.localScale.x,1f/shaft.localScale.y,1f/shaft.localScale.z);
            float radius = Mathf.Clamp(length * RadiusShare * (scatter ? ScatterSizeShare : 1f),MinimumRadius,MaximumRadius);
            if (energy)
            {
                AmmoVisuals.AddLightning(holder);
            }
            else
            {
                foreach (var source in sources)
                {
                    var copy = Object.Instantiate(source.gameObject,holder.transform);
                    copy.name = "AmmoTip_" + source.name;
                    foreach (Transform child in copy.transform.Cast<Transform>().ToArray())
                    {
                        child.gameObject.SetActive(false);
                        Object.DestroyImmediate(child.gameObject);
                    }
                    foreach (var script in copy.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(script);
                    foreach (var collider in copy.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                    foreach (var audio in copy.GetComponents<AudioSource>()) Object.DestroyImmediate(audio);
                    foreach (var light in copy.GetComponents<Light>()) Object.DestroyImmediate(light);
                    copy.transform.localPosition = Vector3.zero;
                    copy.transform.localRotation = Quaternion.identity;
                    copy.transform.localScale = Vector3.one;
                    copy.SetActive(true);
                }
            }
            foreach (var system in holder.GetComponentsInChildren<ParticleSystem>(true))
            {
                Configure(system,radius);
                if (frost)
                {
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.sharedMaterial = AuraMaterial();
                }
            }
            Color color = AmmoPalette.ColorOf(projectile);
            color.a = energy || frost || projectile.name.StartsWith("ArrowPoison") || projectile.name.StartsWith("ArrowFire") ? GlowOpacity : 0f;
            if (color.a > 0f)
            {
                var glow = new GameObject("AmmoTipAura"); glow.transform.SetParent(holder.transform,false);
                var system = glow.AddComponent<ParticleSystem>();
                Configure(system,radius);
                var main = system.main; main.startColor = color; main.startSpeed = 0f; main.startSize = radius * GlowSizeShare;
                system.GetComponent<ParticleSystemRenderer>().sharedMaterial = AuraMaterial();
            }
            holder.AddComponent<AmmoTipPlayback>().Ammo = projectile.name;
            holder.SetActive(true);
            if (Plugin.LogCounters.Value)
                Plugin.Log.LogInfo($"display tip effects {projectile.name}, local particles {holder.GetComponentsInChildren<ParticleSystem>(true).Length}, radius {radius:F3}, energy {energy}");
        }

        private static Material AuraMaterial()
        {
            if (auraMaterial != null) return auraMaterial;
            var texture = new Texture2D(GlowTextureSize,GlowTextureSize,TextureFormat.RGBA32,false);
            texture.name = "AmmoVariants_tip_glow"; texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[GlowTextureSize * GlowTextureSize];
            for (int y = 0; y < GlowTextureSize; y++)
                for (int x = 0; x < GlowTextureSize; x++)
                {
                    float dx = (x+0.5f)*2f/GlowTextureSize-1f;
                    float dy = (y+0.5f)*2f/GlowTextureSize-1f;
                    float alpha = Mathf.Clamp01(1f-dx*dx-dy*dy); alpha *= alpha;
                    pixels[y*GlowTextureSize+x] = new Color(1f,1f,1f,alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false,true);
            auraMaterial = new Material(AmmoAssets.TrailShader) { name = "AmmoVariants_tip_aura", mainTexture = texture, color = Color.white };
            return auraMaterial;
        }

        private static void Configure(ParticleSystem system, float radius)
        {
            system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = true;
            main.startDelay = 0f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = ParticleLifetime;
            main.startSpeed = radius * 0.3f;
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(radius*0.35f,radius*0.7f);
            main.gravityModifier = 0f;
            main.maxParticles = ParticleLimit;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = ParticleRate;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = system.shape; shape.enabled = false;
            var velocity = system.velocityOverLifetime; velocity.enabled = false;
            var force = system.forceOverLifetime; force.enabled = false;
            var inherit = system.inheritVelocity; inherit.enabled = false;
            var external = system.externalForces; external.enabled = false;
            var subEmitters = system.subEmitters; subEmitters.enabled = false;
            var lights = system.lights; lights.enabled = false;
            var collision = system.collision; collision.enabled = false;
            var trigger = system.trigger; trigger.enabled = false;
            var size = system.sizeOverLifetime; size.enabled = false;
            var sizeBySpeed = system.sizeBySpeed; sizeBySpeed.enabled = false;
            var noise = system.noise;
            if (noise.enabled) { noise.separateAxes = false; noise.strength = radius * 0.35f; }
            var trails = system.trails;
            trails.worldSpace = false;
            trails.lifetime = TrailLifetime;
            trails.dieWithParticles = true;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    public sealed class AmmoTipPlayback : MonoBehaviour
    {
        [SerializeField] internal string Ammo;
        private static readonly System.Collections.Generic.HashSet<string> Logged = new System.Collections.Generic.HashSet<string>();

        private void OnEnable()
        {
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in systems)
                if (system.gameObject.activeInHierarchy) system.Play(false);
            if (Plugin.LogCounters.Value && Logged.Add(Ammo ?? name))
                Plugin.Log.LogInfo($"display tip playback {Ammo}, active systems {systems.Length}");
        }

        private void OnDisable()
        {
            foreach (var system in GetComponentsInChildren<ParticleSystem>(true))
                system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
