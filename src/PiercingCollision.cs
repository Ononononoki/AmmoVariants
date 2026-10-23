using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmmoVariants
{
    internal static class PiercingCollision
    {
        internal static RaycastHit[] Expand(Vector3 origin, Vector3 direction, float distance, int mask, RaycastHit[] hits,
            float radius, Func<Collider, bool> isEnemy, Func<Collider, bool> isCreature, bool passWorld = false)
        {
            if (radius <= 0f) return hits;
            float blocker = distance;
            var merged = new List<RaycastHit>(hits);
            var seen = new HashSet<Collider>();
            foreach (var hit in hits)
            {
                seen.Add(hit.collider);
                if (!passWorld && !isCreature(hit.collider)) blocker = Mathf.Min(blocker, hit.distance);
            }
            foreach (var hit in Physics.SphereCastAll(origin, radius, direction, distance, mask))
            {
                if (seen.Contains(hit.collider) || !isEnemy(hit.collider) || hit.distance >= blocker) continue;
                var delta = hit.point - origin;
                bool blocked = false;
                if (!passWorld)
                    foreach (var obstacle in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, mask))
                        if (!isCreature(obstacle.collider) && obstacle.distance < delta.magnitude - 0.001f) { blocked = true; break; }
                if (!blocked) { merged.Add(hit); seen.Add(hit.collider); }
            }
            return merged.ToArray();
        }
    }
}
