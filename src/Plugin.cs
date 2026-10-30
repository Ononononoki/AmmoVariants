using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using System.Reflection;
using UnityEngine;

namespace AmmoVariants
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.example.bowsecondarymultishot", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Searica.Valheim.ProjectileTweaks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(AmmoCompatibility.BetterArcheryGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.onononoki.ammovariants";
        public const string ModName = "AmmoVariants";
        public const string ModVersion = "1.1.0";

        internal static Plugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<float> ScatterShareBow;
        internal static ConfigEntry<float> ScatterShareCrossbow;
        internal static ConfigEntry<int> ScatterPellets;
        internal static ConfigEntry<int> ScatterBowPellets;
        internal static ConfigEntry<float> ScatterBowConeHalfAngle;
        internal static ConfigEntry<float> ScatterConeHalfAngle;
        internal static ConfigEntry<float> ScatterPelletScale;
        internal static ConfigEntry<int> PierceMaxCreatures;
        internal static ConfigEntry<float> PierceDamageShare;
        internal static ConfigEntry<float> PierceLossPerCreature;
        internal static ConfigEntry<float> EnergyRange;
        internal static ConfigEntry<float> EnergyDamageAtRange;
        internal static ConfigEntry<float> EnergyLightningShare;
        internal static ConfigEntry<float> EnergyPierceShare;
        internal static ConfigEntry<float> EnergyFireShare;
        internal static ConfigEntry<int> RecipeScatterBase;
        internal static ConfigEntry<int> RecipeScatterYield;
        internal static ConfigEntry<int> RecipePierceYield;
        internal static ConfigEntry<float> PierceBodyLength;
        internal static ConfigEntry<float> PierceEnemyRadius;
        internal static ConfigEntry<float> FiringSoundVolume;
        internal static ConfigEntry<float> NativeReleaseVolume;
        internal static ConfigEntry<float> EnergyEnemyRadius;
        internal static ConfigEntry<int> RecipeEnergyEitr;
        internal static ConfigEntry<int> RecipeEnergyBase;
        internal static ConfigEntry<int> RecipeEnergyYield;
        internal static ConfigEntry<Color> EnergyColor;
        internal static ConfigEntry<float> ExplosiveImpactPierceShare;
        internal static ConfigEntry<float> ExplosiveImpactFireShare;
        internal static ConfigEntry<float> ExplosiveImpactToolShare;
        internal static ConfigEntry<float> ExplosiveBlastBluntShare;
        internal static ConfigEntry<float> ExplosiveBlastFireShare;
        internal static ConfigEntry<float> ExplosiveBlastChopShare;
        internal static ConfigEntry<float> ExplosiveBlastPickaxeShare;
        internal static ConfigEntry<float> ExplosiveFuse;
        internal static ConfigEntry<float> ExplosiveArrowCoreRadius;
        internal static ConfigEntry<float> ExplosiveArrowRadius;
        internal static ConfigEntry<float> ExplosiveBoltCoreRadius;
        internal static ConfigEntry<float> ExplosiveBoltRadius;
        internal static ConfigEntry<float> ExplosiveAftershockShare;
        internal static ConfigEntry<float> ExplosiveArrowPush;
        internal static ConfigEntry<float> ExplosiveBoltPush;
        internal static ConfigEntry<int> ExplosiveAxeTier;
        internal static ConfigEntry<int> ExplosivePickaxeTier;
        internal static ConfigEntry<float> ExplosiveArrowSpeedFactor;
        internal static ConfigEntry<float> ExplosiveBoltSpeedFactor;
        internal static ConfigEntry<float> ExplosiveArrowGravity;
        internal static ConfigEntry<float> ExplosiveBoltGravity;
        internal static ConfigEntry<bool> ExplosiveBaseProtection;
        internal static ConfigEntry<int> RecipeExplosiveBase;
        internal static ConfigEntry<int> RecipeExplosiveSulfur;
        internal static ConfigEntry<int> RecipeExplosiveCoal;
        internal static ConfigEntry<int> RecipeExplosiveYield;
        internal static ConfigEntry<float> ExplosiveEarlyArrowCoreRadius;
        internal static ConfigEntry<float> ExplosiveEarlyArrowRadius;
        internal static ConfigEntry<float> ExplosiveEarlyBoltCoreRadius;
        internal static ConfigEntry<float> ExplosiveEarlyBoltRadius;
        internal static ConfigEntry<int> ExplosiveEarlyAxeTier;
        internal static ConfigEntry<int> ExplosiveEarlyPickaxeTier;
        internal static ConfigEntry<float> ExplosiveEarlyArrowSpeedFactor;
        internal static ConfigEntry<int> RecipeEarlyExplosiveBase;
        internal static ConfigEntry<int> RecipeEarlyExplosiveCore;
        internal static ConfigEntry<int> RecipeEarlyExplosiveCoal;
        internal static ConfigEntry<int> RecipeEarlyExplosiveGuck;
        internal static ConfigEntry<int> RecipeEarlyExplosiveYield;
        internal static ConfigEntry<float> ExplosivePlainsArrowCoreRadius;
        internal static ConfigEntry<float> ExplosivePlainsArrowRadius;
        internal static ConfigEntry<float> ExplosivePlainsBoltCoreRadius;
        internal static ConfigEntry<float> ExplosivePlainsBoltRadius;
        internal static ConfigEntry<int> ExplosivePlainsAxeTier;
        internal static ConfigEntry<int> ExplosivePlainsPickaxeTier;
        internal static ConfigEntry<float> ExplosivePlainsArrowSpeedFactor;
        internal static ConfigEntry<int> RecipePlainsExplosiveBase;
        internal static ConfigEntry<int> RecipePlainsExplosiveBlackMetal;
        internal static ConfigEntry<int> RecipePlainsExplosiveCore;
        internal static ConfigEntry<int> RecipePlainsExplosiveCoal;
        internal static ConfigEntry<int> RecipePlainsExplosiveYield;
        internal static ConfigEntry<bool> UnlockAllRecipes;
        internal static ConfigEntry<bool> LogCounters;

        private void Awake()
        {
            bool migrate = System.IO.File.Exists(Config.ConfigFilePath) && System.IO.File.ReadAllText(Config.ConfigFilePath).Contains("v0.1.0");
            Instance = this;
            Log = Logger;

            var sync = new ConfigurationManagerAttributes { IsAdminOnly = true };

            ScatterShareBow = Config.Bind("Scatter", "BowDamageShare", 0.30f,
                new ConfigDescription("Damage share of each pellet relative to a normal bow shot.",
                    new AcceptableValueRange<float>(0.01f, 1f), sync));
            ScatterShareCrossbow = Config.Bind("Scatter", "CrossbowDamageShare", 0.1142857f,
                new ConfigDescription("Damage share of each pellet relative to a normal crossbow bolt.",
                    new AcceptableValueRange<float>(0.01f, 1f), sync));
            ScatterPellets = Config.Bind("Scatter", "Pellets", 14,
                new ConfigDescription("Pellets per scatter crossbow shot.",
                    new AcceptableValueRange<int>(2, 32), sync));
            ScatterBowPellets = Config.Bind("Scatter", "BowPellets", 5,
                new ConfigDescription("Projectiles per scatter bow shot.", new AcceptableValueRange<int>(2, 32), sync));
            ScatterBowConeHalfAngle = Config.Bind("Scatter", "BowConeHalfAngle", 4f,
                new ConfigDescription("Half angle of the bow scatter cone in degrees.", new AcceptableValueRange<float>(0.5f, 30f), sync));
            ScatterConeHalfAngle = Config.Bind("Scatter", "ConeHalfAngle", 6f,
                new ConfigDescription("Half angle of the crossbow pellet cone in degrees.",
                    new AcceptableValueRange<float>(0.5f, 30f), sync));
            ScatterPelletScale = Config.Bind("Scatter", "PelletScale", 0.3f,
                new ConfigDescription("Scale of the pellet mesh relative to the vanilla bolt.",
                    new AcceptableValueRange<float>(0.05f, 1f), sync));

            PierceMaxCreatures = Config.Bind("Piercing", "MaxCreatures", 4,
                new ConfigDescription("Creatures one piercing shot can pass.",
                    new AcceptableValueRange<int>(1, 50), sync));
            PierceDamageShare = Config.Bind("Piercing", "DamageShare", 1.10f,
                new ConfigDescription("Initial combat damage relative to the complete normal weapon and ammo shot.",
                    new AcceptableValueRange<float>(0.01f, 2f), sync));
            PierceLossPerCreature = Config.Bind("Piercing", "LossPerCreature", 0.15f,
                new ConfigDescription("Damage lost after each creature passed, as a fraction of the damage.",
                    new AcceptableValueRange<float>(0f, 0.9f), sync));
            PierceBodyLength = Config.Bind("Visual", "PiercingBodyLength", 1.25f,
                new ConfigDescription("Visible piercing arrow and bolt length multiplier. Does not change collision range.", new AcceptableValueRange<float>(1f, 2f)));
            PierceEnemyRadius = Config.Bind("Piercing", "EnemyRadius", 0.35f,
                new ConfigDescription("Extra collision radius in metres against enemies only. World collision keeps the original thin ray.", new AcceptableValueRange<float>(0f, 0.5f), sync));
            FiringSoundVolume = Config.Bind("Audio", "FiringSoundVolume", 1f,
                new ConfigDescription("Additional ammo firing sound volume. Original weapon sounds remain. Uses the game SFX mixer.", new AcceptableValueRange<float>(0f, 2f)));
            NativeReleaseVolume = Config.Bind("Audio", "NativeReleaseVolume", 0.30f,
                new ConfigDescription("Original bow and crossbow release volume for variant ammunition. Ordinary ammunition is unchanged. Requires restart.", new AcceptableValueRange<float>(0f, 1f)));
            EnergyEnemyRadius = Config.Bind("Energy", "EnemyRadius", 0.35f,
                new ConfigDescription("Extra collision radius in metres against enemies only. Energy still passes through world geometry.", new AcceptableValueRange<float>(0f, 0.5f), sync));

            EnergyRange = Config.Bind("Energy", "Range", 200f,
                new ConfigDescription("Distance in metres at which energy ammo ends.",
                    new AcceptableValueRange<float>(10f, 1000f), sync));
            EnergyDamageAtRange = Config.Bind("Energy", "DamageAtRange", 0.25f,
                new ConfigDescription("Damage share at the end range (linear from 100 percent at 0 m).",
                    new AcceptableValueRange<float>(0f, 1f), sync));
            EnergyLightningShare = Config.Bind("Energy", "LightningShare", 0.85f,
                new ConfigDescription("Lightning damage of an energy shot as a share of the complete normal weapon and ammo shot. Item texts change after a restart.",
                    new AcceptableValueRange<float>(0f, 2f), sync));
            EnergyPierceShare = Config.Bind("Energy", "PierceShare", 0.15f,
                new ConfigDescription("Pierce damage of an energy shot as a share of the complete normal weapon and ammo shot. Item texts change after a restart.",
                    new AcceptableValueRange<float>(0f, 2f), sync));
            EnergyFireShare = Config.Bind("Energy", "FireShare", 0.15f,
                new ConfigDescription("Fire damage added to an energy shot as a share of the complete normal weapon and ammo shot. Fire sets targets burning. Item texts change after a restart.",
                    new AcceptableValueRange<float>(0f, 2f), sync));

            RecipeScatterBase = Config.Bind("Recipes", "ScatterBaseAmount", 20,
                new ConfigDescription("Base ammo used per scatter or piercing craft. Station and level follow the original ammo recipe.",
                    new AcceptableValueRange<int>(1, 100), sync));
            RecipeScatterYield = Config.Bind("Recipes", "ScatterYield", 10,
                new ConfigDescription("Items made per scatter craft.",
                    new AcceptableValueRange<int>(1, 100), sync));
            RecipePierceYield = Config.Bind("Recipes", "PiercingYield", 20,
                new ConfigDescription("Items made per piercing craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeEnergyEitr = Config.Bind("Recipes", "EnergyEitrAmount", 5,
                new ConfigDescription("Eitr used per energy craft (Black Forge).",
                    new AcceptableValueRange<int>(1, 100), sync));
            RecipeEnergyBase = Config.Bind("Recipes", "EnergyBaseAmount", 20,
                new ConfigDescription("Base ammo used per energy craft (Black Forge).",
                    new AcceptableValueRange<int>(1, 100), sync));
            RecipeEnergyYield = Config.Bind("Recipes", "EnergyYield", 10,
                new ConfigDescription("Items made per energy craft.",
                    new AcceptableValueRange<int>(1, 100), sync));
            UnlockAllRecipes = Config.Bind("Recipes", "UnlockAllRecipes", false,
                "Marks every recipe of this mod known at once. Off, the recipes open from the ingredients in the character's history.");
            EnergyColor = Config.Bind("Energy", "Color", new Color(0.78f, 0.92f, 1f, 0.55f),
                new ConfigDescription("Tint of the energy bolt and arrow, pale blue-white and translucent.", null, sync));

            ExplosiveImpactPierceShare = Config.Bind("Explosive", "ImpactPierceShare", 0.20f,
                new ConfigDescription("Pierce damage on impact as a share of the shot's combat damage as fired.", new AcceptableValueRange<float>(0f, 2f), sync));
            ExplosiveImpactFireShare = Config.Bind("Explosive", "ImpactFireShare", 0.10f,
                new ConfigDescription("Fire damage on impact as a share of the shot's combat damage as fired.", new AcceptableValueRange<float>(0f, 2f), sync));
            ExplosiveImpactToolShare = Config.Bind("Explosive", "ImpactToolShare", 0.30f,
                new ConfigDescription("Share of the shot's own chop and pickaxe damage kept on impact.", new AcceptableValueRange<float>(0f, 2f), sync));
            ExplosiveBlastBluntShare = Config.Bind("Explosive", "BlastBluntShare", 0.9375f,
                new ConfigDescription("Blunt damage of the blast as a share of a full draw shot's combat damage, whatever the draw. 0.9375 is 75 percent of a 125 percent blast.", new AcceptableValueRange<float>(0f, 3f), sync));
            ExplosiveBlastFireShare = Config.Bind("Explosive", "BlastFireShare", 0.3125f,
                new ConfigDescription("Fire damage of the blast as a share of a full draw shot's combat damage. 0.3125 is 25 percent of a 125 percent blast.", new AcceptableValueRange<float>(0f, 3f), sync));
            ExplosiveBlastChopShare = Config.Bind("Explosive", "BlastChopShare", 1.25f,
                new ConfigDescription("Chop damage of the blast as a share of a full draw shot's combat damage.", new AcceptableValueRange<float>(0f, 3f), sync));
            ExplosiveBlastPickaxeShare = Config.Bind("Explosive", "BlastPickaxeShare", 1.25f,
                new ConfigDescription("Pickaxe damage of the blast as a share of a full draw shot's combat damage.", new AcceptableValueRange<float>(0f, 3f), sync));
            ExplosiveFuse = Config.Bind("Explosive", "FuseSeconds", 1.25f,
                new ConfigDescription("Seconds from the hit to the blast.", new AcceptableValueRange<float>(0.1f, 5f), sync));
            ExplosiveArrowCoreRadius = Config.Bind("Explosive", "ArrowCoreRadius", 2.5f,
                new ConfigDescription("Full damage radius of an arrow blast in metres.", new AcceptableValueRange<float>(0.5f, 10f), sync));
            ExplosiveArrowRadius = Config.Bind("Explosive", "ArrowRadius", 4f,
                new ConfigDescription("Outer radius of an arrow blast in metres, the aftershock ring lies between the core and this.", new AcceptableValueRange<float>(0.5f, 12f), sync));
            ExplosiveBoltCoreRadius = Config.Bind("Explosive", "BoltCoreRadius", 3f,
                new ConfigDescription("Full damage radius of a bolt blast in metres.", new AcceptableValueRange<float>(0.5f, 10f), sync));
            ExplosiveBoltRadius = Config.Bind("Explosive", "BoltRadius", 5f,
                new ConfigDescription("Outer radius of a bolt blast in metres.", new AcceptableValueRange<float>(0.5f, 12f), sync));
            ExplosiveAftershockShare = Config.Bind("Explosive", "AftershockShare", 0.40f,
                new ConfigDescription("Damage share in the aftershock ring.", new AcceptableValueRange<float>(0f, 1f), sync));
            ExplosiveArrowPush = Config.Bind("Explosive", "ArrowPush", 20f,
                new ConfigDescription("Push force of an arrow blast.", new AcceptableValueRange<float>(0f, 200f), sync));
            ExplosiveBoltPush = Config.Bind("Explosive", "BoltPush", 30f,
                new ConfigDescription("Push force of a bolt blast.", new AcceptableValueRange<float>(0f, 200f), sync));
            ExplosiveAxeTier = Config.Bind("Explosive", "AxeTier", 4,
                new ConfigDescription("Tool tier of the blast against trees and logs. 4 is the black metal axe, it fells Yggdrasil shoots.", new AcceptableValueRange<int>(0, 10), sync));
            ExplosivePickaxeTier = Config.Bind("Explosive", "PickaxeTier", 3,
                new ConfigDescription("Tool tier of the blast against rocks, ore and everything else. 3 is the black metal pickaxe.", new AcceptableValueRange<int>(0, 10), sync));
            ExplosiveArrowSpeedFactor = Config.Bind("Explosive", "ArrowSpeedFactor", 0.85f,
                new ConfigDescription("Explosive arrow speed as a share of the speed every other mod and the game gave the shot.", new AcceptableValueRange<float>(0.1f, 1f), sync));
            ExplosiveBoltSpeedFactor = Config.Bind("Explosive", "BoltSpeedFactor", 0.5f,
                new ConfigDescription("Explosive bolt speed as a share of the speed every other mod and the game gave the shot.", new AcceptableValueRange<float>(0.1f, 1f), sync));
            ExplosiveArrowGravity = Config.Bind("Explosive", "ArrowGravity", 5f,
                new ConfigDescription("Gravity of an explosive arrow (the charred arrow has 5).", new AcceptableValueRange<float>(0f, 50f), sync));
            ExplosiveBoltGravity = Config.Bind("Explosive", "BoltGravity", 13.9f,
                new ConfigDescription("Gravity of an explosive bolt. 13.9 at half speed drops like a full drawn arrow.", new AcceptableValueRange<float>(0f, 50f), sync));
            ExplosiveBaseProtection = Config.Bind("Explosive", "BaseProtection", true,
                new ConfigDescription("A blast inside a workbench's build range, measured from where it blows, does not damage buildings, trees, rocks or ore. Creatures are still hit.", null, sync));
            RecipeExplosiveBase = Config.Bind("Recipes", "ExplosiveBaseAmount", 20,
                new ConfigDescription("Charred arrows or bolts used per explosive craft (Black Forge).", new AcceptableValueRange<int>(1, 100), sync));
            RecipeExplosiveSulfur = Config.Bind("Recipes", "ExplosiveSulfurAmount", 3,
                new ConfigDescription("Sulfur used per explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeExplosiveCoal = Config.Bind("Recipes", "ExplosiveCoalAmount", 2,
                new ConfigDescription("Coal used per explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeExplosiveYield = Config.Bind("Recipes", "ExplosiveYield", 5,
                new ConfigDescription("Items made per explosive craft.", new AcceptableValueRange<int>(1, 100), sync));

            ExplosiveEarlyArrowCoreRadius = Config.Bind("Explosive Early", "ArrowCoreRadius", 2f,
                new ConfigDescription("Full damage radius of an early (fire arrow) blast in metres.", new AcceptableValueRange<float>(0.5f, 10f), sync));
            ExplosiveEarlyArrowRadius = Config.Bind("Explosive Early", "ArrowRadius", 3f,
                new ConfigDescription("Outer radius of an early arrow blast in metres.", new AcceptableValueRange<float>(0.5f, 12f), sync));
            ExplosiveEarlyBoltCoreRadius = Config.Bind("Explosive Early", "BoltCoreRadius", 2.5f,
                new ConfigDescription("Full damage radius of an early (bone bolt) blast in metres.", new AcceptableValueRange<float>(0.5f, 10f), sync));
            ExplosiveEarlyBoltRadius = Config.Bind("Explosive Early", "BoltRadius", 4f,
                new ConfigDescription("Outer radius of an early bolt blast in metres.", new AcceptableValueRange<float>(0.5f, 12f), sync));
            ExplosiveEarlyAxeTier = Config.Bind("Explosive Early", "AxeTier", 2,
                new ConfigDescription("Tool tier of the early blast against trees and logs. 2 is the bronze axe.", new AcceptableValueRange<int>(0, 10), sync));
            ExplosiveEarlyPickaxeTier = Config.Bind("Explosive Early", "PickaxeTier", 1,
                new ConfigDescription("Tool tier of the early blast against rocks, ore and everything else. 1 is the bronze pickaxe.", new AcceptableValueRange<int>(0, 10), sync));
            ExplosiveEarlyArrowSpeedFactor = Config.Bind("Explosive Early", "ArrowSpeedFactor", 0.9f,
                new ConfigDescription("Early explosive arrow speed as a share of the speed every other mod and the game gave the shot. Early bolts use the Explosive bolt speed and gravity.", new AcceptableValueRange<float>(0.1f, 1f), sync));
            RecipeEarlyExplosiveBase = Config.Bind("Recipes", "EarlyExplosiveBaseAmount", 20,
                new ConfigDescription("Fire arrows or bone bolts used per early explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeEarlyExplosiveCore = Config.Bind("Recipes", "EarlyExplosiveSurtlingCoreAmount", 1,
                new ConfigDescription("Surtling cores used per early explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeEarlyExplosiveCoal = Config.Bind("Recipes", "EarlyExplosiveCoalAmount", 2,
                new ConfigDescription("Coal used per early explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeEarlyExplosiveGuck = Config.Bind("Recipes", "EarlyExplosiveGuckAmount", 1,
                new ConfigDescription("Guck used per early explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipeEarlyExplosiveYield = Config.Bind("Recipes", "EarlyExplosiveYield", 5,
                new ConfigDescription("Items made per early explosive craft.", new AcceptableValueRange<int>(1, 100), sync));

            ExplosivePlainsArrowCoreRadius = Config.Bind("Explosive Plains", "ArrowCoreRadius", 2.25f,
                new ConfigDescription("Full damage radius of a plains (needle arrow) blast in metres.", new AcceptableValueRange<float>(0.5f, 10f), sync));
            ExplosivePlainsArrowRadius = Config.Bind("Explosive Plains", "ArrowRadius", 3.5f,
                new ConfigDescription("Outer radius of a plains arrow blast in metres.", new AcceptableValueRange<float>(0.5f, 12f), sync));
            ExplosivePlainsBoltCoreRadius = Config.Bind("Explosive Plains", "BoltCoreRadius", 2.75f,
                new ConfigDescription("Full damage radius of a plains (black metal bolt) blast in metres.", new AcceptableValueRange<float>(0.5f, 10f), sync));
            ExplosivePlainsBoltRadius = Config.Bind("Explosive Plains", "BoltRadius", 4.5f,
                new ConfigDescription("Outer radius of a plains bolt blast in metres.", new AcceptableValueRange<float>(0.5f, 12f), sync));
            ExplosivePlainsAxeTier = Config.Bind("Explosive Plains", "AxeTier", 3,
                new ConfigDescription("Tool tier of the plains blast against trees and logs. 3 is the iron axe.", new AcceptableValueRange<int>(0, 10), sync));
            ExplosivePlainsPickaxeTier = Config.Bind("Explosive Plains", "PickaxeTier", 2,
                new ConfigDescription("Tool tier of the plains blast against rocks, ore and everything else. 2 is the iron pickaxe.", new AcceptableValueRange<int>(0, 10), sync));
            ExplosivePlainsArrowSpeedFactor = Config.Bind("Explosive Plains", "ArrowSpeedFactor", 0.875f,
                new ConfigDescription("Plains explosive arrow speed as a share of the speed every other mod and the game gave the shot. Plains bolts use the Explosive bolt speed and gravity.", new AcceptableValueRange<float>(0.1f, 1f), sync));
            RecipePlainsExplosiveBase = Config.Bind("Recipes", "PlainsExplosiveBaseAmount", 20,
                new ConfigDescription("Needle arrows or black metal bolts used per plains explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipePlainsExplosiveBlackMetal = Config.Bind("Recipes", "PlainsExplosiveBlackMetalAmount", 1,
                new ConfigDescription("Black metal used per plains explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipePlainsExplosiveCore = Config.Bind("Recipes", "PlainsExplosiveSurtlingCoreAmount", 1,
                new ConfigDescription("Surtling cores used per plains explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipePlainsExplosiveCoal = Config.Bind("Recipes", "PlainsExplosiveCoalAmount", 2,
                new ConfigDescription("Coal used per plains explosive craft.", new AcceptableValueRange<int>(1, 100), sync));
            RecipePlainsExplosiveYield = Config.Bind("Recipes", "PlainsExplosiveYield", 5,
                new ConfigDescription("Items made per plains explosive craft.", new AcceptableValueRange<int>(1, 100), sync));

            LogCounters = Config.Bind("Debug", "LogCounters", false,
                "Writes a log line for every changed shot, pass and energy hit.");

            foreach (var pair in RecipeRules.Rules)
            {
                var rule = pair.Value;
                var section = "Recipe Materials - " + pair.Key;
                Config.Bind(section, "ScatterMaterial", rule.ScatterMaterial, new ConfigDescription("Extra material prefab for scatter crafting.", null, sync));
                Config.Bind(section, "ScatterAmount", rule.ScatterAmount, new ConfigDescription("Extra material amount for scatter crafting.", new AcceptableValueRange<int>(1, 100), sync));
                Config.Bind(section, "PiercingMaterial", rule.PierceMaterial, new ConfigDescription("Extra material prefab for piercing crafting.", null, sync));
                Config.Bind(section, "PiercingAmount", rule.PierceAmount, new ConfigDescription("Extra material amount for piercing crafting.", new AcceptableValueRange<int>(1, 100), sync));
            }

            if (migrate)
            {
                if (ScatterShareBow.Value == 0.15f) ScatterShareBow.Value = 0.30f;
                if (ScatterConeHalfAngle.Value == 8f) ScatterConeHalfAngle.Value = 6f;
                if (RecipeScatterYield.Value == 20) RecipeScatterYield.Value = 10;
                if (RecipeEnergyEitr.Value == 10) RecipeEnergyEitr.Value = 5;
                if (RecipeEnergyYield.Value == 20) RecipeEnergyYield.Value = 10;
                Config.Save();
            }

            AmmoTrailSettings.Bind(Config);
            Items.Hook();
            Shots.Init();

            var harmony = new Harmony(ModGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            AmmoCompatibility.Init(harmony);
            Config.SettingChanged += (sender, args) => Items.RefreshRecipes();
        }

        internal static T RecipeValue<T>(string baseName, string key)
        {
            return (T)Instance.Config[new ConfigDefinition("Recipe Materials - " + baseName, key)].BoxedValue;
        }
    }
}
