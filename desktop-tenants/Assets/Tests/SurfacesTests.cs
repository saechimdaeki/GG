using System.Collections.Generic;
using DesktopTenants;
using NUnit.Framework;

public class SurfacesTests
{
    const float FloorY = 900, Width = 1500;

    static List<Seg> Of(params WinRect[] frontToBack) => Surfaces.Compute(frontToBack, FloorY, Width);

    [Test]
    public void OffsetDisplays_UseTheirOwnFloorsAndClipWindowEdges()
    {
        var left = new DesktopDisplay(1, new UnityEngine.Rect(-1920, -740, 1920, 1080), 340);
        var right = new DesktopDisplay(0, new UnityEngine.Rect(0, 0, 1500, 900), 850);
        var windows = new[] { new WinRect(1, -200, -500, 400, 600) };
        var segs = new List<Seg>();

        Surfaces.AppendDisplay(windows, left, segs);
        Surfaces.AppendDisplay(windows, right, segs);

        Assert.AreEqual(3, segs.Count);
        Assert.AreEqual(0, segs[0].x1);
        Assert.AreEqual(340, segs[1].y);
        Assert.AreNotEqual(segs[1].win, segs[2].win);
        Assert.AreEqual(850, segs[2].y);
    }

    [Test]
    public void OffsetDisplays_EnterThroughTheSharedSide()
    {
        var main = new DesktopDisplay(0, new UnityEngine.Rect(0, 0, 1512, 982), 924);
        var left = new DesktopDisplay(1, new UnityEngine.Rect(-1920, -740, 1920, 1080), 340);
        var right = new DesktopDisplay(2, new UnityEngine.Rect(1512, -780, 1920, 1080), 300);

        Assert.IsTrue(main.TryRoute(left, 1200, out float departure, out var arrival));
        Assert.That(departure, Is.LessThan(10));
        Assert.That(arrival.y, Is.InRange(0, left.Floor.y));
        Assert.IsTrue(left.TryRoute(main, -600, out _, out arrival));
        Assert.IsTrue(main.Bounds.Contains(arrival));
        Assert.IsTrue(main.TryRoute(right, 1200, out departure, out arrival));
        Assert.That(departure, Is.GreaterThan(1500));
        Assert.IsTrue(right.Bounds.Contains(arrival));
        Assert.IsFalse(left.TryRoute(right, -600, out _, out _));
    }

    [Test]
    public void ExcludedDisplayBetweenMonitors_ConnectsFacingEdges()
    {
        var left = new DesktopDisplay(0, new UnityEngine.Rect(0, 0, 1920, 1080), 1020);
        var right = new DesktopDisplay(2, new UnityEngine.Rect(3432, -41, 1920, 1080), 1039);

        Assert.IsFalse(left.TryRoute(right, 1400, out _, out _));
        Assert.IsTrue(left.TryGapRoute(right, 1400, out float departure, out var arrival));
        Assert.That(departure, Is.EqualTo(1916));
        Assert.That(arrival.x, Is.EqualTo(3456));
        Assert.IsTrue(right.Bounds.Contains(arrival));
        Assert.IsTrue(right.TryGapRoute(left, 4100, out departure, out arrival));
        Assert.That(departure, Is.EqualTo(3436));
        Assert.That(arrival.x, Is.EqualTo(1896));
    }

    [Test]
    public void StackedDisplays_UseOnlyTheOverlappingWidth()
    {
        var lower = new DesktopDisplay(0, new UnityEngine.Rect(0, 0, 1500, 950), 900);
        var upper = new DesktopDisplay(1, new UnityEngine.Rect(750, -1000, 1500, 1000), -30);
        var gap = new DesktopDisplay(2, new UnityEngine.Rect(1550, 0, 1500, 950), 900);

        Assert.IsTrue(lower.TryRoute(upper, 100, out float departure, out var arrival));
        Assert.That(departure, Is.InRange(750, 1500));
        Assert.IsTrue(upper.Bounds.Contains(arrival));
        Assert.IsFalse(lower.TryRoute(gap, 100, out _, out _));
    }

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
