using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DesktopTenants
{
    /// <summary>
    /// The sky over the desktop: clear, cloudy, rain, thunderstorm, snow or gales, changing every
    /// minute or two. Draws clouds, rain, snow, lightning, a snowman and the odd rainbow, and pushes
    /// things around in the wind. Owned and ticked by <see cref="Desk"/>.
    /// </summary>
    public class Weather
    {
        public enum Kind { Clear, Cloudy, Rain, Storm, Snow, Windy }

        static readonly string[] Names = { "맑음", "흐림", "비", "천둥번개", "눈", "강풍" };

        readonly Desk d;
        readonly List<Transform> drops = new List<Transform>(), flakes = new List<Transform>();
        readonly List<SpriteRenderer> clouds = new List<SpriteRenderer>();
        readonly SpriteRenderer flash, bolt, snowman, rainbow;
        Kind now = Kind.Clear;
        float left = 90, windDir = 1, boltT, flashT, snowT, snowmanT, rainbowT, gustT;
        float snowmanX;

        public Kind Now => now;
        public string Label => rainbowT > 0 ? "무지개" : Names[(int)now];
        public bool Rainbow => rainbowT > 0;
        public bool Wet => now == Kind.Rain || now == Kind.Storm;
        /// <summary>Sideways push in points/s² on anything airborne (and a nudge on light things).</summary>
        public float Wind => now == Kind.Windy ? 260 * windDir : now == Kind.Storm ? 120 * windDir : 0;
        public bool Snowman => snowman.enabled;
        public float SnowmanX => snowmanX;

        public Weather(Desk desk, string forced)
        {
            d = desk;
            float px = desk.Tune.pixel;
            var drop = Pixels.RainDrop(px);
            var flakeSprite = Props.Dot(px * 1.2f);
            for (int i = 0; i < 120; i++)
            {
                var particle = Make("Drop", drop, 5);
                particle.enabled = true;
                drops.Add(particle.transform);
            }
            for (int i = 0; i < 90; i++)
            {
                var particle = Make("Flake", flakeSprite, 5);
                particle.enabled = true;
                flakes.Add(particle.transform);
            }
            var cloudSprite = Props.Make(Props.Cloud, px * 1.5f, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 5; i++) clouds.Add(Make("Cloud", cloudSprite, 3));
            flash = Make("Flash", Props.Dot(1), 40);
            bolt = Make("Bolt", Props.BoltTile(px), 39);
            bolt.drawMode = SpriteDrawMode.Tiled;
            snowman = Make("Snowman", Props.Make(Props.Snowman, px, new Vector2(0.5f, 0)), 6);
            rainbow = Make("Rainbow", Props.Make(Props.Rainbow(), px * 2, new Vector2(0.5f, 0)), 2);
            foreach (var t in drops) t.gameObject.SetActive(false);
            foreach (var t in flakes) t.gameObject.SetActive(false);
            if (forced != null && Enum.TryParse(forced, true, out Kind k)) Set(k, 90);
            else if (forced == "rainbow") rainbowT = 25;
        }

        static SpriteRenderer Make(string name, Sprite sprite, int order)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        /// <summary>Switches to <paramref name="k"/> for <paramref name="seconds"/>, announcing it.</summary>
        public void Set(Kind k, float seconds)
        {
            bool wasWet = Wet;
            now = k;
            left = seconds;
            windDir = Random.value < 0.5f ? -1 : 1;
            foreach (var t in drops) { t.gameObject.SetActive(Wet); t.position = Desk.W(new Vector2(Random.Range(0, d.ScreenW), Random.Range(d.TopY, d.FloorY))); }
            foreach (var t in flakes) { t.gameObject.SetActive(k == Kind.Snow); t.position = Desk.W(new Vector2(Random.Range(0, d.ScreenW), Random.Range(d.TopY, d.FloorY))); }
            for (int i = 0; i < clouds.Count; i++)
            {
                clouds[i].enabled = k != Kind.Clear;
                clouds[i].transform.position = Desk.W(new Vector2(Random.Range(0, d.ScreenW), d.TopY + 20 + i * 18));
                clouds[i].color = k == Kind.Storm ? new Color(0.55f, 0.55f, 0.65f) : Color.white;
            }
            if (k == Kind.Snow) snowT = 0;
            if (wasWet && !Wet && Random.value < 0.5f) StartRainbow();
            if (d.Tenants.Count == 0) return;
            Bubble.Popup("날씨: " + Names[(int)k], Desk.W(new Vector2(d.ScreenW / 2, d.TopY + 70)));
            string[] lines = k switch
            {
                Kind.Clear => d.Book.clear,
                Kind.Cloudy => d.Book.cloudy,
                Kind.Rain => d.Book.rain,
                Kind.Storm => d.Book.storm,
                Kind.Snow => d.Book.snow,
                _ => d.Book.windy,
            };
            foreach (var t in d.Tenants) if (Random.value < 0.5f) t.React(t.Type.IsAnimal ? null : TenantBook.Pick(lines), k == Kind.Clear ? "heart" : "!");
        }

        public void StartRainbow()
        {
            rainbowT = 25;
            foreach (var t in d.Tenants) if (Random.value < 0.6f) t.React(t.Type.IsAnimal ? null : TenantBook.Pick(d.Book.rainbow), "heart");
        }

        Kind NextKind()
        {
            int month = DateTime.Now.Month;
            bool winter = month == 12 || month <= 2 || month == 11;
            float r = Random.value * 100;
            if (r < 35) return Kind.Clear;
            if (r < 55) return Kind.Cloudy;
            if (r < 70) return Kind.Rain;
            if (r < 78) return Kind.Storm;
            if (r < 90) return winter ? Kind.Snow : Kind.Rain;
            return Kind.Windy;
        }

        public void Tick(float dt)
        {
            if ((left -= dt) <= 0) { var k = NextKind(); Set(k, k == Kind.Clear ? Random.Range(120f, 240f) : Random.Range(50f, 110f)); }
            float W = Wind;

            foreach (var t in drops)
            {
                if (!t.gameObject.activeSelf) break;
                var p = t.position + new Vector3(-60 + W * 0.5f, -650, 0) * dt;
                if (-p.y > d.FloorY) p = Desk.W(new Vector2(Random.Range(-100, d.ScreenW + 100), d.TopY));
                t.position = p;
            }
            float time = Time.time;
            for (int i = 0; i < flakes.Count; i++)
            {
                var t = flakes[i];
                if (!t.gameObject.activeSelf) break;
                var p = t.position + new Vector3(Mathf.Sin(time * 1.3f + i) * 25 + W * 0.3f, -60, 0) * dt;
                if (-p.y > d.FloorY) p = Desk.W(new Vector2(Random.Range(0, d.ScreenW), d.TopY));
                t.position = p;
            }
            foreach (var c in clouds)
            {
                if (!c.enabled) continue;
                var p = c.transform.position + new Vector3(12 + W * 0.15f, 0, 0) * dt;
                if (p.x > d.ScreenW + 80) p.x = -80;
                if (p.x < -80) p.x = d.ScreenW + 80;
                c.transform.position = p;
            }

            Lightning(dt);
            Snow(dt);
            Gusts(dt);

            rainbow.enabled = rainbowT > 0;
            if (rainbowT > 0)
            {
                rainbowT -= dt;
                rainbow.transform.position = Desk.W(new Vector2(d.ScreenW * 0.5f, d.TopY + 230));
                rainbow.color = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Min(rainbowT, 25 - rainbowT) / 3f) * 0.8f);
            }
        }

        void Lightning(float dt)
        {
            flashT -= dt;
            flash.enabled = flashT > 0;
            if (flash.enabled)
            {
                flash.transform.position = Desk.W(new Vector2(d.ScreenW / 2, d.ScreenH / 2));
                flash.transform.localScale = new Vector3(d.ScreenW, d.ScreenH, 1);
                flash.color = new Color(1, 1, 1, Mathf.Clamp01(flashT / 0.25f) * 0.45f);
            }
            bolt.enabled = flashT > 0.1f;
            if (now != Kind.Storm || (boltT -= dt) > 0) return;
            boltT = Random.Range(3f, 7f);
            flashT = 0.3f;
            // Strike a tenant sometimes, otherwise somewhere on the floor.
            Tenant target = null;
            if (Random.value < 0.4f)
                foreach (var t in d.Tenants) if (t.Carriable && Random.value < 0.3f) { target = t; break; }
            var at = target ? target.P : new Vector2(Random.Range(40, d.ScreenW - 40), d.FloorY);
            bolt.transform.position = Desk.W(new Vector2(at.x, d.TopY));
            bolt.size = new Vector2(7 * d.Tune.pixel, at.y - d.TopY);
            if (target) target.Struck(TenantBook.Pick(d.Book.struck));
        }

        void Snow(float dt)
        {
            if (now == Kind.Snow) snowT += dt;
            // A snowman appears after a while of snow and melts a minute and a half after it stops.
            if (now == Kind.Snow && snowT > 25 && !snowman.enabled)
            {
                snowmanX = Random.Range(60, d.Door.x - 200);
                snowman.enabled = true;
                snowmanT = 90;
                Bubble.Popup("누가 눈사람 만들었다!", Desk.W(new Vector2(snowmanX, d.FloorY - 70)));
                foreach (var t in d.Tenants) if (Vector2.Distance(t.P, new Vector2(snowmanX, d.FloorY)) < 300) t.Cheer();
            }
            if (snowman.enabled)
            {
                snowman.transform.position = Desk.W(new Vector2(snowmanX, d.FloorY), 0.5f);
                if (now != Kind.Snow && (snowmanT -= dt) <= 0) snowman.enabled = false;
            }
        }

        void Gusts(float dt)
        {
            if (now != Kind.Windy || (gustT -= dt) > 0) return;
            gustT = Random.Range(4f, 8f);
            d.ScatterCoins();
            foreach (var t in d.Tenants) if (Random.value < 0.25f) t.BlownAway(Wind);
        }
    }
}
