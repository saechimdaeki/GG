using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    public partial class City
    {
        internal static readonly Vector2 ClinicCenter = new(21f, -7f);
        internal static readonly Vector2 WorkshopCenter = new(21f, -21f);
        internal static readonly Vector2 StoreCenter = new(7f, 7f);
        internal readonly List<Vector2> InteriorZombies = new();

        bool BuildWalkInShop(Vector2 center)
        {
            if (center == ClinicCenter)
            {
                BuildShopRoom(center, "새벽 약국", "pharmacy", new Color(0.38f, 0.65f, 0.58f), new Color(0.57f, 0.61f, 0.54f));
                return true;
            }
            if (center == WorkshopCenter)
            {
                BuildShopRoom(center, "세운 철물", "hardware", new Color(0.90f, 0.58f, 0.27f), new Color(0.48f, 0.38f, 0.28f));
                return true;
            }
            if (center == StoreCenter)
            {
                BuildShopRoom(center, "한빛 편의점", "store", new Color(0.44f, 0.74f, 0.58f), new Color(0.56f, 0.53f, 0.43f));
                return true;
            }
            return false;
        }

        void BuildShopRoom(Vector2 center, string name, string table, Color accent, Color floorTint)
        {
            const float halfWidth = 4.15f, halfDepth = 3.7f, wall = 0.33f, doorHalf = 1.05f;
            var concrete = Resources.Load<Material>("Materials/Concrete");
            float left = center.x - halfWidth, right = center.x + halfWidth;
            float front = center.y - halfDepth, back = center.y + halfDepth;
            var room = Rect.MinMaxRect(left, front + wall, right, back);
            var tile = new Material(Resources.Load<Material>("Materials/FloorTile"));
            tile.SetColor("_BaseColor", Color.Lerp(Color.white, floorTint, 0.3f));
            var walls = ShelterTile(concrete, 7f);
            var trim = SiteMaterial(accent * 0.62f);

            Block(Fx.V3(center, 0.028f), new Vector3(9.15f, 0.048f, 8.9f), SiteMaterial(new Color(0.34f, 0.37f, 0.35f)));
            var floor = new SurfaceBatch();
            for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
                floor.Add(center + new Vector2(x, z), Vector2.one * 0.978f, 0.059f);
            floor.Create(name + " tiled floor", transform, tile);

            void Wall(Rect rect, float height)
            {
                Walls.Add(rect);
                Block(Fx.V3(rect.center, height * 0.5f), new Vector3(rect.width, height, rect.height), walls);
                Block(Fx.V3(rect.center, height + 0.05f), new Vector3(rect.width + 0.04f, 0.12f, rect.height + 0.04f), trim);
            }

            Wall(new Rect(left, back - wall, halfWidth * 2, wall), 2.8f);
            Wall(new Rect(left, front, wall, halfDepth * 2), 2.8f);
            Wall(new Rect(right - wall, front, wall, halfDepth * 2), 0.86f);
            Wall(new Rect(left, front, halfWidth - doorHalf, wall), 0.83f);
            Wall(new Rect(center.x + doorHalf, front, halfWidth - doorHalf, wall), 0.83f);
            foreach (int side in new[] { -1, 1 })
            {
                float postX = center.x + side * (doorHalf + 0.10f);
                Block(new Vector3(postX, 1.22f, front + 0.165f), new Vector3(0.20f, 2.44f, 0.32f), trim);
            }
            var threshold = new SurfaceBatch();
            threshold.Add(new Vector2(center.x, front + 0.02f), new Vector2(doorHalf * 2, 0.72f), 0.063f);
            threshold.Create("Open shop threshold", transform, trim);

            var sign = new Vector3(center.x - 0.95f, 1.88f, back - wall - 0.018f);
            SignFace(table, sign, Vector3.back, 4.15f, 1.01f);
            var fixture = Block(new Vector3(center.x, 2.42f, back - 0.64f), new Vector3(2.65f, 0.12f, 0.22f),
                Resources.Load<Material>("Materials/Bulb"));
            fixture.name = "Shop wall fixture";
            SiteLamp(center + new Vector2(-1, 1.65f), 2.36f, Color.Lerp(accent, Color.white, 0.43f));

            if (table == "pharmacy")
            {
                AddInteriorLoot(room, center + new Vector2(-2.65f, 1.8f), "약품장", table, "medicine_cabinet",
                    new Vector3(1.28f, 1.68f, 0.60f), accent);
                AddInteriorLoot(room, center + new Vector2(2.35f, 2.25f), "약국 계산대", table, null,
                    new Vector3(1.8f, 1.0f, 0.68f), accent);
            }
            else if (table == "hardware")
            {
                AddInteriorLoot(room, center + new Vector2(-2.65f, 0.9f), "철물점 무기 공구함", table, "tool_chest",
                    new Vector3(1.35f, 1.12f, 0.64f), accent);
                AddInteriorLoot(room, center + new Vector2(2.25f, 2.05f), "철물 부품 선반", table, null,
                    new Vector3(1.75f, 1.28f, 0.65f), accent);
            }
            else
            {
                AddInteriorLoot(room, center + new Vector2(-2.35f, 1.65f), "식료품 선반", table, null,
                    new Vector3(1.95f, 1.17f, 0.65f), accent);
                AddInteriorLoot(room, center + new Vector2(2.18f, 0.30f), "편의점 계산대", table, null,
                    new Vector3(1.6f, 0.91f, 0.65f), accent);
            }

            DressShopInterior(center, table, accent);
            if (table != "store") InteriorZombies.Add(center + new Vector2(0.70f, 1.35f));
            Shops.Add(new Shop { name = name, pos = new Vector2(center.x, front - 1.1f), color = accent });
        }

        void DressShopInterior(Vector2 center, string table, Color accent)
        {
            GameObject PlaceFurniture(GameObject prop, Vector2 at, float wide, float tall, float angle)
            {
                var go = Fit(prop, at, wide, tall, angle);
                var bounds = BoundsOf(go);
                Walls.Add(Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z));
                return go;
            }

            if (table == "pharmacy")
            {
                Vector2 at = center + new Vector2(2.82f, -0.75f);
                PlaceFurniture(FieldProp("hospital_bed", "bedSingle"), at, 2.27f, 0.86f, 0f);
                var privacy = Block(Fx.V3(at + new Vector2(0.43f, -1.3f), 0.97f),
                    new Vector3(1.14f, 1.92f, 0.11f), SiteMaterial(new Color(0.38f, 0.48f, 0.43f)));
                privacy.name = "Pharmacy privacy screen";
            }
            else if (table == "hardware")
            {
                Vector2 at = center + new Vector2(2.75f, -0.83f);
                var bench = PlaceFurniture(Haven("metal_office_desk", Vector3.zero, 0), at, 1.8f, 0.94f, 90f);
                var tray = Fit(Haven("cardboard_box_01", Vector3.zero, 0), at, 0.51f, 0.35f, 17);
                tray.transform.position += Vector3.up * BoundsOf(bench).max.y;
                var mat = SiteMaterial(new Color(0.46f, 0.29f, 0.13f));
                var safety = new SurfaceBatch();
                for (int stripe = -2; stripe <= 2; stripe++)
                    safety.Add(at + new Vector2(-0.87f, stripe * 0.3f), new Vector2(0.19f, 0.16f), 0.066f);
                safety.Create("Workshop aisle paint", transform, mat);
            }
            else
            {
                var display = Block(Fx.V3(center + new Vector2(-2.5f, -1.2f), 0.42f),
                    new Vector3(1.45f, 0.84f, 0.74f), SiteMaterial(accent * 0.68f));
                Walls.Add(new Rect(center.x - 3.23f, center.y - 1.57f, 1.45f, 0.74f));
                var coolerLid = Block(display.transform.position + Vector3.up * 0.45f, new Vector3(1.48f, 0.13f, 0.78f),
                    SiteMaterial(new Color(0.42f, 0.54f, 0.51f)));
                coolerLid.name = "Convenience store cooler";
            }
        }

        void AddInteriorLoot(Rect room, Vector2 at, string name, string table, string prop, Vector3 size, Color accent)
        {
            var model = prop == null ? null : LoadHollowsProp(prop);
            GameObject visual;
            if (model)
                visual = Fit(Instantiate(model, transform), at, Mathf.Max(size.x, size.z), size.y, 0);
            else
            {
                visual = Block(Fx.V3(at, size.y * 0.5f), size, SiteMaterial(new Color(0.25f, 0.28f, 0.26f)));
                var rails = Block(Fx.V3(at + Vector2.down * (size.z * 0.5f + 0.01f), size.y * 0.78f),
                    new Vector3(size.x - 0.06f, 0.09f, 0.045f), SiteMaterial(accent));
                rails.transform.SetParent(visual.transform, true);
                for (int bin = -1; bin <= 1; bin++)
                {
                    var goods = Block(Fx.V3(at + new Vector2(bin * size.x * 0.28f, 0), size.y + 0.095f),
                        new Vector3(0.26f, 0.19f, 0.27f), SiteMaterial(bin == 0 ? accent : new Color(0.63f, 0.57f, 0.43f)));
                    goods.transform.SetParent(visual.transform, true);
                }
            }
            visual.name = name;
            var b = BoundsOf(visual);
            Walls.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
            Containers.Add(new Container { t = visual.transform, basePos = visual.transform.position,
                pos = new Vector2(b.center.x, b.min.z - 0.58f), kind = name, table = table, room = room,
                guaranteedItem = prop == "tool_chest" ? "axe" : null });
        }
    }
}
