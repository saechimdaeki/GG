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
        static readonly string[] Events = { "heli", "ufo", "coins", "drill", "fireworks", "heli", "ufo", "coins" };

        readonly Desk d;
        readonly Flyer heli, ufo;
        readonly Truck truck;
        readonly Fireworks fireworks;
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
            }
            return true;
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
