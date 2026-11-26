using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// Schedules the random happenings (helicopter taxi, UFO, coin shower, fire drill, fireworks)
    /// and runs the moving truck that brings new tenants. Owned and ticked by <see cref="Desk"/>.
    /// </summary>
    public class Director
    {
        static readonly string[] Events =
        {
            "heli", "ufo", "coins", "drill", "fireworks", "blackout", "quake", "birthday", "star",
            "birds", "icecream", "plane", "meteor", "marathon", "busking", "confession", "meeting",
            "storm", "snow", "windy", "rainbow",
            "ghost", "boxes", "strays", "movie", "bbq", "snowball", "bungeo",
            "boxes", "strays", "bbq", "movie",
            "heli", "ufo", "birds", "icecream", "plane", "busking", "confession",
        };

        readonly Desk d;
        readonly Flyer heli, ufo;
        readonly Truck truck;
        readonly Fireworks fireworks;
        readonly Limo limo;
        readonly Flock flock;
        readonly Van iceVan;
        readonly GiftPlane plane;
        readonly Meteors meteors;
        readonly Ghost ghost;
        readonly Snowballs snowballs;
        readonly Van foodVan;
        float boxT;
        readonly List<(float at, System.Action act)> script = new List<(float, System.Action)>();
        float clock;
        bool racing;
        float blackoutT, fanT;
        float nextT, drillT, showerT, showerDrop;
        bool nightShowDone;

        public bool TruckBusy => truck.Busy;
        public bool Drill => drillT > 0;

        public Director(Desk desk, string forced)
        {
            d = desk;
            heli = new Flyer(desk, Flyer.Kind.Heli);
            ufo = new Flyer(desk, Flyer.Kind.Ufo);
            truck = new Truck(desk);
            fireworks = new Fireworks(desk);
            limo = new Limo(desk);
            flock = new Flock(desk);
            iceVan = new Van(desk, Props.IceTruck);
            plane = new GiftPlane(desk);
            meteors = new Meteors(desk);
            ghost = new Ghost(desk);
            snowballs = new Snowballs(desk);
            foodVan = new Van(desk, Props.FoodTruck);
            nextT = forced != null ? 6 : Random.Range(desk.Tune.eventMin, desk.Tune.eventMax);
            pending = forced;
        }

        string pending;

        public void Tick(float dt)
        {
            heli.Tick(dt);
            ufo.Tick(dt);
            truck.Tick(dt);
            fireworks.Tick(dt);
            DrillTick(dt);
            limo.Tick(dt);
            flock.Tick(dt);
            iceVan.Tick(dt);
            plane.Tick(dt);
            meteors.Tick(dt);
            ghost.Tick(dt);
            snowballs.Tick(dt);
            foodVan.Tick(dt);
            if (boxT > 0 && (boxT -= dt) % 0.6f < dt) d.DropBox(Random.Range(60f, d.ScreenW - 60f));
            RunScript(dt);
            RaceTick();
            if (blackoutT > 0 && (blackoutT -= dt) <= 0)
            {
                Bubble.Popup("전기 복구 완료!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                foreach (var t in d.Tenants) t.React(t.Type.IsAnimal ? null : "불 들어왔다!", "heart");
            }
            // Fans crowd round a visiting celebrity.
            if (limo.Star && (fanT -= dt) <= 0)
            {
                fanT = 2.5f;
                foreach (var t in d.Tenants)
                    if (t != limo.Star && Vector2.Distance(t.P, limo.Star.P) < 260 && Random.value < 0.5f)
                        t.React(t.Type.IsAnimal ? null : TenantBook.Pick(d.Book.fans), "heart");
            }
            ShowerTick(dt);

            // One fireworks show per night, the first time the app sees night-time.
            if (d.Night && !nightShowDone) { nightShowDone = true; Fireworks(8f); }

            if ((nextT -= dt) > 0) return;
            var name = pending ?? Events[Random.Range(0, Events.Length)];
            // An event with nobody to happen to (no tenants moved in yet) waits a few seconds and retries.
            if (Fire(name)) { pending = null; nextT = Random.Range(d.Tune.eventMin, d.Tune.eventMax); }
            else { pending = name; nextT = 4; }
        }

        /// <summary>Starts event <paramref name="name"/> now. False if it couldn't happen yet.</summary>
        public bool Fire(string name)
        {
            if (d.Tenants.Count == 0) return false;
            Debug.Log("[event] " + name);
            switch (name)
            {
                case "heli": return heli.Start();
                case "ufo": return ufo.Start();
                case "coins": showerT = 6f; showerDrop = 0; Bubble.Popup("보너스 데이! 동전을 주워요!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70))); break;
                case "drill":
                    drillT = d.Tune.drillDuration;
                    Bubble.Popup("삐-삐- 화재 대피 훈련!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants) t.Evacuate(drillT);
                    break;
                case "fireworks": Fireworks(6f); break;
                case "blackout":
                    blackoutT = 15f;
                    d.StartBlackout(blackoutT);
                    Bubble.Popup("정전!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants) t.React(t.Type.IsAnimal ? null : TenantBook.Pick(d.Book.blackout), "?");
                    break;
                case "quake":
                    d.Shake(3f);
                    d.ScatterCoins();
                    Bubble.Popup("지진 발생!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants) t.Quake();
                    break;
                case "birthday":
                    var hero = d.Tenants[Random.Range(0, d.Tenants.Count)];
                    Bubble.Popup($"오늘은 {hero.Type.name} 생일!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants)
                        if (t != hero) t.React(t.Type.IsAnimal ? TenantBook.Pick(t.Type.greet) : TenantBook.Pick(d.Book.birthday), "note", 2.5f);
                    hero.React(hero.Type.IsAnimal ? TenantBook.Pick(hero.Type.greet) : "다들 고마워요~!", "heart", 3f);
                    for (int i = 0; i < 3; i++) d.SpawnCoinAt(hero.P - new Vector2(0, hero.Height + 10), d.Tune.birthdayGift);
                    Fireworks(3f);
                    break;
                case "birds": return flock.Start();
                case "icecream":
                    if (iceVan.Busy) return false;
                    float stop = d.ScreenW * 0.4f;
                    Bubble.Popup("딩동딩동~ 아이스크림 트럭!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    iceVan.Start(stop, 16, () =>
                    {
                        foreach (var t in d.Tenants)
                            t.Gather(iceVan.X + 50, 13, TenantBook.Pick(d.Book.icecream), "heart", 1.6f, false, TenantBook.Pick(d.Book.icecreamDone), 60);
                    });
                    break;
                case "plane":
                    if (plane.Busy) return false;
                    plane.Start();
                    break;
                case "meteor":
                    meteors.Show(10);
                    Bubble.Popup("유성우가 쏟아져요!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants) t.React(t.Type.IsAnimal ? null : TenantBook.Pick(d.Book.wish), "star", 2.5f);
                    break;
                case "marathon":
                    racing = true;
                    Bubble.Popup("마라톤 대회! 아파트 문 앞까지 1등은?", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants)
                    {
                        t.Gather(d.Door.x, 25, null, "sweat", Random.Range(1.6f, 2.8f), false, null, 4);
                        t.React(t.Type.IsAnimal ? null : TenantBook.Pick(d.Book.marathon), "sweat");
                    }
                    Schedule(25, () => racing = false);
                    break;
                case "busking":
                    Tenant singer = null;
                    foreach (var t in d.Tenants) if (t.Type.special == "dj" || (singer == null && !t.Type.IsAnimal)) singer = t;
                    if (singer == null) return false;
                    float spot = d.Town.Buildings.Count > 2 ? d.Town.Buildings[2].X : d.ScreenW * 0.5f;
                    Bubble.Popup($"{singer.Type.name} 길거리 공연!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    singer.Gather(spot, 16, TenantBook.Pick(d.Book.busking), "note", 1.6f, true, "감사합니다~!", 0);
                    foreach (var t in d.Tenants) if (t != singer) t.Gather(spot, 16, null, "note", 1.4f, true, null, 90);
                    break;
                case "confession":
                    var people = new List<Tenant>();
                    foreach (var t in d.Tenants) if (!t.Type.IsAnimal && t.Carriable) people.Add(t);
                    if (people.Count < 2) return false;
                    var a = people[Random.Range(0, people.Count)];
                    people.Remove(a);
                    var b = people[Random.Range(0, people.Count)];
                    b.Gather(b.P.x, 12, null, null, 1, false, null, 0);
                    a.Gather(b.P.x - 20, 12, TenantBook.Pick(d.Book.confess), "heart", 1.4f, false, null, 0);
                    Schedule(6, () =>
                    {
                        if (!b) return;
                        bool yes = Random.value < 0.75f;
                        b.React(yes ? TenantBook.Pick(d.Book.accept) : "죄송해요... 친구로 지내요", yes ? "heart" : "sweat", 2.5f);
                        if (yes) { Fireworks(2.5f); Bubble.Popup("커플 탄생!", Desk.W(b.P - new Vector2(0, 80))); }
                        else if (a) a.React("...괜찮아요 ㅠㅠ", "sweat", 2.5f);
                    });
                    break;
                case "meeting":
                    var crowd = new List<Tenant>();
                    foreach (var t in d.Tenants) if (!t.Type.IsAnimal) crowd.Add(t);
                    if (crowd.Count < 2) return false;
                    var chair = crowd[Random.Range(0, crowd.Count)];
                    Bubble.Popup("반상회 시간입니다", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants) t.Gather(d.Door.x - 90, 18, null, "?", 1.4f, false, null, 80);
                    for (int i = 0; i < d.Book.meetingChair.Length; i++)
                    {
                        string line = d.Book.meetingChair[i];
                        Schedule(4 + i * 3.2f, () => { if (chair) chair.React(line, null, 2.6f); });
                        Schedule(5.6f + i * 3.2f, () =>
                        {
                            foreach (var t in crowd) if (t && t != chair && Random.value < 0.35f) t.React(TenantBook.Pick(d.Book.meetingCrowd), null, 1.4f);
                        });
                    }
                    break;
                case "ghost":
                    if (ghost.Busy) return false;
                    ghost.Start();
                    Bubble.Popup("으스스... 유령이 나타났다! (마우스로 쫓아내기)", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    break;
                case "boxes":
                    boxT = 4.5f;
                    Bubble.Popup("택배 대란! 상자가 쏟아져요", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    break;
                case "strays":
                    if (d.Book.strays == null || d.Book.strays.Length == 0) return false;
                    Bubble.Popup("길고양이 모임", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    var cats = new List<Tenant>();
                    for (int i = 0; i < d.Book.strays.Length; i++)
                        cats.Add(d.SpawnVisitor(d.Book.strays[i], new Vector2(20 + i * 30, d.FloorY - 30)));
                    foreach (var t in d.Tenants) if (t.Type.IsCat && !t.Visitor) t.Gather(140, 30, null, "heart", 1.8f, false, null, 60);
                    foreach (var t in d.Tenants) if (t.Type.IsDog) t.React("멍멍! (흥분)", "!");
                    Schedule(35, () => { foreach (var c in cats) if (c) c.LeaveTo(-60); });
                    break;
                case "movie":
                    d.Board?.Message("오늘의 영화 상영 중 ▶", 22);
                    Bubble.Popup("옥상 광고판 영화 상영!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants)
                        t.Gather(d.Door.x - 30, 22, TenantBook.Pick(d.Book.movie), "heart", 1.4f, false, TenantBook.Pick(d.Book.movieEnd), 110);
                    break;
                case "bbq":
                    Tenant cook = null;
                    foreach (var t in d.Tenants) if (t.Type.special == "cook") cook = t;
                    float grill = d.Town.Buildings.Count > 2 ? d.Town.Buildings[2].X : d.Door.x - 160;
                    Bubble.Popup("바베큐 파티!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foreach (var t in d.Tenants)
                        t.Gather(grill, 16, t == cook ? "고기 굽습니다~" : TenantBook.Pick(d.Book.bbq), t == cook ? "steam" : "heart", 1.5f, t == cook, t == cook ? "다 같이 먹어요!" : "배부르다~", 80);
                    Schedule(16, () => { foreach (var t in d.Tenants) if (t) t.Treat(); });
                    break;
                case "snowball":
                    if (d.Sky.Now != Weather.Kind.Snow) d.Sky.Set(Weather.Kind.Snow, 60);
                    Bubble.Popup("눈싸움 시작!", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    snowballs.Start(20);
                    break;
                case "bungeo":
                    if (foodVan.Busy) return false;
                    Bubble.Popup("붕어빵 트럭 왔어요~", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    foodVan.Start(d.ScreenW * 0.3f, 16, () =>
                    {
                        foreach (var t in d.Tenants)
                            t.Gather(foodVan.X + 50, 13, TenantBook.Pick(d.Book.bungeo), "heart", 1.6f, false, TenantBook.Pick(d.Book.bungeoDone), 60);
                    });
                    break;
                case "storm": d.Sky.Set(Weather.Kind.Storm, 70); break;
                case "snow": d.Sky.Set(Weather.Kind.Snow, 90); break;
                case "windy": d.Sky.Set(Weather.Kind.Windy, 50); break;
                case "rain": d.Sky.Set(Weather.Kind.Rain, 60); break;
                case "rainbow": d.Sky.StartRainbow(); break;
                case "star":
                    if (limo.Busy || d.Book.star == null) return false;
                    limo.Start();
                    Bubble.Popup("연예인 방문! 리무진 도착 중", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
                    break;
            }
            return true;
        }

        /// <summary>Runs <paramref name="act"/> after <paramref name="delay"/> seconds.</summary>
        void Schedule(float delay, System.Action act) => script.Add((clock + delay, act));

        void RunScript(float dt)
        {
            clock += dt;
            for (int i = script.Count - 1; i >= 0; i--)
                if (script[i].at <= clock) { var act = script[i].act; script.RemoveAt(i); act(); }
        }

        /// <summary>Marathon: the first tenant to reach the door wins a prize.</summary>
        void RaceTick()
        {
            if (!racing) return;
            foreach (var t in d.Tenants)
            {
                if (!t.Arrived) continue;
                racing = false;
                Bubble.Popup($"{t.Type.name} 우승!", Desk.W(t.P - new Vector2(0, 90)));
                t.React(t.Type.IsAnimal ? TenantBook.Pick(t.Type.greet) : "1등이다!!", "heart", 3f);
                d.SpawnCoinAt(t.P - new Vector2(0, t.Height + 10), d.Tune.marathonPrize);
                foreach (var o in d.Tenants) if (o != t) o.Cheer();
                return;
            }
        }

        /// <summary>A fireworks show; tenants who see it cheer.</summary>
        public void Fireworks(float seconds)
        {
            fireworks.Show(seconds);
            foreach (var t in d.Tenants) t.Cheer();
        }

        /// <summary>Brings <paramref name="count"/> new tenants by truck.</summary>
        public void SendTruck(int count) => truck.Start(count);

        void DrillTick(float dt)
        {
            if (drillT <= 0) return;
            drillT -= dt;
            if (drillT <= 0) Bubble.Popup("훈련 종료! 수고하셨습니다", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
        }

        void ShowerTick(float dt)
        {
            if (showerT <= 0) return;
            showerT -= dt;
            if ((showerDrop -= dt) > 0) return;
            showerDrop = 0.35f;
            d.DropCoin(Random.Range(60f, d.ScreenW - 60f));
        }
    }

    /// <summary>
    /// A helicopter (rope) or UFO (tractor beam) that picks a tenant up and sets them down elsewhere.
    /// The helicopter lives on the roof helipad when the building has one.
    /// </summary>
    public class Flyer
    {
        public enum Kind { Heli, Ufo }
        enum Phase { Idle, Approach, Lower, Lift, Travel, Away, Return, Leave }

        readonly Desk d;
        readonly Kind kind;
        readonly SpriteRenderer body, line;
        readonly Sprite frameA, frameB;
        Phase phase;
        Vector2 pos, dest;
        Tenant rider;
        float lineLen, t, anim;

        float Speed => kind == Kind.Heli ? 240 : 320;

        public Flyer(Desk desk, Kind k)
        {
            d = desk;
            kind = k;
            float px = desk.Tune.pixel;
            frameA = Props.Make(k == Kind.Heli ? Props.HeliA : Props.UfoA, px, new Vector2(0.5f, 0.5f));
            frameB = Props.Make(k == Kind.Heli ? Props.HeliB : Props.UfoB, px, new Vector2(0.5f, 0.5f));
            body = new GameObject(k.ToString()).AddComponent<SpriteRenderer>();
            body.sprite = frameA;
            body.sortingOrder = 14;
            body.enabled = false;
            line = new GameObject(k + " line").AddComponent<SpriteRenderer>();
            line.sprite = k == Kind.Heli ? Props.Tile(new Color32(0x6a, 0x50, 0x30, 255), 1, px) : Props.Tile(new Color(0.6f, 1f, 0.6f, 0.35f), 8, px);
            line.drawMode = SpriteDrawMode.Tiled;
            line.sortingOrder = 13;
            line.enabled = false;
        }

        bool Parked => kind == Kind.Heli && d.HasHelipad;

        /// <summary>Takes off for a random tenant. False when busy or nobody can be picked up.</summary>
        public bool Start()
        {
            if (phase != Phase.Idle) return false;
            rider = Pick();
            if (rider == null) return false;
            pos = Parked ? d.PadTop - new Vector2(0, 14) : new Vector2(kind == Kind.Heli ? d.ScreenW + 80 : -80, d.TopY + 60);
            phase = Phase.Approach;
            body.enabled = true;
            if (kind == Kind.Ufo) Bubble.Popup("??? 미확인 비행물체 출현", Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
            else Bubble.Popup("헬기 택시 출동!", Desk.W(pos - new Vector2(0, 30)));
            Debug.Log($"[event] {kind} heads for {rider.Type.name}");
            return true;
        }

        Tenant Pick()
        {
            var options = new List<Tenant>();
            foreach (var x in d.Tenants) if (x.Carriable) options.Add(x);
            return options.Count == 0 ? null : options[Random.Range(0, options.Count)];
        }

        public void Tick(float dt)
        {
            if (phase == Phase.Idle)
            {
                // Parked on the helipad between flights.
                body.enabled = Parked;
                if (Parked) { pos = d.PadTop - new Vector2(0, 14); body.sprite = frameA; body.transform.position = Desk.W(pos); }
                return;
            }
            anim += dt;
            t += dt;
            body.sprite = (int)(anim * (kind == Kind.Heli ? 20 : 4)) % 2 == 0 ? frameA : frameB;
            float hover = kind == Kind.Heli ? 110 : 160;
            switch (phase)
            {
                case Phase.Approach:
                    // The target wandered off (jumped, teleported, got grabbed): pick someone else.
                    if (!rider || !rider.Carriable) rider = Pick();
                    if (!rider) { phase = Phase.Leave; break; }
                    if (MoveTo(rider.P - new Vector2(0, hover), dt)) { phase = Phase.Lower; lineLen = 0; }
                    break;
                case Phase.Lower:
                    if (!rider || !rider.Carriable) { rider = null; phase = Phase.Leave; break; }
                    lineLen = Mathf.MoveTowards(lineLen, hover - rider.Height, 220 * dt);
                    if (lineLen >= hover - rider.Height - 0.5f)
                    {
                        rider.Carry(kind == Kind.Heli ? "택시 왔어요?!" : "으아아 납치다!");
                        Debug.Log($"[event] {kind} picks up {rider.Type.name}");
                        d.AlienCheer(kind == Kind.Ufo);
                        phase = Phase.Lift;
                    }
                    break;
                case Phase.Lift:
                    lineLen = Mathf.MoveTowards(lineLen, kind == Kind.Heli ? 50 : 0, (kind == Kind.Heli ? 140 : 60) * dt);
                    if (Mathf.Approximately(lineLen, kind == Kind.Heli ? 50 : 0))
                    {
                        if (kind == Kind.Ufo) { phase = Phase.Away; t = 0; }
                        else { d.RandomSpot(out dest); phase = Phase.Travel; }
                    }
                    break;
                case Phase.Travel:
                    if (MoveTo(dest - new Vector2(0, lineLen + rider.Height + 40), dt)) Release(new Vector2(0, 0));
                    break;
                case Phase.Away:
                    // Zip off the top of the screen with the rider, then come back to return them.
                    pos += new Vector2(0, -500) * dt;
                    if (t > 3.5f) { d.RandomSpot(out dest); pos = new Vector2(dest.x, -120); phase = Phase.Return; }
                    break;
                case Phase.Return:
                    if (MoveTo(dest - new Vector2(0, 170), dt))
                    {
                        Bubble.Popup("삐릿. 반납합니다.", Desk.W(pos - new Vector2(0, 30)));
                        Release(Vector2.zero);
                    }
                    break;
                case Phase.Leave:
                    lineLen = Mathf.MoveTowards(lineLen, 0, 300 * dt);
                    var exit = Parked ? d.PadTop - new Vector2(0, 14) : new Vector2(kind == Kind.Heli ? -120 : pos.x, kind == Kind.Heli ? pos.y : -150);
                    if (MoveTo(exit, dt)) { phase = Phase.Idle; line.enabled = false; body.enabled = Parked; }
                    break;
            }
            body.flipX = kind == Kind.Heli && dirRight;
            body.transform.position = Desk.W(pos);
            body.transform.rotation = Quaternion.Euler(0, 0, kind == Kind.Heli ? (dirRight ? -6 : 6) * Mathf.Clamp01(moving) : Mathf.Sin(anim * 3) * 4);
            if (rider && (phase == Phase.Lift || phase == Phase.Travel || phase == Phase.Return || phase == Phase.Away))
                rider.CarryTo(pos + new Vector2(0, 10 + lineLen + rider.Height));
            line.enabled = lineLen > 1 && phase != Phase.Idle;
            if (line.enabled)
            {
                line.transform.position = Desk.W(pos + new Vector2(0, 10));
                line.size = new Vector2(kind == Kind.Heli ? d.Tune.pixel : 8 * d.Tune.pixel, lineLen);
            }
        }

        bool dirRight;
        float moving;

        bool MoveTo(Vector2 target, float dt)
        {
            var delta = target - pos;
            dirRight = delta.x > 0;
            moving = Mathf.Abs(delta.x) > 4 ? 1 : 0;
            pos = Vector2.MoveTowards(pos, target, Speed * dt);
            return (target - pos).sqrMagnitude < 1;
        }

        void Release(Vector2 v)
        {
            if (rider) Debug.Log($"[event] {kind} drops off {rider.Type.name}");
            rider?.Release(v, kind == Kind.Heli ? "감사합니다~ 기사님!" : "여, 여긴 어디...");
            rider = null;
            phase = Phase.Leave;
        }
    }

    /// <summary>The moving truck: drives in along the floor, drops off new tenants at the house, drives off.</summary>
    public class Truck
    {
        enum Phase { Idle, In, Unload, Out }

        readonly Desk d;
        readonly SpriteRenderer sr;
        readonly float w;
        Phase phase;
        float x, t;
        int left;

        public bool Busy => phase != Phase.Idle;
        float StopX => d.Door.x - 150;

        public Truck(Desk desk)
        {
            d = desk;
            sr = new GameObject("Truck").AddComponent<SpriteRenderer>();
            sr.sprite = Props.Make(Props.Truck, desk.Tune.pixel, new Vector2(0.5f, 0));
            sr.sortingOrder = 12;
            sr.enabled = false;
            w = Props.Truck[0].Length * desk.Tune.pixel;
        }

        public void Start(int count)
        {
            if (Busy) return;
            left = count;
            x = -w;
            phase = Phase.In;
            sr.enabled = true;
            sr.flipX = false;
        }

        public void Tick(float dt)
        {
            if (phase == Phase.Idle) return;
            t += dt;
            switch (phase)
            {
                case Phase.In:
                    x = Mathf.MoveTowards(x, StopX, 300 * dt);
                    d.ScareFromTruck(x + w / 2, 1);
                    if (Mathf.Approximately(x, StopX))
                    {
                        phase = Phase.Unload;
                        t = 0;
                        Bubble.Popup("빵빵! 이삿짐 왔어요", Desk.W(new Vector2(x, d.FloorY - 60)));
                    }
                    break;
                case Phase.Unload:
                    if (t < 0.7f) break;
                    t = 0;
                    if (left-- > 0) d.MoveIn(new Vector2(x + w / 2 + 10, d.FloorY - 4));
                    else { phase = Phase.Out; sr.flipX = true; }
                    break;
                case Phase.Out:
                    x = Mathf.MoveTowards(x, -w - 20, 320 * dt);
                    d.ScareFromTruck(x - w / 2, -1);
                    if (x <= -w - 19) { phase = Phase.Idle; sr.enabled = false; }
                    break;
            }
            // A little bounce on the road.
            float bob = phase == Phase.Unload ? 0 : Mathf.Abs(Mathf.Sin(t * 18)) * 1.5f;
            sr.transform.position = Desk.W(new Vector2(x, d.FloorY - bob), 0.2f);
        }
    }

    /// <summary>The celebrity's limo: drives in, lets the star out, waits, takes them away again.</summary>
    public class Limo
    {
        enum Phase { Idle, In, Visit, Waiting, Out }

        readonly Desk d;
        readonly SpriteRenderer sr;
        readonly float w;
        Phase phase;
        float x, t;

        public Tenant Star { get; private set; }
        public bool Busy => phase != Phase.Idle;
        float StopX => d.ScreenW * 0.45f;

        public Limo(Desk desk)
        {
            d = desk;
            sr = new GameObject("Limo").AddComponent<SpriteRenderer>();
            sr.sprite = Props.Make(Props.Limo, desk.Tune.pixel, new Vector2(0.5f, 0));
            sr.sortingOrder = 12;
            sr.enabled = false;
            w = Props.Limo[0].Length * desk.Tune.pixel;
        }

        public void Start()
        {
            x = -w;
            phase = Phase.In;
            sr.enabled = true;
            sr.flipX = false;
        }

        public void Tick(float dt)
        {
            if (phase == Phase.Idle) return;
            t += dt;
            switch (phase)
            {
                case Phase.In:
                    x = Mathf.MoveTowards(x, StopX, 260 * dt);
                    d.ScareFromTruck(x + w / 2, 1);
                    if (Mathf.Approximately(x, StopX))
                    {
                        Star = d.SpawnVisitor(d.Book.star, new Vector2(x + w / 2 + 12, d.FloorY - 4));
                        phase = Phase.Visit;
                        t = 0;
                    }
                    break;
                case Phase.Visit:
                    if (t < 28 && Star) break;
                    if (Star) Star.LeaveTo(x);
                    phase = Phase.Waiting;
                    t = 0;
                    break;
                case Phase.Waiting:
                    if (Star && t < 30) break;
                    if (Star) d.Remove(Star);
                    Star = null;
                    Bubble.Popup("팬 여러분 안녕~", Desk.W(new Vector2(x, d.FloorY - 60)));
                    phase = Phase.Out;
                    sr.flipX = true;
                    break;
                case Phase.Out:
                    x = Mathf.MoveTowards(x, -w - 20, 300 * dt);
                    d.ScareFromTruck(x - w / 2, -1);
                    if (x <= -w - 19) { phase = Phase.Idle; sr.enabled = false; }
                    break;
            }
            sr.transform.position = Desk.W(new Vector2(x, d.FloorY), 0.2f);
        }
    }

    /// <summary>Bursts of coloured sparks over the top of the screen.</summary>
    public class Fireworks
    {
        struct Spark { public SpriteRenderer r; public Vector2 p, v; public float life; }

        static readonly Color[] Colors =
        {
            new Color(1f, 0.4f, 0.4f), new Color(1f, 0.85f, 0.3f), new Color(0.5f, 0.9f, 1f),
            new Color(0.7f, 1f, 0.5f), new Color(1f, 0.6f, 1f),
        };

        readonly Desk d;
        readonly List<Spark> sparks = new List<Spark>();
        readonly Sprite dot;
        float showT, nextBurst;

        public Fireworks(Desk desk)
        {
            d = desk;
            dot = Props.Dot(desk.Tune.pixel * 2);
        }

        public void Show(float seconds)
        {
            showT = seconds;
            nextBurst = 0;
        }

        public void Tick(float dt)
        {
            if (showT > 0)
            {
                showT -= dt;
                if ((nextBurst -= dt) <= 0)
                {
                    nextBurst = Random.Range(0.35f, 0.8f);
                    Burst(new Vector2(Random.Range(d.ScreenW * 0.15f, d.ScreenW * 0.85f), Random.Range(d.TopY + 80, d.TopY + 260)));
                }
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.life -= dt;
                s.v.y += 160 * dt;
                s.p += s.v * dt;
                if (s.life <= 0) { Object.Destroy(s.r.gameObject); sparks.RemoveAt(i); continue; }
                s.r.transform.position = Desk.W(s.p);
                var c = s.r.color;
                c.a = Mathf.Clamp01(s.life / 0.6f);
                s.r.color = c;
                sparks[i] = s;
            }
        }

        void Burst(Vector2 at)
        {
            var color = Colors[Random.Range(0, Colors.Length)];
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2;
                var r = new GameObject("Spark").AddComponent<SpriteRenderer>();
                r.sprite = dot;
                r.color = color;
                r.sortingOrder = 4;
                float speed = Random.Range(110f, 200f);
                sparks.Add(new Spark { r = r, p = at, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, life = Random.Range(0.9f, 1.4f) });
            }
        }
    }
}
