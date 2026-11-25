using System.Collections.Generic;
using UnityEngine;

namespace CatChaos
{
    /// <summary>A glowing catnip sprig. Walk into it for a fever (faster, stronger paw, double points).</summary>
    public sealed class Catnip : MonoBehaviour
    {
        public static Catnip Spawn(Vector3 at)
        {
            var go = new GameObject("캣닢");
            go.transform.position = at + Vector3.up * 0.12f;
            var leaf = Mats.Flat(new Color(0.45f, 0.95f, 0.4f), 0.4f, 0, 1.2f);
            for (int i = 0; i < 5; i++)
                Mats.Shape(PrimitiveType.Sphere, go.transform, Quaternion.Euler(0, i * 72, 0) * new Vector3(0.04f, 0.02f * (i % 2), 0),
                    new Vector3(0.09f, 0.02f, 0.045f), leaf, new Vector3(-20, i * 72, 0));
            Mats.Shape(PrimitiveType.Cylinder, go.transform, new Vector3(0, -0.04f, 0), new Vector3(0.012f, 0.05f, 0.012f), Mats.Flat(new Color(0.3f, 0.6f, 0.25f)));
            var l = new GameObject("glow").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.type = LightType.Point;
            l.color = new Color(0.5f, 1f, 0.5f);
            l.intensity = 1.2f;
            l.range = 1.2f;
            l.shadows = LightShadows.None;
            return go.AddComponent<Catnip>();
        }

        Vector3 _base;
        void Start() => _base = transform.position;

        void Update()
        {
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.03f;
            transform.Rotate(0, 90 * Time.deltaTime, 0);
            var g = Game.I;
            if (g.S != Game.State.Play) return;
            if ((g.Cat.transform.position + Vector3.up * 0.15f - transform.position).sqrMagnitude < 0.35f * 0.35f)
            {
                g.OnCatnip(transform.position);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>A butterfly that drifts in through a window and flutters about at cat height. Swat it to catch it.</summary>
    public sealed class Butterfly : MonoBehaviour
    {
        public static readonly List<Butterfly> All = new();

        Transform _l, _r;
        Vector3 _home, _goal, _exit;
        float _life, _repick, _phase;
        bool _leaving;

        public static Butterfly Spawn(Vector3 window, Vector3 home, float life)
        {
            var go = new GameObject("나비");
            go.transform.position = window;
            var b = go.AddComponent<Butterfly>();
            b._home = home;
            b._exit = window;
            b._life = life;
            b._goal = home;
            b._phase = Random.value * 10f;
            var wing = Mats.Flat(Color.HSVToRGB(Random.value, 0.6f, 1f), 0.3f, 0, 0.8f);
            Mats.Shape(PrimitiveType.Capsule, go.transform, Vector3.zero, new Vector3(0.012f, 0.025f, 0.012f), Mats.Flat(new Color(0.15f, 0.1f, 0.1f)), new Vector3(90, 0, 0));
            b._l = Wing(go.transform, -1, wing);
            b._r = Wing(go.transform, 1, wing);
            All.Add(b);
            return b;
        }

        static Transform Wing(Transform body, int side, Material m)
        {
            var hinge = new GameObject("wing").transform;
            hinge.SetParent(body, false);
            var w = Mats.Shape(PrimitiveType.Sphere, hinge, new Vector3(side * 0.06f, 0, 0.005f), new Vector3(0.12f, 0.004f, 0.1f), m);
            Mats.NoShadows(w);
            return hinge;
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            float flap = Mathf.Sin(Time.time * 28f + _phase) * 55f;
            _l.localRotation = Quaternion.Euler(0, 0, flap);
            _r.localRotation = Quaternion.Euler(0, 0, -flap);
            var g = Game.I;
            if (g.S == Game.State.Play) _life -= dt;
            if (_life <= 0 && !_leaving) { _leaving = true; _goal = _exit; }
            var p = transform.position;
            var cat = g.Cat.transform.position;
            if (!_leaving && ((_repick -= dt) <= 0 || (p - _goal).sqrMagnitude < 0.04f))
            {
                _repick = Random.Range(1.2f, 2.5f);
                var r = Random.insideUnitCircle * 1.6f;
                _goal = _home + new Vector3(r.x, Random.Range(0.25f, 1.1f), r.y);
            }
            float speed = 0.9f;
            var flat = new Vector3(p.x - cat.x, 0, p.z - cat.z);
            if (!_leaving && flat.magnitude < 0.7f) { _goal = p + flat.normalized * 0.8f + Vector3.up * 0.3f; _goal.y = Mathf.Clamp(_goal.y, 0.25f, 1.4f); speed = 1.6f; }
            var wobble = new Vector3(Mathf.Sin(Time.time * 5f + _phase), Mathf.Sin(Time.time * 7f + _phase) * 1.5f, Mathf.Cos(Time.time * 4f + _phase)) * 0.25f;
            var dir = (_goal - p).normalized + wobble;
            transform.position = p + dir * speed * dt;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z) + Vector3.forward * 0.001f), dt * 4f);
            if (_leaving && (p - _exit).sqrMagnitude < 0.05f) Destroy(gameObject);
        }

        /// <summary>The paw got it.</summary>
        public void Catch()
        {
            if (!All.Remove(this)) return;
            for (int i = 0; i < 8; i++)
                Debris.Spawn(transform.position, Vector3.one * 0.012f, Mats.Flat(new Color(1f, 0.95f, 0.6f), 0.5f, 0, 2f), Random.insideUnitSphere * 1.2f + Vector3.up, 0.005f);
            Game.I.OnButterfly(transform.position);
            Destroy(gameObject);
        }
    }

    /// <summary>Bobbing "!" over the owner's favourite thing of the moment.</summary>
    public sealed class Marker : MonoBehaviour
    {
        public Transform Target;

        public static Marker Spawn(Transform target)
        {
            var go = new GameObject("표적");
            var m = Mats.Flat(new Color(1f, 0.85f, 0.2f), 0.5f, 0, 2.2f);
            Mats.NoShadows(Mats.Shape(PrimitiveType.Capsule, go.transform, new Vector3(0, 0.07f, 0), new Vector3(0.05f, 0.06f, 0.05f), m));
            Mats.NoShadows(Mats.Shape(PrimitiveType.Sphere, go.transform, new Vector3(0, -0.04f, 0), Vector3.one * 0.05f, m));
            var mk = go.AddComponent<Marker>();
            mk.Target = target;
            mk.LateUpdate();
            return mk;
        }

        void LateUpdate()
        {
            if (!Target) return;
            var b = Target.GetComponentInChildren<Renderer>().bounds;
            transform.position = new Vector3(Target.position.x, b.max.y + 0.22f + Mathf.Sin(Time.time * 4f) * 0.04f, Target.position.z);
            transform.rotation = Quaternion.Euler(0, Time.time * 120f, 0);
        }
    }
}
