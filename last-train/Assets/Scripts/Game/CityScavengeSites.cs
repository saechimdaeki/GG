using UnityEngine;

namespace LastTrain
{
    public partial class City
    {
        internal static readonly Vector2 ReliefCourt = new(-7, 7);
        internal static readonly Vector2 SalvageYard = new(-7, -21);
        internal static readonly Vector2 StationParking = new(35, 7);

        bool BuildScavengeSite(Vector2 center)
        {
            if (center == ReliefCourt) BuildReliefCourt(center);
            else if (center == SalvageYard) BuildSalvageYard(center);
            else if (center == StationParking) BuildStationParking(center);
            else return false;
            return true;
        }

        void BuildReliefCourt(Vector2 at)
        {
            var green = new Color(0.47f, 0.72f, 0.53f);
            var curb = SiteMaterial(new Color(0.35f, 0.45f, 0.37f));
            SiteGround(at, new Vector2(10, 10), "Materials/Brick");
            // A straight route through the queue, with an opening into the east alley.
            SiteBarrier(at + new Vector2(-4.15f, 0), new Vector2(0.4f, 7.5f), 0.75f, curb);
            SiteBarrier(at + new Vector2(-2.3f, 3.35f), new Vector2(3.3f, 1.05f), 0.82f, curb);
            SiteBarrier(at + new Vector2(2.8f, -2.25f), new Vector2(3.2f, 0.36f), 0.65f, curb);
            SiteProp("wooden_picnic_table", at + new Vector2(3.15f, 2.5f), 2.15f, 1f, 0);
            SiteCache(at + new Vector2(-2.3f, 1.3f), "무료 배급 식량함", "store", "food", "plastic_crate_03");
            SiteLamp(at + new Vector2(-3.25f, 2.2f), 2.2f, green);
            SiteLabel(at, "임시 배급소", green);
        }

        void BuildSalvageYard(Vector2 at)
        {
            var amber = new Color(0.91f, 0.62f, 0.34f);
            var steel = SiteMaterial(new Color(0.30f, 0.35f, 0.34f));
            SiteGround(at, new Vector2(11.4f, 9.4f), "Materials/Concrete");
            // Offset rows of racks bend the passage between the south and north entrances.
            SiteBarrier(at + new Vector2(-4.65f, 0), new Vector2(0.38f, 8.2f), 1.15f, steel);
            SiteBarrier(at + new Vector2(4.65f, 0), new Vector2(0.38f, 8.2f), 0.85f, steel);
            foreach (float x in new[] { -3f, -1.6f, -0.2f })
                SiteProp("steel_frame_shelves_01", at + new Vector2(x, -0.8f), 1.38f, 1.48f, 0);
            foreach (float x in new[] { 1.85f, 3.3f })
                SiteProp("wooden_military_crate", at + new Vector2(x, 2.45f), 1.35f, 0.95f, 0);
            SiteCache(at + new Vector2(-2.7f, 2.65f), "정비반 자재함", "hardware", "scrap", "wooden_crate_01");
            InteriorZombies.Add(at + new Vector2(2.4f, 0.4f));
            SiteLamp(at + new Vector2(-3.85f, 1.45f), 2.75f, amber);
            SiteLabel(at, "시설반 야적장", amber);
        }

        void BuildStationParking(Vector2 at)
        {
            var blue = new Color(0.49f, 0.74f, 0.95f);
            var paint = SiteMaterial(new Color(0.78f, 0.78f, 0.64f));
            SiteGround(at, new Vector2(11.5f, 10.8f), "Materials/Ground");
            var stripes = new SurfaceBatch();
            foreach (int row in new[] { -1, 1 })
            {
                for (int bay = -2; bay <= 2; bay++)
                    stripes.Add(at + new Vector2(bay * 2.25f, row * 3.1f), new Vector2(0.10f, 3.9f), 0.043f);
                stripes.Add(at + new Vector2(0, row * 5.03f), new Vector2(9.1f, 0.10f), 0.043f);
            }
            stripes.Create("Station parking bay paint", transform, paint);
            SiteProp("covered_car", at + new Vector2(-3.35f, -3.05f), 3.6f, 1.42f, 0);
            SiteProp("covered_car", at + new Vector2(1.12f, 3.1f), 3.6f, 1.42f, 0);
            SiteCache(at + new Vector2(3.35f, 3.6f), "주차장 전기 점검함", "hardware", "fuse", "wooden_military_crate");
            InteriorZombies.Add(at + new Vector2(-1.5f, 0.55f));
            SiteLamp(at + new Vector2(4.8f, 3.6f), 2.6f, blue);
            SiteLabel(at, "세운역 환승 주차장", blue);
        }

        void SiteGround(Vector2 at, Vector2 size, string material)
        {
            Block(Fx.V3(at, 0.018f), new Vector3(size.x, 0.036f, size.y), Resources.Load<Material>(material));
        }

        void SiteBarrier(Vector2 at, Vector2 size, float height, Material material)
        {
            Block(Fx.V3(at, height * 0.5f), new Vector3(size.x, height, size.y), material);
            Walls.Add(new Rect(at - size * 0.5f, size));
        }

        GameObject SiteProp(string name, Vector2 at, float width, float height, float yaw)
        {
            var node = Haven(name, Vector3.zero, yaw);
            Bounds initial = BoundsOf(node);
            float scale = width / Mathf.Max(initial.size.x, initial.size.z);
            float vertical = Mathf.Min(scale, height / Mathf.Max(initial.size.y, 0.01f));
            node.transform.localScale = Vector3.Scale(node.transform.localScale, new Vector3(scale, vertical, scale));
            Bounds sized = BoundsOf(node);
            node.transform.position += Fx.V3(at) - new Vector3(sized.center.x, sized.min.y, sized.center.z);
            Solid(node);
            return node;
        }

        void SiteCache(Vector2 at, string label, string table, string item, string prop)
        {
            var node = SiteProp(prop, at, 1.22f, 0.85f, 0);
            var b = BoundsOf(node);
            Containers.Add(new Container
            {
                t = node.transform, basePos = node.transform.position,
                pos = new Vector2(at.x, b.min.z - 0.64f),
                kind = label, table = table, guaranteedItem = item,
            });
        }

        void SiteLabel(Vector2 at, string label, Color color)
        {
            Shops.Add(new Shop { name = label, pos = at, color = color });
        }
    }
}
