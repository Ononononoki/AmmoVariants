using System.Collections.Generic;
using UnityEngine;

namespace AmmoVariants
{
    public sealed class AmmoTrailDissolve : MonoBehaviour
    {
        private TrailRenderer trail;
        private LineRenderer glow;
        private LineRenderer edge;
        private LineRenderer lightning;
        private Vector3[] points;
        private Vector3[] sparks;
        private float[] distances;
        private float began = -1f;
        private bool energy;
        private Mesh mesh;
        private Transform meshTransform;
        private MeshRenderer meshRenderer;
        private readonly List<Vector3> samples = new List<Vector3>();
        private readonly List<float> times = new List<float>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> coreTriangles = new List<int>();
        private readonly List<int> sparkTriangles = new List<int>();
        private const float EnergyWidth = AmmoVisuals.EnergyTrailWidth;
        private const float EnergyOpacity = 0.45f;
        private readonly Vector3[] buffer = new Vector3[AmmoVisuals.MaximumPathPoints];
        private const float LightningWidth = 0.025f;
        private const float LightningWobble = 0.035f;
        private readonly float[] lengths = new float[AmmoVisuals.MaximumPathPoints];
        private readonly float[] stamps = new float[AmmoVisuals.MaximumPathPoints];
        private GradientAlphaKey[] alphas;
        private float opacity;
        private const int GoldFadeKeys = 8;

        internal void Prepare(TrailRenderer source, ParticleSystem particles, bool ageEnergy = false)
        {
            if (trail != null) return;
            trail = source;
            energy = ageEnergy;
            int initial = trail.GetPositions(buffer);
            for (int i = 0; i < initial; i++) { samples.Add(buffer[i]); times.Add(Time.time); }
            var gradient = trail.colorGradient;
            opacity = trail.startColor.a;
            gradient.SetKeys(gradient.colorKeys, new[] { new GradientAlphaKey(opacity, 0f), new GradientAlphaKey(opacity * 0.75f, 1f) });
            trail.colorGradient = gradient;
            trail.endWidth = trail.startWidth * 0.5f;
            trail.enabled = false;
            var path = new GameObject("AmmoGlowPath");
            path.transform.SetParent(transform, false);
            glow = path.AddComponent<LineRenderer>();
            glow.useWorldSpace = true;
            glow.alignment = LineAlignment.View;
            glow.startWidth = trail.startWidth;
            glow.endWidth = trail.endWidth;
            glow.sharedMaterial = energy ? trail.sharedMaterial : AmmoPalette.DarkMaterial();
            glow.colorGradient = gradient;
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            var tint = source.startColor;
            if (!energy && Mathf.Max(tint.r,Mathf.Max(tint.g,tint.b)) < AmmoPalette.DarkThreshold)
            {
                glow.sharedMaterial = AmmoPalette.DarkMaterial();
                glow.sortingOrder = 1;
                var border = new GameObject("AmmoMaterialEdge"); border.transform.SetParent(transform,false);
                edge = border.AddComponent<LineRenderer>(); edge.useWorldSpace = true; edge.alignment = LineAlignment.View;
                edge.startWidth = glow.startWidth * AmmoPalette.EdgeWidthShare; edge.endWidth = glow.endWidth * AmmoPalette.EdgeWidthShare;
                edge.sharedMaterial = AmmoPalette.DarkMaterial();
                edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; edge.receiveShadows = false;
            }
            if (energy)
            {
                glow.enabled = false;
                var energyObject = new GameObject("AmmoEnergyPath"); energyObject.transform.SetParent(transform, false);
                meshTransform = energyObject.transform;
                mesh = new Mesh { name = "AmmoVariants_energy_path" }; mesh.MarkDynamic();
                energyObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                meshRenderer = energyObject.AddComponent<MeshRenderer>();
                var particleMaterial = particles != null ? particles.GetComponent<ParticleSystemRenderer>().trailMaterial : null;
                meshRenderer.sharedMaterials = new[] { source.sharedMaterial, particleMaterial != null ? particleMaterial : source.sharedMaterial };
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                return;
            }
            if (particles == null) return;
            var material = particles.GetComponent<ParticleSystemRenderer>().trailMaterial;
            if (material == null) return;
            var go = new GameObject("AmmoLightningPath");
            go.transform.SetParent(transform, false);
            lightning = go.AddComponent<LineRenderer>();
            lightning.useWorldSpace = true;
            lightning.alignment = LineAlignment.View;
            lightning.startWidth = LightningWidth;
            lightning.endWidth = LightningWidth;
            lightning.sharedMaterial = material;
            lightning.colorGradient = gradient;
            lightning.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lightning.receiveShadows = false;
        }

        internal void Release()
        {
            if (began >= 0f || trail == null) return;
            if (!energy) RenderGold();
            began = Time.time;
            int count = energy ? trail.positionCount : glow.positionCount;
            points = new Vector3[count];
            distances = new float[count];
            if (energy) trail.GetPositions(points);
            else { glow.GetPositions(points); alphas = glow.colorGradient.alphaKeys; }
            for (int i = 1; i < count; i++) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            if (lightning != null)
            {
                sparks = new Vector3[count];
                for (int i = 0; i < count; i++) sparks[i] = Spark(points[i], i);
            }
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"ammo path released, points {count}, length {(count > 0 ? distances[count - 1] : 0f):F1}, segment aging true, energy {energy}, fade {(energy ? AmmoVisuals.FadeSeconds : AmmoVisuals.PiercingFadeSeconds):F1}");
        }

        internal void RecordPoint(Vector3 point)
        {
            if (samples.Count >= AmmoVisuals.MaximumPathPoints)
            {
                int count = samples.Count;
                int write = 0;
                for (int i = 0; i < count; i += 2) { samples[write] = samples[i]; times[write] = times[i]; write++; }
                samples[write] = samples[count - 1]; times[write] = times[count - 1]; write++;
                samples.RemoveRange(write, count - write); times.RemoveRange(write, count - write);
                trail.Clear(); foreach (var sample in samples) trail.AddPosition(sample);
            }
            samples.Add(point); times.Add(Time.time); trail.AddPosition(point);
        }

        internal static float EnergyAlpha(float age)
        {
            float t = (float)System.Math.Max(0d, System.Math.Min(1d, (age - AmmoVisuals.TrailHoldSeconds) / AmmoVisuals.FadeSeconds));
            return 1f - t * t * (3f - 2f * t);
        }

        private void RenderGold()
        {
            if (samples.Count < 2) return;
            float cutoff = Time.time - AmmoVisuals.TrailHoldSeconds - AmmoVisuals.FadeSeconds;
            int first = 0;
            while (first < samples.Count - 2 && times[first + 1] <= cutoff) first++;
            int count = samples.Count - first;
            for (int i = 0; i < count; i++) { buffer[i] = samples[first + i]; stamps[i] = times[first + i]; }
            if (cutoff > stamps[0] && stamps[1] > stamps[0])
            {
                buffer[0] = Vector3.Lerp(buffer[0], buffer[1], Mathf.Clamp01((cutoff - stamps[0]) / (stamps[1] - stamps[0])));
                stamps[0] = Mathf.Min(cutoff, stamps[1]);
            }
            lengths[0] = 0f;
            for (int i = 1; i < count; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(buffer[i - 1], buffer[i]);
            var keys = new GradientAlphaKey[GoldFadeKeys];
            int point = 1;
            for (int i = 0; i < keys.Length; i++)
            {
                float fraction = (float)i / (keys.Length - 1);
                float distance = fraction * lengths[count - 1];
                while (point < count - 1 && lengths[point] < distance) point++;
                float span = lengths[point] - lengths[point - 1];
                float stamp = Mathf.Lerp(stamps[point - 1], stamps[point], span > 0f ? (distance - lengths[point - 1]) / span : 1f);
                keys[i] = new GradientAlphaKey(opacity * EnergyAlpha(Time.time - stamp) * Mathf.Lerp(1f,0.75f,fraction), fraction);
            }
            var gradient = glow.colorGradient; gradient.SetKeys(trail.colorGradient.colorKeys, keys); glow.colorGradient = gradient;
            glow.positionCount = count;
            glow.SetPositions(buffer);
            RenderEdge(buffer);
        }

        private void RenderEdge(Vector3[] positions)
        {
            if (edge == null) return;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(AmmoPalette.EdgeColor,0f),new GradientColorKey(AmmoPalette.EdgeColor,1f) },
                System.Array.ConvertAll(glow.colorGradient.alphaKeys,key=>new GradientAlphaKey(key.alpha * AmmoPalette.EdgeOpacity,key.time)));
            edge.colorGradient = gradient; edge.positionCount = glow.positionCount; edge.SetPositions(positions);
        }

        private void RenderEnergy()
        {
            if (samples.Count < 2) return;
            vertices.Clear(); colors.Clear(); coreTriangles.Clear(); sparkTriangles.Clear();
            var camera = Camera.main;
            var inverse = meshTransform.worldToLocalMatrix;
            for (int i = 0; i < samples.Count; i++)
            {
                var direction = samples[System.Math.Min(i + 1, samples.Count - 1)] - samples[System.Math.Max(i - 1, 0)];
                var facing = camera != null ? camera.transform.position - samples[i] : Vector3.forward;
                var side = Vector3.Cross(direction, facing).normalized;
                if (side.sqrMagnitude < 0.001f) side = Vector3.up;
                float alpha = EnergyAlpha(Time.time - times[i]);
                for (int ribbon = 0; ribbon < 2; ribbon++)
                {
                    var point = ribbon == 0 ? samples[i] : Spark(samples[i], i);
                    float half = (ribbon == 0 ? EnergyWidth : LightningWidth) * 0.5f;
                    var color = ribbon == 0 ? new Color(0.35f,0.65f,1f,alpha * EnergyOpacity) : new Color(0.45f,0.75f,1f,alpha * 0.75f);
                    for (int row = 0; row < 3; row++)
                    {
                        vertices.Add(inverse.MultiplyPoint3x4(point + side * ((row - 1) * half)));
                        var c = color; if (row != 1) c.a = 0f; colors.Add(c);
                    }
                    if (i > 0)
                    {
                        int a = (i - 1) * 6 + ribbon * 3;
                        var triangles = ribbon == 0 ? coreTriangles : sparkTriangles;
                        for (int row = 0; row < 2; row++)
                        {
                            triangles.Add(a + row); triangles.Add(a + row + 6); triangles.Add(a + row + 1);
                            triangles.Add(a + row + 1); triangles.Add(a + row + 6); triangles.Add(a + row + 7);
                        }
                    }
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.subMeshCount = 2;
            mesh.SetTriangles(coreTriangles, 0); mesh.SetTriangles(sparkTriangles, 1); mesh.RecalculateBounds();
        }

        private static Vector3 Spark(Vector3 point, int index)
        {
            float offset = (Mathf.Sin(index * 2.39f) + Mathf.Sin(index * 5.71f) * 0.5f) * LightningWobble;
            return point + Vector3.up * offset;
        }

        // the path keeps its shape and impact point, opacity falls from the old end behind a soft front.
        internal void Dissolve(float fraction)
        {
            if (points == null || points.Length < 2 || alphas == null) return;
            float front = Mathf.Clamp01(fraction) * (1f + AmmoVisuals.ImpactFeather);
            var keys = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                float progress = Mathf.Clamp01((front - alphas[i].time) / AmmoVisuals.ImpactFeather);
                keys[i] = new GradientAlphaKey(alphas[i].alpha * (1f - progress * progress * (3f - 2f * progress)), alphas[i].time);
            }
            var gradient = glow.colorGradient; gradient.SetKeys(gradient.colorKeys,keys); glow.colorGradient = gradient;
            if (lightning != null) { lightning.positionCount = sparks.Length; lightning.SetPositions(sparks); lightning.colorGradient = gradient; }
            RenderEdge(points);
        }

        private void LateUpdate()
        {
            if (trail == null) return;
            if (energy)
            {
                RenderEnergy();
                if (began >= 0f && times.Count > 0 && Time.time - times[times.Count - 1] >= AmmoVisuals.EnergyHoldSeconds + AmmoVisuals.FadeSeconds) Destroy(gameObject);
                return;
            }
            if (began >= 0f)
            {
                float fraction = (Time.time - began) / AmmoVisuals.PiercingFadeSeconds;
                Dissolve(fraction);
                if (fraction >= 1f) Destroy(gameObject);
            }
            else
            {
                RenderGold();
            }
        }

        internal bool Visible() { return energy ? meshRenderer != null && meshRenderer.isVisible : glow != null && glow.isVisible; }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
