using CatChaos;
using NUnit.Framework;

/// <summary>Combo, steady trickle and grade rules of the chaos score.</summary>
public class ScoreTests
{
    const float Window = 2.5f;
    const int Max = 5;
    static readonly int[] Grades = { 1500, 4000, 8000, 14000 };

    [Test]
    public void Events_inside_window_raise_combo_up_to_cap()
    {
        var s = new Score(Window, Max);
        Assert.AreEqual(10, s.Add(10, 0f));
        Assert.AreEqual(20, s.Add(10, 1f));
        for (int i = 0; i < 10; i++) s.Add(10, 2f + i);
        Assert.AreEqual(Max, s.Combo);
    }

    [Test]
    public void Gap_longer_than_window_resets_combo()
    {
        var s = new Score(Window, Max);
        s.Add(10, 0f);
        s.Add(10, 1f);
        Assert.AreEqual(10, s.Add(10, 1f + Window + 0.01f));
        Assert.AreEqual(1, s.Combo);
    }

    [Test]
    public void Steady_points_ride_combo_without_raising_it()
    {
        var s = new Score(Window, Max);
        s.Add(10, 0f);
        s.Add(10, 0.5f);
        Assert.AreEqual(2, s.Combo);
        Assert.AreEqual(8, s.Add(4, 1f, chain: false));
        Assert.AreEqual(2, s.Combo);
        Assert.AreEqual(4, s.Add(4, 10f, chain: false));
        Assert.AreEqual(1, s.Combo);
    }

    [Test]
    public void Hide_bonus_multiplies_total()
    {
        var s = new Score(Window, Max);
        s.Add(1000, 0f);
        s.Multiply(1.5f);
        Assert.AreEqual(1500, s.Total);
    }

    [TestCase(0, "D")]
    [TestCase(1499, "D")]
    [TestCase(1500, "C")]
    [TestCase(4000, "B")]
    [TestCase(8000, "A")]
    [TestCase(14000, "S")]
    [TestCase(999999, "S")]
    public void Grade_thresholds(int total, string grade) => Assert.AreEqual(grade, Score.Grade(total, Grades));
}
