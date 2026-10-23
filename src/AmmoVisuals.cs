using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace AmmoVariants
{
    internal static class AmmoVisuals
    {
        internal const float FadeSeconds = 2f;
        internal const float PiercingFadeSeconds = 1f;
        internal const float ImpactFeather = 1f;
        internal const float TrailHoldSeconds = 1f;
        internal const float EnergyHoldSeconds = TrailHoldSeconds;
        internal const float EnergyTrailWidth = 0.055f;
        internal const int MaximumPathPoints = 2048;
        internal const float MinimumDistance = 0.02f;
        private const float LightningWidth = 0.025f;
        private const float LightningLength = 0.65f;
        private const float LightningPerMetre = 2f;
        private const int MaximumParticles = 96;
        private static Material material;
        private static readonly AccessTools.FieldRef<MeleeWeaponTrail, Material> WeaponMaterial =
            AccessTools.FieldRefAccess<MeleeWeaponTrail, Material>("_material");

        internal static Material TrailMaterial()
        {
            if (material != null) return material;
            var weapon = PrefabManager.Instance.GetPrefab("THSwordKrom");
            var source = weapon != null ? weapon.GetComponentInChildren<MeleeWeaponTrail>(true) : null;
            var template = source != null ? WeaponMaterial(source) : null;
            if (template == null)
            {
                Plugin.Log.LogWarning("Krom additive trail material unavailable, using the bundled visible trail material");
                material = new Material(AmmoAssets.TrailShader) { name = "AmmoVariants_glow_fallback", color = Color.white };
                return material;
            }
            material = new Material(template) { name = "AmmoVariants_glow", mainTexture = Texture2D.whiteTexture };
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.6f, 0.58f, 0.55f, 0.5f));
            return material;
        }

        internal static void SuppressInherited(GameObject projectile)
        {
            foreach (var system in projectile.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = system.emission;
                emission.enabled = false;
                system.GetComponent<ParticleSystemRenderer>().enabled = false;
            }
            foreach (var trail in projectile.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.emitting = false;
                trail.enabled = false;
            }
        }

        internal static Gradient Gradient(Color start, Color middle, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.5f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a, 0.2f), new GradientAlphaKey(middle.a, 0.55f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        internal static void AddLightning(GameObject projectile)
        {
            var source = PrefabManager.Instance.GetPrefab("fx_eikthyr_forwardshockwave");
            var lightning = source != null ? source.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "lightning" && p.gameObject.activeSelf) : null;
            if (lightning == null)
            {
                Plugin.Log.LogWarning("Eikthyr lightning particles unavailable on " + projectile.name + ", blue glow trail retained");
                return;
            }
            var copy = Object.Instantiate(lightning.gameObject, projectile.transform, false);
            copy.name = "AmmoLightning";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            copy.transform.localScale = Vector3.one;
            var system = copy.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.duration = EnergyHoldSeconds + FadeSeconds;
            main.startDelay = 0f;
            main.startLifetime = EnergyHoldSeconds + FadeSeconds;
            main.startSpeed = LightningLength / (EnergyHoldSeconds + FadeSeconds);
            main.startSize3D = false;
            main.startSize = LightningWidth;
            main.startColor = Color.white;
            main.maxParticles = MaximumParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.stopAction = ParticleSystemStopAction.None;
            main.playOnAwake = false;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = LightningPerMetre;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = system.shape; shape.enabled = false;
            var velocity = system.velocityOverLifetime; velocity.enabled = false;
            var force = system.forceOverLifetime; force.enabled = false;
            var inherit = system.inheritVelocity; inherit.enabled = false;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = LightningWidth;
            noise.frequency = 8f;
            noise.scrollSpeed = 5f;
            var trails = system.trails;
            trails.enabled = true;
            trails.worldSpace = true;
            trails.minVertexDistance = MinimumDistance;
            trails.lifetime = 1f;
            trails.dieWithParticles = true;
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = Gradient(new Color(0.85f, 0.95f, 1f, 1f), new Color(0.35f, 0.6f, 1f, 0.7f), new Color(0.2f, 0.35f, 1f, 0f));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.None;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
