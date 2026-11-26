using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// The overlay: polls the real desktop (windows, mouse) each frame, turns it into walkable
    /// segments, runs tenants and coins, and owns the economy (money, floors, move-ins).
    /// Desktop space is points with a top-left origin; world space is the same with y flipped (see <see cref="W"/>).
    /// </summary>
    public class Desk : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => new GameObject("Desk").AddComponent<Desk>();

        public Tuning Tune { get; private set; }
        public TenantBook Book { get; private set; }
        public Vector2 Mouse { get; private set; }
        public bool MouseDown { get; private set; }
        public float ScreenW { get; private set; }
        public float ScreenH { get; private set; }
        public float FloorY { get; private set; }
        /// <summary>Top of the usable area, just under the menu bar.</summary>
        public float TopY { get; private set; }
        public Vector2 Door { get; private set; }
        /// <summary>Too many windows open: tenants complain.</summary>
        public bool Crowded => wins.Count >= Tune.crowdedWindows;
        /// <summary>Real-world late night: tenants get sleepy.</summary>
        public bool Night { get { int hr = System.DateTime.Now.Hour; return hr >= 23 || hr < 6; } }

        readonly List<WinRect> wins = new List<WinRect>();
        readonly List<Seg> segs = new List<Seg>();
        readonly List<Tenant> tenants = new List<Tenant>();
        readonly List<Coin> coins = new List<Coin>();
        readonly List<Vector2> jumpSpots = new List<Vector2>();
        readonly float[] scr = new float[6], mouse = new float[3], buf = new float[5 * 64];
        Camera cam;
        SpriteRenderer house;
        Bubble moneyLabel, houseTip;
        bool overlay, clickThrough = true, wasDown;
        float spawnT, logT, refreshT, quitAt, houseW, houseH;
        int money, floors, screen;
        float rainT, rainLeft, lightT;
        Director director;
        SpriteRenderer pad, antenna;
        readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        Sprite lightSprite;

        public IReadOnlyList<Tenant> Tenants => tenants;
        /// <summary>Tall enough for a rooftop helipad (the helicopter parks there).</summary>
        public bool HasHelipad => floors >= Tune.helipadFloors;
        /// <summary>Top of the helipad (or of the roof when there is none).</summary>
        public Vector2 PadTop => new Vector2(Door.x, RoofTop);
        float RoofTop => RoofY - (HasHelipad ? 4 * Tune.pixel : 0);
        readonly List<Transform> drops = new List<Transform>();

        /// <summary>A passing shower: tenants hurry, complain, cats hate it.</summary>
        public bool Raining => rainLeft > 0;

        int Capacity => floors * Tune.tenantsPerFloor;
        int UpgradeCost => Tune.upgradeBase * floors * floors;
        int UnlockedTypes => Mathf.Clamp(Tune.startTypes + Tune.typesPerFloor * (floors - Tune.startFloors), 1, Book.types.Length);
        Rect HouseBounds => new Rect(Door.x - houseW / 2, FloorY - houseH, houseW, houseH);

        public static Vector3 W(Vector2 p, float z = 0) => new Vector3(p.x, -p.y, z);
        static string Won(int v) => "₩" + v.ToString("N0");

        void Awake()
        {
            Tune = Tuning.Load();
            Book = TenantBook.Load();
            var args = System.Environment.GetCommandLineArgs();
            // --quit-after N: test runs close themselves instead of covering the screen.
            int qi = System.Array.IndexOf(args, "--quit-after");
            if (qi >= 0 && qi + 1 < args.Length && float.TryParse(args[qi + 1], out var secs)) quitAt = secs;
            if (System.Array.IndexOf(args, "--reset") >= 0) PlayerPrefs.DeleteAll();
            rainT = System.Array.IndexOf(args, "--rain") >= 0 ? 4 : Random.Range(Tune.rainMin, Tune.rainMax);
            // --screen N: which monitor to live on (1 = main). Each monitor can run its own copy.
            int si = System.Array.IndexOf(args, "--screen");
            if (si >= 0 && si + 1 < args.Length && int.TryParse(args[si + 1], out var sn)) screen = sn - 1;
            money = PlayerPrefs.GetInt("money", 0);
            floors = PlayerPrefs.GetInt("floors", Tune.startFloors);
            // --floors N: jump straight to an N-floor building (playtesting all tenant types).
            int fi = System.Array.IndexOf(args, "--floors");
            if (fi >= 0 && fi + 1 < args.Length && int.TryParse(args[fi + 1], out var fl)) floors = Mathf.Clamp(fl, 1, Tune.maxFloors);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Tune.fps;
            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            house = new GameObject("House").AddComponent<SpriteRenderer>();
            moneyLabel = Bubble.Create(house.transform, Vector3.zero);
            houseTip = Bubble.Create(house.transform, Vector3.zero);
            pad = new GameObject("Helipad").AddComponent<SpriteRenderer>();
            pad.sprite = Props.Make(Props.Helipad, Tune.pixel, new Vector2(0.5f, 0));
            pad.sortingOrder = 2;
            antenna = new GameObject("Antenna").AddComponent<SpriteRenderer>();
            antenna.sprite = Props.Make(Props.Antenna, Tune.pixel, new Vector2(0.5f, 0));
            antenna.sortingOrder = 1;
            lightSprite = Props.Dot(Tune.pixel * 4);
            BuildHouse();
            // --event NAME: fire one event a few seconds in (heli, ufo, coins, drill, fireworks).
            int ei = System.Array.IndexOf(args, "--event");
            director = new Director(this, ei >= 0 && ei + 1 < args.Length ? args[ei + 1] : null);
        }

        void BuildHouse()
        {
            var rows = Pixels.House(floors);
            house.sprite = Pixels.Make(rows, Pixels.HousePalette, Tune.pixel);
            houseW = rows[0].Length * Tune.pixel;
            houseH = rows.Length * Tune.pixel;
            // The money sign hangs on the facade under the roof, so tenants on the roof stay visible.
            moneyLabel.SetHome(new Vector3(0, houseH - 9 * Tune.pixel, 0));
            houseTip.SetHome(new Vector3(0, houseH + 60, 0));
            moneyLabel.Say(Won(money), 0);

            // One light per window (the ground floor's middle one is the door), flickered in Update.
            foreach (var l in lights) Destroy(l.gameObject);
            lights.Clear();
            for (int f = 0; f < floors; f++)
                foreach (int col in new[] { 4, 11, 18 })
                {
                    if (f == floors - 1 && col == 11) continue;
                    var l = new GameObject("Light").AddComponent<SpriteRenderer>();
                    l.sprite = lightSprite;
                    l.sortingOrder = 1;
                    l.color = new Color32(0xff, 0xe0, 0x7a, 255);
                    l.transform.SetParent(house.transform, false);
                    // Window centre in sprite pixels (x from left, y from top), relative to the bottom-centre pivot.
                    float px = col + 2 - 13, py = 6 + f * 7 + 4;
                    l.transform.localPosition = new Vector3(px * Tune.pixel, houseH - py * Tune.pixel, -0.1f);
                    lights.Add(l);
                }
        }

        /// <summary>Windows switch on and off on their own; more are dark late at night.</summary>
        void FlickerLights(float dt)
        {
            if ((lightT -= dt) > 0) return;
            lightT = 0.8f;
            float dark = Night ? 0.65f : 0.2f;
            var on = new Color32(0xff, 0xe0, 0x7a, 255);
            var off = new Color32(0x3a, 0x35, 0x50, 255);
            foreach (var l in lights)
                if (Random.value < 0.12f) l.color = Random.value < dark ? off : on;
        }

        void Save()
        {
            PlayerPrefs.SetInt("money", money);
            PlayerPrefs.SetInt("floors", floors);
            PlayerPrefs.Save();
            moneyLabel.Say(Won(money), 0);
        }

        void Update()
        {
            if (!overlay)
            {
                overlay = Bridge.MakeOverlay(screen);
                if (overlay) Debug.Log($"[desk] overlay on screen {screen + 1}. Monitors:\n{Bridge.ScreenList()}");
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (quitAt > 0 && Time.realtimeSinceStartup > quitAt) Application.Quit();
            // The player recreates its Metal surface (opaque again) after resizes; keep re-clearing it.
            if ((refreshT -= dt) <= 0) { refreshT = 0.25f; Bridge.Refresh(); }

            ReadDesktop();
            SpawnTenants(dt);
            Weather(dt);
            director.Tick(dt);
            FlickerLights(dt);

            foreach (var c in coins) c.Tick(this, dt);
            Tenant hoverT = null;
            bool holding = false;
            foreach (var t in tenants)
            {
                t.Tick(dt);
                if (t.Bounds.Contains(Mouse)) hoverT = t;
                holding |= t.Held;
            }
            StartChats();
            KeepApart();
            LayoutBubbles();
            Coin hoverC = hoverT ? null : coins.Find(c => c.Bounds.Contains(Mouse));
            bool hoverH = !hoverT && !hoverC && HouseBounds.Contains(Mouse);
            HandleClicks(hoverT, hoverC, hoverH, ref holding);

            bool through = !(hoverT || hoverC || hoverH || holding);
            if (through != clickThrough) { clickThrough = through; Bridge.SetClickThrough(through); }

            if ((logT -= dt) <= 0)
            {
                logT = 10;
                Debug.Log($"[desk] edges y: {string.Join(",", segs.ConvertAll(x => x.y.ToString("0")))}");
                Debug.Log($"[desk] windows {wins.Count} segs {segs.Count} tenants {tenants.Count}/{Capacity} coins {coins.Count} money {money} floors {floors}");
            }
        }

        void ReadDesktop()
        {
            Bridge.Screen(scr);
            ScreenW = scr[0];
            ScreenH = scr[1];
            TopY = scr[3];
            FloorY = scr[3] + scr[5];
            cam.orthographicSize = ScreenH / 2;
            cam.transform.position = new Vector3(ScreenW / 2, -ScreenH / 2, -10);
            Door = new Vector2(ScreenW - 140, FloorY);
            house.transform.position = W(Door, 1);
            pad.enabled = HasHelipad;
            pad.transform.position = W(new Vector2(Door.x, RoofY), 0.9f);
            antenna.enabled = floors >= Tune.antennaFloors;
            antenna.transform.position = W(new Vector2(Door.x + houseW / 2 - 6 * Tune.pixel, RoofY + 3 * Tune.pixel), 0.95f);

            Bridge.Mouse(mouse);
            Mouse = new Vector2(mouse[0], mouse[1]);
            MouseDown = ((int)mouse[2] & 1) != 0;

            int n = Bridge.Windows(buf, 64);
            wins.Clear();
            for (int i = 0; i < n; i++)
                wins.Add(new WinRect((int)buf[i * 5], buf[i * 5 + 1], buf[i * 5 + 2], buf[i * 5 + 3], buf[i * 5 + 4]));
            Surfaces.Compute(wins, FloorY, ScreenW, segs);
            segs.Add(new Seg(Surfaces.Roof, Door.x - houseW / 2 + 5 * Tune.pixel, Door.x + houseW / 2 - 5 * Tune.pixel, RoofTop));
        }

        float RoofY => FloorY - houseH;

        void Weather(float dt)
        {
            if (rainLeft <= 0 && (rainT -= dt) <= 0)
            {
                rainLeft = Tune.rainDuration;
                rainT = Random.Range(Tune.rainMin, Tune.rainMax);
                if (drops.Count == 0)
                {
                    var sprite = Pixels.RainDrop(Tune.pixel);
                    for (int i = 0; i < 110; i++)
                    {
                        var r = new GameObject("Drop").AddComponent<SpriteRenderer>();
                        r.sprite = sprite;
                        r.sortingOrder = 5;
                        r.transform.position = W(new Vector2(Random.Range(0, ScreenW), Random.Range(TopY, FloorY)));
                        drops.Add(r.transform);
                    }
                }
                foreach (var t in drops) t.gameObject.SetActive(true);
                Bubble.Popup("소나기!", W(new Vector2(ScreenW / 2, TopY + 60)));
            }
            if (rainLeft <= 0) return;
            rainLeft -= dt;
            foreach (var t in drops)
            {
                var p = t.position + new Vector3(-60, -650, 0) * dt;
                if (-p.y > FloorY) p = W(new Vector2(Random.Range(0, ScreenW + 100), TopY));
                t.position = p;
            }
            if (rainLeft <= 0) foreach (var t in drops) t.gameObject.SetActive(false);
        }

        /// <summary>New tenants arrive by moving truck, a few per trip.</summary>
        void SpawnTenants(float dt)
        {
            spawnT += dt;
            if (tenants.Count >= Capacity || spawnT < Tune.spawnEvery || director.TruckBusy) return;
            spawnT = 0;
            director.SendTruck(Mathf.Min(3, Capacity - tenants.Count));
        }

        /// <summary>Called by the truck: the next tenant steps out at <paramref name="at"/>.</summary>
        public void MoveIn(Vector2 at)
        {
            if (tenants.Count >= Capacity) return;
            // Variety first: the unlocked type with the fewest tenants moves in next.
            TenantType pick = null;
            int best = int.MaxValue;
            for (int i = 0; i < UnlockedTypes; i++)
            {
                var type = Book.types[i];
                int count = tenants.FindAll(x => x.Type == type).Count;
                if (count < best || (count == best && Random.value < 0.5f)) { best = count; pick = type; }
            }
            var t = new GameObject("Tenant " + pick.name).AddComponent<Tenant>();
            t.Init(this, pick, at);
            tenants.Add(t);
        }

        /// <summary>Tenants on the floor just ahead of the truck's front (<paramref name="frontX"/>) jump aside.</summary>
        public void ScareFromTruck(float frontX, int dir)
        {
            foreach (var t in tenants)
            {
                float ahead = (t.P.x - frontX) * dir;
                if (ahead > -10 && ahead < 40) t.DodgeTruck(dir);
            }
        }

        /// <summary>The UFO showed up: alien tenants get homesick.</summary>
        public void AlienCheer(bool ufo)
        {
            if (!ufo) return;
            foreach (var t in tenants)
                if (t.Type.special == "teleport") t.Cheer();
        }

        /// <summary>A bonus coin falling from the top of the screen at <paramref name="x"/>.</summary>
        public void DropCoin(float x)
        {
            if (coins.Count >= Tune.maxCoins + 20) return;
            var c = new GameObject("Coin").AddComponent<Coin>();
            // Start below the topmost edge (a maximised window's top) so coins rain down onto the desktop.
            c.Init(this, new Vector2(x, TopY + 50), Tune.bonusCoin, new Vector2(Random.Range(-30f, 30f), 60));
            coins.Add(c);
        }

        void StartChats()
        {
            for (int i = 0; i < tenants.Count; i++)
                for (int j = i + 1; j < tenants.Count; j++)
                {
                    Tenant a = tenants[i], b = tenants[j];
                    if (a.CanChat && b.CanChat && a.On == b.On && Mathf.Abs(a.P.x - b.P.x) < 28)
                    {
                        bool people = !a.Type.IsAnimal && !b.Type.IsAnimal;
                        float r = Random.value;
                        var outcome = r < Tune.danceChance ? Tenant.Outcome.Dance
                            : (r -= Tune.danceChance) < Tune.highfiveChance ? Tenant.Outcome.HighFive
                            : r - Tune.highfiveChance < Tune.argueChance && people ? Tenant.Outcome.Argue
                            : Tenant.Outcome.None;
                        // Scripted two-liners (a squabble when they're going to argue); otherwise both greet.
                        // Either way the second speaker waits for the first bubble before answering.
                        var lines = outcome == Tenant.Outcome.Argue ? Book.fights : Book.convos;
                        var convo = people && (outcome == Tenant.Outcome.Argue || Random.value < 0.6f)
                            ? lines[Random.Range(0, lines.Length)] : null;
                        a.Chat(b, outcome, convo?.a, 0);
                        b.Chat(a, outcome, convo?.b, 2.2f);
                    }
                }
        }

        void HandleClicks(Tenant hoverT, Coin hoverC, bool hoverH, ref bool holding)
        {
            bool pressed = MouseDown && !wasDown;
            wasDown = MouseDown;
            if (hoverH && !houseTip.Visible)
                houseTip.Say(floors >= Tune.maxFloors ? "최고층이에요!" : $"{floors + 1}층 증축 {Won(UpgradeCost)} (클릭)", 1.5f);
            if (!pressed) return;
            if (hoverT) { hoverT.Grab(Mouse); holding = true; }
            else if (hoverC)
            {
                money += hoverC.Value;
                Bubble.Popup("+" + Won(hoverC.Value), hoverC.transform.position + Vector3.up * 20);
                coins.Remove(hoverC);
                Destroy(hoverC.gameObject);
                Save();
            }
            else if (hoverH) Upgrade();
        }

        void Upgrade()
        {
            if (floors >= Tune.maxFloors) { houseTip.Say("더는 못 올려요!"); return; }
            int cost = UpgradeCost;
            if (money < cost) { houseTip.Say($"{Won(cost - money)} 더 필요해요"); return; }
            money -= cost;
            floors++;
            BuildHouse();
            Save();
            houseTip.Say($"뚝딱! {floors}층 완공", 2.5f);
            Bubble.Popup(floors == Tune.helipadFloors ? "옥상 헬기장 오픈!" : floors == Tune.antennaFloors ? "옥상에 안테나 설치!" : "새 세입자 모집 중!", W(Door - new Vector2(0, houseH + 60)));
            director.Fireworks(4f);
            spawnT = Tune.spawnEvery;
        }

        /// <summary>Drops a rent coin worth <paramref name="value"/> at <paramref name="at"/>.</summary>
        public void SpawnCoin(Vector2 at, int value)
        {
            if (coins.Count >= Tune.maxCoins) return;
            var c = new GameObject("Coin").AddComponent<Coin>();
            c.Init(this, at, value);
            coins.Add(c);
        }

        readonly List<Bubble> shown = new List<Bubble>();

        /// <summary>Stacks overlapping speech bubbles upward so every line stays readable.</summary>
        void LayoutBubbles()
        {
            shown.Clear();
            foreach (var t in tenants)
                if (t.Bubble.Visible) { t.Bubble.Lift = 0; shown.Add(t.Bubble); }
            shown.Sort((x, y) => x.Base.y.CompareTo(y.Base.y)); // lowest first; later ones climb over
            for (int i = 0; i < shown.Count; i++)
                for (int pass = 0; pass < shown.Count; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        Rect a = shown[i].Area, o = shown[j].Area;
                        if (!a.Overlaps(o)) continue;
                        shown[i].Lift += o.yMax - a.yMin + 2;
                        moved = true;
                    }
                    if (!moved) break;
                }
        }

        /// <summary>Ladder routes up from <paramref name="from"/> (see <see cref="Surfaces.ClimbOptions"/>).</summary>
        public void ClimbOptions(Seg from, List<(Seg to, float lo, float hi)> into)
        {
            Surfaces.ClimbOptions(segs, from, 40, TopY - 1, into);
            if (RoofFull) into.RemoveAll(o => o.to.win == Surfaces.Roof);
        }

        /// <summary>The roof only fits a couple of tenants.</summary>
        bool RoofFull => tenants.FindAll(t => t.On == Surfaces.Roof).Count >= Tune.roofCapacity;

        /// <summary>
        /// Keeps tenants standing on the same edge from overlapping: free ones closer than
        /// <c>personalSpace</c> are nudged apart (chatting and dancing pairs may stand close).
        /// </summary>
        void KeepApart()
        {
            for (int i = 0; i < tenants.Count; i++)
                for (int j = i + 1; j < tenants.Count; j++)
                {
                    Tenant a = tenants[i], b = tenants[j];
                    if (!a.Free || !b.Free || a.On != b.On) continue;
                    float dx = b.P.x - a.P.x, gap = Tune.personalSpace;
                    if (Mathf.Abs(dx) >= gap) continue;
                    float push = (gap - Mathf.Abs(dx)) * 0.25f * (dx >= 0 ? 1 : -1);
                    if (dx == 0) push = Random.value < 0.5f ? 1 : -1;
                    a.Nudge(-push);
                    b.Nudge(push);
                }
        }

        /// <summary>Everyone free within <paramref name="range"/> of the DJ starts dancing.</summary>
        public void Party(Tenant dj, float range, float seconds)
        {
            foreach (var t in tenants)
                if (t != dj && Vector2.Distance(t.P, dj.P) < range) t.JoinDance(seconds);
        }

        /// <summary>The chef's food cheers up everyone within <paramref name="range"/>.</summary>
        public void Feed(Tenant chef, float range)
        {
            foreach (var t in tenants)
                if (t != chef && Vector2.Distance(t.P, chef.P) < range) t.Treat();
        }

        /// <summary>Nearest cat on the same edge as <paramref name="dog"/>, within <paramref name="range"/>.</summary>
        public Tenant NearestCat(Tenant dog, float range)
        {
            Tenant best = null;
            foreach (var t in tenants)
            {
                float dist = Mathf.Abs(t.P.x - dog.P.x);
                if (t.Type.IsCat && t.On == dog.On && dist < range) { best = t; range = dist; }
            }
            return best;
        }

        /// <summary>A random standing spot on some visible window edge (for teleporting aliens).</summary>
        public bool RandomSpot(out Vector2 spot)
        {
            jumpSpots.Clear();
            foreach (var s in segs)
                if (s.win >= 0 && s.x1 - s.x0 > 40) jumpSpots.Add(new Vector2(Random.Range(s.x0 + 15, s.x1 - 15), s.y));
            if (jumpSpots.Count == 0) jumpSpots.Add(new Vector2(Random.Range(40, ScreenW - 40), FloorY));
            spot = jumpSpots[Random.Range(0, jumpSpots.Count)];
            return true;
        }

        /// <summary>Nearest resting coin on edge <paramref name="on"/> within <paramref name="range"/> points.</summary>
        public Coin CoinNear(Vector2 p, int on, float range)
        {
            Coin best = null;
            float bestD = range;
            foreach (var c in coins)
            {
                float dist = Mathf.Abs(c.P.x - p.x);
                if (c.On == on && dist < bestD) { best = c; bestD = dist; }
            }
            return best;
        }

        /// <summary>A random reachable edge above <paramref name="p"/> for a jumper, as the landing point.</summary>
        public bool JumpTarget(Vector2 p, float maxUp, out Vector2 target)
        {
            jumpSpots.Clear();
            foreach (var s in segs)
            {
                if (s.y > p.y - 30 || s.y < p.y - maxUp || s.x1 - s.x0 < 30) continue;
                if (s.win == Surfaces.Roof && RoofFull) continue;
                float x = Mathf.Clamp(p.x, s.x0 + 10, s.x1 - 10);
                if (Mathf.Abs(x - p.x) <= Tune.jumpReach) jumpSpots.Add(new Vector2(x + Random.Range(-20f, 20f), s.y));
            }
            target = jumpSpots.Count > 0 ? jumpSpots[Random.Range(0, jumpSpots.Count)] : default;
            return jumpSpots.Count > 0;
        }

        /// <summary>Top-left of window <paramref name="id"/> (the floor counts as a window).</summary>
        public bool WinPos(int id, out Vector2 pos)
        {
            if (id == Surfaces.Floor) { pos = new Vector2(0, FloorY); return true; }
            if (id == Surfaces.Roof) { pos = new Vector2(Door.x, RoofTop); return true; }
            foreach (var w in wins)
                if (w.id == id) { pos = new Vector2(w.x, w.y); return true; }
            pos = default;
            return false;
        }

        /// <summary>The visible segment of window <paramref name="id"/> under the feet at <paramref name="p"/>.</summary>
        public bool SegAt(int id, Vector2 p, out Seg seg)
        {
            foreach (var s in segs)
                if (s.win == id && Mathf.Abs(s.y - p.y) < 1.5f && p.x >= s.x0 - 1 && p.x <= s.x1 + 1) { seg = s; return true; }
            seg = default;
            return false;
        }

        /// <summary>Highest segment crossed while falling from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public bool Land(Vector2 a, Vector2 b, out Seg best)
        {
            bool found = false;
            best = default;
            foreach (var s in segs)
            {
                if (s.y < a.y || s.y > b.y) continue;
                float k = b.y > a.y ? (s.y - a.y) / (b.y - a.y) : 0;
                float x = Mathf.Lerp(a.x, b.x, k);
                if (x < s.x0 || x > s.x1) continue;
                if (!found || s.y < best.y) { best = s; found = true; }
            }
            return found;
        }
    }
}
