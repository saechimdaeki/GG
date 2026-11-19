using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastTrain
{
    /// <summary>
    /// Builds the old-town map: a grid of shop buildings (Kenney City Kit) with narrow alleys,
    /// utility poles and sagging wires, street lamps, dumpsters and roadwork clutter, puddles,
    /// the subway entrance, the moon/sun light and the post-processing volume.
    /// Every building footprint becomes a wall rect for the horde and the survivors.
    /// </summary>
    public partial class City : MonoBehaviour
    {
        public const float Half = 56f;
        public const float CellSize = 14f;
        public static readonly Vector2 Station = new(40, 40);

        public readonly List<Rect> Walls = new();
        /// <summary>What zombies collide with: the walls plus the shelter barricade while it stands.</summary>
        public readonly List<Rect> ZombieWalls = new();

        /// <summary>The survivors' shelter: an open-roofed shop with one door facing north.</summary>
        public static readonly Rect Shelter = new(0, -12, 8, 8);
        public static readonly Rect Door = new(3, -4.25f, 2, 0.5f);
        public static readonly Vector2 Bed = new(6.2f, -10.6f);
        public static readonly Vector2 Start = new(4, -7);
        public Transform Barricade;

        public class Container { public Vector2 pos; public bool searched; public Transform t; public string kind, table = "street", guaranteedItem; public Vector3 basePos; public List<(string item, int count)> loot; public bool parcel; public Rect? room; }

        public class Shop { public string name; public Vector2 pos; public Color color; }
        public readonly List<Shop> Shops = new();
        readonly List<Bounds> _buildings = new();

        /// <summary>Station hub: a survivor who trades, and a fuse box that powers the platform.</summary>
        public static readonly Vector2 Trader = new(36.6f, 43.2f);
        public static readonly Vector2 Panel = new(43.8f, 36.4f);
        public readonly List<Light> StationLights = new();
        public Renderer PanelLamp;
        public readonly List<Container> Containers = new();
        public readonly List<Light> StreetLights = new();
        public Light Sun;
        public Volume Volume;
        public ColorAdjustments Grade;
        public Vignette Vignette;
        public Renderer StationRing;

        static readonly Color[] NeonColors =
        {
            new(1f, 0.23f, 0.42f), new(0.23f, 0.82f, 1f), new(1f, 0.82f, 0.23f), new(0.49f, 1f, 0.42f), new(0.77f, 0.42f, 1f),
        };

        System.Random _rng;
        Material _neon;
        Light _shelterLamp;

        public void Build(int seed)
        {
            _rng = new System.Random(seed);
            _neon = Resources.Load<Material>("Materials/Neon");
            BuildLighting();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.localScale = Vector3.one * (Half * 2 + 30) / 10f;
            ground.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Materials/Ground");
            SetStreetSurface(ground.GetComponent<Renderer>());
            Destroy(ground.GetComponent<Collider>());
            var puddle = MakePuddleMaterial();
            var puddleMesh = MakePuddleMesh();
            for (int k = 0; k < 70; k++)
            {
                var p = new GameObject("Rain puddle");
                p.AddComponent<MeshFilter>().sharedMesh = puddleMesh;
                var puddleRenderer = p.AddComponent<MeshRenderer>();
                p.transform.SetParent(transform);
                p.transform.position = new Vector3(R(-Half, Half), 0.005f, R(-Half, Half));
                p.transform.localScale = new Vector3(R(1f, 3.5f), 0.005f, R(0.8f, 2.5f));
                p.transform.rotation = Quaternion.Euler(0, R(0, 180), 0);
                puddleRenderer.sharedMaterial = puddle;
                puddleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            float e = Half;
            AddWall(new Rect(-e - 2, -e - 2, e * 2 + 4, 2), 6);
            AddWall(new Rect(-e - 2, e, e * 2 + 4, 2), 6);
            AddWall(new Rect(-e - 2, -e, 2, e * 2), 6);
            AddWall(new Rect(e, -e, 2, e * 2), 6);

            int cells = Mathf.RoundToInt(Half * 2 / CellSize);
            for (int ix = 0; ix < cells; ix++)
            for (int iz = 0; iz < cells; iz++)
            {
                var c = new Vector2(-Half + CellSize * (ix + 0.5f), -Half + CellSize * (iz + 0.5f));
                if (Vector2.Distance(c, Station) < CellSize * 0.8f) continue;
                if (Shelter.Contains(c)) { BuildShelter(); continue; }
                bool openLot = _rng.NextDouble() < 0.12;
                if (BuildEscapeLocation(c) || BuildWalkInShop(c)) continue;
                if (c == MarketSquare || c == StationCourt) { BuildLandmark(c); continue; }
                if (openLot) { Plaza(c); continue; }
                PlaceBuilding(c);
            }
            for (int ix = 1; ix < cells; ix++)
            for (int iz = 1; iz < cells; iz++)
            {
                var at = new Vector2(-Half + CellSize * ix, -Half + CellSize * iz);
                double r = _rng.NextDouble();
                if (Vector2.Distance(at, Start) < 6f) continue;  // keep the shelter door clear
                if (r < 0.45) Pole(at, ix, iz);
                else if (r < 0.75) Lamp(at);
            }
            DressStreet(cells);
            StreetClutter(cells);
            BuildStation();
            DressStation(Resources.Load<Material>("Materials/Concrete"));
            BuildShops();
            ZombieWalls.AddRange(Walls);
        }

        void BuildShelter()
        {
            var concrete = Resources.Load<Material>("Materials/Concrete");
            var r = Shelter;
            Block(new Vector3(r.center.x, 0.02f, r.center.y), new Vector3(r.width, 0.04f, r.height), Resources.Load<Material>("Materials/WornFloor"));
            const float th = 0.4f, fullHeight = 2.4f, sill = 0.8f;
            void Wall(Rect w, float h = fullHeight)
            {
                Walls.Add(w);
                var size = new Vector3(w.width, h, w.height);
                var center = new Vector3(w.center.x, h / 2, w.center.y);
                Block(center, size, ShelterTile(concrete, Mathf.Max(w.width, w.height)));
                Block(center + Vector3.up * (h / 2 + 0.04f), new Vector3(w.width + 0.06f, 0.12f, w.height + 0.06f), ShelterCap(concrete));
            }
            Wall(new Rect(r.xMin, r.yMin, r.width, th), sill);                        // camera-facing south wall
            Wall(new Rect(r.xMin, r.yMin, th, r.height));                              // west
            Wall(new Rect(r.xMax - th, r.yMin, th, r.height), sill);                   // camera-facing east wall
            Wall(new Rect(r.xMin, r.yMax - th, Door.xMin - r.xMin, th));               // north, left of the door
            Wall(new Rect(Door.xMax, r.yMax - th, r.xMax - Door.xMax, th));            // north, right of the door
            // the barricade: planks across the door (scaled down as it takes damage)
            Barricade = new GameObject("barricade").transform;
            Barricade.SetParent(transform);
            Barricade.position = new Vector3(Door.center.x, 0, Door.center.y);
            for (int k = 0; k < 4; k++)
            {
                var plank = Block(Barricade.position + new Vector3(0, 0.35f + k * 0.42f, 0), new Vector3(2.3f, 0.22f, 0.12f), Resources.Load<Material>("Materials/Planks"));
                plank.transform.SetParent(Barricade, true);
                plank.transform.rotation = Quaternion.Euler(0, 0, (k % 2 == 0 ? 8 : -8));
            }
            // Preserve the corner posts so the cut wall still reads as part of the building.
            var cap = ShelterCap(concrete);
            foreach (Vector2 corner in new[] { new Vector2(r.xMin + th / 2, r.yMin + th / 2),
                new Vector2(r.xMax - th / 2, r.yMin + th / 2), new Vector2(r.xMax - th / 2, r.yMax - th / 2) })
                Block(Fx.V3(corner, 1.2f), new Vector3(0.46f, 2.4f, 0.46f), cap);
            Fit(FieldProp("hospital_bed", "bedSingle"), Bed, 2.15f, 0.95f, 0);
            Fit(Haven("wooden_bookshelf_worn", Vector3.zero, 0), new Vector2(1.06f, -9.32f), 1.35f, 1.95f, 90);
            Haven("hanging_industrial_lamp", new Vector3(r.center.x, 2.35f, r.center.y), 0);
            Haven("wooden_crate_01", new Vector3(5.6f, 0, -8.6f), 20);
            Haven("metal_jerrycan", new Vector3(6.3f, 0, -8.3f), 70);
            Haven("cardboard_box_01", new Vector3(0.9f, 0, -11.2f), 15);
            var l = new GameObject("shelter-lamp").AddComponent<Light>();
            l.transform.SetParent(transform);
            l.transform.position = new Vector3(r.center.x, 2.2f, r.center.y);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.4f);
            l.range = 7;
            l.intensity = 2.5f;
            _shelterLamp = l;
            DressShelter(concrete);
        }

        /// <summary>
        /// Turns some buildings into shops (convenience store, hardware store, pharmacy): a big
        /// colored sign and light, and a row of shelves out front with that shop's loot table.
        /// </summary>
        void BuildShops()
        {
            var free = new List<Bounds>(_buildings);
            free.RemoveAll(b => Vector2.Distance(new Vector2(b.center.x, b.center.z), Start) < 14 || Vector2.Distance(new Vector2(b.center.x, b.center.z), Station) < 14);
            foreach (var cfg in Tuning.Data.shops)
            for (int n = 0; n < cfg.count && free.Count > 0; n++)
            {
                int pick = _rng.Next(free.Count);
                var b = free[pick];
                free.RemoveAt(pick);
                ColorUtility.TryParseHtmlString(cfg.color, out var col);
                int side = _rng.Next(4);
                Vector3 nrm = side switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.left, _ => Vector3.right };
                var along = new Vector3(Mathf.Abs(nrm.z), 0, Mathf.Abs(nrm.x));
                var front = b.center + Vector3.Scale(nrm, b.extents);
                // the sign board over the entrance
                var signCenter = new Vector3(front.x, 3.2f, front.z) + nrm * 0.25f;
                Block(signCenter, along * 4.2f + new Vector3(along.z, 0, along.x) * 0.2f + Vector3.up * 0.98f, Resources.Load<Material>("Materials/Concrete"));
                SignFace(cfg.type, signCenter + nrm * 0.11f, nrm, 4.16f, 0.96f);
                var l = new GameObject(cfg.name).AddComponent<Light>();
                l.transform.SetParent(transform);
                l.transform.position = new Vector3(front.x, 3f, front.z) + nrm * 1.6f;
                l.type = LightType.Point;
                l.color = col;
                l.range = 9;
                l.intensity = 3;
                StreetLights.Add(l);
                Shops.Add(new Shop { name = cfg.name, pos = new Vector2(front.x + nrm.x * 2.4f, front.z + nrm.z * 2.4f), color = col });
                for (int k = 0; k < cfg.shelves; k++)
                {
                    float off = (k - (cfg.shelves - 1) * 0.5f) * 1.5f;
                    var at3 = front + nrm * 0.7f + along * off;
                    var at = new Vector2(at3.x, at3.z);
                    var shelf = new GameObject("shelf").transform;
                    shelf.SetParent(transform);
                    shelf.position = new Vector3(at.x, 0, at.y);
                    float shelfYaw = Quaternion.LookRotation(nrm).eulerAngles.y;
                    Haven(_rng.NextDouble() < 0.5 ? "steel_frame_shelves_01" : "steel_frame_shelves_02", shelf.position, shelfYaw, 1f, shelf);
                    Haven("cardboard_box_01", shelf.position + along * R(-0.3f, 0.3f) + nrm * 0.1f, shelfYaw + R(-20, 20), 0.8f, shelf);
                    Walls.Add(new Rect(at.x - (along.x * 0.6f + Mathf.Abs(nrm.x) * 0.25f), at.y - (along.z * 0.6f + Mathf.Abs(nrm.z) * 0.25f),
                        along.x * 1.2f + Mathf.Abs(nrm.x) * 0.5f, along.z * 1.2f + Mathf.Abs(nrm.z) * 0.5f));
                    Containers.Add(new Container { pos = at, t = shelf, kind = cfg.name + " 진열대", table = cfg.type, basePos = shelf.position });
                }
            }
        }

        /// <summary>A searched container goes dark so you can tell what is left to check.</summary>
        public void MarkSearched(Container c)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.22f));
            foreach (var r in c.t.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpb);
            c.t.position = c.basePos;
        }

        /// <summary>Puts a searchable dumpster or box pile against a building wall, in the alley.</summary>
        void AddContainer(Bounds b)
        {
            int side = _rng.Next(4);
            Vector3 n = side switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.left, _ => Vector3.right };
            var along = new Vector3(Mathf.Abs(n.z), 0, Mathf.Abs(n.x));
            var at3 = b.center + Vector3.Scale(n, b.extents) + n * 0.75f + along * R(-b.extents.x * 0.5f, b.extents.x * 0.5f);
            var at = new Vector2(at3.x, at3.z);
            if (Shelter.Contains(at) || at.magnitude < 3) return;
            bool dumpster = _rng.NextDouble() < 0.45;
            float yaw = Quaternion.LookRotation(n).eulerAngles.y;
            Transform t = dumpster ? TrashPile(at, yaw) : BoxPile(at, yaw);
            var cb = BoundsOf(t.gameObject);  // you walk around a dumpster, not through it
            Walls.Add(new Rect(cb.min.x, cb.min.z, cb.size.x, cb.size.z));
            Containers.Add(new Container { pos = at, t = t, kind = dumpster ? "쓰레기통" : "상자", basePos = t.position });
        }

        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        T Pick<T>(IList<T> list) => list[_rng.Next(list.Count)];

        void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform);
            sunGo.transform.rotation = Quaternion.Euler(58, 35, 0);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.66f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 28;
            RenderSettings.fogEndDistance = 75;

            var vgo = new GameObject("Post");
            vgo.transform.SetParent(transform);
            Volume = vgo.AddComponent<Volume>();
            Volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.72f);
            Grade = profile.Add<ColorAdjustments>(true);
            Grade.contrast.Override(18f);
            Grade.saturation.Override(-8f);
            Grade.postExposure.Override(0.2f);
            Vignette = profile.Add<Vignette>(true);
            Vignette.intensity.Override(0.25f);
            Vignette.smoothness.Override(0.5f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.04f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.9f, 0.95f, 1.1f, 0f));
            Volume.profile = profile;
        }

        GameObject Model(string path, Transform parent = null)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("missing model " + path); return new GameObject(path); }
            var go = Instantiate(prefab, parent ? parent : transform);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            return go;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Scales a model so its footprint fits size×size (height capped), centered at c.</summary>
        GameObject Fit(GameObject go, Vector2 c, float size, float maxHeight, float rotY)
        {
            go.transform.rotation = Quaternion.Euler(0, rotY, 0) * go.transform.rotation;
            var b = BoundsOf(go);
            float s = size / Mathf.Max(b.size.x, b.size.z);
            float sy = b.size.y * s > maxHeight ? maxHeight / b.size.y : s;
            go.transform.localScale = new Vector3(s, sy, s);
            b = BoundsOf(go);
            go.transform.position += new Vector3(c.x - b.center.x, -b.min.y, c.y - b.center.z);
            return go;
        }

        void PlaceBuilding(Vector2 c)
        {
            float size = R(7.4f, 8.4f);
            var b = KitBuilding(c, size, size * R(0.82f, 1f), 1);
            Walls.Add(new Rect(b.min.x, b.min.z, b.size.x, b.size.z));
            _buildings.Add(b);
            DressFacade(b, Pick(new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }));
            _pavement.Add(new Rect(b.min.x - 0.65f, b.min.z - 0.65f, b.size.x + 1.3f, b.size.z + 1.3f));
            if (_rng.NextDouble() < 0.75) AddContainer(b);
            if (_rng.NextDouble() < 0.3) AddContainer(b);
            int signs = _rng.Next(1, 3);
            for (int k = 0; k < signs; k++) Sign(b);
        }

        /// <summary>A vertical neon shop sign sticking out of a wall, with a little colored light.</summary>
        void Sign(Bounds b)
        {
            int side = _rng.Next(4);
            Vector3 n = side switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.left, _ => Vector3.right };
            var along = new Vector3(Mathf.Abs(n.z), 0, Mathf.Abs(n.x));
            var face = b.center + Vector3.Scale(n, b.extents) + n * 0.45f;
            face += along * R(-b.extents.x * 0.6f, b.extents.x * 0.6f);
            float h = Mathf.Min(R(2.6f, 5.5f), b.size.y - 1f);
            var col = Pick(NeonColors);
            float tall = R(1.6f, 2.6f);
            var sign = Block(new Vector3(face.x, h, face.z), along * 1.72f + new Vector3(along.z, 0, along.x) * 0.12f + Vector3.up * 0.47f, _neon);
            // Set the board flush against the facade. The variation follows the city seed.
            sign.transform.position -= n * 0.37f;
            Paint(sign, new Color(0.10f, 0.11f, 0.11f));
            string key = tall < 1.93f ? "print" : tall < 2.26f ? "laundry" : "barber";
            SignFace(key, sign.transform.position + n * 0.061f, n, 1.71f, 0.46f);
            if (_rng.NextDouble() < 0.5)
            {
                var l = new GameObject("neon").AddComponent<Light>();
                l.transform.SetParent(transform);
                l.transform.position = sign.transform.position + n * 0.8f;
                l.type = LightType.Point;
                l.color = col;
                l.range = 5f;
                l.intensity = 2.5f;
                l.shadows = LightShadows.None;
                StreetLights.Add(l);
            }
        }

        void Plaza(Vector2 c)
        {
            var lot = Block(Fx.V3(c, 0.02f), new Vector3(CellSize - 3f, 0.04f, CellSize - 3f), Resources.Load<Material>("Materials/WornFloor"));
            lot.name = "Empty lot";
            int n = _rng.Next(3, 6);
            for (int k = 0; k < n; k++)
            {
                var id = k == 0 && _rng.NextDouble() < 0.6 ? "covered_car" : Pick(LotProps);
                var p = c + new Vector2(R(-3.2f, 3.2f), R(-3.2f, 3.2f));
                if (p.magnitude < 3f) continue;
                var go = Haven(id, Fx.V3(p), id == "covered_car" ? 90 * _rng.Next(4) + R(-8, 8) : R(0, 360));
                if (id is "covered_car" or "concrete_road_barrier" or "portable_generator" or "wooden_military_crate") Solid(go);
            }
        }

        /// <summary>A utility pole with wires sagging to the next corner — the old-town alley look.</summary>
        void Pole(Vector2 at, int ix, int iz)
        {
            Fit(Model("Art/Roads/electricity-pole"), at + new Vector2(1.6f, 1.6f), 1.0f, 7.5f, 0);
            for (int k = 0; k < 2; k++)
            {
                bool east = k == 0;
                var a = new Vector3(at.x + 1.6f, 6.6f, at.y + 1.6f);
                var b = a + (east ? Vector3.right : Vector3.forward) * CellSize;
                var line = new GameObject("wire").AddComponent<LineRenderer>();
                line.transform.SetParent(transform);
                line.positionCount = 9;
                for (int i = 0; i < 9; i++)
                {
                    float t = i / 8f;
                    line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.9f);
                }
                line.widthMultiplier = 0.05f;
                line.sharedMaterial = WireMaterial;
                line.startColor = line.endColor = Color.black;
                line.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        Material _wire;
        Material WireMaterial
        {
            get
            {
                if (_wire) return _wire;
                _wire = new Material(Resources.Load<Material>("Materials/Plain"));
                _wire.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.03f));
                return _wire;
            }
        }

        void Lamp(Vector2 at)
        {
            var go = Haven("street_lamp_01", Fx.V3(at + new Vector2(1.7f, -1.7f)), 90 * _rng.Next(4));
            var b = BoundsOf(go);
            var l = new GameObject("lamp").AddComponent<Light>();
            l.transform.SetParent(transform);
            l.transform.position = new Vector3(b.center.x, b.max.y - 0.35f, b.center.z);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.45f);
            l.range = 11f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            StreetLights.Add(l);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(transform);
            bulb.transform.position = l.transform.position + Vector3.up * 0.1f;
            bulb.transform.localScale = Vector3.one * 0.18f;
            bulb.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Materials/Bulb");
        }

        void BuildStation()
        {
            var concrete = Resources.Load<Material>("Materials/Concrete");
            var green = new Material(Resources.Load<Material>("Materials/Shutter"));
            green.SetColor("_BaseColor", new Color(0.3f, 0.55f, 0.45f));
            var s = Station;
            Block(new Vector3(s.x, 0.08f, s.y), new Vector3(8, 0.16f, 8), concrete);
            // stairwell going down: dark hole, side walls and a green canopy
            Paint(Block(new Vector3(s.x + 3.2f, 0.17f, s.y), new Vector3(2.6f, 0.02f, 4f), null), new Color(0.02f, 0.02f, 0.025f));
            Block(new Vector3(s.x + 1.8f, 0.6f, s.y), new Vector3(0.25f, 1.2f, 4.2f), concrete);
            Block(new Vector3(s.x + 4.6f, 0.6f, s.y), new Vector3(0.25f, 1.2f, 4.2f), concrete);
            Block(new Vector3(s.x + 3.2f, 3.0f, s.y), new Vector3(3.2f, 0.25f, 4.6f), green);
            Block(new Vector3(s.x + 1.8f, 1.5f, s.y - 2.1f), new Vector3(0.2f, 3f, 0.2f), concrete);
            Block(new Vector3(s.x + 1.8f, 1.5f, s.y + 2.1f), new Vector3(0.2f, 3f, 0.2f), concrete);
            var post = Block(new Vector3(s.x - 2.5f, 1.6f, s.y - 2.5f), new Vector3(0.18f, 3.2f, 0.18f), green);
            var board = Block(new Vector3(s.x - 2.5f, 3.3f, s.y - 2.5f), new Vector3(1.1f, 0.5f, 0.12f), green);
            board.transform.SetParent(post.transform, true);
            SignFace("station", new Vector3(s.x - 2.5f, 3.3f, s.y - 2.57f), Vector3.back, 1.05f, 0.46f);
            Haven("flood_lights", new Vector3(s.x - 3.2f, 0.16f, s.y + 3f), 135);
            Haven("portable_generator", new Vector3(s.x - 2.2f, 0.16f, s.y + 3.1f), 160);
            Haven("concrete_road_barrier", new Vector3(s.x - 0.5f, 0.16f, s.y - 3.6f), 0);
            Haven("plastic_crate_01", new Vector3(s.x - 3.4f, 0.16f, s.y - 1.2f), 30);
            var l = new GameObject("station").AddComponent<Light>();
            l.transform.SetParent(transform);
            l.transform.position = new Vector3(s.x, 3.5f, s.y);
            l.type = LightType.Point;
            l.color = new Color(0.4f, 1f, 0.6f);
            l.range = 12;
            l.intensity = 3;
            StreetLights.Add(l);
            Walls.Add(new Rect(s.x + 1.7f, s.y - 2.1f, 3f, 4.2f));  // the stairwell itself is not walkable

            // the station office: a counter where a survivor trades, behind a fence
            Haven("metal_office_desk", new Vector3(Trader.x, 0, Trader.y - 1.1f), 180);
            Haven("CashRegister_01", new Vector3(Trader.x + 0.6f, 0.75f, Trader.y - 1.1f), 180);
            Haven("steel_frame_shelves_02", new Vector3(Trader.x - 1.1f, 0, Trader.y + 0.5f), 180);
            Haven("cardboard_box_01", new Vector3(Trader.x + 1.0f, 0, Trader.y + 0.4f), 10);
            Walls.Add(new Rect(Trader.x - 1.3f, Trader.y - 1.4f, 2.6f, 0.6f));
            Walls.Add(new Rect(Trader.x - 1.6f, Trader.y - 0.8f, 3.2f, 2.2f));  // behind the counter is his
            var sign = Block(new Vector3(Trader.x, 2.2f, Trader.y + 0.9f), new Vector3(2.4f, 0.6f, 0.12f), _neon);  // "역무실" board behind him
            var smpb = new MaterialPropertyBlock();
            smpb.SetColor("_BaseColor", new Color(0.3f, 0.6f, 1f));
            smpb.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 2f);
            sign.GetComponent<Renderer>().SetPropertyBlock(smpb);
            var tl = new GameObject("trader-lamp").AddComponent<Light>();
            tl.transform.SetParent(transform);
            tl.transform.position = new Vector3(Trader.x, 2.3f, Trader.y - 1.2f);
            tl.type = LightType.Point;
            tl.color = new Color(1f, 0.85f, 0.6f);
            tl.range = 6;
            tl.intensity = 2.5f;
            StreetLights.Add(tl);

            // the fuse box that powers the platform lights and shutters
            Haven("utility_box_02", new Vector3(Panel.x, 0, Panel.y), 180);
            Walls.Add(new Rect(Panel.x - 0.45f, Panel.y - 0.25f, 0.9f, 0.5f));
            var lamp = Block(new Vector3(Panel.x, 1.95f, Panel.y), Vector3.one * 0.22f, _neon);
            PanelLamp = lamp.GetComponent<Renderer>();
            for (int k = 0; k < 4; k++)
            {
                var sl = new GameObject("platform-light").AddComponent<Light>();
                sl.transform.SetParent(transform);
                sl.transform.position = new Vector3(s.x + (k % 2 == 0 ? -3 : 3), 4f, s.y + (k < 2 ? -3 : 3));
                sl.type = LightType.Point;
                sl.color = new Color(0.85f, 0.95f, 1f);
                sl.range = 10;
                sl.intensity = 4;
                sl.enabled = false;
                StationLights.Add(sl);
            }

            var ring = new GameObject("evac-ring");
            ring.transform.SetParent(transform);
            ring.transform.position = new Vector3(s.x, 0.2f, s.y);
            ring.transform.localScale = Vector3.one * Tuning.Data.clock.evacRadius;
            ring.AddComponent<MeshFilter>().sharedMesh = Fx.BuildRing(0.93f, 1f, 72);
            StationRing = ring.AddComponent<MeshRenderer>();
            StationRing.sharedMaterial = new Material(Resources.Load<Material>("Materials/FxAdd"));
            post.name = "station-sign";
        }

        /// <summary>Concrete-based material in a given color (primitives' default material is not in the build).</summary>
        public static void Paint(GameObject go, Color c)
        {
            var m = new Material(Resources.Load<Material>("Materials/Concrete"));
            m.SetColor("_BaseColor", c);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        GameObject Block(Vector3 at, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform);
            go.transform.position = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m ? m : Resources.Load<Material>("Materials/Concrete");
            return go;
        }

        void AddWall(Rect r, float h)
        {
            Walls.Add(r);
            Block(new Vector3(r.center.x, h / 2, r.center.y), new Vector3(r.width, h, r.height), Resources.Load<Material>("Materials/Concrete"));
        }

        /// <summary>Day (0) to night (1): moonlight, ambient, fog, lamps, grade.</summary>
        public void SetDarkness(float k)
        {
            Sun.intensity = Mathf.Lerp(2.2f, 0.6f, k);
            Sun.color = Color.Lerp(new Color(1f, 0.93f, 0.82f), new Color(0.55f, 0.66f, 0.72f), k);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.78f, 0.82f, 0.88f), new Color(0.22f, 0.28f, 0.3f), k);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.62f, 0.62f, 0.62f), new Color(0.14f, 0.17f, 0.18f), k);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.42f, 0.4f, 0.38f), new Color(0.1f, 0.1f, 0.14f), k);
            RenderSettings.fogColor = Color.Lerp(new Color(0.58f, 0.6f, 0.6f), new Color(0.04f, 0.055f, 0.06f), k);
            if (_shelterLamp) _shelterLamp.intensity = Mathf.Lerp(1.3f, 7.0f, k);
            foreach (var l in StreetLights) l.enabled = k > 0.15f || l.color.g < 0.9f;
            float t = Time.time;
            for (int i = 0; i < StreetLights.Count; i++)
            {
                var l = StreetLights[i];
                float on = Mathf.Lerp(0.6f, l.range > 10 ? 4f : 3f, k);
                // every fifth lamp is dying: it stutters off now and then
                if (i % 5 == 2 && Mathf.PerlinNoise(i * 3.7f, t * 2.5f) > 0.62f) on *= Mathf.PerlinNoise(t * 30f, i) < 0.5f ? 0.05f : 0.6f;
                l.intensity = on;
            }
            Grade.postExposure.value = Mathf.Lerp(0.55f, 0.85f, k);
            Grade.colorFilter.value = Color.Lerp(new Color(0.98f, 0.97f, 0.94f), new Color(0.86f, 0.97f, 0.95f), k);
            Grade.saturation.value = Mathf.Lerp(2f, -20f, k);
            Grade.contrast.value = Mathf.Lerp(16f, 18f, k);
        }
    }
}
