using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>Pigeons fly in, perch along window tops, and scatter when a dog or the cursor comes close.</summary>
    public class Flock
    {
        class Bird { public SpriteRenderer r; public Vector2 p, perch; public float delay; }

        readonly Desk d;
        readonly Sprite a, b, sit;
        readonly List<Bird> birds = new List<Bird>();
        float stay;
        bool leaving;

        public bool Busy => birds.Count > 0;

        public Flock(Desk desk)
        {
            d = desk;
            float px = desk.Tune.pixel;
            a = Props.Make(Props.BirdA, px, new Vector2(0.5f, 0));
            b = Props.Make(Props.BirdB, px, new Vector2(0.5f, 0));
            sit = Props.Make(Props.BirdSit, px, new Vector2(0.5f, 0));
        }

        public bool Start()
        {
            if (Busy || !d.RandomSpot(out var spot)) return false;
            for (int i = 0; i < 7; i++)
            {
                var r = new GameObject("Pigeon").AddComponent<SpriteRenderer>();
                r.sortingOrder = 7;
                birds.Add(new Bird { r = r, p = new Vector2(-40 - i * 25, d.TopY + 80 + i * 6), perch = spot + new Vector2((i - 3) * 16, 0), delay = i * 0.15f });
            }
            stay = 22;
            leaving = false;
            foreach (var t in d.Tenants) if (Random.value < 0.4f) t.React(t.Type.IsAnimal ? null : TenantBook.Pick(d.Book.birds), "?");
            return true;
        }

        public void Tick(float dt)
        {
            if (!Busy) return;
            bool landed = true;
            foreach (var bird in birds)
            {
                if ((bird.delay -= dt) > 0) { landed = false; continue; }
                var target = leaving ? new Vector2(d.ScreenW + 80, d.TopY - 40) : bird.perch;
                bird.p = Vector2.MoveTowards(bird.p, target, (leaving ? 380 : 230) * dt);
                bool sitting = !leaving && (bird.p - bird.perch).sqrMagnitude < 1;
                landed &= sitting;
                bird.r.sprite = sitting ? sit : (int)(Time.time * 10 + bird.delay * 7) % 2 == 0 ? a : b;
                bird.r.flipX = target.x < bird.p.x;
                bird.r.transform.position = Desk.W(bird.p);
            }
            if (!leaving)
            {
                // Scatter when the cursor or a dog comes near, or when bored.
                bool spooked = false;
                foreach (var bird in birds) if (Vector2.Distance(d.Mouse, bird.p) < 50) spooked = true;
                foreach (var t in d.Tenants) if (t.Type.IsDog && Vector2.Distance(t.P, birds[3].perch) < 70) spooked = true;
                if (landed) stay -= dt;
                if (spooked || stay <= 0)
                {
                    leaving = true;
                    Bubble.Popup("푸드덕!", Desk.W(birds[3].p - new Vector2(0, 30)));
                }
            }
            else if (birds.TrueForAll(x => x.p.x >= d.ScreenW + 79))
            {
                foreach (var bird in birds) Object.Destroy(bird.r.gameObject);
                birds.Clear();
            }
        }
    }

    /// <summary>A small vehicle that drives in along the floor, waits at <c>stopX</c>, then drives off.</summary>
    public class Van
    {
        readonly Desk d;
        readonly SpriteRenderer sr;
        readonly float w;
        float x, wait, stopX;
        int phase; // 0 idle, 1 in, 2 parked, 3 out
        System.Action onArrive;

        public bool Busy => phase != 0;
        public float X => x;

        public Van(Desk desk, string[] art)
        {
            d = desk;
            sr = new GameObject("Van").AddComponent<SpriteRenderer>();
            sr.sprite = Props.Make(art, desk.Tune.pixel, new Vector2(0.5f, 0));
            sr.sortingOrder = 12;
            sr.enabled = false;
            w = art[0].Length * desk.Tune.pixel;
        }

        public void Start(float stop, float seconds, System.Action arrive)
        {
            stopX = stop;
            wait = seconds;
            onArrive = arrive;
            x = -w;
            phase = 1;
            sr.enabled = true;
            sr.flipX = false;
        }

        public void Tick(float dt)
        {
            switch (phase)
            {
                case 1:
                    x = Mathf.MoveTowards(x, stopX, 260 * dt);
                    d.ScareFromTruck(x + w / 2, 1);
                    if (Mathf.Approximately(x, stopX)) { phase = 2; onArrive?.Invoke(); }
                    break;
                case 2:
                    if ((wait -= dt) <= 0) { phase = 3; sr.flipX = true; }
                    break;
                case 3:
                    x = Mathf.MoveTowards(x, -w - 20, 300 * dt);
                    if (x <= -w - 19) { phase = 0; sr.enabled = false; }
                    break;
            }
            if (phase != 0) sr.transform.position = Desk.W(new Vector2(x, d.FloorY), 0.2f);
        }
    }

    /// <summary>A plane towing a banner across the top of the screen, dropping gifts.</summary>
    public class GiftPlane
    {
        readonly Desk d;
        readonly SpriteRenderer sr;
        readonly Bubble banner;
        float x = -1, dropT;

        public bool Busy => x >= 0;

        public GiftPlane(Desk desk)
        {
            d = desk;
            sr = new GameObject("Plane").AddComponent<SpriteRenderer>();
            sr.sprite = Props.Make(Props.Plane, desk.Tune.pixel, new Vector2(0.5f, 0.5f));
            sr.sortingOrder = 15;
            sr.enabled = false;
            banner = Bubble.Create(sr.transform, new Vector3(-90, 0, 0), 15);
        }

        public void Start()
        {
            x = 0.01f;
            sr.enabled = true;
            banner.Say("선물 대방출! 동전 받으세요~", 0);
        }

        public void Tick(float dt)
        {
            if (!Busy) return;
            x += 170 * dt;
            sr.transform.position = Desk.W(new Vector2(x - 60, d.TopY + 45));
            if ((dropT -= dt) <= 0 && x > 120 && x < d.ScreenW)
            {
                dropT = 0.5f;
                d.DropCoin(x - 60);
            }
            if (x > d.ScreenW + 260) { x = -1; sr.enabled = false; banner.Hide(); }
        }
    }

    /// <summary>Shooting stars streaking across the top of the screen.</summary>
    public class Meteors
    {
        struct Streak { public SpriteRenderer[] trail; public Vector2 p, v; public float life; }

        readonly Desk d;
        readonly Sprite dot;
        readonly List<Streak> streaks = new List<Streak>();
        float showT, nextT;

        public Meteors(Desk desk)
        {
            d = desk;
            dot = Props.Dot(desk.Tune.pixel * 1.5f);
        }

        public void Show(float seconds) => showT = seconds;

        public void Tick(float dt)
        {
            if (showT > 0 && (showT -= dt) > 0 && (nextT -= dt) <= 0)
            {
                nextT = Random.Range(0.3f, 0.9f);
                var trail = new SpriteRenderer[6];
                for (int i = 0; i < trail.Length; i++)
                {
                    trail[i] = new GameObject("Meteor").AddComponent<SpriteRenderer>();
                    trail[i].sprite = dot;
                    trail[i].sortingOrder = 4;
                    trail[i].color = new Color(1f, 0.95f, 0.7f, 1f - i / 6f);
                }
                streaks.Add(new Streak { trail = trail, p = new Vector2(Random.Range(d.ScreenW * 0.3f, d.ScreenW * 1.1f), d.TopY + Random.Range(10, 120)), v = new Vector2(-420, 210), life = 1.1f });
            }
            for (int i = streaks.Count - 1; i >= 0; i--)
            {
                var s = streaks[i];
                s.life -= dt;
                s.p += s.v * dt;
                for (int k = 0; k < s.trail.Length; k++) s.trail[k].transform.position = Desk.W(s.p - s.v.normalized * k * 9);
                if (s.life <= 0) { foreach (var r in s.trail) Object.Destroy(r.gameObject); streaks.RemoveAt(i); continue; }
                streaks[i] = s;
            }
        }
    }
}
