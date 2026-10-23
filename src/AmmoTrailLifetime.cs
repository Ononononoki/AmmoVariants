using System.Collections.Generic;
using UnityEngine;

namespace AmmoVariants
{
    public sealed class AmmoTrailLifetime : MonoBehaviour
    {
        public bool Energy;
        private TrailRenderer trail;
        private ParticleSystem lightning;
        private Projectile projectile;
        private bool started;
        private bool released;
        private float elapsed;
        private AmmoTrailDissolve path;
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static readonly HashSet<string> Observed = new HashSet<string>();

        private void Awake() { trail = transform.Find("AmmoTrail")?.GetComponent<TrailRenderer>(); }

        private void Start()
        {
            var view = GetComponent<ZNetView>();
            if (view != null && view.IsValid()) Begin();
        }

        internal void Begin()
        {
            if (started || released) return;
            started = true;
            projectile = GetComponent<Projectile>();
            if (trail == null) trail = transform.Find("AmmoTrail")?.GetComponent<TrailRenderer>();
            if (Energy && transform.Find("AmmoLightning") == null) AmmoVisuals.AddLightning(gameObject);
            lightning = transform.Find("AmmoLightning")?.GetComponent<ParticleSystem>();
            if (trail == null) { Plugin.Log.LogWarning("ammo trail missing on " + name); return; }
            // an independent visual survives projectile hiding, pooling and destruction.
            trail.transform.SetParent(null, true);
            trail.transform.position = transform.position;
            trail.transform.rotation = transform.rotation;
            trail.Clear();
            trail.AddPosition(transform.position);
            trail.enabled = true;
            trail.emitting = false;
            if (lightning != null)
            {
                lightning.transform.SetParent(trail.transform, true);
                lightning.transform.localPosition = Vector3.zero;
                lightning.Play(false);
            }
            path = trail.gameObject.AddComponent<AmmoTrailDissolve>();
            path.Prepare(trail, lightning, Energy);
            if (Plugin.LogCounters.Value && Logged.Add(name))
                Plugin.Log.LogInfo($"ammo trail started {name}, shader {trail.sharedMaterial.shader.name}, supported {trail.sharedMaterial.shader.isSupported}, segment hold {AmmoVisuals.TrailHoldSeconds:F2}, segment fade {AmmoVisuals.FadeSeconds:F2}, impact fade {(Energy ? AmmoVisuals.FadeSeconds : AmmoVisuals.PiercingFadeSeconds):F2}, lightning {lightning != null}");
        }

        private void LateUpdate()
        {
            if (!started)
            {
                var view = GetComponent<ZNetView>();
                if (view != null && view.IsValid()) Begin();
                return;
            }
            if (released || trail == null) return;
            if (projectile != null && Shots.FlightEnded(projectile)) { Detach(); return; }
            trail.transform.position = transform.position;
            trail.transform.rotation = transform.rotation;
            elapsed += Time.deltaTime;
            trail.time = elapsed + AmmoVisuals.FadeSeconds + CleanupMargin;
            if (trail.positionCount == 0 || Vector3.Distance(trail.GetPosition(trail.positionCount - 1), transform.position) >= AmmoVisuals.MinimumDistance)
                RecordPoint(transform.position);
            if (elapsed >= DiagnosticDelay && Plugin.LogCounters.Value && Observed.Add(name))
            {
                var camera = Camera.main;
                bool layer = camera != null && (camera.cullingMask & (1 << trail.gameObject.layer)) != 0;
                Plugin.Log.LogInfo($"ammo trail flight {name}, points {trail.positionCount}, enabled {path.enabled}, visible {path.Visible()}, camera layer {layer}, particles {(lightning != null ? lightning.particleCount : 0)}");
            }
        }

        internal void Detach()
        {
            var released = DetachTrail();
            if (released != null) Destroy(released.gameObject, (Energy ? AmmoVisuals.EnergyHoldSeconds + AmmoVisuals.FadeSeconds : AmmoVisuals.PiercingFadeSeconds) + CleanupMargin);
        }

        private void RecordPoint(Vector3 point)
        {
            path.RecordPoint(point);
        }

        internal TrailRenderer DetachTrail()
        {
            if (this.released) return null;
            this.released = true;
            if (trail == null) trail = transform.Find("AmmoTrail")?.GetComponent<TrailRenderer>();
            if (trail == null) return null;
            if (started && (trail.positionCount == 0 || Vector3.Distance(trail.GetPosition(trail.positionCount - 1), transform.position) >= AmmoVisuals.MinimumDistance))
                RecordPoint(transform.position);
            trail.emitting = false;
            trail.transform.SetParent(null, true);
            if (lightning != null) lightning.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            trail.autodestruct = false;
            if (path == null)
            {
                path = trail.gameObject.AddComponent<AmmoTrailDissolve>();
                path.Prepare(trail, lightning, Energy);
            }
            path.Release();
            var released = trail;
            trail = null;
            return released;
        }

        private const float CleanupMargin = 0.1f;
        private const float DiagnosticDelay = 0.1f;

        private void OnDisable()
        {
            if (gameObject.scene.isLoaded) Detach();
        }
    }
}
