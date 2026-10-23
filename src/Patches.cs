using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AmmoVariants
{
    [HarmonyPatch(typeof(Attack), "ProjectileAttackTriggered")]
    internal static class AttackProjectileTriggerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Shots.TranspileRelease(instructions);
        }
    }

    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    internal static class AttackFireBurstPatch
    {
        private static void Prefix(Attack __instance)
        {
            Shots.BeginBurst(__instance);
        }

        private static void Postfix()
        {
            Shots.EndBurst();
        }

        private static void Finalizer()
        {
            Shots.EndBurst();
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Shots.TranspileBurst(instructions);
        }
    }

    // runs after every other aim patch so energy shots keep the plain crosshair direction.
    [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
    [HarmonyAfter(AmmoCompatibility.BetterArcheryGuid)]
    [HarmonyPriority(Priority.Last)]
    internal static class AttackSpawnPointPatch
    {
        private static void Postfix(Attack __instance, Vector3 __0, ref Vector3 __1)
        {
            Shots.AimEnergy(__instance, __0, ref __1);
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    [HarmonyAfter(Shots.BsmGuid)]
    [HarmonyPriority(Priority.Last)]
    internal static class ProjectileSetupPatch
    {
        private static void Postfix(Projectile __instance, ItemDrop.ItemData ammo, ItemDrop.ItemData item)
        {
            Shots.OnSetup(__instance, ammo, item);
            AmmoBallistaTrail.Attach(__instance,ammo);
        }
    }

    [HarmonyPatch(typeof(Projectile), "OnHit", new[] { typeof(Collider), typeof(Vector3), typeof(bool), typeof(Vector3) })]
    internal static class ProjectileHitPatch
    {
        private static bool Prefix(Projectile __instance, Collider collider, Vector3 hitPoint, out bool __state)
        {
            __state = Shots.BeforeHit(__instance, collider, hitPoint);
            return __state;
        }

        private static void Postfix(Projectile __instance, Collider collider, bool __state)
        {
            if (__state) Shots.AfterHit(__instance, collider);
        }
    }

    // with UnlockAllRecipes on, the mod's recipes are marked known. off, the game's own discovery decides.
    [HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
    internal static class PlayerKnownRecipesPatch
    {
        private static readonly System.Reflection.MethodInfo AddKnown = AccessTools.Method(typeof(Player), "AddKnownRecipe");

        private static void Postfix(Player __instance)
        {
            if (!Plugin.UnlockAllRecipes.Value) return;
            var db = ObjectDB.instance;
            if (db == null) return;
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null) continue;
                if (!Items.Recipes.Contains(recipe.name)) continue;
                var name = recipe.m_item.m_itemData.m_shared.m_name;
                if (!__instance.IsRecipeKnown(name)) AddKnown.Invoke(__instance, new object[] { recipe });
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "FixedUpdate")]
    internal static class ProjectileUpdatePatch
    {
        private static void Prefix(Projectile __instance)
        {
            Shots.BeforeUpdate(__instance);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Shots.TranspileUpdate(instructions);
        }
    }
}
