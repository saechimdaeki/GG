using System;
using System.Linq;
using NUnit.Framework;
using ReelDungeon.Core;

public class RunTests
{
    static TuningConfig ProductionConfig() => UnityEngine.JsonUtility.FromJson<TuningConfig>(
        UnityEngine.Resources.Load<UnityEngine.TextAsset>("tuning").text);

    [Test]
    public void ProductionReels_StartWithoutSelfDamageAndIncludeNewAttacks()
    {
        var cfg = ProductionConfig();
        var run = new Run(cfg, new Random(1));
        Assert.IsTrue(cfg.companions.Where(member => !run.Symbol(member.signature).curse).SelectMany(member => member.reel)
            .Select(run.Symbol).All(face => !face.curse && face.selfDamage == 0));
        CollectionAssert.Contains(run.Reels[0], "parry");
        CollectionAssert.Contains(run.Reels[1], "bounty");
        Assert.AreEqual(3, run.Reels[0].Count(id => run.Symbol(id).damage > 0));
        Assert.AreEqual(5, run.Reels[1].Count(id => run.Symbol(id).damage > 0));
        var outcome = SpinResolver.Resolve(new[] { run.Symbol("parry"), run.Symbol("bounty") }, cfg);
        Assert.AreEqual(4, outcome.damage);
        Assert.AreEqual(2, outcome.block);
        Assert.AreEqual(2, outcome.gold);
    }

    [Test]
    public void PrisonKey_ChoiceAddsPersistentCurseOrLeavesReelsClean()
    {
        var cfg = ProductionConfig();
        var prison = cfg.events.Single(e => e.id == "prison_key");
        Assert.AreEqual(2, prison.chapter);
        cfg.events = new[] { prison };

        Run EnterPrison()
        {
            var run = new Run(cfg, new Random(1));
            run.JumpToChapter(1);
            var node = run.Map.Rows[0][0];
            node.Type = NodeType.Event;
            Assert.IsTrue(run.Choose(node));
            return run;
        }

        var cursed = EnterPrison();
        Assert.IsTrue(cursed.ChooseEvent(0));
        Assert.AreEqual(1, cursed.Relics.Count);
        Assert.AreEqual(1, Skulls(Run.FromSave(cfg, cursed.ToSave())));
        var declined = EnterPrison();
        Assert.IsTrue(declined.ChooseEvent(1));
        Assert.AreEqual(0, Skulls(declined));
        Assert.IsEmpty(declined.Relics);
    }

    [Test]
    public void ProductionContent_UsesKnownMovesAndUnlockIds()
    {
        var cfg = ProductionConfig();
        var kinds = new[] { "attack", "guard", "curse", "enrage", "drain", "steal", "seal", "heal", "jam", "sap", "reflect" };
        var enemies = cfg.chapters.SelectMany(c => c.enemies.Concat(c.elites).Append(c.boss)).Append(cfg.mimic);
        foreach (var e in enemies)
        {
            CollectionAssert.IsSubsetOf(e.moves.Concat(e.phase2 ?? new string[0]).Select(m => m.Split(':')[0]).Distinct(), kinds, e.name);
            if (!string.IsNullOrEmpty(e.tint)) Assert.IsTrue(UnityEngine.ColorUtility.TryParseHtmlString(e.tint, out _), e.name);
        }
        Assert.AreEqual(enemies.Count(), enemies.Select(e => e.name).Distinct().Count(), "enemy names are save keys");
        var ids = cfg.companions.Select(c => c.id).Concat(cfg.items.Select(i => i.id)).ToHashSet();
        CollectionAssert.IsSubsetOf(cfg.unlocks, ids);
        CollectionAssert.IsEmpty(cfg.unlocks.Intersect(cfg.startingParty));
    }

    [Test]
    public void Steal_TakesUnblockedGoldAndReturnsItOnKill()
    {
        var run = Enter(Config("sword", Trio, Moves(30, "steal:6")));
        run.Commit(); // 18 damage, 6 gets through
        Assert.AreEqual(94, run.Gold);
        Assert.AreEqual(6, run.EnemyStolen);
        Assert.AreEqual(6, Run.FromSave(Config("sword", Trio, Moves(30, "steal:6")), run.ToSave()).EnemyStolen);
        run.Faces[0] = run.Faces[1] = run.Faces[2] = run.Symbol("sword");
        run.Commit(); // kill
        Assert.AreEqual(94 + 10 + 6, run.Gold);
        Assert.AreEqual(0, run.EnemyStolen);
    }

    [Test]
    public void Seal_BlocksRerollOnlyOnItsTurn()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "seal:2", "attack:1")));
        Assert.AreEqual(0, run.RerollsLeft);
        Assert.IsFalse(run.Reroll(0));
        run.Commit();
        Assert.AreEqual(38, run.Hp);
        Assert.AreEqual(1, run.RerollsLeft);
    }

    [Test]
    public void Locked_CompanionsAreNeverOfferedAndUnlockInOrder()
    {
        var cfg = Config("sword", Trio, Enemy(999, 100));
        cfg.unlocks = new[] { "g", "h", "t_ring" };
        CollectionAssert.AreEqual(new[] { "h", "t_ring" }, Run.LockedAfter(cfg, 1));
        CollectionAssert.IsEmpty(Run.LockedAfter(cfg, 5));
        for (int seed = 0; seed < 20; seed++)
        {
            var run = new Run(cfg, new Random(seed));
            run.Locked.UnionWith(Run.LockedAfter(cfg, 0));
            var node = run.Map.Rows[0][0];
            node.Type = NodeType.Recruit;
            Assert.IsTrue(run.Choose(node));
            Assert.IsFalse(run.Offers.Any(o => o.CompanionId == "g" || o.CompanionId == "h"));
        }
        var lost = Enter(cfg);
        Assert.AreEqual(0, lost.UnlocksEarned);
        lost.Commit();
        Assert.AreEqual(Phase.Lost, lost.Phase);
        Assert.AreEqual(1, lost.UnlocksEarned);
    }

    [Test]
    public void TrimEvent_RemovesWeakFacesButKeepsSignatureAndThree()
    {
        var cfg = Config("sword", new[] { "g" }, Enemy(10, 1));
        cfg.events = new[] { new EventDef { id = "cut", title = "석공", options = new[] { Option("깎기", ("trim", 2)) } } };
        var run = Enter(cfg, NodeType.Event);
        Assert.IsTrue(run.ChooseEvent(0));
        CollectionAssert.AreEquivalent(new[] { "sword", "shield", "sword" }, run.Reels[0]);
    }

    [Test]
    public void EnemyHeal_RestoresHpWithoutAttacking()
    {
        var run = Enter(Config("sword", Trio, Moves(30, "heal:10")));
        Assert.AreEqual(0, run.EnemyIntent);
        run.Commit(); // 18 damage, heals 10
        Assert.AreEqual(22, run.EnemyHp);
        Assert.AreEqual(40, run.Hp);
    }

    [Test]
    public void Jam_OneReelCountsForNothing()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "jam:0")));
        Assert.That(run.JammedReel, Is.InRange(0, 2));
        Assert.AreEqual(9, run.Preview().damage); // two swords left: 6 x 1.5
        var restored = Run.FromSave(Config("sword", Trio, Moves(999, "jam:0")), run.ToSave());
        Assert.AreEqual(run.JammedReel, restored.JammedReel);
    }

    [Test]
    public void Sap_EmptiesTheFullestGauge()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "sap:1")));
        run.Gauge[1] = 2;
        run.Faces[0] = run.Faces[1] = run.Faces[2] = run.Symbol("shield"); // nothing charges
        run.Commit();
        Assert.AreEqual(0, run.Gauge[1]);
    }

    [Test]
    public void Reflect_BouncesDamageThroughBlock()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "reflect:50")));
        var o = run.Preview();
        Assert.AreEqual(9, run.Reflected(o));
        run.Commit();
        Assert.AreEqual(31, run.Hp);
    }

    [Test]
    public void Phase2_StartsAtHalfHpAndSwitchesMoves()
    {
        var boss = Moves(30, "guard:0");
        boss.phase2 = new[] { "attack:7" };
        boss.phase2Name = "광폭화";
        var run = Enter(Config("sword", Trio, boss));
        run.Commit(); // 18 damage -> 12 / 30
        Assert.IsTrue(run.EnemyPhase2);
        Assert.AreEqual("attack", run.Intent.Kind);
        Assert.AreEqual(7, run.EnemyIntent);
        StringAssert.Contains("광폭화", run.Log);
        Assert.IsTrue(Run.FromSave(Config("sword", Trio, boss), run.ToSave()).EnemyPhase2);
    }

    [Test]
    public void SpecialEvents_MirrorShadowTransformFuse()
    {
        var cfg = Config("sword", new[] { "g" }, Enemy(10, 1), boss: Moves(100, "guard:0"));
        cfg.events = new[] { new EventDef { id = "x", title = "x", options = new[] {
            new EventOption { label = "거울", effects = new[] { new EventEffect { kind = "mirror" } }, victoryUpgradeChoices = 3 },
            new EventOption { label = "그림자", effects = new[] { new EventEffect { kind = "shadow", value = 50 } }, victoryRelic = true },
            new EventOption { label = "변환", effects = new[] { new EventEffect { kind = "transform", target = "shield>coin" } } },
            new EventOption { label = "융합", effects = new[] { new EventEffect { kind = "fuse", value = 1 } } },
        } } };

        var mirror = Enter(cfg, NodeType.Event);
        Assert.IsTrue(mirror.ChooseEvent(0));
        Assert.AreEqual(Phase.Turn, mirror.Phase);
        Assert.AreEqual("거울 속 파티", mirror.Enemy.name);
        Assert.AreEqual(mirror.Hp, mirror.EnemyMaxHp);
        Assert.AreEqual("거울 속 파티", Run.FromSave(cfg, mirror.ToSave()).Enemy.name); // made-up enemy survives a save

        var shadow = Enter(cfg, NodeType.Event);
        Assert.IsTrue(shadow.ChooseEvent(1));
        Assert.AreEqual(50, shadow.EnemyMaxHp);
        while (shadow.Phase == Phase.Turn) { shadow.Faces[0] = shadow.Symbol("sword"); shadow.Commit(); }
        Assert.AreEqual(Phase.Reward, shadow.Phase);
        Assert.IsTrue(shadow.Offers.Any(r => r.Kind == RewardKind.Relic));

        var swap = Enter(cfg, NodeType.Event);
        Assert.IsTrue(swap.ChooseEvent(2));
        CollectionAssert.AreEquivalent(new[] { "sword", "coin", "coin", "sword" }, swap.Reels[0]);

        var fuse = Enter(cfg, NodeType.Event);
        Assert.IsTrue(fuse.ChooseEvent(3));
        Assert.AreEqual(3, fuse.Reels[0].Count);
        Assert.AreEqual(2, fuse.Level("sword")); // signature first
    }

    [Test]
    public void Banner_LiftsNeighboursAndTwinPaysWhenSideBySide()
    {
        var cfg = Config("sword", Trio, Moves(999, "guard:0"));
        cfg.symbols = cfg.symbols.Concat(new[]
        {
            new SymbolDef { id = "banner", name = "깃발", block = 1, neighborBuff = 2 },
            new SymbolDef { id = "twin", name = "쌍검", damage = 2, twinBonus = 4 },
        }).ToArray();
        var run = Enter(cfg);
        run.Faces[0] = run.Symbol("sword"); run.Faces[1] = run.Symbol("banner"); run.Faces[2] = run.Symbol("shield");
        var o = run.Preview();
        Assert.AreEqual(3 + 2, o.damage);   // sword + banner on its right
        Assert.AreEqual(3 + 1 + 2, o.block); // shield + banner's own block + banner on its left
        run.Faces[0] = run.Symbol("twin"); run.Faces[1] = run.Symbol("twin"); run.Faces[2] = run.Symbol("shield");
        Assert.AreEqual((int)(2 * 2 * 1.5f) + 4 + 4, run.Preview().damage); // a pair, then each twin sees the other
    }

    [Test]
    public void AllIn_SpendsHalfTheGoldAsDamage()
    {
        var run = Enter(Config("sword", new[] { "a" }, Moves(999, "guard:0")));
        var cfg = Config("sword", new[] { "a" }, Moves(999, "guard:0"));
        cfg.companions[0].skill = "allIn"; cfg.companions[0].skillValue = 50;
        run = Enter(cfg);
        run.Gauge[0] = 3;
        Assert.IsTrue(run.ToggleSkill(0));
        Assert.AreEqual(3 + 50, run.Preview().damage);
        run.Commit();
        Assert.AreEqual(50, run.Gold);
        Assert.AreEqual(999 - 53, run.EnemyHp);
    }

    [Test]
    public void BoneBurst_TurnsSkullsIntoDamage()
    {
        var cfg = Config("skull", new[] { "a", "b" }, Moves(999, "guard:0"));
        cfg.companions[0].skill = "boneBurst"; cfg.companions[0].skillValue = 5;
        var run = Enter(cfg);
        Assert.AreEqual(4 * 1.5f * 1, run.Preview().selfDamage, 0.01); // two skulls hurt before the skill
        run.Gauge[0] = 3;
        Assert.IsTrue(run.ToggleSkill(0));
        var o = run.Preview();
        Assert.AreEqual(0, o.selfDamage);
        Assert.AreEqual(10, o.damage);
    }

    [Test]
    public void NewRelics_LifestealCurseDamageComboAndRerollGold()
    {
        var cfg = Config("sword", Trio, Moves(999, "guard:0"));
        cfg.items = cfg.items.Concat(new[]
        {
            Item("grail", "relic", "", "lifesteal", 20), Item("crown", "relic", "", "curseDamage", 3),
            Item("medal", "relic", "", "comboBonus", 3), Item("dice", "relic", "", "rerollGold", 3),
        }).ToArray();
        var run = Enter(cfg);
        run.Relics.AddRange(new[] { "grail", "crown", "medal", "dice" });
        run.Faces[0] = run.Faces[1] = run.Symbol("sword"); run.Faces[2] = run.Symbol("skull");
        var o = run.Preview();
        Assert.AreEqual(9 + 3 + 3, o.damage);   // pair of swords, one skull x3, one combo +3
        Assert.AreEqual(15 * 20 / 100, o.heal);
        int gold = run.Gold;
        Assert.IsTrue(run.Reroll(0));
        Assert.AreEqual(gold + 3, run.Gold);
    }

    static SymbolDef Sword => new SymbolDef { id = "sword", name = "칼", damage = 3 };
    static SymbolDef Shield => new SymbolDef { id = "shield", name = "방패", block = 3 };
    static SymbolDef Coin => new SymbolDef { id = "coin", name = "코인", gold = 3 };
    static SymbolDef Skull => new SymbolDef { id = "skull", name = "해골", selfDamage = 2, curse = true };
    static SymbolDef Dagger => new SymbolDef { id = "dagger", name = "단검", damage = 2, poison = 1 };

    static CompanionDef Mate(string id, string skill, int value, params string[] reel) => new CompanionDef
    {
        id = id, name = id, signature = reel[0], skill = skill, skillName = skill, skillValue = value, reel = reel,
    };

    // Reels hold one face (member "e" also a skull), so most spins are known regardless of the seed.
    static TuningConfig Config(string face, string[] party, EnemyDef enemy, EnemyDef boss = null, int chapters = 1) => new TuningConfig
    {
        playerMaxHp = 40, rerollsPerTurn = 1, pairMultiplier = 1.5f, tripleMultiplier = 2f, restHeal = 10, chapterHeal = 15,
        skillCharge = 3, maxParty = 4, startingGold = 100, treasureGold = 30, mimicChance = 0, mapRows = 4,
        nodePool = new[] { "Combat" },
        prices = new Prices { addSymbol = 25, removeCurse = 40, heal = 15, healAmount = 12, recruit = 60, equip = 50, relic = 80, upgrade = 35, upgradeStep = 20, trim = 30, reroll = 15, rerollStep = 10 },
        startingParty = party,
        symbols = new[] { Sword, Shield, Coin, Dagger, Skull },
        companions = new[]
        {
            Mate("a", "shieldBash", 0, face), Mate("b", "volley", 6, face), Mate("c", "rage", 0, face),
            Mate("d", "freeze", 0, face), Mate("e", "prayer", 8, face, "skull"),
            Mate("g", "volley", 6, face, "shield", "shield", "sword"),
            Mate("h", "backstab", 2, face), Mate("i", "transmute", 0, face), Mate("j", "bless", 1, face), Mate("k", "thorns", 100, face),
        },
        chapters = Enumerable.Range(0, chapters)
            .Select(_ => new ChapterDef { name = "장", enemies = new[] { enemy }, elites = new[] { enemy }, boss = boss ?? enemy }).ToArray(),
        mimic = new EnemyDef { name = "미믹", hp = 5, gold = 40, moves = new[] { "attack:1" } },
        items = new[]
        {
            Item("t_sword", "equip", "sword", "damage", 2), Item("t_charm", "equip", "", "charge", 1),
            Item("t_ring", "relic", "", "noSelfDamage", 1), Item("t_heart", "relic", "", "maxHp", 15),
            Item("t_glass", "relic", "", "reroll", 1), Item("t_horn", "relic", "", "battleCharge", 1),
            Item("t_spikes", "relic", "", "spikes", 3), Item("t_regen", "relic", "", "regen", 2), Item("t_first", "relic", "", "firstStrike", 8),
        },
        events = new[] { Altar },
    };

    static ItemDef Item(string id, string slot, string symbol, string stat, int value) =>
        new ItemDef { id = id, name = id, slot = slot, symbol = symbol, stat = stat, value = value };

    static EventOption Option(string label, params (string kind, int value)[] effects) => new EventOption
    {
        label = label, effects = effects.Select(e => new EventEffect { kind = e.kind, value = e.value }).ToArray(),
    };

    static EventDef Altar => new EventDef
    {
        id = "altar", title = "제단",
        options = new[] { Option("피", ("hp", -10), ("relic", 0)), Option("돈", ("gold", -30), ("equip", 0)), Option("싸움", ("fight", 0)), Option("도박", ("gamble", 20)) },
    };

    static readonly string[] Trio = { "a", "b", "e" };
    static EnemyDef Enemy(int hp, int attack, int gold = 10) => new EnemyDef { name = "적", hp = hp, gold = gold, moves = new[] { $"attack:{attack}" } };
    static EnemyDef Moves(int hp, params string[] moves) => new EnemyDef { name = "적", hp = hp, gold = 10, moves = moves };
    static int Skulls(Run run) => run.Reels.Sum(r => r.Count(id => id == "skull"));

    /// <summary>Enter a node of the given type on row 0 and pin every reel to its first face.</summary>
    static Run Enter(TuningConfig cfg, NodeType type = NodeType.Combat)
    {
        var run = new Run(cfg, new Random(1));
        var node = run.Map.Rows[0][0];
        node.Type = type;
        Assert.IsTrue(run.Choose(node));
        for (int i = 0; i < run.Faces.Length; i++) run.Faces[i] = run.Symbol(run.Member(i).reel[0]);
        return run;
    }

    // ---------- spin math ----------

    [Test]
    public void Resolve_Triple_UsesTripleMultiplier()
    {
        var o = SpinResolver.Resolve(new[] { Sword, Sword, Sword }, Config("sword", Trio, Enemy(1, 1)));
        Assert.AreEqual(18, o.damage); // 3 dmg * 3 faces * 2.0
        StringAssert.Contains("x3", o.combo);
    }

    [Test]
    public void Resolve_PairPlusSingle_MultipliesOnlyThePair()
    {
        var o = SpinResolver.Resolve(new[] { Sword, Sword, Shield }, Config("sword", Trio, Enemy(1, 1)));
        Assert.AreEqual(9, o.damage); // 3 * 2 * 1.5
        Assert.AreEqual(3, o.block);
    }

    [Test]
    public void Resolve_SkullPair_IsBadCombo()
    {
        var o = SpinResolver.Resolve(new[] { Skull, Skull, Shield }, Config("sword", Trio, Enemy(1, 1)));
        Assert.AreEqual(6, o.selfDamage);
        Assert.IsTrue(o.badCombo);
    }

    // ---------- combat ----------

    [Test]
    public void Commit_BlockReducesEnemyAttack()
    {
        var run = Enter(Config("shield", Trio, Enemy(99, 5)));
        run.Commit();
        Assert.AreEqual(40, run.Hp); // 18 block vs 5 attack
        Assert.AreEqual(Phase.Turn, run.Phase);
    }

    [Test]
    public void Commit_CoinFaces_GiveGold()
    {
        var run = Enter(Config("coin", Trio, Enemy(99, 1)));
        run.Commit();
        Assert.AreEqual(100 + 18, run.Gold); // 3 gold * 3 faces * 2.0
    }

    [Test]
    public void Commit_HpZero_LosesRun()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 100)));
        run.Commit();
        Assert.AreEqual(Phase.Lost, run.Phase);
        Assert.AreEqual(0, run.Hp);
    }

    [Test]
    public void Reroll_StopsWhenNoneLeft()
    {
        var run = Enter(Config("sword", Trio, Enemy(99, 1)));
        Assert.IsTrue(run.Reroll(0));
        Assert.IsFalse(run.Reroll(0));
        Assert.AreEqual(0, run.RerollsLeft);
    }

    [Test]
    public void Kill_GivesGoldAndRewardThenBackToMap()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5, gold: 12)));
        run.Commit();
        Assert.AreEqual(Phase.Reward, run.Phase);
        Assert.AreEqual(112, run.Gold);
        Assert.AreEqual(RewardKind.AddSymbol, run.Offers[0].Kind);
        int reel = run.Offers[0].Reel, before = run.Reels[reel].Count;
        run.ChooseReward(0);
        Assert.AreEqual(before + 1, run.Reels[reel].Count);
        Assert.AreEqual(Phase.Map, run.Phase);
        CollectionAssert.AreEquivalent(run.Map.Rows[1].Take(2), run.Available);
    }

    [Test]
    public void Reward_OffersCurseRemoval()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)));
        run.Commit();
        Assert.AreEqual(RewardKind.RemoveSymbol, run.Offers[2].Kind);
        run.ChooseReward(2);
        CollectionAssert.DoesNotContain(run.Reels[2], "skull");
    }

    [Test]
    public void Reward_Skip_ChangesNothing()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)));
        run.Commit();
        var reels = run.Reels.Select(r => r.Count).ToArray();
        run.ChooseReward(-1);
        Assert.AreEqual(Phase.Map, run.Phase);
        CollectionAssert.AreEqual(reels, run.Reels.Select(r => r.Count).ToArray());
    }

    [Test]
    public void Boss_LastChapter_WinsRun()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Boss);
        run.Commit();
        Assert.AreEqual(Phase.Won, run.Phase);
    }

    [Test]
    public void Boss_EarlierChapter_StartsNextChapterWithNewMap()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5), chapters: 2), NodeType.Boss);
        var oldMap = run.Map;
        run.Commit();
        Assert.AreEqual(1, run.Chapter);
        Assert.AreNotSame(oldMap, run.Map);
        Assert.IsNull(run.Current);
        Assert.AreEqual(Phase.Reward, run.Phase);
    }

    // ---------- map ----------

    [Test]
    public void Generate_RowsHaveFixedStartRestAndBoss()
    {
        var map = StageMap.Generate(Config("sword", Trio, Enemy(1, 1)), new Random(3));
        Assert.AreEqual(4, map.Rows.Count);
        Assert.IsTrue(map.Rows[0].All(n => n.Type == NodeType.Combat));
        Assert.AreEqual(1, map.Rows[2].Count(n => n.Type == NodeType.Rest));
        Assert.IsTrue(map.Rows[2].All(n => n.Next.Single() == map.Boss));
        Assert.AreEqual(1, map.Rows[1].Count(n => n.Type == NodeType.Recruit));
        Assert.AreEqual(NodeType.Boss, map.Boss.Type);
        Assert.AreEqual(2, map.Rows[0][0].Next.Count); // edge lane links to itself and its neighbour
        Assert.AreEqual(3, map.Rows[0][1].Next.Count);
    }

    [Test]
    public void Choose_UnreachableNode_ReturnsFalse()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)));
        run.Commit();
        run.ChooseReward(-1);
        Assert.IsFalse(run.Choose(run.Map.Rows[1][2]));
        Assert.IsTrue(run.Choose(run.Map.Rows[1][1]));
    }

    // ---------- shop, rest, treasure, recruit ----------

    [Test]
    public void Shop_BuyTakesGoldAndSellsOnce()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Shop);
        Assert.AreEqual(Phase.Shop, run.Phase);
        int k = run.ShopItems.FindIndex(r => r.Kind == RewardKind.AddSymbol);
        int reel = run.ShopItems[k].Reel, before = run.Reels[reel].Count;
        Assert.IsTrue(run.Buy(k));
        Assert.AreEqual(75, run.Gold);
        Assert.AreEqual(before + 1, run.Reels[reel].Count);
        Assert.IsFalse(run.Buy(k));
        run.LeaveShop();
        Assert.AreEqual(Phase.Map, run.Phase);
    }

    [Test]
    public void Shop_NotEnoughGold_RefusesBuy()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5));
        cfg.startingGold = 10;
        var run = Enter(cfg, NodeType.Shop);
        Assert.IsFalse(run.Buy(0));
        Assert.AreEqual(10, run.Gold);
    }

    [Test]
    public void Rest_OffersDoubleHealAndCurseRemoval()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Rest);
        Assert.AreEqual(RewardKind.Heal, run.Offers[0].Kind);
        Assert.AreEqual(20, run.Offers[0].Amount);
        Assert.AreEqual(RewardKind.RemoveSymbol, run.Offers[1].Kind);
        run.ChooseReward(0);
        Assert.AreEqual(Phase.Map, run.Phase);
    }

    [Test]
    public void Treasure_AlwaysMimic_StartsMimicFight()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5));
        cfg.mimicChance = 1;
        var run = Enter(cfg, NodeType.Treasure);
        Assert.AreEqual(Phase.Turn, run.Phase);
        Assert.AreEqual("미믹", run.Enemy.name);
    }

    [Test]
    public void Treasure_NoMimic_GivesGoldAndSymbolChoice()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Treasure);
        Assert.AreEqual(130, run.Gold);
        Assert.AreEqual(Phase.Reward, run.Phase);
        Assert.AreEqual(RewardKind.Equip, run.Offers[0].Kind);
        Assert.IsTrue(run.Offers.Skip(1).All(o => o.Kind == RewardKind.AddSymbol));
    }

    [Test]
    public void Recruit_AddsMemberReelAndFace()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Recruit);
        Assert.AreEqual(3, run.Offers.Count); // c, d and g are free
        run.ChooseReward(0);
        Assert.AreEqual(4, run.Party.Count);
        Assert.AreEqual(4, run.Reels.Count);
        Assert.AreEqual(4, run.Faces.Length);
        Assert.AreEqual(0, run.Gauge[3]);
    }

    [Test]
    public void Recruit_PartyFull_OffersGold()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "c", "e" }, Enemy(10, 5)), NodeType.Recruit);
        Assert.AreEqual(RewardKind.Gold, run.Offers.Single().Kind);
    }

    [Test]
    public void Recruit_ChapterBonusSurvivesSaveAndOnlyAddsToNewReel()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5), chapters: 2);
        cfg.chapters[0].recruitSignatureBonus = true;
        var run = Run.FromSave(cfg, Enter(cfg, NodeType.Recruit).ToSave());
        var offer = run.Offers[0];
        var recruit = run.Companion(offer.CompanionId);
        var existing = run.Reels.Select(reel => reel.ToArray()).ToArray();
        Assert.AreEqual(recruit.signature, offer.SymbolId);
        run.ChooseReward(0);
        CollectionAssert.AreEqual(recruit.reel.Append(recruit.signature), run.Reels.Last());
        for (int i = 0; i < existing.Length; i++) CollectionAssert.AreEqual(existing[i], run.Reels[i]);

        var later = new Run(cfg, new Random(1));
        later.JumpToChapter(1);
        var node = later.Map.Rows[0][0];
        node.Type = NodeType.Recruit;
        Assert.IsTrue(later.Choose(node));
        Assert.IsTrue(later.Offers.All(r => r.SymbolId == null));
    }

    // ---------- skills ----------

    [Test]
    public void Commit_SignatureFace_ChargesGauge()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 1)));
        run.Commit();
        Assert.AreEqual(1, run.Gauge[0]);
        Assert.AreEqual(1, run.Gauge[1]);
    }

    [Test]
    public void ToggleSkill_NotCharged_ReturnsFalse()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 1)));
        Assert.IsFalse(run.ToggleSkill(0));
        Assert.IsEmpty(run.Armed);
    }

    [Test]
    public void Rage_DoublesDamageAndEmptiesGauge()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "c" }, Enemy(999, 1)));
        run.Gauge[2] = 3;
        Assert.IsTrue(run.ToggleSkill(2));
        Assert.AreEqual(36, run.Preview().damage); // 18 * 2
        run.Commit();
        Assert.AreEqual(999 - 36, run.EnemyHp);
        Assert.AreEqual(0, run.Gauge[2]);
    }

    [Test]
    public void Volley_AddsFlatDamageBeforeRage()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "c" }, Enemy(999, 1)));
        run.Gauge[1] = run.Gauge[2] = 3;
        run.ToggleSkill(1);
        run.ToggleSkill(2);
        Assert.AreEqual((18 + 6) * 2, run.Preview().damage);
    }

    [Test]
    public void ShieldBash_AddsBlockAsDamage()
    {
        var run = Enter(Config("shield", new[] { "a", "b", "c" }, Enemy(999, 1)));
        run.Gauge[0] = 3;
        run.ToggleSkill(0);
        Assert.AreEqual(18, run.Preview().damage);
    }

    [Test]
    public void Freeze_StopsEnemyAttack()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "d" }, Enemy(999, 30)));
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        run.Commit();
        Assert.AreEqual(40, run.Hp);
    }

    [Test]
    public void Prayer_HealsAndPurgesCurse()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 1)));
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        Assert.AreEqual(8, run.Preview().heal);
        run.Commit();
        CollectionAssert.DoesNotContain(run.Reels[2], "skull");
    }

    // ---------- equipment, relics, events ----------

    [Test]
    public void Equip_MatchingFace_AddsStat()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "c" }, Enemy(999, 1)));
        run.Equipment[0] = "t_sword";
        Assert.AreEqual(18 + 2, run.Preview().damage);
    }

    [Test]
    public void Equip_Charge_FillsGaugeFaster()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "c" }, Enemy(999, 1)));
        run.Equipment[0] = "t_charm";
        run.Commit();
        Assert.AreEqual(2, run.Gauge[0]);
        Assert.AreEqual(1, run.Gauge[1]);
    }

    [Test]
    public void Relic_SkullRing_RemovesSelfDamage()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 1)));
        for (int i = 0; i < run.Faces.Length; i++) run.Faces[i] = run.Symbol("skull");
        Assert.AreEqual(12, run.Preview().selfDamage);
        run.Relics.Add("t_ring");
        Assert.AreEqual(0, run.Preview().selfDamage);
    }

    [Test]
    public void Relic_MaxHpAndReroll_RaiseLimits()
    {
        var run = new Run(Config("sword", Trio, Enemy(999, 1)), new Random(1));
        run.Relics.Add("t_heart");
        run.Relics.Add("t_glass");
        run.Choose(run.Map.Rows[0][0]);
        Assert.AreEqual(55, run.MaxHp);
        Assert.AreEqual(2, run.RerollsLeft);
    }

    [Test]
    public void Relic_WarHorn_ChargesGaugesAtFightStart()
    {
        var run = new Run(Config("sword", Trio, Enemy(999, 1)), new Random(1));
        run.Relics.Add("t_horn");
        run.Choose(run.Map.Rows[0][0]);
        Assert.IsTrue(run.Gauge.All(g => g == 1));
    }

    [Test]
    public void Elite_Kill_OffersEquipment()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Elite);
        run.Commit();
        Assert.AreEqual(RewardKind.Equip, run.Offers[1].Kind);
        run.ChooseReward(1);
        Assert.AreEqual(1, run.Equipment.Count(e => e != null));
    }

    [Test]
    public void Boss_Kill_OffersRelic()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5), chapters: 2), NodeType.Boss);
        run.Commit();
        Assert.AreEqual(RewardKind.Relic, run.Offers[0].Kind);
        run.ChooseReward(0);
        Assert.AreEqual(1, run.Relics.Count);
    }

    [Test]
    public void Event_Altar_TradesHpForRelic()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Event);
        Assert.AreEqual(Phase.Event, run.Phase);
        Assert.IsTrue(run.ChooseEvent(0));
        Assert.AreEqual(1, run.Relics.Count);
        var relic = run.Item(run.Relics[0]);
        Assert.AreEqual(30 + (relic.stat == "maxHp" ? relic.value : 0), run.Hp); // the random relic may be the max-HP one
        Assert.AreEqual(Phase.Map, run.Phase);
    }

    [Test]
    public void Event_TooPoor_RefusesGoldOption()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5));
        cfg.startingGold = 10;
        var run = Enter(cfg, NodeType.Event);
        Assert.IsFalse(run.CanChoose(run.CurrentEvent.options[1]));
        Assert.IsFalse(run.ChooseEvent(1));
        Assert.AreEqual(Phase.Event, run.Phase);
    }

    [Test]
    public void Event_Fight_StartsEliteFight()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Event);
        run.ChooseEvent(2);
        Assert.AreEqual(Phase.Turn, run.Phase);
    }

    [Test]
    public void Event_Gamble_WinsTripleOrLosesStake()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Event);
        run.ChooseEvent(3);
        CollectionAssert.Contains(new[] { 80, 140 }, run.Gold);
    }

    [Test]
    public void Stats_CountTurnsKillsBestHitAndGold()
    {
        var run = Enter(Config("sword", Trio, Enemy(30, 1, gold: 12)));
        run.Commit(); // 18
        run.Faces[0] = run.Faces[1] = run.Faces[2] = run.Symbol("sword");
        run.Commit(); // kill
        Assert.AreEqual(2, run.Turns);
        Assert.AreEqual(1, run.Kills);
        Assert.AreEqual(18, run.BestHit);
        Assert.AreEqual(12, run.GoldEarned);
    }

    // ---------- enemy moves ----------

    [Test]
    public void Guard_ReducesDamageDealt()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "guard:5")));
        Assert.AreEqual(0, run.EnemyIntent);
        run.Commit();
        Assert.AreEqual(999 - 13, run.EnemyHp);
        Assert.AreEqual(40, run.Hp);
    }

    [Test]
    public void Curse_AddsSkullForThisFightOnly()
    {
        var run = Enter(Config("sword", Trio, Moves(30, "curse:0")));
        int before = Skulls(run);
        run.Commit(); // 18 damage, enemy curses
        Assert.AreEqual(before + 1, Skulls(run));
        run.Faces[0] = run.Faces[1] = run.Faces[2] = run.Symbol("sword");
        run.Commit(); // kill
        Assert.AreEqual(before, Skulls(run));
    }

    [Test]
    public void Enrage_RaisesLaterAttacks()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "enrage:3", "attack:5")));
        run.Commit();
        Assert.AreEqual(40, run.Hp);
        Assert.AreEqual(8, run.EnemyIntent);
        run.Faces[0] = run.Faces[1] = run.Faces[2] = run.Symbol("sword");
        run.Commit();
        Assert.AreEqual(32, run.Hp);
    }

    [Test]
    public void Drain_HealsEnemyByDamageThatGotThrough()
    {
        var run = Enter(Config("sword", Trio, Moves(999, "drain:6")));
        run.Commit();
        Assert.AreEqual(999 - 18 + 6, run.EnemyHp);
        Assert.AreEqual(34, run.Hp);
    }

    [Test]
    public void Freeze_StopsCurse()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "d" }, Moves(999, "curse:4")));
        int before = Skulls(run);
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        run.Commit();
        Assert.AreEqual(before, Skulls(run));
        Assert.AreEqual(40, run.Hp);
    }

    // ---------- gold sinks ----------

    [Test]
    public void Upgrade_AddsMainStatOnEveryFace()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 1)));
        run.SymbolLevel["sword"] = 2;
        Assert.AreEqual(18 + 3 * 2, run.Preview().damage);
    }

    [Test]
    public void Shop_UpgradePriceRisesWithLevel()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Shop);
        int k = run.ShopItems.FindIndex(r => r.Kind == RewardKind.Upgrade && r.SymbolId == "sword");
        Assert.AreEqual(35, run.ShopItems[k].Price);
        Assert.IsTrue(run.Buy(k));
        Assert.AreEqual(1, run.Level("sword"));
        run.LeaveShop();
        var next = run.Map.Rows[1][0];
        next.Type = NodeType.Shop;
        run.Choose(next);
        Assert.AreEqual(55, run.ShopItems.First(r => r.Kind == RewardKind.Upgrade && r.SymbolId == "sword").Price);
    }

    [Test]
    public void Shop_RerollCostsMoreEachTime()
    {
        var run = Enter(Config("sword", Trio, Enemy(10, 5)), NodeType.Shop);
        Assert.IsTrue(run.RerollShop());
        Assert.AreEqual(85, run.Gold);
        Assert.IsTrue(run.RerollShop());
        Assert.AreEqual(60, run.Gold);
        Assert.AreEqual(35, run.ShopRerollCost);
        Assert.AreEqual(Phase.Shop, run.Phase);
    }

    [Test]
    public void Shop_TrimRemovesWeakestSymbol()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "g" }, Enemy(10, 5)), NodeType.Shop);
        int k = run.ShopItems.FindIndex(r => r.Kind == RewardKind.RemoveSymbol && r.Reel == 2);
        Assert.AreEqual(30, run.ShopItems[k].Price);
        run.Buy(k);
        Assert.AreEqual(3, run.Reels[2].Count);
    }

    // ---------- save / continue ----------

    /// <summary>Through JSON, the way the game stores it on disk.</summary>
    static Run Reload(TuningConfig cfg, Run run) =>
        Run.FromSave(cfg, UnityEngine.JsonUtility.FromJson<RunSave>(UnityEngine.JsonUtility.ToJson(run.ToSave())));

    [Test]
    public void Save_OnMap_RestoresPartyMapAndPosition()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5, gold: 7));
        var run = Enter(cfg);
        run.Commit();
        run.ChooseReward(0);
        var back = Reload(cfg, run);
        Assert.AreEqual(Phase.Map, back.Phase);
        CollectionAssert.AreEqual(run.Party, back.Party);
        CollectionAssert.AreEqual(run.Reels.Select(r => r.Count), back.Reels.Select(r => r.Count));
        Assert.AreEqual(run.Gold, back.Gold);
        Assert.AreEqual(run.Hp, back.Hp);
        Assert.AreEqual(run.Kills, back.Kills);
        Assert.AreEqual(0, back.Current.Row);
        Assert.IsTrue(back.Map.Rows[0][0].Visited);
        CollectionAssert.AreEqual(run.Available.Select(n => (n.Row, n.Type)), back.Available.Select(n => (n.Row, n.Type)));
        Assert.IsTrue(back.Choose(back.Available.First()));
    }

    [Test]
    public void Save_MidFight_KeepsEnemyFacesAndFightCurses()
    {
        var cfg = Config("sword", Trio, Moves(30, "curse:0", "attack:5"));
        var run = Enter(cfg);
        int before = Skulls(run);
        run.Commit(); // 18 damage, a skull for this fight
        var back = Reload(cfg, run);
        Assert.AreEqual(Phase.Turn, back.Phase);
        Assert.AreEqual(run.EnemyHp, back.EnemyHp);
        Assert.AreEqual("attack", back.Intent.Kind);
        CollectionAssert.AreEqual(run.Faces.Select(f => f.id), back.Faces.Select(f => f.id));
        back.Faces[0] = back.Faces[1] = back.Faces[2] = back.Symbol("sword");
        back.Commit(); // kill: the fight's skull goes away
        Assert.AreEqual(before, Skulls(back));
    }

    [Test]
    public void Save_InShop_KeepsItemsAndBuysStillWork()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5));
        var run = Enter(cfg, NodeType.Shop);
        var back = Reload(cfg, run);
        Assert.AreEqual(Phase.Shop, back.Phase);
        Assert.AreEqual(run.ShopItems.Count, back.ShopItems.Count);
        int k = back.ShopItems.FindIndex(r => r.Kind == RewardKind.Upgrade);
        Assert.IsNull(back.ShopItems[k].CompanionId);
        Assert.IsTrue(back.Buy(k));
        Assert.AreEqual(1, back.Level("sword"));
    }

    [Test]
    public void Save_KeepsEquipmentRelicsAndUpgrades()
    {
        var cfg = Config("sword", new[] { "a", "b", "c" }, Enemy(999, 1));
        var run = Enter(cfg);
        run.Equipment[0] = "t_sword";
        run.Relics.Add("t_heart");
        run.SymbolLevel["sword"] = 2;
        var back = Reload(cfg, run);
        Assert.AreEqual(run.Preview().damage, back.Preview().damage);
        Assert.AreEqual(55, back.MaxHp);
        Assert.IsNull(back.Equipment[1]);
    }

    // ---------- content: poison, new skills, new relics, difficulty, new events ----------

    [Test]
    public void Poison_StacksThenTicksAndShrinks()
    {
        var run = Enter(Config("dagger", Trio, Enemy(999, 1)));
        run.Faces[2] = run.Symbol("dagger");
        run.Commit(); // 12 damage, 6 poison lands and bites
        Assert.AreEqual(999 - 12 - 6, run.EnemyHp);
        Assert.AreEqual(5, run.EnemyPoison);
    }

    [Test]
    public void Backstab_AddsDamagePerPoison()
    {
        var run = Enter(Config("dagger", new[] { "a", "b", "h" }, Enemy(999, 1)));
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        Assert.AreEqual(12 + 6 * 2, run.Preview().damage);
    }

    [Test]
    public void Transmute_TurnsSkullsIntoCoins()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "i" }, Enemy(999, 1)));
        for (int k = 0; k < 3; k++) run.Faces[k] = run.Symbol("skull");
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        var o = run.Preview();
        Assert.AreEqual(18, o.gold);
        Assert.AreEqual(0, o.selfDamage);
    }

    [Test]
    public void Bless_ChargesTheOthers()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "j" }, Enemy(999, 1)));
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        run.Commit();
        Assert.AreEqual(2, run.Gauge[0]); // +1 signature face, +1 bless
        Assert.AreEqual(0, run.Gauge[2]);
    }

    [Test]
    public void Thorns_ReflectsTheAttack()
    {
        var run = Enter(Config("sword", new[] { "a", "b", "k" }, Moves(999, "attack:10")));
        run.Gauge[2] = 3;
        run.ToggleSkill(2);
        run.Commit();
        Assert.AreEqual(999 - 18 - 10, run.EnemyHp);
    }

    [Test]
    public void Relics_SpikesRegenFirstStrike()
    {
        var run = Enter(Config("sword", Trio, Enemy(999, 5)));
        run.Faces[2] = run.Symbol("sword");
        run.Relics.Add("t_first");
        Assert.AreEqual(18 + 8, run.Preview().damage);
        run.Relics.Add("t_spikes");
        run.Relics.Add("t_regen");
        run.Commit(); // 26 dealt, 5 taken, 3 spikes back, then 2 regen at the next turn
        Assert.AreEqual(999 - 26 - 3, run.EnemyHp);
        Assert.AreEqual(40 - 5 + 2, run.Hp);
        run.Faces[0] = run.Faces[1] = run.Faces[2] = run.Symbol("sword");
        Assert.AreEqual(18, run.Preview().damage); // first strike only on turn one
    }

    [Test]
    public void Ascension_RaisesEnemyHpAttackAndPrices()
    {
        var cfg = Config("sword", Trio, Enemy(100, 5));
        var run = new Run(cfg, new Random(1), 5);
        Assert.AreEqual(0, run.Gold);
        run.Choose(run.Map.Rows[0][0]);
        Assert.AreEqual(110, run.EnemyMaxHp);
        Assert.AreEqual(6, run.EnemyIntent);
        var shop = new Run(cfg, new Random(1), 3);
        shop.Map.Rows[0][0].Type = NodeType.Shop;
        shop.Choose(shop.Map.Rows[0][0]);
        Assert.AreEqual(42, shop.ShopItems.First(r => r.Kind == RewardKind.Upgrade).Price);
    }

    static TuningConfig WithEvent(TuningConfig cfg, params EventOption[] options)
    {
        cfg.events = new[] { new EventDef { id = "t", title = "t", options = options } };
        return cfg;
    }

    [Test]
    public void LedgerFight_AfterResumeOffersOwnedSignaturesAndAppliesChosenUpgrade()
    {
        var cfg = WithEvent(Config("sword", Trio, Enemy(1, 1)), Option("fight", ("fight", 0)), Option("leave"));
        cfg.events[0].options[0].victoryUpgradeChoices = 3;
        cfg.companions.First(c => c.id == "e").signature = "dagger";
        cfg.companions.First(c => c.id == "e").reel = new[] { "sword", "dagger", "skull" };

        var run = Enter(cfg, NodeType.Event);
        Assert.IsTrue(run.ChooseEvent(0));
        Assert.AreEqual(Phase.Turn, run.Phase);
        run = Run.FromSave(cfg, run.ToSave());
        for (int i = 0; i < run.Faces.Length; i++) run.Faces[i] = run.Symbol("sword");
        run.Commit();
        Assert.AreEqual(Phase.Reward, run.Phase);
        Assert.IsTrue(run.Offers.All(r => r.Kind == RewardKind.Upgrade));
        CollectionAssert.AreEqual(new[] { "dagger", "sword" }, run.Offers.Select(r => r.SymbolId));
        run = Run.FromSave(cfg, run.ToSave());
        run.ChooseReward(0);
        Assert.AreEqual(1, run.Level("dagger"));
        Assert.AreEqual(0, run.Level("sword"));

        var skipped = Enter(cfg, NodeType.Event);
        Assert.IsTrue(skipped.ChooseEvent(1));
        Assert.AreEqual(Phase.Map, skipped.Phase);
        Assert.AreEqual(0, skipped.Level("dagger"));
    }

    [Test]
    public void ChapterEvent_DoesNotAppearInOtherChapters()
    {
        var cfg = Config("sword", Trio, Enemy(10, 5), chapters: 2);
        cfg.events = new[] { new EventDef { id = "ledger", chapter = 1 }, Altar };
        var run = new Run(cfg, new Random(1));
        run.JumpToChapter(1);
        var node = run.Map.Rows[0][0];
        node.Type = NodeType.Event;
        Assert.IsTrue(run.Choose(node));
        Assert.AreEqual("altar", run.CurrentEvent.id);
    }

    [Test]
    public void Events_SlotUpgradeChargeDupe()
    {
        var cfg = WithEvent(Config("sword", Trio, Enemy(10, 5)),
            Option("slot", ("slot", 15)), Option("up", ("upgrade", 0)), Option("charge", ("charge", 0)), Option("dupe", ("dupe", 2)));
        var run = Enter(cfg, NodeType.Event);
        run.ChooseEvent(0);
        Assert.AreEqual(85, run.Gold);

        run = Enter(cfg, NodeType.Event);
        run.ChooseEvent(1);
        Assert.AreEqual(1, run.SymbolLevel.Values.Sum());

        run = Enter(cfg, NodeType.Event);
        run.ChooseEvent(2);
        Assert.IsTrue(run.Gauge.All(g => g == 3));

        run = Enter(cfg, NodeType.Event);
        int faces = run.Reels.Sum(r => r.Count);
        run.ChooseEvent(3);
        Assert.AreEqual(faces + 2, run.Reels.Sum(r => r.Count));
    }

    [Test]
    public void Save_KeepsPoisonAndDifficulty()
    {
        var cfg = Config("dagger", Trio, Enemy(999, 1));
        var run = new Run(cfg, new Random(1), 2);
        run.Choose(run.Map.Rows[0][0]);
        for (int k = 0; k < 3; k++) run.Faces[k] = run.Symbol("dagger");
        run.Commit();
        var back = Reload(cfg, run);
        Assert.AreEqual(run.EnemyPoison, back.EnemyPoison);
        Assert.AreEqual(2, back.Ascension);
        Assert.AreEqual(run.EnemyMaxHp, back.EnemyMaxHp);
    }
}
