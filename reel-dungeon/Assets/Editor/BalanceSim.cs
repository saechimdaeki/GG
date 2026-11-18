using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReelDungeon.Core;
using UnityEngine;

/// <summary>
/// Plays many runs with a simple greedy bot and prints win rate and where runs end.
/// Unity -batchmode -quit -projectPath . -executeMethod BalanceSim.Run [-simRuns 500]
/// Emits observations only; deciding what to tune is a person's call.
/// </summary>
public static class BalanceSim
{
    public static void Run()
    {
        var cfg = JsonUtility.FromJson<TuningConfig>(File.ReadAllText("Assets/Resources/tuning.json"));
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-simRuns");
        int runs = i >= 0 && int.TryParse(args[i + 1], out var n) ? n : 500;
        int a = Array.IndexOf(args, "-simAscension");
        int ascension = a >= 0 && int.TryParse(args[a + 1], out var asc) ? asc : 0;

        var endings = new Dictionary<string, int>();
        int wins = 0;
        var chapterReached = new int[cfg.chapters.Length + 1];
        double turns = 0, party = 0, gold = 0, earned = 0, upgrades = 0;
        var bossHp = new List<float>[cfg.chapters.Length];
        for (int c = 0; c < bossHp.Length; c++) bossHp[c] = new List<float>();
        for (int seed = 0; seed < runs; seed++)
        {
            var (run, t) = Play(cfg, seed, bossHp, ascension);
            turns += t;
            party += run.Party.Count;
            gold += run.Gold;
            earned += run.GoldEarned + cfg.startingGold;
            upgrades += run.SymbolLevel.Values.Sum();
            if (run.Phase == Phase.Won) { wins++; chapterReached[cfg.chapters.Length]++; continue; }
            chapterReached[run.Chapter]++;
            string where = $"{run.Chapter + 1}장 {run.Current?.Type} {run.Enemy?.name}";
            endings[where] = endings.TryGetValue(where, out var c) ? c + 1 : 1;
        }

        var report = new List<string>
        {
            $"ascension {ascension}  runs {runs}  win {100.0 * wins / runs:F1}%  avg turns {turns / runs:F1}  avg party {party / runs:F2}  avg gold left {gold / runs:F0}",
            $"gold: earned {earned / runs:F0}, spent {(earned - gold) / runs:F0} ({100 * (earned - gold) / earned:F0}%), upgrades bought {upgrades / runs:F1}",
            "ended in chapter: " + string.Join("  ", chapterReached.Select((v, k) => k < cfg.chapters.Length ? $"{k + 1}장 {v}" : $"클리어 {v}")),
            "HP at boss start: " + string.Join("  ", bossHp.Select((l, k) => l.Count == 0 ? $"{k + 1}장 -" : $"{k + 1}장 {100 * l.Average():F0}%")),
            "top deaths:",
        };
        report.AddRange(endings.OrderByDescending(e => e.Value).Take(8).Select(e => $"  {e.Value,4}  {e.Key}"));
        foreach (var line in report) Console.WriteLine("[sim] " + line);
    }

    static (Run run, int turns) Play(TuningConfig cfg, int seed, List<float>[] bossHp, int ascension)
    {
        var rng = new System.Random(seed * 7919 + 1);
        var run = new Run(cfg, new System.Random(seed), ascension);
        int turns = 0;
        for (int step = 0; step < 5000 && run.Phase != Phase.Won && run.Phase != Phase.Lost; step++)
        {
            switch (run.Phase)
            {
                case Phase.Map:
                    var node = PickNode(run);
                    if (node.Type == NodeType.Boss) bossHp[run.Chapter].Add(HpFrac(run));
                    run.Choose(node);
                    break;
                case Phase.Turn: PlayTurn(run); turns++; break;
                case Phase.Reward: run.ChooseReward(PickOffer(run, run.Offers)); break;
                case Phase.Shop: Shop(run); break;
                case Phase.Event: PickEvent(run, rng); break;
            }
        }
        return (run, turns);
    }

    static float HpFrac(Run run) => (float)run.Hp / run.MaxHp;

    static MapNode PickNode(Run run)
    {
        float hp = HpFrac(run);
        int Score(MapNode n)
        {
            switch (n.Type)
            {
                case NodeType.Rest: return hp < 0.6f ? 90 : 20;
                case NodeType.Shop: return run.Gold >= 60 ? 70 : 15;
                case NodeType.Recruit: return run.Party.Count < 5 ? 80 : 5;
                case NodeType.Elite: return hp > 0.7f ? 50 : 0;
                case NodeType.Treasure: return 55;
                case NodeType.Event: return 45;
                default: return 30;
            }
        }
        return run.Available.OrderByDescending(Score).First();
    }

    static void PlayTurn(Run run)
    {
        // reroll the worst face once: a curse first, else the face doing least
        while (run.RerollsLeft > 0)
        {
            int worst = Enumerable.Range(0, run.Faces.Length)
                .OrderByDescending(k => run.Faces[k].curse ? 100 : 0)
                .ThenBy(k => run.Faces[k].damage + run.Faces[k].block + run.Faces[k].heal)
                .First();
            if (!run.Faces[worst].curse && run.Faces[worst].damage + run.Faces[worst].block > 0) break;
            run.Reroll(worst);
        }
        for (int k = 0; k < run.Party.Count; k++)
        {
            if (!run.SkillReady(k)) continue;
            var skill = run.Member(k).skill;
            bool worth = skill == "freeze" || skill == "thorns" ? run.EnemyIntent >= 8
                : skill == "prayer" ? HpFrac(run) < 0.7f
                : skill == "transmute" ? run.Faces.Any(f => f.curse)
                : skill == "backstab" ? run.EnemyPoison + run.Preview().poison >= 3
                : true;
            if (worth) run.ToggleSkill(k);
        }
        run.Commit();
    }

    static int Value(Run run, Reward r)
    {
        switch (r.Kind)
        {
            case RewardKind.Recruit:
                // a player compares recruits: average punch of the reel they bring
                var reel = run.Companion(r.CompanionId).reel.Select(run.Symbol).ToArray();
                return 100 + (int)(10 * reel.Average(x => x.damage * 2 + x.block + x.heal + x.poison * 3 + x.weaken));
            case RewardKind.Relic: return 90;
            case RewardKind.Equip: return 70;
            case RewardKind.Upgrade: return 75;
            case RewardKind.RemoveSymbol: return 60;
            case RewardKind.Heal: return HpFrac(run) < 0.5f ? 85 : 10;
            case RewardKind.Gold: return 40;
            case RewardKind.AddSymbol:
                var s = run.Symbol(r.SymbolId);
                return 30 + s.damage * 3 + s.block * 2 + s.heal;
            default: return 0;
        }
    }

    static int PickOffer(Run run, List<Reward> offers) =>
        offers.Count == 0 ? -1 : offers.IndexOf(offers.OrderByDescending(r => Value(run, r)).First());

    static void Shop(Run run)
    {
        bool bought = true;
        int restocks = 0;
        while (bought)
        {
            bought = false;
            foreach (var k in Enumerable.Range(0, run.ShopItems.Count).OrderByDescending(k => Value(run, run.ShopItems[k])))
            {
                var item = run.ShopItems[k];
                if (item.Sold || item.Price > run.Gold) continue;
                if (item.Kind == RewardKind.Heal && HpFrac(run) > 0.6f) continue;
                if (item.Kind == RewardKind.AddSymbol && run.Gold < 70) continue; // save for better things
                if (run.Buy(k)) { bought = true; break; }
            }
            // rich and nothing left worth buying: restock, at most twice per visit
            if (!bought && restocks < 2 && run.Gold >= run.ShopRerollCost + 60 && run.RerollShop()) { restocks++; bought = true; }
        }
        run.LeaveShop();
    }

    static void PickEvent(Run run, System.Random rng)
    {
        var options = run.CurrentEvent.options;
        var ok = Enumerable.Range(0, options.Length).Where(k => run.CanChoose(options[k]))
            .Where(k => HpFrac(run) > 0.5f || !options[k].effects.Any(e => e.kind == "fight" || e.kind == "hp" && e.value < 0))
            .ToList();
        run.ChooseEvent(ok.Count > 0 ? ok[rng.Next(ok.Count)] : options.Length - 1);
    }
}
