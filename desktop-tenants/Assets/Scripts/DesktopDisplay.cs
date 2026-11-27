using UnityEngine;

namespace DesktopTenants
{
    /// <summary>One monitor in the shared desktop coordinate system.</summary>
    public readonly struct DesktopDisplay
    {
        public readonly Rect Bounds;
        public readonly Seg Floor;

        public DesktopDisplay(int index, Rect bounds, float floorY)
        {
            Bounds = bounds;
            Floor = new Seg(index == 0 ? Surfaces.Floor : -2 - index, bounds.xMin, bounds.xMax, floorY);
        }

        /// <summary>Finds a route only where two physical display edges share a usable span.</summary>
        public bool TryRoute(DesktopDisplay other, float fromX, out float departure, out Vector2 arrival)
        {
            const float edgeTolerance = 1f, margin = 24f;
            var a = Bounds;
            var b = other.Bounds;
            departure = 0;
            arrival = default;
            if (Floor.win == other.Floor.win) return false;

            float sharedTop = Mathf.Max(a.yMin, b.yMin);
            float sharedBottom = Mathf.Min(Floor.y, other.Floor.y);
            bool right = Mathf.Abs(a.xMax - b.xMin) <= edgeTolerance;
            bool left = Mathf.Abs(a.xMin - b.xMax) <= edgeTolerance;
            if ((right || left) && sharedBottom - sharedTop >= margin * 2)
            {
                departure = right ? a.xMax - 4 : a.xMin + 4;
                arrival = new Vector2(right ? b.xMin + margin : b.xMax - margin, sharedBottom - 8);
                return true;
            }

            float sharedLeft = Mathf.Max(a.xMin, b.xMin);
            float sharedRight = Mathf.Min(a.xMax, b.xMax);
            bool above = Mathf.Abs(a.yMin - b.yMax) <= edgeTolerance;
            bool below = Mathf.Abs(a.yMax - b.yMin) <= edgeTolerance;
            if ((!above && !below) || sharedRight - sharedLeft < margin * 2) return false;
            departure = Mathf.Clamp(fromX, sharedLeft + margin, sharedRight - margin);
            arrival = new Vector2(departure, above ? other.Floor.y - 8 : b.yMin + margin);
            return true;
        }

        /// <summary>Screen-edge departure and arrival for monitors separated by an excluded display.</summary>
        public bool TryGapRoute(DesktopDisplay other, float fromX, out float departure, out Vector2 arrival)
        {
            departure = 0;
            arrival = default;
            if (Floor.win == other.Floor.win) return false;

            var a = Bounds;
            var b = other.Bounds;
            const float margin = 24f;
            if (a.xMax <= b.xMin || b.xMax <= a.xMin)
            {
                bool right = a.center.x < b.center.x;
                departure = right ? a.xMax - 4 : a.xMin + 4;
                arrival = new Vector2(right ? b.xMin + margin : b.xMax - margin, other.Floor.y - 12);
                return true;
            }
            if (a.yMax > b.yMin && b.yMax > a.yMin) return false;
            departure = Mathf.Clamp(fromX, Mathf.Max(a.xMin, b.xMin), Mathf.Min(a.xMax, b.xMax));
            arrival = new Vector2(Mathf.Clamp(departure, b.xMin + margin, b.xMax - margin), other.Floor.y - 12);
            return true;
        }
    }
}
