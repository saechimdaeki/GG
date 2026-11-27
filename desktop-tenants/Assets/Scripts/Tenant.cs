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
        enum State { Idle, Walk, Sleep, Chat, Dance, Special, Quirk, Sit, Dizzy, Flee, Approach, Climb, Fall, Held, Carried, Visit, Inside, Leave, Gather, Riding, Travel }

        /// <summary>What two tenants do once they've finished talking.</summary>
        public enum Outcome { None, Dance, HighFive, Argue, Hug, Rps, Tag, Share, Pet, Mirror, CoinTrick }

        static readonly string[] Count = { "하나!", "둘!", "셋!", "넷!", "다섯!", "여섯!", "일곱!", "여덟!", "아홉!", "열!" };
        static readonly string[] PersonQuirks = { "stretch", "sneeze", "juggle", "shadowbox", "spin", "jumpjack", "hiccup", "meditate", "lookup" };
        static readonly string[] CatQuirks = { "catstretch", "groomfast", "tailchase", "loaf", "pounce" };
        static readonly string[] DogQuirks = { "zoomies", "wagfast", "rollover", "dig", "howl" };
        static readonly string[] OtherQuirks = { "stretch", "sneeze", "spin", "jumpjack", "hiccup" };
        static readonly string[] Hands = { "가위!", "바위!", "보!" };
        static Dictionary<string, Sprite> emoteSprites;
        static Sprite ladderTile;

        Desk d;
        Tuning T => d.Tune;
        public TenantType Type { get; private set; }
        readonly Body b = new Body();
        State s = State.Walk;
        Vector2 grab, holdStart, climbAnchor;
        int dir = -1, pokes, reps, dragDisplay, beforeChatDirection;
        bool asleepInAir, tumbling, fleeOnLand, phased, quirkHit;
        Outcome outcome;
        string chatLine, quirk;
        float chatDelay, climbX;
        float evacT, boostT, petCool, visitX, leaveX;
        Building visiting;

        /// <summary>A guest (celebrity) rather than a resident.</summary>
        public bool Visitor { get; set; }
        float timer, anim, chatCool, holdT, talkCool, rentT, pokeT, lookCool, blinkT = 3, emoteT, happyT, squash, spin, beat, qt;
        Seg climbTo;
        Coin prey;
        Tenant chase, partner;
        int hand;       // rock-paper-scissors pick
        bool pairLead;  // started the pair action (says the opening line)
        string laterLine; float laterT;

        /// <summary>Says <paramref name="line"/> after <paramref name="delay"/> seconds (a punchline after a beat).</summary>
        void DelayedSay(string line, float delay) { laterLine = line; laterT = delay; }

        /// <summary>Stationary residents who can be nudged to make room.</summary>
        public bool AllowsSpacing => b.Grounded && (s == State.Idle || s == State.Sleep || s == State.Sit);
        readonly List<(Seg to, float lo, float hi)> climbOptions = new List<(Seg, float, float)>();

        readonly Dictionary<(string, Face), Sprite> frames = new Dictionary<(string, Face), Sprite>();
        Dictionary<char, Color> pal;
        Transform squashT, spinT;
        SpriteRenderer body, emote, zzz, ladder;
        Bubble bubble;
        float w, h, size = 1;
        Tenant carrier;
        bool hasRider;
        float tickleCool;

        public Vector2 P => b.p;
        public int On => b.on;
        public bool Held => s == State.Held;
        public Bubble Bubble => bubble;
        /// <summary>Standing around (not talking, dancing, climbing or being carried): may be nudged aside.</summary>
        public bool Free => b.Grounded && (s == State.Idle || s == State.Walk || s == State.Sleep || s == State.Sit || s == State.Quirk || s == State.Special || s == State.Gather);

        /// <summary>Shuffle sideways by <paramref name="dx"/> points, staying on the current edge.</summary>
        public void Nudge(float dx)
        {
            var moved = b.p + new Vector2(dx, 0);
            if (d.SegAt(b.on, moved, out var seg)) b.p.x = Mathf.Clamp(moved.x, seg.x0 + 3, seg.x1 - 3);
        }

        public float Height => h;
        public float Width => w;
        public bool Sitting => s == State.Sit;
        public bool HasRider => hasRider;

        /// <summary>The guard saw them dangling their legs over an edge.</summary>
        public void Scold()
        {
            if (s != State.Sit) return;
            Enter(State.Idle, 1.2f);
            Say(TenantBook.Pick(d.Book.scolded), 1.6f);
            Emote("sweat", 1.5f);
        }

        /// <summary>The cursor wiggled over them.</summary>
        public void Tickle()
        {
            if (tickleCool > 0 || !b.Grounded || s == State.Inside) return;
            tickleCool = 3;
            StartQuirk("tickle");
            Say(Type.IsCat ? "냐하하냥" : Type.IsDog ? "헥헥헥!" : Type.IsRobot ? "삐-빅-빅-빅" : TenantBook.Pick(d.Book.tickle), 1.4f);
        }

        /// <summary>Picked up by the magician's trick: reappear somewhere else.</summary>
        public void Vanish(Vector2 spot)
        {
            if (!b.Grounded || s == State.Inside) return;
            HideLadder();
            b.p = spot - new Vector2(0, 30);
            s = State.Fall;
            b.Drop(Vector2.zero);
            Emote("flash", 0.8f);
            Say(Type.IsAnimal ? null : "어? 내가 왜 여기?", 1.6f);
        }

        /// <summary>Someone came to comfort the crying baby.</summary>
        public void Soothe()
        {
            happyT = 2.5f;
            Emote("heart", 2f);
            Say("까르르!", 1.4f);
        }

        /// <summary>Can be picked up by a helicopter or UFO right now.</summary>
        public bool Carriable => b.Grounded && s != State.Held && s != State.Carried && s != State.Inside && s != State.Leave && evacT <= 0;

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

        /// <summary>The cursor rested on them: enjoy it, sometimes leave a tip.</summary>
        public void Pet()
        {
            if (petCool > 0 || s == State.Inside || s == State.Held || s == State.Carried) return;
            petCool = 4;
            happyT = 2.5f;
            squash = -0.35f;
            Emote("heart", 2f);
            Say(Type.IsCat ? "골골골~" : Type.IsDog ? "헥헥 좋아요!" : TenantBook.Pick(d.Book.pet), 1.6f);
            if (Random.value < d.Tune.tipChance) { d.SpawnCoinAt(b.p - new Vector2(0, h), Mathf.Max(100, Type.rent / 4)); Say("이거 팁이에요!", 1.4f); }
        }

        /// <summary>A line and emote from outside (events): ignored while inside a shop.</summary>
        public void React(string line, string emoteName, float seconds = 1.8f)
        {
            if (s == State.Inside) return;
            if (line != null) Say(line, seconds);
            if (emoteName != null) Emote(emoteName, seconds);
        }

        /// <summary>Earthquake: everyone standing gets bounced.</summary>
        public void Quake()
        {
            if (!b.Grounded || s == State.Inside) return;
            HideLadder();
            s = State.Fall;
            b.Drop(new Vector2(Random.Range(-120f, 120f), Random.Range(-320f, -180f)));
            tumbling = Random.value < 0.5f;
            Say(Type.IsCat ? "냐아앙!" : Type.IsDog ? "깨갱!" : Random.value < 0.5f ? "지진이다!" : "으아아 흔들려!", 1.4f);
            Emote("!", 1f);
        }

        /// <summary>A guest's visit is over: walk to <paramref name="x"/> on the floor and go.</summary>
        public void LeaveTo(float x)
        {
            leaveX = x;
            if (s == State.Inside) ComeOut();
            if (b.Grounded) Enter(State.Leave, 30);
            else leavePending = true;
        }

        bool leavePending;

        // ---------------------------------------------------------------- events from the sky and the town

        /// <summary>Hit by lightning: dazed and smoking.</summary>
        public void Struck(string line)
        {
            if (!b.Grounded || s == State.Inside) return;
            HideLadder();
            Enter(State.Dizzy, 2.4f);
            squash = 0.9f;
            Emote("star", 2.4f);
            Say(Type.IsCat ? "냐갸갹!" : Type.IsDog ? "깨개갱!" : line, 1.8f);
        }

        /// <summary>A gust lifts them off whatever they stood on.</summary>
        public void BlownAway(float wind)
        {
            if (!b.Grounded || s == State.Inside || s == State.Gather) return;
            HideLadder();
            s = State.Fall;
            b.Drop(new Vector2(wind * 0.9f, -180));
            tumbling = true;
            Say(Type.IsCat ? "냐아아~" : Type.IsDog ? "멍멍멍!" : "으아아 바람!", 1.4f);
            Emote("!", 1f);
        }

        float gatherX, gatherSpeed;
        bool gatherDance, gatherArrived;
        string gatherLine, gatherEndLine, gatherEmote;

        /// <summary>Reached the spot of the current gathering.</summary>
        public bool Arrived => s == State.Gather && gatherArrived;

        /// <summary>
        /// Go to <paramref name="x"/> on the floor for an event (ice cream van, meeting, race, busking),
        /// say <paramref name="line"/> on arrival, stay until <paramref name="seconds"/> are up, then say <paramref name="endLine"/>.
        /// </summary>
        public void Gather(float x, float seconds, string line, string emoteName, float speedMul = 1.3f, bool dance = false, string endLine = null, float spread = 40)
        {
            if (s == State.Held || s == State.Carried || s == State.Inside || s == State.Travel ||
                touring || arrivalFloor != Body.None || Visitor || d.DisplayAt(P) != 0) return;
            HideLadder();
            if (s == State.Climb) { s = State.Fall; b.Drop(Vector2.zero); }
            gatherX = x + Random.Range(-spread, spread);
            gatherSpeed = speedMul;
            gatherDance = dance;
            gatherArrived = false;
            gatherLine = Type.IsAnimal ? null : line;
            gatherEndLine = Type.IsAnimal ? null : endLine;
            gatherEmote = emoteName;
            if (b.Grounded) Enter(State.Gather, seconds);
            else { gatherPending = seconds; }
        }

        float gatherPending;

        void GatherTick(float dt, Seg seg)
        {
            if (d.DisplayAt(P) != 0) { Enter(State.Idle, 0.8f); return; }
            if (seg.win != Surfaces.Floor)
            {
                // Hop down to street level first.
                dir = gatherX > b.p.x ? 1 : -1;
                s = State.Fall;
                b.Drop(new Vector2(dir * 120, -220));
                gatherPending = timer;
                return;
            }
            if (!gatherArrived)
            {
                float dx = gatherX - b.p.x;
                dir = dx > 0 ? 1 : -1;
                b.p.x += dir * Mathf.Min(Mathf.Abs(dx), Mathf.Max(Type.walkSpeed, 30) * gatherSpeed * dt);
                if (Mathf.Abs(dx) < 3)
                {
                    gatherArrived = true;
                    if (gatherLine != null) Say(gatherLine, 2f);
                    if (gatherEmote != null) Emote(gatherEmote, Mathf.Min(timer, 4f));
                }
            }
            else if (gatherDance && beat > 0.3f) { beat = 0; dir = -dir; squash = -0.35f; }
            if (timer <= 0)
            {
                if (gatherEndLine != null) Say(gatherEndLine, 1.8f);
                Enter(State.Idle, 1.5f);
            }
        }

        /// <summary>Fire drill: everyone heads down to the front door for <paramref name="seconds"/>.</summary>
        public void Evacuate(float seconds)
        {
            if (s == State.Carried || s == State.Held || s == State.Travel || arrivalFloor != Body.None) return;
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

        public bool CanChat => !touring && arrivalFloor == Body.None && (s == State.Walk || s == State.Idle) && b.Grounded && chatCool <= 0;
        public Rect Bounds => s == State.Inside ? Rect.zero : new Rect(b.p.x - w / 2 - 4, b.p.y - h - 4, w + 8, h + 8);

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
            size = type.scale > 0 ? type.scale : 1;
            float hu = idle.Length * T.pixel; // unscaled height, for transforms under the scaled squash node
            w = idle[0].Length * T.pixel * size;
            h = hu * size;

            // root (feet) → squash (scales about the feet) → spin (rotates about the middle) → body
            squashT = new GameObject("Squash").transform;
            squashT.SetParent(transform, false);
            spinT = new GameObject("Spin").transform;
            spinT.SetParent(squashT, false);
            spinT.localPosition = new Vector3(0, hu / 2, 0);
            body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(spinT, false);
            body.transform.localPosition = new Vector3(0, -hu / 2, 0);
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
            arrivalFloor = Body.None;
            dragDisplay = d.DisplayAt(mouse, 0);
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
            beforeChatDirection = s == State.Walk ? dir : 0;
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
        public void Scare(Vector2 from, string line = null)
        {
            if (!b.Grounded || s == State.Flee || s == State.Inside) return;
            Say(line != null && !Type.IsAnimal ? line : Type.IsCat ? "하아악!" : TenantBook.Pick(Type.poke), 1.4f);
            Emote("!", 1f);
            Flee(from);
        }

        // ---------------------------------------------------------------- simulation

        public void Tick(float dt)
        {
            tourCooldown -= dt;
            chatCool -= dt;
            talkCool -= dt;
            pokeT -= dt;
            evacT -= dt;
            boostT -= dt;
            petCool -= dt;
            tickleCool -= dt;
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
                case State.Travel: CrossDisplay(dt); break;
                case State.Carried: break;
                case State.Riding: RidingTick(dt); break;
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
            int currentDisplay = d.DisplayAt(d.Mouse);
            if (currentDisplay >= 0 && dragDisplay != currentDisplay)
            {
                dragDisplay = currentDisplay;
                Debug.Log($"[desk] dragged {Type.name} to display {currentDisplay + 1}");
            }
            if (holdT > 0.3f && !bubble.Visible) { Say(TenantBook.Pick(Type.held), 1.5f); Emote("sweat", 1.5f); }
            if (d.MouseDown) return;
            if (currentDisplay < 0)
            {
                var lastDisplay = d.DisplayBounds(dragDisplay);
                var bounds = lastDisplay.Bounds;
                b.p.x = Mathf.Clamp(b.p.x, bounds.xMin + 24, bounds.xMax - 24);
                b.p.y = Mathf.Clamp(b.p.y, bounds.yMin + h, lastDisplay.Floor.y);
                b.v = Vector2.zero;
            }
            bool poke = holdT < 0.25f && (d.Mouse - holdStart).magnitude < 6;
            s = State.Fall;
            squash = -0.6f;
            if (poke) Poked();
            else if ((d.Town.DoorNear(b.p, 26) ?? d.HomeNear(b.p, 26)) is Building door && b.v.magnitude < 600)
            {
                // Dropped at a shop door: in they go.
                b.p = new Vector2(door.DoorX, d.FloorY);
                b.Stand(d, d.FloorSeg);
                GoInside(door);
            }
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
            // Coming down on someone's head: ride on their shoulders for a bit.
            if (b.v.y > 0 && !asleepInAir && d.HeadAt(this, b.p + b.v * dt) is Tenant under)
            {
                carrier = under;
                under.CarryRider(true);
                s = State.Riding;
                timer = Random.Range(4f, 7f);
                tumbling = false;
                squash = 0.5f;
                happyT = timer;
                Say(Type.IsAnimal ? TenantBook.Pick(Type.land) : TenantBook.Pick(d.Book.piggyRider), 1.8f);
                return;
            }
            switch (b.Fall(d, dt, h))
            {
                case Body.FallResult.Landed:
                    squash = Mathf.Clamp(impact / 1200f, 0.3f, 1f);
                    tumbling = false;
                    if (asleepInAir) { Enter(State.Sleep, Random.Range(T.sleepMin, T.sleepMax)); asleepInAir = false; break; }
                    if (fleeOnLand) { fleeOnLand = false; Flee(d.Mouse); break; }
                    if (arrivalFloor != Body.None)
                    {
                        if (b.on == arrivalFloor)
                        {
                            Enter(State.Walk, 0);
                            break;
                        }
                        if (d.TryDisplayByFloor(arrivalFloor, out var street) && street.Bounds.Contains(P))
                        {
                            b.Drop(Vector2.zero);
                            b.p.y += 2;
                            break;
                        }
                        arrivalFloor = Body.None;
                    }
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

        /// <summary>Someone climbed on (or off) this tenant's shoulders.</summary>
        public void CarryRider(bool on)
        {
            hasRider = on;
            if (!on) return;
            Say(Type.IsAnimal ? TenantBook.Pick(Type.poke) : TenantBook.Pick(d.Book.piggyCarrier), 1.8f);
            Emote("sweat", 2f);
        }

        void RidingTick(float dt)
        {
            timer -= dt;
            if (!carrier || !carrier.Free || timer <= 0)
            {
                if (carrier) carrier.CarryRider(false);
                carrier = null;
                s = State.Fall;
                b.Drop(new Vector2(dir * 90, -260));
                squash = -0.5f;
                return;
            }
            b.p = carrier.P - new Vector2(0, carrier.Height - 3);
            dir = carrier.Dir;
        }

        public int Dir => dir;

        bool touring, approachingSeam, tourCrossesGap;
        float tourDeparture, tourCooldown;
        Vector2 tourArrival, tourEdge;
        int tourSourceFloor, arrivalFloor = Body.None;
        float arrivalTarget;

        void Flee(Vector2 from)
        {
            Enter(State.Flee, 2.2f);
            dir = from.x > b.p.x ? -1 : 1;
        }

        void Enter(State next, float seconds)
        {
            s = next;
            touring = false;
            if (next != State.Walk) arrivalFloor = Body.None;
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

            if (leavePending) { leavePending = false; Enter(State.Leave, 30); }
            if (gatherPending > 0) { float left = gatherPending; gatherPending = 0; Enter(State.Gather, left); }
            if (evacT > 0 && s != State.Inside) { Evac(dt, seg); return; }

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
                    if (TryStartTour(seg)) break;
                    float sleepChance = d.Night ? Mathf.Max(Type.sleepChance, 0.6f) : Type.sleepChance;
                    if (d.Raining) sleepChance = 0;
                    var shop = seg.win == Surfaces.Floor && !Visitor ? d.Town.PickVisit(Type) : null;
                    if (shop != null && d.Raining && shop.Def.HasDoor) { StartVisit(shop, Type.IsAnimal ? null : "비 피하자!"); break; }
                    if (shop != null && Random.value < T.visitChance) { StartVisit(shop); break; }
                    // Late at night people on the street go home to sleep.
                    if (d.Night && seg.win == Surfaces.Floor && !Visitor && Random.value < 0.4f) { StartVisit(d.Home, Type.IsAnimal ? null : "집에 가서 자야지"); break; }
                    if (!string.IsNullOrEmpty(Type.treat) && Random.value < 0.15f && d.NearestPerson(this, 90) is Tenant friend)
                    {
                        dir = friend.P.x > b.p.x ? 1 : -1;
                        Say(Type.treat, 1.6f);
                        friend.Treat();
                        Enter(State.Idle, 2f);
                        break;
                    }
                    float roll = Random.value;
                    if (roll < sleepChance) Enter(State.Sleep, Random.Range(T.sleepMin, T.sleepMax));
                    else if ((roll -= sleepChance) < T.specialChance) StartSpecial();
                    else if ((roll -= T.specialChance) < T.quirkChance) StartQuirk(Pick(Type.IsCat ? CatQuirks : Type.IsDog ? DogQuirks : Type.IsAnimal ? OtherQuirks : PersonQuirks));
                    else { Enter(State.Walk, Random.Range(T.walkMin, T.walkMax)); dir = Random.value < 0.5f ? -1 : 1; Chatter(); }
                    break;
                case State.Sleep:
                    if (timer <= 0 || d.Raining) Enter(State.Idle, Random.Range(T.idleMin, T.idleMax));
                    break;
                case State.Walk: WalkTick(dt, seg); break;
                case State.Visit: VisitTick(dt, seg); break;
                case State.Inside:
                    if (timer <= 0 || evacT > 0) { ComeOut(); Enter(State.Idle, 1.2f); }
                    break;
                case State.Leave: LeaveTick(dt, seg); break;
                case State.Gather: GatherTick(dt, seg); break;
            }
        }

        // ---------------------------------------------------------------- town: shops and the park

        /// <summary>Head for a shop door (or a spot in the park).</summary>
        void StartVisit(Building place, string line = null)
        {
            visiting = place;
            visitX = place.Def.HasDoor ? place.DoorX : place.X + Random.Range(-place.W * 0.3f, place.W * 0.3f);
            Enter(State.Visit, Mathf.Abs(visitX - b.p.x) / (Type.walkSpeed * 1.2f) + 4f);
            dir = visitX > b.p.x ? 1 : -1;
            if (line != null) Say(line, 1.5f);
        }

        void VisitTick(float dt, Seg seg)
        {
            if (seg.win != Surfaces.Floor || visiting == null) { Enter(State.Idle, 1f); return; }
            float dx = visitX - b.p.x;
            dir = dx > 0 ? 1 : -1;
            b.p.x += dir * Mathf.Min(Mathf.Abs(dx), Type.walkSpeed * 1.2f * dt);
            if (Mathf.Abs(dx) > 2) { if (timer <= 0) Enter(State.Idle, 1f); return; }
            if (visiting.Def.HasDoor) GoInside(visiting);
            else
            {
                // The park: a picnic on the spot.
                StartQuirk("picnic");
                Say(TenantBook.Pick(visiting.Def.visit), 1.8f);
            }
        }

        /// <summary>Disappears through <paramref name="place"/>'s door for a few seconds.</summary>
        void GoInside(Building place)
        {
            visiting = place;
            b.p.x = place.DoorX;
            Enter(State.Inside, place.Def.id == "home" ? Random.Range(10f, 18f) : Random.Range(4f, 8f));
            bubble.Hide();
        }

        /// <summary>Steps back out of the shop with whatever they went in for.</summary>
        void ComeOut()
        {
            var place = visiting;
            visiting = null;
            if (place == null) return;
            squash = -0.4f;
            happyT = 1.5f;
            Say(Type.IsAnimal ? TenantBook.Pick(Type.idle) : TenantBook.Pick(place.Def.visit), 1.8f);
            switch (place.Def.id)
            {
                case "cafe": boostT = T.coffeeBoost; Emote("flash", 0.8f); break;
                case "gym": Emote("sweat", 1.8f); break;
                case "karaoke": Emote("note", 2.5f); break;
                default: Emote("heart", 1.5f); break;
            }
            if (place.Def.income > 0) d.SpawnCoinAt(new Vector2(place.DoorX, d.FloorY - 20), place.Def.income * place.Level / 2);
        }

        void LeaveTick(float dt, Seg seg)
        {
            if (d.TryDisplayByFloor(seg.win, out var street) && street.Floor.win != Surfaces.Floor)
            {
                d.Remove(this);
                return;
            }
            if (seg.win != Surfaces.Floor) { s = State.Fall; b.Drop(new Vector2(dir * 100, -200)); leavePending = true; return; }
            float dx = leaveX - b.p.x;
            dir = dx > 0 ? 1 : -1;
            b.p.x += dir * Mathf.Min(Mathf.Abs(dx), Type.walkSpeed * 1.5f * dt);
            if (Mathf.Abs(dx) < 4 || timer <= 0) d.Remove(this);
        }

        /// <summary>Fire drill: off any ledge, then run to the door and wait there, sweating.</summary>
        void Evac(float dt, Seg seg)
        {
            float safeX = d.TryDisplayByFloor(seg.win, out var street) && seg.win != Surfaces.Floor
                ? street.Bounds.center.x : d.Door.x;
            float dx = safeX - b.p.x;
            dir = dx > 0 ? 1 : -1;
            if (!d.TryDisplayByFloor(seg.win, out _))
            {
                s = State.Fall;
                b.Drop(new Vector2(dir * 120, -220));
                return;
            }
            s = Mathf.Abs(dx) > 50 ? State.Flee : State.Idle;
            if (s == State.Flee) b.p.x += dir * Mathf.Max(Type.walkSpeed, 40) * 2.2f * dt;
            if (evacT <= dt) { Enter(State.Idle, 1f); Say(Type.IsAnimal ? null : "휴~ 훈련이었구나", 1.6f); }
        }

        bool TryStartTour(Seg floor)
        {
            if (Visitor || tourCooldown > 0 || Random.value >= T.displayTourChance ||
                !d.TryDisplayRoute(floor, P.x, out tourDeparture, out tourArrival, out tourCrossesGap)) return false;
            Enter(State.Walk, 0);
            touring = true;
            tourCooldown = T.displayTourCooldown;
            Say(Type.IsAnimal ? null : "옆 화면에 다녀올게요!", 2f);
            return true;
        }

        void Tour(float dt, Seg floor)
        {
            dir = tourDeparture >= P.x ? 1 : -1;
            b.p.x = Mathf.MoveTowards(P.x, tourDeparture, Mathf.Max(Type.walkSpeed, 30) * 2.5f * dt);
            if (Mathf.Abs(P.x - tourDeparture) > 1) return;
            int destination = d.DisplayAt(tourArrival);
            if (destination < 0 || destination == d.DisplayAt(P))
            {
                Enter(State.Idle, 1f);
                return;
            }

            tourSourceFloor = floor.win;
            if (tourCrossesGap) { FinishTour(destination); return; }
            bool sideBySide = Mathf.Abs(tourArrival.x - tourDeparture) > 5;
            tourEdge = sideBySide ? new Vector2(P.x, tourArrival.y) : tourArrival;
            approachingSeam = true;
            if (Mathf.Abs(tourEdge.y - P.y) > h)
            {
                ladder.enabled = true;
                ladder.transform.position = Desk.W(new Vector2(P.x, Mathf.Max(P.y, tourEdge.y)), 0.5f);
                ladder.size = new Vector2(7 * T.pixel, Mathf.Abs(P.y - tourEdge.y));
            }
            s = State.Travel;
            b.Drop(Vector2.zero);
        }

        void CrossDisplay(float dt)
        {
            int destination = d.DisplayAt(tourArrival);
            if (destination < 0 || !d.WinPos(tourSourceFloor, out _))
            {
                HideLadder();
                s = State.Fall;
                return;
            }

            if (approachingSeam)
            {
                b.p = Vector2.MoveTowards(P, tourEdge, T.climbSpeed * 1.5f * dt);
                if (P != tourEdge) return;
                approachingSeam = false;
                HideLadder();
            }
            if (P != tourArrival)
            {
                dir = tourArrival.x > P.x ? 1 : -1;
                b.p = Vector2.MoveTowards(P, tourArrival, T.climbSpeed * dt);
                if (P != tourArrival) return;
            }
            FinishTour(destination);
        }

        void FinishTour(int destination)
        {
            var street = d.DisplayBounds(destination);
            arrivalFloor = street.Floor.win;
            arrivalTarget = street.ArrivalWalkTarget(tourArrival.x, Random.Range(170f, 300f));
            b.p = tourArrival;
            s = State.Fall;
            touring = false;
            tourCooldown = T.displayTourCooldown;
            b.Drop(Vector2.zero);
            Emote("!", 1f);
            Debug.Log($"[desk] {Type.name} crossed from floor {tourSourceFloor} to display {destination + 1}");
        }

        void WalkTick(float dt, Seg seg)
        {
            if (touring) { Tour(dt, seg); return; }
            if (arrivalFloor == seg.win)
            {
                dir = arrivalTarget > P.x ? 1 : -1;
                b.p.x = Mathf.MoveTowards(P.x, arrivalTarget, Mathf.Max(32f, Type.walkSpeed) * 1.7f * dt);
                if (Mathf.Abs(P.x - arrivalTarget) <= 1)
                {
                    Debug.Log($"[desk] {Type.name} cleared arrival edge on floor {arrivalFloor}");
                    Enter(State.Idle, 0.7f);
                }
                return;
            }
            if (TryGoUp(dt, seg)) return;
            if (Type.IsCat) HuntCoins(seg);
            if (Type.IsDog) ChaseCats();
            if ((Type.IsDog && !chase) || Type.IsRobot) FetchCoins();
            if (!Type.IsAnimal && Random.value < T.tripChance * dt) { StartQuirk("trip"); return; }
            float speed = (d.Raining && !Type.IsDog ? 1.7f : 1f) * (boostT > 0 ? 1.6f : 1f);
            if (!Walk(dt, seg, speed)) return;
            if (timer <= 0) { Enter(State.Idle, Random.Range(T.idleMin, T.idleMax)); Chatter(); }
        }

        /// <summary>
        /// Heads for a higher edge: a jump when it's close, otherwise (people only) walk to a spot under it
        /// and put up a ladder. The floor is boring, so tenants on it try twice as often.
        /// </summary>
        bool TryGoUp(float dt, Seg seg)
        {
            float floorBoost = d.TryDisplayByFloor(seg.win, out _) ? 2f : 1f;
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
                case Outcome.Hug: StartPair("hug", "hug"); break;
                case Outcome.Rps: StartPair("rps", "rps"); break;
                case Outcome.Tag: StartPair("tagIt", "tagRun"); break;
                case Outcome.Share: StartPair("share", "thanks"); break;
                case Outcome.Mirror: StartPair("mirror", "mirror"); break;
                case Outcome.CoinTrick: StartPair("coinTrick", "coinWatch"); break;
                case Outcome.Pet:
                    if (Type.IsAnimal) StartPair("purr", "petting");
                    else StartPair("petting", "purr");
                    break;
                case Outcome.Argue:
                    Enter(State.Walk, Random.Range(2f, 4f));
                    dir = partner && partner.P.x > b.p.x ? -1 : 1;
                    Say(Type.IsCat ? "흥냥" : Type.IsDog ? "왈!" : "흥!", 1.2f);
                    Emote("anger", 1.5f);
                    break;
                default:
                    Enter(State.Walk, Random.Range(T.walkMin, T.walkMax));
                    dir = beforeChatDirection != 0 ? beforeChatDirection : -dir;
                    break;
            }
        }

        /// <summary>Walks along <paramref name="seg"/>; at an edge maybe peeks, sits, hops off or turns. False if it left the ground.</summary>
        bool Walk(float dt, Seg seg, float speedMul)
        {
            b.p.x += dir * Type.walkSpeed * speedMul * dt;
            if (b.p.x >= seg.x0 + 3 && b.p.x <= seg.x1 - 3) return true;
            b.p.x = Mathf.Clamp(b.p.x, seg.x0 + 3, seg.x1 - 3);
            bool ledge = !d.TryDisplayByFloor(seg.win, out _);  // window tops and the roof
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

        /// <summary>Dogs and the robot bring coins on their edge back to the money sign.</summary>
        void FetchCoins()
        {
            if (!prey || prey.Collecting) prey = d.CoinNear(b.p, b.on, 220);
            if (!prey || prey.On != b.on) { prey = null; return; }
            float dx = prey.P.x - b.p.x;
            dir = dx > 0 ? 1 : -1;
            if (Mathf.Abs(dx) > 10) return;
            prey.Collect();
            prey = null;
            squash = -0.4f;
            happyT = 1.2f;
            Say(Type.IsDog ? "멍! (물어옴)" : TenantBook.Pick(Type.act), 1.2f);
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

        /// <summary>
        /// Starts a two-person action: this tenant does <paramref name="mine"/> and pulls the partner (still
        /// in the chat) into <paramref name="theirs"/> at the same moment, so the halves stay in step.
        /// </summary>
        void StartPair(string mine, string theirs)
        {
            bool join = partner && partner.s == State.Chat && partner.partner == this;
            if (join) { partner.pairLead = false; partner.StartQuirk(theirs); }
            pairLead = true;
            StartQuirk(mine);
            if (join)
            {
                partner.timer = timer;
                if (mine == "mirror" || mine == "coinTrick")
                    Debug.Log($"[encounter] {mine}: {Type.name}, {partner.Type.name}");
            }
        }

        bool PartnerDoing(string q) => partner && partner.s == State.Quirk && partner.quirk == q;

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
                case "picnic": timer = Random.Range(4f, 7f); happyT = timer; Emote("heart", 2f); break;
                case "tickle": timer = 1.2f; happyT = 1.5f; squash = -0.4f; break;
                // ---- solo, people
                case "juggle": timer = 3.5f; Say(Pick(new[] { "저글링 보실래요?", "하나 둘 셋~", "떨어뜨리면 안 돼..." }), 1.6f); Emote("star", 3f); break;
                case "shadowbox": timer = 3f; Say(Pick(new[] { "슉! 슉슉!", "원투! 원투!", "오늘도 단련!" }), 1.4f); break;
                case "spin": timer = 2f; Say(Pick(new[] { "빙그르르~", "돌아라 돌아라~", "삐릭- 회전 모드" }), 1.2f); break;
                case "jumpjack": timer = 3f; Say(Pick(new[] { "하나! 둘! 하나! 둘!", "국민체조 시작!", "몸풀기~" }), 1.6f); break;
                case "hiccup": timer = 3.2f; beat = 0; break;
                case "meditate": timer = Random.Range(4f, 6f); Say(Pick(new[] { "옴~", "마음의 평화...", "(명상 중)" }), 1.8f); break;
                case "lookup": timer = 2.6f; Say(d.Raining ? Pick(new[] { "비 언제 그치지", "우산 챙길걸..." }) : Pick(new[] { "구름 예쁘다~", "하늘 맑다", "저 구름 고양이 닮았다" }), 1.8f); Emote("?", 1.2f); break;
                case "yoyo": timer = 3.5f; Say(TenantBook.Pick(Type.act), 1.6f); Emote("yoyo", timer); break;
                case "mend": timer = 3f; Say(TenantBook.Pick(Type.act), 1.6f); break;
                case "flute": timer = 4f; Say(TenantBook.Pick(Type.act), 1.6f); Emote("note", 4f); break;
                case "coach": timer = 3.2f; Say(TenantBook.Pick(Type.act), 1.6f); Emote("sweat", 2f); break;
                // ---- solo, animals
                case "tailchase": timer = 2.4f; Say("냥냥냥?!", 1.2f); Emote("star", 2f); break;
                case "loaf": timer = Random.Range(4f, 7f); Say("(식빵 굽는 중)", 1.6f); happyT = timer; break;
                case "pounce": timer = 1.6f; quirkHit = false; Say("(엉덩이 씰룩씰룩)", 1f); break;
                case "rollover": timer = 1.6f; Say("(데굴)", 1f); Emote("heart", 1.4f); happyT = 1.6f; break;
                case "dig": timer = 2.4f; Say("(파바바박)", 1.2f); Emote("sweat", 2f); break;
                case "howl": timer = 2.4f; Say("아우우우~~", 2f); Emote("note", 2f); break;
                // ---- pairs (see StartPair)
                case "mirror": timer = 2.8f; if (pairLead) Say("내 동작 따라 해 봐!", 1.3f); break;
                case "coinTrick": timer = 3f; Say("어느 손에 동전이 있게?", 1.5f); break;
                case "coinWatch": timer = 3f; Emote("?", 1.5f); break;
                case "hug": timer = 3f; quirkHit = false; if (pairLead) Say(Pick(new[] { "이리 와~ 안아줄게", "오늘 수고했어!", "보고 싶었어~" }), 1.6f); break;
                case "rps": timer = 3.4f; quirkHit = false; hand = Random.Range(0, 3); if (pairLead) Say("가위바위보 하자!", 1.2f); break;
                case "tagIt": timer = 5f; Say("술래잡기! 내가 술래~", 1.4f); break;
                case "tagRun": timer = 5f; Emote("!", 1f); break;
                case "share": timer = 2.4f; Say(Pick(new[] { "이거 같이 먹을래?", "간식 나눠줄게~", "하나 먹어봐!" }), 1.6f); Emote("steam", 1.6f); break;
                case "thanks": timer = 2.4f; quirkHit = false; break;
                case "petting": timer = 2.8f; Say(Pick(new[] { "착하다~ 착해", "쓰담쓰담", "누가 이렇게 귀여워?" }), 1.6f); break;
                case "purr": timer = 2.8f; happyT = 2.8f; Emote("heart", 2.4f); if (Type.IsCat) Say("그르릉~", 1.4f); else if (Type.IsDog) Say("(꼬리 붕붕)", 1.4f); break;
            }
        }

        void QuirkTick(float dt, Seg seg)
        {
            if (laterLine != null && (laterT -= dt) <= 0) { Say(laterLine, 1.4f); laterLine = null; }
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
                case "spin":
                case "tailchase":
                    if (beat > 0.12f) { beat = 0; dir = -dir; }
                    break;
                case "jumpjack":
                    squash = Mathf.Sin(qt * 12) * 0.4f;
                    break;
                case "hiccup":
                    if (beat > 0.8f) { beat = 0; squash = 0.6f; Say("딸꾹!", 0.6f); }
                    break;
                case "shadowbox":
                    b.p.x += Mathf.Sin(qt * 16) * 20 * dt;
                    break;
                case "pounce":
                    if (!quirkHit && qt > 0.8f)
                    {
                        quirkHit = true;
                        s = State.Fall;
                        b.Drop(new Vector2(dir * 170, -200));
                        squash = -0.6f;
                        Say("냥!", 0.8f);
                        return;
                    }
                    break;
                case "dig":
                    if (!quirkHit && qt > 2f)
                    {
                        quirkHit = true;
                        if (Random.value < 0.3f) { d.SpawnCoin(b.p - new Vector2(0, h * 0.5f), Mathf.Max(1, Type.rent / 2)); Say("멍! (뭔가 찾았다)", 1.4f); Emote("!", 1f); }
                    }
                    break;
                case "hug":
                    if (!partner) break;
                    dir = partner.P.x > b.p.x ? 1 : -1;
                    if (Mathf.Abs(partner.P.x - b.p.x) > 13 && partner.On == b.on) { if (!Walk(dt, seg, 1.1f)) return; }
                    else if (!quirkHit) { quirkHit = true; squash = 0.4f; happyT = 2f; Emote("heart", 2f); if (!pairLead) Say("꼬옥~", 1.2f); }
                    break;
                case "rps":
                    if (!quirkHit && qt > 1.2f && PartnerDoing("rps"))
                    {
                        quirkHit = true;
                        int r = (hand - partner.hand + 3) % 3;
                        Say(Hands[hand], 0.9f);
                        if (r == 1) { happyT = 1.8f; squash = -0.5f; Emote("star", 1.5f); DelayedSay(Pick(new[] { "이겼다~!", "예스!", "내가 이김ㅋ" }), 1f); }
                        else if (r == 2) { Emote("sweat", 1.5f); DelayedSay(Pick(new[] { "으악 졌다", "한 판 더!", "에이~" }), 1f); }
                        else if (pairLead) DelayedSay("비겼네!", 1f);
                    }
                    break;
                case "tagIt":
                    if (!PartnerDoing("tagRun")) { timer = 0; break; }
                    dir = partner.P.x > b.p.x ? 1 : -1;
                    if (partner.On == b.on && Mathf.Abs(partner.P.x - b.p.x) < 12)
                    {
                        Say("잡았다!", 1.2f);
                        happyT = 1.5f;
                        partner.Say("으악 잡혔다!", 1.2f);
                        partner.squash = 0.6f;
                        partner.Enter(State.Idle, 1.5f);
                        timer = 0;
                        break;
                    }
                    if (!Walk(dt, seg, 1.7f)) return;
                    if (timer <= dt) Say("헥헥... 못 잡겠다", 1.4f);
                    break;
                case "tagRun":
                    if (!PartnerDoing("tagIt")) { timer = 0; break; }
                    dir = partner.P.x > b.p.x ? -1 : 1;
                    happyT = 0.5f;
                    if (!Walk(dt, seg, 1.9f)) return;
                    break;
                case "mend":
                    if (!quirkHit && qt > 1.65f)
                    {
                        quirkHit = true;
                        Emote("flash", 0.4f);
                        Say("단추 다시 달았다!", 1.1f);
                        var neighbor = d.NearestPerson(this, 65);
                        if (neighbor && neighbor.On == On) neighbor.React("내 단추도 고쳐 줄래요?", "heart");
                    }
                    break;
                case "flute":
                    if (!quirkHit && qt > 1.3f)
                    {
                        quirkHit = true;
                        foreach (var neighbor in d.Tenants)
                            if (neighbor != this && neighbor.On == On && neighbor.AllowsSpacing &&
                                Mathf.Abs(neighbor.P.x - P.x) < 150)
                                neighbor.React(neighbor.Type.IsAnimal ? null : "어디서 피리 소리가 나지?", "note", 2f);
                    }
                    break;
                case "coach":
                    if (beat >= 0.65f)
                    {
                        beat = 0;
                        squash = -0.6f;
                        reps++;
                        Say(Count[Mathf.Min(reps - 1, Count.Length - 1)], 0.6f);
                    }
                    break;
                case "mirror":
                    if (beat >= 0.55f)
                    {
                        beat = 0;
                        squash = squash < 0 ? 0.55f : -0.55f;
                        if (!quirkHit && qt >= 1.7f && PartnerDoing("mirror"))
                        {
                            quirkHit = true;
                            Emote("star", 1f);
                            if (!pairLead) Say("똑같지?", 1.1f);
                        }
                    }
                    break;
                case "coinTrick":
                    if (!quirkHit && qt >= 1.65f && PartnerDoing("coinWatch"))
                    {
                        quirkHit = true;
                        Say("귀 뒤에 있었지!", 1.25f);
                        Emote("coin", 1f);
                        partner.React("거기 있었어?!", "!", 1.25f);
                        partner.squash = -0.5f;
                    }
                    break;
                case "thanks":
                    if (!quirkHit && qt > 1.3f) { quirkHit = true; Say(Pick(new[] { "고마워! 냠냠", "와 맛있다!", "역시 너밖에 없어" }), 1.4f); Emote("heart", 1.4f); happyT = 1.5f; }
                    break;
            }
            if (timer <= 0) Enter(State.Idle, Random.Range(T.idleMin, T.idleMax));
        }

        // ---------------------------------------------------------------- signature actions

        void StartSpecial()
        {
            if (Type.special == "yoyo") { StartQuirk("yoyo"); return; }
            if (Type.special == "tailor") { StartQuirk("mend"); return; }
            if (Type.special == "flute") { StartQuirk("flute"); return; }
            if (Type.special == "stretchCoach") { StartQuirk("coach"); return; }
            Enter(State.Special, Random.Range(3f, 6f));
            switch (Type.special)
            {
                case "photo": Say(TenantBook.Pick(Type.act), 1.8f); break;
                case "selfie": Say("찰칵!", 1.2f); Emote("flash", 0.3f); break;
                case "sing": Emote("note", timer); Say(TenantBook.Pick(Type.act)); break;
                case "pushup": break;
                case "wag": happyT = timer; Say(TenantBook.Pick(Type.act), 1.5f); break;
                case "dj":
                    Emote("note", timer);
                    Say(TenantBook.Pick(Type.act), 2f);
                    d.Party(this, 260, timer);
                    break;
                case "bake":
                case "cook": Emote("steam", timer); Say(TenantBook.Pick(Type.act), 1.8f); break;
                case "game": Say(TenantBook.Pick(Type.act), 1.8f); break;
                case "vacuum": timer = 6f; Say(TenantBook.Pick(Type.act), 1.4f); break;
                case "slide":
                    timer = d.Sky.Now == Weather.Kind.Snow ? 4f : 2.5f;
                    dir = Random.value < 0.5f ? -1 : 1;
                    Say(TenantBook.Pick(Type.act), 1.4f);
                    break;
                case "cry":
                    timer = 4f;
                    Say(TenantBook.Pick(Type.act), 2f);
                    Emote("sweat", 3f);
                    d.Comfort(this);
                    break;
                case "patrol": timer = 7f; Say(TenantBook.Pick(Type.act), 1.4f); break;
                case "magic": timer = 2.4f; quirkHit = false; Emote("flash", 0.6f); Say("수리수리...", 1.2f); break;
                case "paint": timer = Random.Range(4f, 6f); Say(TenantBook.Pick(Type.act), 1.6f); break;
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
                case "vacuum":
                    FetchCoins();
                    if (!Walk(dt, seg, 1.5f)) return;
                    break;
                case "slide":
                    if (!Walk(dt, seg, 3.6f)) return;
                    break;
                case "patrol":
                    if (beat > 0.8f)
                    {
                        beat = 0;
                        foreach (var t in d.Tenants)
                            if (t != this && t.Sitting && Vector2.Distance(t.P, b.p) < 160) { Say("거기 위험해요! 삑!", 1.4f); Emote("!", 1f); t.Scold(); }
                    }
                    if (!Walk(dt, seg, 1.5f)) return;
                    break;
                case "magic":
                    if (!quirkHit && timer < 1.2f)
                    {
                        quirkHit = true;
                        Emote("flash", 0.5f);
                        squash = -0.6f;
                        float roll = Random.value;
                        if (roll < 0.4f) { d.SpawnCoinAt(b.p - new Vector2(0, h), Type.rent); Say("짠! 동전이 나왔습니다", 1.6f); }
                        else if (roll < 0.75f && d.NearestPerson(this, 260) is Tenant volunteer && d.RandomSpot(out var landing)) { volunteer.Vanish(landing); Say("짠! 사라졌습니다~", 1.6f); }
                        else { Say("짠! ...비둘기는 퇴근했네요", 1.6f); Emote("sweat", 1.2f); }
                    }
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
                case "photo":
                    Say("찰칵! 이웃들과 한 컷", 1.8f);
                    Emote("flash", 0.35f);
                    foreach (var neighbor in d.Tenants)
                        if (neighbor != this && neighbor.On == On && neighbor.Carriable &&
                            Mathf.Abs(neighbor.P.x - P.x) < 140)
                            neighbor.React(neighbor.Type.IsAnimal ? null : "김치!", "heart");
                    break;
                case "bake":
                    Say("갓 구운 소금빵 나눠 드려요!", 1.8f);
                    d.Feed(this, 220);
                    break;
                case "cook": Say("다 됐어요~ 드세요!", 1.8f); d.Feed(this, 220); break;
                case "paint": d.PlacePainting(b.p + new Vector2(dir * 18, 0), b.on); Say("완성! 걸작이다", 1.6f); happyT = 1.5f; break;
                case "cry": Say("까르르", 1.2f); break;
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

        /// <summary>Person dance: arm up, other arm, step, wave — cycled so a dance actually moves.</summary>
        static readonly string[] DanceMoves = { "wave", "climbB", "walkA", "climbA", "walkB", "idle" };

        string PoseNow()
        {
            bool animal = Type.IsAnimal;
            int step = (int)(anim * (Type.walkSpeed > 50 || s == State.Flee ? 10 : 6)) % 2;
            switch (s)
            {
                case State.Walk:
                case State.Approach:
                case State.Visit:
                case State.Leave:
                case State.Flee: return step == 0 ? "walkA" : "walkB";
                case State.Gather: return !gatherArrived ? (step == 0 ? "walkA" : "walkB") : gatherDance && !animal ? DanceMoves[(int)(anim * 3) % DanceMoves.Length] : "idle";
                case State.Travel: return (int)(anim * 8) % 2 == 0 ? "climbA" : "climbB";
                case State.Climb: return (int)(b.p.y / 10) % 2 == 0 ? "climbA" : "climbB";
                case State.Held: return (int)(anim * 8) % 2 == 0 || animal ? "fall" : "wave";
                case State.Carried: return "fall";
                case State.Riding: return animal ? "idle" : "fall";
                case State.Fall: return asleepInAir ? (animal ? "sleep" : "idle") : "fall";
                case State.Sleep: return animal ? "sleep" : "idle";
                case State.Dance: return animal ? (step == 0 ? "walkA" : "idle") : DanceMoves[(int)(anim * 4) % DanceMoves.Length];
                case State.Chat: return chatLine == null && timer > 2f && !animal ? "wave" : "idle";
                case State.Sit: return "sit";
                case State.Quirk:
                    switch (quirk)
                    {
                        case "mirror": return qt % 1.1f < 0.55f ? "wave" : "climbA";
                        case "coinTrick": return qt < 1.65f ? "idle" : "wave";
                        case "coinWatch": return "idle";
                        case "mend": return (int)(anim * 6) % 2 == 0 ? "phone" : "idle";
                        case "flute": return "phone";
                        case "coach": return (int)(qt * 4) % 2 == 0 ? "wave" : "pushA";
                        case "yoyo":
                        case "juggle": return (int)(anim * 6) % 2 == 0 ? "wave" : "climbB";
                        case "shadowbox": return (int)(anim * 8) % 2 == 0 ? "climbA" : "climbB";
                        case "jumpjack": return (int)(qt * 12 / Mathf.PI) % 2 == 0 ? "fall" : "idle";
                        case "hug": return quirkHit && !animal ? "fall" : step == 0 ? "walkA" : "walkB";
                        case "rps": return quirkHit ? (happyT > 0 ? "fall" : "wave") : (int)(anim * 6) % 2 == 0 ? "wave" : "idle";
                        case "tailchase":
                        case "tagIt":
                        case "tagRun": return (int)(anim * 10) % 2 == 0 ? "walkA" : "walkB";
                        case "share": return animal ? "idle" : "wave";
                        case "thanks": return quirkHit && !animal ? "fall" : "idle";
                        case "petting": return !animal && (int)(anim * 5) % 2 == 0 ? "wave" : "idle";
                        case "loaf": return "sleep";
                        case "pounce": return quirkHit ? "fall" : (int)(anim * 8) % 2 == 0 ? "walkA" : "idle";
                        case "rollover": return "fall";
                        case "dig": return (int)(anim * 12) % 2 == 0 ? "walkA" : "idle";
                        case "purr": return Type.IsDog ? ((int)(anim * 10) % 2 == 0 ? "wag" : "idle") : "idle";
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
                        case "photo":
                        case "selfie": return "phone";
                        case "wag": return (int)(anim * 8) % 2 == 0 ? "wag" : "idle";
                        case "bake":
                        case "cook": return (int)(anim * 5) % 2 == 0 ? "wave" : "idle";
                        case "dj": return (int)(anim * 3) % 2 == 0 ? "wave" : "idle";
                        case "deliver":
                        case "vacuum":
                        case "patrol": return (int)(anim * 10) % 2 == 0 ? "walkA" : "walkB";
                        case "slide": return "slide";
                        case "paint": return (int)(anim * 4) % 2 == 0 ? "wave" : "idle";
                        case "magic": return quirkHit ? "fall" : "wave";
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
                case State.Gather when gatherDance: return Face.Happy;
                case State.Special when Type.special == "yawn": return Face.Yawn;
                case State.Special when Type.special == "cry": return Face.Shock;
                case State.Riding:
                case State.Quirk when quirk == "tickle": return Face.Happy;
                case State.Special when Type.special == "sing" || Type.special == "dj" || Type.special == "cook": return Face.Happy;
                case State.Quirk when quirk == "stretch": return Face.Yawn;
                case State.Quirk when quirk == "sneeze": return quirkHit ? Face.Shock : Face.Blink;
                case State.Quirk when quirk == "trip": return Face.Shock;
                case State.Quirk when quirk == "meditate" || quirk == "loaf": return Face.Blink;
                case State.Quirk when quirk == "spin" || quirk == "tailchase" || quirk == "tagRun" || quirk == "juggle" || quirk == "jumpjack" || quirk == "hug" || quirk == "howl": return Face.Happy;
                case State.Quirk when quirk == "hiccup": return beat < 0.2f ? Face.Shock : Face.Normal;
            }
            if (happyT > 0) return Face.Happy;
            return blinkT < 0 ? Face.Blink : Face.Normal;
        }

        void Render(float dt)
        {
            float lift = s == State.Quirk && quirk == "meditate" ? Mathf.Min(qt * 8, 6) + Mathf.Sin(anim * 2) * 2 : 0;
            transform.position = Desk.W(b.p - new Vector2(0, lift));
            bool shown = s != State.Inside;
            body.enabled = shown;
            if (!shown) { emote.enabled = zzz.enabled = false; return; }
            anim += dt;
            body.sprite = Frame(PoseNow(), FaceNow());
            if (Mathf.Abs(b.v.x) > 1 && s == State.Fall) dir = b.v.x > 0 ? 1 : -1;
            body.flipX = dir < 0;

            // Squash on landing (wide and flat), stretch on take-off (tall and thin); idle breathing.
            float breathe = s == State.Idle || s == State.Sleep ? Mathf.Sin(anim * 3f) * 0.03f : 0;
            squashT.localScale = new Vector3(size * (1 + 0.3f * squash), size * (1 - 0.3f * squash + breathe), 1);

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
                State.Quirk when quirk == "tickle" => Mathf.Sin(anim * 40) * 10,
                State.Quirk when quirk == "rollover" => -dir * Mathf.Min(qt / 1.2f, 1) * 360,
                State.Quirk when quirk == "howl" || quirk == "lookup" => dir * 14,
                State.Quirk when quirk == "dig" => -dir * 18,
                State.Quirk when quirk == "meditate" => Mathf.Sin(anim * 2) * 3,
                State.Walk when Type.kind == "penguin" => Mathf.Sin(anim * 9) * 9,
                State.Special when Type.special == "cry" => Mathf.Sin(anim * 20) * 5,
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
                if (EmoteIs("yoyo"))
                    spot = new Vector3(dir * w * 0.8f, h * (0.65f - Mathf.Abs(Mathf.Sin(qt * 5f)) * 0.45f), 0);
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
