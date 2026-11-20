using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTrain
{
    public partial class City
    {
        readonly Dictionary<string, Material> _signFaces = new();
        readonly List<Rect> _pavement = new();
        Material _streetPaint, _shelterCap;
        Light _entranceLamp;
        Renderer _breakerIndicator;
        internal static readonly Vector2 MarketSquare = new(21f, 7f);
        internal static readonly Vector2 StationCourt = new(35f, 21f);
        public static readonly Vector2 ShelterBreaker = new(0.65f, -6.45f);

        static Material ShelterTile(Material concrete, float span) => Resources.Load<Material>("Materials/FacadeTile");

        Material ShelterCap(Material concrete)
        {
            if (_shelterCap) return _shelterCap;
            _shelterCap = new Material(concrete);
            _shelterCap.SetColor("_BaseColor", new Color(0.24f, 0.28f, 0.26f));
            return _shelterCap;
        }

        public void SetShelterPower(bool on)
        {
            if (_shelterLamp) _shelterLamp.enabled = on;
            if (_entranceLamp) _entranceLamp.enabled = on;
            if (!_breakerIndicator) return;
            var block = new MaterialPropertyBlock();
            var light = on ? new Color(0.25f, 0.8f, 0.55f) : new Color(1f, 0.20f, 0.07f);
            block.SetColor("_BaseColor", light);
            block.SetColor("_EmissionColor", light * 1.4f);
            _breakerIndicator.SetPropertyBlock(block);
        }

        static Material MakePuddleMaterial()
        {
            var material = new Material(Resources.Load<Material>("Materials/Puddle"));
            material.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.20f));
            material.SetFloat("_Metallic", 0.06f);
            return material;
        }

        static Mesh MakePuddleMesh()
        {
            const int segments = 24;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float radius = 0.46f + Mathf.Sin(3 * angle) * 0.063f + Mathf.Cos(5 * angle) * 0.025f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Irregular rain puddle" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        void SetStreetSurface(Renderer renderer)
        {
            renderer.sharedMaterial = Resources.Load<Material>("Materials/Ground");
        }

        void DressStreet(int cells)
        {
            var concrete = Resources.Load<Material>("Materials/Concrete");
            var pavement = Resources.Load<Material>("Materials/Sidewalk");
            _streetPaint = new Material(concrete);
            _streetPaint.SetColor("_BaseColor", new Color(0.66f, 0.57f, 0.37f));

            var paving = new SurfaceBatch();
            foreach (var pad in _pavement)
                paving.Add(pad.center, pad.size, 0.024f);
            paving.Create("Sidewalks", transform, pavement);

            var marks = new SurfaceBatch();
            for (int lane = 1; lane < cells; lane++)
            {
                float street = -Half + lane * CellSize;
                for (int block = 0; block < cells; block++)
                {
                    float center = -Half + (block + 0.5f) * CellSize;
                    var north = new Vector2(street, center);
                    var east = new Vector2(center, street);
                    if (street != 28f && !SouthChannelContains(north) && Vector2.Distance(north, Station) > 7f && !Shelter.Contains(north))
                        marks.Add(north, new Vector2(0.10f, 2.1f), 0.018f);
                    if (!(center == 49f && street > 0f) && !(street == 28f && center < -14f) && !(street == 14f && center > 0f && center < 28f) && street != 0f && !SouthChannelContains(east) && Vector2.Distance(east, Station) > 7f && !Shelter.Contains(east))
                        marks.Add(east, new Vector2(2.1f, 0.10f), 0.018f);
                }
            }
            marks.Create("Faded lane markings", transform, _streetPaint);

            var crosswalks = new SurfaceBatch();
            foreach (var intersection in new[] { new Vector2(14, 0), new Vector2(28, 28), new Vector2(42, 28) })
            {
                for (int stripe = -2; stripe <= 2; stripe++)
                {
                    crosswalks.Add(intersection + new Vector2(stripe * 0.47f, 0), new Vector2(0.30f, 2.65f), 0.021f);
                    crosswalks.Add(intersection + new Vector2(0, 3.1f + stripe * 0.47f), new Vector2(2.65f, 0.30f), 0.021f);
                }
            }
            var linePaint = new Material(concrete);
            linePaint.SetColor("_BaseColor", new Color(0.65f, 0.67f, 0.63f));
            crosswalks.Create("Crosswalk paint", transform, linePaint);
        }

        void BuildBoulevards()
        {
            var curbs = new SurfaceBatch();
            var edges = new SurfaceBatch();
            var medians = new SurfaceBatch();
            for (int block = -3; block <= 3; block++)
            {
                float middle = (block + 0.5f) * CellSize;
                foreach (int side in new[] { -1, 1 })
                {
                    curbs.Add(new Vector2(middle, side * 3.02f), new Vector2(9.7f, 0.7f), 0.023f);
                    edges.Add(new Vector2(middle, side * 2.61f), new Vector2(8.6f, 0.10f), 0.020f);
                    medians.Add(new Vector2(middle, side * 0.17f), new Vector2(7.1f, 0.09f), 0.019f);
                    if (middle > 29f) continue;
                    curbs.Add(new Vector2(28 + side * 3.10f, middle), new Vector2(0.82f, 9.7f), 0.023f);
                    edges.Add(new Vector2(28 + side * 2.58f, middle), new Vector2(0.10f, 8.6f), 0.020f);
                    medians.Add(new Vector2(28 + side * 0.17f, middle), new Vector2(0.09f, 7.1f), 0.019f);
                }
            }
            curbs.Create("Boulevard curb paving", transform, Resources.Load<Material>("Materials/Sidewalk"));
            edges.Create("Boulevard white edge lines", transform, SiteMaterial(new Color(0.62f, 0.64f, 0.59f)));
            medians.Create("Boulevard center markings", transform, _streetPaint);
        }

        void BuildStationApproach()
        {
            // Continue the service court into the station forecourt, leaving the panel and contact accessible.
            var center = new Vector2(36.8f, 32.5f);
            var paving = new SurfaceBatch();
            paving.Add(center, new Vector2(10.4f, 15.6f), 0.026f);
            paving.Create("Station approach paving", transform, Resources.Load<Material>("Materials/Sidewalk"));

            var guidance = new SurfaceBatch();
            for (int tile = 0; tile < 16; tile++)
                guidance.Add(new Vector2(38.4f, 25.1f + tile * 0.86f), new Vector2(0.38f, 0.68f), 0.029f);
            guidance.Create("Station tactile path", transform, _streetPaint);

            var concrete = Resources.Load<Material>("Materials/Concrete");
            SiteBarrier(new Vector2(32.3f, 30.1f), new Vector2(0.52f, 4.2f), 0.57f, concrete);
            SiteBarrier(new Vector2(41.1f, 29.6f), new Vector2(0.52f, 3.2f), 0.57f, concrete);
            SiteProp("wooden_picnic_table", new Vector2(33.3f, 36.8f), 2.0f, 0.86f, 0);
            SiteLamp(new Vector2(33.1f, 30.4f), 2.8f, new Color(0.53f, 0.84f, 0.79f));
        }

        void BuildLandmark(Vector2 center)
        {
            bool market = center == MarketSquare;
            var paving = new SurfaceBatch();
            var tile = new Material(Resources.Load<Material>(market ? "Materials/Brick" : "Materials/Sidewalk"));
            tile.SetFloat("_Tile", market ? 1.6f : 2.2f);
            for (int x = -4; x <= 4; x++)
            for (int z = -4; z <= 4; z++)
                paving.Add(center + new Vector2(x, z), Vector2.one * 0.97f, 0.032f);
            paving.Create(market ? "Market brick forecourt" : "Station service court", transform, tile);

            if (market)
            {
                var aisle = new SurfaceBatch();
                aisle.Add(center, new Vector2(2.65f, 8.7f), 0.038f);
                var aisleMaterial = new Material(tile);
                aisleMaterial.SetColor("_BaseColor", new Color(0.62f, 0.51f, 0.38f));
                aisle.Create("Market central aisle", transform, aisleMaterial);

                foreach (int side in new[] { -1, 1 })
                {
                    for (int row = -1; row <= 1; row += 2)
                        BuildMarketStall(center + new Vector2(side * 3.1f, row * 2.3f), side, row);
                }

                // A low directory leaves the central aisle open at both ends.
                var directory = Fx.V3(center + new Vector2(-3.15f, -4.65f), 0.84f);
                var casing = Block(directory, new Vector3(2.6f, 0.95f, 0.22f), _neon);
                Paint(casing, new Color(0.15f, 0.20f, 0.17f));
                SignFace("market", directory + Vector3.back * 0.112f, Vector3.back, 2.58f, 0.68f);
                Walls.Add(new Rect(directory.x - 1.3f, directory.z - 0.11f, 2.6f, 0.22f));
            }
            else
            {
                var paint = new SurfaceBatch();
                for (int bay = -1; bay <= 1; bay++)
                    paint.Add(center + new Vector2(2.2f * bay, 0), new Vector2(0.085f, 4.6f), 0.037f);
                paint.Create("Service bay markings", transform, Resources.Load<Material>("Materials/Concrete"));
                Fit(Model("Art/Roads/construction-cone"), center + new Vector2(-2.2f, -2.5f), 0.48f, 0.8f, 0);
                Fit(Model("Art/Roads/construction-cone"), center + new Vector2(2.2f, -2.5f), 0.48f, 0.8f, 0);
            }
        }

        void BuildMarketStall(Vector2 at, int side, int row)
        {
            var stand = new GameObject("Market stall").transform;
            stand.SetParent(transform, false);
            stand.position = Fx.V3(at);
            float yaw = side < 0 ? 90 : -90;
            var table = Haven("wooden_picnic_table", Fx.V3(at), yaw, 1f, stand);
            float top = BoundsOf(table).max.y;
            for (int crate = -1; crate <= 1; crate++)
                Haven(crate == 0 ? "plastic_crate_03" : "plastic_crate_01", Fx.V3(at + new Vector2(0, crate * 0.62f), top), yaw + R(-12, 12), 0.9f, stand);
            if (row > 0) Haven("wooden_military_crate", Fx.V3(at + new Vector2(side * 0.9f, R(-0.6f, 0.6f))), R(0, 360), 1f, stand);
            else Haven("cardboard_box_01", Fx.V3(at + new Vector2(side * 0.85f, R(-0.6f, 0.6f))), R(0, 360), 1f, stand);
            Walls.Add(new Rect(at.x - 0.675f, at.y - 1.15f, 1.35f, 2.3f));

            if (row < 0)
                Containers.Add(new Container { pos = at + Vector2.left * (side * 0.75f), t = stand,
                    kind = "시장 좌판", table = "store", basePos = stand.position });
        }

        void SignFace(string key, Vector3 at, Vector3 normal, float width, float height, Transform parent = null)
        {
            if (!_signFaces.TryGetValue(key, out var material))
            {
                var texture = Resources.Load<Texture2D>("Art/Street/" + key);
                if (!texture) return;
                material = new Material(_neon);
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_EmissionMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_EmissionColor", Color.white * 0.42f);
                material.SetFloat("_Smoothness", 0.33f);
                _signFaces.Add(key, material);
            }
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(face.GetComponent<Collider>());
            face.name = key + " sign face";
            face.transform.SetParent(transform);
            face.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-normal, Vector3.up));
            face.transform.localScale = new Vector3(width, height, 1);
            if (parent) face.transform.SetParent(parent, true);
            var renderer = face.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void DressShelter(Material concrete)
        {
            var planks = new SurfaceBatch();
            var wood = Resources.Load<Material>("Materials/Planks");
            for (int row = 0; row < 18; row++)
            for (int panel = 0; panel < 3; panel++)
            {
                float x = Shelter.xMin + 0.41f + (panel + 0.5f) * 2.39f;
                float z = Shelter.yMin + 0.42f + (row + 0.5f) * 0.398f;
                planks.Add(new Vector2(x, z), new Vector2(2.37f, 0.381f), 0.042f);
            }
            planks.Create("Shelter floorboards", transform, wood);
            var lintel = Block(new Vector3(1.55f, 2.24f, Shelter.yMax + 0.01f), new Vector3(2.5f, 0.64f, 0.14f), concrete);
            Paint(lintel, new Color(0.17f, 0.13f, 0.10f));
            SignFace("shelter", new Vector3(1.55f, 2.24f, Shelter.yMax + 0.085f), Vector3.forward, 2.46f, 0.61f);
            var entrance = _entranceLamp = new GameObject("Shelter entrance lamp").AddComponent<Light>();
            entrance.transform.SetParent(transform, false);
            entrance.transform.position = new Vector3(Door.center.x, 2.2f, Shelter.yMax + 0.62f);
            entrance.type = LightType.Point;
            entrance.color = new Color(1f, 0.72f, 0.43f);
            entrance.range = 8f;
            entrance.intensity = 3.8f;
            var cabinet = Haven("power_box_01", Fx.V3(ShelterBreaker, 0.9f), 90);
            cabinet.name = "Shelter breaker cabinet";
            _breakerIndicator = Block(Fx.V3(ShelterBreaker, 1.50f) + Vector3.right * 0.17f,
                new Vector3(0.035f, 0.09f, 0.12f), _neon).GetComponent<Renderer>();
            var tablePos = new Vector2(1.75f, -10.85f);
            var radioTable = Haven("metal_office_desk", Fx.V3(tablePos), 0);
            Haven("Lantern_01", Fx.V3(tablePos + new Vector2(0.55f, 0.1f), BoundsOf(radioTable).max.y), 0);
            var rug = new SurfaceBatch();
            rug.Add(new Vector2(4.15f, -9.5f), new Vector2(2.0f, 2.3f), 0.051f);
            rug.Create("Shelter runner", transform, SiteMaterial(new Color(0.35f, 0.14f, 0.12f)));
            var runnerTrim = new SurfaceBatch();
            runnerTrim.Add(new Vector2(3.23f, -9.5f), new Vector2(0.07f, 2.26f), 0.053f);
            runnerTrim.Add(new Vector2(5.07f, -9.5f), new Vector2(0.07f, 2.26f), 0.053f);
            runnerTrim.Create("Shelter runner trim", transform, SiteMaterial(new Color(0.58f, 0.43f, 0.30f)));
            var radio = Fit(FieldProp("valve_radio", "radio"), tablePos, 0.63f, 0.39f, 0);
            radio.transform.position += Vector3.up * BoundsOf(radioTable).max.y;
            SetShelterPower(true);
        }

        void DressStation(Material concrete)
        {
            var s = Station;
            SignFace("station", new Vector3(s.x + 3.2f, 3.05f, s.y - 2.33f), Vector3.back, 3.15f, 0.76f);
            SignFace("station", new Vector3(s.x + 3.2f, 3.05f, s.y + 2.33f), Vector3.forward, 3.15f, 0.76f);
            var tiles = new SurfaceBatch();
            var tileMaterial = Resources.Load<Material>("Materials/Sidewalk");
            for (int row = 0; row < 8; row++)
            for (int column = 0; column < 8; column++)
                tiles.Add(s + new Vector2(column - 3.5f, row - 3.5f), Vector2.one * 0.975f, 0.164f);
            tiles.Create("Station paving tiles", transform, tileMaterial);
            var steps = new Material(concrete);
            steps.SetColor("_BaseColor", new Color(0.22f, 0.25f, 0.26f));
            var edges = new SurfaceBatch();
            for (int i = 0; i < 8; i++)
            {
                float z = s.y - 1.8f + i * 0.47f;
                float height = 0.24f + (7 - i) * 0.08f;
                Block(new Vector3(s.x + 3.2f, height / 2, z), new Vector3(2.56f, height, 0.47f), steps);
                edges.Add(new Vector2(s.x + 3.2f, z - 0.2f), new Vector2(2.54f, 0.045f), height + 0.005f);
            }
            edges.Create("Stair nosings", transform, _streetPaint);
        }

        sealed class SurfaceBatch
        {
            readonly List<Vector3> _vertices = new();
            readonly List<int> _triangles = new();

            public void Add(Vector2 center, Vector2 size, float height)
            {
                var min = center - size * 0.5f;
                var max = center + size * 0.5f;
                int start = _vertices.Count;
                _vertices.Add(new Vector3(min.x, height, min.y));
                _vertices.Add(new Vector3(min.x, height, max.y));
                _vertices.Add(new Vector3(max.x, height, max.y));
                _vertices.Add(new Vector3(max.x, height, min.y));
                _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public void Create(string name, Transform parent, Material material)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(_triangles, 0);
                var uv = new List<Vector2>(_vertices.Count);
                foreach (var point in _vertices) uv.Add(new Vector2(point.x, point.z) * 0.35f);
                mesh.SetUVs(0, uv);
                mesh.RecalculateNormals();
                var surface = new GameObject(name);
                surface.transform.SetParent(parent, false);
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = surface.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
