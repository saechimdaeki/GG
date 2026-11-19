using UnityEngine;

namespace LastTrain
{
    public partial class City
    {
        internal static bool BlocksSight(Bounds bounds, Vector3 camera, Vector3 target)
        {
            Vector3 delta = target - camera;
            float length = delta.magnitude;
            return length > 0.01f &&
                bounds.IntersectRay(new Ray(camera, delta / length), out float hit) && hit < length - 0.04f;
        }

        internal bool PlayerCovered(Vector3 head, Vector3 towardCamera)
        {
            foreach (var building in _buildings)
                if (building.SqrDistance(head) < 16f && BlocksSight(building, head + towardCamera, head))
                    return true;
            return false;
        }

        internal Vector2 FindOccludedAlley(Vector3 oldCameraOffset)
        {
            foreach (var bounds in _buildings)
            {
                var point = new Vector2(bounds.center.x, bounds.max.z + 0.85f);
                if (Vector2.Distance(point, Start) > 34f || Walls.Exists(w => w.Contains(point))) continue;
                if (BlocksSight(bounds, Fx.V3(point) + oldCameraOffset, Fx.V3(point, 1.25f))) return point;
            }
            return Start;
        }
    }
}
