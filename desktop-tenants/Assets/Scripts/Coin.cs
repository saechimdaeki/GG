using UnityEngine;

namespace DesktopTenants
{
    /// <summary>A rent payment. Pops out of a tenant, lands on whatever edge is below, waits to be clicked.</summary>
    public class Coin : MonoBehaviour
    {
        static Sprite sprite;
        readonly Body b = new Body();
        SpriteRenderer sr;
        float size, t;
        public int Value { get; private set; }
        public Vector2 P => b.p;
        public int On => b.on;

        /// <summary>Batted off its edge (cats do this).</summary>
        public void Knock(int dir) => b.Drop(new Vector2(dir * 240, -240));
        public Rect Bounds => new Rect(b.p.x - size, b.p.y - size * 2, size * 2, size * 2);

        public void Init(Desk d, Vector2 at, int value)
        {
            if (!sprite) sprite = Pixels.Make(Pixels.Coin, Pixels.CoinPalette, d.Tune.pixel);
            Value = value;
            size = 7 * d.Tune.pixel / 2;
            b.p = at;
            b.Drop(new Vector2(Random.Range(-90f, 90f), -280));
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 9;
        }

        public void Tick(Desk d, float dt)
        {
            t += dt;
            if (b.Grounded) b.Ride(d, dt, out _);
            else b.Fall(d, dt, size * 2);
            float bob = b.Grounded ? Mathf.Abs(Mathf.Sin(t * 3)) * 3 : 0;
            transform.position = Desk.W(b.p - new Vector2(0, bob));
        }
    }
}
