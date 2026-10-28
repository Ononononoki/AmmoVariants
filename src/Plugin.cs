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
        public const string ModVersion = "1.0.1";

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
