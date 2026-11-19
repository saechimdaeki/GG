using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ReelDungeon.Core;
using UnityEngine;

/// <summary>
/// Prototype screen: draws the arena, the party, the map, shop and reels, plus all effects, and forwards
/// clicks to <see cref="Run"/>. Boots itself, no scene setup. Laid out on a 1280x720 canvas scaled to the window.
/// </summary>
public class GameView : MonoBehaviour
{
    // ponytail: IMGUI draws sprites fine for a prototype; move to SpriteRenderer + UI Toolkit when art direction settles.
    const float W = 1280, H = 720, Px = 5;            // Px: screen pixels per art pixel in the arena
    const float FloorY = 318, FrontX = 470, MemberGap = 92, EnemyX = 960, StrikeX = 820;
    const float ReelY = 376, ReelSize = 126, ReelGap = 30, Tile = 48;

    static readonly Color Poison = new Color(0.55f, 0.95f, 0.35f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.3f), HitRed = new Color(1f, 0.35f, 0.3f),
        HealGreen = new Color(0.45f, 1f, 0.5f), BlockBlue = new Color(0.5f, 0.75f, 1f), Ice = new Color(0.6f, 0.9f, 1f),
        Panel = new Color(0.07f, 0.06f, 0.09f);

    TuningConfig cfg;
    Run run;
    GUIStyle big, mid, small, tiny, button, smallButton, shopButton, turnButton, pop, story_, panel;
    Texture2D glow, vignette;
    readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
    readonly Dictionary<string, int> frameCounts = new Dictionary<string, int>();

    // What is on screen. It lags behind `run` while a turn plays out.
    SymbolDef[] shownFaces;
    EnemyDef shownEnemy;
    int shownHp, shownEnemyHp, shownGold;
    bool busy, heroDown;
    float[] spinUntil = new float[0], joinedAt = new float[0];
    Vector2[] offset = new Vector2[0];                // per-member motion offset (lunge, hop, leap)
    readonly HashSet<int> auraRed = new HashSet<int>(), auraBlue = new HashSet<int>();
    Vector2 enemyOffset;
    float heroHitAt = -9, enemyHitAt = -9, shakeAt = -9, shakeAmount = 8, enemyDeathAt = -9, enemyEnterAt = -9, frozenUntil = -9;
    float flashAt = -9;
    Color flashColor;

    // story: lines waiting to be shown, and what has been told already this run
    StoryData story;
    readonly Queue<Say> talk = new Queue<Say>();
    float lineStart;
    int toldChapter, toldParty;
    readonly HashSet<int> toldBoss = new HashSet<int>();
    bool toldEnd;

    bool onTitle = true;
    int ascensionChoice;
    // meta progression: finished runs open companions and relics in tuning "unlocks" order (PlayerPrefs "unlocks").
    // Players who already cleared once (ascension > 0) start with everything open.
    static int UnlocksOwned => HasArg("--unlockall") ? 99 : HasArg("--unlocks") ? IntArg("--unlocks") : PlayerPrefs.GetInt("unlocks", PlayerPrefs.GetInt("ascension", 0) > 0 ? 99 : 0);
    List<string> newUnlocks = new List<string>();
    void ApplyLocks() { run.Locked.Clear(); run.Locked.UnionWith(Run.LockedAfter(cfg, UnlocksOwned)); }
    string UnlockName(string id) => cfg.companions.FirstOrDefault(c => c.id == id)?.name ?? cfg.items.FirstOrDefault(i => i.id == id)?.name ?? id;
    static int AscensionUnlocked => HasArg("--unlockall") ? 5 : PlayerPrefs.GetInt("ascension", 0);

    // pause menu, settings (saved in PlayerPrefs) and screen fades
    bool paused, showSettings;
    float musicVol, sfxVol, fadeAt = -9;
    bool shakeOn;
    Phase shownPhase;

    // first-run guide: one bubble at a time, each shown once (PlayerPrefs "tips")
    readonly HashSet<string> seenTips = new HashSet<string>();
    string tipKey, tipText;
    Rect tipTarget;

    struct Say { public string name, sprite, text; }

    // sound
    AudioSource sfx, music;
    bool[] stopped = new bool[0];
    float nextTick;

    readonly List<Popup> popups = new List<Popup>();
    readonly List<Particle> particles = new List<Particle>();
    readonly List<Shot> shots = new List<Shot>();
    readonly List<Fx> fxs = new List<Fx>();

    struct Popup { public string text; public Color color; public Vector2 pos; public float at; public int size; }
    struct Particle { public Vector2 pos, vel; public float born, life, size, gravity; public Color color; }
    struct Shot { public Texture2D tex; public Vector2 from, to; public float start, dur, arc, spin, scale, angle; public Color color; public bool glowing; }
    /// <summary>A one-off shape effect: ring, slash, beam, claw or ice.</summary>
    struct Fx { public string kind; public Vector2 pos; public float start, dur, size; public Color color; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => new GameObject(nameof(GameView)).AddComponent<GameView>();

    void Awake()
    {
        cfg = JsonUtility.FromJson<TuningConfig>(Resources.Load<TextAsset>("tuning").text);
        story = JsonUtility.FromJson<StoryData>(Resources.Load<TextAsset>("story").text);
        sfx = gameObject.AddComponent<AudioSource>();
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        musicVol = PlayerPrefs.GetFloat("musicVol", 0.8f);
        sfxVol = PlayerPrefs.GetFloat("sfxVol", 1f);
        shakeOn = PlayerPrefs.GetInt("shake", 1) == 1;
        foreach (var k in PlayerPrefs.GetString("tips", "").Split(',')) if (k != "") seenTips.Add(k);
        if (HasArg("--notips")) seenTips.UnionWith(new[] { "map", "reels", "reroll", "intent", "confirm", "skill", "reward", "shop", "event" });
        music.volume = 0.3f * musicVol;
        if (Camera.main == null || Camera.main.GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        NewRun();
        if (Camera.main)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Panel;
        }
        // Screenshot helpers: --recruit a,b adds members, --node <Type> enters a row-0 node of that type,
        // --charge fills every gauge, --autoturns N plays N turns instantly.
        foreach (var id in StrArg("--recruit")?.Split(',') ?? new string[0])
            if (cfg.companions.Any(c => c.id == id) && !run.Party.Contains(id)) run.Recruit(id);
        if (IntArg("--chapter") > 0) run.JumpToChapter(IntArg("--chapter") - 1);
        if (StrArg("--event") is string eventId) cfg.events = cfg.events.Where(e => e.id == eventId).ToArray(); // screenshots of one event
        if (StrArg("--enemy") is string enemyName) // screenshots of one enemy: --enemy 도둑 고블린 --node Combat
            foreach (var c in cfg.chapters)
            {
                if (c.enemies.FirstOrDefault(e => e.name == enemyName) is EnemyDef pe) run.ChapterDef.enemies = new[] { pe };
                if (c.elites.FirstOrDefault(e => e.name == enemyName) is EnemyDef pl) run.ChapterDef.elites = new[] { pl };
            }
        if (StrArg("--node") is string nodeType && System.Enum.TryParse(nodeType, out NodeType t))
        {
            var node = run.Map.Rows[0][1];
            node.Type = t;
            run.Choose(node);
        }
        if (IntArg("--pick") > 0 && run.Phase == Phase.Event) // screenshots of an event result: --pick N
        {
            var ev = run.CurrentEvent;
            run.ChooseEvent(IntArg("--pick") - 1);
            if (run.Phase != Phase.Turn) ShowEventResult(ev.title, ev.options[IntArg("--pick") - 1].label.Split(new[] { " (" }, System.StringSplitOptions.None)[0]);
        }
        if (HasArg("--charge")) for (int i = 0; i < run.Gauge.Count; i++) run.Gauge[i] = run.SkillCharge;
        foreach (var id in StrArg("--equip")?.Split(',') ?? new string[0])
            for (int i = 0; i < run.Party.Count; i++)
                if (run.Equipment[i] == null && cfg.items.Any(it => it.id == id)) { run.Equipment[i] = id; break; }
        foreach (var id in StrArg("--relic")?.Split(',') ?? new string[0])
            if (cfg.items.Any(it => it.id == id)) run.Relics.Add(id);
        int n = IntArg("--autoturns");
        for (int k = 0; k < n && run.Phase == Phase.Turn; k++) run.Commit();
        if (HasArg("--screenshot") && HasArg("--skip-reward") && run.Phase == Phase.Reward) run.ChooseReward(-1);
        Sync();
        SpinAll();
        if (HasArg("--nostory")) talk.Clear();
        if (System.Environment.GetCommandLineArgs().Any(a => a.StartsWith("--") && a != "--screenshot" && a != "--at" && a != "--after")) onTitle = false;
        if (HasArg("--title")) onTitle = true;
        if (HasArg("--codex")) { onTitle = true; showCodex = true; codexTab = IntArg("--codextab"); if (IntArg("--codexpick") > 0) codexPick = codexTab == 0 ? cfg.companions[IntArg("--codexpick") - 1] : codexTab == 1 ? (object)cfg.items.OrderBy(i => i.slot == "relic" ? 0 : 1).ElementAt(IntArg("--codexpick") - 1) : cfg.chapters.SelectMany(c => c.enemies.Concat(c.elites).Append(c.boss)).ElementAt(IntArg("--codexpick") - 1); } // screenshots of the codex
        if (HasArg("--pause")) { paused = true; Time.timeScale = 0; }
        if (HasArg("--settings")) { paused = true; showSettings = true; Time.timeScale = 0; }
        for (int k = IntArg("--skiplines"); k > 0 && talk.Count > 0; k--) talk.Dequeue(); // screenshots of later lines
        if (HasArg("--continue") && HasSave && ContinueRun()) onTitle = false;
        SaveRun();
    }

    IEnumerator Start()
    {
        // --autocommit: arm every ready skill and press 확정 once the reels stop (screenshots of the effects).
        if (!HasArg("--autocommit")) yield break;
        while (AnySpinning()) yield return null;
        for (int i = 0; i < run.Party.Count; i++) if (run.SkillReady(i)) run.ToggleSkill(i);
        yield return new WaitForSeconds(0.3f);
        StartCoroutine(PlayTurn());
    }

    static bool HasArg(string name) => System.Environment.GetCommandLineArgs().Contains(name);

    static string StrArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static int IntArg(string name) => int.TryParse(StrArg(name), out var v) ? v : 0;

    // ---------- state ----------

    // ---------- save / continue ----------

    static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "run.json");
    // screenshot runs must never touch the player's save
    static bool SavingEnabled => !HasArg("--screenshot") || HasArg("--usesave");
    static bool HasSave => SavingEnabled && System.IO.File.Exists(SavePath);
    string saveError;

    void SaveRun()
    {
        if (!SavingEnabled || onTitle) return;
        try
        {
            if (run.CanSave) System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(run.ToSave()));
            else if (System.IO.File.Exists(SavePath)) System.IO.File.Delete(SavePath); // the run is over
        }
        catch (System.Exception e) { Debug.LogWarning($"save failed: {e.Message}"); }
    }

    void OnApplicationQuit() => SaveRun();

    /// <summary>Resume the saved run. A save from an older, incompatible build is set aside, not crashed on.</summary>
    bool ContinueRun()
    {
        try
        {
            var loaded = Run.FromSave(cfg, JsonUtility.FromJson<RunSave>(System.IO.File.ReadAllText(SavePath)));
            NewRun();
            run = loaded;
            ApplyLocks();
            talk.Clear();
            toldChapter = run.Chapter;
            toldParty = run.Party.Count;
            if (run.Current?.Type == NodeType.Boss) toldBoss.Add(run.Chapter);
            shownEnemy = null;
            spinUntil = new float[0];
            Sync();
            if (run.Phase == Phase.Turn) SpinAll();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"could not load save: {e}");
            System.IO.File.Move(SavePath, SavePath + ".broken");
            saveError = "저장된 판을 불러오지 못했어요. 새 모험을 시작해 주세요.";
            return false;
        }
    }

    void NewRun()
    {
        run = new Run(cfg, new System.Random(), ascensionChoice);
        eventResult = null;
        ApplyLocks();
        newUnlocks.Clear();
        spinUntil = new float[0];
        joinedAt = new float[0];
        offset = new Vector2[0];
        enemyDeathAt = -9;
        talk.Clear();
        toldBoss.Clear();
        toldEnd = false;
        toldChapter = -1;
        toldParty = run.Party.Count;
        Tell(story.prologue);
        Sync();
        SpinAll();
    }

    /// <summary>Queue the story lines the latest state change calls for.</summary>
    void StoryCheck()
    {
        var chapter = run.Chapter < story.chapters.Length ? story.chapters[run.Chapter] : null;
        if (run.Chapter != toldChapter)
        {
            toldChapter = run.Chapter;
            Tell(chapter?.intro);
        }
        for (; toldParty < run.Party.Count; toldParty++)
        {
            var line = story.recruits.FirstOrDefault(r => r.id == run.Party[toldParty]);
            if (line != null) Tell(new[] { new Line { speaker = line.id, text = line.text } });
        }
        if (chapter != null && run.Phase == Phase.Turn && run.Current?.Type == NodeType.Boss && toldBoss.Add(run.Chapter))
        {
            Tell(chapter.boss);
            var response = chapter.bossResponses?.FirstOrDefault(line => run.Party.Contains(line.speaker));
            if (response != null) Tell(new[] { response });
        }
        if (!toldEnd && (run.Phase == Phase.Won || run.Phase == Phase.Lost))
        {
            toldEnd = true;
            if (run.Phase == Phase.Won && run.Ascension >= AscensionUnlocked && run.Ascension < 5)
            {
                PlayerPrefs.SetInt("ascension", run.Ascension + 1); // beating a level opens the next
                PlayerPrefs.Save();
            }
            int owned = UnlocksOwned;
            newUnlocks = Run.LockedAfter(cfg, owned).Take(run.UnlocksEarned).ToList();
            if (newUnlocks.Count > 0 && SavingEnabled)
            {
                PlayerPrefs.SetInt("unlocks", owned + newUnlocks.Count);
                PlayerPrefs.Save();
            }
            Sfx(run.Phase == Phase.Won ? "win" : "lose");
            Tell(run.Phase == Phase.Won ? story.ending : chapter?.defeat ?? story.defeat);
        }
    }

    void Tell(Line[] lines)
    {
        foreach (var l in lines ?? new Line[0])
        {
            var who = l.speaker;
            var c = cfg.companions.FirstOrDefault(x => x.id == who);
            var say = who == "narrator" ? new Say { name = "", sprite = null, text = l.text }
                : who == "boss" ? new Say { name = run.Enemy?.name ?? "???", sprite = run.Enemy?.sprite, text = l.text }
                : new Say { name = c?.name ?? who, sprite = c != null ? c.sprite + "_idle_anim" : null, text = l.text };
            if (talk.Count == 0) lineStart = Time.time;
            talk.Enqueue(say);
        }
    }

    void Sync()
    {
        if (run.Phase != shownPhase && !(shownPhase == Phase.Turn && busy))
        {
            shownPhase = run.Phase;
            fadeAt = Time.unscaledTime; // a short fade in when the screen changes (map, fight, shop…)
        }
        shownFaces = run.Faces.ToArray();
        shownHp = run.Hp;
        shownGold = run.Gold;
        heroDown = run.Phase == Phase.Lost;
        if (run.Phase == Phase.Turn && shownEnemy != run.Enemy) { enemyEnterAt = Time.time; enemyDeathAt = -9; }
        shownEnemy = run.Enemy;
        shownEnemyHp = run.EnemyHp;
        if (run.Enemy != null) MarkSeen(run.Enemy.name);
        int count = run.Party.Count;
        if (spinUntil.Length != count)
        {
            int old = joinedAt.Length;
            System.Array.Resize(ref spinUntil, count);
            System.Array.Resize(ref offset, count);
            System.Array.Resize(ref joinedAt, count);
            for (int i = old; i < count; i++) joinedAt[i] = old == 0 ? -9 : Time.time; // newcomers walk in
        }
        if (story != null) StoryCheck();
        if (!busy) SaveRun();
    }

    void SpinAll()
    {
        for (int i = 0; i < spinUntil.Length; i++) spinUntil[i] = Time.time + 0.45f + 0.15f * i;
    }

    void Update()
    {
        if (!paused) SpawnAmbient();
        if (stopped.Length != spinUntil.Length) stopped = new bool[spinUntil.Length];
        bool any = false;
        for (int i = 0; i < spinUntil.Length; i++)
        {
            if (Spinning(i)) { any = true; stopped[i] = false; }
            else if (!stopped[i]) { stopped[i] = true; Sfx("reel_stop", 0.7f, 0.9f + 0.05f * i); }
        }
        if (any && Time.time > nextTick) { Sfx("reel_tick", 0.35f, Random.Range(0.9f, 1.1f)); nextTick = Time.time + 0.07f; }

        var track = (busy || run.Phase == Phase.Turn) && run.Current?.Type == NodeType.Boss ? "bgm_boss" : "bgm_dungeon";
        if (music.clip == null || music.clip.name != track)
        {
            music.clip = Resources.Load<AudioClip>("Audio/" + track);
            music.Play();
        }
    }

    void Sfx(string name, float volume = 1, float pitch = 1)
    {
        var clip = Resources.Load<AudioClip>("Audio/" + name); // Resources caches loaded assets
        if (clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume * sfxVol);
    }

    bool Spinning(int i) => Time.time < spinUntil[i];
    bool AnySpinning() => spinUntil.Any(t => Time.time < t);

    // ---------- the turn, played out ----------

    IEnumerator PlayTurn()
    {
        busy = true;
        var o = run.Preview();
        var armed = run.Armed.ToList();
        int hpBefore = run.Hp, enemyBefore = run.EnemyHp, goldBefore = run.Gold, attack = run.EnemyAttack(o);
        int poisonTick = run.EnemyPoison + o.poison;
        int guard = run.EnemyGuard, dealt = run.DamageDealt(o);
        var move = run.Intent;
        run.Commit();
        int healed = Mathf.Min(run.MaxHp, hpBefore + o.heal);

        // 1. skills, one after another
        for (int k = 0; k < armed.Count; k++)
        {
            int i = armed[k];
            Pop(run.Member(i).skillName + "!", Gold, new Vector2(FrontX - 120, 120 + k * 40), 36);
            Sfx("skill");
            yield return SkillFx(i);
        }

        // 2. heal and gold
        if (o.heal > 0)
        {
            shownHp = healed;
            Pop($"+{o.heal}", HealGreen, MemberHead(0) + new Vector2(-70, 0), 40);
            for (int i = 0; i < run.Party.Count; i++) Burst(MemberCenter(i), HealGreen, 6, 60, -80, 0.8f, 6);
        }
        if (o.gold > 0)
        {
            shownGold = goldBefore + o.gold;
            Pop($"+{o.gold} 골드", Gold, new Vector2(W - 150, 60), 30);
            Sfx("coin");
        }
        if (o.combo != "")
        {
            Pop(o.combo, o.badCombo ? HitRed : Gold, new Vector2(W / 2, 150), 40);
            Sfx(o.badCombo ? "hurt" : "combo", 0.8f);
        }

        // 3. every member whose face hits (or whose skill fired) attacks in its own style
        if (o.damage > 0)
        {
            var attackers = Enumerable.Range(0, shownFaces.Length)
                .Where(i => shownFaces[i].damage > 0 || armed.Contains(i)).ToList();
            if (attackers.Count == 0) attackers.Add(0);
            int left = dealt;
            if (guard > 0)
            {
                Pop($"방어 {Mathf.Min(guard, o.damage)}", BlockBlue, EnemyBody() + new Vector2(-40, -90), 34);
                AddFx("ring", EnemyBody(), 0.5f, 220, BlockBlue);
            }
            for (int k = 0; k < attackers.Count; k++)
            {
                int share = k == attackers.Count - 1 ? left : dealt / attackers.Count;
                left -= share;
                bool last = k == attackers.Count - 1;
                var co = StartCoroutine(Attack(attackers[k], share, enemyBefore, dealt, last));
                if (last) yield return co;
                else yield return new WaitForSeconds(0.14f);
            }
            if (o.combo != "" || armed.Count > 0)
            {
                Pop($"-{dealt}", Gold, EnemyBody() + new Vector2(0, -50), 80);
                Shake(14);
            }
        }

        // poison bites after the hits (if the enemy is still standing)
        if (poisonTick > 0 && shownEnemyHp > 0)
        {
            yield return new WaitForSeconds(0.12f);
            Burst(EnemyBody(), Poison, 18, 120, -120, 0.7f, 7);
            // Keep the poison text clear of the direct-hit numbers above the enemy.
            Pop($"-{poisonTick} 독", Poison, EnemyBody() + new Vector2(120, 34), 40);
            Sfx("hit", 0.5f, 1.4f);
            shownEnemyHp = Mathf.Max(0, shownEnemyHp - poisonTick);
            enemyHitAt = Time.time;
            yield return new WaitForSeconds(0.25f);
        }

        // 4. enemy dies, or strikes back
        if (run.Phase == Phase.Reward || run.Phase == Phase.Won)
        {
            enemyDeathAt = Time.time;
            Sfx("kill");
            Burst(EnemyBody(), new Color(0.6f, 0.4f, 0.7f), 30, 260, 300, 0.9f, 10);
            Burst(EnemyBody(), Color.white, 12, 180, 0, 0.5f, 6);
            Flash(Color.white, 0.25f);
            yield return Hitstop(0.12f);
            Pop($"+{shownEnemy.gold} 골드", Gold, new Vector2(W - 150, 60), 30);
            shownGold = run.Gold;
            yield return new WaitForSeconds(0.8f);
        }
        else
        {
            yield return new WaitForSeconds(0.15f);
            if (run.EnemyPhase2 && phaseShownFor != run.Enemy)
            {
                phaseShownFor = run.Enemy;
                Sfx("heavy", 0.8f, 0.5f);
                Flash(HitRed, 0.4f);
                Shake(14);
                Burst(EnemyBody(), new Color(1f, 0.2f, 0.1f), 40, 320, -100, 1f, 10);
                Pop($"{shownEnemy.name} {(string.IsNullOrEmpty(shownEnemy.phase2Name) ? "2단계" : shownEnemy.phase2Name)}!", HitRed, new Vector2(W / 2, 200), 64);
                yield return new WaitForSeconds(0.9f);
            }
            if (!o.frozen && EnemySkillName(move.Kind) is string skillName)
            {
                Sfx("skill", 0.7f);
                Burst(EnemyBody(), new Color(0.75f, 0.5f, 1f), 18, 160, -60, 0.7f, 7);
                Pop(skillName + "!", new Color(0.8f, 0.6f, 1f), EnemyBody() + new Vector2(0, -110), 40);
                yield return new WaitForSeconds(0.35f);
            }
            if (o.frozen)
            {
                Pop("얼어붙었다!", Ice, EnemyBody() + new Vector2(-60, -60), 36);
                Burst(EnemyBody(), Ice, 14, 120, 0, 0.7f, 6);
                yield return new WaitForSeconds(0.4f);
            }
            else if (move.Kind == "enrage")
            {
                Sfx("heavy", 0.9f, 0.6f);
                Flash(HitRed, 0.25f);
                for (int k = 0; k < 3; k++)
                {
                    Burst(EnemyBody() + new Vector2(0, 40), new Color(1f, 0.25f, 0.1f), 12, 90, -260, 0.6f, 9);
                    yield return new WaitForSeconds(0.08f);
                }
                Pop($"분노! 공격 +{move.Value}", HitRed, EnemyBody() + new Vector2(0, -80), 38);
                yield return new WaitForSeconds(0.3f);
            }
            else if (move.Kind == "guard")
            {
                yield return new WaitForSeconds(0.2f);
            }
            else if (move.Kind == "heal" || move.Kind == "reflect")
            {
                if (move.Kind == "heal") Burst(EnemyBody(), HealGreen, 16, 120, -120, 0.8f, 7);
                int taken = healed - run.Hp;
                if (taken > 0)
                {
                    heroHitAt = Time.time;
                    Sfx("hurt");
                    Flash(new Color(0.75f, 0.5f, 1f), 0.3f);
                    Pop($"-{taken}", HitRed, new Vector2(FrontX - 80, FloorY - 60), 58);
                }
                shownHp = run.Hp;
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                yield return Tween(v => enemyOffset = new Vector2(-v * (EnemyX - FrontX - 110), -Mathf.Sin(v * Mathf.PI) * 30), 0, 1, 0.15f);
                int blocked = Mathf.Min(o.block, attack), taken = healed - run.Hp;
                var heroCenter = MemberCenter(0);
                if (blocked > 0)
                {
                    Pop($"막음 {blocked}", BlockBlue, MemberHead(0) + new Vector2(80, 10), 34);
                    AddFx("ring", heroCenter, 0.35f, 120, BlockBlue);
                    Sfx("block", 0.7f);
                    Burst(heroCenter + new Vector2(40, 0), BlockBlue, 8, 160, 0, 0.4f, 6);
                }
                if (taken > 0)
                {
                    heroHitAt = Time.time;
                    AddFx("claw", heroCenter, 0.35f, 110, HitRed);
                    Sfx("hurt");
                    Burst(heroCenter, HitRed, 14, 220, 200, 0.6f, 7);
                    Flash(HitRed, 0.35f);
                    Shake(10);
                    Pop($"-{taken}", HitRed, new Vector2(FrontX - 80, FloorY - 60), 58);
                    yield return Hitstop(0.08f);
                }
                shownHp = run.Hp;
                heroDown = run.Phase == Phase.Lost;
                int through = Mathf.Max(0, attack - o.block);
                if (move.Kind == "drain" && through > 0)
                {
                    for (int k = 0; k < 14; k++)
                        shots.Add(new Shot { tex = glow, from = MemberCenter(0) + Random.insideUnitCircle * 30, to = EnemyBody(), start = Time.time + k * 0.02f, dur = 0.35f, arc = Random.Range(-60, 60), scale = 0.25f, color = HitRed, glowing = true });
                    Pop($"+{through} 흡혈", HitRed, EnemyBody() + new Vector2(60, -90), 34);
                    shownEnemyHp = run.EnemyHp;
                }
                if (move.Kind == "curse")
                {
                    Sfx("lose", 0.5f, 1.6f);
                    Burst(new Vector2(W / 2, ReelY + ReelSize / 2), new Color(0.6f, 0.3f, 0.8f), 24, 260, 0, 0.7f, 8);
                    Pop("저주! 슬롯에 해골", new Color(0.75f, 0.5f, 1f), new Vector2(W / 2, ReelY + 30), 36);
                }
                yield return Tween(v => enemyOffset = new Vector2(-v * (EnemyX - FrontX - 110), 0), 1, 0, 0.2f);
                // thorns / spikes: the enemy hurt itself on the party
                if (run.Phase == Phase.Turn && run.EnemyHp < shownEnemyHp)
                {
                    int back = shownEnemyHp - run.EnemyHp;
                    AddFx("ring", MemberCenter(0), 0.4f, 200, Poison);
                    Burst(EnemyBody(), new Color(0.4f, 0.8f, 0.3f), 14, 200, 200, 0.5f, 6);
                    Pop($"-{back} 반사", Poison, EnemyBody() + new Vector2(-40, -70), 38);
                    Sfx("slash", 0.7f, 1.3f);
                    enemyHitAt = Time.time;
                    shownEnemyHp = run.EnemyHp;
                }
            }
            yield return new WaitForSeconds(0.25f);
        }

        busy = false;
        Sync();
        if (run.Phase == Phase.Turn) SpinAll();
    }

    /// <summary>One member's hit in its style: melee lunge, arrow, magic bolt, leap slam or light beam.</summary>
    IEnumerator Attack(int i, int share, int enemyBefore, int total, bool last)
    {
        var m = run.Member(i);
        var from = MemberCenter(i);
        var target = EnemyBody();
        switch (m.attack)
        {
            case "arrow":
                yield return Tween(v => offset[i] = new Vector2(-14 * Mathf.Sin(v * Mathf.PI), 0), 0, 1, 0.12f);
                Sfx("arrow");
                yield return FireShot(T("0x72/weapon_arrow"), from, target, 0.2f, 20, 0, 3, 90, Color.white, false);
                break;
            case "magic":
                yield return Tween(v => offset[i] = new Vector2(0, -20 * Mathf.Sin(v * Mathf.PI)), 0, 1, 0.15f);
                bool ice = shownFaces[i].id == "ice";
                Sfx(ice ? "ice" : "magic", 0.8f);
                var bolt = ice ? Ice : shownFaces[i].id == "fire" ? new Color(1f, 0.55f, 0.2f) : Hex(shownFaces[i].color);
                yield return FireShot(glow, from + new Vector2(20, -40), target, 0.28f, 50, 0, 1.4f, 0, bolt, true);
                break;
            case "leap":
                yield return Tween(v => offset[i] = new Vector2(v * (StrikeX - MemberX(i)), -Mathf.Sin(v * Mathf.PI) * 140), 0, 1, 0.3f);
                AddFx("ring", new Vector2(StrikeX + 40, FloorY - 10), 0.4f, 260, new Color(1f, 0.8f, 0.5f));
                Sfx("heavy");
                Burst(new Vector2(StrikeX + 40, FloorY - 10), new Color(0.7f, 0.6f, 0.5f), 16, 220, 400, 0.6f, 8);
                Shake(10);
                break;
            case "beam":
                yield return Tween(v => offset[i] = new Vector2(0, -16 * Mathf.Sin(v * Mathf.PI)), 0, 1, 0.15f);
                AddFx("beam", target, 0.45f, 90, new Color(1f, 0.95f, 0.6f));
                Sfx("beam", 0.7f);
                Burst(target, new Color(1f, 0.95f, 0.6f), 14, 120, -120, 0.8f, 6);
                yield return new WaitForSeconds(0.12f);
                break;
            default: // melee
                yield return Tween(v => offset[i] = new Vector2(v * (StrikeX - MemberX(i)), 0), 0, 1, 0.14f);
                AddFx("slash", target, 0.25f, 150, Color.white);
                Sfx("slash");
                break;
        }
        // impact
        shownEnemyHp = Mathf.Max(0, shownEnemyHp - share);
        if (last) shownEnemyHp = Mathf.Max(0, enemyBefore - total);
        enemyHitAt = Time.time;
        Sfx("hit", 0.8f, Random.Range(0.9f, 1.15f));
        Burst(target, Gold, 10, 260, 300, 0.45f, 6);
        Pop($"-{share}", Color.white, target + new Vector2(Random.Range(-40, 40), Random.Range(-20, 20)), 40);
        yield return Hitstop(0.05f);
        yield return Tween(v => offset[i] = Vector2.Lerp(offset[i], Vector2.zero, v), 0, 1, 0.18f);
        offset[i] = Vector2.zero;
    }

    /// <summary>The cast effect for one armed skill.</summary>
    IEnumerator SkillFx(int i)
    {
        var m = run.Member(i);
        var at = MemberCenter(i);
        var enemy = EnemyBody();
        switch (m.skill)
        {
            case "shieldBash":
                auraBlue.Add(i);
                yield return FireShot(T("shield"), at, enemy, 0.3f, 60, 720, 5, 0, Color.white, false);
                Sfx("block");
                Sfx("heavy");
                AddFx("ring", enemy, 0.45f, 300, BlockBlue);
                Burst(enemy, BlockBlue, 20, 300, 200, 0.6f, 8);
                Flash(Color.white, 0.4f);
                Shake(12);
                yield return Hitstop(0.08f);
                auraBlue.Remove(i);
                break;
            case "volley":
                for (int k = 0; k < 3; k++)
                {
                    Sfx("arrow", 0.8f, 1 + k * 0.1f);
                    StartCoroutine(FireShot(T("0x72/weapon_arrow"), at + new Vector2(0, -10 + k * 10), enemy + new Vector2(0, -20 + k * 20), 0.18f, 30 + k * 15, 0, 3, 90, Gold, false));
                    yield return new WaitForSeconds(0.08f);
                }
                yield return new WaitForSeconds(0.15f);
                Burst(enemy, Gold, 16, 260, 200, 0.5f, 6);
                break;
            case "freeze":
                frozenUntil = Time.time + 3f;
                Sfx("ice");
                for (int k = 0; k < 6; k++) AddFx("ice", enemy + new Vector2(Mathf.Cos(k) * 70, Mathf.Sin(k * 2.1f) * 50), 2.6f, 50 + k * 6, Ice);
                Burst(enemy, Ice, 24, 160, 0, 1f, 7);
                Flash(Ice, 0.35f);
                yield return new WaitForSeconds(0.35f);
                break;
            case "rage":
                auraRed.Add(i);
                Flash(HitRed, 0.45f);
                Sfx("heavy", 0.8f, 0.7f);
                for (int k = 0; k < 4; k++)
                {
                    Burst(at + new Vector2(0, 30), new Color(1f, 0.3f, 0.1f), 10, 80, -260, 0.6f, 9);
                    yield return new WaitForSeconds(0.07f);
                }
                break;
            case "prayer":
                AddFx("beam", MemberCenter(Mathf.Min(1, run.Party.Count - 1)), 0.7f, 260, new Color(0.8f, 1f, 0.7f));
                Sfx("beam");
                for (int k = 0; k < run.Party.Count; k++) Burst(MemberCenter(k), HealGreen, 10, 50, -140, 1f, 6);
                yield return new WaitForSeconds(0.35f);
                break;
            case "backstab":
                for (int k = 0; k < 3; k++)
                {
                    AddFx("slash", enemy + new Vector2(Random.Range(-30, 30), Random.Range(-30, 30)), 0.2f, 140, Poison);
                    Sfx("slash", 0.8f, 1.2f + k * 0.1f);
                    yield return new WaitForSeconds(0.08f);
                }
                Burst(enemy, Poison, 20, 220, 100, 0.6f, 7);
                break;
            case "transmute":
                Sfx("coin");
                for (int k = 0; k < shownFaces.Length; k++)
                    if (shownFaces[k].curse) Burst(ReelCenter(k), Gold, 24, 160, 100, 0.7f, 7);
                Flash(Gold, 0.25f);
                yield return new WaitForSeconds(0.35f);
                break;
            case "bless":
                AddFx("beam", MemberCenter(Mathf.Min(2, run.Party.Count - 1)), 0.7f, 360, new Color(1f, 0.95f, 0.7f));
                Sfx("skill", 0.8f, 1.3f);
                for (int k = 0; k < run.Party.Count; k++) if (k != i) Burst(MemberCenter(k), Gold, 8, 60, -160, 0.9f, 6);
                yield return new WaitForSeconds(0.35f);
                break;
            case "thorns":
                Sfx("ice", 0.6f, 0.7f);
                for (int k = 0; k < run.Party.Count; k++) AddFx("ring", MemberCenter(k), 0.6f, 160, Poison);
                Burst(MemberCenter(0) + new Vector2(60, 0), new Color(0.3f, 0.6f, 0.2f), 20, 120, 0, 0.8f, 8);
                yield return new WaitForSeconds(0.3f);
                break;
            case "allIn":
                Sfx("coin");
                for (int k = 0; k < 14; k++) FireShotNoWait(T("0x72/coin_anim_f0"), at, enemy, 0.25f + k * 0.02f);
                yield return new WaitForSeconds(0.35f);
                Sfx("heavy", 0.8f, 1.2f);
                Burst(enemy, Gold, 24, 260, 100, 0.7f, 7);
                break;
            case "boneBurst":
                Sfx("heavy", 0.8f, 0.8f);
                Flash(new Color(0.9f, 0.9f, 1f), 0.25f);
                for (int k = 0; k < 6; k++) FireShotNoWait(T("0x72/skull"), at, enemy, 0.22f + k * 0.04f);
                yield return new WaitForSeconds(0.4f);
                Burst(enemy, new Color(0.9f, 0.9f, 0.95f), 26, 280, 200, 0.7f, 8);
                break;
            default:
                yield return new WaitForSeconds(0.2f);
                break;
        }
    }

    /// <summary>A projectile that flies on its own while the caller keeps going.</summary>
    void FireShotNoWait(Texture2D t, Vector2 from, Vector2 to, float time) =>
        StartCoroutine(FireShot(t, from + Random.insideUnitCircle * 20, to + Random.insideUnitCircle * 30, time, 40, 0, 4, 0, Color.white, false));

    Vector2 ReelCenter(int i)
    {
        int n = shownFaces.Length;
        float startX = W / 2 - (n * ReelSize + (n - 1) * ReelGap) / 2;
        return new Vector2(startX + i * (ReelSize + ReelGap) + ReelSize / 2, ReelY + ReelSize / 2);
    }

    IEnumerator FireShot(Texture2D t, Vector2 from, Vector2 to, float dur, float arc, float spin, float scale, float angle, Color c, bool glowing)
    {
        shots.Add(new Shot { tex = t, from = from, to = to, start = Time.time, dur = dur, arc = arc, spin = spin, scale = scale, angle = angle, color = c, glowing = glowing });
        yield return new WaitForSeconds(dur);
        if (glowing) AddFx("ring", to, 0.3f, 140, c);
    }

    IEnumerator Hitstop(float seconds)
    {
        if (!paused) Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = paused ? 0 : 1; // don't unpause the game from under the menu
    }

    static IEnumerator Tween(System.Action<float> set, float from, float to, float dur)
    {
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = 1 - (1 - t / dur) * (1 - t / dur); // ease out
            set(Mathf.Lerp(from, to, k));
            yield return null;
        }
        set(to);
    }

    void Pop(string text, Color c, Vector2 pos, int size) =>
        popups.Add(new Popup { text = text, color = c, pos = pos, at = Time.time, size = size });

    void Burst(Vector2 at, Color c, int count, float speed, float gravity, float life, float size)
    {
        for (int k = 0; k < count; k++)
        {
            float a = Random.Range(0, Mathf.PI * 2), s = Random.Range(0.3f, 1f) * speed;
            particles.Add(new Particle
            {
                pos = at, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, born = Time.time,
                life = life * Random.Range(0.6f, 1f), size = size * Random.Range(0.6f, 1.3f), gravity = gravity, color = c,
            });
        }
    }

    void AddFx(string kind, Vector2 pos, float dur, float size, Color c) =>
        fxs.Add(new Fx { kind = kind, pos = pos, start = Time.time, dur = dur, size = size, color = c });

    void Flash(Color c, float strength)
    {
        flashColor = c;
        flashColor.a = strength;
        flashAt = Time.time;
    }

    void Shake(float amount)
    {
        shakeAt = Time.time;
        shakeAmount = amount;
    }

    // ---------- drawing ----------

    void OnGUI()
    {
        InitStyles();
        // uniform scale, centred: other aspect ratios get dark bars instead of a stretched picture
        float scale = Mathf.Min(Screen.width / W, Screen.height / H);
        var origin = new Vector3((Screen.width - W * scale) / 2, (Screen.height - H * scale) / 2, 0);
        GUI.matrix = Matrix4x4.Translate(origin) * Matrix4x4.Scale(new Vector3(scale, scale, 1)) *
                     Matrix4x4.Translate(shakeOn ? new Vector3(Wobble(shakeAt, shakeAmount), Wobble(shakeAt + 0.05f, shakeAmount * 0.7f), 0) : Vector3.zero);
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && onTitle && showCodex) { showCodex = false; e.Use(); }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && !onTitle) { SetPaused(!paused); e.Use(); }
        if (e.type == EventType.KeyDown && !paused && !onTitle && talk.Count == 0) HandleKey(e);

        DrawArena();
        DrawFx(false);
        DrawShots();
        DrawParticles();
        if (!onTitle) DrawHeader();

        if (paused) { } // the pause menu is drawn on top below; nothing underneath takes clicks
        else if (onTitle && showCodex) DrawCodex();
        else if (onTitle) DrawTitle();
        else if (talk.Count > 0 && !busy) DrawStory();   // dialogue blocks every other panel until it is read
        else if (busy || run.Phase == Phase.Turn) DrawReels();
        else if (eventResult != null) DrawEventResult();
        else if (run.Phase == Phase.Map) DrawMap();
        else if (run.Phase == Phase.Reward) DrawOffers();
        else if (run.Phase == Phase.Shop) DrawShop();
        else if (run.Phase == Phase.Event) DrawEvent();
        else DrawEnd();

        DrawFx(true);
        DrawTip();
        tipKey = null;
        DrawPopups();
        if (paused) DrawPause();
        float fade = 1 - Mathf.Clamp01((Time.unscaledTime - fadeAt) / 0.35f);
        if (fade > 0) Fill(new Rect(-20, -20, W + 40, H + 40), new Color(0, 0, 0, fade * 0.9f));
        float flash = shakeOn ? 1 - Mathf.Clamp01((Time.time - flashAt) / 0.25f) : 0;
        if (flash > 0) Fill(new Rect(-20, -20, W + 40, H + 40), new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * flash));

        // bottom line: hovered button's tooltip, else the last turn's log
        if (GUI.tooltip != "") Shadowed(new Rect(0, 688, W, 28), GUI.tooltip, small, Gold);
        else if (!busy && eventResult == null) Shadowed(new Rect(0, 688, W, 28), run.Log, small, new Color(0.8f, 0.8f, 0.85f));
    }

    static readonly KeyCode[] SkillKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y };
    static readonly string[] SkillKeyNames = { "Q", "W", "E", "R", "T", "Y" };

    /// <summary>Keyboard play: Space/Enter runs the turn, 1-6 reroll a reel, Q-Y arm a skill, 1-3 pick a reward.</summary>
    void HandleKey(Event e)
    {
        if (eventResult != null && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            CloseEventResult();
            e.Use();
            return;
        }
        int digit = e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9 ? e.keyCode - KeyCode.Alpha1
            : e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad9 ? e.keyCode - KeyCode.Keypad1 : -1;
        if (run.Phase == Phase.Turn && !busy && !AnySpinning())
        {
            if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Sfx("ui_click");
                StartCoroutine(PlayTurn());
                e.Use();
            }
            else if (digit >= 0 && digit < run.Reels.Count && run.Reroll(digit))
            {
                shownFaces[digit] = run.Faces[digit];
                spinUntil[digit] = Time.time + 0.45f;
                e.Use();
            }
            else if (System.Array.IndexOf(SkillKeys, e.keyCode) is int k && k >= 0 && k < run.Party.Count && run.ToggleSkill(k))
            {
                Sfx(run.Armed.Contains(k) ? "skill" : "ui_click", 0.6f);
                e.Use();
            }
        }
        else if (run.Phase == Phase.Reward && !busy && digit >= 0 && digit < run.Offers.Count)
        {
            var offer = run.Offers[digit];
            run.ChooseReward(digit);
            Sfx(offer.Kind == RewardKind.Gold ? "coin" : "ui_click");
            Sync();
            e.Use();
        }
    }

    /// <summary>Party HP lost this turn if committed now (0 when the hit kills the enemy first).</summary>
    int Incoming(SpinOutcome o)
    {
        int direct = run.DamageDealt(o);
        int poison = Mathf.Min(Mathf.Max(0, run.EnemyHp - direct), run.EnemyPoison + o.poison);
        return direct + poison >= run.EnemyHp ? 0 : Mathf.Max(0, run.EnemyAttack(o) - o.block) + o.selfDamage + run.Reflected(o);
    }

    int DamageWithPoison(SpinOutcome o)
    {
        int direct = run.DamageDealt(o);
        return direct + Mathf.Min(Mathf.Max(0, run.EnemyHp - direct), run.EnemyPoison + o.poison);
    }

    /// <summary>A pulsing slice of a bar between two values: what this turn would take away or give back.</summary>
    static void Ghost(float centerX, float bottom, float width, int from, int to, int max, Color c)
    {
        if (from == to || max <= 0) return;
        float a = Mathf.Clamp01((float)Mathf.Min(from, to) / max), b = Mathf.Clamp01((float)Mathf.Max(from, to) / max);
        c.a = 0.55f + 0.35f * Mathf.Sin(Time.time * 7);
        Fill(new Rect(centerX - width / 2 + width * a, bottom - 14, width * (b - a), 14), c);
    }

    void DrawHeader()
    {
        int rows = run.Map.Rows.Count;
        string where = run.Current == null ? "입구" : run.Current.Type == NodeType.Boss ? "보스" : $"{run.Current.Row + 1} / {rows}";
        string diff = run.Ascension > 0 ? $" · 어려움 {run.Ascension}" : "";
        Shadowed(new Rect(0, 8, W, 36), $"{run.Chapter + 1}장 · {run.ChapterDef.name} · {where}{diff}", small, Color.white);
        DrawTex(T("0x72/coin_anim_f" + (int)(Time.time * 8) % 4), new Rect(W - 150, 14, 18, 21));
        Shadowed(new Rect(W - 126, 10, 120, 30), shownGold.ToString(), mid, Gold, TextAnchor.MiddleLeft);
        if (!onTitle && GUI.Button(new Rect(W - 190, 10, 30, 30), new GUIContent("Ⅱ", "일시정지 · 설정 (Esc)"), smallButton)) SetPaused(true);

        // relics, top left; hover for what they do
        for (int k = 0; k < run.Relics.Count; k++)
        {
            var item = run.Item(run.Relics[k]);
            var r = new Rect(12 + k * 38, 6, 32, 32);
            Fill(r, new Color(0.25f, 0.2f, 0.12f, 0.9f));
            ItemIcon(item, r);
            GUI.Label(r, new GUIContent("", $"유물 · {item.name}: {item.desc}"));
        }
    }

    void DrawStory()
    {
        var say = talk.Peek();
        var box = new Rect(60, 452, W - 120, 220);
        DrawPanel(box);
        float textX = box.x + 30;
        if (say.sprite != null)
        {
            var t = Anim(say.sprite);
            float s = Mathf.Floor(150f / Mathf.Max(t.width, t.height));
            GUI.DrawTexture(new Rect(box.x + 30, box.yMax - 20 - t.height * s, t.width * s, t.height * s), t);
            textX = box.x + 200;
        }
        if (say.name != "") Shadowed(new Rect(textX, box.y + 18, 400, 34), say.name, mid, Gold, TextAnchor.MiddleLeft);
        int shown = Mathf.Min(say.text.Length, (int)((Time.time - lineStart) * 40)); // typewriter
        story_.normal.textColor = say.name == "" ? new Color(0.85f, 0.85f, 0.9f) : Color.white;
        GUI.Label(new Rect(textX, box.y + 60, box.xMax - textX - 30, 120), say.text.Substring(0, shown), story_);
        if (shown >= say.text.Length && (int)(Time.time * 2) % 2 == 0)
            Shadowed(new Rect(box.xMax - 160, box.yMax - 40, 140, 30), "클릭 ▶", small, Gold, TextAnchor.MiddleRight);
        if (GUI.Button(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none))
        {
            if (shown < say.text.Length) lineStart = -999; // first click finishes the line
            else { talk.Dequeue(); lineStart = Time.time; }
        }
    }

    // what an event choice did, shown until the player presses 계속 (run.Log alone was a small line at the bottom)
    class EventResult { public string title, choice; public string[] lines; public float at; }
    EventResult eventResult;

    static bool BadLine(string l) => !l.Contains("이미") && (l.Contains("해골") || l.Contains("꽝") || l.Contains("뒷면") || l.StartsWith("-") || l.Contains(" -") || l.Contains("없다"));

    void ShowEventResult(string title, string choice)
    {
        var lines = run.Log.Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries);
        eventResult = new EventResult { title = title, choice = choice, at = Time.time,
            lines = lines.Length > 0 ? lines : new[] { "아무 일도 일어나지 않았다" } };
        bool bad = lines.Any(BadLine), good = lines.Any(l => !BadLine(l));
        if (good) Sfx("coin");
        if (bad) Sfx("hurt", 0.6f);
    }

    void DrawEventResult()
    {
        var r = eventResult;
        DrawPanel(new Rect(W / 2 - 450, 345, 900, 335));
        Shadowed(new Rect(W / 2 - 403, 354, 806, 44), r.title, big, Gold, TextAnchor.MiddleLeft);
        Shadowed(new Rect(W / 2 - 403, 398, 806, 28), $"선택: {r.choice}", small, new Color(0.8f, 0.8f, 0.85f), TextAnchor.MiddleLeft);
        Fill(new Rect(W / 2 - 403, 430, 806, 2), new Color(0.37f, 0.29f, 0.24f));
        Shadowed(new Rect(W / 2 - 403, 438, 806, 26), "결과", tiny, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleLeft);
        for (int k = 0; k < r.lines.Length; k++)
        {
            float show = Mathf.Clamp01((Time.time - r.at - 0.25f * k) / 0.2f); // lines land one after another
            if (show <= 0) break;
            var line = r.lines[k];
            bool bad = BadLine(line);
            var row = new Rect(W / 2 - 403 + (1 - show) * 30, 466 + k * 44, 806, 40);
            Fill(row, (bad ? HitRed : HealGreen) * new Color(1, 1, 1, 0.12f * show));
            Fill(new Rect(row.x, row.y, 5, row.height), (bad ? HitRed : HealGreen) * new Color(1, 1, 1, show));
            Shadowed(new Rect(row.x + 20, row.y, row.width - 20, row.height), (bad ? "▼ " : "▲ ") + line, mid,
                (bad ? HitRed : HealGreen) * new Color(1, 1, 1, show), TextAnchor.MiddleLeft);
        }
        if (GUI.Button(new Rect(W / 2 - 110, 622, 220, 48), new GUIContent("계속", "계속 (Space / Enter)"), turnButton)) CloseEventResult();
    }

    void CloseEventResult()
    {
        eventResult = null;
        Sfx("ui_click");
        SaveRun();
    }

    void DrawEvent()
    {
        var e = run.CurrentEvent;
        DrawPanel(new Rect(W / 2 - 450, 345, 900, 335));
        Shadowed(new Rect(W / 2 - 403, 354, 806, 44), e.title, big, Gold, TextAnchor.MiddleLeft);
        Fill(new Rect(W / 2 - 403, 403, 806, 2), new Color(0.37f, 0.29f, 0.24f));
        story_.normal.textColor = new Color(0.9f, 0.9f, 0.95f);
        GUI.Label(new Rect(W / 2 - 403, 414, 806, 66), e.text, story_);
        for (int i = 0; i < e.options.Length; i++)
        {
            var option = e.options[i];
            GUI.enabled = run.CanChoose(option);
            var choice = new Rect(W / 2 - 403, 478 + i * 56, 806, 48);
            bool available = GUI.enabled;
            int effectStart = option.label.LastIndexOf(" (", System.StringComparison.Ordinal);
            bool hasEffects = effectStart > 0 && option.label.EndsWith(")");
            string choiceName = hasEffects ? option.label.Substring(0, effectStart) : option.label;
            string effects = hasEffects ? option.label.Substring(effectStart + 2, option.label.Length - effectStart - 3) : "";
            if (GUI.Button(choice, GUIContent.none, button))
            {
                run.ChooseEvent(i);
                Sfx("ui_click");
                if (run.Phase != Phase.Turn) ShowEventResult(e.title, choiceName);
                Sync();
                if (run.Phase == Phase.Turn) SpinAll();
                Burst(new Vector2(W / 2, 500), Gold, 16, 160, 100, 0.6f, 6);
            }
            GUI.enabled = true;
            bool risky = option.effects.Any(effect => effect.kind == "curse" || effect.kind == "fight"
                || (effect.kind == "hp" && effect.value < 0));
            var choiceColor = available ? Color.white : new Color(0.62f, 0.57f, 0.59f);
            var effectColor = available ? (risky ? new Color(1f, 0.69f, 0.53f) : Gold) : choiceColor;
            Shadowed(new Rect(choice.x + 20, choice.y, 390, choice.height), choiceName, small, choiceColor, TextAnchor.MiddleLeft);
            Shadowed(new Rect(choice.x + 414, choice.y, 371, choice.height), effects, tiny, effectColor, TextAnchor.MiddleRight);
        }
    }

    static readonly Rect IntentPlate = new Rect(EnemyX - 354, 78, 252, 86);

    void DrawArena()
    {
        var tint = Hex(run.ChapterDef.floorTint);
        var old = GUI.color;
        GUI.color = tint;
        for (float x = 0; x < W; x += Tile)
        {
            GUI.DrawTexture(new Rect(x, 44, Tile, Tile), T("0x72/wall_top_mid"));
            GUI.DrawTexture(new Rect(x, 44 + Tile, Tile, Tile), T("0x72/wall_mid"));
            for (float y = 44 + 2 * Tile; y < FloorY + 20; y += Tile)
                GUI.DrawTexture(new Rect(x, y, Tile, Tile), T($"0x72/floor_{1 + (int)(x * 7 + y * 13) % 8}"));
        }
        GUI.color = old;
        DrawDecor();
        // vignette: darker toward the arena's edges
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 44, W, FloorY + 20 - 44), vignette, ScaleMode.StretchToFill);
        GUI.color = old;
        Fill(new Rect(0, FloorY + 20, W, H), Panel);

        for (int i = run.Party.Count - 1; i >= 0; i--) DrawMember(i);
        if (onTitle) return;
        Bar(FrontX - MemberGap * 0.5f, 132, shownHp, run.MaxHp, HealGreen, 220);
        if (run.Phase == Phase.Turn && !busy && talk.Count == 0 && !AnySpinning())
        {
            var preview = run.Preview();
            int after = Mathf.Clamp(run.Hp + preview.heal - Incoming(preview), 0, run.MaxHp);
            Ghost(FrontX - MemberGap * 0.5f, 132, 220, run.Hp, after, run.MaxHp, after < run.Hp ? HitRed : Color.white);
            if (after != run.Hp)
                Shadowed(new Rect(FrontX - MemberGap * 0.5f + 116, 112, 120, 24), after < run.Hp ? $"→ {after}" : $"→ {after}", small, after < run.Hp ? HitRed : HealGreen, TextAnchor.MiddleLeft);
        }
        Shadowed(new Rect(FrontX - MemberGap * 0.5f - 110, 88, 220, 24), "파티 HP", small, Color.white);

        if (!busy && run.Phase != Phase.Turn || shownEnemy == null) return;
        var e = shownEnemy;
        float dead = enemyDeathAt > 0 ? Mathf.Clamp01((Time.time - enemyDeathAt) / 0.5f) : 0;
        float enter = Mathf.Clamp01((Time.time - enemyEnterAt) / 0.4f);
        float ex = EnemyX + enemyOffset.x + Wobble(enemyHitAt, 12) + (1 - enter) * (1 - enter) * 420;
        bool eHit = Time.time - enemyHitAt < 0.15f;
        bool frozen = Time.time < frozenUntil && busy;
        var eTint = eHit ? new Color(1, 1, 1) * 2f : frozen ? Ice : Color.white;
        eTint.a = 1 - dead;
        if (!eHit && !frozen && !string.IsNullOrEmpty(e.tint)) { var a = eTint.a; eTint = Hex(e.tint); eTint.a = a; }
        var eTex = Anim(e.sprite);
        bool elite = IsElite(e);
        if (run.EnemyPhase2 && dead == 0) Glow(new Vector2(ex, FloorY - 90), 380, new Color(1f, 0.15f, 0.1f, 0.3f + 0.12f * Mathf.Sin(Time.time * 8)));
        if (elite && dead == 0) Glow(new Vector2(ex, FloorY - 50), 220, new Color(1f, 0.45f, 0.2f, 0.22f + 0.08f * Mathf.Sin(Time.time * 4)));
        DrawChar(eTex, ex, FloorY + enemyOffset.y + dead * 20, true, eTint, 0, EnemyScale * (1 + dead * 0.3f));
        if (eHit) DrawChar(eTex, ex, FloorY + enemyOffset.y, true, new Color(1, 1, 1, 0.6f), 0, EnemyScale * 1.05f); // white-ish flash layer
        if (dead > 0) return;
        float top = EnemyHead().y;
        Bar(EnemyX, top - 4, shownEnemyHp, Mathf.Max(1, run.EnemyMaxHp), HitRed, 160);
        if (!busy && talk.Count == 0 && !AnySpinning())
        {
            var preview = run.Preview();
            int hurt = DamageWithPoison(preview);
            Ghost(EnemyX, top - 4, 160, run.EnemyHp, Mathf.Max(0, run.EnemyHp - hurt), Mathf.Max(1, run.EnemyMaxHp), Color.white);
            if (hurt >= run.EnemyHp)
                Shadowed(new Rect(EnemyX + 84, top - 24, 90, 24), "처치!", small, Gold, TextAnchor.MiddleLeft);
        }
        if (run.EnemyPoison > 0 && !busy)
        {
            DrawTex(T("0x72/weapon_knife"), new Rect(EnemyX - 80, top + 4, 8, 20));
            Shadowed(new Rect(EnemyX - 66, top + 2, 120, 24), $"독 {run.EnemyPoison}", tiny, Poison, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(EnemyX - 80, top, 120, 28), new GUIContent("", $"독: 매 턴 {run.EnemyPoison} 피해, 그 뒤 1씩 줄어든다"));
        }
        if (run.EnemyStolen > 0 && !busy)
            Shadowed(new Rect(EnemyX - 150, FloorY - 4, 300, 22), $"빼앗긴 골드 {run.EnemyStolen} (처치하면 되찾음)", tiny, Gold);
        bool boss = run.Current?.Type == NodeType.Boss;
        string label = run.EnemyPhase2 ? $"{e.name} · {(string.IsNullOrEmpty(e.phase2Name) ? "2단계" : e.phase2Name)}" : elite ? $"{e.name} · 정예" : e.name;
        Shadowed(new Rect(EnemyX - 200, top - 62, 400, 30), label, mid,
            boss ? Gold : elite ? new Color(1f, 0.62f, 0.35f) : Color.white);
        if (!busy && talk.Count == 0)
        {
            var o = AnySpinning() ? new SpinOutcome() : run.Preview();
            int atk = run.EnemyAttack(o);
            var mv = run.Intent;
            int reduced = run.EnemyIntent - atk;
            string dmg = atk.ToString();
            var purple = new Color(0.75f, 0.5f, 1f);
            (string icon, string label, Color color, string tip) intent =
                o.frozen ? ("0x72/flask_big_blue", "얼음!", Ice, "얼어서 이번 턴 아무것도 못 한다")
                : mv.Kind == "guard" ? ("shield", $"방어 {mv.Value}", BlockBlue, $"이번 턴 받는 피해 -{mv.Value}")
                : mv.Kind == "enrage" ? ("0x72/weapon_mace", $"분노 +{mv.Value}", HitRed, $"이후 모든 공격 +{mv.Value}")
                : mv.Kind == "curse" ? ("0x72/skull", $"저주 {dmg}", purple, $"{atk} 피해 + 슬롯 하나에 해골 (이 전투 동안)")
                : mv.Kind == "drain" ? ("0x72/flask_big_red", $"흡혈 {dmg}", HitRed, $"{atk} 피해, 막지 못한 만큼 회복")
                : mv.Kind == "heal" ? ("0x72/flask_big_green", $"재생 {mv.Value}", HealGreen, $"공격하지 않고 체력 {mv.Value} 회복. 얼리면 막을 수 있다")
                : mv.Kind == "jam" ? ("0x72/weapon_saw_sword", $"슬롯 고장 {dmg}", new Color(0.75f, 0.5f, 1f), $"{atk} 피해, 이번 턴 회색 슬롯 하나는 아무 효과가 없다")
                : mv.Kind == "sap" ? ("0x72/flask_big_blue", $"마력 흡수 {dmg}", new Color(0.75f, 0.5f, 1f), $"{atk} 피해 + 가장 많이 찬 스킬 게이지를 비운다. 찬 스킬은 지금 쓰자")
                : mv.Kind == "reflect" ? ("0x72/weapon_baton_with_spikes", $"가시 갑피 {mv.Value}%", new Color(0.75f, 0.5f, 1f), $"공격하지 않지만, 이번 턴 준 피해의 {mv.Value}%가 파티에 되돌아온다 (방어 무시)")
                : mv.Kind == "steal" ? ("0x72/coin_anim_f0", $"강탈 {dmg}", Gold, $"{atk} 피해, 막지 못한 만큼 골드를 빼앗는다 (처치하면 되찾음)")
                : mv.Kind == "seal" ? ("0x72/crate", $"봉인 {dmg}", new Color(0.6f, 0.7f, 0.9f), $"{atk} 피해, 이번 턴 다시 돌리기 불가")
                : ("0x72/weapon_knight_sword", $"공격 {dmg}", HitRed, $"파티에 {atk} 피해");
            DrawPanel(IntentPlate);
            Fill(new Rect(IntentPlate.x + 7, IntentPlate.y + 9, 4, IntentPlate.height - 18), intent.color);
            DrawFit(T(intent.icon), new Rect(IntentPlate.x + 20, IntentPlate.y + 26, 38, 40));
            bool enemySkill = EnemySkillName(mv.Kind) != null && !o.frozen;
            Shadowed(new Rect(IntentPlate.x + 71, IntentPlate.y + 7, 165, 22), enemySkill ? "적의 스킬" : "적의 다음 행동", tiny,
                enemySkill ? new Color(0.8f, 0.6f, 1f) : Color.white, TextAnchor.MiddleLeft);
            Shadowed(new Rect(IntentPlate.x + 71, IntentPlate.y + 31, 166, 31), intent.label, mid, intent.color, TextAnchor.MiddleLeft);
            if (mv.Kind == "jam" && !o.frozen && run.JammedReel >= 0)
                Shadowed(new Rect(IntentPlate.x + 71, IntentPlate.y + 61, 166, 19), $"{run.Member(run.JammedReel).name} 슬롯 고장", tiny, new Color(0.8f, 0.6f, 1f), TextAnchor.MiddleLeft);
            else if (mv.Kind == "reflect" && !o.frozen && run.Reflected(o) > 0)
                Shadowed(new Rect(IntentPlate.x + 71, IntentPlate.y + 61, 166, 19), $"되돌아올 피해 {run.Reflected(o)}", tiny, HitRed, TextAnchor.MiddleLeft);
            else if (mv.Kind == "seal" && !o.frozen)
                Shadowed(new Rect(IntentPlate.x + 71, IntentPlate.y + 61, 166, 19), "다시 돌리기 봉인", tiny, new Color(0.6f, 0.7f, 0.9f), TextAnchor.MiddleLeft);
            else if (reduced > 0 && !o.frozen)
                Shadowed(new Rect(IntentPlate.x + 71, IntentPlate.y + 61, 166, 19), $"약화로 {reduced} 감소", tiny, Ice, TextAnchor.MiddleLeft);
            GUI.Label(IntentPlate, new GUIContent("", intent.tip), GUIStyle.none);
            if (run.EnemyRage > 0) Shadowed(new Rect(EnemyX + 90, top - 4, 120, 24), $"분노 +{run.EnemyRage}", tiny, HitRed, TextAnchor.MiddleLeft);
        }
    }

    /// <summary>Per-chapter wall dressing: slime and fountains in the swamp, holes and columns in the prison, lava in the cave.</summary>
    void DrawDecor()
    {
        Texture2D Anim3(string b) => T($"0x72/{b}_f{(int)(Time.time * 6) % 3}");
        void Wall(int col, string name) => DrawTex(T("0x72/" + name), new Rect(col * Tile, 44 + Tile, Tile, Tile));
        void Fountain(int col, string color, Color light)
        {
            DrawTex(T("0x72/wall_fountain_top_1"), new Rect(col * Tile, 44, Tile, Tile));
            DrawTex(Anim3($"wall_fountain_mid_{color}_anim"), new Rect(col * Tile, 44 + Tile, Tile, Tile));
            DrawTex(Anim3($"wall_fountain_basin_{color}_anim"), new Rect(col * Tile, 44 + 2 * Tile, Tile, Tile));
            light.a *= 0.8f + 0.2f * Mathf.Sin(Time.time * 7 + col);
            Glow(new Vector2(col * Tile + Tile / 2f, 44 + 2 * Tile), 260, light);
        }
        void Prop(int col, string name, float h) => DrawTex(T("0x72/" + name), new Rect(col * Tile, FloorY - h, Tile, h));

        switch (run.Chapter)
        {
            case 0:
                Wall(3, "wall_goo"); DrawTex(T("0x72/wall_goo_base"), new Rect(3 * Tile, 44 + 2 * Tile, Tile, Tile));
                Wall(12, "wall_goo");
                Wall(7, "wall_banner_green"); Wall(23, "wall_banner_green");
                Fountain(16, "blue", new Color(0.4f, 0.7f, 1f, 0.35f));
                break;
            case 1:
                Wall(4, "wall_hole_1"); Wall(15, "wall_hole_2"); Wall(19, "wall_hole_1");
                Wall(9, "wall_banner_blue"); Wall(23, "wall_banner_blue");
                Prop(0, "column", Tile * 3); Prop(26, "column", Tile * 3);
                Prop(1, "crate", Tile); Prop(25, "crate", Tile);
                break;
            default:
                Fountain(5, "red", new Color(1f, 0.45f, 0.15f, 0.45f));
                Fountain(21, "red", new Color(1f, 0.45f, 0.15f, 0.45f));
                Wall(12, "wall_banner_red"); Wall(2, "wall_banner_yellow"); Wall(25, "wall_banner_red");
                break;
        }
    }

    /// <summary>Drifting chapter particles: swamp fireflies, prison dust, cave embers.</summary>
    void SpawnAmbient()
    {
        if (Random.value > Time.deltaTime * 8) return;
        var p = new Particle { born = Time.time, size = Random.Range(3f, 6f) };
        switch (run.Chapter)
        {
            case 0:
                p.pos = new Vector2(Random.Range(0, W), Random.Range(150, FloorY));
                p.vel = Random.insideUnitCircle * 15; p.life = 3; p.color = new Color(0.8f, 1f, 0.4f);
                break;
            case 1:
                p.pos = new Vector2(Random.Range(0, W), 140);
                p.vel = new Vector2(Random.Range(-5, 5), 25); p.life = 6; p.size = 3; p.color = new Color(0.75f, 0.75f, 0.8f, 0.6f);
                break;
            default:
                p.pos = new Vector2(Random.Range(0, W), FloorY);
                p.vel = new Vector2(Random.Range(-10, 10), -50); p.gravity = -12; p.life = 3.5f; p.color = new Color(1f, 0.55f, 0.15f);
                break;
        }
        particles.Add(p);
    }

    void DrawMember(int i)
    {
        var m = run.Member(i);
        bool front = i == 0;
        bool hit = front && Time.time - heroHitAt < 0.3f;
        bool ready = run.SkillReady(i) || run.Armed.Contains(i);
        Texture2D t;
        bool moving = offset[i].sqrMagnitude > 1 || Time.time - joinedAt[i] < 0.6f || (run.Phase == Phase.Map && !busy);
        if ((heroDown && front) || hit) t = T($"0x72/{m.sprite}_hit_anim_f0") ?? Anim($"{m.sprite}_idle_anim");
        else if (moving && T($"0x72/{m.sprite}_run_anim_f0") != null) t = Anim($"{m.sprite}_run_anim");
        else t = Anim($"{m.sprite}_idle_anim");

        float walkIn = Mathf.Clamp01((Time.time - joinedAt[i]) / 0.6f);
        var c = MemberFeet(i) + offset[i] + new Vector2(-(1 - walkIn) * 300 + (front ? Wobble(heroHitAt, 10) : 0), 0);
        if (auraRed.Contains(i)) Glow(c + new Vector2(0, -60), 150, new Color(1f, 0.25f, 0.1f, 0.6f + 0.2f * Mathf.Sin(Time.time * 20)));
        if (auraBlue.Contains(i)) Glow(c + new Vector2(0, -60), 150, new Color(0.4f, 0.6f, 1f, 0.6f));
        if (ready && !busy) Glow(c + new Vector2(0, -50), 120, new Color(Gold.r, Gold.g, Gold.b, 0.25f + 0.15f * Mathf.Sin(Time.time * 6)));
        var tint = hit ? HitRed : run.Armed.Contains(i) ? Color.Lerp(Color.white, Gold, 0.5f + 0.5f * Mathf.Sin(Time.time * 10)) : Color.white;
        tint.a = walkIn;
        DrawChar(t, c.x, c.y, false, tint, heroDown && front ? 90 : 0, 1);
    }

    void DrawReels()
    {
        int count = shownFaces.Length;
        float startX = W / 2 - (count * ReelSize + (count - 1) * ReelGap) / 2;
        var counts = shownFaces.GroupBy(f => f.id).ToDictionary(g => g.Key, g => g.Count());
        bool settled = !busy && !AnySpinning();
        DrawPanel(new Rect(60, 340, W - 120, 340));
        for (int i = 0; i < count; i++)
        {
            var m = run.Member(i);
            var r = new Rect(startX + i * (ReelSize + ReelGap), ReelY, ReelSize, ReelSize);
            bool spinning = Spinning(i);
            var face = shownFaces[i];
            bool combo = settled && counts[face.id] >= 2;
            float pulse = combo ? 0.6f + 0.4f * Mathf.Sin(Time.time * 6) : 0;
            var frame = combo ? Color.Lerp((face.curse ? HitRed : Gold) * 0.7f, face.curse ? HitRed : Gold, pulse) : new Color(0.55f, 0.45f, 0.3f);
            if (run.Armed.Contains(i)) frame = Color.Lerp(frame, Gold, 0.7f);
            Fill(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), frame);
            Fill(r, new Color(0.12f, 0.1f, 0.14f));

            GUI.BeginGroup(r);
            var local = new Rect(0, 0, r.width, r.height);
            if (spinning)
            {
                var reel = run.Reels[i];
                float s = Time.time * 14 + i * 0.37f;
                float off = (s % 1) * ReelSize;
                for (int k = 0; k < 2; k++)
                {
                    var sym = run.Symbol(reel[((int)s + k * 3 + i) % reel.Count]);
                    var cell = new Rect(0, off - k * ReelSize, ReelSize, ReelSize);
                    Fill(cell, Hex(sym.color) * new Color(1, 1, 1, 0.25f));
                    Icon(sym, cell, 1f, 0.6f);
                }
            }
            else
            {
                float land = Mathf.Clamp01((Time.time - spinUntil[i]) / 0.18f);
                Fill(local, Hex(face.color) * new Color(1, 1, 1, 0.35f));
                Icon(face, new Rect(0, 0, r.width, r.height - 33), 0.88f + 0.20f * (1 - land), 1);
                Fill(new Rect(0, r.height - 31, r.width, 31), new Color(0.08f, 0.07f, 0.11f, 0.94f));
                Shadowed(new Rect(0, r.height - 30, r.width, 28), face.name, small, face.curse ? HitRed : Color.white);
                if (face.id == m.signature && settled) Shadowed(new Rect(0, 2, r.width - 6, 20), "+1", tiny, Gold, TextAnchor.UpperRight);
                if (run.Level(face.id) > 0) Shadowed(new Rect(6, 2, 60, 20), $"Lv{run.Level(face.id) + 1}", tiny, BlockBlue, TextAnchor.LowerLeft);
                if (i == run.JammedReel && !busy)
                {
                    Fill(local, new Color(0.05f, 0.04f, 0.07f, 0.72f));
                    Bar(local.center, r.width * 1.1f, 8, 45, new Color(0.75f, 0.5f, 1f, 0.8f));
                    Bar(local.center, r.width * 1.1f, 8, -45, new Color(0.75f, 0.5f, 1f, 0.8f));
                    Shadowed(new Rect(0, r.height / 2 - 18, r.width, 36), "고장", mid, new Color(0.85f, 0.7f, 1f));
                }
            }
            GUI.EndGroup();
            if (settled) GUI.Label(r, new GUIContent("", SymbolDetails(face)), GUIStyle.none);

            var portrait = Anim($"{m.sprite}_idle_anim");
            DrawTex(portrait, new Rect(r.x - 2, r.y - 34, portrait.width * 1.5f, portrait.height * 1.5f));
            string pips = new string('●', run.Gauge[i]) + new string('○', Mathf.Max(0, run.SkillCharge - run.Gauge[i]));
            tiny.fontSize = m.name.Length > 3 ? 13 : 15;
            Shadowed(new Rect(r.x + 24, r.y - 28, r.width - 20, 22), $"{m.name} {pips}", tiny, run.SkillReady(i) ? Gold : Color.white, TextAnchor.MiddleLeft);
            tiny.fontSize = 15;
            if (run.Equipment[i] != null)
            {
                var item = run.Item(run.Equipment[i]);
                var slot = new Rect(r.xMax - 26, r.y - 30, 26, 26);
                Fill(slot, new Color(0.25f, 0.2f, 0.12f, 0.9f));
                ItemIcon(item, slot);
                GUI.Label(slot, new GUIContent("", $"{m.name}의 장비 · {item.name}: {item.desc}"));
            }

            var strip = run.Reels[i];
            float mini = Mathf.Min(22, (ReelSize + 20) / strip.Count);
            for (int k = 0; k < strip.Count; k++)
                Icon(run.Symbol(strip[k]), new Rect(r.center.x - strip.Count * mini / 2 + k * mini, r.yMax + 7, mini, mini), 1, 0.9f);

            float bw = 42;
            GUI.enabled = !busy && !spinning && run.RerollsLeft > 0;
            var rerollRect = new Rect(r.x, r.yMax + 34, bw, 32);
            if (GUI.Button(rerollRect, new GUIContent("↻", $"{m.name} 슬롯 다시 돌리기 (숫자 {i + 1})"), smallButton))
            {
                run.Reroll(i);
                shownFaces[i] = run.Faces[i];
                spinUntil[i] = Time.time + 0.45f;
            }
            GUI.enabled = true;
            bool skillReady = run.SkillReady(i) && !run.Armed.Contains(i);
            var skillRect = new Rect(r.x + bw + 4, r.yMax + 34, r.width - bw - 4, 32);
            if (skillReady && !busy) Glow(skillRect.center, 150, new Color(Gold.r, Gold.g, Gold.b, 0.35f + 0.2f * Mathf.Sin(Time.time * 6)));
            // progress fill under a skill that is still charging
            if (!run.SkillReady(i) && !run.Armed.Contains(i))
                Fill(new Rect(skillRect.x + 3, skillRect.yMax - 6, (skillRect.width - 6) * run.Gauge[i] / run.SkillCharge, 3), Gold * new Color(1, 1, 1, 0.8f));
            GUI.enabled = !busy && (run.SkillReady(i) || run.Armed.Contains(i));
            var label = run.Armed.Contains(i) ? "취소" : m.skillName;
            smallButton.fontSize = label.Length > 4 ? 13 : 16;
            if (skillReady) smallButton.normal.textColor = Gold;
            if (GUI.Button(skillRect, new GUIContent(label, $"{m.skillName} ({SkillKeyNames[Mathf.Min(i, 5)]}): {m.skillDesc} · 게이지 {run.Gauge[i]}/{run.SkillCharge}"), smallButton))
                if (run.ToggleSkill(i)) Sfx(run.Armed.Contains(i) ? "skill" : "ui_click", 0.6f);
            smallButton.normal.textColor = Color.white;
            smallButton.fontSize = 16;
            GUI.enabled = true;

        }

        // neighbour links: banner and twin pay off across the gap between two slots
        if (settled)
            for (int i = 0; i < count - 1; i++)
            {
                if (i == run.JammedReel || i + 1 == run.JammedReel) continue;
                SymbolDef a = shownFaces[i], b = shownFaces[i + 1];
                int bonus = (a.twinBonus > 0 && a.id == b.id ? a.twinBonus * 2 : 0)
                    + (b.curse ? 0 : a.neighborBuff) + (a.curse ? 0 : b.neighborBuff);
                if (bonus == 0) continue;
                float gx = startX + (i + 1) * (ReelSize + ReelGap) - ReelGap / 2;
                var linkColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f + 0.4f * Mathf.Sin(Time.time * 6));
                Fill(new Rect(gx - 3, ReelY + 14, 6, ReelSize - 28), linkColor);
                Shadowed(new Rect(gx - 24, ReelY + ReelSize / 2 - 14, 48, 28), $"+{bonus}", small, Gold);
                GUI.Label(new Rect(gx - 15, ReelY, 30, ReelSize), new GUIContent("", a.id == b.id && a.twinBonus > 0
                    ? $"쌍검 둘이 나란히: 공격 +{bonus}" : $"깃발: 옆 슬롯 주 능력치 +{bonus}"));
            }

        DrawTurnReadout(settled);
        if (settled)
        {
            int n = shownFaces.Length;
            var reels = new Rect(startX - 6, ReelY - 36, n * ReelSize + (n - 1) * ReelGap + 12, ReelSize + 44);
            Tip("reels", reels, "동료 한 명이 슬롯 하나예요. 서로 다른 슬롯에서 같은 심볼이 2개 이상 뜨면 콤보로 효과가 커져요!");
            Tip("intent", IntentPlate, "적이 다음에 할 행동이에요. 공격이 크면 방패로 막고, 방어 중이면 회복이나 게이지를 모아요.");
            Tip("reroll", new Rect(startX, ReelY + ReelSize + 34, 42, 32), "↻ 버튼으로 턴마다 슬롯 하나를 다시 돌릴 수 있어요. 공격이나 방패가 더 필요할 때 써 보세요.");
            int ready = Enumerable.Range(0, n).FirstOrDefault(i => run.SkillReady(i) && !run.Armed.Contains(i));
            if (run.SkillReady(ready) && !run.Armed.Contains(ready))
                Tip("skill", new Rect(startX + ready * (ReelSize + ReelGap) + 46, ReelY + ReelSize + 34, ReelSize - 46, 32), "게이지(●●●)가 다 찼어요! 스킬 버튼을 누르면 확정할 때 함께 발동해요.");
            Tip("confirm", TurnButtonRect, "준비가 되면 확정! 동료들이 공격하고, 그다음 적이 행동해요.");
        }
        GUI.enabled = true;
    }

    static readonly Rect TurnButtonRect = new Rect(939, 600, 242, 58);

    void DrawTurnReadout(bool settled)
    {
        Fill(new Rect(80, 574, W - 160, 94), new Color(0.13f, 0.11f, 0.16f));
        Fill(new Rect(80, 574, W - 160, 2), new Color(0.38f, 0.31f, 0.26f));
        Shadowed(new Rect(98, 579, 180, 23), "이번 턴 예상", small, Gold, TextAnchor.MiddleLeft);
        Shadowed(new Rect(920, 577, 260, 24), $"다시 돌리기 {run.RerollsLeft}회", tiny, Color.white, TextAnchor.MiddleRight);
        if (settled)
        {
            var outcome = run.Preview();
            int incoming = Incoming(outcome);
            DrawTurnStat(98, "적 피해", DamageWithPoison(outcome), "0x72/weapon_knight_sword", HitRed);
            DrawTurnStat(260, "방어", outcome.block, "shield", BlockBlue);
            DrawTurnStat(409, "회복", outcome.heal, "0x72/flask_big_red", HealGreen);
            DrawTurnStat(555, "골드", outcome.gold, "0x72/coin_anim_f0", Gold);
            DrawTurnStat(710, "받는 피해", incoming, "0x72/skull", incoming > 0 ? HitRed : Color.white);

            string notes = string.Join("   ", new[] {
                outcome.combo,
                outcome.weaken > 0 ? $"약화 {outcome.weaken}" : "",
                outcome.poison > 0 ? $"독 +{outcome.poison}" : "",
                outcome.selfDamage > 0 ? $"저주 피해 {outcome.selfDamage}" : "",
            }.Where(note => !string.IsNullOrEmpty(note)));
            Shadowed(new Rect(98, 641, 760, 22), notes, tiny, outcome.badCombo ? HitRed : Gold, TextAnchor.MiddleLeft);
        }
        else
        {
            Shadowed(new Rect(98, 610, 720, 38), busy ? "전투 결과 처리 중" : "슬롯이 돌아가는 중", mid, Color.white, TextAnchor.MiddleLeft);
        }

        GUI.enabled = settled;
        if (GUI.Button(TurnButtonRect, new GUIContent("턴 실행", "턴 실행 (Space / Enter)"), turnButton)) { Sfx("ui_click"); StartCoroutine(PlayTurn()); }
        GUI.enabled = true;
        int n = run.Party.Count;
        Shadowed(new Rect(300, 579, 600, 23), $"단축키  Space 턴 실행 · 1~{n} 다시 돌리기 · {SkillKeyNames[0]}~{SkillKeyNames[Mathf.Min(n, 6) - 1]} 스킬", tiny,
            new Color(1, 1, 1, 0.45f));
    }

    void DrawTurnStat(float x, string label, int value, string icon, Color color)
    {
        DrawFit(T(icon), new Rect(x, 607, 25, 26));
        Shadowed(new Rect(x + 33, 605, 114, 31), $"{label} {value}", small, value == 0 ? Color.white : color, TextAnchor.MiddleLeft);
    }

    static void DrawFit(Texture2D texture, Rect bounds)
    {
        if (texture == null) return;
        float scale = Mathf.Min(bounds.width / texture.width, bounds.height / texture.height);
        float width = texture.width * scale, height = texture.height * scale;
        DrawTex(texture, new Rect(bounds.center.x - width / 2, bounds.center.y - height / 2, width, height));
    }

    void DrawMap()
    {
        var rows = run.Map.Rows;
        var current = run.Current;
        var available = new HashSet<MapNode>(run.Available);
        var reached = Ice;
        var quiet = new Color(0.62f, 0.59f, 0.68f);
        var entry = new Vector2(110, 416);
        float Column(int row) => Mathf.Lerp(entry.x, W - 110, (row + 1f) / rows.Count);
        Vector2 Position(MapNode node) => new Vector2(Column(node.Row), 270 + node.Lane * 146);

        Fill(new Rect(0, 46, W, H - 46), Panel);
        DrawPanel(new Rect(36, 64, W - 72, 598));
        // the chapter's own floor under the route, dimmed so the cards stay readable
        var board = new Rect(42, 70, W - 84, 586);
        var old = GUI.color;
        GUI.color = Hex(run.ChapterDef.floorTint) * new Color(0.62f, 0.62f, 0.62f, 1);
        for (float x = board.x; x < board.xMax; x += Tile)
            for (float y = board.y; y < board.yMax; y += Tile)
                GUI.DrawTextureWithTexCoords(new Rect(x, y, Mathf.Min(Tile, board.xMax - x), Mathf.Min(Tile, board.yMax - y)),
                    T($"0x72/floor_{1 + (int)(x * 7 + y * 13) % 8}"),
                    new Rect(0, 0, Mathf.Min(1, (board.xMax - x) / Tile), Mathf.Min(1, (board.yMax - y) / Tile)));
        GUI.color = new Color(0, 0, 0, 0.45f);
        GUI.DrawTexture(board, vignette, ScaleMode.StretchToFill);
        GUI.color = old;
        Fill(new Rect(board.x, board.y, board.width, 108), new Color(0.06f, 0.05f, 0.08f, 0.72f));
        var location = current == null ? "입구" : $"{current.Row + 1} / {rows.Count} · {MapLabel(current.Type)}";
        Shadowed(new Rect(64, 80, 550, 44), $"{run.Chapter + 1}장 · {run.ChapterDef.name}", big, Color.white, TextAnchor.MiddleLeft);
        Shadowed(new Rect(66, 127, 540, 35), $"현재 위치 · {location}", mid, reached, TextAnchor.MiddleLeft);
        Shadowed(new Rect(690, 86, 495, 29), $"파티 HP {shownHp} / {run.MaxHp}", small, HealGreen, TextAnchor.MiddleRight);
        Shadowed(new Rect(690, 127, 495, 35), "금색 칸 중 다음 장소를 고르세요", small, Gold, TextAnchor.MiddleRight);
        Fill(new Rect(42, 177, W - 84, 2), new Color(0.34f, 0.26f, 0.21f));

        for (int row = 0; row < rows.Count; row++)
            Shadowed(new Rect(Column(row) - 54, 185, 108, 27), row == rows.Count - 1 ? "보스" : $"{row + 1}", small,
                row == current?.Row ? reached : quiet);

        var connections = rows.SelectMany(row => row).SelectMany(node => node.Next.Select(next => (from: node, to: next)));
        foreach (var link in connections)
            Dotted(Position(link.from), Position(link.to), new Color(0.64f, 0.59f, 0.71f, 0.32f), 3);
        foreach (var link in connections)
        {
            if (link.from.Visited && link.to.Visited)
                Dotted(Position(link.from), Position(link.to), reached, 5);
            else if (link.from == current && available.Contains(link.to))
                Dotted(Position(link.from), Position(link.to), Gold, 6);
        }
        foreach (var node in rows[0])
        {
            if (current == null || node.Visited)
                Dotted(entry, Position(node), node.Visited ? reached : Gold, 6);
        }

        DrawMapCard(entry, "입구", null, current == null, false, current != null, false, false, Ice);
        if (current == null) Tip("map", new Rect(entry.x - 58, entry.y - 44, 116, 88),
            "파란색이 현재 위치예요. 금색 길을 따라 어디로 갈지 고르세요.");

        foreach (var node in rows.SelectMany(row => row))
        {
            var center = Position(node);
            var hitbox = new Rect(center.x - 58, center.y - 44, 116, 88);
            bool canTravel = available.Contains(node);
            bool hovered = hitbox.Contains(Event.current.mousePosition);
            if (node.Type == NodeType.Boss && !node.Visited)
                Glow(center, 230, new Color(1f, 0.25f, 0.15f, 0.3f + 0.12f * Mathf.Sin(Time.time * 3)));
            DrawMapCard(center, MapLabel(node.Type), NodeIcon(node), node == current, canTravel, node.Visited,
                node.Type == NodeType.Event, hovered && canTravel, NodeAccent(node.Type));
            if (canTravel && GUI.Button(hitbox, new GUIContent("", NodeName(node.Type)), GUIStyle.none) && run.Choose(node))
            {
                Sfx("map_step");
                Sync();
                SpinAll();
                return;
            }
            if (!canTravel && hovered) GUI.Label(hitbox, new GUIContent("", NodeName(node.Type)), GUIStyle.none);
        }
    }

    /// <summary>Each stop type keeps one colour on the map, so a route reads at a glance.</summary>
    static Color NodeAccent(NodeType t)
    {
        switch (t)
        {
            case NodeType.Elite: return new Color(1f, 0.5f, 0.3f);
            case NodeType.Boss: return HitRed;
            case NodeType.Shop: return Gold;
            case NodeType.Rest: return HealGreen;
            case NodeType.Treasure: return new Color(0.85f, 0.6f, 0.3f);
            case NodeType.Recruit: return Ice;
            case NodeType.Event: return new Color(0.75f, 0.5f, 1f);
            default: return new Color(0.75f, 0.72f, 0.8f);
        }
    }

    void DrawMapCard(Vector2 center, string label, Texture2D icon, bool here, bool available, bool visited, bool mystery, bool hovered, Color accent)
    {
        var card = new Rect(center.x - 58, center.y - 44, 116, 88);
        if (!hovered) Fill(new Rect(card.x + 5, card.y + 6, card.width, card.height), new Color(0, 0, 0, 0.45f)); // drop shadow
        else card.y -= 3;
        var border = here ? Ice : available ? Gold : visited ? new Color(0.35f, 0.49f, 0.55f) : new Color(0.33f, 0.30f, 0.37f);
        var surface = here ? new Color(0.14f, 0.25f, 0.31f)
            : hovered ? new Color(0.38f, 0.27f, 0.14f)
            : available ? new Color(0.25f, 0.18f, 0.12f)
            : new Color(0.14f, 0.12f, 0.17f);
        Fill(card, border);
        Fill(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), surface);
        var stripe = accent;
        stripe.a = here || available ? 1f : visited ? 0.35f : 0.6f;
        Fill(new Rect(card.x + 2, card.y + 2, card.width - 4, 5), stripe);

        var color = here ? Ice : available ? Color.white : new Color(0.72f, 0.69f, 0.76f);
        if (icon != null)
        {
            float size = 40f / Mathf.Max(icon.width, icon.height);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, here || available ? 1f : visited ? 0.6f : 0.47f);
            DrawTex(icon, new Rect(center.x - icon.width * size / 2, card.y + 29 - icon.height * size / 2,
                icon.width * size, icon.height * size));
            GUI.color = old;
        }
        else
        {
            Shadowed(new Rect(card.x, card.y + 7, card.width, 46), mystery ? "?" : "▶", mid, color);
        }
        Shadowed(new Rect(card.x, card.y + 51, card.width, 26), label, small, color);

        if (here)
        {
            var marker = new Rect(card.x, card.y - 28, card.width, 26);
            Fill(marker, Ice);
            Shadowed(marker, "현재 위치", small, Panel);
        }
    }

    static string MapLabel(NodeType type)
    {
        var description = NodeName(type);
        int separator = description.IndexOf(':');
        return separator < 0 ? description : description.Substring(0, separator);
    }

    void DrawOffers()
    {
        Tip("reward", new Rect(W / 2 - 280, 404, 560, run.Offers.Count * 72), "보상을 하나 골라요. 마우스를 올리면 자세한 효과가 보여요. 마음에 안 들면 건너뛰어도 돼요.");
        DrawPanel(new Rect(W / 2 - 340, 345, 680, 335));
        Shadowed(new Rect(0, 350, W, 48), run.OfferTitle, big, Gold);
        for (int i = 0; i < run.Offers.Count; i++)
        {
            var offer = run.Offers[i];
            var r = new Rect(W / 2 - 280, 404 + i * 72, 560, 62);
            if (GUI.Button(r, new GUIContent(offer.Text, OfferTip(offer)), button))
            {
                run.ChooseReward(i);
                Sfx(offer.Kind == RewardKind.Gold ? "coin" : "ui_click");
                Sync();
                if (offer.Kind == RewardKind.Recruit) Burst(MemberCenter(run.Party.Count - 1) + new Vector2(-300, 0), Gold, 20, 150, -100, 0.8f, 6);
            }
            OfferIcon(offer, r);
            Shadowed(new Rect(r.xMax - 30, r.y + 2, 26, 18), (i + 1).ToString(), tiny, new Color(1, 1, 1, 0.45f), TextAnchor.UpperRight);
            if (r.Contains(Event.current.mousePosition)) focusReward = offer;
        }
        if (!run.Offers.Contains(focusReward)) focusReward = run.Offers.FirstOrDefault();
        if (focusReward != null) DrawRewardInfo(focusReward, false);
        if (GUI.Button(new Rect(W / 2 - 80, 404 + run.Offers.Count * 72 + 4, 160, 40), "건너뛰기", smallButton))
        {
            run.ChooseReward(-1);
            Sync();
        }
    }

    void DrawShop()
    {
        Tip("shop", new Rect(W / 2 - 370, 396, 740, 50), "골드로 강화·장비·유물·동료를 살 수 있어요. 강화는 모든 슬롯의 그 심볼에 적용돼요.");
        DrawPanel(new Rect(W / 2 - 380, 345, 760, 337));
        Shadowed(new Rect(0, 348, W, 40), "떠돌이 상인", big, Gold);
        int cost = run.ShopRerollCost;
        GUI.enabled = run.Gold >= cost;
        if (GUI.Button(new Rect(W / 2 - 370, 352, 160, 34), new GUIContent($"↻ 갱신 {cost}", "상점 물건을 모두 바꾼다 (할 때마다 비싸짐)"), smallButton))
        {
            if (run.RerollShop()) { Sfx("reel_stop"); Sync(); Burst(new Vector2(W / 2, 520), Gold, 20, 200, 100, 0.5f, 5); }
        }
        GUI.enabled = true;
        if (GUI.Button(new Rect(W / 2 + 210, 352, 160, 34), "떠나기", smallButton))
        {
            run.LeaveShop();
            Sync();
        }

        // two columns of items: button with icon on the left, price on the right
        for (int i = 0; i < run.ShopItems.Count; i++)
        {
            var item = run.ShopItems[i];
            var cell = new Rect(W / 2 - 370 + (i % 2) * 375, 396 + (i / 2) * 56, 365, 50);
            var r = new Rect(cell.x, cell.y, cell.width - 66, cell.height);
            GUI.enabled = !item.Sold && run.Gold >= item.Price;
            if (GUI.Button(r, new GUIContent(item.Sold ? "판매 완료" : item.Text, OfferTip(item)), shopButton))
            {
                if (run.Buy(i)) { Sfx(item.Kind == RewardKind.Upgrade ? "skill" : "coin"); Burst(new Vector2(r.xMax + 30, r.center.y), Gold, 12, 120, 200, 0.5f, 5); }
                Sync();
            }
            GUI.enabled = true;
            if (!item.Sold) OfferIcon(item, new Rect(r.x - 2, r.y, 50, r.height));
            if (!item.Sold && cell.Contains(Event.current.mousePosition)) focusReward = item;
            if (focusReward == item) Fill(new Rect(cell.x - 4, cell.y + 4, 3, cell.height - 8), Gold);
            DrawTex(T("0x72/coin_anim_f0"), new Rect(r.xMax + 8, r.y + 16, 15, 18));
            Shadowed(new Rect(r.xMax + 28, r.y, 40, r.height), item.Price.ToString(), small, run.Gold >= item.Price || item.Sold ? Gold : HitRed, TextAnchor.MiddleLeft);
        }
        if (!run.ShopItems.Contains(focusReward) || focusReward.Sold) focusReward = run.ShopItems.FirstOrDefault(x => !x.Sold);
        if (focusReward != null) DrawRewardInfo(focusReward, true);
    }

    Reward focusReward;

    /// <summary>
    /// The arena's empty right half on reward and shop screens: what the hovered (or first) choice does,
    /// spelled out, with the reel it changes. Hover tooltips alone hid this.
    /// </summary>
    void DrawRewardInfo(Reward r, bool shop)
    {
        var box = new Rect(676, 62, 576, 262);
        DrawPanel(box);
        OfferIcon(r, new Rect(box.x + 16, box.y + 18, 72, 72));
        Shadowed(new Rect(box.x + 100, box.y + 14, box.width - 116, 34), r.Text, small, Gold, TextAnchor.MiddleLeft);
        var lines = new List<(string text, Color color)>();
        string[] strip = null;
        string added = null, removed = null;
        switch (r.Kind)
        {
            case RewardKind.Recruit:
            {
                var c = run.Companion(r.CompanionId);
                lines.Add(($"스킬 {c.skillName}: {c.skillDesc}", Color.white));
                lines.Add(($"대표 심볼 {run.Symbol(c.signature).name}: 뜰 때마다 스킬 게이지 +1", Color.white));
                lines.Add(($"파티 {run.Party.Count} → {run.Party.Count + 1} / {run.MaxParty}명", run.Party.Count < run.MaxParty ? HealGreen : HitRed));
                strip = c.reel;
                added = r.SymbolId;
                break;
            }
            case RewardKind.Equip:
            case RewardKind.Relic:
            {
                var item = run.Item(r.ItemId);
                lines.Add((item.desc, Color.white));
                if (r.Kind == RewardKind.Relic) lines.Add(("유물: 파티 전체에 항상 적용", BlockBlue));
                else
                {
                    lines.Add(($"장비: {run.Member(r.Reel).name} 전용 한 칸", BlockBlue));
                    var old = run.Equipment[r.Reel];
                    if (old != null) lines.Add(($"지금 장비 {run.Item(old).name}은(는) 바뀐다", HitRed));
                    strip = run.Reels[r.Reel].ToArray();
                }
                break;
            }
            case RewardKind.AddSymbol:
                lines.Add((SymbolDetails(run.Symbol(r.SymbolId)), Color.white));
                lines.Add(($"{run.Member(r.Reel).name} 슬롯 {run.Reels[r.Reel].Count}칸 → {run.Reels[r.Reel].Count + 1}칸", Color.white));
                strip = run.Reels[r.Reel].ToArray();
                added = r.SymbolId;
                break;
            case RewardKind.RemoveSymbol:
                lines.Add((SymbolDetails(run.Symbol(r.SymbolId)), Color.white));
                lines.Add(($"{run.Member(r.Reel).name} 슬롯 {run.Reels[r.Reel].Count}칸 → {run.Reels[r.Reel].Count - 1}칸: 남은 심볼이 더 자주 뜬다", HealGreen));
                strip = run.Reels[r.Reel].ToArray();
                removed = r.SymbolId;
                break;
            case RewardKind.Upgrade:
            {
                var sym = run.Symbol(r.SymbolId);
                lines.Add((SymbolDetails(sym), Color.white));
                var owners = Enumerable.Range(0, run.Party.Count).Where(i => run.Reels[i].Contains(r.SymbolId)).Select(i => run.Member(i).name);
                lines.Add(($"모든 슬롯의 {sym.name}이(가) 함께 강해진다: {string.Join(", ", owners)}", HealGreen));
                break;
            }
            case RewardKind.Heal:
                lines.Add(($"HP {run.Hp} → {Mathf.Min(run.MaxHp, run.Hp + r.Amount)} / {run.MaxHp}", HealGreen));
                break;
            case RewardKind.Gold:
                lines.Add(($"골드 {run.Gold} → {run.Gold + r.Amount}", Gold));
                break;
        }
        if (shop && !r.Sold)
            lines.Add(run.Gold >= r.Price ? ($"가격 {r.Price} · 사면 {run.Gold - r.Price} 골드 남음", Gold) : ($"가격 {r.Price} · {r.Price - run.Gold} 골드 부족", HitRed));

        for (int k = 0; k < lines.Count; k++)
            Shadowed(new Rect(box.x + 100, box.y + 54 + k * 28, box.width - 116, 26), lines[k].text, tiny, lines[k].color, TextAnchor.MiddleLeft);

        if (strip == null) return;
        var faces = strip.ToList();
        if (added != null) faces.Add(added);
        float size = Mathf.Min(34, (box.width - 40) / faces.Count - 4);
        float x0 = box.x + 20, y0 = box.yMax - size - 22;
        Shadowed(new Rect(x0, y0 - 24, 300, 20), r.Kind == RewardKind.Recruit ? "가져오는 슬롯" : "바뀌는 슬롯", tiny, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
        bool crossed = false;
        for (int k = 0; k < faces.Count; k++)
        {
            var cell = new Rect(x0 + k * (size + 4), y0, size, size);
            bool isNew = added != null && k == faces.Count - 1;
            bool isGone = !crossed && removed != null && faces[k] == removed;
            Fill(cell, isNew ? Gold * new Color(1, 1, 1, 0.5f) : isGone ? HitRed * new Color(1, 1, 1, 0.4f) : new Color(1, 1, 1, 0.08f));
            Icon(run.Symbol(faces[k]), cell, 1, isGone ? 0.4f : 1);
            if (isGone) { Bar(cell.center, size * 1.2f, 3, 45, HitRed); crossed = true; }
        }
    }

    /// <summary>Ask for a guide bubble this frame; the first unseen request wins and is drawn on top.</summary>
    void Tip(string key, Rect target, string text)
    {
        if (tipKey != null || seenTips.Contains(key) || busy || talk.Count > 0 || paused || onTitle) return;
        tipKey = key;
        tipText = text;
        tipTarget = target;
    }

    void DrawTip()
    {
        if (tipKey == null) return;
        // pulsing outline around what the tip is about
        var c = new Color(Gold.r, Gold.g, Gold.b, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6));
        var t = tipTarget;
        Fill(new Rect(t.x - 4, t.y - 4, t.width + 8, 3), c);
        Fill(new Rect(t.x - 4, t.yMax + 1, t.width + 8, 3), c);
        Fill(new Rect(t.x - 4, t.y - 4, 3, t.height + 8), c);
        Fill(new Rect(t.xMax + 1, t.y - 4, 3, t.height + 8), c);
        // bubble above the target if there is room, else below
        var size = new Vector2(420, 118);
        float x = Mathf.Clamp(t.center.x - size.x / 2, 10, W - size.x - 10);
        float y = t.y - size.y - 14 > 40 ? t.y - size.y - 14 : Mathf.Min(t.yMax + 14, H - size.y - 10);
        var box = new Rect(x, y, size.x, size.y);
        DrawPanel(box);
        story_.fontSize = 17;
        story_.normal.textColor = Color.white;
        GUI.Label(new Rect(box.x + 16, box.y + 12, box.width - 32, 64), tipText, story_);
        story_.fontSize = 22;
        if (GUI.Button(new Rect(box.xMax - 120, box.yMax - 42, 106, 32), "알겠어요", smallButton))
        {
            seenTips.Add(tipKey);
            PlayerPrefs.SetString("tips", string.Join(",", seenTips));
            PlayerPrefs.Save();
            Sfx("ui_click");
        }
    }

    void SetPaused(bool on)
    {
        paused = on;
        showSettings = false;
        Time.timeScale = on ? 0 : 1;
        Sfx("ui_click");
    }

    void DrawPause()
    {
        Fill(new Rect(-20, -20, W + 40, H + 40), new Color(0, 0, 0, 0.6f));
        var box = new Rect(W / 2 - 220, 150, 440, 400);
        DrawPanel(box);
        if (showSettings) { DrawSettings(box); return; }
        Shadowed(new Rect(box.x, box.y + 20, box.width, 50), "일시정지", big, Gold);
        float y = box.y + 100;
        if (GUI.Button(new Rect(box.x + 70, y, 300, 52), "계속하기", button)) SetPaused(false);
        if (GUI.Button(new Rect(box.x + 70, y + 66, 300, 52), "설정", button)) showSettings = true;
        if (GUI.Button(new Rect(box.x + 70, y + 132, 300, 52), "저장하고 타이틀로", button)) { SaveRun(); SetPaused(false); onTitle = true; NewRun(); }
        if (GUI.Button(new Rect(box.x + 70, y + 198, 300, 52), "저장하고 종료", button)) { SaveRun(); Application.Quit(); }
    }

    void DrawSettings(Rect box)
    {
        Shadowed(new Rect(box.x, box.y + 20, box.width, 50), "설정", big, Gold);
        float x = box.x + 40, y = box.y + 96;
        Shadowed(new Rect(x, y, 160, 30), "음악", mid, Color.white, TextAnchor.MiddleLeft);
        musicVol = GUI.HorizontalSlider(new Rect(x + 150, y + 10, 200, 20), musicVol, 0, 1);
        Shadowed(new Rect(x, y + 50, 160, 30), "효과음", mid, Color.white, TextAnchor.MiddleLeft);
        float before = sfxVol;
        sfxVol = GUI.HorizontalSlider(new Rect(x + 150, y + 60, 200, 20), sfxVol, 0, 1);
        if (Mathf.Abs(before - sfxVol) > 0.05f) Sfx("coin", 0.6f); // preview while dragging
        music.volume = 0.3f * musicVol;
        if (GUI.Button(new Rect(x, y + 104, 360, 44), Screen.fullScreen ? "전체화면: 켬" : "전체화면: 끔", smallButton))
            Screen.fullScreen = !Screen.fullScreen;
        if (GUI.Button(new Rect(x, y + 156, 360, 44), shakeOn ? "화면 흔들림·번쩍임: 켬" : "화면 흔들림·번쩍임: 끔", smallButton))
            shakeOn = !shakeOn;
        if (GUI.Button(new Rect(x, y + 208, 170, 40), "안내 다시 보기", smallButton))
        {
            seenTips.Clear();
            PlayerPrefs.SetString("tips", "");
        }
        if (GUI.Button(new Rect(x + 190, y + 208, 170, 40), "돌아가기", smallButton))
        {
            PlayerPrefs.SetFloat("musicVol", musicVol);
            PlayerPrefs.SetFloat("sfxVol", sfxVol);
            PlayerPrefs.SetInt("shake", shakeOn ? 1 : 0);
            PlayerPrefs.Save();
            if (onTitle) { paused = false; showSettings = false; } else showSettings = false;
        }
    }

    // ---------- codex (도감): companions and items open with unlocks, enemies once met ----------

    bool showCodex;
    int codexTab;
    object codexPick;
    HashSet<string> seenEnemies;

    HashSet<string> SeenEnemies => seenEnemies ??= new HashSet<string>(PlayerPrefs.GetString("seenEnemies", "").Split(',').Where(x => x != ""));

    void MarkSeen(string enemy)
    {
        if (!SavingEnabled || !SeenEnemies.Add(enemy)) return;
        PlayerPrefs.SetString("seenEnemies", string.Join(",", SeenEnemies));
        PlayerPrefs.Save();
    }

    static string MoveText(string move)
    {
        var p = move.Split(':');
        string v = p.Length > 1 ? p[1] : "";
        switch (p[0])
        {
            case "attack": return $"공격 {v}";
            case "guard": return $"방어 {v}";
            case "curse": return $"저주 {v}";
            case "enrage": return $"분노 +{v}";
            case "drain": return $"흡혈 {v}";
            case "steal": return $"강탈 {v}";
            case "seal": return $"봉인 {v}";
            case "heal": return $"재생 {v}";
            case "jam": return $"슬롯 고장 {v}";
            case "sap": return $"마력 흡수 {v}";
            case "reflect": return $"가시 갑피 {v}%";
            default: return move;
        }
    }

    void DrawCodex()
    {
        Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.04f, 0.07f, 0.93f));
        Shadowed(new Rect(48, 18, 400, 50), "도감", big, Gold, TextAnchor.MiddleLeft);
        if (GUI.Button(new Rect(W - 168, 22, 120, 40), new GUIContent("닫기", "닫기 (Esc)"), smallButton)) showCodex = false;

        var locked = new HashSet<string>(Run.LockedAfter(cfg, UnlocksOwned));
        var enemies = cfg.chapters.SelectMany(c => c.enemies.Concat(c.elites).Append(c.boss)).Append(cfg.mimic).ToList();
        var relics = cfg.items.OrderBy(i => i.slot == "relic" ? 0 : 1).ToList();
        int openMates = cfg.companions.Count(c => !locked.Contains(c.id));
        int openItems = relics.Count(i => !locked.Contains(i.id));
        int seen = enemies.Count(en => SeenEnemies.Contains(en.name));
        string[] tabs = { $"동료 {openMates}/{cfg.companions.Length}", $"유물·장비 {openItems}/{relics.Count}", $"적 {seen}/{enemies.Count}" };
        for (int t = 0; t < tabs.Length; t++)
        {
            var tab = new Rect(48 + t * 200, 80, 190, 40);
            if (t == codexTab) Fill(new Rect(tab.x - 3, tab.y - 3, tab.width + 6, tab.height + 6), Gold);
            if (GUI.Button(tab, tabs[t], smallButton)) { codexTab = t; codexPick = null; Sfx("ui_click"); }
        }

        int total = codexTab == 0 ? cfg.companions.Length : codexTab == 1 ? relics.Count : enemies.Count;
        const int cols = 11;
        const float cell = 98, gap = 8;
        var mouse = Event.current.mousePosition;
        for (int k = 0; k < total; k++)
        {
            var r = new Rect(48 + (k % cols) * (cell + gap), 140 + (k / cols) * (cell + 24 + gap), cell, cell);
            object entry = codexTab == 0 ? cfg.companions[k] : codexTab == 1 ? (object)relics[k] : enemies[k];
            string id = entry is CompanionDef c0 ? c0.id : entry is ItemDef i0 ? i0.id : ((EnemyDef)entry).name;
            bool open = codexTab == 2 ? SeenEnemies.Contains(id) : !locked.Contains(id);
            bool picked = codexPick == entry;
            Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), picked ? Gold : open ? new Color(0.45f, 0.36f, 0.26f) : new Color(0.22f, 0.2f, 0.26f));
            Fill(r, r.Contains(mouse) ? new Color(0.22f, 0.18f, 0.24f) : new Color(0.12f, 0.1f, 0.14f));
            var old = GUI.color;
            GUI.color = open ? Color.white : new Color(0, 0, 0, 0.85f); // locked: a black silhouette
            var inner = new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20);
            if (entry is CompanionDef cd) DrawFit(Anim($"{cd.sprite}_idle_anim"), inner);
            else if (entry is ItemDef idf) ItemIcon(idf, inner);
            else { var en = (EnemyDef)entry; GUI.color = open ? (string.IsNullOrEmpty(en.tint) ? Color.white : Hex(en.tint)) : GUI.color; DrawFit(Anim(en.sprite), inner); }
            GUI.color = old;
            if (!open) Shadowed(new Rect(r.x, r.y, r.width, r.height), "?", big, new Color(1, 1, 1, 0.35f));
            string name = entry is CompanionDef c1 ? c1.name : entry is ItemDef i1 ? i1.name : ((EnemyDef)entry).name;
            tiny.fontSize = 13;
            Shadowed(new Rect(r.x - 4, r.yMax + 2, r.width + 8, 20), open ? name : "???", tiny, open ? Color.white : new Color(1, 1, 1, 0.4f));
            tiny.fontSize = 15;
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { codexPick = entry; Sfx("ui_click", 0.5f); }
        }

        // detail strip for the picked entry
        var box = new Rect(48, 560, W - 96, 136);
        DrawPanel(box);
        if (codexPick == null)
        {
            Shadowed(box, "칸을 누르면 자세히 볼 수 있어요. 동료·유물은 판을 끝낼 때마다 열리고, 적은 한 번 만나면 기록돼요.", small, new Color(1, 1, 1, 0.6f));
            return;
        }
        string pid = codexPick is CompanionDef pc ? pc.id : codexPick is ItemDef pi ? pi.id : ((EnemyDef)codexPick).name;
        bool pOpen = codexPick is EnemyDef ? SeenEnemies.Contains(pid) : !locked.Contains(pid);
        var lines = new List<string>();
        string title;
        if (!pOpen)
        {
            title = "???";
            lines.Add(codexPick is EnemyDef ? "아직 만나지 못한 적" : "아직 잠겨 있다 · 판을 끝내면 순서대로 열린다");
        }
        else if (codexPick is CompanionDef c)
        {
            title = c.name;
            lines.Add($"스킬 {c.skillName}: {c.skillDesc}");
            lines.Add($"대표 심볼 {cfg.symbols.First(x => x.id == c.signature).name} · 슬롯: " + string.Join(" ", c.reel.Select(id => cfg.symbols.First(x => x.id == id).name)));
            var join = story.recruits?.FirstOrDefault(l => l.id == c.id);
            if (join != null) lines.Add($"\"{join.text}\"");
        }
        else if (codexPick is ItemDef it)
        {
            title = it.name + (it.slot == "relic" ? " · 유물" : " · 장비");
            lines.Add(it.desc);
        }
        else
        {
            var en = (EnemyDef)codexPick;
            title = en.name;
            lines.Add($"체력 {en.hp} · 처치 골드 {en.gold}");
            lines.Add("행동 순서: " + string.Join(" → ", en.moves.Select(MoveText)));
            if (en.phase2 != null && en.phase2.Length > 0)
                lines.Add($"절반 아래 {(string.IsNullOrEmpty(en.phase2Name) ? "2단계" : en.phase2Name)}: " + string.Join(" → ", en.phase2.Select(MoveText)));
        }
        Shadowed(new Rect(box.x + 24, box.y + 10, box.width - 48, 32), title, mid, Gold, TextAnchor.MiddleLeft);
        for (int k = 0; k < lines.Count; k++)
            Shadowed(new Rect(box.x + 24, box.y + 46 + k * 28, box.width - 48, 26), lines[k], tiny, Color.white, TextAnchor.MiddleLeft);
    }

    void DrawTitle()
    {
        Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.35f));
        float bob = Mathf.Sin(Time.time * 2) * 6;
        Glow(new Vector2(W / 2, 220 + bob), 700, new Color(1f, 0.75f, 0.3f, 0.25f));
        pop.fontSize = 96;
        Shadowed(new Rect(0, 150 + bob, W, 130), "슬롯 던전", pop, Gold);
        Shadowed(new Rect(0, 270 + bob, W, 40), "동료 한 명이 슬롯 하나 · 운명의 슬롯을 되찾아라", mid, Color.white);
        // three spinning mini reels as a logo flourish
        var faces = cfg.symbols.Where(x => !x.curse).ToArray();
        for (int k = 0; k < 3; k++)
        {
            var r = new Rect(W / 2 - 165 + k * 115, 340, 100, 100);
            Fill(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Gold);
            Fill(r, new Color(0.12f, 0.1f, 0.14f));
            var sym = faces[((int)(Time.time * (3 + k)) + k * 2) % faces.Length];
            Fill(r, Hex(sym.color) * new Color(1, 1, 1, 0.35f));
            Icon(sym, r, 1, 1);
        }
        // difficulty picker, once the first clear unlocks it
        if (AscensionUnlocked > 0)
        {
            var row = new Rect(W / 2 - 200, 446, 400, 24);
            if (GUI.Button(new Rect(row.x, row.y - 2, 28, 28), "◀", smallButton) && ascensionChoice > 0) { ascensionChoice--; NewRun(); }
            if (GUI.Button(new Rect(row.xMax - 28, row.y - 2, 28, 28), "▶", smallButton) && ascensionChoice < AscensionUnlocked) { ascensionChoice++; NewRun(); }
            string[] what = { "보통", "적 체력 +10%", "+ 휴식 회복 절반", "+ 상점 가격 +20%", "+ 적 공격 +1", "+ 시작 골드 0, 보스 체력 +15%" };
            Shadowed(row, ascensionChoice == 0 ? "어려움: 보통" : $"어려움 {ascensionChoice}: {what[ascensionChoice]}", small, ascensionChoice == 0 ? Color.white : HitRed);
        }
        bool canContinue = HasSave;
        if (canContinue && GUI.Button(new Rect(W / 2 - 130, 470, 260, 56), "이어하기", turnButton) && ContinueRun())
        {
            onTitle = false;
            Sfx("combo");
            SaveRun();
        }
        if (GUI.Button(new Rect(W / 2 - 130, canContinue ? 532 : 480, 260, canContinue ? 44 : 64), canContinue ? "새 모험 (저장된 판 지움)" : "모험 시작", canContinue ? smallButton : turnButton))
        {
            NewRun();
            onTitle = false;
            lineStart = Time.time;
            Sfx("combo");
            SaveRun();
        }
        if (saveError != null) Shadowed(new Rect(0, 448, W, 26), saveError, small, HitRed);
        if (GUI.Button(new Rect(W / 2 - 130, 586, 82, 44), "설정", smallButton)) { showSettings = true; paused = true; }
        if (GUI.Button(new Rect(W / 2 - 41, 586, 82, 44), "도감", smallButton)) { showCodex = true; codexPick = null; Sfx("ui_click"); }
        if (GUI.Button(new Rect(W / 2 + 48, 586, 82, 44), "종료", smallButton)) Application.Quit();
        int total = cfg.unlocks?.Length ?? 0;
        if (total > 0)
        {
            int open = Mathf.Min(total, UnlocksOwned);
            var next = Run.LockedAfter(cfg, open).FirstOrDefault();
            Shadowed(new Rect(W - 420, 56, 400, 24), $"열린 동료·유물 {open} / {total}", small, open == total ? Gold : Color.white, TextAnchor.MiddleRight);
            if (next != null) Shadowed(new Rect(W - 420, 80, 400, 22), $"다음: {UnlockName(next)} (판을 끝내면 열림)", tiny, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);
        }
        Shadowed(new Rect(0, 640, W, 30), $"v{Application.version} · 아트: 0x72 DungeonTileset II (CC0) · 글꼴: 갈무리 (OFL) · 소리: 직접 합성", tiny, new Color(1, 1, 1, 0.5f));
    }

    void DrawEnd()
    {
        bool won = run.Phase == Phase.Won;
        DrawPanel(new Rect(W / 2 - 360, 345, 720, 335));
        Shadowed(new Rect(0, 352, W, 60), won ? "던전 정복!" : "쓰러졌다...", big, won ? Gold : HitRed);
        string reached = won ? "운명의 슬롯을 되찾았다" : $"{run.Chapter + 1}장 {run.ChapterDef.name}에서 쓰러짐";
        Shadowed(new Rect(0, 406, W, 30), reached, mid, Color.white);
        if (newUnlocks.Count > 0)
            Shadowed(new Rect(0, 436, W, 26), "새로 열림: " + string.Join(" · ", newUnlocks.Select(UnlockName)) + "  (다음 판부터 영입·보상에 나온다)", small, Gold);
        var stats = new[]
        {
            ("처치", run.Kills.ToString()), ("턴", run.Turns.ToString()), ("최고 한 방", run.BestHit.ToString()),
            ("모은 골드", run.GoldEarned.ToString()), ("동료", $"{run.Party.Count}명"), ("유물", $"{run.Relics.Count}개"),
        };
        for (int k = 0; k < stats.Length; k++)
        {
            var cell = new Rect(W / 2 - 330 + (k % 3) * 220, 466 + (k / 3) * 62, 210, 56);
            Fill(cell, new Color(1, 1, 1, 0.06f));
            Shadowed(new Rect(cell.x, cell.y + 2, cell.width, 22), stats[k].Item1, tiny, new Color(0.8f, 0.8f, 0.85f));
            Shadowed(new Rect(cell.x, cell.y + 22, cell.width, 32), stats[k].Item2, mid, Gold);
        }
        if (GUI.Button(new Rect(W / 2 - 270, 600, 260, 56), "다시 도전", button)) { NewRun(); Sfx("ui_click"); }
        if (GUI.Button(new Rect(W / 2 + 10, 600, 260, 56), "타이틀로", button)) { NewRun(); onTitle = true; Sfx("ui_click"); }
    }

    void DrawPopups()
    {
        popups.RemoveAll(p => Time.time - p.at > 1f);
        foreach (var p in popups)
        {
            float dt = Time.time - p.at;
            pop.fontSize = (int)(p.size * (dt < 0.1f ? 1.4f - 4 * dt : 1));
            var c = p.color;
            c.a = 1 - Mathf.Clamp01((dt - 0.55f) / 0.45f);
            Shadowed(new Rect(p.pos.x - 300, p.pos.y - 70 - dt * 70, 600, 90), p.text, pop, c);
        }
    }

    void DrawParticles()
    {
        particles.RemoveAll(p => Time.time - p.born > p.life);
        foreach (var p in particles)
        {
            float t = Time.time - p.born, k = t / p.life;
            var pos = p.pos + p.vel * t + new Vector2(0, 0.5f * p.gravity * t * t);
            float s = p.size * (1 - k * 0.6f);
            var c = p.color;
            c.a = 1 - k;
            Fill(new Rect(pos.x - s / 2, pos.y - s / 2, s, s), c);
        }
    }

    void DrawShots()
    {
        shots.RemoveAll(s => Time.time - s.start > s.dur);
        foreach (var s in shots)
        {
            float k = Mathf.Clamp01((Time.time - s.start) / s.dur);
            var pos = Vector2.Lerp(s.from, s.to, k) + new Vector2(0, -Mathf.Sin(k * Mathf.PI) * s.arc);
            if (s.glowing)
            {
                Glow(pos, 70 * s.scale, s.color);
                Glow(pos, 30 * s.scale, Color.white);
                if (Random.value < 0.6f) Burst(pos, s.color, 1, 30, 0, 0.3f, 6);
                continue;
            }
            float w = s.tex.width * s.scale, h = s.tex.height * s.scale;
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(s.angle + s.spin * k, pos);
            var old = GUI.color;
            GUI.color = s.color;
            GUI.DrawTexture(new Rect(pos.x - w / 2, pos.y - h / 2, w, h), s.tex);
            GUI.color = old;
            GUI.matrix = saved;
        }
    }

    /// <summary>Shape effects. Beams and ice draw over the UI layer (front), the rest behind it.</summary>
    void DrawFx(bool front)
    {
        fxs.RemoveAll(f => Time.time - f.start > f.dur);
        foreach (var f in fxs)
        {
            bool isFront = f.kind == "beam" || f.kind == "ice";
            if (isFront != front) continue;
            float k = Mathf.Clamp01((Time.time - f.start) / f.dur);
            var c = f.color;
            switch (f.kind)
            {
                case "ring": // expanding glow ring
                    c.a = (1 - k) * 0.9f;
                    Glow(f.pos, f.size * (0.3f + k), c);
                    break;
                case "slash": // two crossing blade streaks
                    c.a = 1 - k;
                    Bar(f.pos, f.size * (0.4f + k), 10 * (1 - k) + 2, -35, c);
                    Bar(f.pos, f.size * (0.3f + k * 0.8f), 6 * (1 - k) + 2, 30, c);
                    break;
                case "claw": // three red rakes
                    c.a = 1 - k;
                    for (int j = -1; j <= 1; j++) Bar(f.pos + new Vector2(j * 22, j * 6), f.size * Mathf.Min(1, k * 4), 6, 60, c);
                    break;
                case "beam": // a column of light from the top of the screen
                    c.a = Mathf.Sin(k * Mathf.PI) * 0.8f;
                    var old = GUI.color;
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(f.pos.x - f.size / 2, 0, f.size, f.pos.y + 40), glow, ScaleMode.StretchToFill);
                    GUI.color = new Color(1, 1, 1, c.a);
                    GUI.DrawTexture(new Rect(f.pos.x - f.size / 6, 0, f.size / 3, f.pos.y + 20), glow, ScaleMode.StretchToFill);
                    GUI.color = old;
                    break;
                case "ice": // crystal shards around a target
                    c.a = Mathf.Min(1, k * 8) * (1 - Mathf.Clamp01((k - 0.8f) * 5));
                    Bar(f.pos, f.size, f.size * 0.35f, 45 + f.size, c);
                    Bar(f.pos, f.size * 0.6f, f.size * 0.18f, 45 + f.size, new Color(1, 1, 1, c.a));
                    break;
            }
        }
    }

    // ---------- helpers ----------

    float MemberX(int i) => FrontX - i * Mathf.Min(MemberGap, 400f / Mathf.Max(1, run.Party.Count - 1));
    Vector2 MemberFeet(int i) => new Vector2(MemberX(i), FloorY - (i % 2) * 14);
    Vector2 MemberCenter(int i) => MemberFeet(i) + new Vector2(0, -70);
    Vector2 MemberHead(int i) => new Vector2(MemberX(i), FloorY - 28 * Px - 16);
    EnemyDef phaseShownFor;

    /// <summary>The name an enemy skill pops up with when used; null for plain attacks, guard and enrage.</summary>
    static string EnemySkillName(string kind)
    {
        switch (kind)
        {
            case "heal": return "재생";
            case "jam": return "슬롯 고장";
            case "sap": return "마력 흡수";
            case "reflect": return "가시 갑피";
            case "seal": return "봉인";
            case "steal": return "강탈";
            case "curse": return "저주";
            case "drain": return "흡혈";
            default: return null;
        }
    }

    Vector2 EnemyHead() => new Vector2(EnemyX, FloorY - Anim(shownEnemy.sprite).height * Px * EnemyScale - 16);
    Vector2 EnemyBody() => new Vector2(EnemyX, FloorY - Anim(shownEnemy.sprite).height * Px * EnemyScale / 2);
    bool IsElite(EnemyDef e) => e != null && cfg.chapters.Any(c => c.elites.Contains(e));
    /// <summary>Elites stand a size bigger than ordinary enemies; bosses already use big sprites.</summary>
    float EnemyScale => IsElite(shownEnemy) ? 1.25f : 1f;

    Texture2D NodeIcon(MapNode n)
    {
        switch (n.Type)
        {
            case NodeType.Elite: return Anim(run.ChapterDef.elites[0].sprite);
            case NodeType.Recruit: return Anim("elf_m_idle_anim");
            case NodeType.Shop: return T("0x72/coin_anim_f0");
            case NodeType.Rest: return T("0x72/flask_big_green");
            case NodeType.Treasure: return T("0x72/chest_full_open_anim_f0");
            case NodeType.Boss: return Anim(run.ChapterDef.boss.sprite);
            case NodeType.Event: return null; // drawn as "?"
            default: return T("0x72/weapon_regular_sword");
        }
    }

    static string NodeName(NodeType t)
    {
        switch (t)
        {
            case NodeType.Elite: return "정예: 강한 적, 골드를 더 준다";
            case NodeType.Recruit: return "영입: 동료가 될 모험가";
            case NodeType.Shop: return "상점: 골드로 심볼·회복·동료";
            case NodeType.Rest: return "휴식: 회복하거나 해골을 버린다";
            case NodeType.Treasure: return "보물상자: 골드와 심볼… 가끔 미믹";
            case NodeType.Boss: return "보스";
            case NodeType.Event: return "이벤트: 무슨 일이 생길지 모른다";
            default: return "전투";
        }
    }

    string OfferTip(Reward r)
    {
        if (r.CompanionId != null) { var c = run.Companion(r.CompanionId); return $"{c.skillName}: {c.skillDesc}"; }
        if (r.ItemId != null) { var item = run.Item(r.ItemId); return $"{item.name}: {item.desc}"; }
        if (r.SymbolId != null)
        {
            return SymbolDetails(run.Symbol(r.SymbolId));
        }
        return "";
    }

    static string SymbolDetails(SymbolDef symbol) =>
        $"{symbol.name}: 공격 {symbol.damage} 방어 {symbol.block} 회복 {symbol.heal}"
        + (symbol.gold > 0 ? $" 골드 {symbol.gold}" : "")
        + (symbol.weaken > 0 ? $" 약화 {symbol.weaken}" : "")
        + (symbol.poison > 0 ? $" 독 +{symbol.poison}" : "")
        + (symbol.selfDamage > 0 ? $" 저주 피해 {symbol.selfDamage}" : "");

    void OfferIcon(Reward r, Rect row)
    {
        if (r.SymbolId != null && r.CompanionId == null) Icon(run.Symbol(r.SymbolId), new Rect(row.x + 8, row.y + 6, row.height - 12, row.height - 12), 1, 1);
        if (r.CompanionId != null)
        {
            var portrait = Anim($"{run.Companion(r.CompanionId).sprite}_idle_anim");
            float s = Mathf.Floor((row.height - 6) / portrait.height);
            DrawTex(portrait, new Rect(row.x + 14, row.yMax - portrait.height * s - 3, portrait.width * s, portrait.height * s));
        }
        if (r.Kind == RewardKind.Heal) DrawTex(T("0x72/flask_big_red"), new Rect(row.x + 12, row.y + 8, row.height - 16, row.height - 16));
        if (r.Kind == RewardKind.Gold) DrawTex(T("0x72/coin_anim_f0"), new Rect(row.x + 16, row.y + 12, 24, 28));
        if (r.ItemId != null) ItemIcon(run.Item(r.ItemId), new Rect(row.x + 8, row.y + 6, row.height - 12, row.height - 12));
    }

    void ItemIcon(ItemDef item, Rect area)
    {
        var t = T(item.icon);
        if (t == null) return;
        float s = Mathf.Max(1, Mathf.Floor(Mathf.Min(area.width * 0.85f / t.width, area.height * 0.85f / t.height)));
        if (t.height * s > area.height) s = area.height * 0.85f / t.height;
        GUI.DrawTexture(new Rect(area.center.x - t.width * s / 2, area.center.y - t.height * s / 2, t.width * s, t.height * s), t);
    }

    /// <summary>Loads Resources/Art/&lt;path&gt;; null when the frame doesn't exist (e.g. a hit frame).</summary>
    Texture2D T(string path)
    {
        if (!tex.TryGetValue(path, out var t)) tex[path] = t = Resources.Load<Texture2D>("Art/" + path);
        return t;
    }

    /// <summary>Current frame of an animation named base_f0, base_f1, … (any frame count).</summary>
    Texture2D Anim(string baseName)
    {
        if (!frameCounts.TryGetValue(baseName, out int n))
        {
            n = 0;
            while (T($"0x72/{baseName}_f{n}") != null) n++;
            frameCounts[baseName] = n = Mathf.Max(1, n);
        }
        return T($"0x72/{baseName}_f{(int)(Time.time * 8) % n}") ?? Texture2D.whiteTexture;
    }

    static float Wobble(float at, float amount)
    {
        float dt = Time.time - at;
        return dt < 0.3f ? Mathf.Sin(dt * 90) * amount * (1 - dt / 0.3f) : 0;
    }

    void DrawChar(Texture2D t, float centerX, float feetY, bool flip, Color tint, float angle, float scale)
    {
        float w = t.width * Px * scale, h = t.height * Px * scale;
        var r = new Rect(centerX - w / 2, feetY - h, w, h);
        var saved = GUI.matrix;
        if (angle != 0) GUIUtility.RotateAroundPivot(angle, new Vector2(centerX, feetY - h / 2));
        Fill(new Rect(centerX - w * 0.4f, feetY - 6, w * 0.8f, 10), new Color(0, 0, 0, 0.35f * tint.a));
        var old = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(r, t, flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
        GUI.color = old;
        GUI.matrix = saved;
    }

    void Icon(SymbolDef s, Rect area, float scale, float alpha)
    {
        var t = T(s.icon);
        float fit = Mathf.Max(1, Mathf.Floor(Mathf.Min(area.width * 0.75f / t.width, area.height * 0.75f / t.height))) * scale;
        float w = t.width * fit, h = t.height * fit;
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(area.center.x - w / 2, area.center.y - h / 2, w, h), t);
        GUI.color = old;
    }

    void Bar(float centerX, float bottom, int value, int max, Color c, float width)
    {
        var r = new Rect(centerX - width / 2, bottom - 14, width, 14);
        Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Color.black);
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01((float)value / max), r.height), c);
        Shadowed(new Rect(r.x, r.y - 2, r.width, r.height + 4), $"{value} / {max}", tiny, Color.white);
    }

    /// <summary>A rotated filled bar centred on <paramref name="center"/>.</summary>
    static void Bar(Vector2 center, float length, float thickness, float angle, Color c)
    {
        var saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, center);
        Fill(new Rect(center.x - length / 2, center.y - thickness / 2, length, thickness), c);
        GUI.matrix = saved;
    }

    void Glow(Vector2 center, float size, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), glow);
        GUI.color = old;
    }

    static void Dotted(Vector2 a, Vector2 b, Color c, float dot)
    {
        int n = (int)(Vector2.Distance(a, b) / 14);
        for (int k = 1; k < n; k++)
        {
            var p = Vector2.Lerp(a, b, (float)k / n);
            Fill(new Rect(p.x - dot / 2, p.y - dot / 2, dot, dot), c);
        }
    }

    static void DrawTex(Texture2D t, Rect r)
    {
        if (t != null) GUI.DrawTexture(r, t);
    }

    void DrawPanel(Rect r) => GUI.Box(r, GUIContent.none, panel);

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Shadowed(Rect r, string text, GUIStyle style, Color c, TextAnchor align = TextAnchor.MiddleCenter)
    {
        style.alignment = align;
        var old = style.normal.textColor;
        style.normal.textColor = new Color(0, 0, 0, c.a * 0.8f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        style.normal.textColor = c;
        GUI.Label(r, text, style);
        style.normal.textColor = old;
    }

    void InitStyles()
    {
        if (big != null) return;
        // Galmuri pixel fonts ship with the game (OFL, Resources/Fonts); the OS font is only a fallback.
        var font = Resources.Load<Font>("Fonts/Galmuri11") ?? Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic" }, 32);
        var bold = Resources.Load<Font>("Fonts/Galmuri11-Bold") ?? font;
        var display = Resources.Load<Font>("Fonts/Galmuri14") ?? bold;
        GUIStyle Make(int size, Font f = null) => new GUIStyle(GUI.skin.label) { font = f ?? font, fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        big = Make(40, bold);
        mid = Make(24);
        small = Make(18);
        tiny = Make(15);
        pop = Make(52, display);
        button = UiSkin.Button(bold, 22);
        turnButton = new GUIStyle(button) { border = new RectOffset(9, 9, 9, 9), fontSize = 25 };
        var normalTurn = T("KenneyPixelAdventure/turn_normal");
        var hoverTurn = T("KenneyPixelAdventure/turn_hover");
        var pressedTurn = T("KenneyPixelAdventure/turn_pressed");
        normalTurn.filterMode = hoverTurn.filterMode = pressedTurn.filterMode = FilterMode.Point;
        turnButton.normal.background = normalTurn;
        turnButton.hover.background = hoverTurn;
        turnButton.active.background = pressedTurn;
        turnButton.normal.textColor = turnButton.hover.textColor = turnButton.active.textColor = Hex("#2d1c17");
        smallButton = UiSkin.Button(font, 16);
        smallButton.padding = new RectOffset(2, 2, 3, 5); // skill names must fit narrow reel buttons
        shopButton = UiSkin.Button(font, 15);
        shopButton.alignment = TextAnchor.MiddleLeft;
        shopButton.wordWrap = true;
        panel = UiSkin.Panel();
        shopButton.padding.left = 54; // room for the item icon
        story_ = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, wordWrap = true, alignment = TextAnchor.UpperLeft };

        // soft round glow, used for auras, magic bolts, rings and light beams
        glow = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                glow.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2)));
            }
        glow.Apply();

        vignette = new Texture2D(64, 36, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 36; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = (x - 31.5f) / 32f, dy = (y - 17.5f) / 18f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                vignette.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 1.25f, d))));
            }
        vignette.Apply();
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;
}
