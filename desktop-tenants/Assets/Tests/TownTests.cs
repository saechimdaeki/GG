using System.Collections.Generic;
using DesktopTenants;
using NUnit.Framework;

public class TownTests
{
    [Test]
    public void LotBook_TenLotsGetPricierInOrder()
    {
        var book = LotBook.Load();

        Assert.AreEqual(10, book.lots.Length);
        for (int i = 1; i < book.lots.Length; i++)
            Assert.Greater(book.lots[i].cost, book.lots[i - 1].cost, book.lots[i].name);
        foreach (var lot in book.lots) Assert.IsNotEmpty(lot.visit, lot.name);
    }

    [Test]
    public void EveryBuildingAndLevel_IsRectangularWithAWalkableTop()
    {
        var tops = new List<(float, float, float)>();
        foreach (var lot in LotBook.Load().lots)
            for (int level = 1; level <= Building.MaxLevel; level++)
            {
                var rows = TownArt.Rows(lot.id, level, tops);

                foreach (var r in rows) Assert.AreEqual(rows[0].Length, r.Length, $"{lot.id} L{level}");
                Assert.IsNotEmpty(tops, $"{lot.id} L{level} has nowhere to stand");
                foreach (var (x0, x1, top) in tops)
                {
                    Assert.Less(x0, x1);
                    Assert.LessOrEqual(top, rows.Length);
                }
            }
    }

    [Test]
    public void Apartment_GetsWiderAsItGetsTaller()
    {
        int w3 = Pixels.House(3)[0].Length, w4 = Pixels.House(4)[0].Length, w10 = Pixels.House(10)[0].Length;

        Assert.Less(w3, w4, "a fourth floor adds a window column");
        Assert.Less(w4, w10);
        foreach (var floors in new[] { 1, 4, 7, 10 })
        {
            var rows = Pixels.House(floors);
            foreach (var r in rows) Assert.AreEqual(rows[0].Length, r.Length, $"{floors} floors");
            StringAssert.Contains("dddd", rows[rows.Length - 2], $"{floors} floors has a door");
        }
    }

    [Test]
    public void ShopUpgrade_ChangesTheArt()
    {
        var tops = new List<(float, float, float)>();

        var plain = string.Join("\n", TownArt.Rows("store", 1, tops));
        var awning = string.Join("\n", TownArt.Rows("store", 2, tops));
        var bulbs = string.Join("\n", TownArt.Rows("store", 3, tops));

        Assert.AreNotEqual(plain, awning);
        Assert.AreNotEqual(awning, bulbs);
    }
}
