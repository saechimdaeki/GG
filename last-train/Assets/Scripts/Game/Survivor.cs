using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LastTrain
{
    /// <summary>What a survivor's attacks need from the world: the horde, effects and noise.</summary>
    public interface IWorld
    {
        Horde Horde { get; }
        void SwingFx(Vector2 from, Vector2 dir, float reach, float half, string weapon, bool stealthKill);
        void SpawnNail(Vector2 from, Vector2 dir, float dmg, int pierce);
        void SpawnFirework(Vector2 from, Vector2 to, float dmg, float radius, float flight);
        /// <summary>A sound at `at` that wakes zombies within `radius`.</summary>
        void Noise(Vector2 at, float radius);
        void ReportImpact(Vector2 position, Vector2 direction, float amount, bool fatal, bool silent);
        bool CanReachForStealth(Vector2 from, Vector2 to);
    }

    /// <summary>
    /// One survivor: position, health, hunger, the weapons in their hands (found by looting)
    /// and the supplies in their bag. Attacks are aimed by the player; no Unity objects inside,
    /// so tests can drive it directly.
    /// </summary>
    public class Survivor
    {
        public class Item { public string id; public int durability; }

        public Vector2 Pos;
        public Vector2 Aim = new(0, 1);
        public float MaxHp, Hp, Hunger;
        public bool Downed, Dead, Crouching, Sprinting;
        public float DownedT, ReviveT;
        /// <summary>Weapon slots; fists are always slot 0 and never break.</summary>
        public readonly List<Item> Slots = new() { new Item { id = "fists" } };
        public int Current;
        internal static readonly string[] SupplyItems = { "food", "bandage", "scrap", "nails", "fireworks", "fuse" };
        public int BagLoad => Bag.Sum(item => item.Value * T.bag.Weight(item.Key));
        public int BagCapacity => T.bag.capacity;
        public readonly Dictionary<string, int> Bag = new() { ["food"] = 1, ["scrap"] = 0, ["bandage"] = 0, ["nails"] = 0, ["fireworks"] = 0, ["fuse"] = 0 };
        /// <summary>Weapon id swung or fired this frame (the view plays an attack animation).</summary>
        public string Attacked;
        public Vector2 LastAttackDirection { get; private set; } = Vector2.up;
        public bool LastWasStealthKill;
        float _cooldown, _sprintDelay;
        public float SprintEnergy { get; private set; } = 1f;
        public bool SprintExhausted { get; private set; }

        internal void TickSprint(bool moving, float dt)
        {
            var cfg = T.player;
            if (Sprinting && moving)
            {
                _sprintDelay = cfg.sprintRecoveryDelay;
                SprintEnergy = Mathf.Max(0f, SprintEnergy - dt / cfg.sprintSeconds);
                if (SprintEnergy <= 0f) SprintExhausted = true;
                return;
            }
            _sprintDelay = Mathf.Max(0f, _sprintDelay - dt);
            if (_sprintDelay > 0f) return;
            SprintEnergy = Mathf.Min(1f, SprintEnergy + dt / cfg.sprintRecoverSeconds);
            if (SprintEnergy >= cfg.sprintRestartThreshold) SprintExhausted = false;
        }
        static Tuning T => Tuning.Data;

        public Survivor()
        {
            MaxHp = Hp = T.player.hp;
            Hunger = T.player.hungerMax;
        }

        public bool Alive => !Downed && !Dead;
        public Item Held => Slots[Mathf.Clamp(Current, 0, Slots.Count - 1)];
        public Tuning.WeaponCfg HeldCfg => T.Weapon(Held.id);
        public float Speed => T.player.speed * (Crouching ? T.player.crouchMult : Sprinting ? T.player.sprintMult : 1f);
        public float StepNoise(bool moving) => !moving ? 0 : Crouching ? T.senses.noiseCrouch : Sprinting ? T.senses.noiseSprint : T.senses.noiseWalk;

        /// <summary>Ammo left for the held weapon, or -1 if it uses none.</summary>
        public int Ammo => Held.id switch { "nailgun" => Bag["nails"], "fireworks" => Bag["fireworks"], _ => -1 };

        public void Hurt(float amount)
        {
            if (!Alive) return;
            Hp -= amount;
            if (Hp > 0) return;
            Hp = 0;
            Downed = true;
            DownedT = T.player.downedSeconds;
            ReviveT = 0;
        }

        internal void TickCooldown(float dt) => _cooldown -= dt;

        /// <summary>Hunger drains; an empty stomach costs health.</summary>
        public void Tick(float dt)
        {
            if (!Alive) return;
            TickCooldown(dt);
            Hunger = Mathf.Max(0, Hunger - T.player.hungerPerSec * dt);
            if (Hunger <= 0) Hurt(T.player.starveDps * dt);
        }

        public bool Eat()
        {
            if (Bag["food"] <= 0 || Hunger >= T.player.hungerMax - 1) return false;
            Bag["food"]--;
            Hunger = Mathf.Min(T.player.hungerMax, Hunger + T.player.foodValue);
            return true;
        }

        public bool Bandage()
        {
            if (Bag["bandage"] <= 0 || Hp >= MaxHp) return false;
            Bag["bandage"]--;
            Hp = Mathf.Min(MaxHp, Hp + T.player.bandageHeal);
            return true;
        }

        public void Select(int slot)
        {
            if (slot >= 0 && slot < Slots.Count) Current = slot;
        }

        public void Cycle(int dir) => Current = (Current + dir + Slots.Count) % Slots.Count;

        public string TradeBlockReason(Tuning.TradeCfg t)
        {
            if (!Bag.TryGetValue(t.give, out int have) || have < t.giveN) return "물자 부족";
            int after = BagLoad - t.giveN * T.bag.Weight(t.give) + t.getN * T.bag.Weight(t.get);
            return after > BagCapacity ? "가방 공간 부족" : "";
        }

        public bool Trade(Tuning.TradeCfg t, out string message)
        {
            message = TradeBlockReason(t);
            if (message.Length != 0) return false;
            Bag[t.give] -= t.giveN;
            message = Take(t.get, t.getN);
            return true;
        }

        public string Take(string item, int count) => Take(item, count, out _);

        public string Take(string item, int count, out int acquired)
        {
            acquired = 0;
            if (item == "nothing" || count <= 0) return "아무것도 없다";
            if (Bag.ContainsKey(item))
            {
                int weight = T.bag.Weight(item);
                acquired = weight > 0 ? Mathf.Clamp((BagCapacity - BagLoad) / weight, 0, count) : count;
                if (acquired == 0) return "가방이 가득 찼다";
                Bag[item] += acquired;
                if (item == "fireworks" && Slots.All(s => s.id != "fireworks")) AddSlot("fireworks");
                return $"{Name(item)} +{acquired}";
            }
            acquired = count;
            var cfg = T.Weapon(item);
            var have = Slots.Find(s => s.id == item);
            if (have != null)
            {
                have.durability = cfg.durability;  // a second one just tops it up
                return $"{cfg.name} (새것으로 교체)";
            }
            if (Slots.Count < T.slots)
            {
                AddSlot(item);
                Current = Slots.Count - 1;
                return $"{cfg.name}을(를) 주웠다!";
            }
            if (Current == 0) Current = 1;
            string dropped = HeldCfg.name;
            Slots[Current] = new Item { id = item, durability = cfg.durability };
            return $"{cfg.name}을(를) 주웠다 ({dropped} 버림)";
        }

        void AddSlot(string id)
        {
            if (Slots.Count >= T.slots) return;
            Slots.Add(new Item { id = id, durability = T.Weapon(id).durability });
        }

        public static string Name(string item) => item switch
        {
            "food" => "먹을 것", "scrap" => "고철", "bandage" => "붕대", "nails" => "못", "fireworks" => "폭죽", "fuse" => "퓨즈",
            _ => T.Weapon(item)?.name ?? item,
        };

        readonly List<int> _hits = new();
        bool IsStealthTarget(IWorld world, int i)
        {
            var horde = world.Horde;
            Vector2 delta = horde.Pos[i] - Pos;
            float range = Mathf.Min(HeldCfg.range, T.senses.stealthRange);
            return !horde.IsAware(i) && horde.Hp[i] > 0 && delta.sqrMagnitude <= range * range &&
                IsBehind(horde.Heading[i], -delta) && Vector2.Angle(Aim, delta) <= HeldCfg.arc * 0.5f &&
                world.CanReachForStealth(Pos, horde.Pos[i]);
        }

        internal static bool IsBehind(Vector2 heading, Vector2 toPlayer) =>
            Vector2.Dot(heading, toPlayer.normalized) < -0.45f;

        public int StealthTarget(IWorld world)
        {
            if (!Alive || !Crouching || _cooldown > 0 || HeldCfg.kind != "melee") return -1;
            int best = -1;
            float nearest = float.PositiveInfinity;
            foreach (int i in world.Horde.Near(Pos, T.senses.stealthRange, _hits))
            {
                float distance = (world.Horde.Pos[i] - Pos).sqrMagnitude;
                if (distance >= nearest || !IsStealthTarget(world, i)) continue;
                best = i;
                nearest = distance;
            }
            return best;
        }

        internal bool WouldReplaceWeapon(string item) =>
            !Bag.ContainsKey(item) && Slots.Count >= T.slots && Slots.All(slot => slot.id != item);


        /// <summary>
        /// Attacks toward `target` with the held weapon if it is ready. Melee hits the nearest reachable enemy in the
        /// arc; an unaware zombie within stealth range dies in one silent blow. Returns false when
        /// nothing happened (cooldown, no ammo).
        /// </summary>
        internal bool CanAttack => Alive && _cooldown <= 0;

        public bool Attack(Vector2 target, IWorld world, int team = 1)
        {
            Attacked = null;
            LastWasStealthKill = false;
            if (!Alive || _cooldown > 0) return false;
            var w = HeldCfg;
            var dir = target - Pos;
            if (dir.sqrMagnitude > 0.0001f) Aim = dir.normalized;
            float dmg = w.damage * (1 + T.player.teamDamagePerMate * Mathf.Max(team - 1, 0));
            var horde = world.Horde;
            switch (w.kind)
            {
                case "melee":
                {
                    float half = w.arc * 0.5f;
                    int stealth = StealthTarget(world);
                    LastWasStealthKill = stealth >= 0;
                    if (LastWasStealthKill)
                    {
                        float damage = horde.Hp[stealth];
                        Vector2 impact = horde.Pos[stealth];
                        horde.Damage(stealth, damage, 0.16f, Aim * w.knockback);
                        world.ReportImpact(impact, Aim, damage, true, true);
                    }
                    else
                    {
                        int first = -1;
                        float closest = float.PositiveInfinity;
                        foreach (int i in horde.Near(Pos, w.range, _hits))
                        {
                            var delta = horde.Pos[i] - Pos;
                            float distance = delta.sqrMagnitude;
                            if (horde.Hp[i] <= 0 || distance >= closest ||
                                (distance > 0.09f && Vector2.Angle(Aim, delta) > half) ||
                                !world.CanReachForStealth(Pos, horde.Pos[i])) continue;
                            first = i;
                            closest = distance;
                        }
                        if (first >= 0)
                        {
                            Vector2 impact = horde.Pos[first];
                            Vector2 direction = (impact - Pos).normalized;
                            bool guarded = horde.Kind[first] == Horde.Armored &&
                                Vector2.Dot(horde.Heading[first], -direction) > 0.35f;
                            float dealt = guarded ? dmg * 0.4f : dmg;
                            float damage = Mathf.Min(dealt, horde.Hp[first]);
                            horde.Damage(first, dealt, guarded ? 0f : w.stagger,
                                direction * w.knockback * (guarded ? 0.25f : 1f));
                            world.ReportImpact(impact, direction, damage, horde.Hp[first] <= 0, false);
                            horde.Alert(first, Pos, T.senses.forgetSeconds);
                        }
                    }
                    world.SwingFx(Pos, Aim, w.range, half * Mathf.Deg2Rad, w.id, LastWasStealthKill);
                    world.Noise(Pos, LastWasStealthKill ? 1f : w.noise);
                    Wear();
                    break;
                }
                case "gun":
                    if (Bag["nails"] <= 0) return false;
                    Bag["nails"]--;
                    world.SpawnNail(Pos, Aim, dmg, w.pierce);
                    world.Noise(Pos, w.noise);
                    break;
                case "throw":
                    if (Bag["fireworks"] <= 0) return false;
                    Bag["fireworks"]--;
                    var to2 = Vector2.ClampMagnitude(target - Pos, w.range);
                    world.SpawnFirework(Pos, Pos + to2, dmg, w.radius, w.flight);
                    break;
            }
            _cooldown = w.cooldown;
            LastAttackDirection = Aim;
            Attacked = w.id;
            return true;
        }

        /// <summary>Melee weapons lose one durability per swing and break at zero.</summary>
        void Wear()
        {
            var item = Held;
            if (item.id == "fists" || T.Weapon(item.id).durability <= 0) return;
            item.durability--;
            if (item.durability > 0) return;
            Slots.RemoveAt(Current);
            Current = 0;
            Broke = T.Weapon(item.id).name;
        }

        /// <summary>Name of the weapon that broke since the game last read it.</summary>
        public string Broke;

        /// <summary>
        /// Co-op rules for one frame: a downed survivor bleeds out unless a standing teammate stays
        /// close; enough time close and they stand up with part of their health.
        /// Returns true while anyone is still standing.
        /// </summary>
        public static bool CoopTick(IReadOnlyList<Survivor> all, float dt)
        {
            var cfg = T.player;
            foreach (var p in all)
            {
                if (!p.Downed || p.Dead) continue;
                bool helped = all.Any(q => q != p && q.Alive && Vector2.Distance(q.Pos, p.Pos) <= cfg.reviveRadius);
                if (helped)
                {
                    p.ReviveT += dt;
                    if (p.ReviveT < cfg.reviveSeconds) continue;
                    p.Downed = false;
                    p.Hp = p.MaxHp * cfg.reviveHp;
                }
                else
                {
                    p.ReviveT = 0;
                    p.DownedT -= dt;
                    if (p.DownedT <= 0) p.Dead = true;
                }
            }
            return all.Any(p => p.Alive);
        }
    }
}
