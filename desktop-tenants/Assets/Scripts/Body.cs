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
                p = new Vector2(next.x, seg.y);
                on = seg.win;
                d.WinPos(on, out anchor);
                v = Vector2.zero;
                return FallResult.Landed;
            }
            p = next;
            if (p.x < 10 || p.x > d.ScreenW - 10) { p.x = Mathf.Clamp(p.x, 10, d.ScreenW - 10); v.x = -v.x * 0.5f; }
            if (p.y - height < 0 && v.y < 0) { p.y = height; v.y = -v.y * 0.3f; return FallResult.Bonk; }
            if (p.y > d.ScreenH + 60) { p = d.Door; v = Vector2.zero; return FallResult.Respawned; }
            return FallResult.Air;
        }
    }
}
