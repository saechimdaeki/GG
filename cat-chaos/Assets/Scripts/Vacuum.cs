using UnityEngine;

namespace CatChaos
{
    /// <summary>
    /// The villain: a robot vacuum that leaves its dock now and then and hoovers up the cat's
    /// mess (shards, soil, tissue trails), taking points back. Three paw swats flip it over.
    /// </summary>
    public sealed class Vacuum : MonoBehaviour
    {
        const float Radius = 0.17f, SuckReach = 0.22f;

        public Rigidbody Body;
        public bool Flipped;

        int _hp;
        float _speed, _repath, _stuckT, _reverseUntil, _wanderYaw, _flash;
        Vector3 _target, _lastPos;
        bool _hasTarget;
        float _phase = Random.value * 10f;
        Transform _brushL, _brushR;
        Material _led;
        AudioSource _hum;
        Game G => Game.I;

        public static Vacuum Spawn(Vector3 at, float yaw, Tuning t)
        {
            var go = new GameObject("로봇청소기");
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.01f, Quaternion.Euler(0, yaw, 0));
            var v = go.AddComponent<Vacuum>();
            v._hp = t.vacuumHits;
            v._speed = t.vacuumSpeed;
            v._wanderYaw = yaw;
            var tr = go.transform;

            var shell = Mats.Flat(new Color(0.93f, 0.93f, 0.95f), 0.75f);
            var dark = Mats.Flat(new Color(0.16f, 0.17f, 0.2f), 0.5f);
            var body = Mats.Shape(PrimitiveType.Cylinder, tr, new Vector3(0, 0.045f, 0), new Vector3(Radius * 2, 0.04f, Radius * 2), shell);
            Mats.Shape(PrimitiveType.Cylinder, tr, new Vector3(0, 0.087f, 0), new Vector3(Radius * 1.7f, 0.004f, Radius * 1.7f), dark);
            Mats.Shape(PrimitiveType.Cylinder, tr, new Vector3(0, 0.095f, -0.03f), new Vector3(0.07f, 0.008f, 0.07f), shell);   // lidar turret
            Mats.Shape(PrimitiveType.Cube, tr, new Vector3(0, 0.04f, Radius - 0.02f), new Vector3(0.26f, 0.05f, 0.05f), dark);  // front bumper
            v._led = new Material(Mats.Flat(new Color(0.3f, 0.9f, 1f), 0.9f, 0, 3f));
            Mats.Shape(PrimitiveType.Sphere, tr, new Vector3(0, 0.092f, 0.08f), new Vector3(0.05f, 0.015f, 0.02f), v._led);
            // two "eyes" so it reads as a little character
            foreach (float x in new[] { -0.045f, 0.045f })
                Mats.Shape(PrimitiveType.Sphere, tr, new Vector3(x, 0.07f, Radius - 0.005f), new Vector3(0.03f, 0.03f, 0.01f), v._led);
            v._brushL = Brush(tr, new Vector3(-0.12f, 0.008f, 0.12f), dark);
            v._brushR = Brush(tr, new Vector3(0.12f, 0.008f, 0.12f), dark);

            // disc-shaped hull: the cylinder mesh on a child carrying the body's scale
            var hull = new GameObject("hull").transform;
            hull.SetParent(tr, false);
            hull.localPosition = body.transform.localPosition;
            hull.localScale = body.transform.localScale;
            var mc = hull.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;

            var rb = v.Body = go.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.centerOfMass = new Vector3(0, 0.02f, 0);
            var p = go.AddComponent<Prop>();
            p.Label = "로봇청소기";
            p.Big = true;
            p.Body = rb;

            v._hum = go.AddComponent<AudioSource>();
            v._hum.clip = Sfx.Hum;
            v._hum.loop = true;
            v._hum.spatialBlend = 1f;
            v._hum.rolloffMode = AudioRolloffMode.Linear;
            v._hum.minDistance = 0.5f;
            v._hum.maxDistance = 7f;
            v._hum.volume = 0.35f;
            v._hum.Play();
            v._lastPos = at;
            return v;
        }

        static Transform Brush(Transform parent, Vector3 at, Material m)
        {
            var b = new GameObject("brush").transform;
            b.SetParent(parent, false);
            b.localPosition = at;
            for (int i = 0; i < 3; i++)
                Mats.Shape(PrimitiveType.Cube, b, Vector3.zero, new Vector3(0.12f, 0.004f, 0.008f), m, new Vector3(0, i * 60, 0));
            return b;
        }

        /// <summary>Swatted by the cat. The last hit pops it over.</summary>
        public void Hit(Vector3 from)
        {
            if (Flipped) return;
            _hp--;
            _flash = 0.3f;
            Sfx.Beep(transform.position, _hp > 0 ? 1f : 0.7f);
            if (_hp > 0)
            {
                G.Hud.Pop(_hp == 1 ? "삐빅?! 한 번 더!" : "삐빅!", transform.position + Vector3.up * 0.35f, new Color(0.5f, 0.9f, 1f), 22);
                _reverseUntil = Time.time + 0.6f;
                return;
            }
            var away = Vector3.ProjectOnPlane(transform.position - from, Vector3.up).normalized;
            Body.AddForce((Vector3.up * 3.2f + away * 0.8f) * Body.mass, ForceMode.Impulse);
            Body.AddTorque(Vector3.Cross(Vector3.up, away) * Body.mass * 0.25f, ForceMode.Impulse);
        }

        void Update()
        {
            float spin = Flipped ? 200f : 900f;
            _brushL.Rotate(0, spin * Time.deltaTime, 0, Space.Self);
            _brushR.Rotate(0, -spin * Time.deltaTime, 0, Space.Self);
            _flash -= Time.deltaTime;
            var c = Flipped ? new Color(1f, 0.25f, 0.2f) : _flash > 0 ? new Color(1f, 0.4f, 0.3f) : new Color(0.3f, 0.9f, 1f);
            if (Flipped) c *= 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            _led.SetColor("_EmissionColor", c * 3f);
            _led.SetColor("_BaseColor", c);
        }

        void FixedUpdate()
        {
            if (Flipped || G.S != Game.State.Play) { if (_hum.isPlaying && (Flipped || G.S == Game.State.Result)) _hum.Stop(); return; }
            if (Vector3.Angle(transform.up, Vector3.up) > 50f) { Flipped = true; _hum.Stop(); return; }
            bool ground = Physics.Raycast(transform.position + Vector3.up * 0.05f, Vector3.down, 0.12f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (!ground) return;

            if ((_repath -= Time.fixedDeltaTime) <= 0) { _repath = 0.4f; PickTarget(); }
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 want;
            float speed = _speed;
            if (Time.time < _reverseUntil) { want = Quaternion.Euler(0, 120, 0) * fwd; speed = -_speed * 0.7f; }
            else if (_hasTarget) want = Vector3.ProjectOnPlane(_target - transform.position, Vector3.up).normalized;
            else
            {
                _wanderYaw += Mathf.Sin(Time.time * 0.7f + _phase) * 40f * Time.fixedDeltaTime;
                want = Quaternion.Euler(0, _wanderYaw, 0) * Vector3.forward;
            }
            if (want.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(want, Vector3.up);
                var yawOnly = Quaternion.Euler(0, Quaternion.RotateTowards(Quaternion.Euler(0, Body.rotation.eulerAngles.y, 0), face, 220f * Time.fixedDeltaTime).eulerAngles.y, 0);
                Body.MoveRotation(yawOnly);
            }
            float align = Mathf.Clamp01(Vector3.Dot(fwd, want) * 1.5f);
            var v = fwd * speed * (speed > 0 ? align : 1f);
            Body.linearVelocity = new Vector3(v.x, Body.linearVelocity.y, v.z);

            // stuck against furniture: back up and turn away
            _stuckT += Time.fixedDeltaTime;
            if (_stuckT > 1.2f)
            {
                if ((transform.position - _lastPos).magnitude < 0.08f && Time.time > _reverseUntil)
                {
                    _reverseUntil = Time.time + 0.9f;
                    _wanderYaw += Random.Range(90f, 200f);
                    _hasTarget = false;
                    _repath = 1.2f;
                }
                _lastPos = transform.position;
                _stuckT = 0;
            }
            Suck();
        }

        void PickTarget()
        {
            float best = 6f;
            _hasTarget = false;
            var p = transform.position;
            foreach (var d in Debris.Pieces)
            {
                if (!d || d.transform.position.y > 0.25f) continue;
                float dist = (d.transform.position - p).sqrMagnitude;
                if (dist < best * best) { best = Mathf.Sqrt(dist); _target = d.transform.position; _hasTarget = true; }
            }
            foreach (var r in Prop.Trails)
            {
                if (!r || r.positionCount < 2) continue;
                var q = r.GetPosition(0);
                if (q.y > 0.25f) continue;
                float dist = (q - p).magnitude;
                if (dist < best) { best = dist; _target = q; _hasTarget = true; }
            }
        }

        void Suck()
        {
            var p = transform.position;
            foreach (var d in Debris.Pieces)
            {
                if (!d || d.transform.position.y > 0.25f) continue;
                if ((d.transform.position - p).sqrMagnitude < SuckReach * SuckReach)
                {
                    Destroy(d);
                    G.OnClean(p);
                }
            }
            foreach (var r in Prop.Trails)
            {
                if (!r || r.positionCount < 2) continue;
                var q = r.GetPosition(0);
                if (q.y > 0.25f || (q - p).sqrMagnitude > SuckReach * SuckReach) continue;
                var pts = new Vector3[r.positionCount];
                r.GetPositions(pts);
                r.positionCount--;
                for (int i = 0; i < r.positionCount; i++) r.SetPosition(i, pts[i + 1]);
                G.OnClean(p);
            }
        }
    }
}
