using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Left 4 Dead 2's melee slot: one swing cuts down a common infected, a few bring down a special.
    /// Models are Poly Haven CC0 scans (Wooden Axe, Crowbar 01, Baseball Bat, Machete).
    /// </summary>
    public sealed class MeleeWeapon
    {
        public readonly Gear gear;
        public readonly int damage;
        public readonly float interval;
        public readonly float reach;
        /// <summary>Most infected one swing can cut through.</summary>
        public readonly int cleave;

        MeleeWeapon(Gear gear, int damage, float interval, float reach, int cleave)
        {
            this.gear = gear;
            this.damage = damage;
            this.interval = interval;
            this.reach = reach;
            this.cleave = cleave;
        }

        public static MeleeWeapon For(Gear gear) => gear switch
        {
            Gear.FireAxe => new MeleeWeapon(gear, 280, 0.78f, 2.0f, 3),
            Gear.Crowbar => new MeleeWeapon(gear, 210, 0.62f, 1.9f, 2),
            Gear.Bat => new MeleeWeapon(gear, 190, 0.66f, 2.15f, 4),
            _ => new MeleeWeapon(gear, 240, 0.52f, 1.85f, 2),
        };

        static float Length(Gear gear) => gear switch
        {
            Gear.FireAxe => 0.78f,
            Gear.Crowbar => 0.6f,
            Gear.Bat => 0.84f,
            _ => 0.66f,
        };

        static string File(Gear gear) => gear switch
        {
            Gear.FireAxe => "wooden_axe",
            Gear.Crowbar => "crowbar_01",
            Gear.Bat => "baseball_bat",
            _ => "machete",
        };

        static readonly Dictionary<Gear, (Mesh mesh, Material material, Quaternion turn, Vector3 offset)[]> parts = new();

        /// <summary>
        /// The weapon upright: grip end at the pivot, striking end up along +Y. The scans come in any orientation,
        /// so the long axis is found from the mesh and the wider end is taken to be the head.
        /// </summary>
        public static GameObject Model(Gear gear)
        {
            var root = new GameObject(gear.Label());
            root.layer = 2;
            if (!parts.TryGetValue(gear, out var pieces))
                parts[gear] = pieces = Load(gear);
            if (pieces.Length == 0)
            {
                // Missing asset: a plain handle keeps the game playable.
                var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(stick.GetComponent<Collider>());
                stick.transform.SetParent(root.transform, false);
                stick.transform.localPosition = Vector3.up * 0.35f;
                stick.transform.localScale = new Vector3(0.04f, 0.35f, 0.04f);
            }
            foreach (var (mesh, material, turn, offset) in pieces)
            {
                var part = new GameObject(mesh.name);
                part.layer = 2;
                part.transform.SetParent(root.transform, false);
                part.transform.SetLocalPositionAndRotation(offset, turn);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.04f;
            collider.height = 0.7f;
            collider.center = Vector3.up * 0.35f;
            return root;
        }

        static (Mesh, Material, Quaternion, Vector3)[] Load(Gear gear)
        {
            string file = File(gear);
            var source = Resources.Load<GameObject>($"ResidualSignal/Melee/{file}/{file}_1k");
            if (source == null)
                return new (Mesh, Material, Quaternion, Vector3)[0];
            var texture = Resources.Load<Texture2D>($"ResidualSignal/Melee/{file}/textures/{file}_diff_1k");
            bool metal = gear is Gear.Crowbar or Gear.Machete;
            var material = GearInfo.Lit($"Melee {file}", Color.white, metal ? 0.55f : 0.05f, metal ? 0.55f : 0.35f, texture);

            // Every vertex in the file's own space, to find the long axis and which end is heavier.
            var filters = source.GetComponentsInChildren<MeshFilter>();
            var points = new List<Vector3>();
            foreach (var filter in filters)
            {
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var vertex in filter.sharedMesh.vertices)
                    points.Add(matrix.MultiplyPoint3x4(vertex));
            }
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
            Vector3 along = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            float low = bounds.min[axis], length = bounds.size[axis];
            float lowSpread = 0f, highSpread = 0f;
            foreach (var point in points)
            {
                float t = (point[axis] - low) / length;
                float spread = Vector3.ProjectOnPlane(point - bounds.center, along).magnitude;
                if (t < 0.3f) lowSpread = Mathf.Max(lowSpread, spread);
                if (t > 0.7f) highSpread = Mathf.Max(highSpread, spread);
            }
            Vector3 head = highSpread >= lowSpread ? along : -along;
            // Scans come in at whatever unit the file used; size each to its real length.
            float fit = Length(gear) / length;
            Debug.Log($"MELEE {file}: {filters.Length} parts, long axis {axis} {length:F3} -> {Length(gear)} m, head {(highSpread >= lowSpread ? "+" : "-")}");
            Quaternion upright = Quaternion.FromToRotation(head, Vector3.up);
            // Grip end on the pivot, centred on the shaft.
            Vector3 grip = (bounds.center - head * (length * 0.5f)) * fit;
            var result = new List<(Mesh, Material, Quaternion, Vector3)>();
            foreach (var filter in filters)
            {
                var local = Matrix4x4.Scale(Vector3.one * fit) * filter.transform.localToWorldMatrix;
                // ponytail: assumes the scan's parts carry no non-uniform scale (true of these four files).
                Quaternion turn = upright * local.rotation;
                Vector3 offset = upright * ((Vector3)local.GetColumn(3) - grip);
                Vector3 scale = local.lossyScale;
                var mesh = filter.sharedMesh;
                if (Mathf.Abs(scale.x - 1f) > 0.0001f)
                    mesh = Scaled(mesh, scale.x);
                result.Add((mesh, material, turn, offset));
            }
            return result.ToArray();
        }

        static Mesh Scaled(Mesh source, float scale)
        {
            var mesh = Object.Instantiate(source);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] *= scale;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
