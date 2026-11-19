using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTrain
{
    /// <summary>
    /// Low-rise brick buildings assembled from the Poly Haven modular apartment facade kit (CC0):
    /// a plinth, a ground floor with doors, window storeys, a cornice and a crown parapet on each
    /// face, around a brick core with a green-painted flat roof (the old-town rooftop look) and
    /// rooftop clutter. Kit pieces are measured once in kit space and laid left to right; each
    /// row is stretched to fit the face exactly.
    /// </summary>
    public partial class City
    {
        class Module { public Transform src; public Bounds b; }

        /// <summary>A kit building whose upper storeys and roof can be cut away when they hide the survivor.</summary>
        class House { public Bounds bounds; public GameObject upper; }
        readonly List<House> _houses = new();

        /// <summary>Cuts away every building that stands between the camera and the survivor (ground floor stays).</summary>
        public void CutAway(Vector3 camera, Vector3 head, Vector3 feet)
        {
            foreach (var h in _houses)
            {
                bool near = h.bounds.SqrDistance(head) < 18f * 18f;
                bool hides = near && (BlocksSight(h.bounds, camera, head) || BlocksSight(h.bounds, camera, feet));
                if (h.upper.activeSelf == hides) h.upper.SetActive(!hides);
            }
        }

        Dictionary<string, Module> _kit;
        Transform _kitRoot;
        Material _wallMat, _glassMat;
        Material[] _cityWindows;

        // kit faces -Z in its own space; holders look along +Z = outward
        const bool KitFrontIsNegZ = true;

        static readonly string[] Ground = { "dado_standard_standard_01", "dado_door_centered_large_01", "dado_standard_standard_01", "dado_door_offset_small_01" };
        static readonly string[] Storey = { "wall_window_centered_large_01", "wall_window_centered_double_01", "wall_window_offset_small_01", "wall_window_centered_small_01" };

        Module Kit(string name)
        {
            if (_kit == null)
            {
                _kit = new Dictionary<string, Module>();
                var prefab = Resources.Load<GameObject>("Art/Haven/Models/modular_urban_apartments_facade/modular_urban_apartments_facade");
                _wallMat = Resources.Load<Material>("Materials/Brick");
                _glassMat = Resources.Load<Material>("Materials/Glass");
                if (!prefab) return null;
                _kitRoot = Instantiate(prefab, transform).transform;
                _kitRoot.name = "facade kit (source)";
                _kitRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _kitRoot.localScale = Vector3.one;
                _kitRoot.gameObject.SetActive(false);
                foreach (Transform c in _kitRoot)
                {
                    var mf = c.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh) continue;
                    var mb = mf.sharedMesh.bounds;
                    var m = _kitRoot.worldToLocalMatrix * c.localToWorldMatrix;
                    var b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
                    for (int i = 0; i < 8; i++)
                        b.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
                    _kit[c.name] = new Module { src = c, b = b };
                }
            }
            return _kit.TryGetValue(name, out var mod) ? mod : null;
        }

        /// <summary>Copies a kit piece under `row`, shifting it so `anchor`'s bottom-left sits at (x, y).</summary>
        void Piece(Transform row, Module piece, Module anchor, float x, float y)
        {
            var go = Instantiate(piece.src.gameObject, row);
            go.SetActive(true);
            var t = go.transform;
            t.localRotation = piece.src.localRotation;
            t.localScale = piece.src.localScale;
            t.localPosition = piece.src.localPosition - new Vector3(anchor.b.min.x - x, anchor.b.min.y - y, anchor.b.max.z);
            var r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = piece.src.name.StartsWith("window_") || piece.src.name.StartsWith("door_") ? _glassMat : _wallMat;
        }

        /// <summary>One row of kit pieces across a face; returns the row height in kit meters.</summary>
        float Row(Transform face, float length, float y, string[] names, float scaleY)
        {
            var mods = new List<Module>();
            float width = 0;
            int guard = 0;
            while (width < length - 0.5f && guard++ < 12)
            {
                var m = Kit(names[_rng.Next(names.Length)]) ?? Kit(names[0]);
                if (m == null) return 0;
                mods.Add(m);
                width += m.b.size.x;
            }
            var row = new GameObject("row").transform;
            row.SetParent(face, false);
            row.localPosition = new Vector3(0, y, 0);
            row.localScale = new Vector3(length / width, scaleY, 1);
            float x = 0, height = 0;
            foreach (var m in mods)
            {
                Piece(row, m, m, x, 0);
                var insert = Kit(m.src.name.Substring(m.src.name.IndexOf('_') + 1));  // wall_window_X → window_X, dado_door_X → door_X
                if (insert != null) Piece(row, insert, m, x, 0);
                x += m.b.size.x;
                height = Mathf.Max(height, m.b.size.y);
            }
            return height * scaleY;
        }

        float CityWindowRow(Transform face, float length, float y, float scaleY, int style)
        {
            if (_cityWindows == null)
            {
                string[] names = { "S_G_000101_S", "S_G_000115_S", "S_G_000163_S" };
                _cityWindows = System.Array.ConvertAll(names, name =>
                {
                    var texture = Resources.Load<Texture2D>("Art/Zenrin/" + name);
                    if (!texture) return null;
                    var material = new Material(Resources.Load<Material>("Materials/Plain"));
                    material.SetTexture("_BaseMap", texture);
                    material.SetFloat("_Smoothness", 0.16f);
                    return material;
                });
                if (System.Array.Exists(_cityWindows, material => material != null))
                    Debug.Log("[art] ZENRIN facade textures active");
            }

            var surface = _cityWindows[style % _cityWindows.Length];
            var module = Kit(Storey[0]);
            if (!surface || module == null) return Row(face, length, y, Storey, scaleY);

            float height = module.b.size.y * scaleY;
            int panels = Mathf.Max(1, Mathf.RoundToInt(length / height));
            float width = length / panels;
            for (int i = 0; i < panels; i++)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "ZENRIN window bay";
                Destroy(panel.GetComponent<Collider>());
                panel.transform.SetParent(face, false);
                panel.transform.localPosition = new Vector3((i + 0.5f) * width, y + height * 0.5f, -0.015f);
                panel.transform.localScale = new Vector3(width, height, 1f);
                panel.GetComponent<Renderer>().sharedMaterial = surface;
            }
            return height;
        }

        /// <summary>A brick building with kit facades on all four sides. Returns its footprint bounds.</summary>
        Bounds KitBuilding(Vector2 c, float sizeX, float sizeZ, int storeys)
        {
            var root = new GameObject("building").transform;
            root.SetParent(transform);
            root.position = Fx.V3(c);
            var upper = new GameObject("upper storeys").transform;
            upper.SetParent(root, false);
            float groundTop = 0;
            const float squash = 0.82f;  // a little lower than the kit so the camera sees the street behind
            float top = 0;
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                float length = n.x != 0 ? sizeZ : sizeX;
                float depth = n.x != 0 ? sizeX : sizeZ;
                var face = new GameObject("face").transform;
                face.SetParent(root, false);
                var look = Quaternion.LookRotation(KitFrontIsNegZ ? -n : n);
                face.localRotation = look;
                var right = Vector3.Cross(Vector3.up, n);
                face.localPosition = n * depth * 0.5f - right * length * 0.5f * (KitFrontIsNegZ ? -1 : 1);
                var faceUp = new GameObject("face upper").transform;
                faceUp.SetParent(upper, false);
                faceUp.SetLocalPositionAndRotation(face.localPosition, face.localRotation);
                float y = 0;
                y += Row(face, length, y, new[] { "base_standard_01" }, squash);
                y += Row(face, length, y, Ground, squash);
                groundTop = y;
                int style = Mathf.Abs(Mathf.RoundToInt(c.x * 3 + c.y) + (n.x != 0 ? 1 : 0));
                for (int s = 0; s < storeys; s++)
                    y += CityWindowRow(faceUp, length, y, squash, style + s);
                y += Row(faceUp, length, y, new[] { "cornice_standard_standard_01" }, squash);
                Row(faceUp, length, y, new[] { "crown_standard_standard_01" }, squash);
                top = Mathf.Max(top, y);
            }
            // brick core and the green waterproofed roof
            var brick = Resources.Load<Material>("Materials/Brick");
            var core = Block(root.position + Vector3.up * groundTop * 0.5f, new Vector3(sizeX - 0.2f, groundTop, sizeZ - 0.2f), brick);
            core.transform.SetParent(root, true);
            var coreUp = Block(root.position + Vector3.up * (groundTop + top) * 0.5f, new Vector3(sizeX - 0.2f, top - groundTop, sizeZ - 0.2f), brick);
            coreUp.transform.SetParent(upper, true);
            var capMat = new Material(Resources.Load<Material>("Materials/WornFloor"));
            capMat.SetColor("_BaseColor", new Color(0.32f, 0.3f, 0.28f));
            var cap = Block(root.position + Vector3.up * (groundTop + 0.01f), new Vector3(sizeX - 0.25f, 0.04f, sizeZ - 0.25f), capMat);
            cap.transform.SetParent(root, true);
            var roofMat = new Material(Resources.Load<Material>("Materials/Concrete"));
            roofMat.SetColor("_BaseColor", Color.Lerp(new Color(0.26f, 0.5f, 0.36f), new Color(0.36f, 0.56f, 0.42f), (float)_rng.NextDouble()));
            var roof = Block(root.position + Vector3.up * (top + 0.02f), new Vector3(sizeX - 0.1f, 0.06f, sizeZ - 0.1f), roofMat);
            roof.transform.SetParent(upper, true);
            RooftopClutter(upper, new Vector3(c.x, top + 0.05f, c.y), sizeX, sizeZ);
            var bounds = new Bounds(root.position + Vector3.up * top * 0.5f, new Vector3(sizeX, top, sizeZ));
            _houses.Add(new House { bounds = bounds, upper = upper.gameObject });
            return bounds;
        }

        void RooftopClutter(Transform root, Vector3 at, float sx, float sz)
        {
            int units = _rng.Next(1, 4);
            for (int k = 0; k < units; k++)
                Haven("exterior_aircon_unit", at + new Vector3(R(-sx * 0.3f, sx * 0.3f), 0, R(-sz * 0.3f, sz * 0.3f)), 90 * _rng.Next(4), 1f, root);
            if (_rng.NextDouble() < 0.5)
                Haven("propane_tank", at + new Vector3(R(-sx * 0.3f, sx * 0.3f), 0, R(-sz * 0.3f, sz * 0.3f)), R(0, 360), 1f, root);
            if (_rng.NextDouble() < 0.4)
                Haven("plastic_monobloc_chair_01", at + new Vector3(R(-sx * 0.3f, sx * 0.3f), 0, R(-sz * 0.3f, sz * 0.3f)), R(0, 360), 1f, root);
        }
    }
}
