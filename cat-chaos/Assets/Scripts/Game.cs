using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CatChaos
{
    /// <summary>
    /// 냥난리: the owner left for work. Wreck the flat before they get home, then (optionally)
    /// hide in your cushion and look innocent. Builds the whole scene from code.
    /// Screenshot runs: 냥난리.app --scene chaos --shot /abs/out.png --at 3
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public enum State { Intro, Play, Paused, Ending, Result }

        /// <summary>One item on the "today's mischief" list.</summary>
        public sealed class Goal
        {
            public string Name, Unit = "";
            public float Target;
            public Func<float> Value;
            public bool Done;
        }

        /// <summary>What the owner will find.</summary>
        public sealed class Stats
        {
            public int Tipped, Dropped, Broken, Soil, Plants, Vacuums, Cleaned;
            public float Tissue, Scratch, MaxHeight;
            public bool TvDown;
        }

        public static Game I;
        public Tuning T;
        public State S = State.Intro;
        public House House;
        public Cat Cat;
        public CameraRig Rig;
        public Hud Hud;
        public Score Score;
        public readonly Stats Mess = new();
        public readonly List<Goal> Goals = new();
        public float TimeLeft;
        public bool Hidden;
        public int HideBonus;
        public string OwnerLine = "";

        Light _sun;
        float _nextVacuum, _cleanPopT;
        int _cleanAcc;
        readonly List<Vacuum> _vacuums = new();
        bool _warned;
        float _steady, _steadyPopT, _tick;
        Vector3 _steadyAt;
        readonly Dictionary<string, string> _args = new();

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 120;
            T = Tuning.Load();
            Physics.gravity = new Vector3(0, T.gravity, 0);
            Score = new Score(T.comboWindow, T.comboMax);
            TimeLeft = T.roundSeconds;
            _nextVacuum = T.vacuumFirst;
            ReadArgs();
            if (_args.TryGetValue("time", out var tl)) TimeLeft = float.Parse(tl, CultureInfo.InvariantCulture);
            Build();
        }

        void Build()
        {
            Sfx.Init(transform);
            Debris.Clear();
            Prop.Trails.Clear();
            Lighting();
            House = new House(new GameObject("House").transform);
            Cat = Cat.Spawn(House.CatStart, T);
            Cat.transform.rotation = Quaternion.Euler(0, 200, 0);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 62;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.75f, 0.85f, 0.95f);
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.gameObject.AddComponent<AudioListener>();
            Rig = cam.gameObject.AddComponent<CameraRig>();
            Rig.Target = Cat.transform;
            Rig.Yaw = 200;
            Rig.Orbit = true;
            cam.transform.position = Cat.transform.position + new Vector3(0, 1, 2);

            Hud = gameObject.AddComponent<Hud>();
            Goals.AddRange(new[]
            {
                new Goal { Name = "물건 3개 깨뜨리기", Target = 3, Value = () => Mess.Broken },
                new Goal { Name = "화분 엎기", Target = 1, Value = () => Mess.Plants },
                new Goal { Name = "TV 넘어뜨리기", Target = 1, Value = () => Mess.TvDown ? 1 : 0 },
                new Goal { Name = "휴지 5m 풀기", Target = 5, Unit = "m", Value = () => Mess.Tissue },
                new Goal { Name = "가구 5초 긁기", Target = 5, Unit = "초", Value = () => Mess.Scratch },
                new Goal { Name = "냉장고 위 올라가기", Target = 1, Value = () => Mess.MaxHeight > 1.85f ? 1 : 0 },
                new Goal { Name = "로봇청소기 뒤집기", Target = 1, Value = () => Mess.Vacuums },
                new Goal { Name = "물건 25개 쓰러뜨리기", Target = 25, Value = () => Mess.Tipped + Mess.Dropped },
            });
        }

        void Lighting()
        {
            _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.85f;
            SetSun(0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.31f, 0.27f);
            foreach (var p in new[] { new Vector3(-2.6f, 2.3f, -2f), new Vector3(3.5f, 2.3f, -2f), new Vector3(-3f, 2.3f, 3f), new Vector3(1.5f, 2.3f, 3f), new Vector3(4.5f, 2.3f, 3f) })
            {
                var l = new GameObject("room light").AddComponent<Light>();
                l.type = LightType.Point;
                l.transform.position = p;
                l.color = new Color(1f, 0.9f, 0.78f);
                l.intensity = 1.4f;
                l.range = 6f;
                l.shadows = LightShadows.None;
            }
            var vol = new GameObject("Post").AddComponent<Volume>();
            vol.isGlobal = true;
            var prof = vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = prof.Add<Bloom>(true);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(1.0f);
            var tone = prof.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = prof.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.postExposure.Override(0.15f);
            var vig = prof.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.5f);
        }

        /// <summary>Afternoon sun at 0, low orange evening sun at 1 (owner on the way home).</summary>
        void SetSun(float t)
        {
            float elev = Mathf.Lerp(30f, 12f, t);
            _sun.transform.rotation = Quaternion.Euler(elev, Mathf.Lerp(30f, 55f, t), 0);
            _sun.color = Color.Lerp(new Color(1f, 0.95f, 0.86f), new Color(1f, 0.6f, 0.35f), t);
            _sun.intensity = Mathf.Lerp(2.2f, 1.7f, t);
        }

        void Start()
        {
            if (_args.TryGetValue("scene", out var scene)) StartCoroutine(ShotScene(scene));
            if (_args.ContainsKey("shot")) StartCoroutine(Shot());
            if (_args.TryGetValue("frames", out var directory)) StartCoroutine(CaptureFrames(directory));
        }

        // ================================================================== flow
        public void StartPlay()
        {
            foreach (var p in House.Props) if (p) p.Arm();
            S = State.Play;
            Rig.Orbit = false;
            Rig.Yaw = Cat.transform.eulerAngles.y;
            Cat.Controllable = true;
            Lock(true);
            Sfx.Meow(Cat.transform.position);
            Hud.Toast("주인이 출근했다! 이제 이 집은 내 거야", 3.5f);
        }

        void Update()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            switch (S)
            {
                case State.Intro:
                    if (m != null && m.leftButton.wasPressedThisFrame && !_args.ContainsKey("shot")) StartPlay();
                    break;
                case State.Play:
                    if (k != null && k.escapeKey.wasPressedThisFrame) { S = State.Paused; Time.timeScale = 0; Lock(false); Cat.Controllable = false; break; }
                    if (m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !_args.ContainsKey("shot")) Lock(true);
                    Tick();
                    break;
                case State.Paused:
                    if (m != null && m.leftButton.wasPressedThisFrame) { S = State.Play; Time.timeScale = 1; Lock(true); Cat.Controllable = true; }
                    break;
                case State.Result:
                    if (k != null && k.rKey.wasPressedThisFrame) { Time.timeScale = 1; SceneManager.LoadScene(0); }
                    break;
            }
        }

        void Tick()
        {
            float dt = Time.deltaTime;
            TimeLeft -= dt;
            SetSun(1f - Mathf.Clamp01(TimeLeft / T.roundSeconds));
            Mess.MaxHeight = Mathf.Max(Mess.MaxHeight, Cat.Grounded ? Cat.transform.position.y : 0);
            if (!_warned && TimeLeft <= T.ownerWarnSeconds)
            {
                _warned = true;
                Sfx.Ding();
                Hud.Toast("[문자] 주인: 이제 퇴근~ 금방 갈게!", 4f);
            }
            if (T.roundSeconds - TimeLeft >= _nextVacuum && TimeLeft > 20f)
            {
                _nextVacuum += T.vacuumEvery;
                _vacuums.RemoveAll(v => !v || v.Flipped);
                if (_vacuums.Count < T.vacuumMax) SpawnVacuum(House.Dock, House.DockYaw);
            }
            if (_cleanAcc > 0 && Time.time > _cleanPopT)
            {
                Hud.Pop($"-{_cleanAcc} 청소됨", _cleanAt + Vector3.up * 0.3f, new Color(0.45f, 0.85f, 1f), 22);
                _cleanAcc = 0;
                _cleanPopT = Time.time + 0.7f;
            }
            if (TimeLeft <= 10 && (_tick -= dt) <= 0) { _tick = 1f; Sfx.Tick(); }

            if (_steady > 0 && Time.time > _steadyPopT)
            {
                int gained = Score.Add(Mathf.RoundToInt(_steady), Time.time, chain: false);
                if (gained > 0) Hud.Pop("+" + gained, _steadyAt, new Color(0.8f, 0.95f, 1f), 20);
                _steady = 0;
                _steadyPopT = Time.time + 0.6f;
            }

            foreach (var g in Goals)
                if (!g.Done && g.Value() >= g.Target)
                {
                    g.Done = true;
                    int gained = Score.Bonus(T.ptsGoal);
                    Sfx.Ding();
                    Hud.Toast($"말썽 완료!  {g.Name}  +{gained}", 3f);
                }

            if (TimeLeft <= 0) StartCoroutine(OwnerComesHome());
        }

        IEnumerator OwnerComesHome()
        {
            S = State.Ending;
            TimeLeft = 0;
            Cat.Controllable = false;
            Cat.AutoMove = Vector3.zero;
            Sfx.Scratching(false);
            var flat = new Vector2(Cat.transform.position.x - House.CatBed.x, Cat.transform.position.z - House.CatBed.z);
            Hidden = flat.magnitude < T.bedRadius && Cat.transform.position.y < 0.3f;
            Sfx.Keys();
            yield return new WaitForSeconds(1.2f);
            Sfx.Door();
            Rig.ShotPos = House.DoorEye;
            Rig.ShotLook = House.DoorLook;
            for (float t = 0; t < 1; t += Time.deltaTime / 1.1f)
            {
                House.Door.localRotation = Quaternion.Euler(0, Mathf.SmoothStep(0, -100, t), 0);
                yield return null;
            }
            string grade = Score.Grade(Score.Total, T.grades);
            OwnerLine = grade switch
            {
                "S" => "…여기 우리 집 맞아?",
                "A" => "야!!! 누가 이랬어!!!",
                "B" => "아이고… 또 사고 쳤구나",
                "C" => "음? 뭔가 좀 어수선한데…",
                _ => "착하게 있었네? …수상한데."
            };
            if (Hidden)
            {
                int before = Score.Total;
                Score.Multiply(T.hideBonus);
                HideBonus = Score.Total - before;
            }
            yield return new WaitForSeconds(2.6f);
            S = State.Result;
            Lock(false);
            Sfx.Pop(0.8f);
        }

        static void Lock(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // ================================================================== events from props and the cat
        void Award(int pts, string label, Vector3 at, Color c, int size = 24)
        {
            int gained = Score.Add(pts, Time.time);
            if (_args.ContainsKey("shot")) Debug.Log($"[mess] {label} {at} t={T.roundSeconds - TimeLeft:0.0}");
            Hud.Pop($"+{gained} {label}", at + Vector3.up * 0.25f, c, size);
            if (Score.Combo > 1) { Hud.ComboPulse(); Sfx.Pop(0.9f + Score.Combo * 0.12f); }
        }

        public Vacuum SpawnVacuum(Vector3 at, float yaw)
        {
            var v = Vacuum.Spawn(at, yaw, T);
            v.GetComponent<Prop>().Arm();
            _vacuums.Add(v);
            Sfx.Beep(at);
            Hud.Toast(Mess.Vacuums == 0 && _vacuums.Count == 1 ? "로봇청소기 출동! 어지른 걸 치운다… 냥펀치 3방이면 뒤집힘" : "로봇청소기가 또 나왔다!", 4f);
            return v;
        }

        Vector3 _cleanAt;

        /// <summary>The vacuum ate a piece of the mess.</summary>
        public void OnClean(Vector3 at)
        {
            Mess.Cleaned++;
            _cleanAcc += Score.Penalty(T.cleanPenalty);
            _cleanAt = at;
        }

        public void OnTip(Prop p)
        {
            if (p.TryGetComponent<Vacuum>(out _))
            {
                Mess.Vacuums++;
                Sfx.Ding();
                Award(T.ptsVacuumFlip, "로봇청소기 뒤집기!", p.transform.position, new Color(0.5f, 0.95f, 1f), 36);
                return;
            }
            Mess.Tipped++;
            if (p.Kind == PropKind.Tv) { Mess.TvDown = true; Award(T.ptsTvDrop, "TV 쾅!", p.transform.position, new Color(1f, 0.45f, 0.4f), 34); return; }
            if (p.Kind == PropKind.Plant) Mess.Plants++;
            Award(p.Big ? T.ptsBigTip : T.ptsTip, p.Label + " 쓰러뜨림", p.transform.position, p.Big ? new Color(1f, 0.75f, 0.35f) : Color.white, p.Big ? 30 : 24);
        }

        public void OnDrop(Prop p)
        {
            Mess.Dropped++;
            if (p.Kind == PropKind.Tv && !Mess.TvDown) { Mess.TvDown = true; Award(T.ptsTvDrop, "TV 추락!", p.transform.position, new Color(1f, 0.45f, 0.4f), 34); return; }
            Award(T.ptsDrop, p.Label + " 떨어뜨림", p.transform.position, new Color(1f, 0.95f, 0.6f));
        }

        public void OnBreak(Prop p)
        {
            Mess.Broken++;
            Award(T.ptsBreak, p.Label + " 와장창!", p.transform.position, new Color(1f, 0.55f, 0.6f), 30);
        }

        public void OnSoil(Prop p)
        {
            Mess.Soil++;
            Award(T.ptsSoil, "흙 쏟음", p.transform.position, new Color(0.85f, 0.65f, 0.45f));
        }

        public void OnUnroll(Prop p, float meters)
        {
            Mess.Tissue += meters;
            _steady += meters * T.ptsTissuePerMeter;
            _steadyAt = p.transform.position;
        }

        public void OnScratch(Scratchable s, float dt, Vector3 at)
        {
            Mess.Scratch += dt;
            _steady += dt * T.ptsScratchPerSecond;
            _steadyAt = at;
        }

        // ================================================================== screenshot runs
        void ReadArgs()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i].StartsWith("--")) _args[a[i].Substring(2)] = a[i + 1];
        }

        IEnumerator ShotScene(string name)
        {
            yield return new WaitForSeconds(0.6f);   // let props settle
            if (name == "intro") yield break;
            StartPlay();
            Lock(false);
            Cat.Controllable = false;
            switch (name)
            {
                case "play":
                    Rig.Pitch = 18;
                    if (_args.ContainsKey("frames"))
                    {
                        yield return new WaitForSeconds(0.8f);
                        Cat.Meow();
                    }
                    break;
                case "chaos":
                    Cat.Place(new Vector3(-2.6f, 0.5f, -1.75f));
                    Rig.Yaw = 160;
                    Rig.Pitch = 28;
                    Rig.Distance = 2.6f;
                    Cat.AutoPaw = true;
                    yield return new WaitForSeconds(0.2f);
                    int i = 0;
                    foreach (var p in House.Props)
                        if (p && Vector3.Distance(p.transform.position, Cat.transform.position) < 3.2f && i++ % 2 == 0)
                            p.Body.AddForceAtPosition((p.transform.position - Cat.transform.position).normalized * 2.5f * p.Body.mass + Vector3.up * p.Body.mass, p.transform.position + Vector3.up * 0.3f, ForceMode.Impulse);
                    break;
                case "kitchen":
                    Cat.Place(new Vector3(3.2f, 0.98f, -4.62f));
                    Cat.Body.rotation = Quaternion.Euler(0, 90, 0);
                    Rig.Yaw = 200;
                    Rig.Pitch = 22;
                    Rig.Distance = 1.9f;
                    if (_args.ContainsKey("frames"))
                    {
                        yield return new WaitForSeconds(0.7f);
                        Cat.Jump();
                        yield return new WaitForSeconds(0.2f);
                        Cat.Jump();
                    }
                    break;
                case "bath":
                    Cat.Place(new Vector3(1.8f, 0.05f, 3.6f));
                    Cat.AutoMove = new Vector3(0.3f, 0, 1f).normalized;
                    Cat.AutoPaw = true;
                    Rig.Yaw = 10;
                    Rig.Pitch = 30;
                    Rig.Distance = 1.8f;
                    break;
                case "robot":
                    Cat.Place(new Vector3(-1.5f, 0.05f, -2.4f));
                    Cat.Body.rotation = Quaternion.Euler(0, 300, 0);
                    Rig.Yaw = 310;
                    Rig.Pitch = 32;
                    Rig.Distance = 1.7f;
                    foreach (var p in House.Props)
                        if (p && p.Kind == PropKind.Fragile && Vector3.Distance(p.transform.position, new Vector3(-2.6f, 0, -1.8f)) < 1.5f) p.Shatter();
                    yield return new WaitForSeconds(1.0f);
                    SpawnVacuum(new Vector3(-1.9f, 0, -0.9f), 180);
                    break;
                case "sprint":
                    Cat.Place(new Vector3(-1.1f, 0.05f, -3.3f));
                    Rig.Yaw = 90;
                    Rig.Pitch = 32;
                    Cat.AutoMove = Vector3.right;
                    yield return new WaitForSeconds(0.65f);
                    Cat.AutoMove = Vector3.zero;
                    Cat.Jump();
                    yield return new WaitForSeconds(0.23f);
                    Cat.Jump();
                    break;
                case "scratch":
                    Cat.Place(new Vector3(-2.9f, 0.05f, -0.37f));
                    Cat.Body.rotation = Quaternion.identity;
                    Rig.Yaw = 25;
                    Rig.Pitch = 33;
                    Cat.AutoScratch = true;
                    break;
                case "plant":
                case "tv":
                    var kind = name == "plant" ? PropKind.Plant : PropKind.Tv;
                    var prop = House.Props.Find(p => p && p.Kind == kind);
                    var forward = Vector3.right;
                    Cat.Place(prop.Body.position - forward * 0.46f);
                    Cat.Body.rotation = Quaternion.LookRotation(forward);
                    Rig.Yaw = 44;
                    Rig.Pitch = 40;
                    yield return new WaitForSeconds(0.65f);
                    Cat.Paw();
                    // Keep a heavy prop falling into view in the staged replay.
                    prop.Body.AddForceAtPosition(
                        (forward * 2.5f + Vector3.up * 0.6f) * prop.Body.mass,
                        prop.Body.worldCenterOfMass + Vector3.up * 0.35f, ForceMode.Impulse);
                    break;
                case "robot-flip":
                    Cat.Place(new Vector3(-1.4f, 0.05f, -2.4f));
                    Rig.Yaw = 145;
                    Rig.Pitch = 38;
                    var vacuum = SpawnVacuum(Cat.transform.position + Vector3.back * 0.5f, 0);
                    for (int hit = 0; hit < T.vacuumHits; hit++)
                    {
                        yield return new WaitForSeconds(0.55f);
                        var toward = Vector3.ProjectOnPlane(vacuum.Body.position - Cat.Body.position, Vector3.up).normalized;
                        Cat.Place(vacuum.Body.position - toward * 0.42f);
                        Cat.Body.rotation = Quaternion.LookRotation(toward);
                        yield return null;
                        Cat.Paw();
                    }
                    break;
                case "hide":
                    Cat.Place(House.CatBed);
                    Rig.Yaw = 150;
                    Rig.Pitch = 36;
                    var fragile = House.Props.Find(p => p && p.Kind == PropKind.Fragile);
                    fragile.Shatter();
                    Cat.Meow();
                    TimeLeft = 2f;
                    break;
                case "ending":
                case "result":
                    Cat.AutoPaw = true;
                    Cat.Place(new Vector3(-2.6f, 0.5f, -1.75f));
                    foreach (var p in House.Props)
                        if (p) p.Body.AddForce(UnityEngine.Random.insideUnitSphere * 3f * p.Body.mass + Vector3.up * p.Body.mass * 2, ForceMode.Impulse);
                    yield return new WaitForSeconds(1.5f);
                    TimeLeft = 0.01f;
                    break;
            }
        }

        IEnumerator CaptureFrames(string directory)
        {
            // Fixed simulation steps keep the GIF duration independent of GPU and PNG speed.
            const int fps = 15;
            Time.captureFramerate = fps;
            float start = _args.TryGetValue("at", out var at) ? float.Parse(at, CultureInfo.InvariantCulture) : 0.8f;
            float seconds = _args.TryGetValue("seconds", out var duration) ? float.Parse(duration, CultureInfo.InvariantCulture) : 4f;
            System.IO.Directory.CreateDirectory(directory);
            yield return new WaitForSeconds(start);
            var endOfFrame = new WaitForEndOfFrame();
            for (int frame = 0; frame < Mathf.RoundToInt(seconds * fps); frame++)
            {
                yield return endOfFrame;
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, $"{frame:D4}.png"), texture.EncodeToPNG());
                Destroy(texture);
                yield return null;
            }
            Debug.Log($"[frames] state={S} score={Score.Total} plants={Mess.Plants} scratch={Mess.Scratch:0.0} tissue={Mess.Tissue:0.0} tv={Mess.TvDown} cleaned={Mess.Cleaned} vacuums={Mess.Vacuums} hidden={Hidden} bonus={HideBonus}");
            Application.Quit();
        }

        IEnumerator Shot()
        {
            float at = _args.TryGetValue("at", out var s) ? float.Parse(s, CultureInfo.InvariantCulture) : 3f;
            yield return new WaitForSeconds(at);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(_args["shot"]);
            yield return null;
            yield return null;
            Debug.Log($"[shot] saved {_args["shot"]} score {Score.Total} cleaned {Mess.Cleaned} props {House.Props.Count} state {S}");
            Application.Quit();
        }
    }
}
