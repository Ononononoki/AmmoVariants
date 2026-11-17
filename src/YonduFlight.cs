using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace AmmoVariants
{
    public sealed class YonduFlight : MonoBehaviour
    {
        internal const string BaseItem = "ArrowBloodGold";
        internal const string ItemName = BaseItem + "Yondu";
        internal const int ItemCount = 1;
        internal const float BodyLength = 1.25f;
        internal const float LaunchSeconds = 0.5f;
        internal const float SearchSeconds = 0.25f;
        internal const float SearchRadius = 50f;
        internal const int MaxStrikes = 10;
        internal static float MaxDurability => Plugin.YonduTestDurability.Value;
        internal const float BasePierce = 90f;
        internal const float RepeatDamageFactor = 0.9f;
        internal static readonly Color StreakColor = new Color(1f, 0.06f, 0.1f, 0.85f);
        internal static readonly Color RibbonColor = new Color(1f, 0.19f, 0.23f, 0.75f);

        private const float HuntSpeed = 50f;
        private const float SearchSpeed = 34f;
        private const float ReturnSpeed = 65f;
        private const float CatchDistance = 0.75f;
        private const float ReservationSeconds = 10f;
        private const float MaximumFlightSeconds = 45f;
        private const string WhistleRpc = "AmmoVariants.Yondu.Whistle";

        private enum Phase { Launch, Hunt, Search, Return }

        private sealed class Slot
        {
            internal float Deadline;
            internal Attack Reservation;
            internal ItemDrop.ItemData Ammo;
            internal YonduFlight Round;
        }

        private static readonly ConditionalWeakTable<Player, Slot> Slots = new ConditionalWeakTable<Player, Slot>();
        private static readonly AccessTools.FieldRef<Attack, Humanoid> Attacker =
            AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
        private static readonly AccessTools.FieldRef<Attack, ItemDrop.ItemData> SelectedAmmo =
            AccessTools.FieldRefAccess<Attack, ItemDrop.ItemData>("m_ammoItem");
        private static readonly AccessTools.FieldRef<Projectile, Skills.SkillType> Skill =
            AccessTools.FieldRefAccess<Projectile, Skills.SkillType>("m_skill");
        private static readonly AccessTools.FieldRef<Projectile, float> SkillRaise =
            AccessTools.FieldRefAccess<Projectile, float>("m_raiseSkillAmount");

        private readonly List<Character> candidates = new List<Character>();
        private readonly Dictionary<Character, int> struck = new Dictionary<Character, int>();
        private readonly HashSet<Character> cycleStruck = new HashSet<Character>();
        private readonly HashSet<Character> launchStruck = new HashSet<Character>();
        private readonly HashSet<Character> missed = new HashSet<Character>();
        private ZNetView view;
        private Projectile projectile;
        private Player shooter;
        private ItemDrop.ItemData firedAmmo;
        private Slot slot;
        private Character target;
        private Character lastTarget;
        private Vector3 launchVelocity;
        private Phase phase;
        private float elapsed;
        private float phaseTime;
        private int strikes;
        private bool finished;

        internal static bool CanReserve(bool active, float deadline, float now)
        {
            return !active && now >= deadline;
        }

        internal static bool CanStrike(int strikes)
        {
            return strikes < MaxStrikes;
        }

        internal static float GuidedDamageShare(int previousHits)
        {
            return (float)Math.Pow(RepeatDamageFactor, previousHits);
        }

        internal static IEnumerable<CodeInstruction> GuardAmmo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = new List<CodeInstruction>(instructions);
            var itemType = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), "m_itemType");
            int field = code.FindIndex(instruction => instruction.LoadsField(itemType));
            if (field < 3 || code[field - 3].opcode != OpCodes.Ldarg_1 || code[field - 2].opcode != OpCodes.Ldind_Ref)
                throw new InvalidOperationException("yondu ammo consumption guard could not find the selected ammo");

            var next = code[field - 3];
            var ordinary = generator.DefineLabel();
            var entry = new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(next);
            next.labels.Add(ordinary);
            code.InsertRange(field - 3, new[]
            {
                entry, new CodeInstruction(OpCodes.Ldind_Ref),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(YonduFlight), nameof(IsReusableAmmo))),
                new CodeInstruction(OpCodes.Brfalse, ordinary),
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldind_Ref),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(YonduFlight), nameof(Reserve))),
                new CodeInstruction(OpCodes.Ret)
            });
            return code;
        }

        private static bool IsReusableAmmo(ItemDrop.ItemData ammo)
        {
            return Items.KindOf(ammo) == Items.AmmoKind.Yondu;
        }

        private static bool Reserve(Attack attack, ItemDrop.ItemData ammo)
        {
            if (!(Attacker(attack) is Player player)) return false;
            var slot = Slots.GetValue(player, _ => new Slot());
            if (!CanReserve(slot.Round != null, slot.Deadline, Time.time))
            {
                player.Message(MessageHud.MessageType.Center, slot.Round != null
                    ? "Yondu Arrow is still flying."
                    : "Yondu Arrow is already being launched.");
                return false;
            }
            if (ammo.m_durability < 1f)
            {
                if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("yondu launch blocked, durability " + ammo.m_durability.ToString("F0"));
                player.Message(MessageHud.MessageType.Center, "Repair this Yondu Arrow at the Black Forge.");
                return false;
            }
            slot.Deadline = Time.time + ReservationSeconds;
            slot.Reservation = attack;
            slot.Ammo = ammo;
            SelectedAmmo(attack) = ammo;
            return true;
        }

        internal static void AddSparks(GameObject projectile)
        {
            var sword = PrefabManager.Instance.GetPrefab("THSwordGold_BloodLightning");
            var source = sword != null ? sword.transform.Find("attach/VFX/Sparcs") : null;
            if (source == null || source.GetComponent<ParticleSystem>() == null) return;
            var sparks = Instantiate(source.gameObject, projectile.transform, false);
            sparks.name = "AmmoLightning";
            sparks.transform.localPosition = Vector3.zero;
            sparks.transform.localRotation = Quaternion.identity;
            var system = sparks.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        private void Awake()
        {
            projectile = GetComponent<Projectile>();
            view = GetComponent<ZNetView>();
            if (view != null && view.IsValid())
                view.Register<int, Vector3>(WhistleRpc, HearWhistle);
        }

        internal void Begin(Player player, Attack attack, Vector3 velocity, float draw)
        {
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            if (player == null || attack == null || !Slots.TryGetValue(player, out var reserved)
                || reserved.Round != null || reserved.Reservation != attack || reserved.Deadline <= Time.time)
            {
                view.Destroy();
                return;
            }
            shooter = player;
            firedAmmo = reserved.Ammo;
            launchVelocity = velocity;
            slot = reserved;
            slot.Reservation = null;
            slot.Ammo = null;
            slot.Deadline = 0f;
            if (!HasUsableArrow())
            {
                slot = null;
                view.Destroy();
                return;
            }
            slot.Round = this;
            if (Plugin.LogCounters.Value)
                Plugin.Log.LogInfo($"yondu launch durability {firedAmmo.m_durability:F0}/{MaxDurability:F0}, draw {draw:F3}, speed {velocity.magnitude:F1} m/s, range {SearchRadius:F0} m, as fired [{Shots.Describe(projectile.m_damage)}]");
            Whistle(0);
        }

        internal void Tick()
        {
            if (finished || view == null || !view.IsValid() || !view.IsOwner()) return;
            if (shooter == null || shooter.IsDead() || elapsed >= MaximumFlightSeconds)
            {
                Finish(false);
                return;
            }
            if (phase != Phase.Return && !HasUsableArrow()) StartReturn();

            float dt = Time.fixedDeltaTime;
            elapsed += dt;
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Launch:
                    Launch(Mathf.Clamp(LaunchSeconds - (phaseTime - dt), 0f, dt));
                    if (phaseTime >= LaunchSeconds) Acquire(false);
                    break;
                case Phase.Hunt:
                    Hunt(dt);
                    break;
                case Phase.Search:
                    Move(transform.position + Vector3.up * (SearchSpeed * dt));
                    if (phaseTime >= SearchSeconds) Acquire(true);
                    break;
                case Phase.Return:
                    var catchPoint = shooter.GetCenterPoint();
                    Move(Vector3.MoveTowards(transform.position, catchPoint, ReturnSpeed * dt));
                    if ((transform.position - catchPoint).sqrMagnitude <= CatchDistance * CatchDistance) Finish(true);
                    break;
            }
        }

        private bool Eligible(Character character)
        {
            return character != null && !character.IsDead() && !(character is Player) && !character.IsTamed()
                && (character.transform.position - shooter.transform.position).sqrMagnitude <= SearchRadius * SearchRadius
                && BaseAI.IsEnemy(shooter, character);
        }

        private bool HasUsableArrow()
        {
            return firedAmmo != null && firedAmmo.m_durability >= 1f
                && shooter.GetInventory()?.ContainsItem(firedAmmo) == true;
        }

        private void Acquire(bool afterSearch)
        {
            candidates.Clear();
            Character.GetCharactersInRange(shooter.transform.position, SearchRadius, candidates);
            target = SelectTarget(afterSearch);
            phaseTime = 0f;
            if (target != null)
            {
                phase = Phase.Hunt;
                Whistle(1 + strikes % AmmoAssets.YonduTurnWhistles);
            }
            else if (afterSearch) StartReturn();
            else phase = Phase.Search;
        }

        private Character SelectTarget(bool afterSearch)
        {
            var playerPosition = shooter.transform.position;
            Character fresh = null, nextCycle = null;
            float freshDistance = float.PositiveInfinity, nextDistance = float.PositiveInfinity;
            bool lastEligible = false;
            foreach (var candidate in candidates)
            {
                if (!Eligible(candidate) || missed.Contains(candidate)) continue;
                if (candidate == lastTarget)
                {
                    lastEligible = true;
                    continue;
                }

                float distance = (candidate.transform.position - playerPosition).sqrMagnitude;
                if (distance < nextDistance) { nextCycle = candidate; nextDistance = distance; }
                if (distance < freshDistance && !cycleStruck.Contains(candidate))
                {
                    fresh = candidate;
                    freshDistance = distance;
                }
            }

            if (fresh != null) return fresh;
            if (nextCycle == null && (!afterSearch || !lastEligible)) return null;
            cycleStruck.Clear();
            return nextCycle != null ? nextCycle : lastTarget;
        }

        private RaycastHit[] Sweep(Vector3 origin, Vector3 end)
        {
            var delta = end - origin;
            float distance = delta.magnitude;
            if (distance <= 0f) return Array.Empty<RaycastHit>();
            var hits = Shots.EnergyHits(projectile, origin, delta / distance, distance, Shots.RayMask(projectile));
            Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
            return hits;
        }

        private void Launch(float dt)
        {
            launchVelocity += Vector3.down * (projectile.m_gravity * dt);
            var origin = transform.position;
            var end = origin + launchVelocity * dt;
            Move(end);
            foreach (var collision in Sweep(origin, end))
            {
                var enemy = Shots.CharacterOf(collision.collider);
                if (!Eligible(enemy) || !launchStruck.Add(enemy)) continue;
                ApplyDamage(enemy, collision, 1f, "launch hit");
            }
        }

        private void Hunt(float dt)
        {
            if (!Eligible(target))
            {
                Acquire(false);
                return;
            }
            var origin = transform.position;
            var targetPoint = target.GetCenterPoint();
            var end = Vector3.MoveTowards(origin, targetPoint, HuntSpeed * dt);
            foreach (var collision in Sweep(origin, end))
            {
                if (Shots.CharacterOf(collision.collider) != target) continue;
                Move(collision.point);
                Strike(collision);
                return;
            }
            Move(end);
            if ((end - targetPoint).sqrMagnitude < 0.0001f)
            {
                missed.Add(target);
                phase = Phase.Search;
                phaseTime = 0f;
            }
        }

        private void Strike(RaycastHit collision)
        {
            if (!HasUsableArrow())
            {
                StartReturn();
                return;
            }
            firedAmmo.m_durability -= 1f;
            struck.TryGetValue(target, out int previousHits);
            struck[target] = previousHits + 1;
            cycleStruck.Add(target);
            lastTarget = target;
            strikes++;
            ApplyDamage(target, collision, GuidedDamageShare(previousHits), $"guided strike {strikes}/{MaxStrikes}, hit {previousHits + 1} on target, durability {firedAmmo.m_durability:F0}");
            if (firedAmmo.m_durability < 1f)
            {
                shooter.Message(MessageHud.MessageType.Center, "Yondu Arrow is worn out. Repair it at the Black Forge.");
                if (Plugin.LogCounters.Value) Plugin.Log.LogInfo("yondu arrow worn out, returning");
            }
            if (CanStrike(strikes) && HasUsableArrow()) Acquire(false);
            else StartReturn();
        }

        private void ApplyDamage(Character enemy, RaycastHit collision, float share, string label)
        {
            var damage = projectile.m_damage;
            damage.Modify(share);
            damage.m_chop = 0f;
            damage.m_pickaxe = 0f;
            var hit = new HitData
            {
                m_damage = damage, m_point = collision.point, m_dir = transform.forward,
                m_hitCollider = collision.collider, m_skill = Skill(projectile),
                m_skillRaiseAmount = SkillRaise(projectile), m_ranged = true,
                m_dodgeable = true, m_blockable = true
            };
            hit.SetAttacker(shooter);
            enemy.Damage(hit);
            // a normal arrow raises the skill once per hit, a full Yondu shot is up to ten hits
            shooter.RaiseSkill(hit.m_skill, hit.m_skillRaiseAmount / MaxStrikes);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"yondu {label}, target {enemy.m_name}, share {share:F4}, damage sent [{Shots.Describe(damage)}]");
        }

        private void StartReturn()
        {
            phase = Phase.Return;
            target = null;
            Whistle(AmmoAssets.YonduReturnWhistle);
        }

        private void Move(Vector3 end)
        {
            var delta = end - transform.position;
            transform.position = end;
            if (delta.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(delta);
        }

        private void Whistle(int cue)
        {
            AmmoAssets.PlayYondu(cue, transform.position);
            view.InvokeRPC(ZNetView.Everybody, WhistleRpc, cue, transform.position);
        }

        private void HearWhistle(long sender, int cue, Vector3 position)
        {
            if (view.IsOwner() || sender != view.GetZDO().GetOwner()) return;
            AmmoAssets.PlayYondu(cue, position);
        }

        private void Finish(bool caught)
        {
            finished = true;
            if (caught) Whistle(AmmoAssets.YonduCatchWhistle);
            if (Plugin.LogCounters.Value) Plugin.Log.LogInfo($"yondu {(caught ? "caught" : "expired")}, strikes {strikes}, {elapsed:F2} s");
            ReleaseSlot();
            GetComponent<AmmoTrailLifetime>()?.Detach();
            view.Destroy();
        }

        private void ReleaseSlot()
        {
            if (slot == null || !ReferenceEquals(slot.Round, this)) return;
            slot.Round = null;
            slot = null;
        }

        private void OnDestroy() { ReleaseSlot(); }
    }
}
