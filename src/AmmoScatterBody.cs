using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AmmoVariants
{
    // widens the flying scatter pellet across its flight axis. the prefab, its held and dropped models and its colliders keep their size.
    public sealed class AmmoScatterBody : MonoBehaviour
    {
        private const float ReportDelay = 0.1f;
        private static readonly HashSet<string> Observed = new HashSet<string>();
        private TrailRenderer trail;
        private float age;

        private void Awake()
        {
            trail = transform.Find("AmmoTrail")?.GetComponent<TrailRenderer>();
            foreach (Transform child in transform)
            {
                if (child.name == "AmmoTrail" || child.name == "AmmoDot" || child.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                var colliders = child.GetComponentsInChildren<Collider>(true);
                if (colliders.Any(c => !(c is BoxCollider))) continue;
                var before = colliders.Select(c => c.transform.lossyScale).ToArray();
                Widen(child, AmmoTrailSettings.ScatterThickness);
                for (int i = 0; i < colliders.Length; i++)
                {
                    var box = (BoxCollider)colliders[i];
                    var after = box.transform.lossyScale;
                    var keep = new Vector3(Ratio(before[i].x, after.x), Ratio(before[i].y, after.y), Ratio(before[i].z, after.z));
                    box.size = Vector3.Scale(box.size, keep);
                    box.center = Vector3.Scale(box.center, keep);
                }
            }
        }

        private static float Ratio(float old, float now)
        {
            return Mathf.Abs(now) > 1e-6f ? old / now : 1f;
        }

        // axes that lie across the flight axis take the factor, the one along it keeps its length.
        private static void Widen(Transform child, float factor)
        {
            var multiplier = Vector3.one;
            for (int axis = 0; axis < 3; axis++)
            {
                var direction = child.localRotation * new Vector3(axis == 0 ? 1f : 0f, axis == 1 ? 1f : 0f, axis == 2 ? 1f : 0f);
                if (Mathf.Abs(direction.z) < 0.5f) multiplier[axis] = factor;
            }
            child.localScale = Vector3.Scale(child.localScale, multiplier);
            var position = child.localPosition;
            child.localPosition = new Vector3(position.x * factor, position.y * factor, position.z);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < ReportDelay) return;
            enabled = false;
            if (trail == null || !Plugin.LogCounters.Value || !Observed.Add(name)) return;
            var camera = Camera.main;
            bool layer = camera != null && (camera.cullingMask & (1 << trail.gameObject.layer)) != 0;
            Plugin.Log.LogInfo($"scatter trail flight {name}, points {trail.positionCount}, enabled {trail.enabled}, emitting {trail.emitting}, visible {trail.isVisible}, camera layer {layer}, width {trail.startWidth:F3}, seconds {trail.time:F2}");
        }
    }
}
