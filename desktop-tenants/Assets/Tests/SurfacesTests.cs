using System.Collections.Generic;
using DesktopTenants;
using NUnit.Framework;

public class SurfacesTests
{
    const float FloorY = 900, Width = 1500;

    static List<Seg> Of(params WinRect[] frontToBack) => Surfaces.Compute(frontToBack, FloorY, Width);

    [Test]
    public void EmptyDesktop_OnlyFloor()
    {
        var s = Of();
        Assert.AreEqual(1, s.Count);
        Assert.AreEqual(Surfaces.Floor, s[0].win);
        Assert.AreEqual(Width, s[0].x1);
    }

    [Test]
    public void FrontWindowOverEdge_SplitsEdge()
    {
        // Back window top edge y=200 spans 100..900; front window covers x 400..600 around y=200.
        var s = Of(new WinRect(2, 400, 150, 200, 300), new WinRect(1, 100, 200, 800, 400));
        var back = s.FindAll(x => x.win == 1);
        Assert.AreEqual(2, back.Count);
        Assert.AreEqual(100, back[0].x0); Assert.AreEqual(400, back[0].x1);
        Assert.AreEqual(600, back[1].x0); Assert.AreEqual(900, back[1].x1);
        Assert.AreEqual(1, s.FindAll(x => x.win == 2).Count, "front edge stays whole");
    }

    [Test]
    public void WindowBelowEdge_KeepsEdgeWhole()
    {
        // Front window starts lower (y=300) than the back edge (y=200): edge is visible.
        var s = Of(new WinRect(2, 400, 300, 200, 300), new WinRect(1, 100, 200, 800, 400));
        Assert.AreEqual(1, s.FindAll(x => x.win == 1).Count);
    }

    [Test]
    public void MaximizedWindow_FloorStillWalkable()
    {
        // Tenants draw above every window, so the floor (the line above the Dock) is never
        // hidden; clipping it left them falling forever whenever a window reached the Dock.
        var s = Of(new WinRect(1, 0, 30, Width, FloorY));
        var floor = s.Find(x => x.win == Surfaces.Floor);
        Assert.AreEqual(Surfaces.Floor, floor.win);
        Assert.AreEqual(Width, floor.x1 - floor.x0);
    }

    [Test]
    public void WindowHangingOffScreen_EdgeClippedToScreen()
    {
        var s = Of(new WinRect(1, -300, 200, 800, 400), new WinRect(2, 1200, 300, 900, 400));

        var left = s.Find(x => x.win == 1);
        var right = s.Find(x => x.win == 2);
        Assert.AreEqual(0, left.x0); Assert.AreEqual(500, left.x1);
        Assert.AreEqual(1200, right.x0); Assert.AreEqual(Width, right.x1);
    }

    [Test]
    public void WindowOnAnotherMonitor_NotWalkable()
    {
        // Entirely left of this screen, and one on a monitor stacked above it.
        var s = Of(new WinRect(1, -1900, 200, 800, 400), new WinRect(2, 100, -700, 800, 400));

        Assert.AreEqual(1, s.Count, "only the floor remains");
    }

    [Test]
    public void ClimbFromFloor_FindsWindowAboveWithLadderRange()
    {
        var segs = Of(new WinRect(1, 300, 400, 500, 300));
        var floor = segs.Find(x => x.win == Surfaces.Floor);
        var options = new List<(Seg to, float lo, float hi)>();

        Surfaces.ClimbOptions(segs, floor, 40, 30, options);

        Assert.AreEqual(1, options.Count);
        Assert.AreEqual(1, options[0].to.win);
        Assert.AreEqual(314, options[0].lo);
        Assert.AreEqual(786, options[0].hi);
    }

    [Test]
    public void ClimbFromWindow_SkipsEdgesNotAboveIt()
    {
        // Standing on window 1 (x 100..400). Window 2 is higher but off to the side; window 3 is lower.
        var segs = Of(new WinRect(2, 700, 100, 300, 200), new WinRect(1, 100, 400, 300, 300), new WinRect(3, 150, 600, 200, 200));
        var on = segs.Find(x => x.win == 1);
        var options = new List<(Seg to, float lo, float hi)>();

        Surfaces.ClimbOptions(segs, on, 40, 30, options);

        Assert.AreEqual(0, options.Count);
    }

    [Test]
    public void GapNarrowerThanMinWidth_Dropped()
    {
        // Two front windows leave a 5pt gap on the back edge.
        var s = Of(new WinRect(3, 0, 100, 500, 300), new WinRect(2, 505, 100, 500, 300), new WinRect(1, 0, 200, 1005, 300));
        Assert.IsFalse(s.Exists(x => x.win == 1));
    }
}
