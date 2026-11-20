using System.Collections.Generic;
using LastTrain;
using NUnit.Framework;
using UnityEngine;

/// <summary>Horde awareness and movement, aimed attacks and stealth kills, looting, hunger, co-op.</summary>
public class CoreTests
{
    static Tuning T => Tuning.Data;

    class FakeWorld : IWorld
    {
        public Horde Horde { get; set; }
        public int Nails, Fireworks, Swings, Impacts;
        public void ReportImpact(Vector2 position, Vector2 direction, float amount, bool fatal, bool silent) => Impacts++;
        public float LoudestNoise;
        public bool ClearStealth = true;
        public bool CanReachForStealth(Vector2 from, Vector2 to) => ClearStealth;
        public void SwingFx(Vector2 f, Vector2 d, float r, float h, string w, bool s) => Swings++;
        public void SpawnNail(Vector2 f, Vector2 d, float dmg, int p) => Nails++;
        public void SpawnFirework(Vector2 f, Vector2 t, float dmg, float r, float fl) => Fireworks++;
        public void Noise(Vector2 at, float r) => LoudestNoise = Mathf.Max(LoudestNoise, r);
    }

    static Horde NewHorde(params Vector2[] at)
    {
        var h = new Horde(T.zombies, new List<Rect>());
        foreach (var p in at) h.Spawn(p, 0);
        h.Step(0.0001f, new List<Vector2>());
        return h;
    }

    [Test]
    public void SprintExpendsOnlyWhileMovingAndRestartsAfterRecovery()
    {
        var player = new Survivor { Sprinting = true };
        player.TickSprint(false, 1f);
        Assert.AreEqual(1f, player.SprintEnergy);
        player.TickSprint(true, T.player.sprintSeconds);
        Assert.IsTrue(player.SprintExhausted);
        Assert.AreEqual(0f, player.SprintEnergy);
        player.Sprinting = false;
        player.TickSprint(false, T.player.sprintRecoveryDelay * 0.5f);
        Assert.AreEqual(0f, player.SprintEnergy);
        player.TickSprint(false, T.player.sprintRecoverSeconds);
        Assert.IsFalse(player.SprintExhausted);
        Assert.Greater(T.zombies[1].chaseSpeed, player.Speed);
        Assert.Less(T.zombies[1].chaseSpeed, player.Speed * T.player.sprintMult);
    }

    [Test]
    public void AlertZombieMovesAroundWallAndCanForgetItsGoal()
    {
        var horde = new Horde(T.zombies, new List<Rect> { new(-1f, -2f, 2f, 4f) });
        Vector2 goal = new(3f, 0.45f);
        horde.Spawn(new Vector2(-3f, 0f), 0);
        horde.Alert(0, goal, 5f);
        for (int frame = 0; frame < 180; frame++)
            horde.Step(1f / 60f, System.Array.Empty<Vector2>());
        Assert.Less(Vector2.Distance(horde.Pos[0], goal), 0.4f);
        for (int frame = 0; frame < 150; frame++)
            horde.Step(1f / 60f, System.Array.Empty<Vector2>());
        Assert.IsFalse(horde.IsAware(0));
    }

    [Test]
    public void DiscardedSuppliesMergeWithoutReplacingOtherLoot()
    {
        var contents = new List<(string item, int count)>();
        Game.AddLoot(contents, "food", 2);
        Game.AddLoot(contents, "fuse", 1);
        Game.AddLoot(contents, "food", 3);
        Assert.AreEqual(2, contents.Count);
        Assert.AreEqual(("food", 5), contents[0]);
        Assert.AreEqual(("fuse", 1), contents[1]);
    }

    [Test]
    public void BagPicksUpOnlyWhatFitsAndReusesFreedCapacity()
    {
        var player = new Survivor();
        int nailWeight = T.bag.Weight("nails");
        int expected = (player.BagCapacity - player.BagLoad) / nailWeight;
        player.Take("nails", 500, out int carried);
        Assert.AreEqual(expected, carried);
        Assert.LessOrEqual(player.BagLoad, player.BagCapacity);
        player.Take("nails", 1, out carried);
        Assert.AreEqual(0, carried);

        player.Hunger = 0;
        Assert.IsTrue(player.Eat());
        player.Take("nails", 1, out carried);
        Assert.AreEqual(1, carried);
    }

    [Test]
    public void TradeThatWouldOverfillBagDoesNotConsumePayment()
    {
        var player = new Survivor();
        player.Take("nails", 500);
        var trade = new Tuning.TradeCfg { give = "food", giveN = 1, get = "scrap", getN = 1 };
        int load = player.BagLoad;
        Assert.IsFalse(player.Trade(trade, out var message));
        Assert.AreEqual("가방 공간 부족", message);
        Assert.AreEqual(1, player.Bag["food"]);
        Assert.AreEqual(0, player.Bag["scrap"]);
        Assert.AreEqual(load, player.BagLoad);
    }

    [Test]
    public void OverheadViewKeepsAlleyVisibleBesideTallBuilding()
    {
        var building = new Bounds(new Vector3(1.5f, 3f, -1.4f), new Vector3(2f, 6f, 2f));
        var target = new Vector3(1.5f, 0.12f, 0);
        Assert.IsTrue(City.BlocksSight(building, target + new Vector3(0, 8f, -8f), target));
        Assert.IsFalse(City.BlocksSight(building, target + Vector3.up * 27f, target));
    }

    [Test]
    public void AlertedZombie_ChasesGoal_UnawareOneBarelyMoves()
    {
        var h = NewHorde(new Vector2(0, 0), new Vector2(50, 0));
        h.Alert(0, new Vector2(10, 0), 5);
        for (int s = 0; s < 10; s++) h.Step(0.1f, new List<Vector2>());
        Assert.Greater(h.Pos[0].x, 2f, "aware zombie runs at the goal");
        Assert.Less(Vector2.Distance(h.Pos[1], new Vector2(50, 0)), 1.5f, "wanderer only shuffles");
        Assert.IsFalse(h.IsAware(1));
    }

    [Test]
    public void Awareness_WearsOff()
    {
        var h = NewHorde(Vector2.zero);
        h.Alert(0, Vector2.one, 0.5f);
        for (int s = 0; s < 10; s++) h.Step(0.1f, new List<Vector2>());
        Assert.IsFalse(h.IsAware(0));
    }

    [Test]
    public void Separation_SpreadsPile_AndWallsPushOut()
    {
        var walls = new List<Rect> { new(2, -5, 4, 10) };
        var h = new Horde(T.zombies, walls);
        for (int i = 0; i < 20; i++) h.Spawn(Vector2.zero, 0);
        h.Spawn(new Vector2(4, 0), 0);
        for (int s = 0; s < 10; s++) h.Step(0.02f, new List<Vector2>());
        float minD = float.MaxValue;
        for (int i = 0; i < 20; i++)
        for (int j = i + 1; j < 20; j++) minD = Mathf.Min(minD, Vector2.Distance(h.Pos[i], h.Pos[j]));
        Assert.Greater(minD, 0.3f);
        Assert.IsFalse(walls[0].Contains(h.Pos[20]));
    }

    [Test]
    public void OnlyAwareZombies_HurtOnContact()
    {
        var h = NewHorde(new Vector2(0.5f, 0));
        Assert.AreEqual(0, h.ContactDps(Vector2.zero, 0.45f));
        h.Alert(0, Vector2.zero, 5);
        Assert.AreEqual(T.zombies[0].dps, h.ContactDps(Vector2.zero, 0.45f));
    }

    [Test]
    public void Melee_HitsOnlyWhereYouAim()
    {
        var h = NewHorde(new Vector2(0, 1.2f), new Vector2(0, -1.2f));
        h.Alert(0, Vector2.zero, 5);
        h.Alert(1, Vector2.zero, 5);
        var s = new Survivor();
        s.Take("pipe", 1);
        var w = new FakeWorld { Horde = h };
        Assert.IsTrue(s.Attack(new Vector2(0, 5), w));
        Assert.Less(h.Hp[0], T.zombies[0].hp);
        Assert.AreEqual(1, w.Impacts);
        Assert.AreEqual(T.zombies[0].hp, h.Hp[1], "behind you is not hit");
        Assert.IsFalse(s.Attack(new Vector2(0, 5), w), "cooldown");
    }

    [TestCase("pipe")]
    [TestCase("axe")]
    [TestCase("bat")]
    public void MeleeHitsOnlyClosestEnemyInCrowd(string weapon)
    {
        var horde = NewHorde(new Vector2(0, 1.05f), new Vector2(0.65f, 1.75f), new Vector2(-0.65f, 1.7f));
        var world = new FakeWorld { Horde = horde };
        var player = new Survivor();
        player.Take(weapon, 1);

        Assert.IsTrue(player.Attack(Vector2.up, world));
        Assert.AreEqual(1, world.Impacts);
        Assert.Less(horde.Hp[0], T.zombies[0].hp);
        for (int i = 1; i < horde.Count; i++)
        {
            Assert.AreEqual(T.zombies[0].hp, horde.Hp[i]);
            Assert.AreEqual(0f, horde.Slow[i]);
            Assert.AreEqual(Vector2.zero, horde.Knock[i]);
        }
    }

    [Test]
    public void KnifeDealsDamageWithoutStagger()
    {
        var horde = NewHorde(new Vector2(0, 1.05f));
        var player = new Survivor();
        player.Take("knife", 1);
        Assert.IsTrue(player.Attack(Vector2.up, new FakeWorld { Horde = horde }));
        Assert.Less(horde.Hp[0], T.zombies[0].hp);
        Assert.AreEqual(0f, horde.Slow[0]);
    }

    [Test]
    public void CrouchedRearAttack_KillsSilently()
    {
        var h = NewHorde(new Vector2(0, 1.2f));
        h.Heading[0] = Vector2.up;
        var s = new Survivor { Crouching = true };
        s.Take("knife", 1);
        var w = new FakeWorld { Horde = h };
        Assert.AreEqual(0, s.StealthTarget(w));
        s.Attack(new Vector2(0, 3), w);
        Assert.LessOrEqual(h.Hp[0], 0);
        Assert.IsTrue(s.LastWasStealthKill);
        Assert.LessOrEqual(w.LoudestNoise, 1f, "a stealth kill barely makes a sound");
    }

    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public void StealthRequiresCrouchRearAndUnblockedPath(bool crouch, bool behind, bool clear)
    {
        var h = NewHorde(new Vector2(0, 1.2f));
        h.Heading[0] = behind ? Vector2.up : Vector2.down;
        var player = new Survivor { Crouching = crouch };
        player.Take("knife", 1);
        var world = new FakeWorld { Horde = h, ClearStealth = clear };
        Assert.AreEqual(-1, player.StealthTarget(world));
        player.Attack(Vector2.up, world);
        Assert.IsFalse(player.LastWasStealthKill);
        Assert.Greater(h.Hp[0], 0);
    }

    [Test]
    public void AlreadyAlertedEnemyHasNoStealthPrompt()
    {
        var h = NewHorde(new Vector2(0, 1.2f));
        h.Heading[0] = Vector2.up;
        h.Alert(0, Vector2.zero, 3f);
        var player = new Survivor { Crouching = true };
        player.Take("knife", 1);
        Assert.AreEqual(-1, player.StealthTarget(new FakeWorld { Horde = h }));
    }

    [Test]
    public void Nailgun_NeedsNails_AndIsLoud()
    {
        var h = NewHorde();
        var s = new Survivor();
        s.Take("nailgun", 1);
        var w = new FakeWorld { Horde = h };
        Assert.IsFalse(s.Attack(new Vector2(0, 5), w), "no nails, no shot");
        s.Take("nails", 2);
        Assert.IsTrue(s.Attack(new Vector2(0, 5), w));
        Assert.AreEqual(1, w.Nails);
        Assert.AreEqual(1, s.Bag["nails"]);
        Assert.AreEqual(T.Weapon("nailgun").noise, w.LoudestNoise);
    }

    [Test]
    public void MeleeWeapon_BreaksAfterItsDurability()
    {
        var h = NewHorde();
        var s = new Survivor();
        s.Take("axe", 1);
        var w = new FakeWorld { Horde = h };
        int uses = T.Weapon("axe").durability;
        for (int i = 0; i < uses; i++)
        {
            Assert.AreEqual("axe", s.Held.id);
            s.Attack(Vector2.up, w);
            s.Tick(10f);  // let the cooldown pass
        }
        Assert.AreEqual("fists", s.Held.id);
        Assert.AreEqual("소방도끼", s.Broke);
    }

    [Test]
    public void Take_FillsSlots_ThenReplacesHeld_FistsStay()
    {
        var s = new Survivor();
        s.Take("pipe", 1); s.Take("bat", 1); s.Take("knife", 1);
        Assert.AreEqual(4, s.Slots.Count);
        s.Select(0);
        s.Take("axe", 1);
        Assert.AreEqual("fists", s.Slots[0].id);
        Assert.IsTrue(s.Slots.Exists(i => i.id == "axe"));
        Assert.AreEqual(4, s.Slots.Count);
        s.Take("food", 2);
        Assert.AreEqual(3, s.Bag["food"]);
    }

    [Test]
    public void Hunger_Drains_ThenStarves_FoodRestores()
    {
        var s = new Survivor();
        s.Tick(1f);
        Assert.AreEqual(T.player.hungerMax - T.player.hungerPerSec, s.Hunger, 0.001f);
        s.Hunger = 0.01f;
        float hp = s.Hp;
        s.Tick(1f);
        Assert.AreEqual(0, s.Hunger);
        Assert.Less(s.Hp, hp, "an empty stomach hurts");
        Assert.IsTrue(s.Eat());
        Assert.AreEqual(T.player.foodValue, s.Hunger, 0.01f);
    }

    [Test]
    public void Coop_TeammateRevives_AloneBleedsOut()
    {
        var a = new Survivor();
        var b = new Survivor { Pos = new Vector2(0.5f, 0) };
        a.Hurt(9999);
        var all = new List<Survivor> { a, b };
        for (int i = 0; i < 40; i++) Survivor.CoopTick(all, 0.1f);
        Assert.IsFalse(a.Downed);
        a.Hurt(9999);
        b.Pos = new Vector2(30, 0);
        for (int i = 0; i < 200; i++) Survivor.CoopTick(all, 0.1f);
        Assert.IsTrue(a.Dead);
        b.Hurt(9999);
        Assert.IsFalse(Survivor.CoopTick(all, 0.1f));
    }

    [Test]
    public void Trade_SwapsGoods_OnlyWhenYouHaveThem()
    {
        var s = new Survivor();
        var nails = new Tuning.TradeCfg { give = "scrap", giveN = 2, get = "nails", getN = 12 };
        Assert.IsFalse(s.Trade(nails, out _), "no scrap yet");
        s.Take("scrap", 3);
        Assert.IsTrue(s.Trade(nails, out _));
        Assert.AreEqual(1, s.Bag["scrap"]);
        Assert.AreEqual(12, s.Bag["nails"]);
    }

    [Test]
    public void EveryShopType_HasALootTable_AndFusesCanBeFound()
    {
        foreach (var shop in T.shops) Assert.IsTrue(System.Array.Exists(T.lootTables, t => t.name == shop.type), shop.type);
        Assert.IsTrue(System.Array.Exists(T.Loot("hardware"), l => l.item == "fuse"));
        Assert.IsTrue(System.Array.Exists(T.Loot("drop"), l => l.item == "fuse"));
        foreach (var tr in T.trades) Assert.IsTrue(new Survivor().Bag.ContainsKey(tr.give), tr.give + " must be a bag item");
    }
}
