using System.Collections.Generic;
using UnityEngine;

namespace AmmoVariants
{
    internal static class AmmoPalette
    {
        internal const float PiercingOpacity = 0.65f;
        internal const float ScatterTrailSeconds = 1f;
        internal const float DarkThreshold = 0.45f;
        internal const float EdgeOpacity = 0.18f;
        internal const float EdgeWidthShare = 1.5f;
        internal static readonly Color Energy = new Color(0.33f,0.62f,1f,1f);
        internal static readonly Color EdgeColor = new Color(0.9f,0.95f,0.96f,1f);
        private static Material darkMaterial;
        private static readonly Dictionary<string,Color> Materials = new Dictionary<string,Color>
        {
            { "Wood", new Color32(201,205,208,255) },
            { "Flint", new Color32(146,154,159,255) },
            { "Iron", Color.white },
            { "Bronze", new Color32(197,140,82,255) },
            { "Blackmetal", new Color32(51,72,62,255) },
            { "Carapace", new Color32(216,201,157,255) },
            { "Obsidian", new Color32(78,80,91,255) },
            { "Bone", new Color32(222,213,186,255) },
            { "Silver", new Color32(205,224,235,255) },
            { "Needle", new Color32(99,179,166,255) },
            { "Charred", new Color32(88,100,111,255) },
            { "BloodGold", new Color32(232,171,56,255) },
            { "Fire", new Color32(255,122,50,255) },
            { "Frost", new Color32(142,235,255,255) },
            { "Poison", new Color32(120,224,68,255) }
        };

        internal static Color ColorOf(GameObject projectile)
        {
            if (Items.EnergyProjectiles.Contains(projectile)) return Energy;
            foreach (var pair in Materials)
                if (projectile.name.StartsWith("Arrow" + pair.Key) || projectile.name.StartsWith("Bolt" + pair.Key)) return pair.Value;
            return Color.white;
        }

        internal static Color MaterialColor(string tier)
        {
            return Materials.TryGetValue(tier,out var color) ? color : Color.white;
        }

        // dark tiers blend toward the pale edge color so a thin line stays readable.
        internal static Color Lifted(Color tint)
        {
            if (Mathf.Max(tint.r,Mathf.Max(tint.g,tint.b)) >= DarkThreshold) return tint;
            return Color.Lerp(tint,EdgeColor,AmmoTrailSettings.ScatterLift);
        }

        internal static Material DarkMaterial()
        {
            if (darkMaterial == null) darkMaterial = new Material(AmmoAssets.TrailShader) { color = Color.white };
            return darkMaterial;
        }
    }
}
