using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace AmmoVariants
{
    // builds the scatter, piercing and energy ammo from the vanilla ammo prefabs when the vanilla prefabs are ready.
    internal static class Items
    {
        // vanilla prefab file names (BoltBlackmetal, not BoltBlackMetal).
        internal static readonly string[] BoltBases =
        {
            "BoltBone", "BoltIron", "BoltBlackmetal", "BoltCarapace", "BoltCharred", "BoltBloodGold"
        };

        internal static readonly string[] ArrowBases =
        {
            "ArrowWood", "ArrowBronze", "ArrowIron", "ArrowFlint", "ArrowObsidian", "ArrowCarapace",
            "ArrowCharred", "ArrowSilver", "ArrowNeedle", "ArrowFrost", "ArrowFire", "ArrowPoison", "ArrowBloodGold"
        };

        // energy tiers: Mistlands (Carapace) and Deep North (BloodGold), bolt and arrow each.
        internal static readonly string[] EnergyBases =
        {
            "BoltCarapace", "ArrowCarapace", "BoltBloodGold", "ArrowBloodGold"
        };

        // explosive tiers: Ashlands (Charred), early (fire arrow, bone bolt) and plains (needle arrow, black metal bolt), arrow
        // and bolt. Explosive.Tiers holds their looks.
        internal static readonly string[] ExplosiveBases =
        {
            "ArrowCharred", "BoltCharred", "ArrowFire", "BoltBone", "ArrowNeedle", "BoltBlackmetal"
        };

        internal enum AmmoKind { None, Scatter, Piercing, Energy, Explosive }

        // the projectile prefabs of our ammo, looked up at launch.
        internal static readonly HashSet<GameObject> ScatterProjectiles = new HashSet<GameObject>();
        internal static readonly HashSet<GameObject> PiercingProjectiles = new HashSet<GameObject>();
        internal static readonly HashSet<GameObject> EnergyProjectiles = new HashSet<GameObject>();
        internal static readonly HashSet<GameObject> ExplosiveProjectiles = new HashSet<GameObject>();

        internal static AmmoKind KindOf(ItemDrop.ItemData ammo)
        {
            var projectile = ammo?.m_shared?.m_attack?.m_attackProjectile;
            if (projectile == null) return AmmoKind.None;
            if (ScatterProjectiles.Contains(projectile)) return AmmoKind.Scatter;
            if (PiercingProjectiles.Contains(projectile)) return AmmoKind.Piercing;
            if (EnergyProjectiles.Contains(projectile)) return AmmoKind.Energy;
            if (ExplosiveProjectiles.Contains(projectile)) return AmmoKind.Explosive;
            return AmmoKind.None;
        }

        internal const string WorkbenchPiece = "piece_workbench";
        internal const string BlackForgePiece = "blackforge";
        internal const string EitrItem = "Eitr";
        internal const string FlintItem = "Flint";
        internal const string SulfurItem = "SulfurStone";
        internal const string CoalItem = "Coal";
        internal const string SurtlingCoreItem = "SurtlingCore";
        internal const string GuckItem = "Guck";
        internal const string BlackMetalItem = "BlackMetal";

        // names of the recipes this mod added, for the log and for the known list.
        internal static readonly List<string> Recipes = new List<string>();

        internal static void Hook()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Register;
            ItemManager.OnItemsRegistered += RegisterRecipes;
            ItemManager.OnItemsRegistered += LogRegistered;
        }

        // with LogCounters on, writes whether the game's object database holds each item and recipe.
        private static void LogRegistered()
        {
            if (ObjectDB.instance == null) return;

            int items = Recipes.Count(r => ObjectDB.instance.GetItemPrefab(r.Replace("Recipe_", "")) != null);
            int found = Recipes.Count(r => ObjectDB.instance.m_recipes.Any(x => x != null && x.name == r));
            int expected = (BoltBases.Length + ArrowBases.Length) * 2 + EnergyBases.Length + ExplosiveBases.Length;
            int projectiles = ScatterProjectiles.Concat(PiercingProjectiles).Concat(EnergyProjectiles).Concat(ExplosiveProjectiles)
                .Count(p => ZNetScene.instance != null && ZNetScene.instance.GetPrefab(p.name) == p);
            if (Plugin.LogCounters.Value)
                Plugin.Log.LogInfo($"ammo registration expected {expected}, items {items}, recipes {found}, network projectiles {projectiles}");
            if (items != expected || found != expected || projectiles != expected)
                Plugin.Log.LogError("ammo registration incomplete, check the item, recipe and projectile warnings above");
            foreach (var r in Recipes.Where(r => !ObjectDB.instance.m_recipes.Any(x => x != null && x.name == r)))
            {
                Plugin.Log.LogWarning($"recipe missing from object database: {r}");
            }
        }

        private static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;
            AmmoAssets.Load();
            AmmoAssets.RegisterReleaseSounds();
            foreach (var b in BoltBases.Concat(ArrowBases))
            {
                RegisterVariant(b, "Scatter");
                RegisterVariant(b, "Piercing");
            }

            foreach (var b in EnergyBases)
            {
                RegisterEnergy(b);
            }

            Explosive.RegisterBlasts();
            foreach (var b in ExplosiveBases)
            {
                RegisterExplosive(b);
            }
            AmmoBallistaTrail.Register();
        }

        // the charred ammo with a molten charge and flames, slower and heavier, sticking on the hit and blowing after a fuse.
        private static void RegisterExplosive(string baseName)
        {
            var name = baseName + "Explosive";
            var basePrefab = PrefabManager.Instance.GetPrefab(baseName);
            var baseProjectile = basePrefab?.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack.m_attackProjectile;
            if (baseProjectile == null)
            {
                Plugin.Log.LogWarning($"missing vanilla ammo or projectile {baseName}, {name} not added");
                return;
            }

            var tier = Explosive.Tiers[baseName];
            bool bolt = tier.Bolt;
            var projectile = PrefabManager.Instance.CreateClonedPrefab(name + "Projectile", baseProjectile);
            if (bolt) Lengthen(projectile, Explosive.BoltBodyLength);
            Explosive.Decorate(projectile, tier);
            ExplosiveProjectiles.Add(projectile);
            AmmoModels.Decorate(projectile, "Explosive", AmmoModels.BoundsOf(baseProjectile).size.z);
            projectile.AddComponent<AmmoFlight>();
            PrefabManager.Instance.AddPrefab(projectile);

            var item = PrefabManager.Instance.CreateClonedPrefab(name, basePrefab);
            var shared = item.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "Explosive " + Pretty(baseName);
            shared.m_attack.m_attackProjectile = projectile;
            shared.m_icons = new[] { AmmoAssets.Icon(name) };
            shared.m_description = "Sticks where it hits and explodes. Safe for you, friends and tames.";
            AmmoModels.DropModel(item, projectile);

            if (!ItemManager.Instance.AddItem(new CustomItem(item, true)))
                Plugin.Log.LogError("could not register ammo item " + name);
        }

        // a scatter or piercing ammo: the vanilla ammo with its own projectile prefab.
        private static void RegisterVariant(string baseName, string kind)
        {
            var name = baseName + kind;
            var basePrefab = PrefabManager.Instance.GetPrefab(baseName);
            if (basePrefab == null)
            {
                Plugin.Log.LogWarning($"missing vanilla ammo {baseName}, {name} not added");
                return;
            }

            var drop = basePrefab.GetComponent<ItemDrop>();
            var shared = drop.m_itemData.m_shared;
            var baseProjectile = shared.m_attack.m_attackProjectile;
            if (baseProjectile == null)
            {
                Plugin.Log.LogWarning($"{baseName} has no projectile, {name} not added");
                return;
            }

            var projectile = PrefabManager.Instance.CreateClonedPrefab(name + "Projectile", baseProjectile);
            projectile.AddComponent<AmmoNativeEffects>().Prepare(kind == "Scatter" ? Plugin.ScatterPelletScale.Value : 1f);
            if (kind == "Scatter")
            {
                ConfigureScatter(projectile);
                ScatterProjectiles.Add(projectile);
                var color = AmmoPalette.Lifted(AmmoPalette.ColorOf(projectile));
                color.a = AmmoTrailSettings.ScatterOpacity;
                if (AmmoTrailSettings.Scatter) AddTrail(projectile, color, new Color(color.r,color.g,color.b,0f), ScatterTrailWidth, ScatterTrailSeconds);
                AddDot(projectile, AmmoPalette.ColorOf(projectile), MarkerSize);
                projectile.AddComponent<AmmoScatterBody>();
            }
            else
            {
                // piercing stops only on terrain in the vanilla flight, Shots.AfterHit stops it on other objects.
                var piercing = projectile.GetComponent<Projectile>();
                piercing.m_onlyStopOnTerrain = false;
                // a creature hit keeps the shot alive, the vanilla hit would destroy it otherwise.
                piercing.m_stayAfterHitDynamic = true;
                PiercingProjectiles.Add(projectile);
                var color = AmmoPalette.ColorOf(projectile); color.a = AmmoTrailSettings.PiercingOpacity;
                if (AmmoTrailSettings.Piercing)
                {
                    AddTrail(projectile, color, new Color(color.r,color.g,color.b,0f), PiercingTrailWidth, PiercingTrailSeconds, true);
                    projectile.AddComponent<AmmoTrailLifetime>();
                }
                AddDot(projectile, AmmoPalette.ColorOf(projectile), MarkerSize);
                Lengthen(projectile, Plugin.PierceBodyLength.Value);
            }

            AmmoModels.Decorate(projectile, kind, AmmoModels.BoundsOf(baseProjectile).size.z);
            projectile.AddComponent<AmmoFlight>();
            PrefabManager.Instance.AddPrefab(projectile);

            var item = PrefabManager.Instance.CreateClonedPrefab(name, basePrefab);
            var itemDrop = item.GetComponent<ItemDrop>();
            itemDrop.m_itemData.m_shared.m_name = kind + " " + Pretty(baseName);
            itemDrop.m_itemData.m_shared.m_attack.m_attackProjectile = projectile;
            itemDrop.m_itemData.m_shared.m_icons = new[] { AmmoAssets.Icon(name) };
            itemDrop.m_itemData.m_shared.m_description = kind == "Scatter"
                ? (baseName.StartsWith("Bolt") ? $"A spread of {Plugin.ScatterPellets.Value} small bolts." : $"A spread of {Plugin.ScatterBowPellets.Value} small arrows.")
                : $"Passes through up to {Plugin.PierceMaxCreatures.Value} creatures.";
            AmmoModels.DropModel(item, projectile);

            if (!ItemManager.Instance.AddItem(new CustomItem(item, true)))
                Plugin.Log.LogError("could not register ammo item " + name);
        }

        // ammo contributes its original total before the full-shot fire bonus.
        private static void RegisterEnergy(string baseName)
        {
            var name = baseName + "Energy";
            var basePrefab = PrefabManager.Instance.GetPrefab(baseName);
            if (basePrefab == null)
            {
                Plugin.Log.LogWarning($"missing vanilla ammo {baseName}, {name} not added");
                return;
            }

            var baseDrop = basePrefab.GetComponent<ItemDrop>();
            var baseShared = baseDrop.m_itemData.m_shared;
            var baseProjectile = baseShared.m_attack.m_attackProjectile;
            if (baseProjectile == null)
            {
                Plugin.Log.LogWarning($"{baseName} has no projectile, {name} not added");
                return;
            }

            var projectile = PrefabManager.Instance.CreateClonedPrefab(name + "Projectile", baseProjectile);
            var energy = projectile.GetComponent<Projectile>();
            projectile.AddComponent<AmmoNativeEffects>().Prepare(1f);
            energy.m_gravity = 0f;
            // energy passes creatures, the vanilla hit would destroy it on the first one.
            energy.m_stayAfterHitDynamic = true;
            EnergyProjectiles.Add(projectile);
            foreach (var renderer in projectile.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
                renderer.materials = renderer.materials.Select(m => Tinted(m, Plugin.EnergyColor.Value)).ToArray();
            }
            var energyStart = Plugin.EnergyColor.Value;
            energyStart.a = EnergyTrailOpacity;
            var energyEnd = new Color(energyStart.r, energyStart.g, energyStart.b, 0f);
            if (AmmoTrailSettings.Energy)
            {
                AddTrail(projectile, energyStart, energyEnd, EnergyTrailWidth, EnergyTrailSeconds, true);
                projectile.AddComponent<AmmoTrailLifetime>().Energy = true;
            }
            AddDot(projectile, DotBlue, MarkerSize);
            AmmoModels.Decorate(projectile, "Energy", AmmoModels.BoundsOf(baseProjectile).size.z);
            projectile.AddComponent<AmmoFlight>();
            PrefabManager.Instance.AddPrefab(projectile);

            var item = PrefabManager.Instance.CreateClonedPrefab(name, basePrefab);
            var itemDrop = item.GetComponent<ItemDrop>();
            var shared = itemDrop.m_itemData.m_shared;
            shared.m_name = "Energy " + Pretty(baseName);

            float pierce = baseShared.m_damages.m_pierce;
            shared.m_damages = new HitData.DamageTypes
            {
                m_lightning = pierce * Plugin.EnergyLightningShare.Value,
                m_pierce = pierce * Plugin.EnergyPierceShare.Value
            };
            shared.m_attack.m_attackProjectile = projectile;
            shared.m_icons = new[] { AmmoAssets.Icon(name) };
            shared.m_description = "Flies straight through creatures and walls, weaker with distance.";
            AmmoModels.DropModel(item, projectile);

            if (!ItemManager.Instance.AddItem(new CustomItem(item, true)))
                Plugin.Log.LogError("could not register ammo item " + name);
        }

        // recipes need the world's populated database, after the early prefab event.
        private static void RegisterRecipes()
        {
            foreach (var baseName in BoltBases.Concat(ArrowBases))
                foreach (var kind in new[] { "Scatter", "Piercing" }) RegisterRecipe(baseName, kind);
            foreach (var baseName in EnergyBases) RegisterRecipe(baseName, "Energy");
            foreach (var baseName in ExplosiveBases) RegisterExplosiveRecipe(baseName);
        }

        // at the base ammo's own station: charred ammo with sulfur and coal, early ammo with a surtling core, coal and guck,
        // plains ammo with black metal, a surtling core and coal.
        private static void RegisterExplosiveRecipe(string baseName)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            var name = baseName + "Explosive";
            bool early = Explosive.Tiers[baseName].Early;
            bool plains = Explosive.Tiers[baseName].Plains;
            var original = FindRecipe(baseName);
            var item = db.GetItemPrefab(name)?.GetComponent<ItemDrop>();
            var baseItem = db.GetItemPrefab(baseName)?.GetComponent<ItemDrop>();
            var parts = (early
                ? new[] { (SurtlingCoreItem, Plugin.RecipeEarlyExplosiveCore.Value), (CoalItem, Plugin.RecipeEarlyExplosiveCoal.Value), (GuckItem, Plugin.RecipeEarlyExplosiveGuck.Value) }
                : plains
                ? new[] { (BlackMetalItem, Plugin.RecipePlainsExplosiveBlackMetal.Value), (SurtlingCoreItem, Plugin.RecipePlainsExplosiveCore.Value), (CoalItem, Plugin.RecipePlainsExplosiveCoal.Value) }
                : new[] { (SulfurItem, Plugin.RecipeExplosiveSulfur.Value), (CoalItem, Plugin.RecipeExplosiveCoal.Value) })
                .Select(p => (name: p.Item1, drop: db.GetItemPrefab(p.Item1)?.GetComponent<ItemDrop>(), amount: p.Item2)).ToArray();
            if (item == null || baseItem == null || parts.Any(p => p.drop == null) || original == null || original.m_craftingStation == null)
            {
                Plugin.Log.LogError($"recipe {name} unavailable, item {item != null}, base {baseItem != null}, {string.Join(", ", parts.Select(p => p.name + " " + (p.drop != null)))}, original {original != null}");
                return;
            }
            string recipeName = "Recipe_" + name;
            var recipe = db.m_recipes.FirstOrDefault(r => r != null && r.name == recipeName);
            bool added = recipe == null;
            if (added) recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = recipeName;
            recipe.m_item = item;
            recipe.m_enabled = true;
            recipe.m_craftingStation = original.m_craftingStation;
            recipe.m_minStationLevel = original.m_minStationLevel;
            recipe.m_amount = early ? Plugin.RecipeEarlyExplosiveYield.Value : plains ? Plugin.RecipePlainsExplosiveYield.Value : Plugin.RecipeExplosiveYield.Value;
            recipe.m_resources = new[] { new Piece.Requirement { m_resItem = baseItem, m_amount = early ? Plugin.RecipeEarlyExplosiveBase.Value : plains ? Plugin.RecipePlainsExplosiveBase.Value : Plugin.RecipeExplosiveBase.Value } }
                .Concat(parts.Select(p => new Piece.Requirement { m_resItem = p.drop, m_amount = p.amount })).ToArray();
            if (!Recipes.Contains(recipeName))
            {
                if (!ItemManager.Instance.AddRecipe(new CustomRecipe(recipe, false, false)))
                {
                    Plugin.Log.LogError("could not register ammo recipe " + recipeName);
                    return;
                }
                Recipes.Add(recipeName);
            }
            if (added) db.m_recipes.Add(recipe);
        }

        private static void RegisterRecipe(string baseName, string kind)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            var name = baseName + kind;
            var original = FindRecipe(baseName);
            var item = db.GetItemPrefab(name)?.GetComponent<ItemDrop>();
            var baseItem = db.GetItemPrefab(baseName)?.GetComponent<ItemDrop>();
            bool energy = kind == "Energy";
            var materialName = energy ? EitrItem : Plugin.RecipeValue<string>(baseName, kind + "Material");
            var material = db.GetItemPrefab(materialName)?.GetComponent<ItemDrop>();
            var station = energy ? PrefabManager.Instance.GetPrefab(BlackForgePiece)?.GetComponent<CraftingStation>() : original?.m_craftingStation;
            if (item == null || baseItem == null || material == null || original == null || station == null)
            {
                Plugin.Log.LogError($"recipe {name} unavailable, item {item != null}, base {baseItem != null}, material {materialName} {material != null}, original {original != null}, station {station != null}");
                return;
            }
            string recipeName = "Recipe_" + name;
            var recipe = db.m_recipes.FirstOrDefault(r => r != null && r.name == recipeName);
            bool added = recipe == null;
            if (added) recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = recipeName;
            recipe.m_item = item;
            recipe.m_enabled = true;
            recipe.m_craftingStation = station;
            recipe.m_minStationLevel = original.m_minStationLevel;
            recipe.m_amount = energy ? Plugin.RecipeEnergyYield.Value : kind == "Scatter" ? Plugin.RecipeScatterYield.Value : Plugin.RecipePierceYield.Value;
            recipe.m_resources = new[]
            {
                new Piece.Requirement { m_resItem = energy ? material : baseItem, m_amount = energy ? Plugin.RecipeEnergyEitr.Value : Plugin.RecipeScatterBase.Value },
                new Piece.Requirement { m_resItem = energy ? baseItem : material, m_amount = energy ? Plugin.RecipeEnergyBase.Value : Plugin.RecipeValue<int>(baseName, kind + "Amount") }
            };
            if (!Recipes.Contains(recipeName))
            {
                if (!ItemManager.Instance.AddRecipe(new CustomRecipe(recipe, false, false)))
                {
                    Plugin.Log.LogError("could not register ammo recipe " + recipeName);
                    return;
                }
                Recipes.Add(recipeName);
            }
            if (added) db.m_recipes.Add(recipe);
        }

        // only the visible children shrink, while the flight root and tracer keep their scale.
        private static void ConfigureScatter(GameObject projectile)
        {
            foreach (Transform child in projectile.transform)
            {
                child.localPosition *= Plugin.ScatterPelletScale.Value;
                child.localScale *= Plugin.ScatterPelletScale.Value;
            }
            foreach (var group in projectile.GetComponentsInChildren<LODGroup>(true))
            {
                group.enabled = false;
                foreach (var lod in group.GetLODs())
                    foreach (var renderer in lod.renderers)
                        if (renderer != null) renderer.enabled = true;
            }
        }

        // the katana's steel trail colors, from KatanaFx.SteelTrail, as values only.
        private static readonly Color GoldStart = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color GoldEnd = new Color(1f, 0.5f, 0.1f, 0f);
        private static readonly Color DotGreen = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color DotRed = new Color(1f, 0.15f, 0.15f, 1f);
        private static readonly Color DotBlue = new Color(0.2f, 0.5f, 1f, 1f);
        private static readonly Color ScatterTrailStart = new Color(1f, 0.15f, 0.15f, 0.85f);
        private const float ScatterTrailWidth = AmmoTrailSettings.ScatterWidth;
        private const float ScatterTrailSeconds = AmmoPalette.ScatterTrailSeconds;
        private const float PiercingTrailWidth = AmmoTrailSettings.PiercingWidth;
        private const float PiercingTrailSeconds = AmmoVisuals.PiercingFadeSeconds;
        private const float EnergyTrailWidth = AmmoVisuals.EnergyTrailWidth;
        private const float EnergyTrailSeconds = AmmoVisuals.FadeSeconds;
        private const float EnergyTrailOpacity = 0.85f;
        private const float MarkerSize = 0.04f;
        private const float MarkerOpacity = 0.40f;

        // a narrow trail that follows the shaft, its own renderer on the projectile.
        private static void AddTrail(GameObject owner, Color start, Color end, float width, float seconds, bool glow = false)
        {
            var go = new GameObject("AmmoTrail");
            go.transform.SetParent(owner.transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = seconds;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.startColor = start;
            trail.endColor = end;
            if (glow)
            {
                var middle = start; middle.a *= 0.7f;
                trail.colorGradient = AmmoVisuals.Gradient(start, middle, end);
                trail.sharedMaterial = AmmoVisuals.TrailMaterial();
                trail.emitting = false;
                trail.enabled = false;
                trail.minVertexDistance = AmmoVisuals.MinimumDistance;
                trail.alignment = LineAlignment.View;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
            }
            else
            {
                trail.material = new Material(AmmoAssets.TrailShader);
                trail.material.color = Color.white;
                trail.endWidth = width * AmmoTrailSettings.ScatterTaper;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(start, 1f) },
                    new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a, AmmoTrailSettings.ScatterHoldShare), new GradientAlphaKey(end.a, 1f) });
                trail.colorGradient = gradient;
            }
        }

        // a small dot on the shaft's tail, so the variant reads at a glance.
        private static void AddDot(GameObject owner, Color color, float size)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "AmmoDot";
            dot.transform.SetParent(owner.transform, false);
            dot.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            dot.transform.localScale = Vector3.one * size;
            UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());
            var renderer = dot.GetComponent<Renderer>();
            renderer.material = new Material(AmmoAssets.TrailShader);
            color.a = MarkerOpacity;
            renderer.material.color = color;
        }

        // a copy of the material with the tint, so the vanilla material is untouched.
        private static Material Tinted(Material source, Color tint)
        {
            if (source == null) return null;
            var copy = new Material(AmmoAssets.TrailShader);
            copy.mainTexture = source.mainTexture;
            copy.color = tint;
            return copy;
        }

        internal static Recipe FindRecipe(string baseName)
        {
            return ObjectDB.instance?.m_recipes.FirstOrDefault(r => r != null && r.m_item != null && r.m_item.gameObject.name == baseName);
        }

        internal static void RefreshRecipes()
        {
            if (ObjectDB.instance == null || Recipes.Count == 0) return;
            foreach (var recipe in ObjectDB.instance.m_recipes)
            {
                if (recipe == null || !Recipes.Contains(recipe.name)) continue;
                string name = recipe.name.Substring("Recipe_".Length);
                if (name.EndsWith("Explosive"))
                {
                    var tier = Explosive.Tiers[name.Substring(0, name.Length - "Explosive".Length)];
                    if (tier.Early)
                    {
                        recipe.m_amount = Plugin.RecipeEarlyExplosiveYield.Value;
                        recipe.m_resources[0].m_amount = Plugin.RecipeEarlyExplosiveBase.Value;
                        recipe.m_resources[1].m_amount = Plugin.RecipeEarlyExplosiveCore.Value;
                        recipe.m_resources[2].m_amount = Plugin.RecipeEarlyExplosiveCoal.Value;
                        recipe.m_resources[3].m_amount = Plugin.RecipeEarlyExplosiveGuck.Value;
                    }
                    else if (tier.Plains)
                    {
                        recipe.m_amount = Plugin.RecipePlainsExplosiveYield.Value;
                        recipe.m_resources[0].m_amount = Plugin.RecipePlainsExplosiveBase.Value;
                        recipe.m_resources[1].m_amount = Plugin.RecipePlainsExplosiveBlackMetal.Value;
                        recipe.m_resources[2].m_amount = Plugin.RecipePlainsExplosiveCore.Value;
                        recipe.m_resources[3].m_amount = Plugin.RecipePlainsExplosiveCoal.Value;
                    }
                    else
                    {
                        recipe.m_amount = Plugin.RecipeExplosiveYield.Value;
                        recipe.m_resources[0].m_amount = Plugin.RecipeExplosiveBase.Value;
                        recipe.m_resources[1].m_amount = Plugin.RecipeExplosiveSulfur.Value;
                        recipe.m_resources[2].m_amount = Plugin.RecipeExplosiveCoal.Value;
                    }
                    continue;
                }
                bool energy = name.EndsWith("Energy");
                bool scatter = name.EndsWith("Scatter");
                string kind = energy ? "Energy" : scatter ? "Scatter" : "Piercing";
                string baseName = name.Substring(0, name.Length - kind.Length);
                recipe.m_amount = energy ? Plugin.RecipeEnergyYield.Value : scatter ? Plugin.RecipeScatterYield.Value : Plugin.RecipePierceYield.Value;
                if (energy)
                {
                    recipe.m_resources[0].m_amount = Plugin.RecipeEnergyEitr.Value;
                    recipe.m_resources[1].m_amount = Plugin.RecipeEnergyBase.Value;
                }
                else
                {
                    recipe.m_resources[0].m_amount = Plugin.RecipeScatterBase.Value;
                    var item = ObjectDB.instance.GetItemPrefab(Plugin.RecipeValue<string>(baseName, kind + "Material"));
                    if (item == null) { Plugin.Log.LogWarning("invalid recipe material for " + recipe.name); continue; }
                    recipe.m_resources[1].m_resItem = item.GetComponent<ItemDrop>();
                    recipe.m_resources[1].m_amount = Plugin.RecipeValue<int>(baseName, kind + "Amount");
                }
            }
        }

        // stretches visible meshes along the flight axis while preserving UV coordinates and colliders.
        private static void Lengthen(GameObject owner, float factor)
        {
            foreach (var filter in owner.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.gameObject.name == "AmmoDot") continue;
                if (!filter.sharedMesh.isReadable)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null) continue;
                    var source = filter.sharedMesh;
                    var matrix = owner.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var visual = new GameObject("AmmoBodyLength");
                    visual.transform.SetParent(owner.transform, false);
                    visual.transform.localPosition = matrix.MultiplyPoint3x4(source.bounds.center);
                    var child = new GameObject(filter.name); child.transform.SetParent(visual.transform, false);
                    child.transform.localPosition = new Vector3(matrix.m03,matrix.m13,matrix.m23) - visual.transform.localPosition;
                    child.transform.localRotation = matrix.rotation; child.transform.localScale = matrix.lossyScale;
                    child.AddComponent<MeshFilter>().sharedMesh = source;
                    var copy = child.AddComponent<MeshRenderer>(); copy.sharedMaterials = renderer.sharedMaterials;
                    copy.shadowCastingMode = renderer.shadowCastingMode; copy.receiveShadows = renderer.receiveShadows;
                    filter.sharedMesh = null; renderer.enabled = false;
                    visual.transform.localScale = new Vector3(1f, 1f, factor);
                    if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("stretched non-readable ammo visual " + owner.name + ", collider transforms retained");
                    continue;
                }
                var mesh = Object.Instantiate(filter.sharedMesh);
                var vertices = mesh.vertices;
                var toRoot = owner.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var fromRoot = toRoot.inverse;
                var center = toRoot.MultiplyPoint3x4(mesh.bounds.center);
                for (int i = 0; i < vertices.Length; i++)
                {
                    var point = toRoot.MultiplyPoint3x4(vertices[i]);
                    point.z = center.z + (point.z - center.z) * factor;
                    vertices[i] = fromRoot.MultiplyPoint3x4(point);
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                filter.sharedMesh = mesh;
            }
        }

        // "BoltBlackmetal" to "Bolt Blackmetal" for the display name.
        private static string Pretty(string baseName)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < baseName.Length; i++)
            {
                if (i > 0 && char.IsUpper(baseName[i])) sb.Append(' ');
                sb.Append(baseName[i]);
            }
            return sb.ToString();
        }
    }
}
