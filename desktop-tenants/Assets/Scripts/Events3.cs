using System.Collections.Generic;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>A ghost drifting across the screen. Tenants it passes run; the cursor chases it off for a reward.</summary>
    public class Ghost
    {
        readonly Desk d;
        readonly SpriteRenderer sr;
        float x = -1, t, baseY;

        public bool Busy => x >= 0;

        public Ghost(Desk desk)
        {
            d = desk;
            sr = new GameObject("Ghost").AddComponent<SpriteRenderer>();
            sr.sprite = Props.Make(Props.Ghost, desk.Tune.pixel * 1.3f, new Vector2(0.5f, 0.5f));
            sr.sortingOrder = 16;
            sr.enabled = false;
        }

        public void Start()
        {
            x = 0.01f;
            t = 0;
            baseY = Random.Range(d.TopY + 150, d.FloorY - 120);
            sr.enabled = true;
        }

        public void Tick(float dt)
        {
            if (!Busy) return;
            t += dt;
            x += 70 * dt;
            var p = new Vector2(x, baseY + Mathf.Sin(t * 1.7f) * 60);
            sr.transform.position = Desk.W(p);
            sr.color = new Color(1, 1, 1, 0.55f + Mathf.Sin(t * 5) * 0.2f);
            sr.flipX = Mathf.Cos(t * 1.7f) < 0;
            foreach (var tenant in d.Tenants)
                if (Vector2.Distance(tenant.P - new Vector2(0, tenant.Height / 2), p) < 90) tenant.Scare(p, TenantBook.Pick(d.Book.ghost));
            if (Vector2.Distance(d.Mouse, p) < 40)
            {
                Bubble.Popup("유령 퇴치! 뿅", Desk.W(p - new Vector2(0, 30)));
                d.SpawnCoinAt(p, 2000);
                End();
            }
            else if (x > d.ScreenW + 60) End();
        }

        void End()
        {
            x = -1;
            sr.enabled = false;
        }
    }

    /// <summary>Snowballs flying in an arc from one tenant to another.</summary>
    public class Snowballs
    {
        struct Ball { public SpriteRenderer r; public Vector2 p, v; public float t; public Tenant target; }

        const float Gravity = 900, Flight = 0.8f;
        readonly Desk d;
        readonly Sprite dot;
        readonly List<Ball> balls = new List<Ball>();
        float fightT, nextT;

        public Snowballs(Desk desk)
        {
            d = desk;
            dot = Props.Dot(desk.Tune.pixel * 2);
        }

        public void Start(float seconds) => fightT = seconds;

        public void Tick(float dt)
        {
            if (fightT > 0 && (fightT -= dt) > 0 && (nextT -= dt) <= 0)
            {
                nextT = Random.Range(0.4f, 0.9f);
                Throw();
            }
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var b = balls[i];
                b.t += dt;
                b.v.y += Gravity * dt;
                b.p += b.v * dt;
                b.r.transform.position = Desk.W(b.p);
                if (b.t >= Flight)
                {
                    if (b.target) b.target.React(b.target.Type.IsAnimal ? null : TenantBook.Pick(d.Book.snowHit), "!", 1.4f);
                    Object.Destroy(b.r.gameObject);
                    balls.RemoveAt(i);
                    continue;
                }
                balls[i] = b;
            }
        }

        void Throw()
        {
            var people = new List<Tenant>();
            foreach (var t in d.Tenants) if (!t.Type.IsAnimal && t.Carriable) people.Add(t);
            if (people.Count < 2) return;
            var from = people[Random.Range(0, people.Count)];
            people.Remove(from);
            Tenant to = null;
            foreach (var t in people) if (Vector2.Distance(t.P, from.P) < 400 && (to == null || Random.value < 0.4f)) to = t;
            if (to == null) return;
            var start = from.P - new Vector2(0, from.Height * 0.8f);
            var end = to.P - new Vector2(0, to.Height * 0.6f);
            // Ballistic launch that lands on the target after Flight seconds.
            var v = new Vector2((end.x - start.x) / Flight, (end.y - start.y - 0.5f * Gravity * Flight * Flight) / Flight);
            var r = new GameObject("Snowball").AddComponent<SpriteRenderer>();
            r.sprite = dot;
            r.sortingOrder = 17;
            balls.Add(new Ball { r = r, p = start, v = v, target = to });
            from.React(TenantBook.Pick(d.Book.snowball), null, 1f);
        }
    }
}
