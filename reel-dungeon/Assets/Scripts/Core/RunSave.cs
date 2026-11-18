using System;
using System.Collections.Generic;
using System.Linq;

namespace ReelDungeon.Core
{
    /// <summary>
    /// Everything needed to resume a run, in a JsonUtility-friendly shape (arrays, no dictionaries or nested lists).
    /// Randomness is not saved: a resumed run continues from a fresh seed taken at save time.
    /// </summary>
    [Serializable]
    public class RunSave
    {
        public int version = 1;
        public int seed;
        public string phase;
        public string[] party;
        public ReelSave[] reels;
        public int[] gauge;
        public string[] equipment;   // "" for an empty slot
        public string[] relics;
        public string[] levelIds;
        public int[] levelValues;
        public int hp, gold, chapter, shopRerolls, turns, kills, bestHit, goldEarned;
        public int[] mapRowSizes;    // node count per row; the last row is the boss
        public string[] mapTypes;    // node types, row by row
        public bool[] mapVisited;
        public int currentRow = -1, currentLane = -1;
        public string enemyName;
        public int enemyHp, enemyTurn, enemyRage, rerollsLeft;
        public int[] tempCurses;
        public string[] faces;
        public int[] armed;
        public Reward[] offers;
        public string offerTitle;
        public Reward[] shop;
        public string eventId;
        public string log;
    }

    [Serializable]
    public class ReelSave
    {
        public string[] faces;
    }

    public partial class Run
    {
        /// <summary>Phases a run can be resumed from (not Won or Lost).</summary>
        public bool CanSave => Phase != Phase.Won && Phase != Phase.Lost;

        public RunSave ToSave()
        {
            var nodes = Map.Rows.SelectMany(r => r).ToArray();
            return new RunSave
            {
                seed = rng.Next(),
                phase = Phase.ToString(),
                party = Party.ToArray(),
                reels = Reels.Select(r => new ReelSave { faces = r.ToArray() }).ToArray(),
                gauge = Gauge.ToArray(),
                equipment = Equipment.Select(e => e ?? "").ToArray(),
                relics = Relics.ToArray(),
                levelIds = SymbolLevel.Keys.ToArray(),
                levelValues = SymbolLevel.Values.ToArray(),
                hp = Hp, gold = Gold, chapter = Chapter, shopRerolls = ShopRerolls,
                turns = Turns, kills = Kills, bestHit = BestHit, goldEarned = GoldEarned,
                mapRowSizes = Map.Rows.Select(r => r.Length).ToArray(),
                mapTypes = nodes.Select(n => n.Type.ToString()).ToArray(),
                mapVisited = nodes.Select(n => n.Visited).ToArray(),
                currentRow = Current?.Row ?? -1,
                currentLane = Current == null ? -1 : Array.IndexOf(Map.Rows[Current.Row], Current),
                enemyName = Enemy?.name ?? "",
                enemyHp = EnemyHp, enemyTurn = enemyTurn, enemyRage = EnemyRage, rerollsLeft = RerollsLeft,
                tempCurses = tempCurses.ToArray(),
                faces = Faces.Select(f => f?.id ?? "").ToArray(),
                armed = Armed.ToArray(),
                offers = Offers.ToArray(),
                offerTitle = OfferTitle,
                shop = ShopItems.ToArray(),
                eventId = CurrentEvent?.id ?? "",
                log = Log,
            };
        }

        /// <summary>Rebuild a run from <see cref="ToSave"/>. Throws if the save names content the config no longer has.</summary>
        public static Run FromSave(TuningConfig cfg, RunSave s)
        {
            var run = new Run(cfg, new Random(s.seed), restoring: true);
            run.Party.AddRange(s.party);
            run.Reels.AddRange(s.reels.Select(r => r.faces.ToList()));
            run.Gauge.AddRange(s.gauge);
            run.Equipment.AddRange(s.equipment.Select(e => e == "" ? null : e));
            run.Relics.AddRange(s.relics);
            for (int i = 0; i < s.levelIds.Length; i++) run.SymbolLevel[s.levelIds[i]] = s.levelValues[i];
            run.Hp = s.hp;
            run.Gold = s.gold;
            run.Chapter = s.chapter;
            run.ShopRerolls = s.shopRerolls;
            run.Turns = s.turns;
            run.Kills = s.kills;
            run.BestHit = s.bestHit;
            run.GoldEarned = s.goldEarned;

            var rows = new List<NodeType[]>();
            int k = 0;
            foreach (int size in s.mapRowSizes)
            {
                rows.Add(s.mapTypes.Skip(k).Take(size).Select(t => (NodeType)Enum.Parse(typeof(NodeType), t)).ToArray());
                k += size;
            }
            run.Map = StageMap.FromTypes(rows);
            var nodes = run.Map.Rows.SelectMany(r => r).ToArray();
            for (int i = 0; i < nodes.Length; i++) nodes[i].Visited = s.mapVisited[i];
            run.Current = s.currentRow >= 0 ? run.Map.Rows[s.currentRow][s.currentLane] : null;

            run.Enemy = s.enemyName == "" ? null : AllEnemies(cfg).First(e => e.name == s.enemyName);
            run.EnemyHp = s.enemyHp;
            run.enemyTurn = s.enemyTurn;
            run.EnemyRage = s.enemyRage;
            run.RerollsLeft = s.rerollsLeft;
            run.tempCurses.AddRange(s.tempCurses);
            run.Faces = s.faces.Select(id => id == "" ? null : run.byId[id]).ToArray();
            foreach (int a in s.armed) run.Armed.Add(a);
            run.Offers.AddRange(s.offers.Select(Clean));
            run.OfferTitle = s.offerTitle;
            run.ShopItems.AddRange(s.shop.Select(Clean));
            run.CurrentEvent = s.eventId == "" ? null : cfg.events.First(e => e.id == s.eventId);
            run.Log = s.log;
            run.Phase = (Phase)Enum.Parse(typeof(Phase), s.phase);
            return run;
        }

        static IEnumerable<EnemyDef> AllEnemies(TuningConfig cfg) =>
            cfg.chapters.SelectMany(c => c.enemies.Concat(c.elites).Append(c.boss)).Append(cfg.mimic);

        /// <summary>JsonUtility turns null strings into ""; the rest of the code checks for null.</summary>
        static Reward Clean(Reward r)
        {
            if (r.SymbolId == "") r.SymbolId = null;
            if (r.CompanionId == "") r.CompanionId = null;
            if (r.ItemId == "") r.ItemId = null;
            return r;
        }
    }
}
