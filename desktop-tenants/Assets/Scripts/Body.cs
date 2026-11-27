using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// Shared desktop physics for tenants and coins: stand on a window edge and ride it,
    /// get flung when it moves fast, fall under gravity, bonk on the top of the screen.
    /// Desktop space: points, top-left origin, y grows downward.
    /// </summary>
    public class Body
    {
        public enum RideResult { Ok, Lost, Flung }
        public enum FallResult { Air, Landed, Bonk, Respawned }

        public const int None = int.MinValue;
        public Vector2 p, v;
        public int on = None;
        Vector2 anchor;

        public bool Grounded => on != None;

        public void Drop(Vector2 vel)
        {
            on = None;
            v = vel;
        }

        /// <summary>Stand on <paramref name="seg"/> at the current x (end of a ladder climb).</summary>
        public void Stand(Desk d, Seg seg)
        {
            p.y = seg.y;
            v = Vector2.zero;
            on = seg.win;
            d.WinPos(on, out anchor);
        }

        /// <summary>Follow the supporting window. <paramref name="seg"/> is the edge underfoot when Ok.</summary>
        public RideResult Ride(Desk d, float dt, out Seg seg)
        {
            seg = default;
            if (!d.WinPos(on, out var wp)) { Drop(Vector2.zero); return RideResult.Lost; }
            var moved = wp - anchor;
            anchor = wp;
            if (moved != Vector2.zero)
            {
                if (moved.magnitude / dt > d.Tune.holdSpeed) { Drop(Vector2.ClampMagnitude(moved / dt * d.Tune.flingScale, d.Tune.throwMax)); return RideResult.Flung; }
                p += moved;
            }
            if (!d.SegAt(on, p, out seg)) { Drop(Vector2.zero); return RideResult.Lost; }
            return RideResult.Ok;
        }

        /// <summary>One step of free fall for something <paramref name="height"/> points tall.</summary>
        public FallResult Fall(Desk d, float dt, float height)
        {
            v.y += d.Tune.gravity * dt;
            v.x += d.Wind * dt * 0.6f;
            var next = p + v * dt;
            if (v.y > 0 && d.Land(p, next, out var seg))
            {
                p = new Vector2(Mathf.Clamp(next.x, seg.x0, seg.x1), seg.y);
                on = seg.win;
                d.WinPos(on, out anchor);
                v = Vector2.zero;
                return FallResult.Landed;
            }
            int from = d.DisplayAt(p, 0);
            int to = d.DisplayAt(next);
            var display = d.DisplayBounds(to >= 0 ? to : from);
            var bounds = display.Bounds;
            p = next;
            if (to < 0 && (p.x < bounds.xMin || p.x >= bounds.xMax))
            {
                p.x = Mathf.Clamp(p.x, bounds.xMin + 10, bounds.xMax - 10);
                v.x = -v.x * 0.5f;
            }
            if (p.y - height < bounds.yMin && v.y < 0 && d.DisplayAt(p - new Vector2(0, height)) < 0)
            {
                p.y = bounds.yMin + height;
                v.y = -v.y * 0.3f;
                return FallResult.Bonk;
            }
            if (p.y > display.Floor.y && v.y >= 0 && to < 0)
            {
                Stand(d, display.Floor);
                return FallResult.Landed;
            }
            return FallResult.Air;
        }
    }
}
