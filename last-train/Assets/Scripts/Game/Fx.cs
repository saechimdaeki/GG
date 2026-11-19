using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    /// <summary>
    /// Every short-lived effect: blood bursts, explosions, extinguisher spray (shared particle
    /// systems fed with Emit), swing fans and rings (meshes that fade), blood pools on the
    /// asphalt, flash lights and camera shake.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public Material add, alpha;

        ParticleSystem _blood, _sparks, _smoke, _spray, _debris;
        readonly List<(Transform t, Material m, float life, float max, float grow, Color c)> _fading = new();
        readonly List<(Light l, float life, float max, float energy)> _flashes = new();
        readonly Queue<Transform> _pools = new();
        Mesh _quad, _ring;

        public static Vector3 V3(Vector2 p, float y = 0) => new(p.x, y, p.y);

        public void Init(Material addMat, Material alphaMat)
        {
            var soft = SoftDot(64);
            add = new Material(addMat);
            add.SetTexture("_BaseMap", soft);
            alpha = new Material(alphaMat);
            alpha.SetTexture("_BaseMap", soft);
            addMat = add;
            alphaMat = alpha;
            _blood = System("Blood", alphaMat, 0.55f, 9f, 0.08f, 0.2f, 1.6f);
            _sparks = System("Sparks", addMat, 0.35f, 12f, 0.05f, 0.12f, 0.4f);
            _smoke = System("Smoke", alphaMat, 1.4f, 2.5f, 0.6f, 1.6f, -0.05f);
            _spray = System("Spray", alphaMat, 0.45f, 11f, 0.25f, 0.7f, 0f);
            _debris = System("Debris", alphaMat, 0.9f, 7f, 0.06f, 0.14f, 2.2f);
            _quad = BuildQuad();
            _ring = BuildRing(0.92f, 1f, 64);
        }

        ParticleSystem System(string name, Material mat, float life, float speed, float sizeMin, float sizeMax, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 4000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, name == "Smoke" ? 2.2f : 0.4f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 at, int n, Color c, Vector3 dirBias, float spread = 1f)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < n; i++)
            {
                p.position = at + Random.insideUnitSphere * 0.15f;
                var d = (Random.insideUnitSphere * spread + dirBias).normalized;
                p.velocity = d * Random.Range(0.3f, 1f) * ps.main.startSpeed.constantMax;
                p.startColor = c * Random.Range(0.75f, 1.1f);
                ps.Emit(p, 1);
            }
        }

        public void BloodBurst(Vector2 at, Vector2 away, int n = 10)
        {
            Emit(_blood, V3(at, 1.1f), n, new Color(0.45f, 0.02f, 0.03f, 1), V3(away, 0.7f), 0.8f);
        }

        /// <summary>Grey dust and grit kicked up from a container being rummaged through.</summary>
        public void Dust(Vector2 at, int n) => Emit(_smoke, V3(at, 0.7f), n, new Color(0.55f, 0.52f, 0.48f, 0.45f), Vector3.up * 0.4f, 0.6f);

        /// <summary>Red flare smoke rising from a supply drop.</summary>
        public void Flare(Vector2 at) => Emit(_smoke, V3(at, 1.0f), 2, new Color(0.85f, 0.15f, 0.12f, 0.55f), Vector3.up * 2.5f, 0.3f);

        public void HitSparks(Vector2 at, Color c, int n = 4) => Emit(_sparks, V3(at, 1.0f), n, c, Vector3.up * 0.3f);

        public void Spray(Vector2 from, Vector2 dir, float reach, float half)
        {
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 70; i++)
            {
                float a = Random.Range(-half, half);
                var d = Rotate(dir, a * Mathf.Rad2Deg);
                p.position = V3(from + dir * 0.5f, 1.0f);
                p.velocity = V3(d, Random.Range(-0.05f, 0.15f)) * Random.Range(reach * 0.9f, reach * 2.2f);
                p.startColor = new Color(0.92f, 0.95f, 1f, 0.55f);
                p.startSize = Random.Range(0.3f, 0.8f);
                _spray.Emit(p, 1);
            }
        }

        public void Explosion(Vector2 at, float r)
        {
            Emit(_sparks, V3(at, 0.6f), 60, new Color(1f, 0.6f, 0.2f), Vector3.up * 0.6f, 1f);
            Emit(_smoke, V3(at, 0.8f), 18, new Color(0.18f, 0.16f, 0.15f, 0.7f), Vector3.up, 0.8f);
            Emit(_debris, V3(at, 0.5f), 20, new Color(0.25f, 0.22f, 0.2f), Vector3.up * 1.2f, 1f);
            Ring(at, r, new Color(1f, 0.55f, 0.15f), 0.4f);
            Disc(at, r * 0.9f, new Color(1f, 0.45f, 0.1f, 0.6f), 0.25f, add);
            FlashLight(at, new Color(1f, 0.55f, 0.2f), r * 4f, 14f, 0.35f);
        }

        public void FlashLight(Vector2 at, Color c, float range, float energy, float life)
        {
            var go = new GameObject("flash");
            go.transform.SetParent(transform);
            go.transform.position = V3(at, 1.5f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = energy;
            l.shadows = LightShadows.None;
            _flashes.Add((l, life, life, energy));
        }

        /// <summary>A thin expanding ring on the ground.</summary>
        public void Ring(Vector2 at, float r, Color c, float life)
        {
            var t = Piece(_ring, add, c, out var m);
            t.position = V3(at, 0.08f);
            t.localScale = Vector3.one * r * 0.4f;
            _fading.Add((t, m, life, life, r, c));
        }

        public void Disc(Vector2 at, float r, Color c, float life, Material mat)
        {
            var t = Piece(_quad, mat, c, out var m);
            t.position = V3(at, 0.06f);
            t.localScale = new Vector3(r * 2, 1, r * 2);
            t.GetComponent<MeshFilter>().sharedMesh = DiscMesh;
            _fading.Add((t, m, life, life, 0, c));
        }

        /// <summary>The pale fan of a pipe swing or extinguisher blast.</summary>
        public void Fan(Vector2 from, Vector2 dir, float reach, float half, Color c, float life = 0.16f)
        {
            var mesh = FanMesh(half, 14);
            var t = Piece(mesh, add, c, out var m);
            t.position = V3(from, 0.9f);
            t.rotation = Quaternion.LookRotation(V3(dir), Vector3.up);
            t.localScale = Vector3.one * reach;
            _fading.Add((t, m, life, life, 0, c));
        }

        /// <summary>A dark pool on the asphalt where a zombie fell; old ones fade.</summary>
        public void BloodPool(Vector2 at, float size)
        {
            if (_pools.Count > 160) Destroy(_pools.Dequeue().gameObject);
            var t = Piece(DiscMesh, alpha, new Color(0.22f, 0.01f, 0.02f, 0.85f), out _);
            t.position = V3(at + Random.insideUnitCircle * 0.3f, 0.015f + _pools.Count * 0.00005f);
            t.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            t.localScale = new Vector3(Random.Range(0.8f, 1.4f), 1, Random.Range(0.6f, 1.1f)) * size;
            _pools.Enqueue(t);
        }

        Transform Piece(Mesh mesh, Material baseMat, Color c, out Material m)
        {
            var go = new GameObject("fx");
            go.transform.SetParent(transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            m = new Material(baseMat);
            m.SetColor("_BaseColor", c);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                var f = _fading[i];
                f.life -= dt;
                float k = Mathf.Clamp01(f.life / f.max);
                var c = f.c;
                c.a *= k;
                f.m.SetColor("_BaseColor", c);
                if (f.grow > 0) f.t.localScale = Vector3.one * f.grow * (1 - k * 0.6f);
                _fading[i] = f;
                if (f.life > 0) continue;
                Destroy(f.t.gameObject);
                Destroy(f.m);
                _fading.RemoveAt(i);
            }
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var f = _flashes[i];
                f.life -= dt;
                f.l.intensity = f.energy * Mathf.Clamp01(f.life / f.max);
                _flashes[i] = f;
                if (f.life > 0) continue;
                Destroy(f.l.gameObject);
                _flashes.RemoveAt(i);
            }
        }

        public static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>A white dot that fades to clear at the edge: particles and blood pools.</summary>
        public static Texture2D SoftDot(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, Mathf.InverseLerp(0.35f, 1f, d))));
            }
            t.Apply();
            return t;
        }

        static Mesh _disc;
        public static Mesh DiscMesh => _disc ??= BuildRing(0f, 0.5f, 40);

        public static Mesh BuildRing(float inner, float outer, int seg)
        {
            var v = new Vector3[seg * 2];
            var uv = new Vector2[seg * 2];
            var tri = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = d * inner;
                v[i * 2 + 1] = d * outer;
                uv[i * 2] = new Vector2(0.5f, 0.5f);
                uv[i * 2 + 1] = inner == 0 ? new Vector2(0.5f + d.x * 0.5f, 0.5f + d.z * 0.5f) : new Vector2(0.5f, 0.5f);
                int n = (i + 1) % seg;
                tri[i * 6] = i * 2; tri[i * 6 + 1] = n * 2; tri[i * 6 + 2] = i * 2 + 1;
                tri[i * 6 + 3] = i * 2 + 1; tri[i * 6 + 4] = n * 2; tri[i * 6 + 5] = n * 2 + 1;
            }
            var m = new Mesh { vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh FanMesh(float half, int steps)
        {
            var v = new List<Vector3> { Vector3.zero };
            var tri = new List<int>();
            for (int s = 0; s <= steps; s++)
            {
                float a = -half + 2 * half * s / steps;
                v.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)));
                if (s > 0) { tri.Add(0); tri.Add(s); tri.Add(s + 1); }
            }
            var uv = new Vector2[v.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0.5f, 0.5f);
            var m = new Mesh { vertices = v.ToArray(), triangles = tri.ToArray(), uv = uv };
            m.RecalculateNormals();
            return m;
        }

        static Mesh BuildQuad()
        {
            var m = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
            m.RecalculateNormals();
            return m;
        }
    }
}
