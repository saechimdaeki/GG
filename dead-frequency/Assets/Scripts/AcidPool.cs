using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// The spitter's acid: a glob lobbed in an arc that splashes into a spreading green pool. Standing in it burns
    /// harder the longer you stay, so the counter is to move. Only the host's copy hurts survivors.
    /// </summary>
    public sealed class AcidPool : MonoBehaviour
    {
        public const float Radius = 2.4f;
        public const float Seconds = 7f;
        const float Tick = 0.5f;

        static readonly List<AcidPool> active = new();
        /// <summary>Pools on the ground right now.</summary>
        public static int Active => active.Count;
        static AudioClip hiss;
        static Material globMaterial;

        bool authoritative;
        bool landed;
        Vector3 from;
        Vector3 to;
        float flight;
        float started;
        float nextTick;
        Light glow;
        ParticleSystem bubbles;
        readonly Dictionary<SquadMember, float> soaking = new();

        /// <summary>Spit from <paramref name="mouth"/>; it splashes down at <paramref name="landing"/> after <paramref name="seconds"/>.</summary>
        public static AcidPool Lob(Vector3 mouth, Vector3 landing, float seconds, StationTrial mission, bool authoritative)
        {
            var glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(glob.GetComponent<Collider>());
            glob.name = "Spitter acid";
            glob.layer = 2;
            glob.transform.position = mouth;
            glob.transform.localScale = Vector3.one * 0.22f;
            if (globMaterial == null)
            {
                globMaterial = new Material(Shader.Find("HDRP/Lit"));
                globMaterial.SetColor("_BaseColor", new Color(0.35f, 0.75f, 0.12f));
                globMaterial.SetColor("_EmissiveColor", new Color(0.3f, 0.9f, 0.1f) * 30f);
                globMaterial.SetFloat("_Smoothness", 0.9f);
            }
            glob.GetComponent<Renderer>().sharedMaterial = globMaterial;
            var pool = glob.AddComponent<AcidPool>();
            pool.authoritative = authoritative;
            pool.from = mouth;
            pool.to = landing;
            pool.flight = seconds;
            pool.started = Time.time;
            var trail = ShotFeedback.Burst("Acid trail", new Color(0.4f, 0.95f, 0.15f), new Color(0.6f, 1f, 0.3f),
                0.35f, 0.04f, 0.09f, 0.1f, 0.5f, 0.6f, false, additive: true);
            trail.transform.SetParent(glob.transform, false);
            var emission = trail.emission;
            emission.enabled = true;
            emission.rateOverTime = 70f;
            return pool;
        }

        /// <summary>Is <paramref name="point"/> standing in acid? <paramref name="away"/> points out of the nearest pool.</summary>
        public static bool Escape(Vector3 point, out Vector3 away)
        {
            foreach (var pool in active)
            {
                Vector3 offset = Vector3.ProjectOnPlane(point - pool.transform.position, Vector3.up);
                if (offset.magnitude < Radius + 0.4f && Mathf.Abs(point.y - pool.transform.position.y) < 2f)
                {
                    away = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.forward;
                    return true;
                }
            }
            away = Vector3.zero;
            return false;
        }

        void OnDestroy() => active.Remove(this);

        void Update()
        {
            if (!landed)
            {
                float t = Mathf.Clamp01((Time.time - started) / flight);
                Vector3 position = Vector3.Lerp(from, to, t) + Vector3.up * (4f * 1.6f * t * (1f - t));
                transform.position = position;
                if (t >= 1f)
                    Splash();
                return;
            }
            float age = Time.time - started;
            glow.intensity = 90f * Mathf.Clamp01((Seconds - age) / 1.5f) * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 4f, 0f));
            if (age >= Seconds)
            {
                var emission = bubbles.emission;
                emission.enabled = false;
                if (age >= Seconds + 1.5f)
                    Destroy(gameObject);
                return;
            }
            if (!authoritative || Time.time < nextTick)
                return;
            nextTick = Time.time + Tick;
            foreach (var member in SquadMember.All)
            {
                if (!member.Available)
                {
                    soaking.Remove(member);
                    continue;
                }
                Vector3 offset = member.body.position - transform.position;
                if (Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > Radius || Mathf.Abs(offset.y) > 1.5f)
                {
                    soaking.Remove(member);
                    continue;
                }
                // Ramps up the longer you stand in it.
                soaking.TryGetValue(member, out float soaked);
                soaking[member] = soaked + Tick;
                member.hurt(Mathf.RoundToInt(Mathf.Lerp(2f, 8f, soaked / 2.5f)));
            }
        }

        void Splash()
        {
            landed = true;
            started = Time.time;
            Vector3 point = to;
            if (Physics.Raycast(to + Vector3.up * 1.5f, Vector3.down, out RaycastHit floor, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                point = floor.point;
            transform.SetPositionAndRotation(point, Quaternion.identity);
            transform.localScale = Vector3.one;
            GetComponent<Renderer>().enabled = false;
            foreach (var trail in GetComponentsInChildren<ParticleSystem>())
                Destroy(trail.gameObject);
            active.Add(this);
            ShotFeedback.Scorch(point, Radius, new Color(0.18f, 0.42f, 0.05f, 0.9f), Seconds + 1.5f);
            bubbles = ShotFeedback.Burst("Acid bubbles", new Color(0.35f, 0.9f, 0.1f), new Color(0.6f, 1f, 0.25f),
                0.8f, 0.05f, 0.16f, 0.2f, 0.9f, -0.15f, false, additive: true);
            bubbles.transform.SetParent(transform, false);
            bubbles.transform.SetLocalPositionAndRotation(Vector3.up * 0.02f, Quaternion.LookRotation(Vector3.up));
            var shape = bubbles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Radius * 1.4f, Radius * 1.4f, 0.02f);
            var emission = bubbles.emission;
            emission.enabled = true;
            emission.rateOverTime = 90f;
            glow = new GameObject("Acid glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 0.6f;
            glow.type = LightType.Point;
            glow.color = new Color(0.45f, 1f, 0.2f);
            glow.range = Radius * 2.5f;
            glow.shadows = LightShadows.None;
            var voice = gameObject.AddComponent<AudioSource>();
            voice.clip = hiss ??= Hiss();
            voice.loop = true;
            voice.spatialBlend = 1f;
            voice.maxDistance = 18f;
            voice.volume = 0.35f;
            voice.Play();
        }

        /// <summary>A second of filtered noise, looped: the sizzle of acid on tile.</summary>
        static AudioClip Hiss()
        {
            const int rate = 22050;
            var samples = new float[rate];
            var random = new System.Random(7);
            float smooth = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                smooth += (white - smooth) * 0.35f;
                float crackle = random.NextDouble() < 0.003 ? (float)random.NextDouble() : 0f;
                samples[i] = (white - smooth) * 0.5f + crackle * 0.6f;
            }
            var clip = AudioClip.Create("Acid hiss", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
