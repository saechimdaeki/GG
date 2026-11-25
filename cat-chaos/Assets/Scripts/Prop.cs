using UnityEngine;
using UnityEngine.Rendering;

namespace CatChaos
{
    public enum PropKind { Thing, Fragile, Plant, Tissue, Tv }

    /// <summary>Something the cat can push around. Reports tipping, falling, breaking, spilling and unrolling.</summary>
    [DisallowMultipleComponent]
    public sealed class Prop : MonoBehaviour
    {
        public string Label = "물건";
        public PropKind Kind;
        public bool Big;
        public Rigidbody Body;
        public bool Tipped, Dropped, Broken;
        public float Unrolled;
        public Transform Paper;   // tissue: the visual roll that shrinks as it unrolls

        bool _armed;
        float _baseY, _lastHit;
        Vector3 _lastMark;
        LineRenderer _ribbon;

        /// <summary>Every tissue trail on the floor (the robot vacuum eats them from the start).</summary>
        public static readonly System.Collections.Generic.List<LineRenderer> Trails = new();

        Game G => Game.I;
        bool Playing => G && G.S == Game.State.Play;
        bool Tilted => Kind != PropKind.Tissue && Vector3.Angle(transform.up, Vector3.up) > G.T.tipAngle;

        /// <summary>Start counting from where it rests now (props settle before play starts).</summary>
        public void Arm()
        {
            _baseY = transform.position.y;
            Tipped = Tilted;
            Dropped = false;
            _lastMark = transform.position;
            _armed = true;
        }

        void FixedUpdate()
        {
            if (!_armed || Broken || !Playing) return;
            if (!Tipped && Tilted)
            {
                Tipped = true;
                G.OnTip(this);
                if (Kind == PropKind.Plant) SpillSoil();
            }
            if (!Dropped && transform.position.y < _baseY - G.T.dropDistance)
            {
                Dropped = true;
                G.OnDrop(this);
            }
            if (Kind == PropKind.Tissue) Unroll();
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v > 0.7f && Time.time - _lastHit > 0.12f)
            {
                _lastHit = Time.time;
                Sfx.Impact(transform.position, v, Body.mass, Kind == PropKind.Fragile);
            }
            bool byCat = c.rigidbody && c.rigidbody.GetComponent<Cat>();
            if (_armed && Playing && !Broken && Kind == PropKind.Fragile && v > G.T.breakSpeed && !byCat) Shatter();
        }

        public void Shatter()
        {
            Broken = true;
            var rends = GetComponentsInChildren<MeshRenderer>();
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            int n = Mathf.Clamp(Mathf.RoundToInt(b.size.magnitude * 45), 6, 16);
            for (int i = 0; i < n; i++)
            {
                var at = b.center + Vector3.Scale(Random.insideUnitSphere * 0.5f, b.size);
                float s = b.size.magnitude * Random.Range(0.1f, 0.22f);
                var mat = rends[Random.Range(0, rends.Length)].sharedMaterial;
                Debris.Spawn(at, new Vector3(s, s * 0.35f, s * Random.Range(0.6f, 1.2f)), mat,
                    Body.linearVelocity * 0.4f + Random.insideUnitSphere * 1.6f + Vector3.up * 1.2f);
            }
            Sfx.Shatter(transform.position);
            G.OnBreak(this);
            Destroy(gameObject);
        }

        void SpillSoil()
        {
            var b = GetComponentInChildren<Renderer>().bounds;
            foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var soil = Mats.Flat(new Color(0.27f, 0.18f, 0.11f), 0.05f);
            Vector3 spill = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            for (int i = 0; i < 14; i++)
            {
                float s = Random.Range(0.03f, 0.07f) * Mathf.Clamp(b.size.y * 2f, 0.6f, 1.6f);
                Debris.Spawn(transform.position + transform.up * b.size.y * 0.7f + Random.insideUnitSphere * 0.05f,
                    new Vector3(s, s * 0.5f, s), soil, spill * Random.Range(0.6f, 2f) + Random.insideUnitSphere * 0.6f, 0.02f);
            }
            Sfx.Soil(transform.position);
            G.OnSoil(this);
        }

        /// <summary>A rolling tissue leaves a paper trail on whatever it rolls over.</summary>
        void Unroll()
        {
            if (Unrolled >= G.T.tissueLength) return;
            var p = transform.position;
            if (Body.angularVelocity.magnitude < 1.5f) { _lastMark = p; return; }
            if (!Physics.Raycast(p, Vector3.down, out var hit, 0.15f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_lastMark.x, _lastMark.z));
            if (d < 0.08f) return;
            if (!_ribbon)
            {
                var g = new GameObject("휴지 자국");
                g.transform.rotation = Quaternion.Euler(90, 0, 0);
                _ribbon = g.AddComponent<LineRenderer>();
                _ribbon.alignment = LineAlignment.TransformZ;
                _ribbon.useWorldSpace = true;
                _ribbon.widthMultiplier = 0.1f;
                _ribbon.numCornerVertices = 2;
                _ribbon.generateLightingData = true;
                _ribbon.sharedMaterial = Mats.Saved("Paper");
                _ribbon.shadowCastingMode = ShadowCastingMode.Off;
                _ribbon.positionCount = 1;
                _ribbon.SetPosition(0, new Vector3(_lastMark.x, hit.point.y + 0.004f, _lastMark.z));
                Trails.Add(_ribbon);
            }
            _ribbon.positionCount++;
            _ribbon.SetPosition(_ribbon.positionCount - 1, hit.point + Vector3.up * 0.004f);
            Unrolled = Mathf.Min(G.T.tissueLength, Unrolled + d);
            _lastMark = p;
            if (Paper) { float k = Mathf.Lerp(1f, 0.45f, Unrolled / G.T.tissueLength); Paper.localScale = new Vector3(0.11f * k, Paper.localScale.y, 0.11f * k); }
            G.OnUnroll(this, d);
            if (Random.value < 0.3f) Sfx.Paper(p);
        }
    }

    /// <summary>Marks furniture the cat can sharpen its claws on.</summary>
    public sealed class Scratchable : MonoBehaviour
    {
        public string Label = "소파";
    }
}
