using HarmonyLib;
using UnityEngine;

namespace AmmoVariants
{
    public sealed class AmmoLoadedVisual : MonoBehaviour
    {
        private GameObject visual;
        private GameObject prefab;
        private Renderer original;
        private bool originalEnabled;

        internal void Refresh(WeaponLoadState state, ItemDrop.ItemData ammo)
        {
            var kind = Items.KindOf(ammo);
            if (kind == Items.AmmoKind.None)
            {
                if (visual != null) { visual.SetActive(false); Destroy(visual); }
                if (original != null) original.enabled = originalEnabled;
                prefab = null;
                return;
            }
            var source = ammo.m_shared.m_attack.m_attackProjectile;
            if (source == prefab && visual != null) return;
            if (visual != null) { visual.SetActive(false); Destroy(visual); }
            prefab = source;
            visual = AmmoModels.Visual(source, kind == Items.AmmoKind.Scatter);
            visual.name = "AmmoLoadedVariant";
            visual.transform.SetParent(state.m_loaded.transform, false);
            Position(state.m_loaded.transform, visual.transform);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("loaded crossbow variant model " + source.name);
        }

        private void Position(Transform loaded, Transform model)
        {
            MeshFilter anchor = null;
            float best = 0f;
            foreach (var filter in loaded.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.transform == loaded || filter.transform.IsChildOf(model) || filter.sharedMesh == null) continue;
                bool variant = false;
                for (var parent = filter.transform; parent != null && parent != loaded; parent = parent.parent)
                    if (parent.name == "AmmoLoadedVariant") { variant = true; break; }
                if (variant) continue;
                var size = filter.sharedMesh.bounds.size;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float volume = Mathf.Max(0.0001f, size.x * size.y * size.z);
                float score = longest * longest * longest / volume;
                if (score > best) { best = score; anchor = filter; }
            }
            if (anchor == null)
            {
                model.localPosition = Vector3.zero;
                Plugin.Log.LogWarning("crossbow loaded ammo anchor unavailable, using loaded origin");
                return;
            }
            if (original == null) { original = anchor.GetComponent<Renderer>(); if (original != null) originalEnabled = original.enabled; }
            if (original != null) original.enabled = false;
            var bounds = anchor.sharedMesh.bounds;
            var size3 = bounds.size;
            var axis = size3.y >= size3.x && size3.y >= size3.z ? Vector3.up : size3.z >= size3.x ? Vector3.forward : Vector3.right;
            float length = Mathf.Max(size3.x, Mathf.Max(size3.y, size3.z));
            var rearWorld = anchor.transform.TransformPoint(bounds.center - axis * length * 0.5f);
            var frontWorld = anchor.transform.TransformPoint(bounds.center + axis * length * 0.5f);
            var rear = loaded.InverseTransformPoint(rearWorld);
            var front = loaded.InverseTransformPoint(frontWorld);
            var direction = (front - rear).normalized;
            var modelBounds = AmmoModels.BoundsOf(model.gameObject);
            float ratio = modelBounds.size.z > 0f ? Vector3.Distance(front, rear) / modelBounds.size.z : 1f;
            if (Items.ScatterProjectiles.Contains(prefab))
            {
                ratio *= AmmoModels.LoadedScatterLengthShare;
                float width = 0f;
                for (int i = 0; i < 3; i++)
                {
                    if (axis[i] != 0f) continue;
                    var span = Vector3.zero; span[i] = size3[i];
                    width = Mathf.Max(width, loaded.InverseTransformVector(anchor.transform.TransformVector(span)).magnitude);
                }
                float transverse = Mathf.Max(modelBounds.size.x, modelBounds.size.y);
                if (width > 0f && transverse > 0f) ratio = Mathf.Min(ratio, width * AmmoModels.LoadedScatterWidthShare / transverse);
            }
            model.localScale = Vector3.one * ratio;
            model.localRotation = Quaternion.LookRotation(direction);
            model.localPosition = (front + rear) * 0.5f;
            if (Items.ScatterProjectiles.Contains(prefab)) model.localPosition = rear + direction * (modelBounds.size.z * ratio * 0.5f);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"loaded ammo footprint {prefab.name}, shafts {model.childCount}, length {modelBounds.size.z * ratio:F3}, width {Mathf.Max(modelBounds.size.x, modelBounds.size.y) * ratio:F3}");
        }
    }

    [HarmonyPatch(typeof(WeaponLoadState), "Update")]
    internal static class AmmoWeaponLoadPatch
    {
        private static readonly AccessTools.FieldRef<WeaponLoadState, Player> Owner = AccessTools.FieldRefAccess<WeaponLoadState, Player>("m_owner");

        private static void Postfix(WeaponLoadState __instance)
        {
            var player = Owner(__instance);
            if (player == null || __instance.m_loaded == null) return;
            var ammo = player.GetAmmoItem();
            var component = __instance.GetComponent<AmmoLoadedVisual>();
            if (component == null && Items.KindOf(ammo) == Items.AmmoKind.None) return;
            if (component == null) component = __instance.gameObject.AddComponent<AmmoLoadedVisual>();
            component.Refresh(__instance, ammo);
        }
    }
}
