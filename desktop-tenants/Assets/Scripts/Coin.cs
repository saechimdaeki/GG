using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// A rent payment. Pops out of a tenant and lands on whatever edge is below. Sweeping the mouse
    /// over it (or leaving it long enough) sends it flying to the building's money sign.
    /// </summary>
    public class Coin : MonoBehaviour
    {
        static Sprite sprite, boxSprite;

        /// <summary>A parcel from the delivery-box event rather than a coin: opening it is an unboxing.</summary>
        public bool IsBox { get; private set; }
        readonly Body b = new Body();
        SpriteRenderer sr;
        float size, t;
        Vector2 flyFrom;
        float flyT = -1;

        public int Value { get; private set; }
        public Vector2 P => b.p;
        public int On => b.on;
        /// <summary>Seconds since it was dropped.</summary>
        public float Age => t;
        /// <summary>On its way to the money sign; no longer pickable.</summary>
        public bool Collecting => flyT >= 0;
        /// <summary>Reached the money sign: count it and remove it.</summary>
        public bool Arrived => flyT >= 1;

        /// <summary>Batted off its edge (cats do this).</summary>
        public void Knock(int dir)
        {
            if (!Collecting) b.Drop(new Vector2(dir * 240, -240));
        }

        public Rect Bounds => new Rect(b.p.x - size, b.p.y - size * 2, size * 2, size * 2);

        /// <param name="vel">Launch velocity; default pops it up out of the tenant's hand.</param>
        public void Init(Desk d, Vector2 at, int value, Vector2? vel = null, bool box = false)
        {
            if (!sprite) sprite = Pixels.Make(Pixels.Coin, Pixels.CoinPalette, d.Tune.pixel);
            if (!boxSprite) boxSprite = Props.Make(Props.Box, d.Tune.pixel, new Vector2(0.5f, 0));
            IsBox = box;
            Value = value;
            size = 7 * d.Tune.pixel / 2;
            b.p = at;
            b.Drop(vel ?? new Vector2(Random.Range(-90f, 90f), -280));
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = box ? boxSprite : sprite;
            sr.sortingOrder = 9;
        }

        /// <summary>Starts the flight to the money sign.</summary>
        public void Collect()
        {
            if (Collecting) return;
            flyFrom = b.p;
            flyT = 0;
            sr.sortingOrder = 30;
        }

        public void Tick(Desk d, float dt, Vector2 sign)
        {
            t += dt;
            if (Collecting)
            {
                // Arc up and over to the sign, spinning, in about half a second.
                flyT = Mathf.Min(1, flyT + dt * 2f);
                var p = Vector2.Lerp(flyFrom, sign, flyT * flyT) - new Vector2(0, Mathf.Sin(flyT * Mathf.PI) * 80);
                transform.position = Desk.W(p - new Vector2(0, size));
                transform.localScale = new Vector3(Mathf.Cos(flyT * 20), 1, 1);
                return;
            }
            if (b.Grounded) b.Ride(d, dt, out _);
            else b.Fall(d, dt, size * 2);
            float bob = b.Grounded ? Mathf.Abs(Mathf.Sin(t * 3)) * 3 : 0;
            transform.position = Desk.W(b.p - new Vector2(0, bob));
        }
    }
}
