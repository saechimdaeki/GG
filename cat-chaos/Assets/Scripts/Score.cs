using System;

namespace CatChaos
{
    /// <summary>
    /// Running chaos score. Discrete events inside the combo window raise the multiplier
    /// (x1 up to the cap); steady trickles (tissue, scratching) only ride the current one.
    /// </summary>
    public sealed class Score
    {
        static readonly string[] Letters = { "D", "C", "B", "A", "S" };
        readonly float _window;
        readonly int _max;
        float _last = float.NegativeInfinity;

        public int Total { get; private set; }
        public int Combo { get; private set; } = 1;

        public Score(float window, int max) { _window = window; _max = max; }

        /// <summary>Adds points at time <paramref name="now"/>; returns what was actually gained.</summary>
        public int Add(int points, float now, bool chain = true)
        {
            bool live = now - _last <= _window;
            if (chain) { Combo = live ? Math.Min(_max, Combo + 1) : 1; _last = now; }
            else if (!live) Combo = 1;
            int gained = points * Combo;
            Total += gained;
            return gained;
        }

        /// <summary>0..1 of the combo window still left (0 when there is no combo).</summary>
        public float ComboLeft(float now) => Combo > 1 ? Math.Max(0f, 1f - (now - _last) / _window) : 0f;

        /// <summary>Flat points that ignore the combo (goal rewards).</summary>
        public int Bonus(int points) { Total += points; return points; }

        /// <summary>Takes points away (never below zero); returns how many were actually lost.</summary>
        public int Penalty(int points) { int lost = Math.Min(points, Total); Total -= lost; return lost; }

        public void Multiply(float factor) => Total = (int)Math.Round(Total * factor);

        /// <summary>D below the first threshold, then C, B, A, S.</summary>
        public static string Grade(int total, int[] thresholds)
        {
            int i = 0;
            while (i < thresholds.Length && i < Letters.Length - 1 && total >= thresholds[i]) i++;
            return Letters[i];
        }
    }
}
