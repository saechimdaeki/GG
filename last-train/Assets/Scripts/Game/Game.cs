using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace LastTrain
{
    /// <summary>
    /// 막차까지 — the whole run. Day: leave the shelter, search dumpsters and boxes for weapons
    /// and supplies, sneak up on zombies. Night: zombies come for the shelter; repair the
    /// barricade, fight, or sleep through it. After the last night: hold the subway platform.
    /// Controls: WASD, mouse aim, left click attack, 1-4/wheel weapons, Shift run, Ctrl sneak,
    /// E search/repair/sleep, F eat, H bandage.
    /// Screenshot runs: LastTrain.app --scene night --shot /abs/out.png --at 3 [--seed 7]
    /// </summary>
    public partial class Game : MonoBehaviour, IWorld
    {
        static Tuning T => Tuning.Data;

        public Horde Horde { get; private set; }
        public readonly List<Survivor> Players = new();
        public Survivor Me;
        public City City;

        public int Day = 1;
        public string Phase = "day";   // day | night | evac | won | lost
        public float PhaseT;           // day/night: seconds left. evac: seconds held
        public int Kills, StealthKills;
        public float BarricadeHp;
        public bool Sleeping;
        public bool Powered, Trading;
        public int FusesIn;
        public List<Tuning.TradeCfg> TodayTrades = new();
        City.Container _drop;
        Light _flareLight;
        Renderer _beaconLens;
        float _dropAt, _flareT;
        bool _dropDone;
        City.Container _searching;
        float _searchT, _searchNoiseT, _nightCallT;

        class Nail { public Transform t; public Vector2 pos, dir; public float dmg, life; public int pierce; public HashSet<int> hit = new(); }
        class Firework { public Transform t; public Vector2 from, to; public float time, flight, dmg, r; }
        readonly List<Nail> _nails = new();
        readonly List<Firework> _fireworks = new();

        Fx _fx;
        Actors _actors;
        Hud _hud;
        Camera _cam;
        Light _flashlight, _glow;
        LineRenderer _reach, _aimLine;
        System.Random _rng = new();
        int _encounterSeed;
        float _spawnAcc, _dark;
        readonly Dictionary<string, string> _args = new();
        readonly List<Vector2> _bodies = new();
        readonly List<int> _near = new();
        bool _moving, _occlusionComparison;
        Vector2 _aimPoint;
        string _prompt = "";
        class Pop { public Vector3 at; public string text; public Color color; public float t, screenLift; }
        readonly List<Pop> _pops = new();

        /// <summary>A label that floats up from a spot in the world and fades (loot found, kills).</summary>
        void PopText(Vector2 at, string text, Color c, float screenLift = 0f) =>
            _pops.Add(new Pop { at = Fx.V3(at, 1.6f), text = text, color = c, screenLift = screenLift });

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            ReadArgs();
            int seed = _args.TryGetValue("seed", out var s) ? int.Parse(s) : Random.Range(0, 99999);
            _rng = new System.Random(seed);
            _encounterSeed = seed;
            Random.InitState(seed);

            City = new GameObject("City").AddComponent<City>();
            City.Build(seed);
            Horde = new Horde(T.zombies, City.ZombieWalls);
            BarricadeHp = T.shelter.barricadeHp;
            City.ZombieWalls.Add(City.Door);
            _fx = new GameObject("Fx").AddComponent<Fx>();
            _fx.Init(Resources.Load<Material>("Materials/FxAdd"), Resources.Load<Material>("Materials/FxAlpha"));
            BuildRain();
            _actors = new GameObject("Actors").AddComponent<Actors>();
            _actors.Init();
            Me = new Survivor { Pos = City.Start };
            Players.Add(Me);
            _combatAudio = gameObject.AddComponent<CombatAudio>();
            _combatAudio.Build();
            BuildCamera();
            FramePlayer(0f, true);
            BuildPlayerExtras();
            _hud = new GameObject("Hud").AddComponent<Hud>();
            _hud.Build();
            _hud.BuildInteractionPanels(ChooseLoot, TakeAllLoot, CloseLoot, ResumeMenu, FinishTutorial);
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            _actors.SpawnNpc("Casual_Male", City.Trader, 180);
            SpawnRouteContacts();
            CreateRouteClues();
            StartPhase("day");
            if (_args.TryGetValue("scene", out var scene) && scene != "opening" && scene != "verify-opening" && scene != "tutorial-aim")
            {
                if (scene is not ("motion-preview" or "keyboard-qa")) StartCityPopulation();
                SetupScene(scene);
            }
            else if (scene == "verify-opening") VerifyOpening();
            else if (scene == "tutorial-aim") SetupScene(scene);
            else BeginTutorial();
            if (scene == "keyboard-qa") StartCoroutine(VerifyKeyboard());
            else if (_args.ContainsKey("shot")) StartCoroutine(Shot());
            if (scene == "motion-preview" && _args.TryGetValue("frames", out var frames))
                StartCoroutine(CaptureMotion(frames));
        }

        // ---------------------------------------------------------------- run flow

        void StartPhase(string name)
        {
            Phase = name;
            switch (name)
            {
                case "day":
                    ResetStreetEvents();
                    ScheduleEncounter();
                    PhaseT = T.clock.daySeconds;
                    _dropAt = PhaseT - Mathf.Lerp(T.airdrop.earliest, T.airdrop.latest, (float)_rng.NextDouble());
                    _dropDone = false;
                    TodayTrades = T.trades.OrderBy(_ => _rng.Next()).Take(3).ToList();
                    DeliverBroadcast();
                    break;
                case "night":
                    ClearEncounter();
                    PhaseT = T.clock.nightSeconds;
                    _nightCallT = 0;
                    Say("밤이다. 놈들이 몰려온다 — 쉼터 바리케이드를 지키거나 잠들어라");
                    StartStreetNight();
                    break;
                case "evac":
                    PhaseT = 0;
                    Sleeping = false;
                    Say($"{RouteName(_escapeRoute ?? EscapeRoute.Rail)} · 준비가 끝날 때까지 구역을 지켜라");
                    break;
                case "won":
                    _hud?.ShowEnd($"{RouteName(_escapeRoute ?? EscapeRoute.Rail)} 탈출\n{EndingStory()}\n\n{Day}일차 · 좀비 {Kills}마리 (암살 {StealthKills})\n\n<size=30>R: 다시 하기</size>");
                    break;
                case "lost":
                    _hud?.ShowEnd($"쓰러졌다…\n\n{Day}일차 {(IsNight ? "밤" : "낮")} · 좀비 {Kills}마리 (암살 {StealthKills})\n\n<size=30>R: 다시 하기</size>");
                    break;
            }
        }

        public bool IsNight => Phase == "night" || (Phase == "evac" && _evacAtNight);
        public bool InShelter(Vector2 p) => City.Shelter.Contains(p);

        void Clock(float dt)
        {
            if (Phase == "evac")
            {
                TickEscape(dt);
                return;
            }
            float before = PhaseT;
            PhaseT -= dt;
            if (Phase == "day" && before > 25 && PhaseT <= 25) Say("해가 지고 있다 — 쉼터로 돌아가라!");
            if (Phase == "day" && !_dropDone && PhaseT <= _dropAt) SpawnDrop();
            if (PhaseT > 0) return;
            if (Phase == "day") StartPhase("night");
            else
            {
                Day++;
                if (Sleeping) Wake("아침이 밝았다");
                StartPhase("day");
            }
        }

        public void Simulate(float dt)
        {
            if (!InPractice)
            {
                Clock(dt);
                if (Phase == "won") return;
                TickStreetEvents(dt);
                Spawn(dt);
                TickStoryLocation();
            }
            _bodies.Clear();
            foreach (var p in Players)
                if (p.Alive) _bodies.Add(p.Pos);
            if (_encounterKind != EncounterKind.None) _bodies.Add(_encounterPosition);
            Senses(dt);
            TickScreamers();
            DirectEncounterAttackers();
            Horde.Step(dt, _bodies, _tutorialZombie, _windingZombies);
            UpdateNails(dt);
            UpdateFireworks(dt);
            UpdateBarricade(dt);
            UpdateFlare(dt);
            TickIncomingAttacks(dt);
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                if (p != Me && !InPractice) p.Hurt(Horde.ContactDps(p.Pos, 0.45f) * dt);
                if (InPractice) p.TickCooldown(dt);
                else p.Tick(dt * (Sleeping ? 0.5f : 1f));
                if (Sleeping) p.Hp = Mathf.Min(p.MaxHp, p.Hp + T.player.sleepHealPerSec * dt);
                if (p.Broke != null) { Say($"{p.Broke}이(가) 부서졌다"); p.Broke = null; }
            }
            foreach (var d in Horde.Reap())
            {
                if (d.id == _tutorialZombie)
                {
                    _tutorialZombie = -1;
                    if (_lesson == Lesson.Stealth)
                    {
                        ResetPracticeZombie();
                        Say("C로 숙이고(다시 C: 일어서기) 등 뒤에서 공격해 보자");
                    }
                }
                else Kills++;
                EncounterAttackerDefeated(d.id);
                _screamed.Remove(d.id);
                _actors?.Kill(d.id, -d.heading);
                _fx?.BloodBurst(d.pos, d.heading * -1f, 14);
                _fx?.BloodPool(d.pos, d.kind == 2 ? 2.2f : 1.3f);
            }
            if (InPractice) TickTutorial();
            else TickEncounter(dt);
            if (!Survivor.CoopTick(Players, dt)) StartPhase("lost");
        }

        // ---------------------------------------------------------------- senses

        /// <summary>
        /// Unaware zombies notice a survivor they can see (in front, in range, wall not in the way)
        /// or bump into. Aware ones keep tracking whoever they can still sense. At night the horde
        /// knows where you are and keeps coming — at the shelter door if you are inside.
        /// </summary>
        void Senses(float dt)
        {
            var sn = T.senses;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                float sight = IsNight ? sn.sightNight + (Sleeping ? 0 : sn.flashlightBonus) : sn.sightDay;
                if (p.Crouching) sight *= sn.crouchSight;
                if (InShelter(p.Pos) && BarricadeHp > 0) sight *= 0.4f;
                foreach (int i in Horde.Near(p.Pos, Mathf.Max(sight * 1.4f, sn.touch), _near).ToArray())
                {
                    var z = Horde.Pos[i];
                    float d = Vector2.Distance(z, p.Pos);
                    float touch = p.Crouching && Survivor.IsBehind(Horde.Heading[i], p.Pos - z) ? 0.68f : sn.touch;
                    bool sees = d <= touch
                        || (d <= sight && Vector2.Angle(Horde.Heading[i], p.Pos - z) <= sn.sightAngle * 0.5f && Clear(z, p.Pos))
                        || (Horde.IsAware(i) && d <= sight * 1.4f && Clear(z, p.Pos));
                    if (sees) Horde.Alert(i, p.Pos, sn.forgetSeconds);
                }
                Noise(p.Pos, p.StepNoise(_moving && p == Me));
            }
            if (!IsNight) return;
            _nightCallT -= dt;
            if (_nightCallT > 0) return;
            _nightCallT = 2f;
            var target = Players.Where(p => p.Alive).Select(p => p.Pos).DefaultIfEmpty(City.Start).First();
            var goal = InShelter(target) && BarricadeHp > 0 ? City.Door.center + Vector2.up * 0.9f : target;
            for (int i = 0; i < Horde.Count; i++)
            {
                if (TryDivert(i, target)) continue;
                if (Horde.IsAware(i) || (Horde.Pos[i] - target).sqrMagnitude < 30 * 30)
                    Horde.Alert(i, goal + Random.insideUnitCircle * 1.5f, 6f);
            }
        }

        /// <summary>True when no wall stands between a and b (sampled along the segment).</summary>
        bool Clear(Vector2 a, Vector2 b)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.7f);
            for (int s = 1; s < steps; s++)
                if (InWall(Vector2.Lerp(a, b, (float)s / steps), 0, City.ZombieWalls)) return false;
            return true;
        }

        public bool CanReachForStealth(Vector2 from, Vector2 to) => Clear(from, to);

        readonly List<int> _noiseHits = new();

        public void Noise(Vector2 at, float radius)
        {
            if (radius <= 0) return;
            foreach (int i in Horde.Near(at, radius, _noiseHits)) Horde.Alert(i, at, T.senses.forgetSeconds * 0.7f);
        }

        readonly HashSet<int> _screamed = new();

        void TickScreamers()
        {
            if (InPractice || !Me.Alive) return;
            for (int i = 0; i < Horde.Count; i++)
            {
                if (Horde.Kind[i] != Horde.Screamer || !Horde.IsAware(i) || Horde.Hp[i] <= 0 || Horde.Flash[i] > 0f) continue;
                var at = Horde.Pos[i];
                if ((at - Me.Pos).sqrMagnitude > 64f || !Clear(at, Me.Pos) || !_screamed.Add(Horde.Id[i])) continue;
                Noise(at, 19f);
                _combatAudio.Play("scream", 0.65f);
                _fx.Ring(at, 2.5f, new Color(0.95f, 0.42f, 0.67f, 0.83f), 0.65f);
                PopText(at, "비명", new Color(0.95f, 0.53f, 0.70f));
            }
        }

        // ---------------------------------------------------------------- input

        void HandleInput(float dt)
        {
            _moving = false;
            Me.Sprinting = Me.Crouching = false;
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k == null || !Me.Alive) return;

            if (m != null)
            {
                var ray = _cam.ScreenPointToRay(m.position.ReadValue());
                if (new Plane(Vector3.up, Vector3.up * 1f).Raycast(ray, out float hit))
                {
                    var p = ray.GetPoint(hit);
                    _aimPoint = new Vector2(p.x, p.z);
                    var to = _aimPoint - Me.Pos;
                    if (to.sqrMagnitude > 0.01f && !Sleeping) Me.Aim = to.normalized;
                }
            }

            if (k.fKey.wasPressedThisFrame) Say(Me.Eat() ? "먹었다 — 배고픔 회복" : "먹을 게 없다 (또는 배부르다)");
            if (k.jKey.wasPressedThisFrame && !InPractice && Phase != "evac") _trackedRoute = (EscapeRoute)(((int)_trackedRoute + 1) % _contacts.Length);
            if (k.hKey.wasPressedThisFrame) Say(Me.Bandage() ? "붕대를 감았다" : "붕대가 없다 (또는 다치지 않았다)");
            if (Sleeping)
            {
                if (k.eKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Wake("일어났다");
                return;
            }
            for (int s = 0; s < 4; s++)
                if ((s == 0 ? k.digit1Key : s == 1 ? k.digit2Key : s == 2 ? k.digit3Key : k.digit4Key).wasPressedThisFrame) Me.Select(s);
            if (m != null && Mathf.Abs(m.scroll.ReadValue().y) > 0.1f) Me.Cycle(m.scroll.ReadValue().y > 0 ? -1 : 1);

            var v = Vector2.zero;
            if (k.wKey.isPressed) v.y += 1;
            if (k.sKey.isPressed) v.y -= 1;
            if (k.aKey.isPressed) v.x -= 1;
            if (k.dKey.isPressed) v.x += 1;
            if (k.leftCtrlKey.wasPressedThisFrame || k.cKey.wasPressedThisFrame) _crouchToggle = !_crouchToggle;
            if (k.leftShiftKey.isPressed && v != Vector2.zero) _crouchToggle = false;
            Me.Crouching = _crouchToggle;
            Me.Sprinting = !Me.Crouching && k.leftShiftKey.isPressed;
            if (v != Vector2.zero && _searching == null)
            {
                var before = Me.Pos;
                MoveFromInput(v, dt);
                _moving = (Me.Pos - before).sqrMagnitude > 0.00000001f;
                if (_moving) _stepT -= dt;
                if (_moving && _stepT <= 0)
                {
                    _stepT = Me.Crouching ? 0.55f : Me.Sprinting ? 0.27f : 0.36f;
                    _combatAudio.Play("step", Me.Crouching ? 0.15f : Me.Sprinting ? 0.7f : 0.4f);
                }
            }

            if (m != null && !m.leftButton.isPressed) _blockAttackUntilRelease = false;
            if (m != null && m.leftButton.isPressed && _searching == null && !_blockAttackUntilRelease)
            {
                bool hit = false;
                if (Me.HeldCfg.kind == "melee") QueueMelee();
                else if (_meleeWeapon == null) hit = Me.Attack(_aimPoint, this, Players.Count(p => p.Alive));
                if (hit && Me.LastWasStealthKill) ConfirmStealthKill();
                if (!hit && Me.Ammo == 0 && m.leftButton.wasPressedThisFrame) Say(Me.Held.id == "nailgun" ? "못이 없다" : "폭죽이 없다");
            }
            // screenshot runs: --auto 1 swings at the nearest zombie, --autosearch 1 holds E
            if (_args.ContainsKey("auto") && _searching == null)
            {
                int n = Horde.Nearest(Me.Pos, 3f);
                if (n >= 0) { _aimPoint = Horde.Pos[n]; if (Me.Attack(_aimPoint, this) && Me.LastWasStealthKill) ConfirmStealthKill(); }
            }
            if (_args.TryGetValue("scene", out var shotScene) && shotScene == "tutorial-aim" && _args.ContainsKey("shot"))
            {
                Me.Crouching = true;
                Me.Aim = Vector2.up;
            }
            if (k.gKey.wasPressedThisFrame && (PharmacyPrompt(false, true) || TryReserveBeacon())) return;
            bool autoE = _args.ContainsKey("autosearch");
            Interact(k.eKey.isPressed || autoE, k.eKey.wasPressedThisFrame || autoE, dt);
        }

        /// <summary>E: hold to search the nearest container, tap to repair the barricade or sleep.</summary>
        void Interact(bool held, bool pressed, float dt)
        {
            _prompt = "";
            var near = City.Containers.Where(c => !c.searched && ContainerAccessible(c) && Vector2.Distance(c.pos, Me.Pos) < 1.9f)
                .OrderBy(c => Vector2.Distance(c.pos, Me.Pos)).FirstOrDefault();
            bool atDoor = Vector2.Distance(Me.Pos, City.Door.center) < 2.2f;
            bool atBed = Vector2.Distance(Me.Pos, City.Bed) < 1.8f;

            if (_searching != null && (!held || !ContainerAccessible(_searching) || Vector2.Distance(_searching.pos, Me.Pos) > 2.1f)) _searching = null;
            if (_searching != null)
            {
                _searchT += dt;
                _searchNoiseT -= dt;
                if (_searchNoiseT <= 0) { Noise(Me.Pos, T.senses.noiseSearch); _searchNoiseT = 0.5f; }
                _prompt = $"{_searching.kind} 뒤지는 중…";
                if (Random.value < dt * 6f) _fx?.Dust(_searching.pos, 2);
                if (_searchT >= T.shelter.searchSeconds) FinishSearch(_searching);
                return;
            }
            if (InteractShelterPower(pressed) || InteractEncounter(pressed) || InteractRadio(pressed) || InteractEscapeRoutes(pressed) || PharmacyPrompt(pressed, false)) return;
            if (near != null)
            {
                if (near.loot != null)
                {
                    _prompt = $"E · {near.kind} 열기";
                    if (pressed) OpenLoot(near);
                    return;
                }
                _prompt = near == _drop && Phase == "day"
                    ? "E 길게 · 보급 회수  /  G · 밤 유인등으로 예약"
                    : $"E 길게 · {near.kind} 뒤지기";
                if (held) { _searching = near; _searchT = 0; _searchNoiseT = 0; }
                return;
            }
            if (atDoor && BarricadeHp < T.shelter.barricadeHp)
            {
                _prompt = Me.Bag["scrap"] > 0 ? $"E: 바리케이드 고치기 (고철 1개, 남은 {Me.Bag["scrap"]})" : "바리케이드가 상했다 — 고철이 필요하다";
                if (pressed && Me.Bag["scrap"] > 0) RepairBarricade();
                return;
            }
            if (Vector2.Distance(Me.Pos, City.Trader + new Vector2(0, -2.0f)) < 1.6f)
            {
                _prompt = "E: 역무실 생존자와 거래";
                if (pressed) { Trading = true; Say("\"살아 있었구만. 뭐 줄 거 있나?\""); }
                return;
            }
            if (Vector2.Distance(Me.Pos, City.Panel) < 1.9f)
            {
                int need = T.station.fusesNeeded - FusesIn;
                _prompt = Powered ? "역 전력 복구됨 · 승강장에서 열차 출발 요청"
                    : Me.Bag["fuse"] > 0 ? $"E: 퓨즈 끼우기 (가진 {Me.Bag["fuse"]}, 필요 {need})"
                    : $"배전반 — 퓨즈 {need}개 더 필요 (철물점·보급품에서 찾기)";
                if (pressed && !Powered && Me.Bag["fuse"] > 0) InsertFuses();
                return;
            }
            if (atBed)
            {
                _prompt = Phase == "night" ? "E: 잠자기 (밤이 빨리 지나가고 체력 회복)" : "잠은 밤에만 잘 수 있다";
                if (pressed && Phase == "night") { Sleeping = true; Say("잠이 들었다…"); }
            }
        }

        bool ContainerAccessible(City.Container c) => !c.room.HasValue || c.room.Value.Contains(Me.Pos);

        void FinishSearch(City.Container c)
        {
            _searching = null;
            c.t.position = c.basePos;
            c.loot = new List<(string, int)>();
            if (c.guaranteedItem != null) AddLoot(c.loot, c.guaranteedItem, 1);
            int rolls = c.table == "drop" ? T.airdrop.rolls : 1;
            for (int r = 0; r < rolls; r++)
            {
                var loot = RollLoot(c.table);
                if (loot.item != "nothing") AddLoot(c.loot, loot.item, _rng.Next(loot.min, loot.max + 1));
            }
            if (c.loot.Count == 0) PopText(c.pos, "텅 비었다", Color.gray);
            if (c == _drop) EndFlare();
            _fx?.Dust(c.pos, 14);
            OpenLoot(c);
        }

        /// <summary>Weighted pick from a loot table in tuning.json (street, store, hardware, pharmacy, drop).</summary>
        public Tuning.LootCfg RollLoot(string table = "street")
        {
            var items = T.Loot(table);
            int total = items.Sum(l => l.weight);
            int r = _rng.Next(total);
            foreach (var l in items)
            {
                if (r < l.weight) return l;
                r -= l.weight;
            }
            return items[^1];
        }

        void InsertFuses()
        {
            int put = Mathf.Min(Me.Bag["fuse"], T.station.fusesNeeded - FusesIn);
            Me.Bag["fuse"] -= put;
            FusesIn += put;
            if (FusesIn < T.station.fusesNeeded) { Say($"퓨즈를 끼웠다 ({FusesIn}/{T.station.fusesNeeded})"); return; }
            Powered = true;
            foreach (var l in City.StationLights) l.enabled = true;
            Noise(City.Panel, 12f);
            Say(_pharmacyFuseRecovered ? "박 기관사 · 약사님이 남긴 메모군요. 승강장에 마지막 신호가 들어왔습니다."
                : "승강장에 전력이 들어왔다. 기관사와 연락하고 열차를 출발시키자");
        }

        /// <summary>Trade panel: 1-3 buys an offer, E/Esc or walking off closes it.</summary>
        void HandleTrade()
        {
            var k = Keyboard.current;
            if (k == null) return;
            for (int i = 0; i < TodayTrades.Count; i++)
                if ((i == 0 ? k.digit1Key : i == 1 ? k.digit2Key : k.digit3Key).wasPressedThisFrame)
                {
                    Me.Trade(TodayTrades[i], out var msg);
                    Say(msg);
                }
            if (k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame) Trading = false;
        }

        // ---------------------------------------------------------------- airdrop

        /// <summary>
        /// Once a day a supply crate parachutes into a street nearby, trailing red smoke. It holds
        /// several rolls of the best loot table — and the flare keeps calling zombies until you open it.
        /// </summary>
        void SpawnDrop()
        {
            _dropDone = true;
            Vector2? landing = null;
            for (int i = 0; i < 96; i++)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2;
                var p = Me.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(T.airdrop.minDistance, T.airdrop.maxDistance, (float)_rng.NextDouble());
                if (Placeable(p) && !InWall(p, 1.5f, City.Walls)) { landing = p; break; }
            }
            if (!landing.HasValue) return;
            Vector2 at = landing.Value;
            var crate = new GameObject("airdrop").transform;
            crate.position = Fx.V3(at);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(crate, false);
            body.transform.localPosition = Vector3.up * 0.45f;
            body.transform.localScale = new Vector3(1.4f, 0.9f, 0.9f);
            City.Paint(body, new Color(0.3f, 0.36f, 0.2f));
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(crate, false);
            band.transform.localPosition = Vector3.up * 0.45f;
            band.transform.localScale = new Vector3(0.25f, 0.95f, 0.95f);
            City.Paint(band, new Color(0.85f, 0.75f, 0.2f));
            for (int i = -1; i <= 1; i += 2)
            {
                var runner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(runner.GetComponent<Collider>());
                runner.transform.SetParent(crate, false);
                runner.transform.localPosition = new Vector3(0, 0.11f, i * 0.34f);
                runner.transform.localScale = new Vector3(1.55f, 0.18f, 0.18f);
                City.Paint(runner, new Color(0.12f, 0.14f, 0.12f));
            }
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(crate, false);
            beacon.transform.localPosition = new Vector3(0, 1.03f, 0);
            beacon.transform.localScale = new Vector3(0.22f, 0.30f, 0.22f);
            _beaconLens = beacon.GetComponent<Renderer>();
            _beaconLens.sharedMaterial = Resources.Load<Material>("Materials/Neon");
            _drop = new City.Container { pos = at, t = crate, kind = "보급 상자", table = "drop", basePos = crate.position };
            City.Containers.Add(_drop);
            LightBeacon(new Color(1f, 0.15f, 0.1f));
            Noise(at, T.airdrop.flareNoise);
            Say("긴급 보급 도착 — 회수하거나 밤의 유인등으로 남길 수 있다");
        }

        void LightBeacon(Color color)
        {
            _flareLight = new GameObject("flare").AddComponent<Light>();
            _flareLight.transform.position = Fx.V3(_drop.pos, 1.2f);
            _flareLight.type = LightType.Point;
            _flareLight.color = color;
            var signal = new MaterialPropertyBlock();
            signal.SetColor("_BaseColor", color);
            signal.SetColor("_EmissionColor", color * 4f);
            _beaconLens.SetPropertyBlock(signal);
            _flareLight.range = 9;
            _flareLight.intensity = 5;
            _flareT = 0;
        }

        void UpdateFlare(float dt)
        {
            if (_flareLight == null) return;
            _flareT -= dt;
            _flareLight.intensity = 4f + Mathf.PerlinNoise(Time.time * 9f, 0) * 3f;
            if (!BeaconActive && Random.value < dt * 10f) _fx?.Flare(_drop.pos);
            if (BeaconActive) return; // The night call diverts zombies that are far enough from the player.
            if (_flareT > 0) return;
            _flareT = T.airdrop.flareEvery;
            Noise(_drop.pos, T.airdrop.flareNoise);
        }

        void EndFlare()
        {
            if (_flareLight) Destroy(_flareLight.gameObject);
            _flareLight = null;
            if (!_beaconLens) return;
            var signal = new MaterialPropertyBlock();
            signal.SetColor("_BaseColor", Color.gray);
            signal.SetColor("_EmissionColor", Color.black);
            _beaconLens.SetPropertyBlock(signal);
        }

        void RepairBarricade()
        {
            Me.Bag["scrap"]--;
            if (BarricadeHp <= 0) City.ZombieWalls.Add(City.Door);
            BarricadeHp = Mathf.Min(T.shelter.barricadeHp, BarricadeHp + T.shelter.repairPerScrap);
            Noise(Me.Pos, 6f);
            Say("바리케이드를 덧댔다 (쾅쾅)");
        }

        /// <summary>Zombies pressed against the door wear the barricade down; at zero it falls.</summary>
        void UpdateBarricade(float dt)
        {
            if (BarricadeHp <= 0) return;
            float dps = 0;
            foreach (int i in Horde.Near(City.Door.center, 1.6f, _near))
                if (Horde.IsAware(i)) dps += Horde.Type(i).dps * 0.6f;
            if (dps <= 0) return;
            BarricadeHp -= dps * dt;
            if (BarricadeHp > 0) return;
            BarricadeHp = 0;
            City.ZombieWalls.Remove(City.Door);
            _fx?.Explosion(City.Door.center, 1.2f);
            Say("바리케이드가 뚫렸다!");
            if (Sleeping) Wake("바리케이드가 뚫렸다!");
        }

        void Wake(string why)
        {
            Sleeping = false;
            Say(why);
        }

        // ---------------------------------------------------------------- spawning

        void Spawn(float dt)
        {
            var sp = T.spawn;
            float rate = Phase == "evac" ? sp.evacPerSec * EvacSpawnMultiplier : Phase == "night" ? sp.nightPerSec : sp.dayPerSec;
            rate *= 1 + sp.growthPerDay * Mathf.Min(Day - 1, 5);
            _spawnAcc += rate * dt;
            while (_spawnAcc >= 1)
            {
                _spawnAcc -= 1;
                if (Horde.Count >= sp.maxAlive) continue;
                var alive = Players.Where(p => p.Alive).ToList();
                if (alive.Count == 0) return;
                int i = SpawnOne(alive[_rng.Next(alive.Count)].Pos, sp.ringMin, sp.ringMax);
                if (i >= 0 && (IsNight || Phase == "evac") && !TryDivert(i, Me.Pos)) Horde.Alert(i, Me.Pos, 8f);
            }
        }

        int Kind()
        {
            var sp = T.spawn;
            double r = _rng.NextDouble();
            if (Day >= 3 && r > 0.94) return Horde.Armored;
            if (Day >= 2 && r > 0.89) return Horde.Screamer;
            return Day >= sp.bruteFromDay && r < sp.brute ? 2 : r < (IsNight ? sp.runnerNight : sp.runnerDay) ? 1 : 0;
        }

        /// <summary>Spawns one zombie on a ring around `anchor`; returns its index or -1.</summary>
        public int SpawnOne(Vector2 anchor, float ringMin, float ringMax, int? kind = null)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2;
                var p = anchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(ringMin, ringMax, (float)_rng.NextDouble());
                if (!Placeable(p)) continue;
                Horde.Spawn(p, kind ?? Kind(), 1 + T.spawn.hpGrowthPerDay * (Day - 1));
                return Horde.Count - 1;
            }
            return -1;
        }

        void SpawnAnywhere(float awayFromStart)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var p = new Vector2((float)_rng.NextDouble() * 2 - 1, (float)_rng.NextDouble() * 2 - 1) * (City.Half - 2);
                if (Vector2.Distance(p, City.Start) < awayFromStart || !Placeable(p)) continue;
                Horde.Spawn(p, Kind());
                return;
            }
        }

        bool Placeable(Vector2 p) => Mathf.Abs(p.x) < City.Half - 1 && Mathf.Abs(p.y) < City.Half - 1 && !InWall(p, 0.5f, City.ZombieWalls) && !InShelter(p);

        static bool InWall(Vector2 p, float r, List<Rect> walls)
        {
            foreach (var w in walls)
                if (new Rect(w.xMin - r, w.yMin - r, w.width + 2 * r, w.height + 2 * r).Contains(p)) return true;
            return false;
        }

        // ---------------------------------------------------------------- attacks (IWorld)

        public void SwingFx(Vector2 from, Vector2 dir, float reach, float half, string weapon, bool stealthKill)
        {
            if (_fx == null) return;
            _fx.Fan(from, dir, reach, half, stealthKill ? new Color(0.6f, 0.05f, 0.05f, 0.5f) : new Color(1f, 0.95f, 0.85f, 0.4f));
            if (!stealthKill) _combatAudio.Play("swing");
        }

        public void SpawnNail(Vector2 from, Vector2 dir, float dmg, int pierce)
        {
            _combatAudio.Play("nail");
            var t = Visual(PrimitiveType.Cube, new Color(1f, 0.85f, 0.55f), 3f, new Vector3(0.06f, 0.06f, 0.55f));
            var trail = t.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.08f;
            trail.widthMultiplier = 0.08f;
            trail.sharedMaterial = _fx ? _fx.add : null;
            trail.startColor = new Color(1f, 0.8f, 0.4f, 0.8f);
            trail.endColor = new Color(1f, 0.5f, 0.2f, 0);
            t.rotation = Quaternion.LookRotation(Fx.V3(dir));
            t.position = Fx.V3(from + dir * 0.5f, 1.1f);
            _fx?.FlashLight(from + dir * 0.6f, new Color(1f, 0.8f, 0.5f), 5f, 4f, 0.06f);
            _nails.Add(new Nail { t = t, pos = from + dir * 0.5f, dir = dir, dmg = dmg, pierce = pierce, life = 0.7f });
        }

        public void SpawnFirework(Vector2 from, Vector2 to, float dmg, float radius, float flight)
        {
            var t = Visual(PrimitiveType.Sphere, new Color(1f, 0.55f, 0.2f), 6f, Vector3.one * 0.3f);
            var trail = t.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthMultiplier = 0.25f;
            trail.sharedMaterial = _fx ? _fx.add : null;
            trail.startColor = new Color(1f, 0.7f, 0.3f, 1);
            trail.endColor = new Color(1f, 0.2f, 0.1f, 0);
            _fireworks.Add(new Firework { t = t, from = from, to = to, flight = flight, dmg = dmg, r = radius });
        }

        Transform Visual(PrimitiveType type, Color c, float glow, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Resources.Load<Material>("Materials/Neon");
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_EmissionColor", c * glow);
            r.SetPropertyBlock(mpb);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void Explode(Vector2 c, float r, float dmg)
        {
            foreach (int i in Horde.Near(c, r, _near))
            {
                if (Horde.Hp[i] <= 0) continue;
                Vector2 impact = Horde.Pos[i];
                Vector2 away = (impact - c).normalized;
                float applied = Mathf.Min(dmg, Horde.Hp[i]);
                Horde.Damage(i, dmg, 0.16f, away * 1.2f);
                ReportImpact(impact, away, applied, Horde.Hp[i] <= 0, false);
            }
            foreach (var p in Players)
                if (p.Alive && Vector2.Distance(p.Pos, c) < r) HurtPlayer(p, dmg * 0.3f, c);
            Noise(c, T.Weapon("fireworks").noise);
            _fx?.Explosion(c, r);
            _combatAudio.Play("blast");
        }

        void UpdateNails(float dt)
        {
            for (int n = _nails.Count - 1; n >= 0; n--)
            {
                var nail = _nails[n];
                nail.pos += nail.dir * T.Weapon("nailgun").speed * dt;
                nail.life -= dt;
                foreach (int i in Horde.Near(nail.pos, 0.2f, _near))
                {
                    if (Horde.Hp[i] <= 0 || nail.pierce < 0 || !nail.hit.Add(Horde.Id[i])) continue;
                    float applied = Mathf.Min(nail.dmg, Horde.Hp[i]);
                    Horde.Damage(i, nail.dmg, 0, nail.dir * 0.3f);
                    Horde.Alert(i, Me.Pos, T.senses.forgetSeconds);
                    ReportImpact(Horde.Pos[i], nail.dir, applied, Horde.Hp[i] <= 0, false);
                    nail.pierce--;
                }
                nail.t.position = Fx.V3(nail.pos, 1.1f);
                if (nail.life > 0 && nail.pierce >= 0 && !InWall(nail.pos, 0, City.Walls)) continue;
                Destroy(nail.t.gameObject);
                _nails.RemoveAt(n);
            }
        }

        void UpdateFireworks(float dt)
        {
            for (int n = _fireworks.Count - 1; n >= 0; n--)
            {
                var f = _fireworks[n];
                f.time += dt;
                float k = Mathf.Min(f.time / f.flight, 1);
                var p = Vector2.Lerp(f.from, f.to, k);
                f.t.position = Fx.V3(p, 1f + Mathf.Sin(k * Mathf.PI) * 3.2f);
                if (k < 1) continue;
                Explode(f.to, f.r, f.dmg);
                Destroy(f.t.gameObject);
                _fireworks.RemoveAt(n);
            }
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            _swingLock = Mathf.Max(0f, _swingLock - Time.deltaTime);
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var k = Keyboard.current;
            if (k != null && (Phase == "won" || Phase == "lost") && k.rKey.wasPressedThisFrame)
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            if (HandleOpeningMenu(k))
            {
                Render(dt);
                return;
            }
            TickMelee(dt);
            if (Trading && Phase != "won" && Phase != "lost")
            {
                HandleTrade();
                if (Vector2.Distance(Me.Pos, City.Trader) > 3.5f) Trading = false;
            }
            else if (Phase != "won" && Phase != "lost")
            {
                if (_lootSource == null && !Sleeping && k != null && k.tabKey.wasPressedThisFrame) _bagOpen = !_bagOpen;
                if (_bagOpen && k != null && k.escapeKey.wasPressedThisFrame) _bagOpen = false;
                if (_lootSource != null) HandleLoot(k);
                else if (_bagOpen) HandleBag(k);
                else if (_motionPreview) StepMotionPreview(dt);
                else HandleInput(dt);
                int steps = Sleeping ? Mathf.RoundToInt(T.clock.sleepSpeed) : 1;
                for (int s = 0; s < steps && Phase != "won" && Phase != "lost"; s++) Simulate(dt);
            }
            Render(dt);
        }

        const float MeleeWindup = 0.11f;
        string _meleeWeapon;
        float _meleeRemaining;
        Vector2 _meleeDirection;

        void MoveFromInput(Vector2 input, float dt)
        {
            Vector3 right = _cam.transform.right;
            var screenRight = new Vector2(right.x, right.z);
            var screenUp = new Vector2(-screenRight.y, screenRight.x);
            var travel = (screenRight * input.x + screenUp * input.y).normalized;
            float pace = _meleeWeapon != null ? 0.50f : _swingLock > 0f ? 0.72f : 1f;
            Me.Pos = Horde.Slide(Me.Pos, travel * (Me.Speed * pace * dt), 0.4f, City.Walls);
        }

        void QueueMelee()
        {
            if (_meleeWeapon != null || !Me.CanAttack) return;
            var toward = _aimPoint - Me.Pos;
            _meleeDirection = toward.sqrMagnitude > 0.0001f ? toward.normalized : Me.Aim;
            _meleeWeapon = Me.Held.id;
            _meleeRemaining = MeleeWindup;
            _actors.BeginMelee(_meleeDirection, _meleeWeapon, Me.HeldCfg.cooldown, MeleeWindup);
        }

        void TickMelee(float dt)
        {
            if (_meleeWeapon == null) return;
            if (!Me.Alive || Me.Held.id != _meleeWeapon || Sleeping || _lootSource != null || _bagOpen || Trading)
            {
                _meleeWeapon = null;
                _actors.CancelMelee();
                return;
            }
            _meleeRemaining -= dt;
            if (_meleeRemaining > 0f) return;
            _meleeWeapon = null;
            if (!Me.Attack(Me.Pos + _meleeDirection, this, Players.Count(p => p.Alive)))
            {
                _actors.CancelMelee();
                return;
            }
            _swingLock = 0.16f;
            if (Me.LastWasStealthKill) ConfirmStealthKill();
        }

        void BuildCamera()
        {
            var go = new GameObject("Camera");
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = false;
            _cam.fieldOfView = CameraFov;
            _cam.nearClipPlane = 0.5f;
            _cam.farClipPlane = 150;
            _cam.backgroundColor = Color.black;
            var cameraData = go.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            go.AddComponent<AudioListener>();
            go.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0);
        }

        static readonly Vector3 OldCameraOffset = Quaternion.Euler(56, 0, 0) * Vector3.back * 20f;
        Vector3 _cameraFocus;

        void FramePlayer(float dt, bool snap)
        {
            var focus = Fx.V3(Me.Pos);
            if (_occlusionComparison)
            {
                _cam.orthographic = false;
                _cam.fieldOfView = 38;
                _cam.transform.SetPositionAndRotation(focus + OldCameraOffset, Quaternion.Euler(56, 0, 0));
                return;
            }
            if (_args.ContainsKey("side")) // motion QA: low side view shows torso lean and stride
            {
                _cam.fieldOfView = 30;
                _cam.transform.SetPositionAndRotation(focus + new Vector3(-6.5f, 1.6f, 0.4f), Quaternion.Euler(6, 90, 0));
                return;
            }
            _cameraFocus = snap ? focus : Vector3.Lerp(_cameraFocus, focus, 1f - Mathf.Exp(-dt * 7f));
            _cam.transform.position = _cameraFocus - _cam.transform.forward * CameraDistance;
            _cam.fieldOfView = CameraFov;
        }

        void BuildPlayerExtras()
        {
            var p = _actors.Player.transform;
            _flashlight = new GameObject("flashlight").AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.transform.SetParent(p, false);
            _flashlight.transform.localPosition = new Vector3(0, 1.5f, 0.2f);
            _flashlight.transform.localRotation = Quaternion.Euler(20, 0, 0);
            _flashlight.spotAngle = 62;
            _flashlight.innerSpotAngle = 30;
            _flashlight.range = 17;
            _flashlight.color = new Color(1f, 0.95f, 0.85f);
            _flashlight.shadows = LightShadows.Soft;
            _glow = new GameObject("glow").AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.transform.SetParent(p, false);
            _glow.transform.localPosition = new Vector3(0, 3f, 0);
            _glow.range = 8;
            _glow.color = new Color(1f, 0.9f, 0.8f);

            // weapon reach: the red circle around you, and a faint aim line
            _reach = Line("reach", new Color(1f, 0.55f, 0.3f, 0.22f), 0.035f);
            _reach.loop = true;
            _reach.positionCount = 64;
            _aimLine = Line("aim", new Color(1f, 0.3f, 0.2f, 0.35f), 0.04f);
            _aimLine.positionCount = 2;
        }

        LineRenderer Line(string name, Color c, float width)
        {
            var l = new GameObject(name).AddComponent<LineRenderer>();
            l.sharedMaterial = _fx.add;
            l.widthMultiplier = width;
            l.startColor = l.endColor = c;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return l;
        }

        void Render(float dt)
        {
            _dark = Mathf.MoveTowards(_dark, IsNight ? 1 : 0, dt / 3f);
            City.SetDarkness(_dark);
            MoveRain(_dark);
            _flashlight.intensity = Sleeping ? 0 : Mathf.Lerp(0f, 9f, _dark);
            _glow.intensity = Mathf.Lerp(0.3f, 1.4f, _dark);
            float hurt = Mathf.Clamp01(0.5f - Me.Hp / Me.MaxHp);
            float attackFlash = _damageCue / 0.55f;
            City.Vignette.intensity.value = Mathf.Lerp(0.14f, 0.34f, _dark) + hurt * 0.5f + attackFlash * 0.19f + (Me.Crouching ? 0.12f : 0);
            City.Vignette.color.value = Color.Lerp(Color.black, new Color(0.5f, 0, 0), Mathf.Max(hurt * 2f, attackFlash));

            _actors.SyncHorde(Horde, dt, _attackers.Count > 0 ? _encounterPosition : null, _windingZombies);
            _actors.SyncPlayer(Me, _moving, dt, _playerRecoil > 0f);

            var w = Me.HeldCfg;
            float reach = w.kind == "melee" ? w.range : 1.0f;
            var center = Fx.V3(Me.Pos, 0.06f);
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                _reach.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * reach);
            }
            float aimLen = w.kind == "melee" ? w.range : Mathf.Min(w.range, Vector2.Distance(_aimPoint, Me.Pos));
            _aimLine.SetPosition(0, Fx.V3(Me.Pos + Me.Aim * 0.4f, 0.08f));
            _aimLine.SetPosition(1, Fx.V3(Me.Pos + Me.Aim * aimLen, 0.08f));
            _reach.enabled = _aimLine.enabled = Me.Alive && !Sleeping;

            var bar = City.Barricade;
            if (bar)
            {
                bar.gameObject.SetActive(BarricadeHp > 0);
                bar.localScale = new Vector3(1, Mathf.Lerp(0.35f, 1f, BarricadeHp / T.shelter.barricadeHp), 1);
            }

            FramePlayer(dt, false);

            bool open = Phase == "evac";
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            City.StationRing.enabled = open && _escapeRoute == EscapeRoute.Rail;
            City.RoadRing.enabled = open && _escapeRoute == EscapeRoute.Road;
            City.DrainRing.enabled = open && _escapeRoute == EscapeRoute.Drain;
            var ring = _escapeRoute == EscapeRoute.Road ? City.RoadRing : _escapeRoute == EscapeRoute.Drain ? City.DrainRing : City.StationRing;
            if (open) ring.sharedMaterial.SetColor("_BaseColor", new Color(0.35f, 1f, 0.62f, 0.65f + 0.35f * pulse));

            // the container being searched rattles; searchable ones get a marker in the HUD
            foreach (var c in City.Containers)
                if (c == _searching) c.t.position = c.basePos + new Vector3(Mathf.Sin(Time.time * 47f), 0, Mathf.Cos(Time.time * 39f)) * 0.035f;
            _actors.Searching = _searching != null;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                _pops[i].t += dt;
                if (_pops[i].t > 1.8f) _pops.RemoveAt(i);
            }

            if (_hud == null) return;
            int chasing = 0;
            for (int i = 0; i < Horde.Count; i++)
                if (Horde.IsAware(i) && (Horde.Goal[i] - Me.Pos).sqrMagnitude < 4f && (Horde.Pos[i] - Me.Pos).sqrMagnitude < 400) chasing++;
            var routeMarker = RouteMarker();
            var playerHead = Fx.V3(Me.Pos, 1.6f);
            City.CutAway(_cam.transform.position, playerHead, Fx.V3(Me.Pos, 0.3f));
            var playerCovered = City.PlayerCovered(playerHead, -_cam.transform.forward * CameraDistance);
            var shelterSp = _cam.WorldToScreenPoint(Fx.V3(City.Door.center, 2f));
            _hud.Refresh(new Hud.State
            {
                day = Day, kills = Kills, stealthKills = StealthKills,
                phase = Phase == "won" || Phase == "lost" ? (IsNight ? "night" : "day") : Phase, phaseT = PhaseT,
                hp = Me.Hp, maxHp = Me.MaxHp, hunger = Me.Hunger, hungerMax = T.player.hungerMax, downed = !Me.Alive,
                evacT = PhaseT, evacNeed = EvacNeed, nightLighting = IsNight,
                objectives = Objectives(), streetEvent = StreetEventStatus(), beaconActive = BeaconActive,
                trades = Trading ? TodayTrades.Select(t => $"{Survivor.Name(t.give)} {t.giveN}  →  {Survivor.Name(t.get)} {t.getN}" + TradeWarning(t)).ToList() : null,
                shops = City.Shops.Where(sh => (sh.pos - Me.Pos).sqrMagnitude < 30 * 30).Select(sh => (_cam.WorldToScreenPoint(Fx.V3(sh.pos, 3.2f)), sh.name, sh.color)).ToList(),
                dropScreen = _flareLight ? _cam.WorldToScreenPoint(Fx.V3(_drop.pos, 1.5f)) : Vector3.zero, showDrop = _flareLight != null,
                dropDist = _drop != null ? Vector2.Distance(_drop.pos, Me.Pos) : 0,
                slots = Me.Slots.Select(s => (T.Weapon(s.id).name, s.durability, T.Weapon(s.id).durability, s.id == "nailgun" ? Me.Bag["nails"] : s.id == "fireworks" ? Me.Bag["fireworks"] : -1)).ToList(),
                current = Me.Current, bag = Me.Bag, bagLoad = Me.BagLoad, bagCapacity = Me.BagCapacity,
                bagOpen = _bagOpen && Phase != "won" && Phase != "lost", bagRow = _bagRow,
                encounter = TrackedOpportunity(),
                loot = LootHud(), tutorial = TutorialHud(), menuOpen = _menuOpen, radioLog = RadioLog, canSkipPractice = InPractice,
                stealth = GetStealthHud(), combat = CombatHud(),
                playerMarker = playerCovered && Me.Alive ? _cam.WorldToScreenPoint(playerHead) : Vector3.zero,
                prompt = _prompt, searchK = _searching != null ? _searchT / T.shelter.searchSeconds : -1,
                barricade = BarricadeHp / T.shelter.barricadeHp, showBarricade = IsNight || BarricadeHp < T.shelter.barricadeHp || InShelter(Me.Pos),
                sleeping = Sleeping, crouching = Me.Crouching, chasing = chasing,
                stationScreen = routeMarker.screen, stationOpen = routeMarker.active, routeLabel = routeMarker.label,
                stationOnScreen = OnScreen(routeMarker.screen),
                shelterScreen = shelterSp, shelterOnScreen = OnScreen(shelterSp), shelterDist = Vector2.Distance(Me.Pos, City.Door.center),
                pops = _pops.Select(p => (_cam.WorldToScreenPoint(p.at + Vector3.up * p.t * 0.9f) + Vector3.up * p.screenLift, p.text, p.color, Mathf.Clamp01(1.8f - p.t) * Mathf.Clamp01(p.t * 6f))).ToList(),
                lootMarks = Trading ? new List<Vector3>() : City.Containers.Where(c => !c.searched && c != _searching && ContainerAccessible(c) && (c.pos - Me.Pos).sqrMagnitude < 14 * 14)
                    .Select(c => _cam.WorldToScreenPoint(Fx.V3(c.pos, 1.5f))).ToList(),
                showShelter = !InShelter(Me.Pos) && Phase != "evac" && Vector2.Distance(Me.Pos, City.Door.center) > 6f,
            }, dt);
        }

        string Objectives() => RouteObjectives();

        static bool OnScreen(Vector3 sp) => sp.z > 0 && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;

        void Say(string s) => _hud?.Say(s);

        // ---------------------------------------------------------------- screenshot scenes

        void ReadArgs()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i].StartsWith("--")) _args[a[i].Substring(2)] = a[i + 1];
        }

        /// <summary>Jumps the run to a moment worth looking at (screenshots and tests).</summary>
        public void SetupScene(string name)
        {
            switch (name)
            {
                case "verify-pharmacy":
                    VerifyPharmacy();
                    break;
                case "pharmacy-event":
                    Day = 2;
                    StartPhase("day");
                    Me.Pos = _pharmacyCase.pos + Vector2.down * 1.2f;
                    Me.Take("scrap", 1);
                    break;
                case "keyboard-qa":
                    Me.Pos = City.MarketSquare;
                    FramePlayer(0f, true);
                    break;
                case "story-mechanic":
                case "story-rescue":
                    Day = name == "story-mechanic" ? 2 : 3;
                    StartPhase("day");
                    PhaseT = _encounterAt;
                    Me.Pos = _storyEncounterAt + Vector2.down * 1.25f;
                    Me.Take("pipe", 1);
                    Me.Take("bandage", 1);
                    TickEncounter(0f);
                    FramePlayer(0f, true);
                    break;
                case "verify-survivors":
                    VerifyStoryEncounters();
                    break;
                case "verify-controls":
                    VerifyControls();
                    break;
                case "motion-preview":
                    _motionPreview = true;
                    Me.Pos = City.MarketSquare + Vector2.down * 2.5f;
                    Horde.Spawn(Me.Pos + Vector2.up * 7f + Vector2.left * 1.4f, 0);
                    Horde.Spawn(Me.Pos + Vector2.up * 8f + Vector2.right * 1.4f, 1);
                    break;
                case "attack-facing":
                    Me.Pos = City.MarketSquare;
                    Me.Take("pipe", 1);
                    VerifyAttackFacing();
                    break;
                case "combat-impact":
                case "combat-incoming":
                case "combat-hurt":
                    SetupCombatCapture(name);
                    break;
                case "shop-interior":
                    Me.Pos = City.ClinicCenter + Vector2.down * 1f;
                    Me.Take("pipe", 1);
                    break;
                case "hardware-interior":
                    Me.Pos = City.WorkshopCenter + Vector2.down * 1f;
                    Me.Take("pipe", 1);
                    break;
                case "verify-interiors":
                    VerifyWalkInShops();
                    break;
                case "shelter-cutaway":
                    StartPhase("night");
                    _dark = 1f;
                    Me.Pos = City.Start;
                    break;
                case "maintenance":
                    Me.Pos = City.DrainContact + Vector2.down * 1.1f;
                    _trackedRoute = EscapeRoute.Drain;
                    break;
                case "story-clue":
                    DiscoverRoute(EscapeRoute.Drain);
                    Me.Pos = _cluePositions[(int)EscapeRoute.Drain] + Vector2.down * 0.8f;
                    InteractRouteClue(false);
                    FramePlayer(0f, true);
                    break;
                case "verify-routes":
                    VerifyEscapeRoutes();
                    break;
                case "road":
                case "drain":
                    var route = name == "road" ? EscapeRoute.Road : EscapeRoute.Drain;
                    Me.Pos = RoutePosition(route) + Vector2.down * 0.8f;
                    Me.Take("pipe", 1);
                    DiscoverRoute(route);
                    RecordClue(route);
                    if (route == EscapeRoute.Road) Me.Take("scrap", T.escape.roadScrap);
                    else
                    {
                        Vector2 startup = Me.Pos;
                        foreach (Vector2 valve in City.DrainValves)
                        {
                            Me.Pos = valve;
                            InteractDrainValve(true);
                        }
                        Me.Pos = startup;
                    }
                    TryBeginEscape(route);
                    break;
                case "camera-walk":
                    VerifySteadyCamera();
                    break;
                case "tutorial-aim":
                    BeginTutorial();
                    _lesson = Lesson.Stealth;
                    Me.Take("pipe", 1);
                    Me.Pos = TrainingZombiePos + Vector2.down * 1.25f;
                    Me.Aim = Vector2.up;
                    ResetPracticeZombie();
                    break;
                case "loot":
                case "loot-selected":
                    Me.Pos = City.MarketSquare;
                    var parcel = LeaveSupplies(Me.Pos + Vector2.up * 1.3f, ("food", 2));
                    parcel.kind = "시장 상자";
                    AddLoot(parcel.loot, "fuse", 1);
                    AddLoot(parcel.loot, "bandage", 2);
                    OpenLoot(parcel);
                    if (name == "loot-selected") ChooseLoot(1);
                    break;
                case "controls":
                    _menuOpen = true;
                    break;
                case "story-day2":
                case "story-day3":
                case "story-day5":
                    Day = name == "story-day2" ? 2 : name == "story-day3" ? 3 : 5;
                    StartPhase("day");
                    Me.Pos = Horde.PushOut(_storyCache.pos + Vector2.up * 5.6f, 0.4f, City.Walls);
                    break;
                case "bag":
                    Me.Pos = City.MarketSquare;
                    Me.Take("food", 3);
                    Me.Take("scrap", 3);
                    Me.Take("nails", 20);
                    Me.Take("bandage", 2);
                    _bagOpen = true;
                    break;
                case "bag-overflow":
                    Me.Pos = City.MarketSquare;
                    Me.Take("nails", Me.BagCapacity);
                    Me.Hunger = 0;
                    Me.Eat();
                    LeaveSupplies(Me.Pos + Vector2.up * 1.5f, ("food", 2));
                    break;
                case "request":
                    Day = T.encounters.requestDay;
                    Me.Pos = City.MarketSquare;
                    StartEncounter(EncounterKind.Request, Me.Pos + Vector2.up * 1.4f);
                    Me.Take(_requestItem, 1);
                    break;
                case "rescue":
                case "rescue-reward":
                    Day = T.encounters.rescueDay;
                    Me.Pos = City.MarketSquare + Vector2.down * (name == "rescue" ? 3.5f : 1.4f);
                    Me.Take("pipe", 1);
                    StartEncounter(EncounterKind.Rescue, City.MarketSquare);
                    if (name == "rescue-reward")
                        for (int i = 0; i < Horde.Count; i++)
                            if (_attackers.Contains(Horde.Id[i])) Horde.Damage(i, Horde.Hp[i] + 1, 0, Vector2.zero);
                    break;
                case "market":
                    Me.Pos = City.MarketSquare;
                    Me.Aim = Vector2.up;
                    Me.Take("pipe", 1);
                    break;
                case "occlusion-before":
                case "occlusion":
                    _occlusionComparison = name == "occlusion-before";
                    Me.Pos = City.FindOccludedAlley(OldCameraOffset);
                    Me.Take("pipe", 1);
                    Horde.Spawn(Me.Pos + Vector2.right * 1.15f, 0);
                    Horde.Spawn(Me.Pos + Vector2.up * 1.8f, 0);
                    break;
                case "day":   // just outside the door, zombies wandering
                    Me.Pos = City.Door.center + new Vector2(0, 2.5f);
                    for (int k = 0; k < 14; k++) SpawnOne(Me.Pos, 5, 13);
                    Me.Take("pipe", 1);
                    break;
                case "sneak": // crouched behind an unaware zombie
                    Me.Pos = City.Door.center + new Vector2(2, 3f);
                    Me.Take("knife", 1);
                    for (int k = 0; k < 8; k++) SpawnOne(Me.Pos, 6, 12);
                    Horde.Spawn(Me.Pos + new Vector2(0, 1.6f), 0);
                    Horde.Heading[Horde.Count - 1] = Vector2.up;
                    Me.Aim = Vector2.up;
                    break;
                case "fight":
                    Me.Pos = City.Door.center + new Vector2(4, 6f);
                    Me.Take("axe", 1);
                    Me.Take("nailgun", 1);
                    Me.Take("nails", 30);
                    Me.Take("fireworks", 3);
                    Me.Select(1);
                    for (int k = 0; k < 40; k++)
                    {
                        int i = SpawnOne(Me.Pos, 2.5f, 9);
                        if (i >= 0) Horde.Alert(i, Me.Pos, 30);
                    }
                    Me.Attack(Me.Pos + Vector2.up, this);
                    break;
                case "night":  // siege at the shelter door
                    Day = 2;
                    StartPhase("night");
                    PhaseT = 70;
                    Me.Pos = City.Door.center + new Vector2(0, -1.6f);
                    Me.Take("bat", 1);
                    Me.Take("scrap", 3);
                    BarricadeHp = T.shelter.barricadeHp * 0.55f;
                    for (int k = 0; k < 90; k++)
                    {
                        int i = SpawnOne(City.Door.center + Vector2.up * 4f, 0.5f, 10);
                        if (i >= 0) Horde.Alert(i, City.Door.center + Vector2.up, 30);
                    }
                    _dark = 1;
                    break;
                case "sleep":
                    Day = 3;
                    StartPhase("night");
                    Me.Pos = City.Bed + new Vector2(-0.9f, 0);
                    Sleeping = true;
                    _dark = 1;
                    break;
                case "evac":
                    Day = 2;
                    PrepareEscape(EscapeRoute.Rail);
                    Me.Pos = City.Station + new Vector2(-2, -1);
                    Me.Take("axe", 1);
                    PhaseT = 28;
                    for (int k = 0; k < 120; k++)
                    {
                        int i = SpawnOne(Me.Pos, 4, 16);
                        if (i >= 0) Horde.Alert(i, Me.Pos, 30);
                    }
                    _dark = 1;
                    break;
                case "search":
                {
                    var c = City.Containers.OrderBy(x => Vector2.Distance(x.pos, City.Door.center)).First();
                    Me.Pos = c.pos + (City.Door.center - c.pos).normalized * 1.35f;
                    Me.Aim = (c.pos - Me.Pos).normalized;
                    for (int k = 0; k < 6; k++) SpawnOne(Me.Pos, 9, 14);
                    break;
                }
                case "kill":
                    Me.Pos = City.Door.center + new Vector2(0, 5f);
                    Me.Take("axe", 1);
                    for (int k = 0; k < 14; k++)
                    {
                        int i = SpawnOne(Me.Pos, 2.5f, 7);
                        if (i >= 0) Horde.Alert(i, Me.Pos, 30);
                    }
                    break;
                case "station":
                    Me.Pos = City.Trader + new Vector2(1.5f, -3.4f);
                    Me.Take("fuse", 2);
                    Me.Take("scrap", 4);
                    FusesIn = 1;
                    Me.Aim = Vector2.up;
                    break;
                case "trade":
                    Me.Pos = City.Trader + new Vector2(0, -2f);
                    Me.Take("scrap", 6);
                    Me.Take("food", 2);
                    Trading = true;
                    break;
                case "blackout":
                    Day = T.streetEvents.blackoutDay;
                    StartPhase("night");
                    PhaseT = T.clock.nightSeconds - T.streetEvents.blackoutAfter;
                    Me.Pos = City.ShelterBreaker + Vector2.right;
                    Me.Take("scrap", T.streetEvents.blackoutScrap);
                    _dark = 1;
                    break;
                case "beacon-street":
                case "beacon":
                    Me.Pos = City.Door.center + Vector2.up * 2f;
                    SpawnDrop();
                    if (_drop == null) break;
                    Me.Pos = _drop.pos;
                    TryReserveBeacon();
                    Me.Pos = City.Start;
                    if (name == "beacon-street")
                    {
                        Me.Pos = Horde.PushOut(_drop.pos + Vector2.down * 5f, 0.4f, City.Walls);
                        Me.Take("pipe", 1);
                    }
                    StartPhase("night");
                    if (name == "beacon-street")
                        for (int i = 0; i < 14; i++) SpawnOne(_drop.pos, 3f, 10f);
                    _dark = 1;
                    break;
                case "drop":
                    Me.Pos = City.Door.center + new Vector2(0, 3f);
                    Me.Take("pipe", 1);
                    SpawnDrop();
                    if (_drop != null) Me.Pos = _drop.pos + Vector2.right;
                    break;
                case "shop":
                {
                    var sh = City.Shops.First(x => x.name == "철물점");
                    Me.Pos = sh.pos + new Vector2(0.3f, -0.6f);
                    Me.Take("knife", 1);
                    break;
                }
                case "won":
                    Day = 3; _escapeRoute = EscapeRoute.Rail; Kills = 412; StealthKills = 57;
                    StartPhase("won");
                    break;
            }
            FramePlayer(0f, true);
            _aimPoint = Me.Pos + Me.Aim * 3f;
        }

        void VerifyAttackFacing()
        {
            foreach (Vector2 aim in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                Me.TickCooldown(2f);
                Me.Aim = -aim;
                _actors.SyncPlayer(Me, true, 0.016f, false);
                if (!Me.Attack(Me.Pos + aim * 2f, this))
                    throw new System.InvalidOperationException("Test attack did not fire");
                Me.Aim = -aim;
                _actors.SyncPlayer(Me, true, 0.016f, false);
                _actors.SyncPlayer(Me, true, 0.07f, false);
                if (Vector3.Dot(_actors.Player.transform.forward, Fx.V3(aim)) < 0.999f)
                    throw new System.InvalidOperationException("Attack body did not follow the committed attack direction");
            }
            Debug.Log("[facing] four directions and cursor reversal verified");
        }

        void VerifyWalkInShops()
        {
            foreach (Vector2 center in new[] { City.StoreCenter, City.ClinicCenter, City.WorkshopCenter })
            {
                var container = City.Containers.First(c => c.room.HasValue && c.room.Value.Contains(center));
                Me.Pos = center + Vector2.down * 4.5f;
                if (ContainerAccessible(container)) throw new System.InvalidOperationException("Indoor shelf is accessible from the street");
                for (int i = 0; i < 140; i++)
                    Me.Pos = Horde.PushOut(Vector2.MoveTowards(Me.Pos, center, 0.06f), 0.4f, City.Walls);
                if (Vector2.Distance(Me.Pos, center) > 0.1f || !ContainerAccessible(container))
                    throw new System.InvalidOperationException("Shop doorway is blocked");
                Me.Pos = Vector2.MoveTowards(container.pos, center, 0.7f);
                Interact(true, true, 0f);
                if (_searching != container) throw new System.InvalidOperationException("Indoor shelf did not accept search");
                if (center == City.WorkshopCenter)
                {
                    FinishSearch(container);
                    if (_lootSource != container || !container.loot.Any(item => item.item == "axe"))
                        throw new System.InvalidOperationException("Workshop weapon cache is missing its axe");
                    ChooseLoot(container.loot.FindIndex(item => item.item == "axe"));
                    if (!Me.Slots.Any(slot => slot.id == "axe"))
                        throw new System.InvalidOperationException("Workshop axe could not be taken");
                    Debug.Log("[loot] workshop axe searched and collected");
                }
                CloseLoot();
                _searching = null;
                for (int i = 0; i < 48; i++)
                    Me.Pos = Horde.PushOut(Me.Pos + Vector2.left * 0.065f, 0.4f, City.Walls);
                if (!container.room.Value.Contains(Me.Pos)) throw new System.InvalidOperationException("Player moved through a shop wall");
            }
            Debug.Log("[interiors] entrances, indoor search and wall collision verified for three shops");
        }

        void VerifySteadyCamera()
        {
            Quaternion rotation = _cam.transform.rotation;
            float zoom = _cam.fieldOfView;
            int renderedSteps = 0;
            foreach (var waypoint in new[] { new Vector2(4f, 0f), new Vector2(14f, 0f), new Vector2(14f, 8f), new Vector2(14f, 0f), new Vector2(4f, 0f), City.Start })
            {
                for (int step = 0; step < 280 && Vector2.Distance(waypoint, Me.Pos) > 0.08f; step++)
                {
                    float dt = step % 2 == 0 ? 1f / 60f : 1f / 40f;
                    var next = Vector2.MoveTowards(Me.Pos, waypoint, Me.Speed * dt);
                    Me.Pos = Horde.PushOut(next, 0.4f, City.Walls);
                    FramePlayer(dt, false);
                    if (Quaternion.Angle(_cam.transform.rotation, rotation) > 0.001f || Mathf.Abs(_cam.fieldOfView - zoom) > 0.001f)
                        throw new System.InvalidOperationException("Camera rotation or zoom changed during movement");
                    renderedSteps++;
                }
                if (Vector2.Distance(waypoint, Me.Pos) > 0.08f)
                    throw new System.InvalidOperationException($"Camera test could not reach {waypoint}");
            }
            Debug.Log($"[camera] steady through doorway and alley, {renderedSteps} tracking steps");
        }

        void VerifyControls()
        {
            Me.Pos = City.StoreCenter + Vector2.down * 4.5f;
            float dt = 0.05f;
            // W through the shop entrance, then hold into each wall and reverse.
            foreach (var input in new[] { Vector2.up, Vector2.right, Vector2.left, Vector2.down, Vector2.one, -Vector2.one })
                for (int i = 0; i < 36; i++)
                {
                    var before = Me.Pos;
                    MoveFromInput(input, dt);
                    if (Vector2.Distance(before, Me.Pos) > Me.Speed * dt + 0.0001f)
                        throw new System.InvalidOperationException("WASD movement exceeded its frame budget");
                    if (input == Vector2.up && i == 18 && !City.Containers.Any(c => c.room.HasValue && c.room.Value.Contains(Me.Pos)))
                        throw new System.InvalidOperationException("WASD cannot enter the convenience store");
                }
            Me.Pos = City.MarketSquare;
            _aimPoint = Me.Pos + Vector2.up * 2f;
            QueueMelee();
            TickMelee(MeleeWindup * 0.5f);
            if (Me.Attacked != null) throw new System.InvalidOperationException("Melee struck before its windup");
            _aimPoint = Me.Pos + Vector2.down * 2f;
            TickMelee(MeleeWindup);
            if (Me.Attacked == null || Vector2.Dot(Me.LastAttackDirection, Vector2.up) < 0.99f || Me.Pos != City.MarketSquare)
                throw new System.InvalidOperationException("Melee changed committed direction or displaced the player");
            Debug.Log("[controls] WASD doorway, walls, diagonals and timed melee verified");
        }

        IEnumerator VerifyKeyboard()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var endOfFrame = new WaitForEndOfFrame();
            float maxError = 0f;
            foreach (var key in new[] { Key.W, Key.S, Key.A, Key.D })
            {
                Me.Pos = City.MarketSquare;
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                yield return endOfFrame;
                var start = Me.Pos;
                var pos = Me.Pos;
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
                for (int frame = 0; frame < 32; frame++)
                {
                    yield return null;
                    yield return endOfFrame;
                    var step = Me.Pos - pos;
                    var visual = _actors.PlayerVisualCenter;
                    float error = Vector2.Distance(Me.Pos, new Vector2(visual.x, visual.z));
                    maxError = Mathf.Max(error, maxError);
                    if (step.magnitude > Me.Speed * Mathf.Min(Time.deltaTime, 0.05f) + 0.003f || error > 0.01f)
                        throw new System.InvalidOperationException($"Held {key}: invalid frame displacement {step.magnitude:F3}/{error:F3} position={Me.Pos} visual={_actors.PlayerVisualCenter} root={_actors.Player.transform.position}");
                    if (frame == 10) HurtPlayer(Me, 5f, Me.Pos + Vector2.right);
                    pos = Me.Pos;
                }
                var axis = key switch { Key.W => Vector2.up, Key.S => Vector2.down, Key.A => Vector2.left, _ => Vector2.right };
                if (Vector2.Dot(pos - start, axis) < 0.5f)
                    throw new System.InvalidOperationException($"Held {key}: keyboard input did not move the player");
                Debug.Log($"[keyboard] {key}: 32 frames, {Vector2.Dot(pos - start, axis):F2}m");
            }
            InputSystem.RemoveDevice(keyboard);
            Debug.Log($"[keyboard] verified all four held keys and damage, maximum visual drift {maxError:F4}m");
            Application.Quit();
        }

        bool _motionPreview;
        float _motionPreviewTime;

        void StepMotionPreview(float dt)
        {
            _motionPreviewTime += dt;
            float t = _motionPreviewTime;
            Me.Crouching = t >= 2.2f && t < 3.5f;
            Me.Sprinting = t >= 4f && t < 4.6f;
            Vector2 direction = t < 0.45f ? Vector2.zero : t < 1.1f ? Vector2.up :
                t < 1.65f ? Vector2.right : t < 2.2f ? Vector2.zero : t < 3.5f ? Vector2.down :
                t < 4f ? Vector2.zero : t < 4.6f ? Vector2.left : Vector2.zero;
            var before = Me.Pos;
            MoveFromInput(direction, dt);
            float distance = Vector2.Distance(Me.Pos, before);
            _moving = distance > 0.0001f;
            if (distance > Me.Speed * dt + 0.0001f || Time.timeScale != 1f)
                throw new System.InvalidOperationException("Held movement jumped or slowed the entire game");
        }

        IEnumerator CaptureMotion(string directory)
        {
            System.IO.Directory.CreateDirectory(directory);
            Time.captureFramerate = 30;
            // Capture at 15 fps while running the ordinary frame and animation updates at 30 fps.
            for (int i = 0; i < 78; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory, $"frame-{i:000}.png"));
                yield return null;
            }
            Debug.Log("[motion] idle, travel, turn, crouch, sprint and stop preview captured");
            Application.Quit();
        }

        IEnumerator Shot()
        {
            float at = _args.TryGetValue("at", out var s) ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : 3f;
            yield return new WaitForSeconds(at);
            yield return new WaitForEndOfFrame();
            Debug.Log($"[pose] max live zombie tilt {_actors.MaxLiveTilt():0} deg, {Horde.Count} zombies; player {_actors.PlayerState}");
            ScreenCapture.CaptureScreenshot(_args["shot"]);
            yield return null;
            yield return null;
            Debug.Log("[shot] saved " + _args["shot"]);
            Application.Quit();
        }
    }
}
