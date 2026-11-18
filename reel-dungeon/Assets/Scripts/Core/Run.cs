using System;
using System.Collections.Generic;
using System.Linq;

namespace ReelDungeon.Core
{
    public enum Phase { Map, Turn, Reward, Shop, Event, Won, Lost }
    public enum RewardKind { AddSymbol, RemoveSymbol, Heal, Recruit, Gold, Equip, Relic }

    /// <summary>One choice on a reward, rest, recruit or treasure screen, or one shop item.</summary>
    public class Reward
    {
        public RewardKind Kind;
        public int Reel;
        public string SymbolId;
        public string CompanionId;
        public string ItemId;
        public int Amount;
        public int Price;
        public bool Sold;
        public string Text;
    }

    /// <summary>
    /// One dungeon run: the party (one reel and one equipment slot per member), relics, skill gauges,
    /// gold, the chapter map, the current fight and the screens between fights. Pure logic with no
    /// Unity dependency; pass a seeded Random for deterministic tests.
    /// </summary>
    public class Run
    {
        readonly TuningConfig cfg;
        readonly Random rng;
        readonly Dictionary<string, SymbolDef> byId;
        readonly Dictionary<string, CompanionDef> companions;
        readonly Dictionary<string, ItemDef> items;
        int enemyTurn;

        public List<string> Party { get; } = new List<string>();
        public List<List<string>> Reels { get; } = new List<List<string>>();
        public List<int> Gauge { get; } = new List<int>();
        /// <summary>Equipment item id per member, null when the slot is empty.</summary>
        public List<string> Equipment { get; } = new List<string>();
        public List<string> Relics { get; } = new List<string>();
        public HashSet<int> Armed { get; } = new HashSet<int>();
        public SymbolDef[] Faces { get; private set; } = new SymbolDef[0];
        public List<Reward> Offers { get; } = new List<Reward>();
        public List<Reward> ShopItems { get; } = new List<Reward>();
        public string OfferTitle { get; private set; } = "";
        public EventDef CurrentEvent { get; private set; }
        public Phase Phase { get; private set; }
        public int Hp { get; private set; }
        public int Gold { get; private set; }
        public int Chapter { get; private set; }
        public StageMap Map { get; private set; }
        public MapNode Current { get; private set; }
        public EnemyDef Enemy { get; private set; }
        public int EnemyHp { get; private set; }
        public int RerollsLeft { get; private set; }
        public string Log { get; private set; } = "";
        /// <summary>Run stats for the result screen.</summary>
        public int Turns { get; private set; }
        public int Kills { get; private set; }
        public int BestHit { get; private set; }
        public int GoldEarned { get; private set; }

        public ChapterDef ChapterDef => cfg.chapters[Chapter];
        public int EnemyIntent => Enemy.attacks[enemyTurn % Enemy.attacks.Length];
        public int SkillCharge => cfg.skillCharge;
        public int MaxHp => cfg.playerMaxHp + RelicTotal("maxHp");
        public int RerollsPerTurn => cfg.rerollsPerTurn + RelicTotal("reroll");

        /// <summary>Nodes the party can walk to next.</summary>
        public IEnumerable<MapNode> Available => Current == null ? Map.Rows[0] : Current.Next;

        public Run(TuningConfig cfg, Random rng)
        {
            this.cfg = cfg;
            this.rng = rng;
            byId = cfg.symbols.ToDictionary(s => s.id);
            companions = cfg.companions.ToDictionary(c => c.id);
            items = (cfg.items ?? new ItemDef[0]).ToDictionary(i => i.id);
            Hp = cfg.playerMaxHp;
            Gold = cfg.startingGold;
            foreach (var id in cfg.startingParty) Recruit(id);
            Map = StageMap.Generate(cfg, rng);
            Phase = Phase.Map;
        }

        public SymbolDef Symbol(string id) => byId[id];
        public CompanionDef Member(int i) => companions[Party[i]];
        public CompanionDef Companion(string id) => companions[id];
        public ItemDef Item(string id) => items[id];

        int RelicTotal(string stat) => Relics.Select(Item).Where(i => i.stat == stat).Sum(i => i.value);

        // ---------- map ----------

        /// <summary>Walk to an available node and enter it. Returns false if it isn't reachable.</summary>
        public bool Choose(MapNode node)
        {
            if (Phase != Phase.Map || !Available.Contains(node)) return false;
            node.Visited = true;
            Current = node;
            switch (node.Type)
            {
                case NodeType.Combat: StartFight(Pick(ChapterDef.enemies)); break;
                case NodeType.Elite: StartFight(Pick(ChapterDef.elites)); break;
                case NodeType.Boss: StartFight(ChapterDef.boss); break;
                case NodeType.Shop: OpenShop(); break;
                case NodeType.Rest: OpenRest(); break;
                case NodeType.Recruit: OpenRecruit(); break;
                case NodeType.Treasure: OpenTreasure(); break;
                case NodeType.Event: OpenEvent(Pick(cfg.events)); break;
            }
            return true;
        }

        T Pick<T>(T[] list) => list[rng.Next(list.Length)];

        // ---------- combat ----------

        /// <summary>What committing now would do: the faces, then equipment and relics, then armed skills.</summary>
        public SpinOutcome Preview()
        {
            var o = SpinResolver.Resolve(Faces, cfg);
            for (int i = 0; i < Faces.Length; i++)
                foreach (var item in ItemsOn(i))
                    if (item.symbol == Faces[i].id) AddStat(o, item.stat, item.value);
            if (RelicTotal("noSelfDamage") > 0) o.selfDamage = 0;

            var skills = Armed.Select(Member).ToList();
            foreach (var m in skills)
            {
                if (m.skill == "shieldBash") o.damage += o.block;
                if (m.skill == "volley") o.damage += m.skillValue;
                if (m.skill == "prayer") o.heal += m.skillValue;
            }
            foreach (var m in skills)
                if (m.skill == "rage") o.damage *= 2;
            o.frozen = skills.Any(m => m.skill == "freeze");
            return o;
        }

        /// <summary>Member i's equipment plus every relic.</summary>
        IEnumerable<ItemDef> ItemsOn(int member)
        {
            if (Equipment[member] != null) yield return Item(Equipment[member]);
            foreach (var r in Relics) yield return Item(r);
        }

        static void AddStat(SpinOutcome o, string stat, int v)
        {
            switch (stat)
            {
                case "damage": o.damage += v; break;
                case "block": o.block += v; break;
                case "heal": o.heal += v; break;
                case "gold": o.gold += v; break;
                case "weaken": o.weaken += v; break;
            }
        }

        /// <summary>How hard the enemy hits this turn once freeze and weaken apply.</summary>
        public int EnemyAttack(SpinOutcome o) => o.frozen ? 0 : Math.Max(0, EnemyIntent - o.weaken);

        public bool SkillReady(int member) => Gauge[member] >= cfg.skillCharge;

        /// <summary>Arm a full-gauge skill for this turn, or disarm it. Returns false if it isn't ready.</summary>
        public bool ToggleSkill(int member)
        {
            if (Phase != Phase.Turn) return false;
            if (Armed.Remove(member)) return true;
            if (!SkillReady(member)) return false;
            Armed.Add(member);
            return true;
        }

        /// <summary>Spin one reel again. Returns false when no rerolls are left.</summary>
        public bool Reroll(int reel)
        {
            if (Phase != Phase.Turn || RerollsLeft <= 0) return false;
            RerollsLeft--;
            SpinReel(reel);
            return true;
        }

        /// <summary>Apply the faces, items and armed skills, charge gauges, then the enemy attacks if it survived.</summary>
        public void Commit()
        {
            if (Phase != Phase.Turn) return;
            var o = Preview();
            int attack = EnemyAttack(o);

            for (int i = 0; i < Party.Count; i++)
            {
                if (Armed.Contains(i)) Gauge[i] = 0;
                else if (Faces[i].id == Member(i).signature)
                {
                    int extra = Equipment[i] != null && Item(Equipment[i]).stat == "charge" ? Item(Equipment[i]).value : 0;
                    Gauge[i] = Math.Min(cfg.skillCharge, Gauge[i] + 1 + extra);
                }
            }
            if (Armed.Any(i => Member(i).skill == "prayer")) PurgeOneCurse();
            Armed.Clear();

            Hp = Math.Min(MaxHp, Hp + o.heal);
            Gold += o.gold;
            GoldEarned += o.gold;
            Turns++;
            BestHit = Math.Max(BestHit, o.damage);
            EnemyHp -= o.damage;
            if (EnemyHp <= 0) { Win(o.damage); return; }

            int hit = Math.Max(0, attack - o.block) + o.selfDamage;
            Hp -= hit;
            enemyTurn++;
            Log = $"{o.damage} 피해를 주고 {hit} 피해를 받았다";
            if (Hp <= 0) { Hp = 0; Phase = Phase.Lost; return; }
            StartTurn();
        }

        void Win(int damage)
        {
            EnemyHp = 0;
            Gold += Enemy.gold;
            GoldEarned += Enemy.gold;
            Kills++;
            Log = $"{Enemy.name} 처치! ({damage} 피해, +{Enemy.gold} 골드)";
            var type = Current.Type;
            Offers.Clear();
            if (type == NodeType.Boss)
            {
                if (Chapter == cfg.chapters.Length - 1) { Phase = Phase.Won; return; }
                Chapter++;
                Hp = Math.Min(MaxHp, Hp + cfg.chapterHeal);
                Map = StageMap.Generate(cfg, rng);
                Current = null;
                Log += $"  ·  {ChapterDef.name}(으)로!";
                Offers.Add(RelicOffer());
                Offers.Add(EquipOffer());
                Offers.Add(AddOffer());
                OpenOffers("보스의 보물을 하나 고르세요");
                return;
            }
            Offers.Add(AddOffer());
            // elites, mimics and event fights drop equipment
            Offers.Add(type == NodeType.Combat ? AddOffer() : EquipOffer());
            Offers.Add(CurseOffer() ?? new Reward { Kind = RewardKind.Heal, Amount = cfg.restHeal, Text = $"HP {cfg.restHeal} 회복" });
            OpenOffers("전리품을 하나 고르세요");
        }

        void StartFight(EnemyDef enemy)
        {
            Enemy = enemy;
            EnemyHp = enemy.hp;
            enemyTurn = 0;
            Phase = Phase.Turn;
            int charge = RelicTotal("battleCharge");
            for (int i = 0; i < Gauge.Count; i++) Gauge[i] = Math.Min(cfg.skillCharge, Gauge[i] + charge);
            StartTurn();
        }

        void StartTurn()
        {
            RerollsLeft = RerollsPerTurn;
            for (int i = 0; i < Reels.Count; i++) SpinReel(i);
        }

        void SpinReel(int i) => Faces[i] = byId[Reels[i][rng.Next(Reels[i].Count)]];

        // ---------- offers: reward, rest, recruit, treasure ----------

        /// <summary>Take one of <see cref="Offers"/>, or skip with -1, and go back to the map.</summary>
        public void ChooseReward(int index)
        {
            if (Phase != Phase.Reward) return;
            if (index >= 0)
            {
                Apply(Offers[index]);
                Log = Offers[index].Text;
            }
            Phase = Phase.Map;
        }

        void OpenOffers(string title)
        {
            OfferTitle = title;
            Phase = Phase.Reward;
        }

        void OpenRest()
        {
            Offers.Clear();
            Offers.Add(new Reward { Kind = RewardKind.Heal, Amount = cfg.restHeal * 2, Text = $"푹 쉬기: HP {cfg.restHeal * 2} 회복" });
            var curse = CurseOffer();
            if (curse != null) Offers.Add(curse);
            OpenOffers("모닥불에서 쉬어 간다");
        }

        void OpenRecruit()
        {
            Offers.Clear();
            var free = cfg.companions.Where(c => !Party.Contains(c.id)).OrderBy(_ => rng.Next()).Take(3);
            if (Party.Count < cfg.maxParty)
                foreach (var c in free) Offers.Add(RecruitOffer(c));
            if (Offers.Count == 0) Offers.Add(GoldOffer(cfg.treasureGold, "파티가 가득 찼다"));
            OpenOffers("동료가 될 모험가를 만났다");
        }

        void OpenTreasure()
        {
            if (rng.NextDouble() < cfg.mimicChance)
            {
                Log = "보물상자가 아니라 미믹이다!";
                StartFight(cfg.mimic);
                return;
            }
            Gold += cfg.treasureGold;
            Log = $"보물상자: +{cfg.treasureGold} 골드";
            Offers.Clear();
            Offers.Add(EquipOffer());
            Offers.Add(AddOffer());
            Offers.Add(AddOffer());
            OpenOffers("보물상자 안에 무언가 있다");
        }

        // ---------- events ----------

        void OpenEvent(EventDef e)
        {
            CurrentEvent = e;
            Phase = Phase.Event;
        }

        /// <summary>False when an option costs more gold than the party has.</summary>
        public bool CanChoose(EventOption option) => option.effects.All(e =>
            e.kind == "gamble" ? Gold >= e.value
            : e.kind == "gold" && e.value < 0 ? Gold >= -e.value
            : true);

        /// <summary>Resolve one option of <see cref="CurrentEvent"/>; most go back to the map, "fight" starts an elite fight.</summary>
        public bool ChooseEvent(int index)
        {
            if (Phase != Phase.Event) return false;
            var option = CurrentEvent.options[index];
            if (!CanChoose(option)) return false;
            Phase = Phase.Map;
            var log = new List<string>();
            foreach (var e in option.effects)
            {
                switch (e.kind)
                {
                    case "hp":
                        Hp = Math.Max(1, Math.Min(MaxHp, Hp + e.value));
                        log.Add(e.value >= 0 ? $"HP +{e.value}" : $"HP {e.value}");
                        break;
                    case "gold":
                        Gold += e.value;
                        log.Add(e.value >= 0 ? $"+{e.value} 골드" : $"{e.value} 골드");
                        break;
                    case "relic": { var r = RelicOffer(); Apply(r); log.Add(r.Text); break; }
                    case "equip": { var r = EquipOffer(); Apply(r); log.Add(r.Text); break; }
                    case "recruit":
                        var free = cfg.companions.Where(c => !Party.Contains(c.id)).ToArray();
                        if (Party.Count < cfg.maxParty && free.Length > 0) { var c = Pick(free); Recruit(c.id); log.Add($"{c.name} 합류"); }
                        else { Gold += cfg.treasureGold; log.Add($"+{cfg.treasureGold} 골드"); }
                        break;
                    case "curse":
                        int reel = rng.Next(Reels.Count);
                        var curse = cfg.symbols.First(s => s.curse);
                        Reels[reel].Add(curse.id);
                        log.Add($"{Member(reel).name} 릴에 {curse.name}");
                        break;
                    case "removeCurse": { var r = CurseOffer(); if (r != null) { Apply(r); log.Add(r.Text); } break; }
                    case "gamble":
                        Gold -= e.value;
                        bool won = rng.Next(2) == 0;
                        if (won) Gold += e.value * 3;
                        log.Add(won ? $"대박! +{e.value * 2} 골드" : $"꽝… -{e.value} 골드");
                        break;
                    case "fight":
                        StartFight(Pick(ChapterDef.elites));
                        log.Add("싸움이 벌어졌다!");
                        break;
                }
            }
            Log = string.Join(", ", log);
            return true;
        }

        // ---------- shop ----------

        void OpenShop()
        {
            var p = cfg.prices;
            ShopItems.Clear();
            for (int k = 0; k < 2; k++) ShopItems.Add(Priced(AddOffer(), p.addSymbol));
            var curse = CurseOffer();
            if (curse != null) ShopItems.Add(Priced(curse, p.removeCurse));
            ShopItems.Add(new Reward { Kind = RewardKind.Heal, Amount = p.healAmount, Price = p.heal, Text = $"HP {p.healAmount} 회복" });
            var equip = EquipOffer();
            if (equip.Kind == RewardKind.Equip) ShopItems.Add(Priced(equip, p.equip));
            var relic = RelicOffer();
            if (relic.Kind == RewardKind.Relic) ShopItems.Add(Priced(relic, p.relic));
            var free = cfg.companions.Where(c => !Party.Contains(c.id)).ToArray();
            if (Party.Count < cfg.maxParty && free.Length > 0) ShopItems.Add(Priced(RecruitOffer(Pick(free)), p.recruit));
            Phase = Phase.Shop;
        }

        static Reward Priced(Reward r, int price)
        {
            r.Price = price;
            return r;
        }

        /// <summary>Buy one shop item. Returns false if it is sold out or too expensive.</summary>
        public bool Buy(int index)
        {
            if (Phase != Phase.Shop) return false;
            var item = ShopItems[index];
            if (item.Sold || Gold < item.Price) return false;
            if (item.Kind == RewardKind.Recruit && Party.Count >= cfg.maxParty) return false;
            Gold -= item.Price;
            item.Sold = true;
            Apply(item);
            Log = $"구매: {item.Text}";
            return true;
        }

        public void LeaveShop()
        {
            if (Phase == Phase.Shop) Phase = Phase.Map;
        }

        // ---------- shared ----------

        void Apply(Reward r)
        {
            switch (r.Kind)
            {
                case RewardKind.AddSymbol: Reels[r.Reel].Add(r.SymbolId); break;
                case RewardKind.RemoveSymbol: Reels[r.Reel].Remove(r.SymbolId); break;
                case RewardKind.Heal: Hp = Math.Min(MaxHp, Hp + r.Amount); break;
                case RewardKind.Recruit: Recruit(r.CompanionId); break;
                case RewardKind.Gold: Gold += r.Amount; break;
                case RewardKind.Equip: Equipment[r.Reel] = r.ItemId; break;
                case RewardKind.Relic:
                    Relics.Add(r.ItemId);
                    if (Item(r.ItemId).stat == "maxHp") Hp += Item(r.ItemId).value;
                    break;
            }
        }

        /// <summary>Add a member and their reel (already spun). Does not check maxParty; callers do.</summary>
        public void Recruit(string id)
        {
            Party.Add(id);
            Reels.Add(companions[id].reel.ToList());
            Gauge.Add(0);
            Equipment.Add(null);
            var faces = Faces;
            Array.Resize(ref faces, Reels.Count);
            Faces = faces;
            SpinReel(Reels.Count - 1);
        }

        void PurgeOneCurse()
        {
            var reel = Reels.FirstOrDefault(r => r.Count > 1 && r.Any(id => byId[id].curse));
            reel?.Remove(reel.First(id => byId[id].curse));
        }

        Reward AddOffer()
        {
            var good = cfg.symbols.Where(s => !s.curse).ToArray();
            var s = Pick(good);
            int reel = rng.Next(Reels.Count);
            return new Reward { Kind = RewardKind.AddSymbol, Reel = reel, SymbolId = s.id, Text = $"{Member(reel).name} 릴에 {s.name} 추가" };
        }

        // ponytail: removes only curses; offer removing any face once reels get crowded.
        Reward CurseOffer()
        {
            int cursed = Reels.FindIndex(r => r.Count > 1 && r.Any(id => byId[id].curse));
            if (cursed < 0) return null;
            var id = Reels[cursed].First(x => byId[x].curse);
            return new Reward { Kind = RewardKind.RemoveSymbol, Reel = cursed, SymbolId = id, Text = $"{Member(cursed).name} 릴에서 {byId[id].name} 제거" };
        }

        Reward RecruitOffer(CompanionDef c) =>
            new Reward { Kind = RewardKind.Recruit, CompanionId = c.id, Text = $"{c.name} 영입 ({c.skillName})" };

        Reward GoldOffer(int amount, string why) =>
            new Reward { Kind = RewardKind.Gold, Amount = amount, Text = $"{why}: {amount} 골드" };

        /// <summary>Equipment the party doesn't wear yet, given to the member whose reel uses its symbol most.</summary>
        Reward EquipOffer()
        {
            var pool = items.Values.Where(i => i.slot == "equip" && !Equipment.Contains(i.id)).ToArray();
            if (pool.Length == 0) return GoldOffer(cfg.treasureGold, "장비 대신");
            var item = Pick(pool);
            int member = Enumerable.Range(0, Party.Count)
                .OrderByDescending(i => item.stat == "charge" ? 1 : Reels[i].Count(id => id == item.symbol))
                .ThenBy(i => Equipment[i] == null ? 0 : 1)
                .First();
            return new Reward { Kind = RewardKind.Equip, ItemId = item.id, Reel = member, Text = $"{Member(member).name}에게 {item.name} 장착" };
        }

        Reward RelicOffer()
        {
            var pool = items.Values.Where(i => i.slot == "relic" && !Relics.Contains(i.id)).ToArray();
            if (pool.Length == 0) return GoldOffer(cfg.treasureGold * 2, "유물 대신");
            var item = Pick(pool);
            return new Reward { Kind = RewardKind.Relic, ItemId = item.id, Text = $"유물: {item.name}" };
        }
    }
}
