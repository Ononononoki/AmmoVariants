using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace AmmoVariants
{
    internal static class AmmoCompatibility
    {
        internal const string BetterArcheryGuid = "ishid4.mods.betterarchery";

        internal static void Init(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.TryGetValue(BetterArcheryGuid, out var info)) return;
            Patch(harmony, info.Instance.GetType().Assembly);
        }

        internal static void Patch(Harmony harmony, Assembly assembly)
        {
            var visual = assembly.GetType("BetterArchery.NockedArrow")?.GetMethod("BuildVisual", BindingFlags.NonPublic | BindingFlags.Static);
            var recovery = assembly.GetType("BetterArchery.RetrieveArrows")?.GetMethod("Prefix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (visual != null) harmony.Patch(visual, prefix: new HarmonyMethod(typeof(AmmoCompatibility), nameof(Nocked)));
            if (recovery != null) harmony.Patch(recovery, prefix: new HarmonyMethod(typeof(AmmoCompatibility), nameof(Recovery)));
            Plugin.Log.LogInfo($"BetterArchery variant compatibility, static held visual {visual != null}, recovery bypass {recovery != null}, aim and draw retained");
        }

        private static bool Nocked(ItemDrop.ItemData __0, ref GameObject __result)
        {
            var kind = Items.KindOf(__0);
            if (kind == Items.AmmoKind.None) return true;
            __result = new GameObject("BA_NockedArrow");
            var model = AmmoModels.Visual(__0.m_shared.m_attack.m_attackProjectile, kind == Items.AmmoKind.Scatter, true);
            model.transform.SetParent(__result.transform, false);
            return false;
        }

        private static bool Recovery(ItemDrop.ItemData ammo, ref bool __result)
        {
            if (Items.KindOf(ammo) == Items.AmmoKind.None) return true;
            __result = true;
            return false;
        }
    }
}
