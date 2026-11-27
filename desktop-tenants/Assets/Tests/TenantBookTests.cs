using System;
using DesktopTenants;
using NUnit.Framework;
using UnityEngine;

public class TenantBookTests
{
    static readonly string[] Specials = { "phone", "yawn", "pushup", "groom", "selfie", "sing", "wag", "dj", "cook", "game", "deliver", "teleport", "vacuum", "slide", "cry", "patrol", "magic", "paint", "bake", "photo", "yoyo", "tailor", "flute", "stretchCoach" };

    [Test]
    public void EveryType_HasLookRentAndCoreLines()
    {
        var book = TenantBook.Load();

        Assert.GreaterOrEqual(book.types.Length, 18);
        foreach (var t in book.types)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(t.hair, out _), t.name + " hair");
            Assert.IsTrue(ColorUtility.TryParseHtmlString(t.shirt, out _), t.name + " shirt");
            Assert.Greater(t.rent, 0, t.name + " rent");
            foreach (var lines in new[] { t.idle, t.fling, t.poke, t.greet, t.pay })
                Assert.IsNotEmpty(lines, t.name + " is missing a line set");
            if (t.jumper) Assert.IsNotEmpty(t.jump, t.name + " jumps but has no jump line");
            CollectionAssert.Contains(Specials, t.special, t.name + " special");
            CollectionAssert.Contains(new[] { "person", "cat", "dog", "robot", "penguin" }, t.kind, t.name + " kind");
        }
    }

    [Test]
    public void NewResidents_UnlockWithinTheApartmentFloorLimit()
    {
        var book = TenantBook.Load();
        var tuning = Tuning.Load();
        int maxTypes = tuning.startTypes + tuning.typesPerFloor * (tuning.maxFloors - tuning.startFloors);

        foreach (string action in new[] { "bake", "photo" })
        {
            int index = Array.FindIndex(book.types, type => type.special == action);
            Assert.That(index, Is.InRange(0, maxTypes - 1), action);
        }
    }

    [TestCase("yoyo", 3)]
    [TestCase("tailor", 4)]
    [TestCase("flute", 5)]
    [TestCase("stretchCoach", 6)]
    public void NewResidents_HaveExplicitEarlyFloorUnlocks(string special, int floor)
    {
        var type = Array.Find(TenantBook.Load().types, candidate => candidate.special == special);
        Assert.IsNotNull(type, special);
        Assert.AreEqual(floor, type.minFloor);
    }

    [Test]
    public void Convos_AreCompletePairs()
    {
        var book = TenantBook.Load();

        Assert.GreaterOrEqual(book.convos.Length, 8);
        foreach (var c in book.convos)
        {
            Assert.IsNotEmpty(c.a);
            Assert.IsNotEmpty(c.b, "answer to: " + c.a);
        }
    }

    [Test]
    public void TypeIdleLines_NeverNameAWeekday()
    {
        // Weekday talk comes from the real clock (TenantBook.Day), never hard-coded per type.
        var book = TenantBook.Load();

        foreach (var t in book.types)
            foreach (var line in t.idle)
                StringAssert.DoesNotMatch("[월화수목금토일]요일", line, t.name);
    }

    [Test]
    public void Day_FollowsTheCalendar()
    {
        var book = TenantBook.Load();

        // 2026-11-26 is a Thursday, 2026-11-30 a Monday.
        Assert.AreSame(book.thu, book.Day(new DateTime(2026, 11, 26)));
        Assert.AreSame(book.mon, book.Day(new DateTime(2026, 11, 30)));
        for (int d = 0; d < 7; d++)
            Assert.IsNotEmpty(book.Day(new DateTime(2026, 11, 23).AddDays(d)), "day " + d);
    }

    [Test]
    public void TimeOfDay_LunchAtNoonNothingAtFourAm()
    {
        var book = TenantBook.Load();

        Assert.AreSame(book.lunch, book.TimeOfDay(new DateTime(2026, 11, 26, 12, 30, 0)));
        Assert.IsNull(book.TimeOfDay(new DateTime(2026, 11, 26, 4, 0, 0)));
    }
}
