using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>Facial expression, painted into a pose's eye/mouth placeholders.</summary>
    public enum Face { Normal, Blink, Happy, Shock, Sleep, Yawn }

    /// <summary>
    /// Tenant poses as ASCII pixel art with face placeholders: U/L = upper/lower eye pixel,
    /// M = mouth, E = cat eye. <see cref="Pose"/> fills them in for a <see cref="Face"/>.
    /// Palette: h hair, s skin, c shirt/fur, p pants, o shoes, f phone, g screen, n cat nose,
    /// k accent (gear, dog ears), x dog nose.
    /// </summary>
    public static class Poses
    {
        static readonly string[] Head =
        {
            "...hhhhh...",
            "..hhhhhhh..",
            "..hssUssU..",
            "..hssLssL..",
            "..ssssMMs..",
            "...sssss...",
        };
        static readonly string[] ArmsDown = { "..ccccccc..", ".scccccccs.", ".scccccccs.", "..ccccccc.." };
        static readonly string[] Stand = { "...pp.pp...", "...oo.oo..." };

        static readonly Dictionary<string, string[]> Person = new Dictionary<string, string[]>
        {
            ["idle"] = Join(Head, ArmsDown, Stand),
            ["walkA"] = Join(Head, ArmsDown, new[] { "..pp...pp..", ".oo.....oo." }),
            ["walkB"] = Join(Head, ArmsDown, new[] { "....ppp....", "....ooo...." }),
            ["fall"] = new[]
            {
                "...hhhhh...", "..hhhhhhh..", "s.hssUssU.s", "s.hssLssL.s", "s.ssssMMs.s", ".s.sssss.s.",
                "..ccccccc..", "..ccccccc..", "..ccccccc..", "..ccccccc..", "..pp...pp..", "..oo...oo..",
            },
            ["wave"] = Join(new[] { Head[0], Head[1], Head[2], "..hssLssL.s", "..ssssMMs.s", "...sssss.s." },
                new[] { "..ccccccc..", ".sccccccc..", ".sccccccc..", "..ccccccc.." }, Stand),
            ["phone"] = Join(new[] { Head[0], Head[1], Head[2], "..hssLssLf.", "..ssssMMsg.", "...sssss.s." },
                new[] { "..ccccccs..", ".sccccccc..", ".sccccccc..", "..ccccccc.." }, Stand),
            // Sitting on an edge: the last two rows hang below it (see SitPivot).
            ["sit"] = Join(Head, new[] { "..ccccccc..", ".scccccccs.", ".scccccccs.", "..cpppppp..", ".......pp..", ".......oo.." }),
            // Climbing a ladder: hands alternate high, legs alternate.
            ["climbA"] = Join(new[] { Head[0], Head[1], Head[2], "..hssLssL.s", "..ssssMMs.s", "...sssss.s." },
                new[] { "..ccccccc..", ".sccccccc..", "..ccccccc..", "..ccccccc.." }, new[] { "..pp...pp..", "..o.....o.." }),
            ["climbB"] = Join(new[] { Head[0], Head[1], "s.hssUssU..", "s.hssLssL..", ".sssssMMs..", "...sssss..." },
                new[] { "..ccccccc..", "..cccccccs.", "..ccccccc..", "..ccccccc.." }, new[] { "....ppp....", "....o.o...." }),
            ["pushA"] = new[] { "...........hh.", "..........hhsU", "ppppcccccccss.", "o.......s.....", "o.......s....." },
            ["pushB"] = new[] { "..............", "...........hh.", "..........hhsL", "ppppcccccccss.", "o......ss....." },
        };

        static readonly Dictionary<string, string[]> Cat = new Dictionary<string, string[]>
        {
            ["idle"] = new[] { "......c..c..", "......cccc..", "......ccEc..", "c.....cccn..", "c...ccccc...", ".c.ccccccc..", "..cccccccc..", "..cccccccc.." },
            ["walkA"] = new[] { "........c..c", "........cccc", "........ccEc", "c.......cccn", ".cccccccccc.", "..ccccccccc.", "..cc.....cc.", "..c.......c." },
            ["walkB"] = new[] { "........c..c", "........cccc", "........ccEc", "c.......cccn", ".cccccccccc.", "..ccccccccc.", "....cc.cc...", ".....c..c..." },
            ["sleep"] = new[] { "............", "............", "............", "....cccc....", "..cccccccc..", ".ccccccccccc", ".cccccccccc.", ".cccccccccc." },
            ["fall"] = new[] { "........c..c", "........cccc", "........ccEc", "c.......cccn", ".cccccccccc.", "c.cccccccc.c", "c..........c", "............" },
            ["groom"] = new[] { "......c..c..", "......cccc..", "......cc-c..", "c.....cccnc.", "c...ccccc.c.", ".c.ccccccc..", "..cccccccc..", "..cccccccc.." },
            ["stretch"] = new[] { "..............c..c", "c.............cccc", ".ccc.........ccEcn", "..cccccc...cccccc.", "..cc..ccccccc.....", "..cc...........cc." },
        };

        static readonly Dictionary<string, string[]> Dog = new Dictionary<string, string[]>
        {
            ["idle"] = new[] { "......cccc..", ".....kcccEc.", ".....kcccccx", "......cccc..", "c...ccccc...", ".c.ccccccc..", "..cccccccc..", "..cc..cc.c.." },
            ["wag"] = new[] { "......cccc..", ".....kcccEc.", "c....kcccccx", ".c....cccc..", "..c.ccccc...", "...cccccccc.", "..cccccccc..", "..cc..cc.c.." },
            ["walkA"] = new[] { "........cccc", ".......kcccE", "c......kccccx", ".c......cccc", "..ccccccccc.", "..ccccccccc.", "..cc.....cc.", "..c.......c." },
            ["walkB"] = new[] { "........cccc", ".......kcccE", ".c.....kccccx", "c.......cccc", "..ccccccccc.", "..ccccccccc.", "....cc.cc...", ".....c..c..." },
            ["sleep"] = new[] { "............", "............", "............", ".........cc.", "..cccccckc-cx", ".ccccccccccc.", ".cccccccccc..", ".cc.cc.cc.cc." },
            ["fall"] = new[] { "........cccc", ".......kcccE", ".......kccccx", "c.......cccc", ".cccccccccc.", "c.cccccccc.c", "c..........c", "............" },
        };

        // Robot vacuum: a dome on wheels with one eye. c body, k accent light, E eye.
        static readonly Dictionary<string, string[]> Robot = new Dictionary<string, string[]>
        {
            ["idle"] = new[] { "....kk.....", "...cccc....", ".ccccEccc..", "ccccccccccc", "cpppppppppc", ".oo.....oo." },
            ["walkA"] = new[] { "....kk.....", "...cccc....", ".ccccEccc..", "ccccccccccc", "cpppppppppc", ".o.o...o.o." },
            ["walkB"] = new[] { "...k.k.....", "...cccc....", ".ccccEccc..", "ccccccccccc", "cpppppppppc", "..oo...oo.." },
            ["sleep"] = new[] { "...........", "...cccc....", ".cccc-ccc..", "ccccccccccc", "cpppppppppc", ".oo.....oo." },
            ["fall"] = new[] { "..k...k....", "...cccc....", ".ccccEccc..", "ccccccccccc", "cpppppppppc", "o.o.....o.o" },
        };

        // Penguin, facing right. c black, w belly, k beak and feet, E eye.
        static readonly Dictionary<string, string[]> Penguin = new Dictionary<string, string[]>
        {
            ["idle"] = new[] { "...ccc...", "..ccccc..", "..cccEc..", "..ccccckk", ".cccwwwc.", "ccwwwwwcc", "c.wwwwwc.", "..wwwww..", "..wwwww..", "...www...", "..kk.kk.." },
            ["walkA"] = new[] { "...ccc...", "..ccccc..", "..cccEc..", "..ccccckk", ".cccwwwc.", "ccwwwwwcc", "c.wwwwwc.", "..wwwww..", "..wwwww..", "...www...", ".kk...kk." },
            ["walkB"] = new[] { "...ccc...", "..ccccc..", "..cccEc..", "..ccccckk", ".cccwwwc.", "ccwwwwwcc", "c.wwwwwc.", "..wwwww..", "..wwwww..", "...www...", "...kkk..." },
            ["sleep"] = new[] { "...ccc...", "..ccccc..", "..ccc-c..", "..ccccckk", ".cccwwwc.", "ccwwwwwcc", "c.wwwwwc.", "..wwwww..", "..wwwww..", "...www...", "..kk.kk.." },
            ["fall"] = new[] { "...ccc...", "c.ccccc.c", "c.cccEc.c", ".cccccckk", "..ccwwwc.", "..wwwwwc.", "..wwwwww.", "..wwwww..", "..wwwww..", "...www...", ".kk...kk." },
            // Belly slide.
            ["slide"] = new[] { ".........ccc..", "......ccccEck.", "ccwwwwwwwccckk", "kcwwwwwwwwcc..", "..cccccccc...." },
        };

        // Accessories painted over person poses (rows from the top of the head; '.' keeps what is there).
        // k = the type's accent colour, w = white, h = hair.
        static readonly Dictionary<string, string[]> Gear = new Dictionary<string, string[]>
        {
            ["tie"] = new[] { "", "", "", "", "", "", ".....k.....", ".....k.....", ".....kk...." },
            ["shades"] = new[] { "", "", "....kkkkkk.", ".....kk.kk." },
            ["headphones"] = new[] { "...kkkkk...", "..k........", ".kk........", ".kk........" },
            ["cap"] = new[] { "..kkkkk....", "..kkkkkkkkk" },
            ["longhair"] = new[] { "", "", ".h.........", ".hh........", ".hh........", ".hh........", ".hh........" },
            ["apron"] = new[] { "", "", "", "", "", "", "", "...wwwww...", "...wwwww...", "...wwwww..." },
            ["antenna"] = new[] { "..k.....k..", "...k...k..." },
            ["backpack"] = new[] { "", "", "", "", "", "", "kk.........", "kk.........", "kk........." },
        };

        static Dictionary<string, string[]> Of(string kind) => kind switch
        {
            "cat" => Cat,
            "dog" => Dog,
            "robot" => Robot,
            "penguin" => Penguin,
            _ => Person,
        };

        /// <summary>Pivot for the "sit" pose: two rows of legs dangle below the edge.</summary>
        public static readonly Vector2 SitPivot = new Vector2(0.5f, 2f / 12f);

        public static bool Has(string pose, string kind) => Of(kind).ContainsKey(pose);

        /// <summary>
        /// Pose rows for a <paramref name="kind"/> (person, cat, dog) with the face placeholders painted
        /// for <paramref name="face"/> and any <paramref name="gear"/> drawn on top.
        /// </summary>
        public static string[] Pose(string pose, Face face, string kind, string[] gear = null)
        {
            var src = Of(kind)[pose];
            bool closed = face == Face.Blink || face == Face.Happy || face == Face.Sleep || face == Face.Yawn;
            char upper = closed ? 's' : 'e';
            char mouth = face switch { Face.Happy => 'm', Face.Shock or Face.Yawn => 'e', _ => 's' };
            char catEye = closed ? '-' : 'e';
            var rows = new string[src.Length];
            for (int i = 0; i < src.Length; i++)
                rows[i] = src[i].Replace('U', upper).Replace('L', 'e').Replace('M', mouth).Replace('E', catEye);
            // Gear lines up with the standing head (rows 0-5); push-ups lie down, so they go bare.
            if (gear != null && kind == "person" && !pose.StartsWith("push"))
                foreach (var g in gear)
                    if (Gear.TryGetValue(g, out var over)) Paint(rows, over);
            return rows;
        }

        static void Paint(string[] rows, string[] over)
        {
            for (int y = 0; y < over.Length && y < rows.Length; y++)
            {
                var r = rows[y].ToCharArray();
                for (int x = 0; x < over[y].Length && x < r.Length; x++)
                    if (over[y][x] != '.') r[x] = over[y][x];
                rows[y] = new string(r);
            }
        }

        // Emotes shown beside the head. r red, w white, y yellow, b blue.
        public static readonly Dictionary<string, string[]> Emotes = new Dictionary<string, string[]>
        {
            ["!"] = new[] { "yy", "yy", "yy", "..", "yy" },
            ["?"] = new[] { ".www.", "w...w", "...w.", "..w..", ".....", "..w.." },
            ["heart"] = new[] { ".rr.rr.", "rrrrrrr", "rrrrrrr", ".rrrrr.", "..rrr..", "...r..." },
            ["anger"] = new[] { "rr...rr", "r.r.r.r", "...r...", "r.r.r.r", "rr...rr" },
            ["note"] = new[] { "..wwww", "..w..w", "..w..w", "www.www", "www.www" },
            ["sweat"] = new[] { "..b..", ".bbb.", "bbbbb", "bbbbb", ".bbb." },
            ["star"] = new[] { "..y..", "yyyyy", ".yyy.", "y...y" },
            ["flash"] = new[] { "...w...", "...w...", "www.www", "...w...", "...w..." },
            ["yoyo"] = new[] { "..w..", "..w..", "..w..", ".rrr.", "ryyyr", "rwwyr", ".rrr." },
            ["coin"] = new[] { ".yyy.", "ywwwy", "ywywy", "ywwwy", ".yyy." },
            ["steam"] = new[] { ".w..w.", "w..w..", ".w..w.", "w..w.." },
        };

        public static Dictionary<char, Color> EmotePalette => new Dictionary<char, Color>
        {
            { 'r', new Color32(0xe8, 0x40, 0x50, 255) }, { 'w', Color.white },
            { 'y', new Color32(0xff, 0xd8, 0x40, 255) }, { 'b', new Color32(0x60, 0xb8, 0xf0, 255) },
        };

        static string[] Join(params string[][] parts)
        {
            var all = new List<string>();
            foreach (var p in parts) all.AddRange(p);
            return all.ToArray();
        }
    }
}
