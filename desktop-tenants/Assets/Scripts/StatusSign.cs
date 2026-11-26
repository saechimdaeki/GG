using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// An LED billboard standing on two steel legs on the apartment roof: a pixel weather icon,
    /// the time in amber, and CPU / memory as 10-segment LED bars that go green → yellow → red.
    /// Blinks red when the CPU runs hot; goes dark in a blackout.
    /// </summary>
    public class StatusSign
    {
        const int Segs = 10;
        const float BoardW = 46, BoardH = 27; // art pixels

        static readonly Color Amber = new Color32(0xff, 0xb8, 0x40, 255);
        static readonly Color Green = new Color32(0x60, 0xf0, 0x80, 255);
        static readonly Color Cyan = new Color32(0x60, 0xd8, 0xff, 255);
        static readonly Color Yellow = new Color32(0xff, 0xe0, 0x50, 255);
        static readonly Color Red = new Color32(0xff, 0x50, 0x50, 255);
        static readonly Color Off = new Color32(0x2a, 0x30, 0x4a, 255);

        readonly Desk d;
        readonly float px;
        readonly Transform root;
        readonly SpriteRenderer board, icon, chainL, chainR;
        readonly TextMesh time, cpuLabel, cpuText, memLabel, memText;
        readonly SpriteRenderer[] cpuBar = new SpriteRenderer[Segs], memBar = new SpriteRenderer[Segs];
        readonly Dictionary<Weather.Kind, Sprite> icons = new Dictionary<Weather.Kind, Sprite>();
        readonly Sprite rainbowIcon;
        float cpu, memShare, msgT;
        readonly TextMesh msg;

        /// <summary>A notice is currently replacing the clock row.</summary>
        public bool ShowingMessage => msgT > 0;

        /// <summary>Shows <paramref name="text"/> across the top row for <paramref name="seconds"/> (house prices, news).</summary>
        public void Message(string text, float seconds)
        {
            msg.text = text;
            msgT = seconds;
        }
        bool hot;

        public StatusSign(Desk desk)
        {
            d = desk;
            px = desk.Tune.pixel;
            root = new GameObject("Status sign").transform;
            board = Sprite(root, Pixels.Make(Board(), Pal, px, new Vector2(0, 1)), 24);
            chainL = Sprite(null, Chain(), 23);
            chainR = Sprite(null, Chain(), 23);
            chainL.drawMode = chainR.drawMode = SpriteDrawMode.Tiled;
            foreach (Weather.Kind k in System.Enum.GetValues(typeof(Weather.Kind)))
                icons[k] = Pixels.Make(Icon(k), Pal, px * 0.8f, new Vector2(0.5f, 0.5f));
            rainbowIcon = Pixels.Make(RainbowIcon, Pal, px * 0.8f, new Vector2(0.5f, 0.5f));
            icon = Sprite(root, icons[Weather.Kind.Clear], 26);
            icon.transform.localPosition = new Vector3(8 * px, -8 * px, 0);

            time = Text("--:--", Amber, 12, TextAnchor.MiddleLeft, new Vector2(15, 8));
            msg = Text("", Yellow, 11, TextAnchor.MiddleCenter, new Vector2(BoardW / 2, 8));
            cpuLabel = Text("CPU", Green, 10, TextAnchor.MiddleLeft, new Vector2(3, 16.5f));
            memLabel = Text("MEM", Cyan, 10, TextAnchor.MiddleLeft, new Vector2(3, 22.5f));
            cpuText = Text("0%", Green, 10, TextAnchor.MiddleRight, new Vector2(BoardW - 2.5f, 16.5f));
            memText = Text("0G", Cyan, 10, TextAnchor.MiddleRight, new Vector2(BoardW - 2.5f, 22.5f));
            var seg = Props.Dot(1);
            for (int i = 0; i < Segs; i++)
            {
                cpuBar[i] = Segment(seg, 12.5f + i * 1.9f, 16.5f);
                memBar[i] = Segment(seg, 12.5f + i * 1.9f, 22.5f);
            }
        }

        // ---------------------------------------------------------------- building blocks

        static readonly Dictionary<char, Color> Pal = new Dictionary<char, Color>
        {
            { 'o', Pixels.Ink }, { 'n', new Color32(0x16, 0x19, 0x2c, 255) }, { 'g', new Color32(0x5a, 0x60, 0x7a, 255) },
            { 'r', new Color32(0xb0, 0xb8, 0xc8, 255) }, { 'y', new Color32(0xff, 0xd8, 0x40, 255) }, { 'w', new Color32(0xf0, 0xf0, 0xf8, 255) },
            { 's', new Color32(0x90, 0x98, 0xb0, 255) }, { 'b', new Color32(0x60, 0xb0, 0xff, 255) }, { 'd', new Color32(0x50, 0x54, 0x70, 255) },
            { '1', new Color32(0xff, 0x50, 0x50, 255) }, { '2', new Color32(0xff, 0xa0, 0x40, 255) }, { '3', new Color32(0xff, 0xe8, 0x50, 255) },
            { '4', new Color32(0x60, 0xd0, 0x60, 255) }, { '5', new Color32(0x50, 0x90, 0xf0, 255) },
        };

        /// <summary>Dark LED panel with a steel frame and rivets in the corners.</summary>
        static string[] Board()
        {
            int w = (int)BoardW, h = (int)BoardH;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var r = new char[w];
                for (int x = 0; x < w; x++)
                {
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    bool frame = x == 1 || y == 1 || x == w - 2 || y == h - 2;
                    bool rivet = (x == 2 || x == w - 3) && (y == 2 || y == h - 3);
                    r[x] = edge ? 'o' : rivet ? 'r' : frame ? 'g' : 'n';
                }
                rows[y] = new string(r);
            }
            // Divider under the clock row.
            var div = rows[12].ToCharArray();
            for (int x = 3; x < w - 3; x++) div[x] = 'd';
            rows[12] = new string(div);
            return rows;
        }

        /// <summary>A steel billboard leg with cross-bracing, tiled upward from the roof.</summary>
        Sprite Chain()
        {
            var sp = Pixels.Make(new[] { "gsg", "g.g", "gsg", "g.g" }, Pal, px);
            sp.texture.wrapMode = TextureWrapMode.Repeat;
            return UnityEngine.Sprite.Create(sp.texture, sp.rect, new Vector2(0.5f, 0), 1f / px, 0, SpriteMeshType.FullRect);
        }

        static string[] Icon(Weather.Kind k) => k switch
        {
            Weather.Kind.Clear => new[] { "y...y...y", ".y..y..y.", "...yyy...", "..yyyyy..", "yyyyyyyyy", "..yyyyy..", "...yyy...", ".y..y..y.", "y...y...y" },
            Weather.Kind.Cloudy => new[] { ".........", ".........", "...www...", "..wwwww..", ".wwwwwww.", "wwwwwwwww", ".wwwwwww.", ".........", "........." },
            Weather.Kind.Rain => new[] { "...www...", "..wwwww..", ".wwwwwww.", "wwwwwwwww", ".........", ".b..b..b.", "b..b..b..", ".b..b..b.", "........." },
            Weather.Kind.Storm => new[] { "...sss...", "..sssss..", ".sssssss.", "sssssssss", "....yy...", "...yy....", "..yyyy...", "....yy...", "...y....." },
            Weather.Kind.Snow => new[] { "...www...", "..wwwww..", ".wwwwwww.", "wwwwwwwww", ".........", "w...w...w", "..w...w..", "w...w...w", "........." },
            _ => new[] { ".........", "wwwwww...", "......w..", "wwwwwww..", ".........", "..wwwwww.", "........w", "..wwwwww.", "........." },
        };

        static readonly string[] RainbowIcon = { "..11111..", ".1222221.", "12333332.", "23444443.", "34555554.", "45.....5.", "5.......5", ".........", "........." };

        static SpriteRenderer Sprite(Transform parent, Sprite s, int order)
        {
            var r = new GameObject("Part").AddComponent<SpriteRenderer>();
            if (parent) r.transform.SetParent(parent, false);
            r.sprite = s;
            r.sortingOrder = order;
            return r;
        }

        TextMesh Text(string s, Color c, float size, TextAnchor anchor, Vector2 at)
        {
            var t = new GameObject("LED text").AddComponent<TextMesh>();
            t.transform.SetParent(root, false);
            t.transform.localPosition = new Vector3(at.x * px, -at.y * px, 0);
            t.font = Bubble.SystemFont;
            t.fontSize = 64;
            t.characterSize = size * 10f / 64f;
            t.anchor = anchor;
            t.color = c;
            t.text = s;
            var r = t.GetComponent<MeshRenderer>();
            r.sharedMaterial = Bubble.SystemFont.material;
            r.sortingOrder = 26;
            return t;
        }

        SpriteRenderer Segment(Sprite dot, float x, float y)
        {
            var r = Sprite(root, dot, 25);
            r.transform.localPosition = new Vector3(x * px, -y * px, 0);
            r.transform.localScale = new Vector3(1.4f * px, 3.2f * px, 1);
            return r;
        }

        // ---------------------------------------------------------------- live data

        /// <summary>New readings (every couple of seconds).</summary>
        public void Set(Weather.Kind sky, bool rainbow, string clock, float cpuPercent, float memUsed, float memTotal)
        {
            cpu = cpuPercent;
            memShare = memTotal > 0 ? memUsed / memTotal : 0;
            hot = cpu >= d.Tune.hotCpu;
            icon.sprite = rainbow ? rainbowIcon : icons[sky];
            time.text = clock;
            cpuText.text = $"{cpu:0}%";
            memText.text = $"{memUsed:0}/{memTotal:0}G";
            Fill(cpuBar, cpu / 100f, false);
            Fill(memBar, memShare, true);
        }

        static void Fill(SpriteRenderer[] bar, float share, bool mem)
        {
            int lit = Mathf.Clamp(Mathf.CeilToInt(share * Segs - 0.05f), 0, Segs);
            for (int i = 0; i < Segs; i++)
            {
                // Each segment keeps its own colour band, so a fuller bar shows the climb into the red.
                var band = i < 6 ? (mem ? Cyan : Green) : i < 8 ? Yellow : Red;
                bar[i].color = i < lit ? band : Off;
            }
        }

        /// <summary>
        /// Every frame: stand on the apartment roof (legs tall enough for the helipad and helicopter to
        /// fit underneath), rattle when the CPU runs hot, go dark in a blackout.
        /// </summary>
        public void Tick(float dt, bool blackout)
        {
            float w = BoardW * px, h = BoardH * px, legs = 16 * px;
            float shake = hot ? Random.Range(-1.2f, 1.2f) : 0;
            float roof = d.RoofY;
            var topLeft = new Vector2(d.Door.x - w / 2 + shake, roof - legs - h);
            root.position = Desk.W(topLeft);
            // Legs from the roof up to the board's bottom corners.
            // Legs stand on the roof even when the board is wider than the building.
            float span = Mathf.Min(w / 2 - 5 * px, d.HouseWidth / 2 - 7 * px);
            foreach (var (leg, x) in new[] { (chainL, d.Door.x - span), (chainR, d.Door.x + span) })
            {
                leg.transform.position = Desk.W(new Vector2(x, roof + px), -0.1f);
                leg.size = new Vector2(3 * px, legs + px);
            }

            bool lit = !blackout;
            bool blinkOff = hot && Mathf.Repeat(Time.time, 0.6f) < 0.25f;
            msgT -= dt;
            bool notice = msgT > 0;
            foreach (var t in new[] { cpuLabel, memLabel, cpuText, memText }) t.gameObject.SetActive(lit);
            time.gameObject.SetActive(lit && !notice);
            icon.enabled = lit && !notice;
            // Notices blink gently so they read as fresh news.
            msg.gameObject.SetActive(lit && notice && Mathf.Repeat(Time.time, 0.9f) < 0.75f);
            foreach (var s in cpuBar) s.enabled = lit && !blinkOff;
            foreach (var s in memBar) s.enabled = lit;
            cpuText.color = cpuLabel.color = hot ? Red : Green;
        }
    }
}
