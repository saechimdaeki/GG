using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DesktopTenants
{
    /// <summary>One buildable lot type, from Resources/town.json.</summary>
    [Serializable]
    public class LotDef
    {
        public string id, name;
        public int cost, income;
        public string[] visit, fans;
        /// <summary>Buildings with a door are entered; the park is walked to instead.</summary>
        public bool HasDoor => id != "park";
    }

    [Serializable]
    public class LotBook
    {
        public LotDef[] lots;
        public static LotBook Load() => JsonUtility.FromJson<LotBook>(Resources.Load<TextAsset>("town").text);
    }

    /// <summary>A built shop or park on the floor, left of the apartment.</summary>
    public class Building
    {
        public const int MaxLevel = 3;
        public LotDef Def;
        public int Level = 1, Index;
        public float X, W, H;         // centre x, size in points
        public SpriteRenderer Sr;
        public Bubble Sign;
        public float IncomeT;
        public readonly List<(float x0, float x1, float topFromFloor)> Tops = new List<(float, float, float)>();

        public float DoorX => X + W * 0.22f;
        public string Title => Def.name + new string('★', Level);
    }

    /// <summary>
    /// The neighbourhood growing to the left of the apartment: buy the next lot, upgrade buildings,
    /// collect their takings, and let tenants walk in and out. Owned and ticked by <see cref="Desk"/>.
    /// </summary>
    public class Town
    {
        /// <summary>Surface ids for building roofs/tree tops: TopBase - index*8 - k.</summary>
        public const int TopBase = -100;
        const float Gap = 18f;

        readonly Desk d;
        readonly LotBook book;
        readonly List<Building> buildings = new List<Building>();
        readonly SpriteRenderer lotSign;
        readonly Bubble tip;
        float lotX;
        bool dim;

        public IReadOnlyList<Building> Buildings => buildings;
        LotDef NextLot => buildings.Count < book.lots.Length ? book.lots[buildings.Count] : null;
        Rect LotRect => new Rect(lotX - 20, d.FloorY - 40, 40, 40);

        public Town(Desk desk)
        {
            d = desk;
            book = LotBook.Load();
            lotSign = new GameObject("Lot for sale").AddComponent<SpriteRenderer>();
            lotSign.sprite = Pixels.Make(TownArt.ForSale, TownArt.Pal, desk.Tune.pixel);
            lotSign.sortingOrder = 1;
            tip = Bubble.Create(lotSign.transform, Vector3.zero);
            // Saved as "store:2,cafe:1".
            foreach (var part in PlayerPrefs.GetString("town", "").Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || NextLot == null || NextLot.id != kv[0]) continue;
                Add(NextLot, Mathf.Clamp(int.Parse(kv[1]), 1, Building.MaxLevel));
            }
        }

        void Add(LotDef def, int level)
        {
            var b = new Building { Def = def, Level = level, Index = buildings.Count, IncomeT = Random.Range(20f, 40f) };
            b.Sr = new GameObject(def.name).AddComponent<SpriteRenderer>();
            b.Sr.sortingOrder = 1;
            b.Sign = Bubble.Create(b.Sr.transform, Vector3.zero, 3);
            buildings.Add(b);
            Draw(b);
        }

        void Draw(Building b)
        {
            var rows = TownArt.Rows(b.Def.id, b.Level, b.Tops);
            float px = d.Tune.pixel;
            b.Sr.sprite = Pixels.Make(rows, TownArt.Pal, px);
            b.W = rows[0].Length * px;
            b.H = rows.Length * px;
            for (int i = 0; i < b.Tops.Count; i++)
                b.Tops[i] = (b.Tops[i].x0 * px, b.Tops[i].x1 * px, b.Tops[i].topFromFloor * px);
            b.Sign.SetHome(new Vector3(0, b.Def.HasDoor ? b.H - 2.5f * px : b.H + 12, 0));
            b.Sign.Say(b.Title, 0);
        }

        /// <summary>Playtesting: build the first <paramref name="count"/> lots at <paramref name="level"/> (not saved).</summary>
        public void DevBuild(int count, int level)
        {
            while (buildings.Count < count && NextLot != null) Add(NextLot, Mathf.Clamp(level, 1, Building.MaxLevel));
        }

        public void Save()
        {
            PlayerPrefs.SetString("town", string.Join(",", buildings.ConvertAll(b => b.Def.id + ":" + b.Level)));
        }

        /// <summary>Lays the town out leftwards from <paramref name="rightEdge"/> and runs takings.</summary>
        public void Tick(float dt, float rightEdge, bool blackout)
        {
            float x = rightEdge - Gap;
            foreach (var b in buildings)
            {
                b.X = x - b.W / 2;
                x -= b.W + Gap;
                b.Sr.transform.position = Desk.W(new Vector2(b.X, d.FloorY), 1);
                if ((b.IncomeT -= dt) > 0) continue;
                b.IncomeT = Random.Range(45f, 70f) / b.Level;
                if (b.Def.id == "bank")
                {
                    // The bank pays interest on savings instead of takings.
                    int interest = Mathf.Clamp((int)(d.Money * d.Tune.interestRate * b.Level), 300, d.Tune.interestCap);
                    d.SpawnCoinAt(new Vector2(b.DoorX, d.FloorY - 20), interest);
                    b.Sign.Say("이자 지급!", 1.5f);
                }
                else if (b.Def.income > 0) d.SpawnCoinAt(new Vector2(b.DoorX, d.FloorY - 20), b.Def.income * b.Level);
            }
            lotX = x - 20;
            lotSign.enabled = NextLot != null && lotX > 40;
            lotSign.transform.position = Desk.W(new Vector2(lotX, d.FloorY), 1);
            tip.SetHome(new Vector3(0, 60, 0));
            if (blackout != dim)
            {
                dim = blackout;
                foreach (var b in buildings) b.Sr.color = dim ? new Color(0.45f, 0.45f, 0.55f) : Color.white;
            }
        }

        /// <summary>Walkable tops (shop roofs, the park's tree and bench) as surface segments.</summary>
        public void AddSegs(List<Seg> segs)
        {
            foreach (var b in buildings)
                for (int k = 0; k < b.Tops.Count; k++)
                {
                    float left = b.X - b.W / 2;
                    segs.Add(new Seg(TopBase - b.Index * 8 - k, left + b.Tops[k].x0, left + b.Tops[k].x1, d.FloorY - b.Tops[k].topFromFloor));
                }
        }

        /// <summary>Fixed anchor for a town surface id (buildings never move once laid out).</summary>
        public bool SegPos(int id, out Vector2 pos)
        {
            pos = default;
            if (id > TopBase) return false;
            int index = (TopBase - id) / 8;
            if (index >= buildings.Count) return false;
            pos = new Vector2(buildings[index].X, d.FloorY);
            return true;
        }

        public Building At(Vector2 p)
        {
            foreach (var b in buildings)
                if (new Rect(b.X - b.W / 2, d.FloorY - b.H, b.W, b.H).Contains(p)) return b;
            return null;
        }

        public bool OverLot(Vector2 p) => lotSign.enabled && LotRect.Contains(p);

        /// <summary>A building whose door is within <paramref name="range"/> of <paramref name="p"/>.</summary>
        public Building DoorNear(Vector2 p, float range)
        {
            foreach (var b in buildings)
                if (b.Def.HasDoor && Mathf.Abs(b.DoorX - p.x) < range && Mathf.Abs(d.FloorY - p.y) < 60) return b;
            return null;
        }

        /// <summary>A random place to visit, favouring buildings this tenant is a fan of.</summary>
        public Building PickVisit(TenantType who)
        {
            if (buildings.Count == 0) return null;
            Building best = null;
            float bestScore = -1;
            foreach (var b in buildings)
            {
                float score = Random.value * (Array.IndexOf(b.Def.fans ?? new string[0], who.name) >= 0 ? 3 : 1);
                if (score > bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        public void Hover(Vector2 p, int money)
        {
            if (tip.Visible) return;
            if (OverLot(p)) tip.Say($"{NextLot.name} 짓기 ₩{NextLot.cost:N0} (클릭)", 1.5f);
            var b = At(p);
            if (b != null) b.Sign.Say(b.Level >= Building.MaxLevel ? b.Title + " 최고 등급" : $"{b.Title} → 업그레이드 ₩{d.Tune.ShopUpgradeCost(b.Def.cost, b.Level):N0}", 1.5f);
        }

        /// <summary>Click on the lot or a building. Returns the money spent (0 if nothing bought).</summary>
        public int Click(Vector2 p, int money, bool save = true)
        {
            if (OverLot(p))
            {
                var def = NextLot;
                if (money < def.cost) { tip.Say($"₩{def.cost - money:N0} 더 필요해요", 1.6f); return 0; }
                Add(def, 1);
                if (save) Save();
                Bubble.Popup($"{def.name} 오픈!", Desk.W(new Vector2(buildings[buildings.Count - 1].X, d.FloorY - 80)));
                return def.cost;
            }
            var b = At(p);
            if (b == null) return 0;
            if (b.Level >= Building.MaxLevel) { b.Sign.Say(b.Title + " 최고 등급", 1.5f); return 0; }
            int cost = d.Tune.ShopUpgradeCost(b.Def.cost, b.Level);
            if (money < cost) { b.Sign.Say($"₩{cost - money:N0} 더 필요해요", 1.6f); return 0; }
            b.Level++;
            Draw(b);
            if (save) Save();
            Bubble.Popup($"{b.Def.name} 리모델링!", Desk.W(new Vector2(b.X, d.FloorY - b.H - 30)));
            return cost;
        }

        /// <summary>After resetting the sign text the bubble shows the title again.</summary>
        public void RefreshSigns()
        {
            foreach (var b in buildings) if (!b.Sign.Visible) b.Sign.Say(b.Title, 0);
        }
    }

    /// <summary>Procedural pixel art for town buildings.</summary>
    public static class TownArt
    {
        public static readonly Dictionary<char, Color> Pal = new Dictionary<char, Color>
        {
            { 'o', Pixels.Ink }, { 'w', new Color32(0xf4, 0xf0, 0xe8, 255) }, { 'g', new Color32(0x90, 0xd0, 0xf0, 255) },
            { 'd', new Color32(0x7a, 0x4a, 0x2e, 255) }, { 'B', new Color32(0x40, 0x80, 0xe0, 255) }, { 'G', new Color32(0x50, 0xc0, 0x70, 255) },
            { 'c', new Color32(0xe8, 0xd4, 0xb0, 255) }, { 'b', new Color32(0x8a, 0x5a, 0x34, 255) }, { 's', new Color32(0xa8, 0xb0, 0xbc, 255) },
            { 'R', new Color32(0xd8, 0x40, 0x40, 255) }, { 'p', new Color32(0x6a, 0x4a, 0xa8, 255) }, { 'P', new Color32(0xff, 0x60, 0xc0, 255) },
            { 'r', new Color32(0xe0, 0x50, 0x50, 255) }, { 'y', new Color32(0xff, 0xe0, 0x60, 255) }, { 'L', new Color32(0x40, 0xa0, 0x50, 255) },
            { 'l', new Color32(0x60, 0xc8, 0x60, 255) }, { 't', new Color32(0x6a, 0x44, 0x28, 255) },
            { 'n', new Color32(0x30, 0x40, 0x70, 255) }, { 'C', new Color32(0x40, 0xd0, 0xe0, 255) },
        };

        public static readonly string[] ForSale =
        {
            "wwwwwwwwwww", "wRRRRRRRRRw", "wRwwwwwwwRw", "wRRRRRRRRRw", "wwwwwwwwwww", ".....b.....", ".....b.....", ".....b.....", ".....b.....",
        };

        /// <summary>Rows for building <paramref name="id"/> at <paramref name="level"/>; fills walkable tops (pixels).</summary>
        public static string[] Rows(string id, int level, List<(float x0, float x1, float top)> tops)
        {
            tops.Clear();
            switch (id)
            {
                case "store": return Shop(30, 20, 'w', 'B', 'G', level, tops);
                case "cafe": return Shop(28, 22, 'c', 'b', 'w', level, tops);
                case "gym": return Shop(34, 24, 's', 'R', 'w', level, tops);
                case "karaoke": return Shop(26, 28, 'p', 'P', 'y', level, tops);
                case "chicken": return Shop(30, 20, 'w', 'y', 'r', level, tops);
                case "pcbang": return Shop(32, 22, 'n', 'C', 'w', level, tops);
                case "bank": return Shop(32, 26, 's', 'B', 'y', level, tops);
                case "flower": return Shop(24, 20, 'w', 'l', 'P', level, tops);
                case "library": return Shop(36, 24, 'c', 'b', 'y', level, tops);
                default: return Park(level, tops);
            }
        }

        /// <summary>A flat-roofed shop: sign band, shop window, door; awning at level 2, bulbs at 3.</summary>
        static string[] Shop(int w, int h, char wall, char sign, char stripe, int level, List<(float, float, float)> tops)
        {
            var g = Grid(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = wall;
                    if (y == 0 || y == h - 1 || x == 0 || x == w - 1) c = 'o';
                    else if (y <= 4) c = (y == 1 || y == 4) ? sign : (x % 4 == 0 ? stripe : sign);
                    else if (x >= 2 && x <= w * 0.55f && y >= 7 && y <= h - 4) c = (x == 2 || y == 7 || y == h - 4) ? 'o' : 'g';
                    else if (x >= w * 0.66f && x <= w * 0.66f + 5 && y >= h - 10) c = y == h - 10 ? 'o' : 'd';
                    g[y][x] = c;
                }
            if (level >= 2)
                for (int x = 1; x < w - 1; x++) { g[5][x] = (x / 2) % 2 == 0 ? 'r' : 'w'; g[6][x] = x % 2 == 0 ? 'r' : '.'; }
            if (level >= 3)
                for (int x = 1; x < w - 1; x += 3) g[0][x] = 'y';
            tops.Add((1, w - 1, h));
            return Rows(g);
        }

        /// <summary>A park: a climbable tree, a bench, flowers that multiply with level.</summary>
        static string[] Park(int level, List<(float, float, float)> tops)
        {
            const int w = 42, h = 30;
            var g = Grid(w, h);
            // Tree canopy: a blob centred at (9, 7).
            for (int y = 0; y < 15; y++)
                for (int x = 0; x < 19; x++)
                {
                    float dx = (x - 9) / 9f, dy = (y - 7) / 7.5f;
                    if (dx * dx + dy * dy <= 1) g[y][x] = (x + y) % 5 == 0 ? 'l' : 'L';
                }
            for (int y = 12; y < h; y++) for (int x = 8; x <= 10; x++) g[y][x] = 't';
            // Bench.
            for (int x = 22; x <= 34; x++) { g[h - 9][x] = 'b'; g[h - 6][x] = 'b'; }
            for (int y = h - 5; y < h; y++) { g[y][23] = 'o'; g[y][33] = 'o'; }
            // Lamp post.
            for (int y = 8; y < h; y++) g[y][39] = 'o';
            g[7][38] = g[7][39] = g[7][40] = 'y';
            // Flowers.
            string[] flowers = { "P", "y", "r" };
            for (int i = 0; i < level * 4; i++) g[h - 1][(i * 7 + 13) % (w - 2) + 1] = flowers[i % 3][0];
            tops.Add((2, 17, h));        // tree top
            tops.Add((22, 35, 9));       // bench seat back (from the floor)
            return Rows(g);
        }

        static char[][] Grid(int w, int h)
        {
            var g = new char[h][];
            for (int y = 0; y < h; y++) { g[y] = new char[w]; for (int x = 0; x < w; x++) g[y][x] = '.'; }
            return g;
        }

        static string[] Rows(char[][] g)
        {
            var rows = new string[g.Length];
            for (int i = 0; i < g.Length; i++) rows[i] = new string(g[i]);
            return rows;
        }
    }
}
