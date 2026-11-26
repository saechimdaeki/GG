using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>Pixel art for event vehicles and building extras. Pivots are noted per sprite.</summary>
    public static class Props
    {
        static readonly Dictionary<char, Color> Pal = new Dictionary<char, Color>
        {
            { 'k', Pixels.Ink }, { 'b', new Color32(0xd8, 0x40, 0x40, 255) }, { 'g', new Color32(0x90, 0xd8, 0xff, 255) },
            { 's', new Color32(0xb8, 0xc0, 0xd0, 255) }, { 'G', new Color32(0x70, 0xe0, 0x80, 255) }, { 'y', new Color32(0xff, 0xd8, 0x40, 255) },
            { 'w', new Color32(0xf4, 0xf0, 0xe8, 255) }, { 'o', new Color32(0x50, 0x48, 0x58, 255) }, { 'r', new Color32(0xe0, 0x60, 0x40, 255) },
            { 'R', new Color32(0xff, 0x40, 0x40, 255) }, { 'P', new Color32(0xff, 0x50, 0xd0, 255) },
            { 'i', new Color32(0xff, 0xd0, 0xe8, 255) }, { 'c', new Color32(0xe8, 0xb0, 0x60, 255) }, { 'O', new Color32(0xff, 0x90, 0x30, 255) },
            { '1', new Color32(0xff, 0x50, 0x50, 255) }, { '2', new Color32(0xff, 0xa0, 0x40, 255) }, { '3', new Color32(0xff, 0xe8, 0x50, 255) },
            { '4', new Color32(0x60, 0xd0, 0x60, 255) }, { '5', new Color32(0x50, 0x90, 0xf0, 255) }, { '6', new Color32(0xa0, 0x60, 0xe0, 255) },
        };

        // Helicopter, facing left; two rotor frames. Pivot: centre.
        static readonly string[] HeliBody =
        {
            "........k...............",
            ".....bbbbbbb............",
            "...bbgggbbbbbbbbbbbbbb.k",
            "..bbggggbbbbbbbbbb...kkk",
            "..bbbbbbbbbbbb.......k..",
            "...bbbbbbbbb............",
            "....k....k..............",
            "..kkkkkkkkkk............",
        };
        public static string[] HeliA => Prepend("..kkkkkkkkkkkkkk........", HeliBody);
        public static string[] HeliB => Prepend(".......kkkk.............", HeliBody);

        // Flying saucer; lights blink between frames. Pivot: centre.
        public static readonly string[] UfoA =
        {
            "......GGGGGG......", ".....GGGGGGGG.....", "...ssssssssssss...", ".ssssssssssssssss.",
            "ssyssssyssssyssssy", ".ssssssssssssssss.", "...ssssssssssss...",
        };
        public static readonly string[] UfoB =
        {
            "......GGGGGG......", ".....GGGGGGGG.....", "...ssssssssssss...", ".ssssssssssssssss.",
            "sssyssssyssssyssss", ".ssssssssssssssss.", "...ssssssssssss...",
        };

        // Moving truck, facing right. Pivot: bottom centre (wheels on the floor).
        public static readonly string[] Truck =
        {
            "oooooooooooooooooo..........",
            "owwwwwwwwwwwwwwwwo..........",
            "owwyyywwwwwwwwwwwo.ooooo....",
            "owwyyywwwwwwwwwwwo.oggggo...",
            "owwwwwwwwwwwwwwwwo.ogggggo..",
            "owwwwwwwwwwwwwwwwooorrrrrro.",
            "owwwwwwwwwwwwwwwwoorrrrrrrro",
            "oooooooooooooooooorrrrrrrrro",
            "..kkk........kkk......kkk...",
            "..kkk........kkk......kkk...",
        };

        // Rooftop extras. Pivot: bottom centre (sits on the roof).
        public static readonly string[] Helipad = { "yyyyyyyyyyyyyy", "yykyyyyyyyykyy", "yykkkkkkkkkkyy", "yykyyyyyyyykyy" };
        public static readonly string[] Antenna = { ".R.", ".k.", ".k.", ".k.", ".k.", ".k.", ".k.", "kkk" };

        // "GG" neon sign for tall apartments. Pivot: centre.
        public static readonly string[] Neon =
        {
            "PPPPP.PPPPP", "P.....P....", "P.PPP.P.PPP", "P...P.P...P", "PPPPP.PPPPP",
        };

        // Black limousine for celebrity visits, facing right. Pivot: bottom centre.
        public static readonly string[] Limo =
        {
            "........oooooooooooo..........",
            ".......oggggoggggoggo.........",
            "oooooooooooooooooooooooooooo..",
            "okkkkkkkkkkkkkkkkkkkkkkkkkkkoo",
            "okkkkkkkkkkkkkkkkkkkkkkkkkkkky",
            "oooooooooooooooooooooooooooooo",
            "...kkk..................kkk...",
            "...kkk..................kkk...",
        };

        public static readonly string[] Cloud =
        {
            "....wwwww.......", "..wwwwwwwww.....", ".wwwwwwwwwwwww..", "wwwwwwwwwwwwwwww", ".wwwwwwwwwwwwww.", "...wwwwwwwwww...",
        };

        public static readonly string[] Snowman =
        {
            "...kkk...", "...kkk...", "..kkkkk..", "..wwwww..", ".wwkwkww.", ".wwwwOOw.", ".wwwwwww.", "..wwwww..",
            ".wwwwwww.", "wwwwkwwww", "wwwwwwwww", "wwwwkwwww", ".wwwwwww.",
        };

        /// <summary>A half-ring of six colour bands. Pivot: bottom centre.</summary>
        public static string[] Rainbow()
        {
            const int r = 32, w = r * 2 + 1;
            var rows = new string[r];
            for (int y = 0; y < r; y++)
            {
                var row = new char[w];
                for (int x = 0; x < w; x++)
                {
                    float dist = Mathf.Sqrt((x - r) * (x - r) + (r - y) * (r - y));
                    int band = (int)(r - dist);
                    row[x] = dist <= r && band < 12 ? (char)('1' + band / 2) : '.';
                }
                rows[y] = new string(row);
            }
            return rows;
        }

        /// <summary>Ice-cream van: the moving truck repainted pink with a cone on the side.</summary>
        public static string[] IceTruck => System.Array.ConvertAll(Truck, r => r.Replace('w', 'i').Replace('y', 'c'));

        // Small plane, facing right. Pivot: centre.
        public static readonly string[] Plane =
        {
            "...b................", "...bb..........kk...", "bbbbbbbbbbbbbbbbbbg.", ".bbbbbbbbbbbbbbbbbbb", "......bbbbbb........",
        };

        // Pigeon: wings up, wings down, perched. Pivot: bottom centre.
        public static readonly string[] BirdA = { "s...s", ".sss.", "..s.." };
        public static readonly string[] BirdB = { ".....", "sssss", "..s.." };
        public static readonly string[] BirdSit = { "..ss.", ".sssk", ".sss." };

        /// <summary>A repeating zig-zag lightning bolt tile (pivot top) for a tiled SpriteRenderer.</summary>
        public static Sprite BoltTile(float pixel)
        {
            var rows = new[] { "..y....", "...y...", "....y..", "...y...", "..y....", ".y....." };
            var sp = Pixels.Make(rows, Pal, pixel);
            sp.texture.wrapMode = TextureWrapMode.Repeat;
            return Sprite.Create(sp.texture, sp.rect, new Vector2(0.5f, 1f), 1f / pixel, 0, SpriteMeshType.FullRect);
        }

        /// <summary>An easel holding a small random painting (sky, hill, sun). Pivot: bottom centre.</summary>
        public static string[] Easel(int seed)
        {
            var rnd = new System.Random(seed);
            char sky = "bgy"[rnd.Next(3)], hill = "GLc"[rnd.Next(3)], sun = "yRw"[rnd.Next(3)];
            int sunX = 2 + rnd.Next(5), hillY = 4 + rnd.Next(2);
            var rows = new List<string>();
            rows.Add("oooooooooo");
            for (int y = 1; y < 8; y++)
            {
                var r = new char[10];
                for (int x = 0; x < 10; x++)
                    r[x] = x == 0 || x == 9 ? 'o' : (y == 2 && (x == sunX || x == sunX + 1)) ? sun : y >= hillY + (x % 3 == 0 ? 0 : 1) ? hill : sky;
                rows.Add(new string(r));
            }
            rows.Add("oooooooooo");
            rows.Add("...t..t...");
            rows.Add("..t....t..");
            rows.Add(".t......t.");
            return rows.ToArray();
        }

        public static Dictionary<char, Color> EaselPalette => new Dictionary<char, Color>
        {
            { 'o', new Color32(0x7a, 0x50, 0x30, 255) }, { 't', new Color32(0x8a, 0x5a, 0x34, 255) }, { 'b', new Color32(0x80, 0xc0, 0xff, 255) },
            { 'g', new Color32(0xff, 0xc0, 0x90, 255) }, { 'y', new Color32(0xff, 0xe0, 0x80, 255) }, { 'G', new Color32(0x60, 0xc0, 0x60, 255) },
            { 'L', new Color32(0x40, 0x90, 0x70, 255) }, { 'c', new Color32(0xc0, 0x90, 0x60, 255) }, { 'R', new Color32(0xff, 0x60, 0x40, 255) },
            { 'w', Color.white },
        };

        // A friendly ghost. Pivot: centre.
        public static readonly string[] Ghost =
        {
            "..wwwww..", ".wwwwwww.", "wwkwwwkww", "wwwwwwwww", "wwwkkkwww", "wwwwwwwww", "wwwwwwwww", "w.ww.ww.w",
        };

        // A parcel. Pivot: bottom centre.
        public static readonly string[] Box = { "ccccccc", "cccyccc", "yyyyyyy", "cccyccc", "cccyccc", "ccccccc" };

        /// <summary>Fish-shaped-bun van: the truck in brown with an orange logo.</summary>
        public static string[] FoodTruck => System.Array.ConvertAll(Truck, r => r.Replace('w', 'c').Replace('y', 'O'));

        public static Sprite Make(string[] rows, float pixel, Vector2 pivot) => Pixels.Make(rows, Pal, pixel, pivot);

        /// <summary>A 1-pixel-wide vertical tile (rope or tractor beam) for a tiled SpriteRenderer.</summary>
        public static Sprite Tile(Color c, int width, float pixel)
        {
            var row = new string('x', width);
            var sp = Pixels.Make(new[] { row, row }, new Dictionary<char, Color> { { 'x', c } }, pixel);
            sp.texture.wrapMode = TextureWrapMode.Repeat;
            return Sprite.Create(sp.texture, sp.rect, new Vector2(0.5f, 1f), 1f / pixel, 0, SpriteMeshType.FullRect);
        }

        /// <summary>A white dot, tinted per spark.</summary>
        public static Sprite Dot(float pixel) => Pixels.Make(new[] { "w" }, new Dictionary<char, Color> { { 'w', Color.white } }, pixel, new Vector2(0.5f, 0.5f));

        static string[] Prepend(string first, string[] rest)
        {
            var all = new string[rest.Length + 1];
            all[0] = first;
            rest.CopyTo(all, 1);
            return all;
        }
    }
}
