using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace AmmoVariants
{
    // state of one shot of our ammo, made when the projectile is set up.
    internal sealed class ShotState
    {
        internal Items.AmmoKind Kind;
        internal bool Pierce;
        internal Vector3 Origin;
        internal int Passed;
        internal readonly HashSet<Character> Creatures = new HashSet<Character>();
        internal bool NewHit;
        internal HitData.DamageTypes BaseDamage;
        // explosive shots: the fuse after the hit and the blast's damage from the full draw shot
        internal ExplosiveTier Tier;
        internal bool Bolt;
        internal bool Stuck;
        internal bool Dud;
        internal bool Exploded;
        internal float Fuse;
        internal Vector3 LastPosition;
        internal HitData.DamageTypes BlastDamage;
        internal Character Owner;
        internal ItemDrop.ItemData Ammo;
    }

    // the scatter, piercing and energy rules. all hooks are on the vanilla shot path.
    internal static class Shots
    {
        internal const string BsmGuid = "com.example.bowsecondarymultishot";

        // true while the burst of a secondary attack is fired (BowSecondaryMultishot's volley).
        internal static bool FiringSecondary;

        internal static bool BsmInstalled;

        private static readonly ConditionalWeakTable<Projectile, ShotState> States =
            new ConditionalWeakTable<Projectile, ShotState>();

        private static readonly AccessTools.FieldRef<Projectile, bool> DidHit =
            AccessTools.FieldRefAccess<Projectile, bool>("m_didHit");

        private static readonly AccessTools.FieldRef<Projectile, Vector3> Vel =
            AccessTools.FieldRefAccess<Projectile, Vector3>("m_vel");

        private static readonly AccessTools.FieldRef<Projectile, HashSet<Collider>> HitList =
            AccessTools.FieldRefAccess<Projectile, HashSet<Collider>>("m_hitList");

        private static readonly AccessTools.FieldRef<Attack, ItemDrop.ItemData> AmmoItemRef =
            AccessTools.FieldRefAccess<Attack, ItemDrop.ItemData>("m_ammoItem");

        private static readonly AccessTools.FieldRef<Attack, ItemDrop.ItemData> WeaponRef =
            AccessTools.FieldRefAccess<Attack, ItemDrop.ItemData>("m_weapon");
        private static readonly AccessTools.FieldRef<Attack, Humanoid> CharacterRef = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
        private static readonly AccessTools.FieldRef<Attack, float> DrawRef = AccessTools.FieldRefAccess<Attack, float>("m_attackDrawPercentage");

        // the bow draw of the burst being fired, 1 for weapons without a draw.
        private static float burstDraw = 1f;
        private static readonly AccessTools.FieldRef<Humanoid, bool> SecondaryRef = AccessTools.FieldRefAccess<Humanoid, bool>("m_currentAttackIsSecondary");

        private static readonly FieldInfo RaySolids = AccessTools.Field(typeof(Projectile), "s_rayMaskSolids");

        private static FieldInfo bsmPiercingArrows;
        private static FieldInfo bsmPierceNormal;
        private static bool burstSoundPending;
        internal const string SoundKey = "AmmoVariants.Sound";
        private static readonly AccessTools.FieldRef<Projectile, Character> OwnerRef = AccessTools.FieldRefAccess<Projectile, Character>("m_owner");
        private static readonly System.Func<Projectile, IDestructible, bool> ValidTarget =
            AccessTools.MethodDelegate<System.Func<Projectile, IDestructible, bool>>(AccessTools.Method(typeof(Projectile), "IsValidTarget"));

        internal static void Init()
        {
            BsmInstalled = Chainloader.PluginInfos.ContainsKey(BsmGuid);
            Plugin.Log.LogInfo(BsmInstalled
                ? "BowSecondaryMultishot found, piercing is checked per shot"
                : "BowSecondaryMultishot not installed");
            if (Chainloader.PluginInfos.ContainsKey("Searica.Valheim.ProjectileTweaks"))
                Plugin.Log.LogInfo("ProjectileTweaks found, original weapon spread and velocity remain active alongside the scatter cone");
        }

        // the mod's piercing setting of BowSecondaryMultishot for this shot, read when the arrow launches.
        internal static bool BsmPierces(bool secondary)
        {
            if (!BsmInstalled) return false;
            if (!FindBsmFields()) return true;

            var field = secondary ? bsmPiercingArrows : bsmPierceNormal;
            var entry = field.GetValue(null);
            var value = entry.GetType().GetProperty("Value").GetValue(entry, null);
            return value is bool on && on;
        }

        private static bool FindBsmFields()
        {
            if (bsmPiercingArrows != null && bsmPierceNormal != null) return true;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "BowSecondaryMultishot") continue;
                var type = asm.GetType("BowSecondaryMultishot.BowSecondaryMultishotPlugin");
                if (type == null) return false;
                bsmPiercingArrows = type.GetField("PiercingArrows", flags);
                bsmPierceNormal = type.GetField("PierceNormalShots", flags);
                return bsmPiercingArrows != null && bsmPierceNormal != null;
            }
            return false;
        }

        internal static Items.AmmoKind KindOf(ItemDrop.ItemData ammo)
        {
            return FiringSecondary ? Items.AmmoKind.None : Items.KindOf(ammo);
        }

        internal static bool IsSecondary(Attack attack)
        {
            var character = CharacterRef(attack);
            if (character != null) return SecondaryRef(character);
            var weapon = WeaponRef(attack);
            return weapon != null && weapon.m_shared != null && ReferenceEquals(weapon.m_shared.m_secondaryAttack, attack);
        }

        internal static void BeginBurst(Attack attack)
        {
            FiringSecondary = BsmInstalled && IsSecondary(attack);
            burstSoundPending = true;
            burstDraw = attack.m_bowDraw ? DrawRef(attack) : 1f;
        }

        internal static void EndBurst()
        {
            FiringSecondary = false;
            burstSoundPending = false;
            burstDraw = 1f;
        }

        internal static int ConsumeSound(Items.AmmoKind kind, bool crossbow)
        {
            int code = burstSoundPending ? 2 + ((int)kind - 1) * 2 + (crossbow ? 1 : 0) : 1;
            burstSoundPending = false;
            return code;
        }

        // the pellet count of a burst: scatter ammo fires the configured pellets, everything else the weapon's count.
        internal static int ProjectileCount(Attack attack)
        {
            if (FiringSecondary || Items.KindOf(AmmoItemRef(attack)) != Items.AmmoKind.Scatter)
            {
                return attack.m_projectiles;
            }
            return IsCrossbow(WeaponRef(attack)) ? Plugin.ScatterPellets.Value : Plugin.ScatterBowPellets.Value;
        }

        internal static IEnumerable<CodeInstruction> TranspileBurst(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(typeof(Attack), "m_projectiles");
            var method = AccessTools.Method(typeof(Shots), nameof(ProjectileCount));
            var matcher = new CodeMatcher(instructions);
            matcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, field));
            while (matcher.IsValid)
            {
                matcher.Set(OpCodes.Call, method);
                matcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, field));
            }
            return TranspileRelease(matcher.InstructionEnumeration());
        }

        // energy flies without gravity, so its aim goes straight to the crosshair point. any aim offset from other mods is dropped.
        internal static void AimEnergy(Attack attack, Vector3 spawnPoint, ref Vector3 aimDir)
        {
            if (attack.m_useCharacterFacing || KindOf(AmmoItemRef(attack)) != Items.AmmoKind.Energy) return;
            var character = CharacterRef(attack);
            if (character == null) return;
            var aim = character.GetAimDir(spawnPoint);
            if (Plugin.LogCounters.Value && Vector3.Angle(aim, aimDir) > 0.01f) Plugin.Log.LogInfo($"energy aim reset by {Vector3.Angle(aim, aimDir):F2} degrees");
            aimDir = aim;
        }

        internal static bool IsVariantAttack(Attack attack)
        {
            return !(BsmInstalled && IsSecondary(attack)) && Items.KindOf(AmmoItemRef(attack)) != Items.AmmoKind.None;
        }

        internal static IEnumerable<CodeInstruction> TranspileRelease(IEnumerable<CodeInstruction> instructions)
        {
            var target = AccessTools.Method(typeof(EffectList), nameof(EffectList.Create));
            var wrapper = AccessTools.Method(typeof(AmmoAssets), nameof(AmmoAssets.CreateRelease));
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(target))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = wrapper;
                }
                yield return instruction;
            }
        }

        // swaps the solids mask of the raycasts for energy projectiles.
        internal static IEnumerable<CodeInstruction> TranspileUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var method = AccessTools.Method(typeof(Shots), nameof(RayMask));
            var matcher = new CodeMatcher(instructions);
            matcher.MatchStartForward(new CodeMatch(OpCodes.Ldsfld, RaySolids));
            while (matcher.IsValid)
            {
                matcher.Set(OpCodes.Call, method);
                matcher.Insert(new CodeInstruction(OpCodes.Ldarg_0));
                matcher.MatchStartForward(new CodeMatch(OpCodes.Ldsfld, RaySolids));
            }
            var output = new List<CodeInstruction>(matcher.InstructionEnumeration());
            for (int i = 0; i < output.Count; i++)
            {
                if (!(output[i].operand is MethodInfo target) || target.DeclaringType != typeof(Physics)) continue;
                string helper = target.Name == "RaycastAll" ? nameof(RaycastAll) : target.Name == "SphereCastAll" ? nameof(SphereCastAll) : null;
                if (helper == null) continue;
                output.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0));
                output[i].operand = AccessTools.Method(typeof(Shots), helper);
            }
            return output;
        }

        internal static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float distance, int mask, Projectile projectile)
        {
            var hits = Physics.RaycastAll(origin, direction, distance, mask);
            if (States.TryGetValue(projectile, out var state) && state.Kind == Items.AmmoKind.Energy)
                return Widen(projectile, origin, direction, distance, mask, System.Array.FindAll(hits, hit => CharacterOf(hit.collider) != null));
            return Widen(projectile, origin, direction, distance, mask, hits);
        }

        internal static RaycastHit[] SphereCastAll(Vector3 origin, float radius, Vector3 direction, float distance, int mask, Projectile projectile)
        {
            if (States.TryGetValue(projectile, out var state) && (state.Pierce || state.Kind == Items.AmmoKind.Energy))
            {
                if (state.Kind == Items.AmmoKind.Energy) return RaycastAll(origin, direction, distance, mask, projectile);
                return Widen(projectile, origin, direction, distance, mask, Physics.RaycastAll(origin, direction, distance, mask));
            }
            return Physics.SphereCastAll(origin, radius, direction, distance, mask);
        }

        private static RaycastHit[] Widen(Projectile projectile, Vector3 origin, Vector3 direction, float distance, int mask, RaycastHit[] hits)
        {
            if (!States.TryGetValue(projectile, out var state) || (!state.Pierce && state.Kind != Items.AmmoKind.Energy)) return hits;
            var owner = OwnerRef(projectile);
            bool energy = state.Kind == Items.AmmoKind.Energy;
            return PiercingCollision.Expand(origin, direction, distance, mask, hits, energy ? Plugin.EnergyEnemyRadius.Value : Plugin.PierceEnemyRadius.Value,
                collider =>
                {
                    var character = CharacterOf(collider);
                    return character != null && owner != null && BaseAI.IsEnemy(owner, character) && ValidTarget(projectile, character);
                }, collider => CharacterOf(collider) != null, energy);
        }

        internal static bool IsCrossbow(ItemDrop.ItemData weapon)
        {
            return weapon != null && (weapon.m_shared.m_ammoType == "$ammo_bolts" || weapon.m_shared.m_skillType == Skills.SkillType.Crossbows);
        }

        internal static int RayMask(Projectile projectile)
        {
            return (int)RaySolids.GetValue(null);
        }

        // called after the vanilla Setup, the projectile has its velocity and damage now.
        internal static void OnSetup(Projectile projectile, ItemDrop.ItemData ammo, ItemDrop.ItemData weapon)
        {
            projectile.GetComponent<AmmoNativeEffects>()?.Begin();
            projectile.GetComponent<AmmoTrailLifetime>()?.Begin();
            var native = projectile.m_damage;
            var kind = KindOf(ammo);
            if (kind == Items.AmmoKind.None) { LogSetup(ammo, weapon, kind, native, native); States.Remove(projectile); return; }
            var st = new ShotState { Kind = kind, Origin = projectile.transform.position };
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"setup {ammo?.m_shared?.m_name} kind {kind}");
            // the whole numbers of the item text, times what skill, draw and effects did to this shot
            var shot = BaseShot(weapon, ammo != null ? ammo.GetDamage() : new HitData.DamageTypes());
            float fired = FiredFactor(native, shot);

            switch (kind)
            {
                case Items.AmmoKind.Energy:
                    // energy passes creatures, the vanilla terrain stop is set so only AfterHit decides a stop.
                    projectile.m_onlyStopOnTerrain = true;
                    projectile.m_damage = fired > 0f ? Scaled(PerProjectile(kind, shot, false), fired)
                        : EnergyDamage(projectile.m_damage, Plugin.EnergyLightningShare.Value, Plugin.EnergyPierceShare.Value, Plugin.EnergyFireShare.Value);
                    break;
                case Items.AmmoKind.Scatter:
                {
                    bool crossbow = IsCrossbow(weapon);
                    float share = crossbow ? Plugin.ScatterShareCrossbow.Value : Plugin.ScatterShareBow.Value;
                    projectile.m_damage = fired > 0f ? Scaled(PerProjectile(kind, shot, crossbow), fired) : Scaled(projectile.m_damage, share);
                    // one volley gives the weapon's skill gain once: every pellet takes its share.
                    projectile.m_raiseSkillAmount /= crossbow ? Plugin.ScatterPellets.Value : Plugin.ScatterBowPellets.Value;
                    projectile.m_attackForce *= share;
                    projectile.m_adrenaline /= crossbow ? Plugin.ScatterPellets.Value : Plugin.ScatterBowPellets.Value;

                    var velocity = Vel(projectile);
                    float speed = velocity.magnitude;
                    if (speed > 0f)
                    {
                        Vel(projectile) = Cone(velocity / speed, crossbow ? Plugin.ScatterConeHalfAngle.Value : Plugin.ScatterBowConeHalfAngle.Value) * speed;
                    }
                    break;
                }
                case Items.AmmoKind.Explosive:
                {
                    // the impact follows the draw like any shot, the blast takes the shot as if fully drawn
                    st.Tier = Explosive.TierOf(ammo?.m_shared?.m_attack?.m_attackProjectile);
                    st.Bolt = st.Tier?.Bolt ?? ammo?.m_shared?.m_ammoType == "$ammo_bolts";
                    st.Owner = OwnerRef(projectile);
                    st.Ammo = ammo;
                    st.BlastDamage = fired > 0f ? Scaled(BlastBase(shot), fired * Explosive.FullDrawFactor(burstDraw))
                        : Explosive.BlastDamage(Scaled(native, Explosive.FullDrawFactor(burstDraw)), Plugin.ExplosiveBlastBluntShare.Value,
                        Plugin.ExplosiveBlastFireShare.Value, Plugin.ExplosiveBlastChopShare.Value, Plugin.ExplosiveBlastPickaxeShare.Value);
                    projectile.m_damage = fired > 0f ? Scaled(PerProjectile(kind, shot, st.Bolt), fired)
                        : Explosive.ImpactDamage(native, Plugin.ExplosiveImpactPierceShare.Value, Plugin.ExplosiveImpactFireShare.Value, Plugin.ExplosiveImpactToolShare.Value);
                    projectile.m_stayTTL = Plugin.ExplosiveFuse.Value + Explosive.StayAfterFuse;
                    // slower and heavier than whatever the game and other mods gave the shot
                    Vel(projectile) *= st.Bolt ? Plugin.ExplosiveBoltSpeedFactor.Value : st.Tier != null && st.Tier.Early ? Plugin.ExplosiveEarlyArrowSpeedFactor.Value
                        : st.Tier != null && st.Tier.Plains ? Plugin.ExplosivePlainsArrowSpeedFactor.Value : Plugin.ExplosiveArrowSpeedFactor.Value;
                    projectile.m_gravity = st.Bolt ? Plugin.ExplosiveBoltGravity.Value : Plugin.ExplosiveArrowGravity.Value;
                    if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"explosive setup {(st.Tier != null && st.Tier.Early ? "early " : st.Tier != null && st.Tier.Plains ? "plains " : "")}{(st.Bolt ? "bolt" : "arrow")}, draw {burstDraw:F2}, speed {Vel(projectile).magnitude:F1} m/s, gravity {projectile.m_gravity:F1}, blast [{Describe(st.BlastDamage)}]");
                    break;
                }
                case Items.AmmoKind.Piercing:
                    // the vanilla pass rule stays for shots that BowSecondaryMultishot pierces itself.
                    st.Pierce = !BsmPierces(false);
                    if (st.Pierce)
                    {
                        projectile.m_onlyStopOnTerrain = true;
                        projectile.m_damage = fired > 0f ? Scaled(PerProjectile(kind, shot, false), fired) : PiercingDamage(projectile.m_damage, Plugin.PierceDamageShare.Value);
                        if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"pierce initial damage share {Plugin.PierceDamageShare.Value:F2}, limit {Plugin.PierceMaxCreatures.Value}, creature loss {Plugin.PierceLossPerCreature.Value:F2}");
                    }
                    break;
            }

            st.BaseDamage = projectile.m_damage;
            LogSetup(ammo, weapon, kind, native, st.BaseDamage);
            States.Remove(projectile);
            States.Add(projectile, st);
            var view = projectile.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner())
            {
                int sound = ConsumeSound(kind, IsCrossbow(weapon));
                // early explosive ammo has its own lighter firing sounds two codes up, plains ammo its middle ones four up
                if (sound >= 2 && st.Tier != null && st.Tier.Early) sound += 2;
                else if (sound >= 2 && st.Tier != null && st.Tier.Plains) sound += 4;
                view.GetZDO().Set(SoundKey, sound);
                if (sound >= 2) view.GetZDO().Set(SoundKey + ".Origin", st.Origin);
                if (sound >= 2)
                {
                    AmmoAssets.Play(sound, st.Origin);
                    var flight = projectile.GetComponent<AmmoFlight>();
                    if (flight != null) flight.Played();
                }
            }
        }

        // energy damage falls with the distance from the shot's origin, nothing else changes it.
        internal static bool BeforeHit(Projectile projectile, Collider collider, Vector3 hitPoint, bool water)
        {
            if (!States.TryGetValue(projectile, out var st))
            {
                if (Plugin.LogCounters.Value && Items.KindOf(AmmoOf(projectile)) != Items.AmmoKind.None)
                    Plugin.Log.LogWarning($"hit on {collider.name} without a shot state");
                return true;
            }

            var hits = HitList(projectile);
            st.NewHit = hits == null || !hits.Contains(collider);
            var creature = collider != null ? CharacterOf(collider) : null;
            if ((st.Pierce || st.Kind == Items.AmmoKind.Energy) && creature != null)
            {
                if (st.Creatures.Contains(creature)) return false;
                if (!ValidTarget(projectile, creature)) { st.NewHit = false; return true; }
                st.NewHit &= st.Creatures.Add(creature);
            }
            if (st.Pierce && st.NewHit && (creature == null || PierceEnds(st.Passed + 1, Plugin.PierceMaxCreatures.Value)))
                projectile.m_onlyStopOnTerrain = false;

            // an explosive shot sticks where it hits and its fuse starts, water drowns it
            if (st.Kind == Items.AmmoKind.Explosive && !st.Stuck && !st.Dud)
            {
                st.Dud = water;
                st.Stuck = !water;
                st.Fuse = Plugin.ExplosiveFuse.Value;
                st.LastPosition = hitPoint;
                if (Plugin.LogCounters.Value) Plugin.Log.LogInfo(water ? "explosive dud, the shot hit water" : $"explosive fuse {st.Fuse:F2} s, stuck in {(creature != null ? creature.m_name : collider != null ? collider.name : "nothing")}");
            }

            if (st.Kind != Items.AmmoKind.Energy) { LogHit(projectile, st, creature); return true; }

            float distance = Vector3.Distance(hitPoint, st.Origin);
            if (distance > Plugin.EnergyRange.Value) return false;
            float factor = EnergyFactor(distance, Plugin.EnergyRange.Value, Plugin.EnergyDamageAtRange.Value);
            projectile.m_damage = Scaled(st.BaseDamage, factor);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"energy hit at {distance:F1} m, factor {factor:F2}, fire {projectile.m_damage.m_fire:F1}");
            LogHit(projectile, st, creature);
            return true;
        }

        // creatures pass with a loss per creature, anything else stops the shot.
        internal static void AfterHit(Projectile projectile, Collider collider)
        {
            if (!States.TryGetValue(projectile, out var st) || !st.Pierce || !st.NewHit) return;

            var creature = CharacterOf(collider);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"pierce hit on {collider.name}, creature {creature != null}, passed {st.Passed}");
            if (creature != null)
            {
                // a creature has several colliders, only its first one counts as a pass.
                st.Passed++;
                projectile.m_damage = Scaled(projectile.m_damage, 1f - Plugin.PierceLossPerCreature.Value);
                if (PierceEnds(st.Passed, Plugin.PierceMaxCreatures.Value)) DidHit(projectile) = true;
            }
            else
            {
                DidHit(projectile) = true;
            }
            if (DidHit(projectile)) projectile.GetComponent<AmmoTrailLifetime>()?.Detach();
        }

        // a creature collider has the Character on its own object or on its rigidbody's object.
        private static Character CharacterOf(Collider collider)
        {
            if (collider == null) return null;
            var character = collider.GetComponentInParent<Character>();
            if (character != null) return character;
            var body = collider.attachedRigidbody;
            return body != null ? body.GetComponent<Character>() : null;
        }

        private static readonly AccessTools.FieldRef<Projectile, ItemDrop.ItemData> AmmoOfRef =
            AccessTools.FieldRefAccess<Projectile, ItemDrop.ItemData>("m_ammo");

        private static ItemDrop.ItemData AmmoOf(Projectile projectile)
        {
            return AmmoOfRef(projectile);
        }

        // energy ends at the range. the ttl is set to a tiny value so the vanilla update destroys it.
        internal static bool FlightEnded(Projectile projectile)
        {
            return DidHit(projectile) || projectile.m_ttl <= Time.fixedDeltaTime;
        }

        internal static void BeforeUpdate(Projectile projectile)
        {
            if (!States.TryGetValue(projectile, out var st)) return;
            if (projectile.m_ttl <= Time.fixedDeltaTime) projectile.GetComponent<AmmoTrailLifetime>()?.Detach();
            if (st.Kind == Items.AmmoKind.Explosive) { Explosive.Tick(projectile, st); return; }
            if (st.Kind != Items.AmmoKind.Energy) return;

            if (EnergyEnded(Vector3.Distance(projectile.transform.position, st.Origin), Plugin.EnergyRange.Value))
            {
                projectile.GetComponent<AmmoTrailLifetime>()?.Detach();
                projectile.m_ttl = 0.0001f;
            }
        }

        // a stuck explosive shot removed before its fuse ended, only the owner holds its state.
        internal static void ExplosiveRemoved(Projectile projectile)
        {
            if (States.TryGetValue(projectile, out var st) && st.Kind == Items.AmmoKind.Explosive) Explosive.Removed(projectile, st);
        }

        // damage share at a distance: 1 at 0 m, atRange at the range, linear in between.
        internal static float EnergyFactor(float distance, float range, float atRange)
        {
            float t = System.Math.Min(System.Math.Max(distance / range, 0f), 1f);
            return 1f + (atRange - 1f) * t;
        }

        internal static bool EnergyEnded(float distance, float range)
        {
            return distance >= range;
        }

        // a pierce ends when the creature count reaches the limit.
        internal static bool PierceEnds(int passed, int maxCreatures)
        {
            return passed >= maxCreatures;
        }

        internal static HitData.DamageTypes Scaled(HitData.DamageTypes damage, float factor)
        {
            var copy = damage;
            copy.Modify(factor);
            return copy;
        }

        internal static HitData.DamageTypes EnergyDamage(HitData.DamageTypes damage, float lightningShare, float pierceShare, float fireShare)
        {
            float total = damage.m_damage + damage.m_blunt + damage.m_slash + damage.m_pierce
                + damage.m_fire + damage.m_frost + damage.m_lightning + damage.m_poison + damage.m_spirit + damage.m_nonPlayer;
            return new HitData.DamageTypes { m_lightning = total * lightningShare, m_pierce = total * pierceShare, m_fire = total * fireShare };
        }

        internal static HitData.DamageTypes PiercingDamage(HitData.DamageTypes damage, float factor)
        {
            var copy = Scaled(damage, factor);
            copy.m_chop = damage.m_chop;
            copy.m_pickaxe = damage.m_pickaxe;
            return copy;
        }

        // the shot before skill, draw and effects: the weapon's damage plus the ammo's, added as the game does.
        internal static HitData.DamageTypes BaseShot(ItemDrop.ItemData weapon, HitData.DamageTypes ammo)
        {
            var shot = weapon != null ? weapon.GetDamage() : new HitData.DamageTypes();
            shot.Add(ammo, 1);
            return shot;
        }

        // every channel rounded up to a whole point, the slack keeps float noise like 28.00001 at 28.
        internal static HitData.DamageTypes Whole(HitData.DamageTypes damage)
        {
            float Up(float value) => value > 0f ? (float)System.Math.Ceiling(value - WholeSlack) : value;
            return new HitData.DamageTypes
            {
                m_damage = Up(damage.m_damage), m_blunt = Up(damage.m_blunt), m_slash = Up(damage.m_slash), m_pierce = Up(damage.m_pierce),
                m_chop = Up(damage.m_chop), m_pickaxe = Up(damage.m_pickaxe), m_fire = Up(damage.m_fire), m_frost = Up(damage.m_frost),
                m_lightning = Up(damage.m_lightning), m_poison = Up(damage.m_poison), m_spirit = Up(damage.m_spirit), m_nonPlayer = Up(damage.m_nonPlayer)
            };
        }

        private const float WholeSlack = 0.001f;

        // one projectile from the base shot in whole points, the same numbers the item text shows.
        internal static HitData.DamageTypes PerProjectile(Items.AmmoKind kind, HitData.DamageTypes shot, bool crossbow)
        {
            switch (kind)
            {
                case Items.AmmoKind.Scatter:
                    return Whole(Scaled(shot, crossbow ? Plugin.ScatterShareCrossbow.Value : Plugin.ScatterShareBow.Value));
                case Items.AmmoKind.Piercing:
                    return Whole(PiercingDamage(shot, Plugin.PierceDamageShare.Value));
                case Items.AmmoKind.Energy:
                    return Whole(EnergyDamage(shot, Plugin.EnergyLightningShare.Value, Plugin.EnergyPierceShare.Value, Plugin.EnergyFireShare.Value));
                case Items.AmmoKind.Explosive:
                    return Whole(Explosive.ImpactDamage(shot, Plugin.ExplosiveImpactPierceShare.Value, Plugin.ExplosiveImpactFireShare.Value, Plugin.ExplosiveImpactToolShare.Value));
                default:
                    return shot;
            }
        }

        // the blast core from the full draw base shot in whole points.
        internal static HitData.DamageTypes BlastBase(HitData.DamageTypes shot)
        {
            return Whole(Explosive.BlastDamage(shot, Plugin.ExplosiveBlastBluntShare.Value, Plugin.ExplosiveBlastFireShare.Value,
                Plugin.ExplosiveBlastChopShare.Value, Plugin.ExplosiveBlastPickaxeShare.Value));
        }

        // how far skill, draw and effects moved the fired shot from its base, by combat damage. 0 without a base.
        internal static float FiredFactor(HitData.DamageTypes fired, HitData.DamageTypes shot)
        {
            float start = Explosive.Combat(shot);
            return start > 0f ? Explosive.Combat(fired) / start : 0f;
        }

        // the item text of our ammo: the vanilla damage lines become one projectile, count x value, of the ammo's own damage like a
        // vanilla arrow's (scatter arrows as a bow's spread, scatter bolts as a crossbow's). with the bow or crossbow that shoots it
        // each line adds the value with the weapon's damage in, in a light blue bracket. explosive adds one blast line and its radius.
        internal static string Tooltip(ItemDrop.ItemData item, int quality, float worldLevel, string text)
        {
            var kind = Items.KindOf(item);
            if (kind == Items.AmmoKind.None || string.IsNullOrEmpty(text)) return text;
            var ammo = item.GetDamage(quality, worldLevel);
            string vanilla = ammo.GetTooltipString(item.m_shared.m_skillType);
            int at = string.IsNullOrEmpty(vanilla) ? -1 : text.IndexOf(vanilla, System.StringComparison.Ordinal);
            if (at < 0) return text;
            var player = Player.m_localPlayer;
            var weapon = player != null ? player.GetCurrentWeapon() : null;
            if (weapon != null && weapon.m_shared.m_ammoType != item.m_shared.m_ammoType) weapon = null;
            bool crossbow = item.m_shared.m_ammoType == "$ammo_bolts";
            float low = 1f, high = 1f;
            if (player != null) player.GetSkills().GetRandomSkillRange(out low, out high, item.m_shared.m_skillType);
            var shot = BaseShot(null, ammo);
            var one = PerProjectile(kind, shot, crossbow);
            var armedShot = weapon != null ? BaseShot(weapon, ammo) : new HitData.DamageTypes();
            var armed = weapon != null ? PerProjectile(kind, armedShot, IsCrossbow(weapon)) : new HitData.DamageTypes();
            string tag = weapon == null ? null : IsCrossbow(weapon) ? "crossbow" : "bow";
            int count = kind == Items.AmmoKind.Scatter ? (crossbow ? Plugin.ScatterPellets.Value : Plugin.ScatterBowPellets.Value) : 1;
            var lines = new System.Text.StringBuilder();
            DamageLines(lines, one, armed, tag, count, low, high);
            if (kind == Items.AmmoKind.Explosive)
            {
                var tier = Explosive.TierOf(item.m_shared.m_attack.m_attackProjectile);
                bool early = tier != null && tier.Early, plains = tier != null && tier.Plains, bolt = tier != null && tier.Bolt;
                float core = early ? (bolt ? Plugin.ExplosiveEarlyBoltCoreRadius.Value : Plugin.ExplosiveEarlyArrowCoreRadius.Value)
                    : plains ? (bolt ? Plugin.ExplosivePlainsBoltCoreRadius.Value : Plugin.ExplosivePlainsArrowCoreRadius.Value)
                    : bolt ? Plugin.ExplosiveBoltCoreRadius.Value : Plugin.ExplosiveArrowCoreRadius.Value;
                float radius = early ? (bolt ? Plugin.ExplosiveEarlyBoltRadius.Value : Plugin.ExplosiveEarlyArrowRadius.Value)
                    : plains ? (bolt ? Plugin.ExplosivePlainsBoltRadius.Value : Plugin.ExplosivePlainsArrowRadius.Value)
                    : bolt ? Plugin.ExplosiveBoltRadius.Value : Plugin.ExplosiveArrowRadius.Value;
                Line(lines, "Blast", Explosive.Combat(BlastBase(shot)), weapon != null ? Explosive.Combat(BlastBase(armedShot)) : 0f, tag, 1, low, high);
                lines.Append("\nBlast radius: <color=orange>").Append(core.ToString("0.##")).Append('-').Append(radius.ToString("0.##")).Append(" m</color>");
            }
            return text.Remove(at, vanilla.Length).Insert(at, lines.ToString());
        }

        // the game's damage lines with the skill range, count x value when a shot has several projectiles, and the weapon's bracket.
        private static void DamageLines(System.Text.StringBuilder lines, HitData.DamageTypes damage, HitData.DamageTypes armed, string tag, int count, float low, float high)
        {
            Line(lines, "$inventory_damage", damage.m_damage, armed.m_damage, tag, count, low, high); Line(lines, "$inventory_blunt", damage.m_blunt, armed.m_blunt, tag, count, low, high);
            Line(lines, "$inventory_slash", damage.m_slash, armed.m_slash, tag, count, low, high); Line(lines, "$inventory_pierce", damage.m_pierce, armed.m_pierce, tag, count, low, high);
            Line(lines, "$inventory_fire", damage.m_fire, armed.m_fire, tag, count, low, high); Line(lines, "$inventory_frost", damage.m_frost, armed.m_frost, tag, count, low, high);
            Line(lines, "$inventory_lightning", damage.m_lightning, armed.m_lightning, tag, count, low, high); Line(lines, "$inventory_poison", damage.m_poison, armed.m_poison, tag, count, low, high);
            Line(lines, "$inventory_spirit", damage.m_spirit, armed.m_spirit, tag, count, low, high);
        }

        // the ammo's own value, then [bow 21] with the equipped weapon (with the count too when only the weapon has this damage)
        private static void Line(System.Text.StringBuilder lines, string label, float value, float armed, string tag, int count, float low, float high)
        {
            bool bracket = tag != null && armed > 0f;
            if (value <= 0f && !bracket) return;
            lines.Append('\n').Append(label).Append(": ");
            if (value > 0f)
                lines.Append("<color=orange>").Append(count > 1 ? count + " x " : "").Append(Mathf.RoundToInt(value))
                    .Append("</color> <color=yellow>(").Append(Mathf.RoundToInt(value * low)).Append('-').Append(Mathf.RoundToInt(value * high)).Append(") </color>");
            if (bracket)
                lines.Append("<color=").Append(BracketColor).Append(">[").Append(tag).Append(' ').Append(value <= 0f && count > 1 ? count + " x " : "")
                    .Append(Mathf.RoundToInt(armed)).Append("]</color>");
        }

        private const string BracketColor = "#8FD3FF";

        private const int OrdinaryLogLimit = 15;
        private const int VariantLogLimitPerAmmo = 20;
        private const int HitLogLimitPerKind = 30;
        private static int ordinaryLogs;
        private static readonly Dictionary<string, int> VariantLogs = new Dictionary<string, int>();
        private static readonly Dictionary<Items.AmmoKind, int> HitLogs = new Dictionary<Items.AmmoKind, int>();

        // counts one more line for a key and says whether it is still inside the limit.
        private static bool WithinLimit<T>(Dictionary<T, int> counts, T key, int limit)
        {
            counts.TryGetValue(key, out int used);
            if (used >= limit) return false;
            counts[key] = used + 1;
            return true;
        }

        // the non-zero channels of a damage set, with the combat and total sums.
        internal static string Describe(HitData.DamageTypes damage)
        {
            var text = new System.Text.StringBuilder();
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            void Add(string label, float value) { if (value != 0f) text.Append(label).Append(' ').Append(value.ToString("F1", culture)).Append(' '); }
            Add("damage", damage.m_damage); Add("blunt", damage.m_blunt); Add("slash", damage.m_slash); Add("pierce", damage.m_pierce);
            Add("chop", damage.m_chop); Add("pickaxe", damage.m_pickaxe); Add("fire", damage.m_fire); Add("frost", damage.m_frost);
            Add("lightning", damage.m_lightning); Add("poison", damage.m_poison); Add("spirit", damage.m_spirit); Add("nonPlayer", damage.m_nonPlayer);
            float total = damage.GetTotalDamage();
            return text.Append("combat ").Append((total - damage.m_chop - damage.m_pickaxe).ToString("F1", culture)).Append(" total ").Append(total.ToString("F1", culture)).ToString();
        }

        // bounded damage lines for arrows and bolts, native input against the value after this mod's scaling.
        private static void LogSetup(ItemDrop.ItemData ammo, ItemDrop.ItemData weapon, Items.AmmoKind kind, HitData.DamageTypes native, HitData.DamageTypes final)
        {
            if (!Plugin.LogCounters.Value) return;
            string type = ammo?.m_shared?.m_ammoType;
            if (kind == Items.AmmoKind.None)
            {
                if (type != "$ammo_arrows" && type != "$ammo_bolts") return;
                if (ordinaryLogs++ >= OrdinaryLogLimit) return;
            }
            else if (!WithinLimit(VariantLogs, ammo?.m_shared?.m_name ?? "", VariantLogLimitPerAmmo)) return;
            Plugin.Log.LogInfo($"damage setup {ammo?.m_shared?.m_name} kind {kind} weapon {weapon?.m_shared?.m_name} native [{Describe(native)}] final [{Describe(final)}]");
        }

        private static void LogHit(Projectile projectile, ShotState st, Character creature)
        {
            if (!Plugin.LogCounters.Value || creature == null || !st.NewHit || !WithinLimit(HitLogs, st.Kind, HitLogLimitPerKind)) return;
            Plugin.Log.LogInfo($"damage hit {st.Kind} on {creature.m_name} id {creature.GetInstanceID()} health {creature.GetHealth():F0} of {creature.GetMaxHealth():F0} [{Describe(projectile.m_damage)}]");
        }

        // a direction inside a cone of the given half angle, uniform over the cone's disc.
        private static Vector3 Cone(Vector3 direction, float halfAngle)
        {
            return ConeDirection(direction, halfAngle, UnityEngine.Random.value, UnityEngine.Random.value);
        }

        // u and v in [0, 1) pick the point on the disc: u the radius, v the angle.
        // plain math, no unity native calls, so the offline check can run it.
        internal static Vector3 ConeDirection(Vector3 direction, float halfAngle, float u, float v)
        {
            var side = Cross(direction, new Vector3(0f, 1f, 0f));
            if (side.x * side.x + side.y * side.y + side.z * side.z < 1e-4f) side = Cross(direction, new Vector3(1f, 0f, 0f));
            float length = (float)System.Math.Sqrt(side.x * side.x + side.y * side.y + side.z * side.z);
            side = new Vector3(side.x / length, side.y / length, side.z / length);

            float tilt = (float)System.Math.Sqrt(u) * halfAngle;
            float spin = v * 360f;
            return Rotate(Rotate(direction, side, tilt), direction, spin);
        }

        private static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }

        // rotates v around a unit axis by degrees (Rodrigues' formula).
        private static Vector3 Rotate(Vector3 v, Vector3 axis, float degrees)
        {
            double r = degrees * System.Math.PI / 180.0;
            double c = System.Math.Cos(r);
            double s = System.Math.Sin(r);
            double dot = axis.x * v.x + axis.y * v.y + axis.z * v.z;
            var k = Cross(axis, v);
            return new Vector3(
                (float)(v.x * c + k.x * s + axis.x * dot * (1 - c)),
                (float)(v.y * c + k.y * s + axis.y * dot * (1 - c)),
                (float)(v.z * c + k.z * s + axis.z * dot * (1 - c)));
        }
    }
}
