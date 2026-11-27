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
        readonly List<Camera> cameras = new List<Camera>();
        readonly List<DesktopDisplay> displays = new List<DesktopDisplay>();
        readonly float[] displayRect = new float[5];
        Camera cam;
        bool externalScreens;
        int nextDisplay;
        float bindDeadline;
        SpriteRenderer house;
        Bubble moneyLabel, houseTip;
        bool overlay, clickThrough = true, wasDown;
        /// <summary>Started with --floors/--town: nothing is written to the save.</summary>
        bool playtest;
        string forcedWeather;
        float spawnT, logT, refreshT, quitAt, houseW, houseH;
        int money, floors, screen;
        float lightT, statT, hotT, hotCool, memCool;
        readonly float[] stats = new float[3];
        StatusSign board;
        public Weather Sky { get; private set; }
        /// <summary>Latest CPU busy % and memory use (GB) for chatter and events.</summary>
        public float Cpu => stats[0];
        public float MemUsed => stats[1];
        public float MemTotal => stats[2];
        public float Wind => Sky?.Wind ?? 0;
        Director director;
        SpriteRenderer pad, antenna;
        readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        Sprite lightSprite;

        public IReadOnlyList<Tenant> Tenants => tenants;
        public Town Town { get; private set; }
        public int Money => money;
        /// <summary>Power cut: every light in town is off.</summary>
        public bool Blackout => blackoutT > 0;
        float blackoutT, shakeT, hoverTime, tickerT;
        Tenant hoverLast;
        SpriteRenderer neon;
        Bubble ticker;
        int tickerPos;
        /// <summary>Tall enough for a rooftop helipad (the helicopter parks there).</summary>
        public bool HasHelipad => floors >= Tune.helipadFloors;
        /// <summary>Top of the helipad (or of the roof when there is none).</summary>
        public Vector2 PadTop => new Vector2(Door.x, RoofTop);
        float RoofTop => RoofY - (HasHelipad ? 4 * Tune.pixel : 0);

        /// <summary>A passing shower: tenants hurry, complain, cats hate it.</summary>
        public bool Raining => Sky != null && Sky.Wet;

        int Capacity => floors * Tune.tenantsPerFloor;
        int UpgradeCost => Tune.ApartmentCost(floors);
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
            // --weather KIND: start in that weather (clear, cloudy, rain, storm, snow, windy, rainbow).
            int wi = System.Array.IndexOf(args, "--weather");
            forcedWeather = wi >= 0 && wi + 1 < args.Length ? args[wi + 1] : null;
            // --screen N: which monitor to live on (1 = main). Each monitor can run its own copy.
            int si = System.Array.IndexOf(args, "--screen");
            externalScreens = si < 0;
            if (si >= 0 && si + 1 < args.Length && int.TryParse(args[si + 1], out var sn)) screen = sn - 1;
            money = PlayerPrefs.GetInt("money", 0);
            floors = Mathf.Clamp(PlayerPrefs.GetInt("floors", Tune.startFloors), 1, Tune.maxFloors);
            // --floors N: jump straight to an N-floor building (playtesting all tenant types).
            int fi = System.Array.IndexOf(args, "--floors");
            if (fi >= 0 && fi + 1 < args.Length && int.TryParse(args[fi + 1], out var fl)) { floors = Mathf.Clamp(fl, 1, Tune.maxFloors); playtest = true; }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Tune.fps;
            cam = CreateCamera(0);
            cameras.Add(cam);
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
            neon = new GameObject("Neon").AddComponent<SpriteRenderer>();
            neon.sprite = Props.Make(Props.Neon, Tune.pixel, new Vector2(0.5f, 0.5f));
            neon.sortingOrder = 3;
            neon.transform.SetParent(house.transform, false);
            ticker = Bubble.Create(house.transform, Vector3.zero, 4);
            BuildHouse();
            Town = new Town(this);
            Home = new Building { Def = new LotDef { id = "home", name = "집", visit = Book.home }, W = 0 };
            // --town N[:L]: playtest with N town buildings at level L.
            int ti = System.Array.IndexOf(args, "--town");
            if (ti >= 0 && ti + 1 < args.Length)
            {
                var parts = args[ti + 1].Split(':');
                if (int.TryParse(parts[0], out var count)) Town.DevBuild(count, parts.Length > 1 && int.TryParse(parts[1], out var lv) ? lv : 1);
                playtest = true;
            }
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
            neon.transform.localPosition = new Vector3(0, houseH - 16 * Tune.pixel, -0.2f);
            ticker.SetHome(new Vector3(0, houseH - 23 * Tune.pixel, 0)); // under the neon

            // One light per window (the ground floor's middle one is the door), flickered in Update.
            foreach (var l in lights) Destroy(l.gameObject);
            lights.Clear();
            int cols = Pixels.HouseCols(floors), width = 7 * cols + 5, door0 = width / 2 - 2;
            for (int f = 0; f < floors; f++)
                for (int c = 0; c < cols; c++)
                {
                    int col = 4 + 7 * c;
                    if (f == floors - 1 && col + 3 >= door0 && col <= door0 + 3) continue; // the door
                    var l = new GameObject("Light").AddComponent<SpriteRenderer>();
                    l.sprite = lightSprite;
                    l.sortingOrder = 1;
                    l.color = new Color32(0xff, 0xe0, 0x7a, 255);
                    l.transform.SetParent(house.transform, false);
                    // Window centre in sprite pixels (x from left, y from top), relative to the bottom-centre pivot.
                    float px = col + 2 - width / 2f, py = 6 + f * 7 + 4;
                    l.transform.localPosition = new Vector3(px * Tune.pixel, houseH - py * Tune.pixel, -0.1f);
                    lights.Add(l);
                }
        }

        /// <summary>Windows switch on and off on their own; more are dark late at night.</summary>
        void FlickerLights(float dt)
        {
            // Rooftop neon (7+ floors) blinks; the ticker (9+ floors) scrolls town news.
            neon.enabled = floors >= Tune.neonFloors && !Blackout && Mathf.Repeat(Time.time, 1.2f) < 0.85f;
            if (floors >= Tune.tickerFloors && !Blackout && (tickerT -= dt) <= 0)
            {
                tickerT = 0.22f;
                var today = System.DateTime.Now;
                string day = "일월화수목금토"[(int)today.DayOfWeek] + "요일";
                string news = $"   GG 타워 · {today.Month}월 {today.Day}일 {day} · 세입자 {tenants.Count}명 · 자산 {Won(money)} · 오늘도 평화로운 바탕화면   ";
                tickerPos = (tickerPos + 1) % news.Length;
                ticker.Say((news + news).Substring(tickerPos, 12), 0);
            }
            else if (floors < Tune.tickerFloors || Blackout) ticker.Hide();

            if (Blackout) { foreach (var l in lights) l.color = new Color32(0x2a, 0x26, 0x38, 255); lightT = 0; return; }
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
            if (!playtest)
            {
                PlayerPrefs.SetInt("money", money);
                PlayerPrefs.SetInt("floors", floors);
                PlayerPrefs.Save();
            }
            moneyLabel.Say(Won(money), 0);
        }

        static Camera CreateCamera(int index)
        {
            var camera = new GameObject($"Display {index + 1} camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetDisplay = index;
            return camera;
        }

        void BindDisplays()
        {
            if (!externalScreens || cameras.Count >= Display.displays.Length) return;
            if (nextDisplay == 0)
            {
                nextDisplay = cameras.Count;
                Display.displays[nextDisplay].Activate();
                bindDeadline = Time.realtimeSinceStartup + 5;
            }
            if (Bridge.BindDisplay())
            {
                cameras.Add(CreateCamera(nextDisplay));
                Debug.Log($"[desk] bound display output {nextDisplay + 1}");
                nextDisplay = 0;
            }
            else if (Time.realtimeSinceStartup >= bindDeadline)
            {
                Debug.LogWarning($"[desk] could not bind display output {nextDisplay + 1}");
                externalScreens = false;
            }
        }

        void Update()
        {
            if (!overlay)
            {
                overlay = Bridge.MakeOverlay(screen, externalScreens);
                if (overlay) Debug.Log($"[desk] overlay mode {(externalScreens ? "external" : $"screen {screen + 1}")}. Monitors:\n{Bridge.ScreenList()}");
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (quitAt > 0 && Time.realtimeSinceStartup > quitAt) Application.Quit();
            // The player recreates its Metal surface (opaque again) after resizes; keep re-clearing it.
            if ((refreshT -= dt) <= 0) { refreshT = 0.25f; Bridge.Refresh(); }

            BindDisplays();
            ReadDesktop();
            Town.Tick(dt, Door.x - houseW / 2, Blackout);
            Town.AddSegs(segs);
            blackoutT -= dt;
            if ((shakeT -= dt) > 0) cam.transform.position += (Vector3)(Random.insideUnitCircle * 7);
            SpawnTenants(dt);
            if (Sky == null) Sky = new Weather(this, forcedWeather);
            Sky.Tick(dt);
            SystemLoad(dt);
            director.Tick(dt);
            FlickerLights(dt);

            CollectCoins(dt);
            Tenant hoverT = null;
            bool holding = false;
            foreach (var t in tenants)
            {
                t.Tick(dt);
                if (t.Bounds.Contains(Mouse)) hoverT = t;
                holding |= t.Held;
            }
            StartChats();
            KeepApart(dt);
            LayoutBubbles();
            bool hoverH = !hoverT && HouseBounds.Contains(Mouse);
            bool hoverTown = !hoverT && !hoverH && (Town.At(Mouse) != null || Town.OverLot(Mouse));
            if (hoverTown) Town.Hover(Mouse, money);
            else Town.RefreshSigns();
            PetCheck(hoverT, dt);
            TickleCheck(hoverT, dt);
            Paintings(dt);
            HandleClicks(hoverT, hoverH, ref holding);
            if (hoverTown && MouseDown && !wasDownTown)
            {
                int spent = Town.Click(Mouse, money, !playtest);
                if (spent > 0) { money -= spent; Save(); director.Fireworks(2.5f); }
            }
            wasDownTown = MouseDown;

            bool through = !(hoverT || hoverH || hoverTown || holding);
            if (through != clickThrough) { clickThrough = through; Bridge.SetClickThrough(through); }

            if ((logT -= dt) <= 0)
            {
                logT = 10;
                Debug.Log($"[desk] edges y: {string.Join(",", segs.ConvertAll(x => x.y.ToString("0")))}");
                Debug.Log($"[desk] windows {wins.Count} segs {segs.Count} tenants {tenants.Count}/{Capacity} coins {coins.Count} money {money} floors {floors}");
            }
        }

        bool wasDownTown;

        /// <summary>Resting the cursor on a tenant for a moment pets them.</summary>
        void PetCheck(Tenant hover, float dt)
        {
            if (hover == null || hover != hoverLast || MouseDown) { hoverLast = hover; hoverTime = 0; return; }
            if ((hoverTime += dt) < Tune.petDelay) return;
            hoverTime = -2f; // don't pet again straight away
            hover.Pet();
        }

        void ReadDesktop()
        {
            Bridge.Screen(scr);
            ScreenW = scr[0];
            ScreenH = scr[1];
            TopY = scr[3];
            FloorY = scr[3] + scr[5];
            displays.Clear();
            for (int i = 0; i < cameras.Count; i++)
            {
                if (!Bridge.DisplayRect(i, displayRect))
                {
                    if (i != 0) { cameras[i].enabled = false; continue; }
                    displayRect[0] = displayRect[1] = 0;
                    displayRect[2] = ScreenW;
                    displayRect[3] = ScreenH;
                    displayRect[4] = FloorY;
                }
                var rect = new Rect(displayRect[0], displayRect[1], displayRect[2], displayRect[3]);
                displays.Add(new DesktopDisplay(i, rect, displayRect[4]));
                var camera = cameras[i];
                camera.enabled = true;
                camera.orthographicSize = rect.height / 2;
                camera.aspect = rect.width / rect.height;
                camera.transform.position = W(rect.center, -10);
            }
            Door = new Vector2(ScreenW - 140, FloorY);
            house.transform.position = W(Door, 1);
            if (Home != null) Home.X = Door.x;
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
            segs.Clear();
            foreach (var display in displays) Surfaces.AppendDisplay(wins, display, segs);
            segs.Add(new Seg(Surfaces.Roof, Door.x - houseW / 2 + 5 * Tune.pixel, Door.x + houseW / 2 - 5 * Tune.pixel, RoofTop));
        }

        /// <summary>Top of the apartment's roof.</summary>
        public float RoofY => FloorY - houseH;
        public float HouseWidth => houseW;

        /// <summary>
        /// Reads CPU/memory every couple of seconds for the LED billboard on the apartment roof,
        /// and lets tenants complain when the machine is working hard.
        /// </summary>
        void SystemLoad(float dt)
        {
            if (board == null) board = new StatusSign(this);
            board.Tick(dt, Blackout);
            hotCool -= dt;
            memCool -= dt;
            if ((statT -= dt) > 0) return;
            statT = 2;
            Bridge.Stats(stats);
            var now = System.DateTime.Now;
            string clock = $"{(now.Hour < 12 ? "오전" : "오후")} {(now.Hour % 12 == 0 ? 12 : now.Hour % 12)}:{now.Minute:00}";
            board.Set(Sky.Now, Sky.Rainbow, clock, Cpu, MemUsed, MemTotal);

            // Busy CPU for a few readings in a row: the tenants feel the heat.
            hotT = Cpu > Tune.hotCpu ? hotT + 2 : 0;
            if (hotT >= 6 && hotCool <= 0)
            {
                hotCool = 90;
                Bubble.Popup($"CPU 과열 {Cpu:0}%!", W(new Vector2(ScreenW / 2, TopY + 70)));
                foreach (var t in tenants)
                    t.React(t.Type.IsAnimal ? null : string.Format(TenantBook.Pick(Book.hot), Cpu.ToString("0")), "sweat", 2.5f);
            }
            if (MemTotal > 0 && MemUsed / MemTotal > Tune.fullMemory && memCool <= 0)
            {
                memCool = 120;
                foreach (var t in tenants) if (Random.value < 0.4f) t.React(t.Type.IsAnimal ? null : TenantBook.Pick(Book.memfull), "sweat");
            }
        }

        void SpawnTenants(float dt)
        {
            spawnT += dt;
            if (Residents >= Capacity || spawnT < Tune.spawnEvery || director.TruckBusy) return;
            spawnT = 0;
            director.SendTruck(Mathf.Min(3, Capacity - Residents));
        }

        /// <summary>Called by the truck: the next tenant steps out at <paramref name="at"/>.</summary>
        public void MoveIn(Vector2 at)
        {
            if (Residents >= Capacity) return;
            // Variety first: the unlocked type with the fewest tenants moves in next.
            TenantType pick = null;
            int best = int.MaxValue;
            for (int i = 0; i < Book.types.Length; i++)
            {
                var type = Book.types[i];
                if (type.minFloor > 0 ? floors < type.minFloor : i >= UnlockedTypes) continue;
                int count = tenants.FindAll(x => x.Type == type && !x.Visitor).Count;
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

        /// <summary>The apartment's own front door, enterable like a shop ("going home").</summary>
        public Building Home { get; private set; }

        /// <summary>The apartment door if <paramref name="p"/> is within <paramref name="range"/> of it.</summary>
        public Building HomeNear(Vector2 p, float range) =>
            Mathf.Abs(Door.x - p.x) < range && Mathf.Abs(FloorY - p.y) < 60 ? Home : null;

        /// <summary>A free tenant whose head is right under <paramref name="p"/> (for piggyback rides).</summary>
        public Tenant HeadAt(Tenant faller, Vector2 p)
        {
            foreach (var t in tenants)
            {
                if (t == faller || !t.Free || t.HasRider || t.Type.scale < 0.9f) continue;
                float top = t.P.y - t.Height;
                if (Mathf.Abs(t.P.x - p.x) < t.Width * 0.35f && p.y >= top - 2 && p.y <= top + 10) return t;
            }
            return null;
        }

        /// <summary>The nearest free person (not an animal) to <paramref name="from"/> within <paramref name="range"/>.</summary>
        public Tenant NearestPerson(Tenant from, float range)
        {
            Tenant best = null;
            foreach (var t in tenants)
            {
                float dist = Vector2.Distance(t.P, from.P);
                if (t != from && !t.Type.IsAnimal && t.Free && dist < range) { best = t; range = dist; }
            }
            return best;
        }

        /// <summary>The baby cried: the nearest grown-up comes over and soothes them.</summary>
        public void Comfort(Tenant baby)
        {
            Tenant best = null;
            float range = 600;
            foreach (var t in tenants)
            {
                float dist = Vector2.Distance(t.P, baby.P);
                if (t != baby && !t.Type.IsAnimal && t.Type.scale >= 0.9f && t.Carriable && dist < range) { best = t; range = dist; }
            }
            if (best == null) return;
            best.Gather(baby.P.x + 16, 6, "아이고 우리 아기~", "heart", 1.8f, false, null, 0);
            comforter = (best, baby);
        }

        (Tenant who, Tenant baby) comforter;

        readonly List<(SpriteRenderer r, float until, Vector2 p)> paintings = new List<(SpriteRenderer, float, Vector2)>();
        float admireT, paintingHoverT;
        SpriteRenderer hoveredPainting;

        /// <summary>The painter leaves a finished picture on an easel at <paramref name="at"/> for a while.</summary>
        public void PlacePainting(Vector2 at, int on)
        {
            var r = new GameObject("Painting").AddComponent<SpriteRenderer>();
            r.sprite = Pixels.Make(Props.Easel(Random.Range(0, 9999)), Props.EaselPalette, Tune.pixel);
            r.sortingOrder = 6;
            r.transform.position = W(at, 0.4f);
            paintings.Add((r, Time.time + 50, at));
        }

        void Paintings(float dt)
        {
            for (int i = paintings.Count - 1; i >= 0; i--)
                if (Time.time > paintings[i].until) { Destroy(paintings[i].r.gameObject); paintings.RemoveAt(i); }
            InspectPainting(dt);
            if ((admireT -= dt) > 0 || paintings.Count == 0) return;
            admireT = 3;
            foreach (var (_, _, p) in paintings)
                foreach (var t in tenants)
                    if (!t.Type.IsAnimal && t.Free && Vector2.Distance(t.P, p) < 45 && Random.value < 0.5f)
                        t.React(TenantBook.Pick(Book.admire), "heart");
            // The crying baby's comforter arrived.
            if (comforter.who && comforter.baby && comforter.who.Arrived) { comforter.baby.Soothe(); comforter = default; }
        }

        void InspectPainting(float dt)
        {
            SpriteRenderer target = null;
            Vector3 cursor = W(Mouse);
            for (int i = paintings.Count - 1; i >= 0; i--)
            {
                var sprite = paintings[i].r;
                var bounds = sprite.bounds;
                if (cursor.x < bounds.min.x || cursor.x > bounds.max.x ||
                    cursor.y < bounds.min.y || cursor.y > bounds.max.y) continue;
                target = sprite;
                break;
            }

            if (target != hoveredPainting)
            {
                hoveredPainting = target;
                paintingHoverT = 0;
            }
            if (!target || MouseDown || paintingHoverT >= Tune.petDelay) return;
            paintingHoverT += dt;
            if (paintingHoverT < Tune.petDelay) return;
            Bubble.Popup("오늘의 작품 잘 보고 가요!", target.bounds.center + Vector3.up * 35);
            foreach (var tenant in tenants)
                if (!tenant.Type.IsAnimal && tenant.Carriable && Vector2.Distance(tenant.P, Mouse) < 110)
                    tenant.React(TenantBook.Pick(Book.admire), "star");
        }

        Vector2 lastMouse;
        float tickleT;

        /// <summary>Wiggling the cursor quickly over a tenant tickles them.</summary>
        void TickleCheck(Tenant hover, float dt)
        {
            float speed = (Mouse - lastMouse).magnitude / Mathf.Max(dt, 0.001f);
            lastMouse = Mouse;
            if (hover == null || MouseDown || speed < 600) { tickleT = Mathf.Max(0, tickleT - dt); return; }
            if ((tickleT += dt) < 0.35f) return;
            tickleT = 0;
            hover.Tickle();
        }

        public StatusSign Board => board;

        /// <summary>A parcel falling from the top of the screen at <paramref name="x"/>.</summary>
        public void DropBox(float x)
        {
            var c = new GameObject("Parcel").AddComponent<Coin>();
            c.Init(this, new Vector2(x, TopY + 50), Tune.boxValue, new Vector2(Random.Range(-30f, 30f), 60), true);
            coins.Add(c);
        }

        /// <summary>A coin dropped at <paramref name="at"/> (shop takings, tips, gifts).</summary>
        public void SpawnCoinAt(Vector2 at, int value)
        {
            if (coins.Count >= Tune.maxCoins + 20) return;
            var c = new GameObject("Coin").AddComponent<Coin>();
            c.Init(this, at, value, new Vector2(Random.Range(-60f, 60f), -220));
            coins.Add(c);
        }

        /// <summary>The floor's surface segment (for tenants placed straight onto it).</summary>
        public Seg FloorSeg => new Seg(Surfaces.Floor, 0, ScreenW, FloorY);

        /// <summary>Power cut for <paramref name="seconds"/>.</summary>
        public void StartBlackout(float seconds) => blackoutT = seconds;

        /// <summary>Shakes the whole overlay for <paramref name="seconds"/>.</summary>
        public void Shake(float seconds) => shakeT = seconds;

        /// <summary>Knocks every resting coin off its edge (earthquake).</summary>
        public void ScatterCoins()
        {
            foreach (var c in coins) c.Knock(Random.value < 0.5f ? -1 : 1);
        }

        /// <summary>A guest (not a resident: doesn't use a room) arrives at <paramref name="at"/>.</summary>
        public Tenant SpawnVisitor(TenantType type, Vector2 at)
        {
            var t = new GameObject("Visitor " + type.name).AddComponent<Tenant>();
            t.Init(this, type, at);
            t.Visitor = true;
            tenants.Add(t);
            return t;
        }

        public void Remove(Tenant t)
        {
            tenants.Remove(t);
            Destroy(t.gameObject);
        }

        int Residents => tenants.FindAll(t => !t.Visitor).Count;

        /// <summary>A bonus coin falling from the top of the screen at <paramref name="x"/>.</summary>
        public void DropCoin(float x)
        {
            if (coins.Count >= Tune.maxCoins + 20) return;
            var c = new GameObject("Coin").AddComponent<Coin>();
            // Start below the topmost edge (a maximised window's top) so coins rain down onto the desktop.
            c.Init(this, new Vector2(x, TopY + 50), Tune.bonusCoin, new Vector2(Random.Range(-30f, 30f), 60));
            coins.Add(c);
        }

        /// <summary>What a chance meeting turns into: each outcome gets its slice of one roll (see tuning.json).</summary>
        Tenant.Outcome PickOutcome(Tenant a, Tenant b)
        {
            bool people = !a.Type.IsAnimal && !b.Type.IsAnimal;
            bool petPair = a.Type.IsAnimal != b.Type.IsAnimal && (a.Type.IsCat || a.Type.IsDog || b.Type.IsCat || b.Type.IsDog);
            float r = Random.value;
            if ((r -= Tune.danceChance) < 0) return Tenant.Outcome.Dance;
            if ((r -= Tune.highfiveChance) < 0) return Tenant.Outcome.HighFive;
            if (petPair && (r -= Tune.petChance) < 0) return Tenant.Outcome.Pet;
            if (!people) return (r -= Tune.tagChance) < 0 ? Tenant.Outcome.Tag : Tenant.Outcome.None;
            if ((r -= Tune.argueChance) < 0) return Tenant.Outcome.Argue;
            if ((r -= Tune.rpsChance) < 0) return Tenant.Outcome.Rps;
            if ((r -= Tune.hugChance) < 0) return Tenant.Outcome.Hug;
            if ((r -= Tune.tagChance) < 0) return Tenant.Outcome.Tag;
            if ((r -= Tune.shareChance) < 0) return Tenant.Outcome.Share;
            if ((r -= Tune.mirrorChance) < 0) return Tenant.Outcome.Mirror;
            if ((r -= Tune.coinTrickChance) < 0) return Tenant.Outcome.CoinTrick;
            return Tenant.Outcome.None;
        }

        void StartChats()
        {
            for (int i = 0; i < tenants.Count; i++)
                for (int j = i + 1; j < tenants.Count; j++)
                {
                    Tenant a = tenants[i], b = tenants[j];
                    if (a.CanChat && b.CanChat && a.On == b.On && Mathf.Abs(a.P.y - b.P.y) < 2 && Mathf.Abs(a.P.x - b.P.x) < 28)
                    {
                        bool people = !a.Type.IsAnimal && !b.Type.IsAnimal;
                        var outcome = PickOutcome(a, b);
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

        /// <summary>Where collected coins fly to: the money sign on the facade.</summary>
        Vector2 Sign => new Vector2(Door.x, FloorY - (houseH - 9 * Tune.pixel));

        /// <summary>
        /// Coins are picked up by sweeping the mouse near them (no clicking, so work isn't
        /// interrupted), or on their own once they've sat around for a while.
        /// </summary>
        void CollectCoins(float dt)
        {
            int gained = 0;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var c = coins[i];
                c.Tick(this, dt, Sign);
                if (!c.Collecting && (Vector2.Distance(Mouse, c.P - new Vector2(0, 10)) < Tune.collectRadius || c.Age > Tune.autoCollect))
                    Open(c, null);
                // Tenants walking past a parcel open it.
                if (c.IsBox && !c.Collecting && c.On != Body.None)
                    foreach (var t in tenants)
                        if (t.Free && t.On == c.On && Mathf.Abs(t.P.x - c.P.x) < 12) { Open(c, t); break; }
                if (!c.Arrived) continue;
                gained += c.Value;
                coins.RemoveAt(i);
                Destroy(c.gameObject);
            }
            if (gained == 0) return;
            money += gained;
            Bubble.Popup("+" + Won(gained), W(Sign - new Vector2(0, 24)));
            Save();
        }

        void Open(Coin c, Tenant opener)
        {
            c.Collect();
            if (!c.IsBox) return;
            Bubble.Popup("언박싱!", W(c.P - new Vector2(0, 30)));
            if (opener) opener.React(opener.Type.IsAnimal ? null : TenantBook.Pick(Book.unbox), "heart");
        }

        void HandleClicks(Tenant hoverT, bool hoverH, ref bool holding)
        {
            bool pressed = MouseDown && !wasDown;
            wasDown = MouseDown;
            if (hoverH && board != null && !board.ShowingMessage)
                HouseSay(floors >= Tune.maxFloors ? "최고층이에요!" : $"{floors + 1}층 증축 {Won(UpgradeCost)}", 1.5f);
            if (!pressed) return;
            if (hoverT) { hoverT.Grab(Mouse); holding = true; }
            else if (hoverH) Upgrade();
        }

        /// <summary>House notices (upgrade price, completion) scroll on the rooftop billboard.</summary>
        void HouseSay(string text, float seconds)
        {
            if (board != null) board.Message(text, seconds);
            else houseTip.Say(text, seconds);
        }

        void Upgrade()
        {
            if (floors >= Tune.maxFloors) { HouseSay("더는 못 올려요!", 2f); return; }
            int cost = UpgradeCost;
            if (money < cost) { HouseSay($"{Won(cost - money)} 부족", 2f); return; }
            money -= cost;
            floors++;
            BuildHouse();
            Save();
            HouseSay($"뚝딱! {floors}층 완공", 3f);
            bool wider = Pixels.HouseCols(floors) > Pixels.HouseCols(floors - 1);
            Bubble.Popup(floors == Tune.helipadFloors ? "옥상 헬기장 오픈!" : floors == Tune.antennaFloors ? "옥상에 안테나 설치!" : wider ? "옆으로 확장 공사 완료!" : "새 세입자 모집 중!", W(Door - new Vector2(0, houseH + 60)));
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
        /// Separates idle tenants gradually. Walking and interaction animations keep
        /// their own movement, so separation cannot push a tenant back every frame.
        /// </summary>
        void KeepApart(float dt)
        {
            for (int i = 0; i < tenants.Count; i++)
                for (int j = i + 1; j < tenants.Count; j++)
                {
                    Tenant a = tenants[i], b = tenants[j];
                    if (!a.AllowsSpacing || !b.AllowsSpacing || a.On != b.On || Mathf.Abs(a.P.y - b.P.y) >= 2) continue;
                    float dx = b.P.x - a.P.x, gap = Tune.personalSpace;
                    if (Mathf.Abs(dx) >= gap) continue;
                    float push = Mathf.Min((gap - Mathf.Abs(dx)) * 0.5f, 12f * dt) * (dx >= 0 ? 1 : -1);
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
                if (!c.Collecting && c.On == on && dist < bestD) { best = c; bestD = dist; }
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
            foreach (var display in displays)
                if (id == display.Floor.win) { pos = new Vector2(display.Floor.x0, display.Floor.y); return true; }
            if (id == Surfaces.Roof) { pos = new Vector2(Door.x, RoofTop); return true; }
            if (id <= Town.TopBase) return Town.SegPos(id, out pos);
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

        public int DisplayAt(Vector2 point, int fallback = -1)
        {
            for (int i = 0; i < displays.Count; i++)
            {
                var rect = displays[i].Bounds;
                if (point.x >= rect.xMin && point.x < rect.xMax &&
                    point.y >= rect.yMin && point.y <= rect.yMax) return i;
            }
            return fallback;
        }

        public int DisplayCount => displays.Count;

        public DesktopDisplay DisplayBounds(int index) => displays[Mathf.Clamp(index, 0, displays.Count - 1)];

        public bool TryDisplayByFloor(int floorId, out DesktopDisplay display)
        {
            foreach (var candidate in displays)
                if (candidate.Floor.win == floorId) { display = candidate; return true; }
            display = default;
            return false;
        }

        public bool TryDisplayRoute(Seg floor, float fromX, out float departure, out Vector2 arrival, out bool crossesGap)
        {
            crossesGap = false;
            departure = 0;
            arrival = default;
            if (displays.Count <= 1) return false;
            int source = displays.FindIndex(display => display.Floor.win == floor.win);
            if (source < 0) return false;
            int start = Random.Range(0, displays.Count);
            for (int step = 0; step < displays.Count; step++)
                if (displays[source].TryRoute(displays[(start + step) % displays.Count], fromX, out departure, out arrival))
                    return true;
            if (!externalScreens) return false;
            for (int step = 0; step < displays.Count; step++)
            {
                if (!displays[source].TryGapRoute(displays[(start + step) % displays.Count], fromX, out departure, out arrival)) continue;
                crossesGap = true;
                return true;
            }
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
