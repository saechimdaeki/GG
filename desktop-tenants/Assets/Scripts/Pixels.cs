using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>Builds point-filtered sprites from ASCII pixel art. Pivot is bottom-centre (the feet).</summary>
    public static class Pixels
    {
        public static readonly Color Ink = new Color32(0x2b, 0x22, 0x33, 255);

        public static Sprite Make(string[] rows, Dictionary<char, Color> pal, float pixel, Vector2? pivot = null)
        {
            int w = 0, h = rows.Length;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = x < rows[y].Length ? rows[y][x] : '.';
                    px[(h - 1 - y) * w + x] = pal.TryGetValue(c, out var col) ? col : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0f), 1f / pixel);
        }

        public static readonly string[] Coin =
        {
            ".ooooo.",
            "oyyyyyo",
            "oyywyyo",
            "oywwwyo",
            "oyywyyo",
            "oyyyyyo",
            ".ooooo.",
        };

        public static Dictionary<char, Color> CoinPalette => new Dictionary<char, Color>
        {
            { 'o', new Color32(0x9a, 0x6a, 0x10, 255) }, { 'y', new Color32(0xff, 0xd0, 0x40, 255) }, { 'w', new Color32(0xff, 0xf4, 0xc0, 255) },
        };

        /// <summary>One repeating section of a wooden ladder, for a tiled SpriteRenderer.</summary>
        public static Sprite LadderTile(float pixel)
        {
            var rows = new[] { "o.....o", "ooooooo", "o.....o", "o.....o" };
            var pal = new Dictionary<char, Color> { { 'o', new Color32(0xa8, 0x70, 0x38, 255) } };
            var sp = Make(rows, pal, pixel);
            sp.texture.wrapMode = TextureWrapMode.Repeat;
            return Sprite.Create(sp.texture, sp.rect, new Vector2(0.5f, 0f), 1f / pixel, 0, SpriteMeshType.FullRect);
        }

        public static Sprite RainDrop(float pixel) =>
            Make(new[] { "b", "b", "b" }, new Dictionary<char, Color> { { 'b', new Color(0.55f, 0.78f, 1f, 0.75f) } }, pixel * 0.7f);

        public static readonly string[] Zzz = { "wwww", "..w.", ".w..", "wwww" };

        /// <summary>Window columns across the apartment: it gets wider as it gets taller.</summary>
        public static int HouseCols(int floors) => 3 + (floors - 1) / 3;

        /// <summary>
        /// A walk-up apartment with <paramref name="floors"/> floors and <see cref="HouseCols"/> window
        /// columns: roof, brick walls, lit windows, a door in the middle of the ground floor.
        /// </summary>
        public static string[] House(int floors)
        {
            int cols = HouseCols(floors), W = 7 * cols + 5, H = 6 + floors * 7 + 3;
            int door0 = W / 2 - 2;
            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var r = new char[W];
                for (int x = 0; x < W; x++)
                {
                    char c = '.';
                    if (y < 6) { int inset = 5 - y; if (x >= inset && x < W - inset) c = y == 0 ? 'o' : 'r'; }
                    else if (x == 1 || x == W - 2 || y == H - 1) c = 'o';
                    else if (x > 1 && x < W - 2)
                    {
                        c = 'b';
                        int f = (y - 6) / 7, k = (y - 6) % 7;
                        bool winRow = f < floors && k >= 2 && k <= 5;
                        bool winCol = x >= 4 && (x - 4) % 7 <= 3 && (x - 4) / 7 < cols;
                        if (winRow && winCol) c = 'y';
                        int level = floors - f;
                        if (k == 6 && (level == 12 || level == 15)) c = 't';
                        if (y >= H - 6 && x >= door0 && x <= door0 + 3) c = 'd';
                    }
                    r[x] = c;
                }
                rows[y] = new string(r);
            }
            return rows;
        }

        public static Dictionary<char, Color> HousePalette => new Dictionary<char, Color>
        {
            { 'o', Ink }, { 'r', new Color32(0xc8, 0x4b, 0x3c, 255) }, { 'b', new Color32(0xe8, 0xc6, 0x9a, 255) },
            { 'y', new Color32(0xff, 0xe0, 0x7a, 255) }, { 'd', new Color32(0x7a, 0x4a, 0x2e, 255) },
            { 't', new Color32(0x73, 0x8a, 0x95, 255) },
        };

    }
}
