using DesktopTenants;
using NUnit.Framework;

public class EconomyTests
{
    [Test]
    public void ApartmentCost_IncreasesWithinBothTiers()
    {
        var t = Tuning.Load();

        int prev = 0;
        for (int floors = t.startFloors; floors < t.maxFloors; floors++)
        {
            int cost = t.ApartmentCost(floors);
            Assert.Greater(cost, prev, $"floor {floors + 1}");
            if (floors >= t.towerStartFloor)
                Assert.Less(cost / (float)prev, t.upgradeGrowth, $"floor {floors + 1} uses tower pricing");
            prev = cost;
        }
    }

    [Test]
    public void ApartmentExtension_PreservesOldPricesAndExpandsAtTheTop()
    {
        var t = Tuning.Load();
        Assert.AreEqual(16, t.maxFloors);
        Assert.AreEqual(50_000, t.ApartmentCost(3));
        Assert.AreEqual(7_402_000, t.ApartmentCost(9));
        Assert.AreEqual(8_290_000, t.ApartmentCost(10));
        Assert.Greater(Pixels.House(16).Length, Pixels.House(10).Length);
        Assert.Greater(Pixels.HouseCols(16), Pixels.HouseCols(10));
    }

    [Test]
    public void FirstExtraFloor_TakesAtLeastThreeMinutesOfStartingRent()
    {
        // Guards against the economy drifting back to "upgrade every two minutes".
        var t = Tuning.Load();
        var book = TenantBook.Load();
        float rent = 0;
        for (int i = 0; i < t.startTypes; i++) rent += book.types[i].rent;
        rent /= t.startTypes;
        int tenants = t.startFloors * t.tenantsPerFloor;
        float perMinute = tenants * rent / ((t.rentMin + t.rentMax) / 2) * 60;

        Assert.GreaterOrEqual(t.ApartmentCost(t.startFloors), perMinute * 3, $"starting income ≈ ₩{perMinute:N0}/min");
    }

    [Test]
    public void ShopUpgrades_CostMoreThanTheLotEachLevel()
    {
        var t = Tuning.Load();

        foreach (var lot in LotBook.Load().lots)
        {
            Assert.Greater(t.ShopUpgradeCost(lot.cost, 1), lot.cost, lot.name);
            Assert.Greater(t.ShopUpgradeCost(lot.cost, 2), t.ShopUpgradeCost(lot.cost, 1), lot.name);
        }
    }
}
