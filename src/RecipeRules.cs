using System;
using System.Collections.Generic;

namespace AmmoVariants
{
    internal sealed class RecipeRule
    {
        internal readonly string ScatterMaterial;
        internal readonly int ScatterAmount;
        internal readonly string PierceMaterial;
        internal readonly int PierceAmount;

        internal RecipeRule(string scatterMaterial, int scatterAmount, string pierceMaterial, int pierceAmount)
        {
            ScatterMaterial = scatterMaterial;
            ScatterAmount = scatterAmount;
            PierceMaterial = pierceMaterial;
            PierceAmount = pierceAmount;
        }
    }

    internal static class RecipeRules
    {
        internal static readonly Dictionary<string, RecipeRule> Rules = new Dictionary<string, RecipeRule>
        {
            ["ArrowWood"] = new RecipeRule("Resin", 2, "Flint", 2),
            ["ArrowFlint"] = new RecipeRule("Resin", 2, "Flint", 2),
            ["ArrowFire"] = new RecipeRule("Resin", 2, "Flint", 2),
            ["ArrowBronze"] = new RecipeRule("Coal", 2, "Bronze", 1),
            ["ArrowIron"] = new RecipeRule("Coal", 2, "Iron", 1),
            ["ArrowSilver"] = new RecipeRule("Coal", 2, "Silver", 1),
            ["ArrowObsidian"] = new RecipeRule("Obsidian", 1, "Obsidian", 2),
            ["ArrowFrost"] = new RecipeRule("Obsidian", 1, "Obsidian", 2),
            ["ArrowPoison"] = new RecipeRule("Obsidian", 1, "Obsidian", 2),
            ["ArrowNeedle"] = new RecipeRule("Needle", 1, "Needle", 2),
            ["ArrowCarapace"] = new RecipeRule("Carapace", 1, "Carapace", 2),
            ["ArrowCharred"] = new RecipeRule("CharredBone", 1, "CharredBone", 2),
            ["ArrowBloodGold"] = new RecipeRule("Frostwood", 2, "Gold", 1),
            ["BoltBone"] = new RecipeRule("Coal", 2, "BoneFragments", 2),
            ["BoltIron"] = new RecipeRule("Coal", 2, "Iron", 1),
            ["BoltBlackmetal"] = new RecipeRule("Coal", 2, "BlackMetal", 1),
            ["BoltCarapace"] = new RecipeRule("Carapace", 1, "Carapace", 2),
            ["BoltCharred"] = new RecipeRule("CharredBone", 1, "CharredBone", 2),
            ["BoltBloodGold"] = new RecipeRule("Frostwood", 2, "Gold", 1)
        };
    }
}
