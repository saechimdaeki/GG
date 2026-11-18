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

    static readonly Color Gold = new Color(1f, 0.85f, 0.3f), HitRed = new Color(1f, 0.35f, 0.3f),
        HealGreen = new Color(0.45f, 1f, 0.5f), BlockBlue = new Color(0.5f, 0.75f, 1f), Ice = new Color(0.6f, 0.9f, 1f),
        Panel = new Color(0.07f, 0.06f, 0.09f);

    TuningConfig cfg;
    Run run;
    GUIStyle big, mid, small, tiny, button, smallButton, pop, story_;
    Texture2D glow;
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
        music.volume = 0.3f;
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
        if (StrArg("--node") is string nodeType && System.Enum.TryParse(nodeType, out NodeType t))
        {
            var node = run.Map.Rows[0][1];
            node.Type = t;
            run.Choose(node);
        }
        if (HasArg("--charge")) for (int i = 0; i < run.Gauge.Count; i++) run.Gauge[i] = run.SkillCharge;
        foreach (var id in StrArg("--equip")?.Split(',') ?? new string[0])
            for (int i = 0; i < run.Party.Count; i++)
                if (run.Equipment[i] == null && cfg.items.Any(it => it.id == id)) { run.Equipment[i] = id; break; }
        foreach (var id in StrArg("--relic")?.Split(',') ?? new string[0])
            if (cfg.items.Any(it => it.id == id)) run.Relics.Add(id);
        int n = IntArg("--autoturns");
        for (int k = 0; k < n && run.Phase == Phase.Turn; k++) run.Commit();
        Sync();
        SpinAll();
        if (HasArg("--nostory")) talk.Clear();
        if (System.Environment.GetCommandLineArgs().Any(a => a.StartsWith("--") && a != "--screenshot" && a != "--at" && a != "--after")) onTitle = false;
        if (HasArg("--title")) onTitle = true;
        for (int k = IntArg("--skiplines"); k > 0 && talk.Count > 0; k--) talk.Dequeue(); // screenshots of later lines
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

    void NewRun()
    {
        run = new Run(cfg, new System.Random());
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
        if (run.Chapter != toldChapter)
        {
            toldChapter = run.Chapter;
            if (run.Chapter < story.chapters.Length) Tell(story.chapters[run.Chapter].intro);
        }
        for (; toldParty < run.Party.Count; toldParty++)
        {
            var line = story.recruits.FirstOrDefault(r => r.id == run.Party[toldParty]);
            if (line != null) Tell(new[] { new Line { speaker = line.id, text = line.text } });
        }
        if (run.Phase == Phase.Turn && run.Current?.Type == NodeType.Boss && toldBoss.Add(run.Chapter) && run.Chapter < story.chapters.Length)
            Tell(story.chapters[run.Chapter].boss);
        if (!toldEnd && (run.Phase == Phase.Won || run.Phase == Phase.Lost))
        {
            toldEnd = true;
            Sfx(run.Phase == Phase.Won ? "win" : "lose");
            Tell(run.Phase == Phase.Won ? story.ending : story.defeat);
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
        shownFaces = run.Faces.ToArray();
        shownHp = run.Hp;
        shownGold = run.Gold;
        heroDown = run.Phase == Phase.Lost;
        if (run.Phase == Phase.Turn && shownEnemy != run.Enemy) { enemyEnterAt = Time.time; enemyDeathAt = -9; }
        shownEnemy = run.Enemy;
        shownEnemyHp = run.EnemyHp;
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
    }

    void SpinAll()
    {
        for (int i = 0; i < spinUntil.Length; i++) spinUntil[i] = Time.time + 0.45f + 0.15f * i;
    }

    void Update()
    {
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
        sfx.PlayOneShot(clip, volume);
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
                    Pop("저주! 릴에 해골", new Color(0.75f, 0.5f, 1f), new Vector2(W / 2, ReelY + 30), 36);
                }
                yield return Tween(v => enemyOffset = new Vector2(-v * (EnemyX - FrontX - 110), 0), 1, 0, 0.2f);
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
                yield return FireShot(glow, from + new Vector2(20, -40), target, 0.28f, 50, 0, 1.4f, 0, ice ? Ice : new Color(1f, 0.55f, 0.2f), true);
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
            default:
                yield return new WaitForSeconds(0.2f);
                break;
        }
    }

    IEnumerator FireShot(Texture2D t, Vector2 from, Vector2 to, float dur, float arc, float spin, float scale, float angle, Color c, bool glowing)
    {
        shots.Add(new Shot { tex = t, from = from, to = to, start = Time.time, dur = dur, arc = arc, spin = spin, scale = scale, angle = angle, color = c, glowing = glowing });
        yield return new WaitForSeconds(dur);
        if (glowing) AddFx("ring", to, 0.3f, 140, c);
    }

    static IEnumerator Hitstop(float seconds)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = 1;
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
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / W, Screen.height / H, 1)) *
                     Matrix4x4.Translate(new Vector3(Wobble(shakeAt, shakeAmount), Wobble(shakeAt + 0.05f, shakeAmount * 0.7f), 0));

        DrawArena();
        DrawFx(false);
        DrawShots();
        DrawParticles();
        DrawHeader();

        if (onTitle) DrawTitle();
        else if (talk.Count > 0 && !busy) DrawStory();   // dialogue blocks every other panel until it is read
        else if (busy || run.Phase == Phase.Turn) DrawReels();
        else if (run.Phase == Phase.Map) DrawMap();
        else if (run.Phase == Phase.Reward) DrawOffers();
        else if (run.Phase == Phase.Shop) DrawShop();
        else if (run.Phase == Phase.Event) DrawEvent();
        else DrawEnd();

        DrawFx(true);
        DrawPopups();
        float flash = 1 - Mathf.Clamp01((Time.time - flashAt) / 0.25f);
        if (flash > 0) Fill(new Rect(-20, -20, W + 40, H + 40), new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * flash));

        // bottom line: hovered button's tooltip, else the last turn's log
        if (GUI.tooltip != "") Shadowed(new Rect(0, 688, W, 28), GUI.tooltip, small, Gold);
        else if (!busy) Shadowed(new Rect(0, 688, W, 28), run.Log, small, new Color(0.8f, 0.8f, 0.85f));
    }

    void DrawHeader()
    {
        int rows = run.Map.Rows.Count;
        string where = run.Current == null ? "입구" : run.Current.Type == NodeType.Boss ? "보스" : $"{run.Current.Row + 1} / {rows}";
        Shadowed(new Rect(0, 8, W, 36), $"{run.Chapter + 1}장 · {run.ChapterDef.name} · {where}", small, Color.white);
        DrawTex(T("0x72/coin_anim_f" + (int)(Time.time * 8) % 4), new Rect(W - 150, 14, 18, 21));
        Shadowed(new Rect(W - 126, 10, 120, 30), shownGold.ToString(), mid, Gold, TextAnchor.MiddleLeft);
        if (GUI.Button(new Rect(W - 190, 10, 30, 30), new GUIContent(AudioListener.volume > 0 ? "♪" : "×", "소리 켜기/끄기"), smallButton))
            AudioListener.volume = AudioListener.volume > 0 ? 0 : 1;

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
        Fill(new Rect(box.x - 4, box.y - 4, box.width + 8, box.height + 8), new Color(0.55f, 0.45f, 0.3f));
        Fill(box, new Color(0.08f, 0.07f, 0.1f, 0.96f));
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

    void DrawEvent()
    {
        var e = run.CurrentEvent;
        Fill(new Rect(W / 2 - 400, 345, 800, 335), new Color(0, 0, 0, 0.6f));
        Shadowed(new Rect(0, 350, W, 44), e.title, big, Gold);
        story_.normal.textColor = new Color(0.9f, 0.9f, 0.95f);
        GUI.Label(new Rect(W / 2 - 360, 398, 720, 70), e.text, story_);
        for (int i = 0; i < e.options.Length; i++)
        {
            var option = e.options[i];
            GUI.enabled = run.CanChoose(option);
            if (GUI.Button(new Rect(W / 2 - 300, 474 + i * 56, 600, 48), option.label, smallButton))
            {
                run.ChooseEvent(i);
                Sfx("ui_click");
                Sync();
                if (run.Phase == Phase.Turn) SpinAll();
                Burst(new Vector2(W / 2, 500), Gold, 16, 160, 100, 0.6f, 6);
            }
            GUI.enabled = true;
        }
    }

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
        Fill(new Rect(0, FloorY + 20, W, H), Panel);

        for (int i = run.Party.Count - 1; i >= 0; i--) DrawMember(i);
        Bar(FrontX - MemberGap * 0.5f, 132, shownHp, run.MaxHp, HealGreen, 220);
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
        var eTex = Anim(e.sprite);
        DrawChar(eTex, ex, FloorY + enemyOffset.y + dead * 20, true, eTint, 0, 1 + dead * 0.3f);
        if (eHit) DrawChar(eTex, ex, FloorY + enemyOffset.y, true, new Color(1, 1, 1, 0.6f), 0, 1.05f); // white-ish flash layer
        if (dead > 0) return;
        float top = EnemyHead().y;
        Bar(EnemyX, top - 4, shownEnemyHp, e.hp, HitRed, 160);
        Shadowed(new Rect(EnemyX - 150, top - 62, 300, 30), e.name, mid, run.Current?.Type == NodeType.Boss ? Gold : Color.white);
        if (!busy && !AnySpinning())
        {
            var o = run.Preview();
            int atk = run.EnemyAttack(o);
            var mv = run.Intent;
            string dmg = atk < run.EnemyIntent ? $"{atk} ({run.EnemyIntent})" : atk.ToString();
            var purple = new Color(0.75f, 0.5f, 1f);
            (string icon, string label, Color color, string tip) intent =
                o.frozen ? ("0x72/flask_big_blue", "얼음!", Ice, "얼어서 이번 턴 아무것도 못 한다")
                : mv.Kind == "guard" ? ("shield", $"방어 {mv.Value}", BlockBlue, $"이번 턴 받는 피해 -{mv.Value}")
                : mv.Kind == "enrage" ? ("0x72/weapon_mace", $"분노 +{mv.Value}", HitRed, $"이후 모든 공격 +{mv.Value}")
                : mv.Kind == "curse" ? ("0x72/skull", $"저주 {dmg}", purple, $"{atk} 피해 + 릴 하나에 해골 (이 전투 동안)")
                : mv.Kind == "drain" ? ("0x72/flask_big_red", $"흡혈 {dmg}", HitRed, $"{atk} 피해, 막지 못한 만큼 회복")
                : ("0x72/weapon_knight_sword", dmg, HitRed, $"{atk} 피해");
            var iconTex = T(intent.icon);
            float s = 40f / Mathf.Max(iconTex.width, iconTex.height);
            // speech-bubble style plate in the gap between the party and the enemy
            var plate = new Rect(EnemyX - 330, FloorY - 215, 200, 52);
            Fill(new Rect(plate.x - 3, plate.y - 3, plate.width + 6, plate.height + 6), new Color(intent.color.r, intent.color.g, intent.color.b, 0.7f));
            Fill(plate, new Color(0.08f, 0.07f, 0.1f, 0.9f));
            var iconRect = new Rect(plate.x + 10 + (40 - iconTex.width * s) / 2, plate.y + 6, iconTex.width * s, iconTex.height * s);
            GUI.DrawTexture(iconRect, iconTex);
            var labelRect = new Rect(plate.x + 58, plate.y + 10, 140, 32);
            Shadowed(labelRect, intent.label, mid, intent.color, TextAnchor.MiddleLeft);
            GUI.Label(plate, new GUIContent("", "적의 다음 행동: " + intent.tip));
            if (run.EnemyRage > 0) Shadowed(new Rect(EnemyX + 90, top - 4, 120, 24), $"분노 +{run.EnemyRage}", tiny, HitRed, TextAnchor.MiddleLeft);
        }
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
        else if (moving) t = Anim($"{m.sprite}_run_anim");
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
                Icon(face, local, 1 + 0.25f * (1 - land), 1);
                if (face.id == m.signature && settled) Shadowed(new Rect(0, 2, r.width - 6, 20), "+1", tiny, Gold, TextAnchor.UpperRight);
            }
            GUI.EndGroup();

            var portrait = Anim($"{m.sprite}_idle_anim");
            DrawTex(portrait, new Rect(r.x - 2, r.y - 34, portrait.width * 1.5f, portrait.height * 1.5f));
            string pips = new string('●', run.Gauge[i]) + new string('○', Mathf.Max(0, run.SkillCharge - run.Gauge[i]));
            Shadowed(new Rect(r.x + 24, r.y - 28, r.width - 20, 22), $"{m.name} {pips}", tiny, run.SkillReady(i) ? Gold : Color.white, TextAnchor.MiddleLeft);
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
            if (GUI.Button(new Rect(r.x, r.yMax + 34, bw, 32), new GUIContent("↻", "릴 다시 돌리기"), smallButton))
            {
                run.Reroll(i);
                shownFaces[i] = run.Faces[i];
                spinUntil[i] = Time.time + 0.45f;
            }
            GUI.enabled = !busy && (run.SkillReady(i) || run.Armed.Contains(i));
            var label = run.Armed.Contains(i) ? "취소" : m.skillName;
            if (GUI.Button(new Rect(r.x + bw + 4, r.yMax + 34, r.width - bw - 4, 32), new GUIContent(label, $"{m.name} · {m.skillName}: {m.skillDesc}"), smallButton))
                if (run.ToggleSkill(i)) Sfx(run.Armed.Contains(i) ? "skill" : "ui_click", 0.6f);
            GUI.enabled = true;
        }

        if (settled)
        {
            var o = run.Preview();
            var line = $"공격 {o.damage}   방어 {o.block}   회복 {o.heal}" + (o.gold > 0 ? $"   골드 {o.gold}" : "") + (o.selfDamage > 0 ? $"   자해 {o.selfDamage}" : "");
            Shadowed(new Rect(0, 578, W, 30), line, mid, Color.white);
        }
        Shadowed(new Rect(0, 622, W / 2 - 140, 40), $"다시 돌리기 {run.RerollsLeft}회", small, new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleRight);
        GUI.enabled = settled;
        if (GUI.Button(new Rect(W / 2 - 120, 616, 240, 58), "확정!", button)) { Sfx("ui_click"); StartCoroutine(PlayTurn()); }
        GUI.enabled = true;
    }

    void DrawMap()
    {
        var map = run.Map;
        int rows = map.Rows.Count;
        Vector2 Pos(MapNode n) => new Vector2(150 + n.Row * (980f / (rows - 1)), 430 + n.Lane * 92);
        Shadowed(new Rect(0, 348, W, 40), $"{run.ChapterDef.name} — 다음 갈 곳을 고르세요", mid, Gold);

        var available = run.Available.ToList();
        foreach (var row in map.Rows)
            foreach (var n in row)
                foreach (var next in n.Next)
                {
                    bool lit = n == run.Current && available.Contains(next);
                    var c = lit ? Gold : n.Visited && next.Visited ? new Color(0.9f, 0.85f, 0.7f, 0.9f) : new Color(1, 1, 1, 0.18f);
                    Dotted(Pos(n), Pos(next), c, lit ? 6 : 4);
                }

        foreach (var row in map.Rows)
            foreach (var n in row)
            {
                var p = Pos(n);
                bool can = available.Contains(n);
                bool here = n == run.Current;
                float size = n.Type == NodeType.Boss ? 84 : 58;
                var r = new Rect(p.x - size / 2, p.y - size / 2, size, size);
                if (can) Glow(p, size * 2.2f, new Color(Gold.r, Gold.g, Gold.b, 0.35f + 0.25f * Mathf.Sin(Time.time * 5)));
                if (here) Glow(p, size * 2f, new Color(1, 1, 1, 0.4f));
                Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), can ? Gold : here ? Color.white : new Color(0.35f, 0.3f, 0.28f));
                Fill(r, n.Visited && !here ? new Color(0.2f, 0.18f, 0.2f) : new Color(0.14f, 0.12f, 0.16f));
                var icon = NodeIcon(n);
                if (icon == null) Shadowed(r, "?", big, n.Visited && !here ? new Color(1, 1, 1, 0.35f) : Gold);
                else
                {
                    float s = Mathf.Floor(Mathf.Min(r.width * 0.8f / icon.width, r.height * 0.8f / icon.height));
                    var oldC = GUI.color;
                    GUI.color = n.Visited && !here ? new Color(1, 1, 1, 0.35f) : n.Type == NodeType.Elite ? new Color(1f, 0.6f, 0.6f) : Color.white;
                    GUI.DrawTexture(new Rect(p.x - icon.width * s / 2, p.y - icon.height * s / 2, icon.width * s, icon.height * s), icon);
                    GUI.color = oldC;
                }
                GUI.enabled = can;
                if (GUI.Button(r, new GUIContent("", NodeName(n.Type)), GUIStyle.none) && can)
                {
                    run.Choose(n);
                    Sfx("map_step");
                    Sync();
                    SpinAll();
                }
                GUI.enabled = true;
            }
    }

    void DrawOffers()
    {
        Fill(new Rect(W / 2 - 340, 345, 680, 335), new Color(0, 0, 0, 0.55f));
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
        }
        if (GUI.Button(new Rect(W / 2 - 80, 404 + run.Offers.Count * 72 + 4, 160, 40), "건너뛰기", smallButton))
        {
            run.ChooseReward(-1);
            Sync();
        }
    }

    void DrawShop()
    {
        Fill(new Rect(W / 2 - 360, 345, 720, 335), new Color(0, 0, 0, 0.55f));
        Shadowed(new Rect(0, 348, W, 44), "떠돌이 상인", big, Gold);
        for (int i = 0; i < run.ShopItems.Count; i++)
        {
            var item = run.ShopItems[i];
            var r = new Rect(W / 2 - 300, 396 + i * 48, 460, 44);
            GUI.enabled = !item.Sold && run.Gold >= item.Price;
            if (GUI.Button(r, new GUIContent(item.Sold ? "판매 완료" : item.Text, OfferTip(item)), smallButton))
            {
                if (run.Buy(i)) Sfx("coin");
                Sync();
                Burst(new Vector2(r.xMax + 60, r.center.y), Gold, 12, 120, 200, 0.5f, 5);
            }
            GUI.enabled = true;
            if (!item.Sold) OfferIcon(item, new Rect(r.x, r.y - 6, r.width, r.height + 12));
            DrawTex(T("0x72/coin_anim_f0"), new Rect(r.xMax + 16, r.y + 14, 15, 18));
            Shadowed(new Rect(r.xMax + 38, r.y, 100, r.height), item.Price.ToString(), mid, run.Gold >= item.Price || item.Sold ? Gold : HitRed, TextAnchor.MiddleLeft);
        }
        if (GUI.Button(new Rect(W / 2 - 90, 642, 180, 36), "떠나기", smallButton))
        {
            run.LeaveShop();
            Sync();
        }
    }

    void DrawTitle()
    {
        Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.35f));
        float bob = Mathf.Sin(Time.time * 2) * 6;
        Glow(new Vector2(W / 2, 220 + bob), 700, new Color(1f, 0.75f, 0.3f, 0.25f));
        pop.fontSize = 96;
        Shadowed(new Rect(0, 150 + bob, W, 130), "릴 던전", pop, Gold);
        Shadowed(new Rect(0, 270 + bob, W, 40), "동료 한 명이 릴 하나 · 운명의 릴을 되찾아라", mid, Color.white);
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
        if (GUI.Button(new Rect(W / 2 - 130, 480, 260, 64), "모험 시작", button))
        {
            onTitle = false;
            lineStart = Time.time;
            Sfx("combo");
        }
        if (GUI.Button(new Rect(W / 2 - 130, 556, 260, 48), "종료", smallButton)) Application.Quit();
        Shadowed(new Rect(0, 640, W, 30), "아트: 0x72 DungeonTileset II (CC0) · 소리: 직접 합성", tiny, new Color(1, 1, 1, 0.5f));
    }

    void DrawEnd()
    {
        bool won = run.Phase == Phase.Won;
        Fill(new Rect(W / 2 - 360, 345, 720, 335), new Color(0, 0, 0, 0.6f));
        Shadowed(new Rect(0, 352, W, 60), won ? "던전 정복!" : "쓰러졌다...", big, won ? Gold : HitRed);
        string reached = won ? "운명의 릴을 되찾았다" : $"{run.Chapter + 1}장 {run.ChapterDef.name}에서 쓰러짐";
        Shadowed(new Rect(0, 410, W, 30), reached, mid, Color.white);
        var stats = new[]
        {
            ("처치", run.Kills.ToString()), ("턴", run.Turns.ToString()), ("최고 한 방", run.BestHit.ToString()),
            ("모은 골드", run.GoldEarned.ToString()), ("동료", $"{run.Party.Count}명"), ("유물", $"{run.Relics.Count}개"),
        };
        for (int k = 0; k < stats.Length; k++)
        {
            var cell = new Rect(W / 2 - 330 + (k % 3) * 220, 452 + (k / 3) * 62, 210, 56);
            Fill(cell, new Color(1, 1, 1, 0.06f));
            Shadowed(new Rect(cell.x, cell.y + 2, cell.width, 22), stats[k].Item1, tiny, new Color(0.8f, 0.8f, 0.85f));
            Shadowed(new Rect(cell.x, cell.y + 22, cell.width, 32), stats[k].Item2, mid, Gold);
        }
        if (GUI.Button(new Rect(W / 2 - 270, 590, 260, 56), "다시 도전", button)) { NewRun(); Sfx("ui_click"); }
        if (GUI.Button(new Rect(W / 2 + 10, 590, 260, 56), "타이틀로", button)) { NewRun(); onTitle = true; Sfx("ui_click"); }
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

    float MemberX(int i) => FrontX - i * MemberGap;
    Vector2 MemberFeet(int i) => new Vector2(MemberX(i), FloorY - (i % 2) * 14);
    Vector2 MemberCenter(int i) => MemberFeet(i) + new Vector2(0, -70);
    Vector2 MemberHead(int i) => new Vector2(MemberX(i), FloorY - 28 * Px - 16);
    Vector2 EnemyHead() => new Vector2(EnemyX, FloorY - Anim(shownEnemy.sprite).height * Px - 16);
    Vector2 EnemyBody() => new Vector2(EnemyX, FloorY - Anim(shownEnemy.sprite).height * Px / 2);

    Texture2D NodeIcon(MapNode n)
    {
        switch (n.Type)
        {
            case NodeType.Elite: return T("0x72/skull");
            case NodeType.Recruit: return Anim("elf_m_idle_anim");
            case NodeType.Shop: return T("0x72/coin_anim_f" + (int)(Time.time * 6) % 4);
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
            var s = run.Symbol(r.SymbolId);
            return $"{s.name}: 공격 {s.damage} 방어 {s.block} 회복 {s.heal}" + (s.gold > 0 ? $" 골드 {s.gold}" : "") + (s.weaken > 0 ? $" 약화 {s.weaken}" : "") + (s.selfDamage > 0 ? $" 자해 {s.selfDamage}" : "");
        }
        return "";
    }

    void OfferIcon(Reward r, Rect row)
    {
        if (r.SymbolId != null) Icon(run.Symbol(r.SymbolId), new Rect(row.x + 8, row.y + 6, row.height - 12, row.height - 12), 1, 1);
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
        var font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Malgun Gothic" }, 32);
        GUIStyle Make(int size) => new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = TextAnchor.MiddleCenter };
        big = Make(40);
        mid = Make(26);
        small = Make(18);
        tiny = Make(15);
        pop = Make(52);
        big.fontStyle = pop.fontStyle = FontStyle.Bold;
        button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22 };
        smallButton = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
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
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;
}
