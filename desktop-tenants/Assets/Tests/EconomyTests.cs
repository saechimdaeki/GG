using DesktopTenants;
using NUnit.Framework;

public class EconomyTests
{
    [Test]
    public void ApartmentCost_GrowsFasterEveryFloor()
    {
        var t = Tuning.Load();

        int prev = 0, prevStep = 0;
        for (int floors = t.startFloors; floors < t.maxFloors; floors++)
        {
            int cost = t.ApartmentCost(floors);
            Assert.Greater(cost, prev, $"floor {floors + 1}");
            Assert.Greater(cost - prev, prevStep, $"floor {floors + 1} should jump more than the last one");
            prevStep = cost - prev;
            prev = cost;
        }
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
