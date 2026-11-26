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
            { 'R', new Color32(0xff, 0x40, 0x40, 255) },
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
