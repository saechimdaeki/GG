using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>An app window on screen, in points with a top-left origin.</summary>
    public struct WinRect
    {
        public int id;
        public float x, y, w, h;
        public WinRect(int id, float x, float y, float w, float h) { this.id = id; this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    /// <summary>A walkable horizontal line: the visible part of a window's top edge, or the floor.</summary>
    public struct Seg
    {
        public int win;
        public float x0, x1, y;
        public Seg(int win, float x0, float x1, float y) { this.win = win; this.x0 = x0; this.x1 = x1; this.y = y; }
        public override string ToString() => $"[{win}] {x0}..{x1} @ {y}";
    }

    /// <summary>Turns the desktop's window stack into the lines tenants can stand on.</summary>
    public static class Surfaces
    {
        public const int Floor = -1;
        /// <summary>The apartment's roof, walkable like a window edge.</summary>
        public const int Roof = -2;
        public const float MinWidth = 12f;

        /// <summary>
        /// Walkable segments for windows given front-to-back, plus the floor at <paramref name="floorY"/>.
        /// Each window's top edge is clipped by every window in front of it, so a tenant never stands
        /// on a line that is hidden behind another window.
        /// </summary>
        public static List<Seg> Compute(IReadOnlyList<WinRect> wins, float floorY, float width, List<Seg> into = null)
        {
            var segs = into ?? new List<Seg>();
            segs.Clear();
            for (int i = 0; i < wins.Count; i++)
            {
                // Only the part of the edge on this screen is walkable (windows can hang off it).
                float x0 = Mathf.Max(wins[i].x, 0), x1 = Mathf.Min(wins[i].x + wins[i].w, width);
                if (wins[i].y < 0 || wins[i].y > floorY) continue;
                Clip(new Seg(wins[i].id, x0, x1, wins[i].y), wins, i, 0, segs);
            }
            // The floor is never clipped: tenants draw above every window, and a window reaching
            // the Dock would otherwise leave nowhere to stand.
            segs.Add(new Seg(Floor, 0, width, floorY));
            return segs;
        }

        /// <summary>
        /// Edges a tenant standing on <paramref name="from"/> could put a ladder up to: at least
        /// <paramref name="minRise"/> higher, below <paramref name="ceiling"/>, and overlapping
        /// <paramref name="from"/> by enough to stand a ladder under. Each option carries the x range
        /// (<c>lo..hi</c>) where the ladder can go.
        /// </summary>
        public static void ClimbOptions(List<Seg> segs, Seg from, float minRise, float ceiling, List<(Seg to, float lo, float hi)> into)
        {
            into.Clear();
            const float margin = 14f;
            foreach (var s in segs)
            {
                if (s.win == from.win || s.y > from.y - minRise || s.y < ceiling) continue;
                float lo = Mathf.Max(from.x0, s.x0) + margin, hi = Mathf.Min(from.x1, s.x1) - margin;
                if (hi > lo) into.Add((s, lo, hi));
            }
        }

        // ponytail: recursive interval subtraction, O(n²) in windows; fine for the ~30 windows a desktop has.
        static void Clip(Seg s, IReadOnlyList<WinRect> wins, int limit, int j, List<Seg> outSegs)
        {
            for (; j < limit; j++)
            {
                var f = wins[j];
                if (s.y <= f.y || s.y >= f.y + f.h) continue;
                float a = f.x, b = f.x + f.w;
                if (b <= s.x0 || a >= s.x1) continue;
                if (a > s.x0) Clip(new Seg(s.win, s.x0, a, s.y), wins, limit, j + 1, outSegs);
                if (b < s.x1) Clip(new Seg(s.win, b, s.x1, s.y), wins, limit, j + 1, outSegs);
                return;
            }
            if (s.x1 - s.x0 >= MinWidth) outSegs.Add(s);
        }
    }
}
