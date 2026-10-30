using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace AmmoVariants
{
    // one explosive ammo: the look picks from the 29 Oct drafts, in metres along the shaft (the charge's size and the
    // distance of its center behind the tip), the flame and blast effect sizes and the blast prefab it uses.
    internal sealed class ExplosiveTier
    {
        internal readonly bool Early;
        // the plains tier (needle arrow, black metal bolt), between early and charred
        internal readonly bool Plains;
        internal readonly bool Bolt;
        internal readonly bool Fins;
        internal readonly float ChargeSize;
        internal readonly float ChargeBack;
        internal readonly float FlameScale;
        internal readonly float BlastScale;
        internal GameObject Blast;

        internal ExplosiveTier(bool early, bool bolt, bool fins, float chargeSize, float chargeBack, float flameScale, float blastScale, bool plains = false)
        {
            Early = early; Plains = plains; Bolt = bolt; Fins = fins;
            ChargeSize = chargeSize; ChargeBack = chargeBack; FlameScale = flameScale; BlastScale = blastScale;
        }
    }

    // the explosive kind: a charge on the shaft (the molten ball on charred ammo, the surtling core on early and plains ammo)
    // with the staff of embers' flames, a stuck fuse after the hit and a blast cloned from the game's siege bomb blast.
    internal static class Explosive
    {
        internal static readonly Dictionary<string, ExplosiveTier> Tiers = new Dictionary<string, ExplosiveTier>
        {
            ["ArrowCharred"] = new ExplosiveTier(false, false, false, 0.18f, 0.114f, 0.7f, 1f),
            ["BoltCharred"] = new ExplosiveTier(false, true, false, 0.15f, 0.083f, 0.5f, 1.4f),
            ["ArrowFire"] = new ExplosiveTier(true, false, true, 0.09f, 0.14f, 0.5f, 0.7f),
            ["BoltBone"] = new ExplosiveTier(true, true, false, 0.08f, 0.14f, 0.4f, 1f),
            ["ArrowNeedle"] = new ExplosiveTier(false, false, false, 0.105f, 0.14f, 0.6f, 0.85f, true),
            ["BoltBlackmetal"] = new ExplosiveTier(false, true, false, 0.095f, 0.14f, 0.45f, 1.2f, true),
        };
        internal const float BoltBodyLength = 1.2f;
        internal const float MinimumDraw = 0.05f;
        internal const float StayAfterFuse = 2f;
        internal const string WorkbenchName = "$piece_workbench";
        private const string ChargeSource = "bombdynamite_projectile";
        private const string CoreSource = "SurtlingCore";
        private const string FinSource = "bow_projectile_charred";
        private const string FlameSource = "staff_fireball_projectile";
        private const string BlastSource = "siegebomb_explosion";
        private static readonly string[] FlameParts = { "flames", "flames_world", "Point light" };
        // the surtling core turned so one corner points along the shaft, a diamond from every side
        private static Quaternion PointFirst => Quaternion.Euler(0f, 0f, 45f) * Quaternion.Euler(35.264f, 0f, 0f) * Quaternion.Euler(0f, 45f, 0f);
        private static readonly Color FinColor = new Color(0.16f, 0.18f, 0.21f);
        private const float FinLength = 0.10f;
        private const float FinHeight = 0.032f;
        private const float CoreLightRange = 0.5f;
        // the shaft heat behind the charge's back face in metres: red-hot, cooled to black, char end, soot end
        internal static readonly Vector4 HeatZones = new Vector4(0.03f, 0.06f, 0.10f, 0.18f);
        internal const float HeatGlow = 1.15f;

        private static readonly Dictionary<GameObject, ExplosiveTier> ByProjectile = new Dictionary<GameObject, ExplosiveTier>();
        private const int HitLogLimit = 30;
        private static int hitLogs;
        private static bool quitting;

        internal static ExplosiveTier TierOf(GameObject projectile)
        {
            return projectile != null && ByProjectile.TryGetValue(projectile, out var tier) ? tier : null;
        }

        // the blast prefabs: the siege bomb blast without its crater, its sound swapped for the mod's blast sound.
        // the charred ones keep their first names, the early ones are smaller, the plains ones in between
        internal static void RegisterBlasts()
        {
            Application.quitting += () => quitting = true;
            var source = PrefabManager.Instance.GetPrefab(BlastSource);
            if (source == null || source.GetComponent<Aoe>() == null)
            {
                Plugin.Log.LogError("missing game blast " + BlastSource + ", explosive ammo will not explode");
                return;
            }
            foreach (var pair in Tiers)
            {
                string name = "AmmoVariants_ExplosiveBlast" + (pair.Value.Early ? "Early" : pair.Value.Plains ? "Plains" : "") + (pair.Value.Bolt ? "Bolt" : "Arrow");
                var clip = pair.Value.Early ? (pair.Value.Bolt ? AmmoAssets.EarlyBlastBolt : AmmoAssets.EarlyBlastArrow)
                    : pair.Value.Plains ? (pair.Value.Bolt ? AmmoAssets.PlainsBlastBolt : AmmoAssets.PlainsBlastArrow)
                    : (pair.Value.Bolt ? AmmoAssets.BlastBolt : AmmoAssets.BlastArrow);
                pair.Value.Blast = Blast(source, name, pair.Value.BlastScale, clip);
            }
        }

        private static GameObject Blast(GameObject source, string name, float scale, AudioClip clip)
        {
            var blast = PrefabManager.Instance.CreateClonedPrefab(name, source);
            var aoe = blast.GetComponent<Aoe>();
            aoe.m_useAttackSettings = false;
            aoe.m_damage = new HitData.DamageTypes();
            aoe.m_spawnOnHitTerrain = null;
            aoe.m_hitTerrain = false;
            aoe.m_hitOwner = false;
            aoe.m_hitFriendly = false;
            aoe.m_hitProps = true;
            aoe.m_scaleDamageByDistance = true;
            aoe.m_skill = Skills.SkillType.None;
            foreach (var system in blast.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            foreach (Transform child in blast.transform)
                if (child.GetComponentInChildren<ParticleSystem>(true) != null) child.localScale *= scale;
            foreach (var light in blast.GetComponentsInChildren<Light>(true)) light.range *= scale;
            int sounds = 0;
            foreach (var sfx in blast.GetComponentsInChildren<ZSFX>(true))
            {
                if (clip == null) break;
                sfx.m_audioClips = new[] { clip };
                sfx.m_minPitch = sfx.m_maxPitch = 1f;
                sounds++;
            }
            blast.AddComponent<AmmoExplosiveBlast>();
            PrefabManager.Instance.AddPrefab(blast);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive blast prefab {name}, effect size {scale:F1}, sounds swapped {sounds}");
            return blast;
        }

        // the charge behind the tip (the molten ball with its cap toward the shaft, or the surtling core point first), the
        // flames on it, the built fins where the shaft has none that show, then the native effect setup.
        internal static void Decorate(GameObject projectile, ExplosiveTier tier)
        {
            ByProjectile[projectile] = tier;
            var body = AmmoModels.BoundsOf(projectile);
            var shaft = projectile.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() is MeshRenderer r && r.enabled).ToList();
            var charge = tier.Early || tier.Plains ? Core(projectile, body, tier.ChargeSize, tier.ChargeBack) : Charge(projectile, body, tier.ChargeSize, tier.ChargeBack);
            if (charge != null && (tier.Early || tier.Plains)) Heat(projectile, shaft, RearOf(projectile, charge));
            if (tier.Fins) Fins(projectile, body);
            // the fire arrow's cinders could light buildings near a workbench, which the blast spares
            foreach (var cinders in projectile.GetComponentsInChildren<CinderSpawner>(true)) Object.DestroyImmediate(cinders);
            if (AmmoTrailSettings.Explosive && charge != null) Flames(projectile, charge.transform.localPosition, tier.FlameScale);
            var native = projectile.AddComponent<AmmoNativeEffects>();
            native.KeepBurningAfterHit();
            native.Prepare(1f);
            var view = projectile.GetComponent<Projectile>();
            view.m_attachToClosestBone = true;
            view.m_stayAfterHitStatic = true;
            view.m_stayAfterHitDynamic = true;
            view.m_stopEmittersOnHit = false;
            projectile.AddComponent<AmmoExplosiveFuse>();
        }

        private static GameObject Charge(GameObject projectile, Bounds body, float size, float back)
        {
            var source = PrefabManager.Instance.GetPrefab(ChargeSource);
            var filter = source?.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "bomb" && f.sharedMesh != null);
            var renderer = filter?.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                Plugin.Log.LogWarning("missing game charge model " + ChargeSource + ", explosive ammo keeps the plain shaft");
                return null;
            }
            var mesh = filter.sharedMesh.bounds;
            // the longest side lies along the shaft and the cap end points back toward the fletching
            var turn = mesh.size.x >= mesh.size.y && mesh.size.x >= mesh.size.z ? Quaternion.Euler(0f, 90f, 0f)
                : mesh.size.y >= mesh.size.z ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            if ((turn * mesh.center).z > 0f) turn = Quaternion.Euler(0f, 180f, 0f) * turn;
            float scale = size / Mathf.Max(mesh.size.x, Mathf.Max(mesh.size.y, mesh.size.z));
            var fitted = Turned(mesh, turn, scale);
            var charge = new GameObject("AmmoCharge");
            charge.transform.SetParent(projectile.transform, false);
            charge.transform.localRotation = turn;
            charge.transform.localScale = Vector3.one * scale;
            var place = new Vector3(body.center.x, body.center.y, body.max.z - back - (fitted.size.z - fitted.size.x));
            charge.transform.localPosition = place - fitted.center;
            charge.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            charge.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive charge {projectile.name}, shaft {body.size.z:F3} m, charge {fitted.size.ToString("F3")} at {place.z:F3}, tip {body.max.z:F3}, cap back {(turn * mesh.center).z <= 0f}");
            return charge;
        }

        // the surtling core crystal point first, its center the given distance behind the tip, with the core's own glow.
        private static GameObject Core(GameObject projectile, Bounds body, float size, float back)
        {
            var source = PrefabManager.Instance.GetPrefab(CoreSource);
            var filter = source?.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "core" && f.sharedMesh != null);
            var renderer = filter?.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                Plugin.Log.LogWarning("missing game core model " + CoreSource + ", explosive ammo keeps the plain shaft");
                return null;
            }
            var mesh = filter.sharedMesh.bounds;
            float scale = size / Mathf.Max(mesh.size.x, Mathf.Max(mesh.size.y, mesh.size.z));
            var fitted = Turned(mesh, PointFirst, scale);
            var charge = new GameObject("AmmoCharge");
            charge.transform.SetParent(projectile.transform, false);
            charge.transform.localRotation = PointFirst;
            charge.transform.localScale = Vector3.one * scale;
            var place = new Vector3(body.center.x, body.center.y, body.max.z - back);
            charge.transform.localPosition = place - fitted.center;
            charge.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            charge.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            var light = source.GetComponentsInChildren<Light>(true).FirstOrDefault();
            if (light != null)
            {
                var glow = Object.Instantiate(light.gameObject, projectile.transform, false);
                glow.name = "AmmoChargeGlow";
                foreach (var script in glow.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                glow.transform.localPosition = place;
                glow.transform.localScale = Vector3.one;
                foreach (var l in glow.GetComponentsInChildren<Light>(true)) l.range *= CoreLightRange;
            }
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive core {projectile.name}, shaft {body.size.z:F3} m, core {fitted.size.ToString("F3")} at {place.z:F3}, tip {body.max.z:F3}");
            return charge;
        }

        // the charge's back face along the shaft in the shot's space, where the heat zones start
        private static float RearOf(GameObject projectile, GameObject charge)
        {
            var toShot = projectile.transform.worldToLocalMatrix * charge.transform.localToWorldMatrix;
            var box = charge.GetComponent<MeshFilter>().sharedMesh.bounds;
            float rear = float.MaxValue;
            for (int i = 0; i < 8; i++)
                rear = Mathf.Min(rear, toShot.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z)).z);
            return rear;
        }

        // two layers over each shaft mesh that reaches the soot: the red-hot head and charge area cooling to char, then the
        // soot fading into the shaft's own color. the shaders read the shot's space from the mesh's through three rows,
        // so copies of the shaft (drop model, loaded crossbow) keep the same look at any scale.
        private static void Heat(GameObject projectile, List<MeshFilter> shaft, float coreZ)
        {
            if (!AmmoTrailSettings.ShaftHeat || AmmoAssets.HeatShader == null || AmmoAssets.SootShader == null || !AmmoAssets.HeatShader.isSupported || !AmmoAssets.SootShader.isSupported) return;
            float seed = (projectile.name.GetStableHashCode() & 1023) * 0.37f;
            int layers = 0;
            foreach (var source in shaft)
            {
                var mesh = source.sharedMesh;
                var toShot = projectile.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                float front = float.MinValue;
                var box = mesh.bounds;
                for (int i = 0; i < 8; i++)
                    front = Mathf.Max(front, toShot.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z)).z);
                // the needle arrow's feather quads lie wholly behind the soot
                if (front < coreZ - HeatZones.w) continue;
                var row2 = toShot.GetRow(2); row2.w -= coreZ;
                foreach (var shader in new[] { AmmoAssets.HeatShader, AmmoAssets.SootShader })
                {
                    var layer = new GameObject(shader == AmmoAssets.HeatShader ? "AmmoHeat" : "AmmoSoot");
                    layer.transform.SetParent(source.transform, false);
                    layer.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = layer.AddComponent<MeshRenderer>();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var material = new Material(shader) { name = "AmmoVariants_" + layer.name };
                    material.SetVector("_HeatRow0", toShot.GetRow(0));
                    material.SetVector("_HeatRow1", toShot.GetRow(1));
                    material.SetVector("_HeatRow2", row2);
                    material.SetVector("_HeatZones", HeatZones);
                    material.SetFloat("_HeatSeed", seed);
                    material.SetFloat("_HeatGlow", HeatGlow);
                    material.SetFloat("_HeatLinear", QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f : 0f);
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, mesh.subMeshCount)).ToArray();
                    layers++;
                }
            }
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive shaft heat {projectile.name}, layers {layers}, charge back at {coreZ:F3}, zones {HeatZones.ToString("F2")}");
        }

        // four fins in a cross shaped like the charred arrow's (whose model can not be read at runtime), both faces with
        // their own vertices, lit by the charred arrow's shader in a dark slate color.
        private static void Fins(GameObject projectile, Bounds body)
        {
            var source = PrefabManager.Instance.GetPrefab(FinSource)?.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.sharedMaterial != null);
            if (source == null)
            {
                Plugin.Log.LogWarning("missing game fin material " + FinSource + ", explosive ammo keeps its own feathers");
                return;
            }
            float length = body.size.z, fin = length * FinLength, height = length * FinHeight, start = length * 0.01f;
            var profile = new[] { new Vector2(0.08f, 0.1f), new Vector2(-0.08f, 1f), new Vector2(0.55f, 1f), new Vector2(1f, 0.1f) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int side = 0; side < 4; side++)
            {
                var radial = Quaternion.Euler(0f, 0f, side * 90f) * Vector3.up;
                for (int face = 0; face < 2; face++)
                {
                    int first = vertices.Count;
                    foreach (var p in profile) vertices.Add(radial * (p.y * height) + Vector3.forward * (start + p.x * fin));
                    triangles.AddRange(face == 0 ? new[] { first, first + 1, first + 2, first, first + 2, first + 3 } : new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                }
            }
            var mesh = new Mesh { name = "AmmoVariants_fins" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.uv = vertices.Select(v => new Vector2(0.5f, 0.5f)).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var slate = new Texture2D(1, 1);
            slate.SetPixel(0, 0, FinColor);
            slate.Apply();
            var material = new Material(source.sharedMaterial) { name = "AmmoVariants_fins" };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", slate);
            var fins = new GameObject("AmmoFins");
            fins.transform.SetParent(projectile.transform, false);
            fins.transform.localPosition = new Vector3(body.center.x, body.center.y, body.min.z);
            fins.AddComponent<MeshFilter>().sharedMesh = mesh;
            fins.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive fins {projectile.name}, length {fin:F3} m, height {height:F3} m");
        }

        // the mesh bounds after the turn and the uniform scale, in the parent's space
        private static Bounds Turned(Bounds mesh, Quaternion turn, float scale)
        {
            var bounds = new Bounds(turn * mesh.center * scale, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? mesh.min.x : mesh.max.x, (i & 2) == 0 ? mesh.min.y : mesh.max.y, (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                bounds.Encapsulate(turn * corner * scale);
            }
            return bounds;
        }

        // the staff of embers' flame parts on the charge, scaled, without the fireball and without its cinder spawner.
        private static void Flames(GameObject projectile, Vector3 center, float scale)
        {
            var source = PrefabManager.Instance.GetPrefab(FlameSource);
            int added = 0;
            foreach (string name in FlameParts)
            {
                var part = source == null ? null : source.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                if (part == null) continue;
                var copy = Object.Instantiate(part.gameObject, projectile.transform, false);
                copy.name = "AmmoFlame_" + name;
                foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                copy.transform.localPosition = center;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale *= scale;
                foreach (var system in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                foreach (var light in copy.GetComponentsInChildren<Light>(true)) light.range *= scale;
                added++;
            }
            if (added < FlameParts.Length) Plugin.Log.LogWarning($"explosive flames {projectile.name}, found {added} of {FlameParts.Length} parts of {FlameSource}");
            else if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive flames {projectile.name}, size {scale:F1}");
        }

        // 30 percent of the shot as fired: pierce and fire from the combat damage, the shot's own chop and pickaxe cut the same way.
        internal static HitData.DamageTypes ImpactDamage(HitData.DamageTypes shot, float pierceShare, float fireShare, float toolShare)
        {
            float combat = Combat(shot);
            return new HitData.DamageTypes { m_pierce = combat * pierceShare, m_fire = combat * fireShare, m_chop = shot.m_chop * toolShare, m_pickaxe = shot.m_pickaxe * toolShare };
        }

        // the blast from the full draw shot: blunt and fire from its combat damage, chop and pickaxe as shares of it.
        internal static HitData.DamageTypes BlastDamage(HitData.DamageTypes fullShot, float bluntShare, float fireShare, float chopShare, float pickaxeShare)
        {
            float combat = Combat(fullShot);
            return new HitData.DamageTypes { m_blunt = combat * bluntShare, m_fire = combat * fireShare, m_chop = combat * chopShare, m_pickaxe = combat * pickaxeShare };
        }

        internal static float Combat(HitData.DamageTypes damage)
        {
            return damage.m_damage + damage.m_blunt + damage.m_slash + damage.m_pierce + damage.m_fire + damage.m_frost
                + damage.m_lightning + damage.m_poison + damage.m_spirit + damage.m_nonPlayer;
        }

        // the game multiplies a bow shot by the draw, this undoes it with a floor for a near zero draw.
        internal static float FullDrawFactor(float draw)
        {
            return 1f / System.Math.Max(draw, MinimumDraw);
        }

        // full damage inside the core, the aftershock share out to the radius. plain math for the offline check.
        internal static float ZoneScale(float distance, float core, float radius, float aftershock)
        {
            return distance <= core ? 1f : distance <= radius ? aftershock : 0f;
        }

        // the game's distance curve for the same zones, on distance divided by the radius.
        private static AnimationCurve Curve(float core, float radius, float aftershock)
        {
            float edge = Mathf.Clamp(core / radius, 0.01f, 0.98f);
            return new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(edge, 1f, 0f, 0f),
                new Keyframe(edge + 0.01f, aftershock, 0f, 0f), new Keyframe(1f, aftershock, 0f, 0f));
        }

        // the owner's fuse: counts down while stuck, then blows. a shot that never hit or hit water is a dud.
        internal static void Tick(Projectile projectile, ShotState st)
        {
            if (st.Exploded) return;
            if (!st.Stuck)
            {
                if (!st.Dud && projectile.m_ttl <= Time.fixedDeltaTime)
                {
                    st.Dud = true;
                    if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("explosive dud, flight ended without a hit");
                }
                return;
            }
            st.LastPosition = projectile.transform.position;
            st.Fuse -= Time.fixedDeltaTime;
            if (st.Fuse > 0f) return;
            Blow(st, st.LastPosition);
            projectile.m_ttl = 0.0001f;
        }

        // the stuck shot went away before its fuse ended (the creature it sat in was removed): the blast happens where it was.
        internal static void Removed(Projectile projectile, ShotState st)
        {
            if (st.Exploded || !st.Stuck || quitting || ZNetScene.instance == null || Game.instance == null || Game.instance.IsShuttingDown()) return;
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive shot removed during the fuse, {st.Fuse:F2} s left, blast at the last place");
            Blow(st, st.LastPosition);
        }

        private static void Blow(ShotState st, Vector3 position)
        {
            st.Exploded = true;
            var prefab = st.Tier?.Blast;
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            var aoe = go.GetComponent<Aoe>();
            aoe.Setup(st.Owner, Vector3.zero, aoe.m_hitNoise, null, null, st.Ammo);
            bool early = st.Tier.Early;
            bool plains = st.Tier.Plains;
            float core = early ? (st.Bolt ? Plugin.ExplosiveEarlyBoltCoreRadius.Value : Plugin.ExplosiveEarlyArrowCoreRadius.Value)
                : plains ? (st.Bolt ? Plugin.ExplosivePlainsBoltCoreRadius.Value : Plugin.ExplosivePlainsArrowCoreRadius.Value)
                : st.Bolt ? Plugin.ExplosiveBoltCoreRadius.Value : Plugin.ExplosiveArrowCoreRadius.Value;
            float radius = Mathf.Max(core, early ? (st.Bolt ? Plugin.ExplosiveEarlyBoltRadius.Value : Plugin.ExplosiveEarlyArrowRadius.Value)
                : plains ? (st.Bolt ? Plugin.ExplosivePlainsBoltRadius.Value : Plugin.ExplosivePlainsArrowRadius.Value)
                : st.Bolt ? Plugin.ExplosiveBoltRadius.Value : Plugin.ExplosiveArrowRadius.Value);
            aoe.m_radius = radius;
            aoe.m_scaleDamageByDistance = true;
            aoe.m_distanceScaleCurve = Curve(core, radius, Plugin.ExplosiveAftershockShare.Value);
            aoe.m_damage = st.BlastDamage;
            aoe.m_attackForce = st.Bolt ? Plugin.ExplosiveBoltPush.Value : Plugin.ExplosiveArrowPush.Value;
            aoe.m_toolTier = early ? Plugin.ExplosiveEarlyPickaxeTier.Value : plains ? Plugin.ExplosivePlainsPickaxeTier.Value : Plugin.ExplosivePickaxeTier.Value;
            var marker = go.GetComponent<AmmoExplosiveBlast>();
            marker.Core = core;
            marker.AxeTier = early ? Plugin.ExplosiveEarlyAxeTier.Value : plains ? Plugin.ExplosivePlainsAxeTier.Value : Plugin.ExplosiveAxeTier.Value;
            marker.PickaxeTier = aoe.m_toolTier;
            marker.Protected = Plugin.ExplosiveBaseProtection.Value && CraftingStation.HaveBuildStationInRange(WorkbenchName, position) != null;
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive blast {(early ? "early " : plains ? "plains " : "")}{(st.Bolt ? "bolt" : "arrow")} at {position.ToString("F1")}, core {core:F1} m, radius {radius:F1} m, workbench protected {marker.Protected} [{Shots.Describe(st.BlastDamage)}]");
        }

        // inside a workbench's range the blast leaves buildings, trees, rocks and ore alone. creatures are always hit.
        internal static bool AllowHit(Aoe aoe, Collider collider)
        {
            var marker = aoe.GetComponent<AmmoExplosiveBlast>();
            if (marker == null || collider == null) return true;
            var creature = collider.GetComponentInParent<Character>();
            if (creature == null && marker.Protected)
            {
                if (Plugin.LogCounters.Value && marker.Skipped++ == 0) Plugin.Log.LogInfo("explosive blast skipped props inside a workbench range, first " + collider.name);
                return false;
            }
            if (Plugin.LogCounters.Value && hitLogs < HitLogLimit)
            {
                hitLogs++;
                var target = creature != null ? creature.transform : collider.transform;
                float distance = Vector3.Distance(target.position, aoe.transform.position);
                float scale = aoe.m_distanceScaleCurve.Evaluate(Mathf.Clamp01(distance / aoe.m_radius));
                string who = creature != null ? $"{creature.m_name} id {creature.GetInstanceID()} health {creature.GetHealth():F0} of {creature.GetMaxHealth():F0}" : collider.name;
                Plugin.Log.LogInfo($"blast hit {who}, distance {distance:F2} m, scale {scale:F2}, zone {(distance <= marker.Core ? "core" : "aftershock")}");
            }
            // trees, logs and stumps take the axe tier, rocks, ore and the rest the pickaxe tier
            aoe.m_toolTier = creature == null && IsWood(collider) ? marker.AxeTier : marker.PickaxeTier;
            return true;
        }

        internal static bool IsWood(Collider collider)
        {
            var destructible = collider.GetComponentInParent<Destructible>();
            return collider.GetComponentInParent<TreeBase>() != null || collider.GetComponentInParent<TreeLog>() != null
                || destructible != null && destructible.m_destructibleType == DestructibleType.Tree;
        }

        // every player hears the fuse once the shot sticks.
        internal static void Heard(Projectile projectile)
        {
            if (projectile.GetComponent<AmmoExplosiveFuse>() == null) return;
            AmmoAssets.PlayFuse(projectile.transform);
        }
    }

    // marks an explosive shot. on the owner it ends the fuse with a blast when the shot is removed early.
    public sealed class AmmoExplosiveFuse : MonoBehaviour
    {
        private void OnDestroy()
        {
            var projectile = GetComponent<Projectile>();
            if (projectile != null) Shots.ExplosiveRemoved(projectile);
        }
    }

    // marks a blast of this mod and carries the owner's workbench check.
    public sealed class AmmoExplosiveBlast : MonoBehaviour
    {
        internal bool Protected;
        internal float Core;
        internal int Skipped;
        internal int AxeTier;
        internal int PickaxeTier;
    }
}
