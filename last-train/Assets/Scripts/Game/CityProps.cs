using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTrain
{
    /// <summary>
    /// Real-world-scale Poly Haven props (CC0) for the alleys: trash cans and bag piles, crates,
    /// covered cars, road barriers, payphones, hydrants, wall-mounted air conditioners, shutters,
    /// street lamps and manholes. Each model ships with its own PBR material (Lit.mat).
    /// </summary>
    public partial class City
    {
        readonly Dictionary<string, (GameObject model, Material mat)> _haven = new();

        GameObject Haven(string id, Vector3 at, float yaw, float scale = 1f, Transform parent = null)
        {
            if (!_haven.TryGetValue(id, out var src))
            {
                src = (Resources.Load<GameObject>($"Art/Haven/Models/{id}/{id}"), Resources.Load<Material>($"Art/Haven/Models/{id}/Lit"));
                _haven[id] = src;
            }
            if (!src.model) { Debug.LogWarning("missing haven model " + id); return new GameObject(id); }
            var go = Instantiate(src.model, parent ? parent : transform);
            go.name = id;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0) * src.model.transform.rotation);
            go.transform.localScale *= scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (src.mat)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = src.mat;
                    r.sharedMaterials = mats;
                }
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            return go;
        }

        void Solid(GameObject go)
        {
            var b = BoundsOf(go);
            Walls.Add(new Rect(b.min.x, b.min.z, b.size.x, b.size.z));
        }

        /// <summary>A trash can with garbage bags heaped around it — the searchable alley bin.</summary>
        Transform TrashPile(Vector2 at, float yaw)
        {
            var t = new GameObject("trash pile").transform;
            t.SetParent(transform);
            t.SetPositionAndRotation(Fx.V3(at), Quaternion.Euler(0, yaw, 0));
            Haven("metal_trash_can", t.position, yaw + R(-20, 20), 1f, t);
            for (int k = 0; k < 3; k++)
                Haven("trashbag", t.position + t.rotation * new Vector3(R(0.45f, 0.9f) * (k == 1 ? -1 : 1), 0, R(-0.4f, 0.4f)), R(0, 360), R(0.8f, 1.1f), t);
            return t;
        }

        /// <summary>Cardboard boxes stacked on a plastic crate.</summary>
        Transform BoxPile(Vector2 at, float yaw)
        {
            var t = new GameObject("boxes").transform;
            t.SetParent(transform);
            t.SetPositionAndRotation(Fx.V3(at), Quaternion.Euler(0, yaw, 0));
            Haven(_rng.NextDouble() < 0.5 ? "plastic_crate_01" : "plastic_crate_03", t.position, yaw + R(-10, 10), 1f, t);
            var crateTop = BoundsOf(t.gameObject).max.y;
            Haven("cardboard_box_01", t.position + Vector3.up * crateTop, yaw + R(-25, 25), R(0.9f, 1.15f), t);
            Haven("cardboard_box_01", t.position + t.rotation * new Vector3(0.7f, 0, R(-0.2f, 0.2f)), yaw + R(-30, 30), R(0.9f, 1.2f), t);
            return t;
        }

        static readonly string[] LotProps =
        {
            "covered_car", "concrete_road_barrier", "old_tyre", "propane_tank", "metal_jerrycan", "portable_generator",
            "korean_public_payphone_01", "fire_hydrant", "plastic_crate_01", "trashbag", "hand_truck", "wooden_military_crate",
        };

        /// <summary>Wall clutter for a building face: AC units, utility boxes, security lights, a shutter at street level.</summary>
        void DressFacade(Bounds b, Vector3 n)
        {
            var along = new Vector3(Mathf.Abs(n.z), 0, Mathf.Abs(n.x));
            float half = Vector3.Dot(b.extents, along) - 0.8f;
            var face = b.center + Vector3.Scale(n, b.extents);
            face.y = 0;
            float yaw = Quaternion.LookRotation(n).eulerAngles.y;
            if (_rng.NextDouble() < 0.7)
                Haven("exterior_aircon_unit", face + n * 0.3f + along * R(-half, half), yaw);
            if (_rng.NextDouble() < 0.4)
                Haven("power_box_01", face + n * 0.08f + along * R(-half, half) + Vector3.up * 1.1f, yaw);
            if (_rng.NextDouble() < 0.4)
                Haven("security_light", face + n * 0.05f + along * R(-half, half) + Vector3.up * 2.6f, yaw);
            if (_rng.NextDouble() < 0.5)
                Haven("rollershutter_door", face + n * 0.02f + along * R(-half * 0.6f, half * 0.6f), yaw);
        }

        void StreetClutter(int cells)
        {
            for (int k = 0; k < 26; k++)
            {
                var p = new Vector2(R(-Half + 2, Half - 2), R(-Half + 2, Half - 2));
                if (Walls.Exists(w => w.Overlaps(new Rect(p.x - 0.6f, p.y - 0.6f, 1.2f, 1.2f))) || (p - Start).magnitude < 5f) continue;
                Haven("water_manhole_cover", Fx.V3(p, 0.012f), R(0, 360));
            }
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector2(R(-Half + 3, Half - 3), R(-Half + 3, Half - 3));
                if (Walls.Exists(w => w.Contains(p))) continue;
                Haven("street_rat", Fx.V3(p), R(0, 360));
            }
        }
    }
}
