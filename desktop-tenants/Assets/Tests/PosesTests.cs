using DesktopTenants;
using NUnit.Framework;

public class PosesTests
{
    static readonly string[] PersonPoses = { "idle", "walkA", "walkB", "fall", "wave", "phone", "sit", "pushA", "pushB", "climbA", "climbB" };
    static readonly string[] CatPoses = { "idle", "walkA", "walkB", "sleep", "fall", "groom", "stretch" };
    static readonly string[] DogPoses = { "idle", "wag", "walkA", "walkB", "sleep", "fall" };
    static readonly string[] AllGear = { "tie", "shades", "headphones", "cap", "longhair", "apron", "antenna", "backpack" };

    [Test]
    public void EveryPoseAndFace_LeavesNoPlaceholders()
    {
        foreach (Face face in System.Enum.GetValues(typeof(Face)))
        {
            foreach (var pose in PersonPoses) AssertPainted(Poses.Pose(pose, face, "person", AllGear), pose, face);
            foreach (var pose in CatPoses) AssertPainted(Poses.Pose(pose, face, "cat"), "cat " + pose, face);
            foreach (var pose in DogPoses) AssertPainted(Poses.Pose(pose, face, "dog"), "dog " + pose, face);
            foreach (var pose in new[] { "idle", "walkA", "walkB", "sleep", "fall" }) AssertPainted(Poses.Pose(pose, face, "robot"), "robot " + pose, face);
            foreach (var pose in new[] { "idle", "walkA", "walkB", "sleep", "fall", "slide" }) AssertPainted(Poses.Pose(pose, face, "penguin"), "penguin " + pose, face);
        }
    }

    [Test]
    public void BlinkAndNormal_DifferOnlyInTheFace()
    {
        var open = Poses.Pose("idle", Face.Normal, "person");
        var shut = Poses.Pose("idle", Face.Blink, "person");

        Assert.AreNotEqual(open[2], shut[2], "upper eye row closes");
        Assert.AreEqual(open[0], shut[0]);
        Assert.AreEqual(open[11], shut[11]);
    }

    [Test]
    public void Gear_PaintsOverPoseWithoutChangingSize()
    {
        var bare = Poses.Pose("idle", Face.Normal, "person");
        var dressed = Poses.Pose("idle", Face.Normal, "person", new[] { "tie" });

        Assert.AreEqual(bare.Length, dressed.Length);
        Assert.AreEqual(bare[6].Length, dressed[6].Length);
        Assert.AreEqual('k', dressed[6][5], "tie knot under the collar");
        Assert.AreNotEqual('k', bare[6][5]);
    }

    static void AssertPainted(string[] rows, string pose, Face face)
    {
        foreach (var r in rows)
            StringAssert.DoesNotMatch("[ULME]", r, $"{pose}/{face}");
    }
}
