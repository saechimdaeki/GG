using DesktopTenants;
using NUnit.Framework;

public class ChatOutcomeTests
{
    [Test]
    public void PeopleOutcomeSlices_FitInOneRollWithRoomForPlainChat()
    {
        // Every outcome takes a slice of one Random.value roll; past 1.0 the last ones never happen.
        var t = Tuning.Load();

        float total = t.danceChance + t.highfiveChance + t.argueChance + t.rpsChance + t.hugChance + t.tagChance + t.shareChance + t.mirrorChance + t.coinTrickChance;

        Assert.Less(total, 1f, "some chats should stay plain chats");
    }

    [Test]
    public void DanceChance_StaysAMinorityOfChats()
    {
        // Regression: at 0.4 the tenants looked like they danced all day.
        Assert.LessOrEqual(Tuning.Load().danceChance, 0.2f);
    }
}
