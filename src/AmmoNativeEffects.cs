using UnityEngine;

namespace AmmoVariants
{
    public sealed class AmmoNativeEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] systems;
        [SerializeField] private bool[] play;
        [SerializeField] private TrailRenderer[] trails;
        [SerializeField] private bool[] emit;
        [SerializeField] private Light[] lights;
        [SerializeField] private bool[] illuminate;
        private bool started;
        private bool stopped;
        private Projectile projectile;

        internal void Prepare(float scale)
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            play = new bool[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                play[i] = main.playOnAwake;
                main.playOnAwake = false;
                if (scale < 1f)
                {
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(main.maxParticles * scale));
                    var emission = systems[i].emission;
                    emission.rateOverTimeMultiplier *= scale;
                    emission.rateOverDistanceMultiplier *= scale;
                }
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            trails = GetComponentsInChildren<TrailRenderer>(true);
            emit = new bool[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                emit[i] = trails[i].enabled && trails[i].emitting;
                trails[i].emitting = false;
                trails[i].enabled = false;
                trails[i].widthMultiplier *= scale;
                trails[i].minVertexDistance *= scale;
            }
            lights = GetComponentsInChildren<Light>(true);
            illuminate = new bool[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                illuminate[i] = lights[i].enabled;
                lights[i].enabled = false;
                lights[i].range *= scale;
                lights[i].intensity *= scale;
            }
            if (Plugin.LogCounters.Value)
                Plugin.Log.LogInfo($"native ammo effects prepared {name}, particles {systems.Length}, trails {trails.Length}, lights {lights.Length}, size {scale:F2}, density {scale:F2}");
        }

        internal void Begin()
        {
            if (started || stopped || systems == null) return;
            projectile = GetComponent<Projectile>();
            if (projectile != null && Shots.FlightEnded(projectile)) return;
            started = true;
            for (int i = 0; i < systems.Length; i++)
                if (play[i] && systems[i].gameObject.activeInHierarchy) systems[i].Play(false);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
                trails[i].enabled = emit[i];
                trails[i].emitting = emit[i];
            }
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = illuminate[i];
            if (Plugin.LogCounters.Value && (systems.Length > 0 || trails.Length > 0))
                Plugin.Log.LogInfo($"native ammo effects launched {name}, particles {systems.Length}, trails {trails.Length}");
        }

        private void Update()
        {
            if (!started)
            {
                var view = GetComponent<ZNetView>();
                if (view != null && view.IsValid()) Begin();
                return;
            }
            if (stopped || projectile == null || !Shots.FlightEnded(projectile)) return;
            stopped = true;
            foreach (var system in systems) system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (var trail in trails) trail.emitting = false;
            foreach (var light in lights) light.enabled = false;
        }
    }
}
