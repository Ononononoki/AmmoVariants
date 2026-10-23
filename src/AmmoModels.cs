using System.Collections.Generic;
using UnityEngine;

namespace AmmoVariants
{
    internal static class AmmoModels
    {
        internal const int BundleCount = 3;
        private const float BundleSpacing = 0.045f;
        private const float ScatterBodyLength = 0.42f;
        private const float ScatterArrowBodyLength = 1f;
        private const float ScatterBundleSpacing = 0.010f;
        internal const float LoadedScatterLengthShare = 0.5f;
        internal const float LoadedScatterWidthShare = 0.8f;
        private const float HeadLengthShare = 0.08f;
        private const float HeadRadiusShare = 0.014f;
        private static readonly Dictionary<GameObject, float> Lengths = new Dictionary<GameObject, float>();

        internal static Bounds BoundsOf(GameObject root)
        {
            var bounds = new Bounds();
            bool found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name == "AmmoDot" || filter.name == "AmmoTipGlow") continue;
                var box = filter.sharedMesh.bounds;
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        internal static void Decorate(GameObject projectile, string kind, float originalLength)
        {
            Lengths[projectile] = originalLength * (kind == "Piercing" ? Plugin.PierceBodyLength.Value : kind == "Scatter" ? (projectile.name.StartsWith("Arrow") ? ScatterArrowBodyLength : ScatterBodyLength) : 1f);
            var bounds = BoundsOf(projectile);
            float length = Mathf.Max(bounds.size.z, 0.1f);
            var head = new GameObject("AmmoHead"); head.transform.SetParent(projectile.transform, false);
            var mesh = new Mesh { name = "AmmoVariants_head" };
            const int sides = 8;
            var vertices = new Vector3[sides + 2];
            var triangles = new int[sides * 6];
            float rear = bounds.max.z - length * HeadLengthShare;
            vertices[0] = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + length * 0.01f);
            vertices[1] = new Vector3(bounds.center.x, bounds.center.y, rear);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 2] = new Vector3(bounds.center.x + Mathf.Cos(angle) * length * HeadRadiusShare, bounds.center.y + Mathf.Sin(angle) * length * HeadRadiusShare, rear);
                int next = (i + 1) % sides + 2;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 2; triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = 1; triangles[i * 6 + 4] = next; triangles[i * 6 + 5] = i + 2;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            head.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = head.AddComponent<MeshRenderer>();
            var color = AmmoPalette.ColorOf(projectile); color.a = 0.9f;
            renderer.sharedMaterial = new Material(AmmoAssets.TrailShader) { color = color };
        }

        internal static GameObject Visual(GameObject projectile, bool bundle, bool nocked = false)
        {
            var root = new GameObject("AmmoVariantVisual");
            var bounds = BoundsOf(projectile);
            float scale = Lengths.TryGetValue(projectile, out var length) && bounds.size.z > 0f ? length / bounds.size.z : 1f;
            int count = bundle ? BundleCount : 1;
            float spacing = Items.ScatterProjectiles.Contains(projectile) ? ScatterBundleSpacing : BundleSpacing;
            for (int i = 0; i < count; i++)
            {
                var shaft = new GameObject("AmmoShaft"); shaft.transform.SetParent(root.transform, false);
                if (Items.ScatterProjectiles.Contains(projectile) && projectile.name.StartsWith("Arrow")) shaft.transform.localScale = new Vector3(ScatterBodyLength / ScatterArrowBodyLength, ScatterBodyLength / ScatterArrowBodyLength, 1f);
                shaft.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * spacing, 0f, i == 1 ? spacing / 3f : 0f);
                foreach (var source in projectile.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = source.GetComponent<MeshRenderer>();
                    if (source.sharedMesh == null || renderer == null || source.name == "AmmoDot") continue;
                    var leaf = new GameObject(source.name); leaf.transform.SetParent(shaft.transform, false);
                    var matrix = projectile.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                    leaf.transform.localPosition = (new Vector3(matrix.m03, matrix.m13, matrix.m23) - bounds.center) * scale;
                    leaf.transform.localRotation = matrix.rotation;
                    leaf.transform.localScale = matrix.lossyScale * scale;
                    leaf.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    var copy = leaf.AddComponent<MeshRenderer>(); copy.sharedMaterials = renderer.sharedMaterials;
                    copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var tip = new Vector3(bounds.center.x,bounds.center.y,bounds.max.z);
                AmmoDisplayEffects.Add(projectile,shaft.transform,(tip-bounds.center)*scale,length,Items.ScatterProjectiles.Contains(projectile));
            }
            if (nocked) root.transform.localPosition = new Vector3(0f,0f,length * 0.5f);
            return root;
        }

        internal static void DropModel(GameObject item, GameObject projectile)
        {
            item.GetComponent<ItemDrop>().m_itemData.m_dropPrefab = item;
            foreach (var group in item.GetComponentsInChildren<LODGroup>(true)) group.enabled = false;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var system in item.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main; main.playOnAwake = false;
                system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            foreach (var light in item.GetComponentsInChildren<Light>(true)) light.enabled = false;
            var bundle = Visual(projectile, !Items.PiercingProjectiles.Contains(projectile));
            bundle.name = "AmmoBundle";
            bundle.transform.SetParent(item.transform, false);
            bundle.transform.localRotation = Quaternion.identity;
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("variant drop model " + item.name + ", shafts " + bundle.transform.childCount + ", bounds " + BoundsOf(bundle).size.ToString("F3"));
        }
    }
}
