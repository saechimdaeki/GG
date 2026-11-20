using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    public partial class City
    {
        // North to south. The entry points for shops, contacts and escapes are built first.
        // H: hillside homes, A: apartments, M: storefronts, W: workshops, P: pocket courtyard, G: public square, B: brick corner, D: vacant lot.
        static readonly string[] DistrictPlan =
        {
            "HHHPAAAG",
            "HHPAAMAG",
            "HPBMMAAG",
            "PMMMMPMP",
            "HHPMMBMM",
            "HHHPWWWW",
            "WDPWGWPW",
            "WGPWWWPW",
        };

        readonly Dictionary<string, Material> _districtMaterials = new();

        Material DistrictMaterial(string id, string source, Color tint)
        {
            if (_districtMaterials.TryGetValue(id, out var material)) return material;
            material = new Material(Resources.Load<Material>("Materials/" + source));
            material.SetColor("_BaseColor", tint);
            _districtMaterials.Add(id, material);
            return material;
        }

        void BuildDistrictParcel(Vector2 at, char use)
        {
            if (at == new Vector2(21, 21))
            {
                BuildMarketCourt(at);
                return;
            }
            if (at == new Vector2(7, 21) || at == new Vector2(35, -7))
            {
                BuildSteppedCorner(at, at.x == 7);
                return;
            }
            switch (use)
            {
                case 'H':
                    float offset = at.y > 0 ? 1.7f : -1.3f;
                    BuildHouse(at + new Vector2(-2.9f, offset), new Vector2(3.6f, 5.25f), true);
                    BuildHouse(at + new Vector2(2.75f, -offset * 0.5f), new Vector2(4.0f, 5.55f), false);
                    break;
                case 'A':
                    BuildApartment(at + Vector2.up * 0.9f);
                    break;
                case 'M':
                    BuildMarketRow(at + Vector2.up * (at.y > 0 ? 1.5f : 0.6f));
                    break;
                case 'W':
                    BuildWorkshop(at);
                    break;
                case 'B':
                    var brick = KitBuilding(at + new Vector2(0.5f, 0.8f), 6.6f, 7.9f, 1);
                    RegisterBuilding(brick, true);
                    DressFacade(brick, Vector3.back);
                    break;
                case 'D':
                    Plaza(at);
                    break;
                case 'G':
                    BuildCourtyard(at, true);
                    break;
                default:
                    BuildCourtyard(at, false);
                    break;
            }
        }

        void BuildMarketCourt(Vector2 at)
        {
            var brick = DistrictMaterial("court red brick", "Brick", new Color(0.72f, 0.65f, 0.56f));
            var plaster = DistrictMaterial("court cream plaster", "Plaster", new Color(0.77f, 0.71f, 0.60f));
            var red = DistrictMaterial("court roof red", "RoofTile", new Color(0.50f, 0.22f, 0.17f));
            var green = DistrictMaterial("court roof green", "RoofTile", new Color(0.26f, 0.42f, 0.36f));
            var trim = DistrictMaterial("court dark oak", "Planks", new Color(0.31f, 0.22f, 0.17f));
            Part(transform, at, 0.025f, new Vector3(12.1f, 0.05f, 11.8f),
                DistrictMaterial("court paving", "Brick", new Color(0.53f, 0.49f, 0.42f)));

            // Three built wings with a deep open court. The lower right wing leaves a second access.
            Vector2 left = at + new Vector2(-4.35f, -0.75f);
            Vector2 rear = at + new Vector2(-0.25f, 3.75f);
            Vector2 right = at + new Vector2(4.32f, 1.45f);
            BuildCourtWing(left, new Vector2(3.0f, 9.2f), 2.7f, plaster, red, trim, true);
            BuildCourtWing(rear, new Vector2(5.35f, 3.35f), 4.55f, brick, green, trim, false, enterable: true);
            BuildMarketUpperFloor();
            BuildCourtWing(right, new Vector2(3.1f, 4.7f), 2.35f, brick, red, trim, true);

            var porch = at + new Vector2(0, 1.26f);
            var trimLight = DistrictMaterial("court worn stone", "Concrete", new Color(0.62f, 0.58f, 0.50f));
            Part(transform, porch, 0.08f, new Vector3(4.6f, 0.13f, 1.75f), trimLight);
            SiteProp("wooden_picnic_table", at + new Vector2(-1.8f, -0.66f), 1.45f, 0.92f, 90);
            SiteProp("plastic_crate_03", at + new Vector2(1.7f, 0.35f), 0.79f, 0.53f, 0);
            var posts = new[] { at + new Vector2(-2.44f, 1.08f), at + new Vector2(2.03f, 1.08f) };
            foreach (Vector2 post in posts) SiteBarrier(post, new Vector2(0.18f, 0.18f), 2.48f, trim);
            Part(transform, porch, 2.48f, new Vector3(4.9f, 0.20f, 1.65f), green);
            SignFace("market", Fx.V3(at + new Vector2(0f, 0.40f), 2.50f), Vector3.back, 2.12f, 0.38f);
        }

        void BuildSteppedCorner(Vector2 at, bool laundry)
        {
            var plaster = DistrictMaterial("corner blue plaster", "Plaster", new Color(0.53f, 0.65f, 0.64f));
            var brick = DistrictMaterial("corner dark brick", "Brick", new Color(0.62f, 0.53f, 0.46f));
            var roof = DistrictMaterial("corner zinc", "RoofTile", new Color(0.35f, 0.44f, 0.46f));
            var trim = DistrictMaterial("corner ochre wood", "Planks", new Color(0.48f, 0.32f, 0.23f));
            Vector2 rear = at + new Vector2(-0.8f, 2.85f);
            Vector2 side = at + new Vector2(laundry ? -3.2f : 3.2f, -1.1f);
            Vector2 sideSize = new Vector2(4.0f, 5.3f);
            BuildCourtWing(rear, new Vector2(9.5f, 3.0f), 3.35f, brick, roof, trim, false);
            BuildCourtWing(side, sideSize, 2.55f, plaster, roof, trim, true);

            // The missing corner remains a traversable forecourt instead of a solid block.
            float openSide = laundry ? 1f : -1f;
            var yard = at + new Vector2(2.15f * openSide, -1.32f);
            Part(transform, yard, 0.022f, new Vector3(5.95f, 0.044f, 5.5f),
                DistrictMaterial("corner pavement", "WornFloor", new Color(0.60f, 0.56f, 0.48f)));
            SiteProp("metal_trash_can", yard + new Vector2(1.85f * openSide, 1.42f), 0.62f, 0.82f, 0);
            SiteProp("wooden_crate_01", yard + new Vector2(0.94f * openSide, 1.52f), 0.84f, 0.72f, 12);
            SignFace(laundry ? "laundry" : "print", Fx.V3(side + Vector2.down * (sideSize.y * 0.5f + 0.18f), 2.18f),
                Vector3.back, 2.22f, 0.46f);
        }

        void BuildCourtWing(Vector2 at, Vector2 size, float eaves, Material wall, Material roof, Material trim, bool ridgeNorth, bool enterable = false)
        {
            float rise = ridgeNorth ? 1.05f : 1.12f;
            var upper = enterable
                ? BuildMarketGroundFloor(at, size, eaves, eaves + rise + 0.2f, wall, trim)
                : DistrictBuilding(at, size, "Market masonry wing", eaves, eaves + rise + 0.2f, wall, false);
            float run = (ridgeNorth ? size.x : size.y) * 0.5f + 0.23f;
            float ridgeLength = (ridgeNorth ? size.y : size.x) + 0.65f;
            float pitch = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float slope = Mathf.Sqrt(run * run + rise * rise);
            foreach (int side in new[] { -1, 1 })
            {
                var cross = ridgeNorth ? Vector2.right : Vector2.up;
                var slab = Block(Fx.V3(at + cross * (side * run * 0.5f), eaves + rise * 0.5f),
                    new Vector3(ridgeLength, 0.17f, slope), roof);
                slab.transform.rotation = Quaternion.Euler(0, ridgeNorth ? 90f : 0f, 0) *
                    Quaternion.Euler(side * pitch, 0, 0);
                slab.transform.SetParent(upper, true);
            }
            // Fill the pitched ends as part of the roof volume.
            var polygon = new Mesh { name = "Market pitched ends" };
            float halfRidge = (ridgeNorth ? size.y : size.x) * 0.5f;
            float halfCross = (ridgeNorth ? size.x : size.y) * 0.5f;
            Vector3 Vertex(float ridge, float cross, float y) =>
                ridgeNorth ? new Vector3(cross, y, -ridge) : new Vector3(ridge, y, cross);
            polygon.vertices = new[] {
                Vertex(-halfRidge, -halfCross, 0), Vertex(-halfRidge, 0, rise), Vertex(-halfRidge, halfCross, 0),
                Vertex(halfRidge, -halfCross, 0), Vertex(halfRidge, 0, rise), Vertex(halfRidge, halfCross, 0) };
            polygon.uv = new[] { Vector2.zero, new Vector2(0.5f, 1), Vector2.right, Vector2.zero, new Vector2(0.5f, 1), Vector2.right };
            polygon.triangles = new[] { 0, 2, 1, 3, 4, 5 };
            polygon.RecalculateNormals();
            var gables = new GameObject("Market gable walls");
            gables.transform.SetParent(upper, false);
            gables.transform.position = Fx.V3(at, eaves);
            gables.AddComponent<MeshFilter>().sharedMesh = polygon;
            gables.AddComponent<MeshRenderer>().sharedMaterial = wall;

            if (enterable) return;
            float door = Mathf.Min(size.x * 0.55f, 1.85f);
            StreetOpening(at, size, 0.12f, 1.94f, door, "Shutter");
            var front = at + Vector2.down * (size.y * 0.5f + 0.21f);
            Part(transform, front, 2.07f, new Vector3(size.x - 0.12f, 0.13f, 0.49f), trim);
            if (size.x > 4.5f)
            {
                Window(at, size, -size.x * 0.32f, 1.42f, 1.12f, 0.91f, trim, transform);
                Window(at, size, size.x * 0.32f, 1.42f, 1.12f, 0.91f, trim, transform);
            }
        }

        void BuildDistrictWalkways()
        {
            var brick = Resources.Load<Material>("Materials/Brick");
            var paving = DistrictMaterial("pedestrian stone", "Sidewalk", new Color(0.63f, 0.61f, 0.57f));
            var seams = new SurfaceBatch();
            var walkways = new SurfaceBatch();

            // The west residential street is a continuous pedestrian lane with courts opening onto it.
            walkways.Add(new Vector2(-34f, 28f), new Vector2(39f, 4.6f), 0.0215f);
            // The east promenade joins the station side and the open squares on a single broad path.
            walkways.Add(new Vector2(49f, 28f), new Vector2(5.0f, 59.5f), 0.023f);
            walkways.Create("District pedestrian connections", transform, paving);
            for (int x = -52; x <= -16; x += 4)
                seams.Add(new Vector2(x, 28f), new Vector2(0.28f, 4.3f), 0.027f);
            for (int z = 1; z <= 53; z += 4)
                seams.Add(new Vector2(49f, z), new Vector2(4.7f, 0.27f), 0.027f);
            seams.Create("Pedestrian paver joints", transform, brick);

            var marketWalk = new SurfaceBatch();
            marketWalk.Add(new Vector2(12f, 14f), new Vector2(27.5f, 4.2f), 0.023f);
            marketWalk.Create("Market footstreet", transform,
                DistrictMaterial("market pavers", "Brick", new Color(0.48f, 0.44f, 0.39f)));
        }

        // Each street-facing elevation has a different silhouette, roof and window treatment.
        void BuildHouse(Vector2 at, Vector2 size, bool warm)
        {
            var wall = warm
                ? DistrictMaterial("home brick", "Brick", new Color(0.58f, 0.42f, 0.33f))
                : DistrictMaterial("home plaster", "Plaster", new Color(0.66f, 0.67f, 0.61f));
            var roof = warm
                ? DistrictMaterial("tile red", "RoofTile", new Color(0.54f, 0.24f, 0.17f))
                : DistrictMaterial("tile blue", "RoofTile", new Color(0.30f, 0.40f, 0.43f));
            var frame = DistrictMaterial("house trim", "Concrete", new Color(0.60f, 0.58f, 0.48f));
            const float eaves = 2.65f;
            var upper = DistrictBuilding(at, size, "Hillside home", eaves, eaves + 1.04f, wall, false);
            float run = size.y * 0.5f + 0.27f;
            const float rise = 0.98f;
            float pitch = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            foreach (int side in new[] { -1, 1 })
            {
                var slab = Block(Fx.V3(at + Vector2.up * (side * run * 0.5f), eaves + rise * 0.5f),
                    new Vector3(size.x + 0.46f, 0.17f, Mathf.Sqrt(run * run + rise * rise)), roof);
                slab.transform.rotation = Quaternion.Euler(side * pitch, 0, 0);
                slab.transform.SetParent(upper, true);
            }
            // Close the two gable ends with the same wall surface.
            var gables = new GameObject("Gable end walls");
            gables.transform.SetParent(upper, false);
            gables.transform.position = Fx.V3(at, eaves);
            var mesh = new Mesh { name = "House gables" };
            float x = size.x * 0.5f, z = size.y * 0.5f;
            mesh.vertices = new[] { new Vector3(-x, 0, -z), new Vector3(-x, rise, 0), new Vector3(-x, 0, z),
                new Vector3(x, 0, -z), new Vector3(x, rise, 0), new Vector3(x, 0, z) };
            mesh.uv = new[] { Vector2.zero, new Vector2(0.5f, 1f), Vector2.right,
                Vector2.zero, new Vector2(0.5f, 1f), Vector2.right };
            mesh.triangles = new[] { 0, 2, 1, 3, 4, 5 };
            mesh.RecalculateNormals();
            gables.AddComponent<MeshFilter>().sharedMesh = mesh;
            gables.AddComponent<MeshRenderer>().sharedMaterial = wall;
            Window(at, size, -0.83f, 1.62f, 1.22f, 0.86f, frame, transform);
            StreetOpening(at, size, 0.68f, 1.92f, 0.83f, "Planks");
        }

        void BuildApartment(Vector2 at)
        {
            var wall = DistrictMaterial("apartment plaster", "Plaster", new Color(0.63f, 0.65f, 0.62f));
            var trim = DistrictMaterial("apartment concrete", "Concrete", new Color(0.47f, 0.57f, 0.55f));
            var roof = DistrictMaterial("apartment roof", "WornFloor", new Color(0.30f, 0.34f, 0.34f));
            var size = new Vector2(9.6f, 5.85f);
            var upper = DistrictBuilding(at, size, "Rail apartments", 2.25f, 6.5f, wall, false);
            Part(upper, at, 4.25f, new Vector3(size.x, 4.0f, size.y), wall);
            FlatRoof(upper, at, size, 6.28f, roof, trim);
            for (int floor = 0; floor < 2; floor++)
            {
                float height = 3.12f + floor * 2.01f;
                for (int bay = -1; bay <= 1; bay++)
                    Window(at, size, bay * 2.68f, height, 1.74f, 1.06f, trim, upper);
                Part(upper, at + Vector2.down * (size.y * 0.5f + 0.26f), height - 0.61f, new Vector3(8.9f, 0.15f, 0.56f), trim);
            }
            Part(upper, at + new Vector2(-2.8f, 1.2f), 6.71f, new Vector3(1.65f, 0.86f, 1.8f), trim);
            StreetOpening(at, size, 0, 2.05f, 1.45f, "Glass");
            Window(at, size, -2.7f, 1.26f, 1.72f, 0.90f, trim, transform);
            Window(at, size, 2.7f, 1.26f, 1.72f, 0.90f, trim, transform);
        }

        void BuildMarketRow(Vector2 at)
        {
            var roof = DistrictMaterial("market roof", "WornFloor", new Color(0.39f, 0.38f, 0.35f));
            var trim = DistrictMaterial("market fascia", "PaintedConcrete", new Color(0.26f, 0.31f, 0.28f));
            var wall = DistrictMaterial("market plaster", "Plaster", new Color(0.70f, 0.62f, 0.49f));
            var size = new Vector2(10.35f, 5.0f);
            var upper = DistrictBuilding(at, size, "Covered storefront row", 2.62f, 4.70f, wall, true);
            var setback = at + new Vector2(-1.35f, 0.78f);
            Part(upper, setback, 3.45f, new Vector3(7.2f, 1.68f, 3.4f), wall);
            FlatRoof(upper, setback, new Vector2(7.2f, 3.4f), 4.32f, roof, trim);
            Part(upper, at, 2.68f, new Vector3(size.x + 0.13f, 0.13f, size.y + 0.13f), roof);
            Part(upper, at + Vector2.down * 3.27f, 2.51f, new Vector3(10.6f, 0.15f, 1.55f), trim);
            for (int bay = -1; bay <= 1; bay++)
            {
                float x = bay * 3.1f;
                StreetOpening(at, size, x, 1.92f, 2.40f, "Shutter");
                var accent = bay == 0
                    ? DistrictMaterial("market ochre", "PaintedConcrete", new Color(0.60f, 0.42f, 0.24f))
                    : DistrictMaterial("market jade", "PaintedConcrete", new Color(0.31f, 0.49f, 0.42f));
                Part(upper, at + new Vector2(x, -4.01f), 2.50f, new Vector3(2.94f, 0.24f, 0.12f), accent);
            }
            for (int post = -1; post <= 1; post += 2)
                SiteBarrier(at + new Vector2(post * 5.14f, -3.91f), new Vector2(0.19f, 0.20f), 2.53f, trim);
        }

        void BuildWorkshop(Vector2 at)
        {
            var wall = DistrictMaterial("yard corrugate", "Shutter", new Color(0.40f, 0.43f, 0.42f));
            var roof = DistrictMaterial("yard roof", "Shutter", new Color(0.32f, 0.36f, 0.38f));
            var trim = DistrictMaterial("yard concrete", "Concrete", new Color(0.49f, 0.49f, 0.43f));
            SiteGround(at, new Vector2(10.6f, 10.7f), "Materials/WornFloor");
            var atRear = at + new Vector2(0, 2.0f);
            var size = new Vector2(9.8f, 5.2f);
            var upper = DistrictBuilding(atRear, size, "Workshop shed", 2.60f, 4.5f, wall, false);
            Part(upper, atRear + Vector2.up * 0.95f, 3.2f, new Vector3(9.8f, 1.25f, 3.3f), roof);
            FlatRoof(upper, atRear + Vector2.up * 0.95f, new Vector2(9.8f, 3.3f), 3.85f, roof, trim);
            StreetOpening(atRear, size, -2.1f, 2.15f, 3.25f, "Shutter");
            Window(atRear, size, 2.8f, 1.72f, 2.18f, 0.93f, trim, transform);
            SiteBarrier(at + new Vector2(-4.7f, -2.85f), new Vector2(0.36f, 4.05f), 0.75f, trim);
            SiteProp("wooden_military_crate", at + new Vector2(3.65f, -2.85f), 1.75f, 1.15f, 0);
        }

        void BuildCourtyard(Vector2 at, bool square)
        {
            var stone = DistrictMaterial("court stone", "Sidewalk", new Color(0.66f, 0.63f, 0.55f));
            Part(transform, at, 0.018f, new Vector3(square ? 11.6f : 8.7f, 0.036f, 10.4f), stone);
            var planter = DistrictMaterial("planter concrete", "Concrete", new Color(0.52f, 0.51f, 0.43f));
            var leaf = DistrictMaterial("courtyard leaf", "Plaster", new Color(0.29f, 0.38f, 0.23f));
            var leafShade = DistrictMaterial("courtyard leaf shade", "Plaster", new Color(0.21f, 0.30f, 0.18f));
            var trunk = DistrictMaterial("courtyard bark", "Planks", new Color(0.40f, 0.30f, 0.22f));
            foreach (int side in new[] { -1, 1 })
            {
                var bed = at + new Vector2(side * (square ? 4.25f : 3.13f), side * 1.05f);
                SiteBarrier(bed, new Vector2(1.55f, 3.4f), 0.57f, planter);
                if (square && side > 0)
                {
                    Part(transform, bed, 1.49f, new Vector3(0.20f, 1.90f, 0.19f), trunk);
                    PlantCanopy(bed, 2.54f, new Vector3(2.36f, 1.77f, 2.16f), leafShade);
                    PlantCanopy(bed + new Vector2(-0.66f, 0.4f), 2.77f, new Vector3(1.55f, 1.41f, 1.68f), leaf);
                    PlantCanopy(bed + new Vector2(0.59f, -0.24f), 2.83f, new Vector3(1.48f, 1.28f, 1.64f), leaf);
                    PlantCanopy(bed + new Vector2(0.12f, 0.23f), 3.18f, new Vector3(1.42f, 1.24f, 1.52f), leaf);
                    continue;
                }
                for (int i = -1; i <= 1; i++)
                {
                    var shrub = bed + new Vector2(i == 0 ? 0.15f : -0.10f, i * 0.92f);
                    PlantCanopy(shrub, 0.78f, new Vector3(1.16f, 0.65f, 1.11f), i == 0 ? leaf : leafShade);
                }
            }
            SiteProp("wooden_picnic_table", at + new Vector2(-1.9f, 2.7f), 1.85f, 0.92f, 90f);
        }

        void PlantCanopy(Vector2 at, float height, Vector3 size, Material material)
        {
            var plant = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            plant.name = "Courtyard canopy";
            Destroy(plant.GetComponent<Collider>());
            plant.transform.SetParent(transform, false);
            plant.transform.position = Fx.V3(at, height);
            plant.transform.localScale = size;
            plant.GetComponent<Renderer>().sharedMaterial = material;
        }

        Transform DistrictBuilding(Vector2 at, Vector2 size, string name, float baseHeight, float height, Material wall, bool commerce)
        {
            var baseModel = Block(Fx.V3(at, baseHeight * 0.5f), new Vector3(size.x, baseHeight, size.y), wall);
            baseModel.name = name;
            var upper = new GameObject("Roof and upper frontage").transform;
            upper.SetParent(transform, false);
            var bounds = new Bounds(Fx.V3(at, height * 0.5f), new Vector3(size.x, height, size.y));
            _houses.Add(new House { bounds = bounds, upper = upper.gameObject });
            RegisterBuilding(bounds, commerce);
            return upper;
        }

        void FlatRoof(Transform upper, Vector2 at, Vector2 size, float height, Material roof, Material trim)
        {
            Part(upper, at, height, new Vector3(size.x + 0.16f, 0.17f, size.y + 0.16f), roof);
            foreach (int side in new[] { -1, 1 })
            {
                Part(upper, at + Vector2.up * (side * size.y * 0.5f), height + 0.19f, new Vector3(size.x + 0.13f, 0.30f, 0.17f), trim);
                Part(upper, at + Vector2.right * (side * size.x * 0.5f), height + 0.19f, new Vector3(0.17f, 0.30f, size.y), trim);
            }
        }

        void Window(Vector2 at, Vector2 size, float offset, float height, float width, float tall, Material frame, Transform parent)
        {
            var face = at + new Vector2(offset, -size.y * 0.5f);
            Part(parent, face + Vector2.down * 0.044f, height, new Vector3(width + 0.17f, tall + 0.18f, 0.12f), frame);
            Part(parent, face + Vector2.down * 0.114f, height, new Vector3(width, tall, 0.075f), Resources.Load<Material>("Materials/Glass"));
            Part(parent, face + Vector2.down * 0.16f, height, new Vector3(0.064f, tall, 0.055f), frame);
        }

        void StreetOpening(Vector2 at, Vector2 size, float x, float tall, float width, string material)
        {
            Part(transform, at + new Vector2(x, -size.y * 0.5f - 0.045f), tall * 0.5f,
                new Vector3(width, tall, 0.14f), Resources.Load<Material>("Materials/" + material));
        }

        void Part(Transform parent, Vector2 at, float height, Vector3 scale, Material mat)
        {
            var part = Block(Fx.V3(at, height), scale, mat);
            part.transform.SetParent(parent, true);
        }
    }
}
