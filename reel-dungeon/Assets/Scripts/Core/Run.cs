using System;
using System.Collections.Generic;
using System.Linq;

namespace ReelDungeon.Core
{
    public enum Phase { Map, Turn, Reward, Shop, Event, Won, Lost }
    public enum RewardKind { AddSymbol, RemoveSymbol, Heal, Recruit, Gold, Equip, Relic, Upgrade }

    /// <summary>One enemy action, parsed from "kind:value".</summary>
    public struct EnemyMove
    {
        public string Kind;
        public int Value;
    }

    /// <summary>One choice on a reward, rest, recruit or treasure screen, or one shop item.</summary>
    [Serializable]
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
    public partial class Run
    {
        readonly TuningConfig cfg;
        readonly Random rng;
        readonly Dictionary<string, SymbolDef> byId;
        readonly Dictionary<string, CompanionDef> companions;
        readonly Dictionary<string, ItemDef> items;
        int enemyTurn;
        readonly List<int> tempCurses = new List<int>(); // reels that got a skull from this fight's curses

        public List<string> Party { get; } = new List<string>();
        public List<List<string>> Reels { get; } = new List<List<string>>();
        public List<int> Gauge { get; } = new List<int>();
        /// <summary>Equipment item id per member, null when the slot is empty.</summary>
        public List<string> Equipment { get; } = new List<string>();
        public List<string> Relics { get; } = new List<string>();
        /// <summary>Upgrade level per symbol id; each level adds +1 to that symbol's main stat on every reel.</summary>
        public Dictionary<string, int> SymbolLevel { get; } = new Dictionary<string, int>();
        public int ShopRerolls { get; private set; }
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
        public int EnemyRage { get; private set; }
        public int EnemyPoison { get; private set; }
        /// <summary>Gold the current enemy has stolen with "steal"; it drops back when the enemy dies.</summary>
        public int EnemyStolen { get; private set; }
        /// <summary>True once an enemy with a <c>phase2</c> list dropped to half HP; its turns then come from phase2.</summary>
        public bool EnemyPhase2 { get; private set; }
        /// <summary>The reel a "jam" move broke this turn (its face counts for nothing), or -1.</summary>
        public int JammedReel { get; private set; } = -1;
        /// <summary>Companion and relic ids not unlocked yet; they are never offered. Set by the caller, not saved with the run.</summary>
        public HashSet<string> Locked { get; } = new HashSet<string>();
        /// <summary>The current enemy's full HP after the difficulty bonus.</summary>
        public int EnemyMaxHp { get; private set; }
        /// <summary>Difficulty level 0-5; each level adds the ones below it (see <see cref="Ascension"/> effects in StartFight etc.).</summary>
        public int Ascension { get; private set; }
        public int MaxParty => cfg.maxParty + RelicTotal("maxParty");

        /// <summary>The enemy's next action, shown to the player before they commit.</summary>
        public EnemyMove Intent
        {
            get
            {
                var moves = EnemyPhase2 ? Enemy.phase2 : Enemy.moves;
                var parts = moves[enemyTurn % moves.Length].Split(':');
                return new EnemyMove { Kind = parts[0], Value = parts.Length > 1 ? int.Parse(parts[1]) : 0 };
            }
        }

        /// <summary>Damage the next action deals before block (0 for guard and enrage); includes rage.</summary>
        public int EnemyIntent => NoAttack(Intent.Kind) ? 0 : Intent.Value + EnemyRage + (Ascension >= 4 ? 1 : 0);

        /// <summary>How much of the party's damage the enemy shrugs off this turn.</summary>
        public int EnemyGuard => Intent.Kind == "guard" ? Intent.Value : 0;

        public int DamageDealt(SpinOutcome o) => Math.Max(0, o.damage - EnemyGuard);

        static bool NoAttack(string kind) => kind == "guard" || kind == "enrage" || kind == "heal" || kind == "reflect";

        /// <summary>Damage a "reflect:P" move bounces back to the party: P% of what hits the enemy. Block doesn't stop it.</summary>
        public int Reflected(SpinOutcome o) => Intent.Kind == "reflect" && !o.frozen ? DamageDealt(o) * Intent.Value / 100 : 0;

        static readonly SymbolDef Broken = new SymbolDef { id = "_jam", name = "고장", color = "#555555" };
        public int SkillCharge => cfg.skillCharge;
        public int MaxHp => cfg.playerMaxHp + RelicTotal("maxHp");
        public int RerollsPerTurn => cfg.rerollsPerTurn + RelicTotal("reroll");

        /// <summary>Nodes the party can walk to next.</summary>
        /// <summary>Ids still locked after <paramref name="unlocked"/> unlocks, in <c>cfg.unlocks</c> order.</summary>
        public static IEnumerable<string> LockedAfter(TuningConfig cfg, int unlocked) => (cfg.unlocks ?? new string[0]).Skip(unlocked);

        /// <summary>Unlocks this finished run earns: 2 for a win, 1 for a loss, 0 while it's still going.</summary>
        public int UnlocksEarned => Phase == Phase.Won ? 2 : Phase == Phase.Lost ? 1 : 0;

        IEnumerable<CompanionDef> FreeCompanions => cfg.companions.Where(c => !Party.Contains(c.id) && !Locked.Contains(c.id));

        public IEnumerable<MapNode> Available => Current == null ? Map.Rows[0] : Current.Next;

        public Run(TuningConfig cfg, Random rng, int ascension = 0) : this(cfg, rng, restoring: true)
        {
            Ascension = ascension;
            Hp = cfg.playerMaxHp;
            Gold = Ascension >= 5 ? 0 : cfg.startingGold;
            foreach (var id in cfg.startingParty) Recruit(id);
            Map = StageMap.Generate(cfg, rng);
            Phase = Phase.Map;
        }

        /// <summary>Lookups only; <see cref="FromSave"/> fills in the rest.</summary>
        Run(TuningConfig cfg, Random rng, bool restoring)
        {
            this.cfg = cfg;
            this.rng = rng;
            byId = cfg.symbols.ToDictionary(s => s.id);
            companions = cfg.companions.ToDictionary(c => c.id);
            items = (cfg.items ?? new ItemDef[0]).ToDictionary(i => i.id);
        }

        public SymbolDef Symbol(string id) => byId[id];
        public CompanionDef Member(int i) => companions[Party[i]];
        public CompanionDef Companion(string id) => companions[id];
        public ItemDef Item(string id) => items[id];

        public int Level(string symbolId) => SymbolLevel.TryGetValue(symbolId, out var l) ? l : 0;

        /// <summary>The stat an upgrade raises: the symbol's biggest one (damage, block, heal, gold, weaken).</summary>
        public static string MainStat(SymbolDef s)
        {
            var stats = new[] { ("damage", s.damage), ("block", s.block), ("heal", s.heal), ("gold", s.gold), ("weaken", s.weaken) };
            return stats.OrderByDescending(x => x.Item2).First().Item1;
        }

        public int ShopRerollCost => cfg.prices.reroll + ShopRerolls * cfg.prices.rerollStep;

        int RelicTotal(string stat) => Relics.Select(Item).Where(i => i.stat == stat).Sum(i => i.value);

        // ---------- map ----------

        /// <summary>Start a later chapter on a fresh map (screenshots, a future chapter select).</summary>
        public void JumpToChapter(int chapter)
        {
            Chapter = Math.Max(0, Math.Min(cfg.chapters.Length - 1, chapter));
            Map = StageMap.Generate(cfg, rng);
            Current = null;
            Phase = Phase.Map;
        }

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
                case NodeType.Event: OpenEvent(Pick(cfg.events.Where(e => e.chapter == 0 || e.chapter == Chapter + 1).ToArray())); break;
            }
            return true;
        }

        T Pick<T>(T[] list) => list[rng.Next(list.Length)];

        // ---------- combat ----------

        /// <summary>What committing now would do: the faces, then equipment and relics, then armed skills.</summary>
        public SpinOutcome Preview()
        {
            var skillsArmed = Armed.Select(Member).ToList();
            var faces = skillsArmed.Any(m => m.skill == "transmute") && byId.ContainsKey("coin")
                ? Faces.Select(f => f.curse ? byId["coin"] : f).ToArray() : Faces;
            if (JammedReel >= 0 && JammedReel < faces.Length) { faces = faces.ToArray(); faces[JammedReel] = Broken; }
            var o = SpinResolver.Resolve(faces, cfg);
            for (int i = 0; i < faces.Length; i++)
                foreach (var item in ItemsOn(i))
                    if (item.symbol == faces[i].id) AddStat(o, item.stat, item.value);
            foreach (var f in faces)
                if (!f.curse && Level(f.id) > 0) AddStat(o, MainStat(f), Level(f.id));
            // neighbours are the slots left and right in party order
            for (int i = 0; i < faces.Length; i++)
                foreach (int j in new[] { i - 1, i + 1 })
                {
                    if (j < 0 || j >= faces.Length || faces[j].curse || faces[j] == Broken || faces[i] == Broken) continue;
                    if (faces[i].neighborBuff > 0) AddStat(o, MainStat(faces[j]), faces[i].neighborBuff);
                    if (faces[i].twinBonus > 0 && faces[j].id == faces[i].id) o.damage += faces[i].twinBonus;
                }
            int skulls = faces.Count(f => f.curse);
            o.damage += skulls * RelicTotal("curseDamage");
            o.damage += faces.Where(f => f != Broken).GroupBy(f => f.id).Count(g => g.Count() >= 2) * RelicTotal("comboBonus");
            if (RelicTotal("noSelfDamage") > 0) o.selfDamage = 0;
            if (enemyTurn == 0) o.damage += RelicTotal("firstStrike");

            var skills = Armed.Select(Member).ToList();
            foreach (var m in skills)
            {
                if (m.skill == "shieldBash") o.damage += o.block;
                if (m.skill == "volley") o.damage += m.skillValue;
                if (m.skill == "prayer") o.heal += m.skillValue;
                if (m.skill == "backstab") o.damage += (EnemyPoison + o.poison) * m.skillValue;
                if (m.skill == "allIn") o.damage += AllInStake(m);
                if (m.skill == "boneBurst") { o.damage += skulls * m.skillValue; o.selfDamage = 0; }
            }
            foreach (var m in skills)
                if (m.skill == "rage") o.damage *= 2;
            o.frozen = skills.Any(m => m.skill == "freeze");
            o.heal += o.damage * RelicTotal("lifesteal") / 100;
            return o;
        }

        /// <summary>Gold an armed "allIn" skill spends this turn; it is also the extra damage.</summary>
        int AllInStake(CompanionDef m) => Gold * m.skillValue / 100;

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
                case "poison": o.poison += v; break;
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
            Gold += RelicTotal("rerollGold");
            SpinReel(reel);
            return true;
        }

        /// <summary>Apply the faces, items and armed skills, charge gauges, then the enemy attacks if it survived.</summary>
        public void Commit()
        {
            if (Phase != Phase.Turn) return;
            var o = Preview();
            int attack = EnemyAttack(o);
            int dealt = DamageDealt(o);
            var move = Intent;

            for (int i = 0; i < Party.Count; i++)
            {
                if (Armed.Contains(i)) Gauge[i] = 0;
                else if (i != JammedReel && Faces[i].id == Member(i).signature)
                {
                    int extra = Equipment[i] != null && Item(Equipment[i]).stat == "charge" ? Item(Equipment[i]).value : 0;
                    Gauge[i] = Math.Min(cfg.skillCharge, Gauge[i] + 1 + extra);
                }
            }
            if (Armed.Any(i => Member(i).skill == "prayer")) PurgeOneCurse();
            foreach (int b in Armed.Where(i => Member(i).skill == "bless").ToList())
                for (int i = 0; i < Party.Count; i++)
                    if (i != b) Gauge[i] = Math.Min(cfg.skillCharge, Gauge[i] + Member(b).skillValue);
            int thornsPct = Armed.Where(i => Member(i).skill == "thorns").Sum(i => Member(i).skillValue);
            int stake = Armed.Select(Member).Where(m => m.skill == "allIn").Sum(AllInStake);
            Armed.Clear();

            Gold -= Math.Min(Gold, stake);
            Hp = Math.Min(MaxHp, Hp + o.heal);
            Gold += o.gold;
            GoldEarned += o.gold;
            Turns++;
            BestHit = Math.Max(BestHit, dealt);
            EnemyHp -= dealt;
            if (EnemyHp <= 0) { Win(dealt); return; }
            // poison: this turn's stacks land, then the whole stack bites and shrinks by one
            EnemyPoison += o.poison;
            if (EnemyPoison > 0)
            {
                EnemyHp -= EnemyPoison;
                dealt += EnemyPoison;
                EnemyPoison--;
                if (EnemyHp <= 0) { Win(dealt); return; }
            }

            bool newPhase = !EnemyPhase2 && Enemy.phase2 != null && Enemy.phase2.Length > 0 && EnemyHp * 2 <= EnemyMaxHp;
            int through = Math.Max(0, attack - o.block);
            int reflected = Reflected(o);
            int hit = through + o.selfDamage + reflected;
            Hp -= hit;
            string note = reflected > 0 ? $", 가시 갑피로 {reflected} 되돌아옴" : "";
            if (!o.frozen)
            {
                switch (move.Kind)
                {
                    case "drain":
                        EnemyHp = Math.Min(EnemyMaxHp, EnemyHp + through);
                        if (through > 0) note = $", 적이 {through} 흡혈";
                        break;
                    case "curse":
                        int reel = rng.Next(Reels.Count);
                        Reels[reel].Add(cfg.symbols.First(s => s.curse).id);
                        tempCurses.Add(reel);
                        note = $", {Member(reel).name} 슬롯에 저주";
                        break;
                    case "enrage":
                        EnemyRage += move.Value;
                        note = $", 적 공격력 +{move.Value}";
                        break;
                    case "heal":
                        int healed = Math.Min(EnemyMaxHp - EnemyHp, move.Value);
                        EnemyHp += healed;
                        if (healed > 0) note += $", 적이 {healed} 회복";
                        break;
                    case "sap":
                        int fullest = Enumerable.Range(0, Gauge.Count).OrderByDescending(i => Gauge[i]).First();
                        if (Gauge[fullest] > 0)
                        {
                            Gauge[fullest] = 0;
                            note += $", {Member(fullest).name}의 스킬 게이지를 빨아들였다";
                        }
                        break;
                    case "steal":
                        int taken = Math.Min(Gold, through);
                        Gold -= taken;
                        EnemyStolen += taken;
                        if (taken > 0) note = $", {taken} 골드를 빼앗겼다";
                        break;
                }
            }
            // thorns (skill) and spikes (relic) hurt an enemy that actually attacked
            if (!o.frozen && attack > 0)
            {
                int back = attack * thornsPct / 100 + RelicTotal("spikes");
                if (back > 0)
                {
                    EnemyHp -= back;
                    note += $", 가시로 {back} 반사";
                    if (EnemyHp <= 0 && Hp > 0) { Win(dealt + back); return; }
                }
            }
            if (newPhase)
            {
                EnemyPhase2 = true;
                enemyTurn = 0;
                note += $" · {Enemy.name} {(string.IsNullOrEmpty(Enemy.phase2Name) ? "2단계" : Enemy.phase2Name)}!";
            }
            else enemyTurn++;
            Log = $"{dealt} 피해를 주고 {hit} 피해를 받았다{note}";
            if (Hp <= 0) { Hp = 0; Phase = Phase.Lost; return; }
            StartTurn();
        }

        void Win(int damage)
        {
            EnemyHp = 0;
            ClearTempCurses();
            Gold += Enemy.gold + EnemyStolen;
            GoldEarned += Enemy.gold;
            Kills++;
            Log = $"{Enemy.name} 처치! ({damage} 피해, +{Enemy.gold} 골드)" + (EnemyStolen > 0 ? $" · 빼앗긴 {EnemyStolen} 골드 되찾음" : "");
            EnemyStolen = 0;
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
            if (victoryRelic)
            {
                victoryRelic = false;
                Offers.Add(RelicOffer());
                Offers.Add(EquipOffer());
                Offers.Add(UpgradeOffer(Party.Select(id => companions[id].signature).First()));
                OpenOffers("그림자가 남긴 보물을 하나 고르세요");
                return;
            }
            if (victoryUpgradeChoices > 0)
            {
                var owned = Reels.SelectMany(reel => reel).ToHashSet();
                var candidates = Party.AsEnumerable().Reverse().Select(id => companions[id].signature)
                    .Concat(Reels.SelectMany(reel => reel)).Distinct()
                    .Where(id => owned.Contains(id) && !byId[id].curse);
                Offers.AddRange(candidates.Take(victoryUpgradeChoices).Select(UpgradeOffer));
                victoryUpgradeChoices = 0;
                if (Offers.Count > 0)
                {
                    OpenOffers("왕실 장부: 강화할 심볼을 고르세요");
                    return;
                }
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
            float hpScale = 1 + (Ascension >= 1 ? 0.1f : 0) + (Ascension >= 5 && Current?.Type == NodeType.Boss ? 0.15f : 0);
            EnemyMaxHp = (int)Math.Round(enemy.hp * hpScale);
            EnemyHp = EnemyMaxHp;
            EnemyPoison = 0;
            EnemyStolen = 0;
            EnemyPhase2 = false;
            enemyTurn = 0;
            EnemyRage = 0;
            ClearTempCurses();
            Phase = Phase.Turn;
            int charge = RelicTotal("battleCharge");
            for (int i = 0; i < Gauge.Count; i++) Gauge[i] = Math.Min(cfg.skillCharge, Gauge[i] + charge);
            StartTurn();
        }

        void StartTurn()
        {
            RerollsLeft = Intent.Kind == "seal" ? 0 : RerollsPerTurn; // seal: no reroll this turn
            JammedReel = Intent.Kind == "jam" ? rng.Next(Reels.Count) : -1;
            if (enemyTurn > 0) Hp = Math.Min(MaxHp, Hp + RelicTotal("regen"));
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
            int rest = Ascension >= 2 ? cfg.restHeal : cfg.restHeal * 2;
            Offers.Add(new Reward { Kind = RewardKind.Heal, Amount = rest, Text = $"푹 쉬기: HP {rest} 회복" });
            var curse = CurseOffer();
            if (curse != null) Offers.Add(curse);
            OpenOffers("모닥불에서 쉬어 간다");
        }

        void OpenRecruit()
        {
            Offers.Clear();
            var free = FreeCompanions.OrderBy(_ => rng.Next()).Take(3);
            if (Party.Count < MaxParty)
                foreach (var c in free) Offers.Add(RecruitOffer(c, ChapterDef.recruitSignatureBonus));
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

        int victoryUpgradeChoices;
        bool victoryRelic;

        void OpenEvent(EventDef e)
        {
            CurrentEvent = e;
            Phase = Phase.Event;
        }

        /// <summary>False when an option costs more gold than the party has.</summary>
        public bool CanChoose(EventOption option) => option.effects.All(e =>
            e.kind == "gamble" || e.kind == "slot" ? Gold >= e.value
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
                        var free = FreeCompanions.ToArray();
                        if (Party.Count < MaxParty && free.Length > 0) { var c = Pick(free); Recruit(c.id); log.Add($"{c.name} 합류"); }
                        else { Gold += cfg.treasureGold; log.Add($"+{cfg.treasureGold} 골드"); }
                        break;
                    case "curse":
                        int reel = rng.Next(Reels.Count);
                        var curse = cfg.symbols.First(s => s.curse);
                        Reels[reel].Add(curse.id);
                        log.Add($"{Member(reel).name} 슬롯에 {curse.name}");
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
                        victoryUpgradeChoices = option.victoryUpgradeChoices;
                        victoryRelic = option.victoryRelic;
                        log.Add("싸움이 벌어졌다!");
                        break;
                    case "mirror": // the party's own reflection: as tough as the party is right now
                        int hitBase = 4 + 3 * Chapter + Party.Count;
                        StartFight(new EnemyDef
                        {
                            name = "거울 속 파티", sprite = Member(0).sprite + "_idle_anim", tint = "#b4a0ff",
                            hp = Math.Max(10, Hp), gold = 20 + 10 * Chapter,
                            moves = new[] { $"attack:{hitBase}", $"guard:{hitBase}", $"attack:{hitBase + 3}", $"heal:{hitBase}" },
                        });
                        victoryUpgradeChoices = option.victoryUpgradeChoices;
                        victoryRelic = option.victoryRelic;
                        log.Add("거울 속 파티가 칼을 뽑았다!");
                        break;
                    case "shadow": // this chapter's boss at reduced HP, without its second phase
                        var b = ChapterDef.boss;
                        StartFight(new EnemyDef
                        {
                            name = b.name + "의 그림자", sprite = b.sprite, tint = "#7060a0",
                            hp = b.hp * Math.Max(10, e.value) / 100, gold = 20, moves = b.moves,
                        });
                        victoryUpgradeChoices = option.victoryUpgradeChoices;
                        victoryRelic = option.victoryRelic;
                        log.Add($"{b.name}의 그림자가 일어섰다!");
                        break;
                    case "coinflip":
                        if (rng.Next(2) == 0) { var r = RelicOffer(); Apply(r); log.Add("앞면! " + r.Text); }
                        else
                        {
                            int fr = rng.Next(Reels.Count);
                            Reels[fr].Add(cfg.symbols.First(x => x.curse).id);
                            log.Add($"뒷면… {Member(fr).name} 슬롯에 해골");
                        }
                        break;
                    case "hpFull":
                        log.Add(Hp == MaxHp ? "HP는 이미 가득" : $"HP +{MaxHp - Hp} (가득)");
                        Hp = MaxHp;
                        break;
                    case "goldAll":
                        log.Add($"-{Gold} 골드");
                        Gold = 0;
                        break;
                    case "transform": // target "from>to": every reel's from faces become to
                        var pair = (e.target ?? "").Split('>');
                        if (pair.Length == 2 && byId.ContainsKey(pair[0]) && byId.ContainsKey(pair[1]))
                        {
                            int changed = 0;
                            foreach (var reelFaces in Reels)
                                for (int k = 0; k < reelFaces.Count; k++)
                                    if (reelFaces[k] == pair[0]) { reelFaces[k] = pair[1]; changed++; }
                            log.Add($"{byId[pair[0]].name} {changed}칸이 {byId[pair[1]].name}(으)로");
                        }
                        break;
                    case "fuse": // melt one copy of a doubled face; that symbol gains two levels
                        for (int t = 0; t < Math.Max(1, e.value); t++)
                        {
                            var spot = Enumerable.Range(0, Reels.Count)
                                .SelectMany(i => Reels[i].Where(id => !byId[id].curse).Distinct()
                                    .Where(id => Reels[i].Count(x => x == id) >= 2).Select(id => (reel: i, id)))
                                .OrderByDescending(p => p.id == Member(p.reel).signature).FirstOrDefault();
                            if (spot.id == null) { log.Add("녹일 겹친 심볼이 없다"); break; }
                            Reels[spot.reel].Remove(spot.id);
                            SymbolLevel[spot.id] = Level(spot.id) + 2;
                            log.Add($"{Member(spot.reel).name} 슬롯 {byId[spot.id].name} 융합: Lv{Level(spot.id) + 1}");
                        }
                        break;
                    case "relicId":
                        if (items.TryGetValue(e.target ?? "", out var named) && !Relics.Contains(named.id))
                        {
                            Apply(new Reward { Kind = RewardKind.Relic, ItemId = named.id });
                            log.Add($"유물: {named.name}");
                        }
                        else { Gold += cfg.treasureGold; log.Add($"+{cfg.treasureGold} 골드"); }
                        break;
                    case "slot": // pay, pull, one of four outcomes
                        Gold -= e.value;
                        switch (rng.Next(4))
                        {
                            case 0: { var r = RelicOffer(); Apply(r); log.Add("잭팟! " + r.Text); break; }
                            case 1: { var r = EquipOffer(); Apply(r); log.Add(r.Text); break; }
                            case 2: Hp = Math.Min(MaxHp, Hp + 20); log.Add("HP +20"); break;
                            default:
                                int cr = rng.Next(Reels.Count);
                                Reels[cr].Add(cfg.symbols.First(x => x.curse).id);
                                log.Add($"꽝! {Member(cr).name} 슬롯에 해골");
                                break;
                        }
                        break;
                    case "upgrade":
                        var owned = Reels.SelectMany(r => r).Distinct().Where(id => !byId[id].curse).ToArray();
                        if (owned.Length > 0)
                        {
                            var up = Pick(owned);
                            SymbolLevel[up] = Level(up) + 1;
                            log.Add($"{byId[up].name} 강화 Lv{Level(up) + 1}");
                        }
                        break;
                    case "trim":
                        for (int t = 0; t < Math.Max(1, e.value); t++)
                        {
                            var cut = TrimOffer();
                            if (cut == null) break;
                            Apply(cut);
                            log.Add(cut.Text);
                        }
                        break;
                    case "charge":
                        for (int i = 0; i < Gauge.Count; i++) Gauge[i] = cfg.skillCharge;
                        log.Add("모든 스킬 게이지 가득");
                        break;
                    case "dupe":
                        int dr = rng.Next(Reels.Count);
                        var good = Reels[dr].Where(id => !byId[id].curse).ToArray();
                        if (good.Length > 0)
                        {
                            var copy = Pick(good);
                            for (int c = 0; c < Math.Max(1, e.value); c++) Reels[dr].Add(copy);
                            log.Add($"{Member(dr).name} 슬롯에 {byId[copy].name} x{Math.Max(1, e.value)}");
                        }
                        break;
                }
            }
            Log = string.Join(", ", log);
            return true;
        }

        // ---------- shop ----------

        /// <summary>Spend gold to replace every shop item. Each restock this run costs more.</summary>
        public bool RerollShop()
        {
            if (Phase != Phase.Shop || Gold < ShopRerollCost) return false;
            Gold -= ShopRerollCost;
            ShopRerolls++;
            OpenShop();
            Log = "상점 물건이 바뀌었다";
            return true;
        }

        void OpenShop()
        {
            var p = cfg.prices;
            ShopItems.Clear();
            // upgrades for two symbols the party actually has, and one trim
            var owned = Reels.SelectMany(r => r).Distinct().Where(id => !byId[id].curse).OrderBy(_ => rng.Next()).Take(2);
            foreach (var id in owned) ShopItems.Add(Priced(UpgradeOffer(id), p.upgrade + Level(id) * p.upgradeStep));
            var trim = TrimOffer();
            if (trim != null) ShopItems.Add(Priced(trim, p.trim));
            for (int k = 0; k < 2; k++) ShopItems.Add(Priced(AddOffer(), p.addSymbol));
            var curse = CurseOffer();
            if (curse != null) ShopItems.Add(Priced(curse, p.removeCurse));
            ShopItems.Add(new Reward { Kind = RewardKind.Heal, Amount = p.healAmount, Price = p.heal, Text = $"HP {p.healAmount} 회복" });
            var equip = EquipOffer();
            if (equip.Kind == RewardKind.Equip) ShopItems.Add(Priced(equip, p.equip));
            var relic = RelicOffer();
            if (relic.Kind == RewardKind.Relic) ShopItems.Add(Priced(relic, p.relic));
            var free = FreeCompanions.ToArray();
            if (Party.Count < MaxParty && free.Length > 0) ShopItems.Add(Priced(RecruitOffer(Pick(free)), p.recruit));
            Phase = Phase.Shop;
        }

        Reward Priced(Reward r, int price)
        {
            r.Price = Ascension >= 3 ? (int)Math.Ceiling(price * 1.2) : price;
            return r;
        }

        /// <summary>Buy one shop item. Returns false if it is sold out or too expensive.</summary>
        public bool Buy(int index)
        {
            if (Phase != Phase.Shop) return false;
            var item = ShopItems[index];
            if (item.Sold || Gold < item.Price) return false;
            if (item.Kind == RewardKind.Recruit && Party.Count >= MaxParty) return false;
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
                case RewardKind.Recruit: Recruit(r.CompanionId, r.SymbolId); break;
                case RewardKind.Gold: Gold += r.Amount; break;
                case RewardKind.Equip: Equipment[r.Reel] = r.ItemId; break;
                case RewardKind.Upgrade: SymbolLevel[r.SymbolId] = Level(r.SymbolId) + 1; break;
                case RewardKind.Relic:
                    Relics.Add(r.ItemId);
                    if (Item(r.ItemId).stat == "maxHp") Hp += Item(r.ItemId).value;
                    break;
            }
        }

        /// <summary>Add a member and their reel (already spun). Does not check maxParty; callers do.</summary>
        public void Recruit(string id) => Recruit(id, null);

        void Recruit(string id, string bonusSymbol)
        {
            Party.Add(id);
            Reels.Add(companions[id].reel.ToList());
            if (bonusSymbol != null) Reels[Reels.Count - 1].Add(bonusSymbol);
            Gauge.Add(0);
            Equipment.Add(null);
            var faces = Faces;
            Array.Resize(ref faces, Reels.Count);
            Faces = faces;
            SpinReel(Reels.Count - 1);
        }

        void ClearTempCurses()
        {
            var curse = cfg.symbols.FirstOrDefault(s => s.curse);
            foreach (int reel in tempCurses) Reels[reel].Remove(curse.id);
            tempCurses.Clear();
        }

        void PurgeOneCurse()
        {
            int index = Reels.FindIndex(reel => reel.Count > 1 && reel.Any(id => byId[id].curse));
            if (index < 0) return;
            var faces = Reels[index];
            // Purify a persistent curse first. If all curses here came from combat,
            // take one off the cleanup list as well.
            if (faces.Count(id => byId[id].curse) == tempCurses.Count(reel => reel == index))
                tempCurses.Remove(index);
            faces.Remove(faces.First(id => byId[id].curse));
        }

        Reward AddOffer()
        {
            var good = cfg.symbols.Where(s => !s.curse).ToArray();
            var s = Pick(good);
            int reel = rng.Next(Reels.Count);
            return new Reward { Kind = RewardKind.AddSymbol, Reel = reel, SymbolId = s.id, Text = $"{Member(reel).name} 슬롯에 {s.name} 추가" };
        }

        // ponytail: removes only curses; offer removing any face once reels get crowded.
        Reward CurseOffer()
        {
            int cursed = Reels.FindIndex(r => r.Count > 1 && r.Any(id => byId[id].curse));
            if (cursed < 0) return null;
            var id = Reels[cursed].First(x => byId[x].curse);
            return new Reward { Kind = RewardKind.RemoveSymbol, Reel = cursed, SymbolId = id, Text = $"{Member(cursed).name} 슬롯에서 {byId[id].name} 제거" };
        }

        Reward UpgradeOffer(string id)
        {
            var s = byId[id];
            string stat = MainStat(s) == "damage" ? "공격" : MainStat(s) == "block" ? "방어" : MainStat(s) == "heal" ? "회복" : MainStat(s) == "gold" ? "골드" : "약화";
            return new Reward { Kind = RewardKind.Upgrade, SymbolId = id, Text = $"{s.name} 강화 Lv{Level(id) + 1}: {stat} +1" };
        }

        /// <summary>Remove the weakest ordinary symbol from a reel that has more than three faces.</summary>
        Reward TrimOffer()
        {
            var candidates = Enumerable.Range(0, Reels.Count).Where(i => Reels[i].Count > 3).ToList();
            if (candidates.Count == 0) return null;
            int reel = candidates[rng.Next(candidates.Count)];
            var id = Reels[reel].Where(x => !byId[x].curse && x != Member(reel).signature) // keep the skill's fuel
                .OrderBy(x => byId[x].damage + byId[x].block + byId[x].heal + byId[x].gold).FirstOrDefault();
            if (id == null) return null;
            return new Reward { Kind = RewardKind.RemoveSymbol, Reel = reel, SymbolId = id, Text = $"{Member(reel).name} 슬롯 다듬기: {byId[id].name} 빼기" };
        }

        Reward RecruitOffer(CompanionDef c, bool bonus = false)
        {
            var reward = new Reward { Kind = RewardKind.Recruit, CompanionId = c.id, Text = $"{c.name} 영입 ({c.skillName})" };
            if (bonus)
            {
                reward.SymbolId = c.signature;
                reward.Text += $" · {byId[c.signature].name} +1칸";
            }
            return reward;
        }

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
            var pool = items.Values.Where(i => i.slot == "relic" && !Relics.Contains(i.id) && !Locked.Contains(i.id)).ToArray();
            if (pool.Length == 0) return GoldOffer(cfg.treasureGold * 2, "유물 대신");
            var item = Pick(pool);
            return new Reward { Kind = RewardKind.Relic, ItemId = item.id, Text = $"유물: {item.name}" };
        }
    }
}
