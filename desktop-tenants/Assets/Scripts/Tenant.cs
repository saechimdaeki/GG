using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DesktopTenants
{
    /// <summary>
    /// One tenant. Walks, climbs (hops up or puts up a ladder), naps, chats, dances, argues and
    /// does small quirks and its signature action on window edges; pays rent as coins; gets flung
    /// when its window moves fast; and shows how it feels with a face, an emote and a speech bubble.
    /// </summary>
    public class Tenant : MonoBehaviour
    {
        enum State { Idle, Walk, Sleep, Chat, Dance, Special, Quirk, Sit, Dizzy, Flee, Approach, Climb, Fall, Held, Carried }

        /// <summary>What two tenants do once they've finished talking.</summary>
        public enum Outcome { None, Dance, HighFive, Argue }

        static readonly string[] Count = { "하나!", "둘!", "셋!", "넷!", "다섯!", "여섯!", "일곱!", "여덟!", "아홉!", "열!" };
        static readonly string[] PersonQuirks = { "stretch", "sneeze", "stretch", "sneeze" };
        static readonly string[] CatQuirks = { "catstretch", "groomfast" };
        static readonly string[] DogQuirks = { "zoomies", "wagfast" };
        static Dictionary<string, Sprite> emoteSprites;
        static Sprite ladderTile;

        Desk d;
        Tuning T => d.Tune;
        public TenantType Type { get; private set; }
        readonly Body b = new Body();
        State s = State.Walk;
        Vector2 grab, holdStart, climbAnchor;
        int dir = -1, pokes, reps;
        bool asleepInAir, tumbling, fleeOnLand, phased, quirkHit;
        Outcome outcome;
        string chatLine, quirk;
        float chatDelay, climbX;
        float evacT;
        float timer, anim, chatCool, holdT, talkCool, rentT, pokeT, lookCool, blinkT = 3, emoteT, happyT, squash, spin, beat, qt;
        Seg climbTo;
        Coin prey;
        Tenant chase, partner;
        readonly List<(Seg to, float lo, float hi)> climbOptions = new List<(Seg, float, float)>();

        readonly Dictionary<(string, Face), Sprite> frames = new Dictionary<(string, Face), Sprite>();
        Dictionary<char, Color> pal;
        Transform squashT, spinT;
        SpriteRenderer body, emote, zzz, ladder;
        Bubble bubble;
        float w, h;

        public Vector2 P => b.p;
        public int On => b.on;
        public bool Held => s == State.Held;
        public Bubble Bubble => bubble;
        /// <summary>Standing around (not talking, dancing, climbing or being carried): may be nudged aside.</summary>
        public bool Free => b.Grounded && (s == State.Idle || s == State.Walk || s == State.Sleep || s == State.Sit || s == State.Quirk || s == State.Special);

        /// <summary>Shuffle sideways by <paramref name="dx"/> points, staying on the current edge.</summary>
        public void Nudge(float dx)
        {
            var moved = b.p + new Vector2(dx, 0);
            if (d.SegAt(b.on, moved, out var seg)) b.p.x = Mathf.Clamp(moved.x, seg.x0 + 3, seg.x1 - 3);
        }

        public float Height => h;

        /// <summary>Can be picked up by a helicopter or UFO right now.</summary>
        public bool Carriable => b.Grounded && s != State.Held && s != State.Carried && evacT <= 0;

        /// <summary>Hooked by a helicopter rope or caught in a UFO beam.</summary>
        public void Carry(string line)
        {
            HideLadder();
            s = State.Carried;
            b.on = Body.None;
            b.v = Vector2.zero;
            asleepInAir = false;
            Say(Type.IsCat ? "냐아아앙?!" : Type.IsDog ? "깨갱깨갱!" : line, 2f);
            Emote("!", 1.2f);
        }

        /// <summary>Moved by the carrier each frame (feet position).</summary>
        public void CarryTo(Vector2 feet) => b.p = feet;

        /// <summary>Let go of by the carrier: drop with <paramref name="v"/>.</summary>
        public void Release(Vector2 v, string line)
        {
            if (s != State.Carried) return;
            s = State.Fall;
            b.Drop(v);
            if (!Type.IsAnimal) Say(line, 1.8f);
        }

        /// <summary>Fire drill: everyone heads down to the front door for <paramref name="seconds"/>.</summary>
        public void Evacuate(float seconds)
        {
            if (s == State.Carried || s == State.Held) return;
            HideLadder();
            if (s == State.Climb) { s = State.Fall; b.Drop(Vector2.zero); }
            evacT = seconds;
            Say(Type.IsCat ? "냥?!" : Type.IsDog ? "멍멍멍!" : "대피! 대피!", 1.6f);
            Emote("sweat", 2f);
        }

        /// <summary>Fireworks: look up and cheer.</summary>
        public void Cheer()
        {
            if (!b.Grounded || s == State.Sleep) return;
            happyT = 3f;
            Emote("heart", 2f);
            if (Random.value < 0.5f) Say(Type.IsAnimal ? TenantBook.Pick(Type.idle) : "와아~!", 1.6f);
        }

        /// <summary>The moving truck is coming through: jump out of the way.</summary>
        public void DodgeTruck(int dir)
        {
            if (!b.Grounded || b.on != Surfaces.Floor || s == State.Held || s == State.Carried) return;
            s = State.Fall;
            b.Drop(new Vector2(dir * 140, -420));
            squash = -0.6f;
            Say(Type.IsCat ? "캬악!" : Type.IsDog ? "왈왈!" : "으악 트럭!", 1.2f);
            Emote("!", 1f);
        }

        public bool CanChat => (s == State.Walk || s == State.Idle) && b.Grounded && chatCool <= 0;
        public Rect Bounds => new Rect(b.p.x - w / 2 - 4, b.p.y - h - 4, w + 8, h + 8);

        public void Init(Desk desk, TenantType type, Vector2 at)
        {
            d = desk;
            Type = type;
            b.p = at;
            b.Drop(Vector2.zero);
            rentT = Random.Range(T.rentMin, T.rentMax) * 0.5f;
            chatCool = 5;
            pal = new Dictionary<char, Color>
            {
                { 'h', TenantType.Hex(type.hair) }, { 's', TenantType.Hex(type.skin) }, { 'e', Pixels.Ink },
                { 'k', TenantType.Hex(type.acc) }, { 'x', Pixels.Ink },
                { 'c', TenantType.Hex(type.shirt) }, { 'p', TenantType.Hex(type.pants) }, { 'o', Pixels.Ink },
                { 'm', new Color32(0xc8, 0x40, 0x50, 255) }, { 'n', new Color32(0xf0, 0x8a, 0x9a, 255) },
                { '-', new Color(Pixels.Ink.r, Pixels.Ink.g, Pixels.Ink.b, 0.55f) },
                { 'f', Pixels.Ink }, { 'g', new Color32(0x80, 0xd0, 0xff, 255) }, { 'w', Color.white },
            };
            if (emoteSprites == null)
            {
                emoteSprites = new Dictionary<string, Sprite>();
                foreach (var kv in Poses.Emotes) emoteSprites[kv.Key] = Pixels.Make(kv.Value, Poses.EmotePalette, T.pixel);
                ladderTile = Pixels.LadderTile(T.pixel);
            }
            var idle = Poses.Pose("idle", Face.Normal, type.kind);
            w = idle[0].Length * T.pixel;
            h = idle.Length * T.pixel;

            // root (feet) → squash (scales about the feet) → spin (rotates about the middle) → body
            squashT = new GameObject("Squash").transform;
            squashT.SetParent(transform, false);
            spinT = new GameObject("Spin").transform;
            spinT.SetParent(squashT, false);
            spinT.localPosition = new Vector3(0, h / 2, 0);
            body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(spinT, false);
            body.transform.localPosition = new Vector3(0, -h / 2, 0);
            body.sortingOrder = 10;
            emote = Child("Emote", null);
            zzz = Child("Zzz", Pixels.Make(Pixels.Zzz, pal, T.pixel));
            // The ladder stands in the world, not on the tenant, so it stays put while they climb.
            ladder = new GameObject(type.name + " ladder").AddComponent<SpriteRenderer>();
            ladder.sprite = ladderTile;
            ladder.drawMode = SpriteDrawMode.Tiled;
            ladder.sortingOrder = 8;
            ladder.enabled = false;
            bubble = Bubble.Create(transform, new Vector3(0, h + 18, 0));
            Say(type.name + " 입주!", 2.5f);
            Emote("heart", 1.5f);
        }

        SpriteRenderer Child(string name, Sprite sprite)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            r.sprite = sprite;
            r.sortingOrder = 11;
            r.enabled = false;
            return r;
        }

        Sprite Frame(string pose, Face face)
        {
            if (!Poses.Has(pose, Type.kind)) pose = "idle";
            if (!frames.TryGetValue((pose, face), out var sp))
                frames[(pose, face)] = sp = Pixels.Make(Poses.Pose(pose, face, Type.kind, Type.gear), pal, T.pixel, pose == "sit" ? Poses.SitPivot : (Vector2?)null);
            return sp;
        }

        void Say(string line, float seconds = 2.2f)
        {
            if (line == null) return;
            bubble.Say(line, seconds);
            talkCool = Mathf.Max(talkCool, seconds + 1);
        }

        void Emote(string name, float seconds)
        {
            emote.sprite = emoteSprites[name];
            emoteT = seconds;
        }

        bool EmoteIs(string name) => emoteT > 0 && emote.sprite == emoteSprites[name];

        /// <summary>Idle chatter, rate-limited. People talk about the real weekday, time and weather too.</summary>
        void Chatter()
        {
            if (talkCool > 0) return;
            if (d.Raining && Random.value < 0.6f)
            {
                Say(Type.IsCat ? "냐아앙! 물 싫어!" : Type.IsDog ? "멍멍! 비다!" : TenantBook.Pick(d.Book.rain));
                return;
            }
            if (Random.value > T.lineChance) return;
            var now = DateTime.Now;
            if (!Type.IsAnimal && d.Crowded && Random.value < 0.4f) Say(TenantBook.Pick(d.Book.crowded));
            else if (!Type.IsAnimal && d.Night && Random.value < 0.4f) Say(TenantBook.Pick(d.Book.night));
            else if (!Type.IsAnimal && Random.value < T.sharedLineChance)
                Say(TenantBook.Pick(Random.value < 0.5f ? d.Book.Day(now) : d.Book.TimeOfDay(now) ?? d.Book.Day(now)));
            else Say(TenantBook.Pick(Type.idle));
        }

        // ---------------------------------------------------------------- input from Desk

        public void Grab(Vector2 mouse)
        {
            HideLadder();
            s = State.Held;
            grab = b.p - mouse;
            holdStart = mouse;
            holdT = 0;
            b.v = Vector2.zero;
            b.on = Body.None;
            asleepInAir = false;
        }

        /// <summary>
        /// Starts a conversation turn: says <paramref name="line"/> after <paramref name="delay"/> seconds,
        /// so the two halves of an exchange come one after the other instead of on top of each other.
        /// </summary>
        public void Chat(Tenant other, Outcome then, string line, float delay)
        {
            s = State.Chat;
            partner = other;
            outcome = then;
            chatLine = line ?? TenantBook.Pick(Type.greet);
            chatDelay = delay;
            timer = delay + 2.6f;
            chatCool = T.chatCooldown;
            dir = other.P.x > P.x ? 1 : -1;
            if (delay <= 0) SayChat();
        }

        void SayChat()
        {
            Say(chatLine, 2.4f);
            Emote(outcome == Outcome.Argue ? "anger" : "heart", 1.4f);
            chatLine = null;
        }

        /// <summary>Someone nearby put music on: dance along if free.</summary>
        public void JoinDance(float seconds)
        {
            if (!b.Grounded || (s != State.Idle && s != State.Walk && s != State.Sit && s != State.Chat)) return;
            Enter(State.Dance, seconds);
            Emote("note", seconds);
        }

        /// <summary>Someone nearby cooked: cheer up.</summary>
        public void Treat()
        {
            if (!b.Grounded || s == State.Sleep) return;
            happyT = 2f;
            Emote("heart", 1.5f);
            Say(Type.IsAnimal ? TenantBook.Pick(Type.greet) : "냠냠! 맛있다", 1.6f);
        }

        /// <summary>Chased (cats by dogs): hiss and run away from <paramref name="from"/>.</summary>
        public void Scare(Vector2 from)
        {
            if (!b.Grounded || s == State.Flee) return;
            Say(Type.IsCat ? "하아악!" : TenantBook.Pick(Type.poke), 1.4f);
            Emote("!", 1f);
            Flee(from);
        }

        // ---------------------------------------------------------------- simulation

        public void Tick(float dt)
        {
            chatCool -= dt;
            talkCool -= dt;
            pokeT -= dt;
            evacT -= dt;
            lookCool -= dt;
            emoteT -= dt;
            happyT -= dt;
            blinkT -= dt;
            if (blinkT < -0.12f) blinkT = Random.Range(2f, 5f);
            squash = Mathf.MoveTowards(squash, 0, dt * 3.5f);
            switch (s)
            {
                case State.Held: Hold(dt); break;
                case State.Fall: Falling(dt); break;
                case State.Climb: Climbing(dt); break;
                case State.Carried: break;
                default: Grounded(dt); break;
            }
            Render(dt);
        }

        void Hold(float dt)
        {
            holdT += dt;
            var next = d.Mouse + grab;
            b.v = Vector2.Lerp(b.v, (next - b.p) / dt, 0.5f);
            b.p = next;
            if (holdT > 0.3f && !bubble.Visible) { Say(TenantBook.Pick(Type.held), 1.5f); Emote("sweat", 1.5f); }
            if (d.MouseDown) return;
            bool poke = holdT < 0.25f && (d.Mouse - holdStart).magnitude < 6;
            s = State.Fall;
            squash = -0.6f;
            if (poke) Poked();
            else
            {
                b.Drop(Vector2.ClampMagnitude(b.v, T.throwMax));
                tumbling = b.v.magnitude > 700;
                if (b.v.magnitude > 600) { Say(TenantBook.Pick(Type.fling)); Emote("!", 1f); }
            }
        }

        void Poked()
        {
            pokes = pokeT > 0 ? pokes + 1 : 1;
            pokeT = 4;
            b.Drop(new Vector2(0, -320));
            if (pokes >= T.pokeLimit)
            {
                pokes = 0;
                Say(Type.IsCat ? "하아악!!" : Type.IsDog ? "으르르르!" : TenantBook.Pick(d.Book.angry), 2f);
                Emote("anger", 2.5f);
                fleeOnLand = true;
            }
            else
            {
                Say(TenantBook.Pick(Type.poke));
                Emote("?", 1f);
            }
        }

        void Falling(float dt)
        {
            float impact = b.v.y;
            switch (b.Fall(d, dt, h))
            {
                case Body.FallResult.Landed:
                    squash = Mathf.Clamp(impact / 1200f, 0.3f, 1f);
                    tumbling = false;
                    if (asleepInAir) { Enter(State.Sleep, Random.Range(T.sleepMin, T.sleepMax)); asleepInAir = false; break; }
                    if (fleeOnLand) { fleeOnLand = false; Flee(d.Mouse); break; }
                    if (impact > T.dizzySpeed)
                    {
                        Enter(State.Dizzy, 1.8f);
                        Emote("star", 1.8f);
                        Say(Type.IsCat ? "냐..." : Type.IsDog ? "깨갱..." : TenantBook.Pick(d.Book.dizzy), 1.8f);
                        break;
                    }
                    Enter(State.Idle, 0.8f);
                    if (Random.value < 0.5f) Say(TenantBook.Pick(Type.land), 1.6f);
                    break;
                case Body.FallResult.Bonk:
                    Emote("star", 0.8f);
                    Say(TenantBook.Pick(Type.bonk), 1.4f);
                    break;
            }
        }

        void Flee(Vector2 from)
        {
            Enter(State.Flee, 2.2f);
            dir = from.x > b.p.x ? -1 : 1;
        }

        void Enter(State next, float seconds)
        {
            s = next;
            timer = seconds;
            reps = 0;
            beat = 0;
            qt = 0;
            quirkHit = false;
        }

        void Grounded(float dt)
        {
            var r = b.Ride(d, dt, out var seg);
            if (r != Body.RideResult.Ok)
            {
                bool sleeping = s == State.Sleep && Type.sleepFall;
                s = State.Fall;
                if (r == Body.RideResult.Flung)
                {
                    squash = -0.6f;
                    tumbling = true;
                    Emote("!", 1f);
                    if (sleeping) { asleepInAir = true; Say("드르렁... 쿨...", 2f); }
                    else Say(TenantBook.Pick(Type.fling), 1.8f);
                }
                return;
            }

            if (s != State.Sleep && s != State.Dizzy && (rentT -= dt) <= 0)
            {
                rentT = Random.Range(T.rentMin, T.rentMax);
                d.SpawnCoin(b.p - new Vector2(0, h * 0.6f), Type.rent);
                Say(TenantBook.Pick(Type.pay), 1.8f);
                happyT = 1.5f;
                squash = -0.4f;
            }

            if (evacT > 0) { Evac(dt, seg); return; }

            timer -= dt;
            beat += dt;
            qt += dt;
            switch (s)
            {
                case State.Chat: ChatTick(dt); break;
                case State.Dance:
                    if (beat > 0.28f) { beat = 0; dir = -dir; squash = -0.35f; }
                    if (timer <= 0) { Enter(State.Idle, 1f); happyT = 1f; }
                    break;
                case State.Dizzy:
                case State.Sit:
                    if (timer <= 0) Enter(State.Idle, Random.Range(T.idleMin, T.idleMax));
                    break;
                case State.Special: Special(dt, seg); break;
                case State.Quirk: QuirkTick(dt, seg); break;
                case State.Approach: ApproachTick(dt, seg); break;
                case State.Flee:
                    Walk(dt, seg, 2.4f);
                    if (timer <= 0) Enter(State.Idle, 1.5f);
                    break;
                case State.Idle:
                    if (LookAtCursor()) break;
                    if (timer > 0) break;
                    float sleepChance = d.Night ? Mathf.Max(Type.sleepChance, 0.6f) : Type.sleepChance;
                    if (d.Raining) sleepChance = 0;
                    float roll = Random.value;
                    if (roll < sleepChance) Enter(State.Sleep, Random.Range(T.sleepMin, T.sleepMax));
                    else if ((roll -= sleepChance) < T.specialChance) StartSpecial();
                    else if ((roll -= T.specialChance) < T.quirkChance) StartQuirk(Pick(Type.IsCat ? CatQuirks : Type.IsDog ? DogQuirks : PersonQuirks));
                    else { Enter(State.Walk, Random.Range(T.walkMin, T.walkMax)); dir = Random.value < 0.5f ? -1 : 1; Chatter(); }
                    break;
                case State.Sleep:
                    if (timer <= 0 || d.Raining) Enter(State.Idle, Random.Range(T.idleMin, T.idleMax));
                    break;
                case State.Walk: WalkTick(dt, seg); break;
            }
        }

        /// <summary>Fire drill: off any ledge, then run to the door and wait there, sweating.</summary>
        void Evac(float dt, Seg seg)
        {
            float dx = d.Door.x - b.p.x;
            dir = dx > 0 ? 1 : -1;
            if (seg.win != Surfaces.Floor)
            {
                s = State.Fall;
                b.Drop(new Vector2(dir * 120, -220));
                return;
            }
            s = Mathf.Abs(dx) > 50 ? State.Flee : State.Idle;
            if (s == State.Flee) b.p.x += dir * Mathf.Max(Type.walkSpeed, 40) * 2.2f * dt;
            if (evacT <= dt) { Enter(State.Idle, 1f); Say(Type.IsAnimal ? null : "휴~ 훈련이었구나", 1.6f); }
        }

        void WalkTick(float dt, Seg seg)
        {
            if (TryGoUp(dt, seg)) return;
            if (Type.IsCat) HuntCoins(seg);
            if (Type.IsDog) ChaseCats();
            if (!Type.IsAnimal && Random.value < T.tripChance * dt) { StartQuirk("trip"); return; }
            float speed = d.Raining && !Type.IsDog ? 1.7f : 1f;
            if (!Walk(dt, seg, speed)) return;
            if (timer <= 0) { Enter(State.Idle, Random.Range(T.idleMin, T.idleMax)); Chatter(); }
        }

        /// <summary>
        /// Heads for a higher edge: a jump when it's close, otherwise (people only) walk to a spot under it
        /// and put up a ladder. The floor is boring, so tenants on it try twice as often.
        /// </summary>
        bool TryGoUp(float dt, Seg seg)
        {
            float floorBoost = seg.win == Surfaces.Floor ? 2f : 1f;
            float jumpUp = Type.jumper ? T.jumpUp : T.hopUp;
            float jumpChance = Type.jumper ? T.jumpChance : T.hopChance;
            if (Random.value < jumpChance * floorBoost * dt && d.JumpTarget(b.p, jumpUp, out var target)) { Jump(target); return true; }
            if (Type.IsAnimal || Random.value >= T.climbChance * floorBoost * dt) return false;
            d.ClimbOptions(seg, climbOptions);
            if (climbOptions.Count == 0) return false;
            var pick = climbOptions[Random.Range(0, climbOptions.Count)];
            climbTo = pick.to;
            climbX = Random.Range(pick.lo, pick.hi);
            // Enough time to walk there at approach speed, plus a little slack.
            Enter(State.Approach, Mathf.Abs(climbX - b.p.x) / (Type.walkSpeed * 1.3f) + 3f);
            Debug.Log($"[tenant] {Type.name} heads for a ladder: {b.p.y - climbTo.y:0}pt up to window {climbTo.win}");
            Say(TenantBook.Pick(d.Book.climb), 1.6f);
            return true;
        }

        void ApproachTick(float dt, Seg seg)
        {
            float dx = climbX - b.p.x;
            dir = dx > 0 ? 1 : -1;
            if (Mathf.Abs(dx) > 2)
            {
                b.p.x += dir * Mathf.Min(Mathf.Abs(dx), Type.walkSpeed * 1.3f * dt);
                if (timer <= 0 || b.p.x < seg.x0 || b.p.x > seg.x1) Enter(State.Idle, 1f);
                return;
            }
            if (!d.WinPos(climbTo.win, out climbAnchor)) { Enter(State.Idle, 1f); return; }
            // Up the ladder: leave the edge we stood on and climb in the open.
            b.p.x = climbX;
            b.on = Body.None;
            s = State.Climb;
            beat = 0;
            ladder.enabled = true;
            ladder.transform.position = Desk.W(b.p, 0.5f);
            ladder.size = new Vector2(7 * T.pixel, b.p.y - climbTo.y + 6);
            squash = -0.3f;
        }

        void Climbing(float dt)
        {
            // Someone moved the window at the top: the ladder goes, and so does the tenant.
            if (!d.WinPos(climbTo.win, out var wp) || (wp - climbAnchor).sqrMagnitude > 4)
            {
                HideLadder();
                s = State.Fall;
                b.Drop(new Vector2(dir * 90, -60));
                tumbling = true;
                Say(TenantBook.Pick(d.Book.ladderFall), 1.8f);
                Debug.Log($"[tenant] {Type.name} fell off the ladder");
                Emote("!", 1.2f);
                return;
            }
            beat += dt;
            b.p.y -= T.climbSpeed * dt;
            if (b.p.y > climbTo.y) return;
            b.p.y = climbTo.y;
            HideLadder();
            if (!d.SegAt(climbTo.win, b.p, out var top)) { s = State.Fall; b.Drop(Vector2.zero); return; }
            b.Stand(d, top);
            Enter(State.Idle, 1.2f);
            squash = 0.5f;
            happyT = 1.5f;
            Say(TenantBook.Pick(d.Book.arrive), 1.6f);
            Debug.Log($"[tenant] {Type.name} climbed onto window {top.win}");
        }

        void HideLadder()
        {
            if (ladder) ladder.enabled = false;
        }

        void ChatTick(float dt)
        {
            if (chatLine != null && (chatDelay -= dt) <= 0) SayChat();
            if (timer > 0) return;
            switch (outcome)
            {
                case Outcome.Dance:
                    Enter(State.Dance, 3.5f);
                    Emote("note", 3.5f);
                    break;
                case Outcome.HighFive:
                    StartQuirk("highfive");
                    break;
                case Outcome.Argue:
                    Enter(State.Walk, Random.Range(2f, 4f));
                    dir = partner && partner.P.x > b.p.x ? -1 : 1;
                    Say(Type.IsCat ? "흥냥" : Type.IsDog ? "왈!" : "흥!", 1.2f);
                    Emote("anger", 1.5f);
                    break;
                default:
                    Enter(State.Walk, Random.Range(T.walkMin, T.walkMax));
                    dir = -dir;
                    break;
            }
        }

        /// <summary>Walks along <paramref name="seg"/>; at an edge maybe peeks, sits, hops off or turns. False if it left the ground.</summary>
        bool Walk(float dt, Seg seg, float speedMul)
        {
            b.p.x += dir * Type.walkSpeed * speedMul * dt;
            if (b.p.x >= seg.x0 + 3 && b.p.x <= seg.x1 - 3) return true;
            b.p.x = Mathf.Clamp(b.p.x, seg.x0 + 3, seg.x1 - 3);
            bool ledge = seg.win != Surfaces.Floor;  // window tops and the roof
            if (s == State.Walk && !Type.IsAnimal && ledge)
            {
                float roll = Random.value;
                if (roll < T.peekChance) { StartQuirk("peek"); return false; }
                if (roll < T.peekChance + T.sitChance)
                {
                    Enter(State.Sit, Random.Range(4f, 8f)); // facing out, legs over the edge
                    if (Random.value < 0.4f) Say(TenantBook.Pick(Type.idle));
                    return false;
                }
            }
            // The roof is a perch, not a home: tenants hop back down far more often.
            if (Random.value < (seg.win == Surfaces.Roof ? T.roofJumpOff : T.jumpOffChance))
            {
                s = State.Fall;
                b.Drop(new Vector2(dir * 60, -260));
                squash = -0.5f;
                return false;
            }
            dir = -dir;
            return true;
        }

        /// <summary>Cats walk up to coins on their edge and bat them off.</summary>
        void HuntCoins(Seg seg)
        {
            if (!prey) prey = d.CoinNear(b.p, b.on, 160);
            if (!prey || prey.On != b.on) { prey = null; return; }
            float dx = prey.P.x - b.p.x;
            dir = dx > 0 ? 1 : -1;
            if (Mathf.Abs(dx) > 10) return;
            prey.Knock(dir);
            prey = null;
            squash = -0.4f;
            Say("냥!", 1f);
            Emote("!", 0.8f);
        }

        /// <summary>Dogs run at cats on their edge; a cat that gets caught hisses and bolts.</summary>
        void ChaseCats()
        {
            if (!chase) chase = d.NearestCat(this, 220);
            if (!chase || chase.On != b.on) { chase = null; return; }
            float dx = chase.P.x - b.p.x;
            dir = dx > 0 ? 1 : -1;
            b.p.x += dir * Type.walkSpeed * 0.8f * Time.deltaTime; // sprint
            if (Mathf.Abs(dx) > 18) return;
            chase.Scare(b.p);
            chase = null;
            Say("멍멍!", 1.2f);
            Emote("!", 0.8f);
            happyT = 1.5f;
        }

        static string Pick(string[] options) => options[Random.Range(0, options.Length)];

        // ---------------------------------------------------------------- quirks: short everyday actions

        void StartQuirk(string name)
        {
            Enter(State.Quirk, 2f);
            quirk = name;
            switch (name)
            {
                case "stretch": Say(TenantBook.Pick(d.Book.stretch), 1.6f); break;
                case "sneeze": timer = 1.8f; Say("에... 에...", 0.9f); break;
                case "trip": timer = 1.8f; Say(TenantBook.Pick(d.Book.trip), 1.4f); Emote("star", 1.4f); squash = 0.9f; break;
                case "peek": timer = 2.4f; Say(TenantBook.Pick(d.Book.peek), 1.8f); break;
                case "wave": timer = 1.8f; Say(TenantBook.Pick(Type.greet), 1.6f); happyT = 1.8f; break;
                case "highfive": timer = 0.9f; squash = -0.5f; Emote("flash", 0.5f); if (partner && partner.P.x > b.p.x) Say("짝!", 0.9f); break;
                case "swat": timer = 1.4f; Say("냥냥!", 1f); break;
                case "follow": timer = 3f; Say("멍! 멍!", 1.2f); happyT = 3f; break;
                case "zoomies": timer = 2.5f; Say("우다다다!", 1.4f); happyT = 2.5f; break;
                case "wagfast": timer = 2f; happyT = 2f; Emote("heart", 1.5f); break;
                case "catstretch": timer = 2f; Say("냐아아~", 1.2f); break;
                case "groomfast": timer = 2f; break;
            }
        }

        void QuirkTick(float dt, Seg seg)
        {
            switch (quirk)
            {
                case "sneeze":
                    if (!quirkHit && qt > 0.9f) { quirkHit = true; Say("에취!!", 1f); squash = 0.8f; Emote("sweat", 1f); b.p.x -= dir * 6; }
                    break;
                case "swat":
                case "follow":
                    dir = d.Mouse.x > b.p.x ? 1 : -1;
                    if (quirk == "follow" && Mathf.Abs(d.Mouse.x - b.p.x) > 12 && !Walk(dt, seg, 1.8f)) return;
                    break;
                case "zoomies":
                    if (beat > 0.55f) { beat = 0; dir = -dir; squash = -0.4f; }
                    if (!Walk(dt, seg, 3.2f)) return;
                    break;
            }
            if (timer <= 0) Enter(State.Idle, Random.Range(T.idleMin, T.idleMax));
        }

        // ---------------------------------------------------------------- signature actions

        void StartSpecial()
        {
            Enter(State.Special, Random.Range(3f, 6f));
            switch (Type.special)
            {
                case "selfie": Say("찰칵!", 1.2f); Emote("flash", 0.3f); break;
                case "sing": Emote("note", timer); Say(TenantBook.Pick(Type.act)); break;
                case "pushup": break;
                case "wag": happyT = timer; Say(TenantBook.Pick(Type.act), 1.5f); break;
                case "dj":
                    Emote("note", timer);
                    Say(TenantBook.Pick(Type.act), 2f);
                    d.Party(this, 260, timer);
                    break;
                case "cook": Emote("steam", timer); Say(TenantBook.Pick(Type.act), 1.8f); break;
                case "game": Say(TenantBook.Pick(Type.act), 1.8f); break;
                case "deliver":
                    timer = 3f;
                    dir = Random.value < 0.5f ? -1 : 1;
                    Say(TenantBook.Pick(Type.act), 2f);
                    break;
                case "teleport":
                    timer = 0.9f;
                    phased = false;
                    Emote("flash", 0.9f);
                    Say(TenantBook.Pick(Type.act), 1f);
                    break;
                default: Say(TenantBook.Pick(Type.act)); break;
            }
        }

        void Special(float dt, Seg seg)
        {
            switch (Type.special)
            {
                case "deliver":
                    if (!Walk(dt, seg, 2.6f)) return;
                    break;
                case "dj":
                    if (beat > 0.35f) { beat = 0; squash = -0.3f; }
                    break;
                case "teleport":
                    if (!phased && timer < 0.45f && d.RandomSpot(out var spot))
                    {
                        phased = true;
                        b.p = spot - new Vector2(0, 30);
                        s = State.Fall;
                        b.Drop(Vector2.zero);
                        squash = -0.8f;
                        Emote("flash", 0.6f);
                        return;
                    }
                    break;
                case "pushup":
                    if (beat > 0.45f) { beat = 0; reps++; if (reps % 2 == 0) Say(Count[Mathf.Min(reps / 2 - 1, Count.Length - 1)], 0.6f); }
                    break;
                case "selfie":
                    if (beat > 1.3f) { beat = 0; Emote("flash", 0.3f); dir = -dir; }
                    break;
            }
            if (timer > 0) return;
            switch (Type.special)
            {
                case "pushup" when reps > 1: Say("후... 개운하다", 1.5f); Emote("sweat", 1.5f); break;
                case "cook": Say("다 됐어요~ 드세요!", 1.8f); d.Feed(this, 220); break;
                case "deliver": d.SpawnCoin(b.p - new Vector2(0, h), Type.rent * 2); Say("여기 두고 갈게요!", 1.6f); break;
                case "game":
                    bool win = Random.value < 0.5f;
                    Say(win ? "이겼다!! ㅋㅋㅋ" : "아 졌다!! 핵이야", 1.8f);
                    if (win) happyT = 1.5f; else Emote("anger", 1.5f);
                    break;
            }
            Enter(State.Idle, Random.Range(T.idleMin, T.idleMax));
        }

        /// <summary>Turns to the cursor when it comes close; the first time, reacts with a quirk. True if it started one.</summary>
        bool LookAtCursor()
        {
            var to = d.Mouse - (b.p - new Vector2(0, h / 2));
            if (to.sqrMagnitude > 70 * 70) return false;
            dir = to.x > 0 ? 1 : -1;
            if (lookCool > 0) return false;
            lookCool = 8;
            if (Type.IsCat) StartQuirk("swat");
            else if (Type.IsDog) StartQuirk("follow");
            else if (Random.value < 0.6f) StartQuirk("wave");
            else { Emote("?", 1f); return false; }
            return true;
        }

        /// <summary>Ballistic hop that peaks just above <paramref name="target"/> and lands on it.</summary>
        void Jump(Vector2 target)
        {
            const float over = 25f;
            float rise = b.p.y - target.y + over;
            float vy = -Mathf.Sqrt(2 * T.gravity * rise);
            float time = -vy / T.gravity + Mathf.Sqrt(2 * over / T.gravity);
            s = State.Fall;
            b.Drop(new Vector2((target.x - b.p.x) / time, vy));
            dir = target.x > b.p.x ? 1 : -1;
            squash = -0.7f;
            Say(TenantBook.Pick(Type.jump != null && Type.jump.Length > 0 ? Type.jump : new[] { "얍!", "영차!", "점프!" }), 1.2f);
        }

        // ---------------------------------------------------------------- presentation

        string PoseNow()
        {
            bool animal = Type.IsAnimal;
            int step = (int)(anim * (Type.walkSpeed > 50 || s == State.Flee ? 10 : 6)) % 2;
            switch (s)
            {
                case State.Walk:
                case State.Approach:
                case State.Flee: return step == 0 ? "walkA" : "walkB";
                case State.Climb: return (int)(b.p.y / 10) % 2 == 0 ? "climbA" : "climbB";
                case State.Held: return (int)(anim * 8) % 2 == 0 || animal ? "fall" : "wave";
                case State.Carried: return "fall";
                case State.Fall: return asleepInAir ? (animal ? "sleep" : "idle") : "fall";
                case State.Sleep: return animal ? "sleep" : "idle";
                case State.Dance: return animal ? (step == 0 ? "walkA" : "idle") : "wave";
                case State.Chat: return chatLine == null && timer > 2f && !animal ? "wave" : "idle";
                case State.Sit: return "sit";
                case State.Quirk:
                    switch (quirk)
                    {
                        case "stretch": return "fall";
                        case "trip": return "fall";
                        case "wave":
                        case "highfive": return "wave";
                        case "swat":
                        case "groomfast": return (int)(anim * 6) % 2 == 0 ? "groom" : "idle";
                        case "catstretch": return "stretch";
                        case "follow":
                        case "zoomies": return step == 0 ? "walkA" : "walkB";
                        case "wagfast": return (int)(anim * 12) % 2 == 0 ? "wag" : "idle";
                        default: return "idle";
                    }
                case State.Special:
                    switch (Type.special)
                    {
                        case "pushup": return reps % 2 == 0 ? "pushA" : "pushB";
                        case "groom": return (int)(anim * 3) % 2 == 0 ? "groom" : "idle";
                        case "phone":
                        case "game":
                        case "selfie": return "phone";
                        case "wag": return (int)(anim * 8) % 2 == 0 ? "wag" : "idle";
                        case "cook": return (int)(anim * 5) % 2 == 0 ? "wave" : "idle";
                        case "dj": return (int)(anim * 3) % 2 == 0 ? "wave" : "idle";
                        case "deliver": return (int)(anim * 10) % 2 == 0 ? "walkA" : "walkB";
                        default: return "idle";
                    }
                default: return "idle";
            }
        }

        Face FaceNow()
        {
            switch (s)
            {
                case State.Fall when asleepInAir:
                case State.Sleep: return Face.Sleep;
                case State.Fall:
                case State.Held:
                case State.Carried:
                case State.Flee:
                case State.Dizzy: return Face.Shock;
                case State.Chat when outcome == Outcome.Argue: return Face.Shock;
                case State.Chat:
                case State.Dance: return Face.Happy;
                case State.Special when Type.special == "yawn": return Face.Yawn;
                case State.Special when Type.special == "sing" || Type.special == "dj" || Type.special == "cook": return Face.Happy;
                case State.Quirk when quirk == "stretch": return Face.Yawn;
                case State.Quirk when quirk == "sneeze": return quirkHit ? Face.Shock : Face.Blink;
                case State.Quirk when quirk == "trip": return Face.Shock;
            }
            if (happyT > 0) return Face.Happy;
            return blinkT < 0 ? Face.Blink : Face.Normal;
        }

        void Render(float dt)
        {
            transform.position = Desk.W(b.p);
            anim += dt;
            body.sprite = Frame(PoseNow(), FaceNow());
            if (Mathf.Abs(b.v.x) > 1 && s == State.Fall) dir = b.v.x > 0 ? 1 : -1;
            body.flipX = dir < 0;

            // Squash on landing (wide and flat), stretch on take-off (tall and thin); idle breathing.
            float breathe = s == State.Idle || s == State.Sleep ? Mathf.Sin(anim * 3f) * 0.03f : 0;
            squashT.localScale = new Vector3(1 + 0.3f * squash, 1 - 0.3f * squash + breathe, 1);

            // Tumble when thrown, wriggle when held, wobble when dizzy, nod off asleep, fall flat when tripping.
            if (tumbling && s == State.Fall) spin -= b.v.x * dt * 0.8f;
            else spin = s switch
            {
                State.Held => Mathf.Sin(anim * 25) * 12,
                State.Carried => Mathf.Sin(anim * 3) * 14,
                State.Dizzy => Mathf.Sin(anim * 9) * 10,
                State.Sleep when !Type.IsAnimal => Mathf.Sin(anim * 1.5f) * 4 - 4,
                State.Dance => Mathf.Sin(anim * 11) * 8,
                State.Climb => Mathf.Sin(anim * 6) * 3,
                State.Quirk when quirk == "trip" => (qt < 1.2f ? -dir * Mathf.Min(qt * 600, 90) : -dir * 90 * Mathf.Max(0, 1 - (qt - 1.2f) * 3)),
                State.Quirk when quirk == "peek" => -dir * 22,
                State.Quirk when quirk == "stretch" => Mathf.Sin(anim * 4) * 5,
                _ => 0,
            };
            spinT.localRotation = Quaternion.Euler(0, 0, spin);

            zzz.enabled = s == State.Sleep || asleepInAir;
            if (zzz.enabled) zzz.transform.localPosition = new Vector3(w * 0.4f, h + 4 + Mathf.Sin(anim * 2) * 4, 0);
            emote.enabled = emoteT > 0;
            if (emote.enabled)
            {
                var spot = new Vector3(-w * 0.6f, h - 2, 0);
                if (EmoteIs("star")) spot = new Vector3(Mathf.Cos(anim * 8) * w * 0.5f, h + 2 + Mathf.Sin(anim * 8) * 4, 0);
                if (EmoteIs("steam")) spot = new Vector3(w * 0.7f, h * 0.6f + Mathf.Repeat(anim * 10, 8), 0);
                if (EmoteIs("note")) spot += new Vector3(0, Mathf.Abs(Mathf.Sin(anim * 4)) * 6, 0);
                emote.transform.localPosition = spot;
            }
        }

        void OnDestroy()
        {
            if (ladder) Destroy(ladder.gameObject);
        }
    }
}
