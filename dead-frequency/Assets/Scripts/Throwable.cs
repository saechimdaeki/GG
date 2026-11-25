using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>
    /// Left 4 Dead's throwables. A pipe bomb beeps and draws every common infected nearby, then blows them apart;
    /// a molotov bursts on impact into a pool of fire that burns whatever walks through it.
    /// In co-op every peer simulates the same throw; only the host's copy damages and lures infected.
    /// </summary>
    public sealed class Throwable : MonoBehaviour
    {
        public enum Kind { PipeBomb, Molotov }

        public const float LureRadius = 26f;
        public const float BlastRadius = 6.5f;
        public const int BlastDamage = 900;
        public const float FuseSeconds = 6f;
        public const float FireRadius = 3.4f;
        public const float FireSeconds = 9f;
        public const int FireDamagePerTick = 35;
        const float FireTick = 0.25f;
        const float FlameRate = 260f;
        const float BottleHeight = 0.3f;
        const float LedNits = 60f;

        static readonly List<Throwable> lures = new();
        static AudioClip beep;
        static Mesh bottleMesh;
        static bool bottleLoaded;
        static Quaternion bottleTurn = Quaternion.identity;
        static readonly Dictionary<string, Material> materials = new();

        Kind kind;
        bool authoritative;
        Rigidbody body;
        Light glow;
        Renderer led;
        AudioSource voice;
        AudioClip boom;
        float armedAt;
        float nextBeep;
        bool burning;
        float burnUntil;
        float nextBurn;
        ParticleSystem wick;
        readonly List<ParticleSystem> fire = new();
        SquadMember decoy;

        public static string Label(Kind kind) => kind == Kind.PipeBomb ? "파이프 폭탄" : "화염병";

        /// <summary>A live pipe bomb within lure range of <paramref name="point"/>, as a target commons can chase.</summary>
        public static SquadMember LureNear(Vector3 point)
        {
            foreach (var lure in lures)
                if (lure != null && (lure.transform.position - point).sqrMagnitude < LureRadius * LureRadius)
                    return lure.decoy;
            return null;
        }

        /// <summary>
        /// Throw along <paramref name="velocity"/>. <paramref name="boom"/> is played pitched down for the blast or shatter.
        /// Only an <paramref name="authoritative"/> copy hurts or lures infected; the others are what co-op clients see.
        /// </summary>
        public static Throwable Launch(Kind kind, Vector3 origin, Vector3 velocity, AudioClip boom, bool authoritative)
        {
            // Spin from the throw itself, so every peer's copy tumbles and rolls the same way.
            Vector3 spin = Vector3.Cross(Vector3.up, velocity).normalized * 11f;
            var node = Model(kind, origin, Quaternion.LookRotation(velocity) * Quaternion.Euler(90f, 0f, 0f));
            var throwable = node.AddComponent<Throwable>();
            throwable.kind = kind;
            throwable.boom = boom;
            throwable.authoritative = authoritative;
            throwable.body = node.AddComponent<Rigidbody>();
            throwable.body.mass = 0.6f;
            throwable.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            throwable.body.linearVelocity = velocity;
            throwable.body.angularVelocity = spin;
            // The player's own capsule must not swallow the throw.
            if (StationTrial.Local != null && StationTrial.Local.TryGetComponent(out Collider self))
                Physics.IgnoreCollision(node.GetComponent<Collider>(), self);
            var lamp = new GameObject("Glow").AddComponent<Light>();
            lamp.transform.SetParent(node.transform, false);
            lamp.type = LightType.Point;
            lamp.color = kind == Kind.PipeBomb ? new Color(1f, 0.12f, 0.08f) : new Color(1f, 0.55f, 0.2f);
            lamp.range = kind == Kind.PipeBomb ? 4f : 3.5f;
            lamp.intensity = kind == Kind.PipeBomb ? 0f : 160f;
            lamp.shadows = LightShadows.None;
            throwable.glow = lamp;
            throwable.voice = node.AddComponent<AudioSource>();
            throwable.voice.spatialBlend = 1f;
            throwable.voice.maxDistance = 40f;
            throwable.armedAt = Time.time;
            if (kind == Kind.PipeBomb)
            {
                var light = node.transform.Find("LED");
                throwable.led = light != null ? light.GetComponent<Renderer>() : null;
                if (authoritative)
                {
                    throwable.decoy = new SquadMember
                    {
                        body = node.transform,
                        hurt = _ => { },
                        knock = (_, _) => { },
                        warn = _ => { },
                        pin = _ => false,
                        drag = _ => false,
                        release = _ => { },
                        blind = _ => { },
                    };
                    lures.Add(throwable);
                }
            }
            else
            {
                // The lit rag trails flame through the air.
                var rag = node.transform.Find("Rag");
                throwable.wick = ShotFeedback.Burst("Molotov wick", new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.35f),
                    0.28f, 0.025f, 0.06f, 0.2f, 0.7f, -0.4f, true);
                throwable.wick.transform.SetParent(rag != null ? rag : node.transform, false);
                var emission = throwable.wick.emission;
                emission.enabled = true;
                emission.rateOverTime = 90f;
                lamp.transform.localPosition = rag != null ? rag.localPosition : Vector3.zero;
            }
            return throwable;
        }

        /// <summary>
        /// The object itself, at rest or in flight: a taped steel pipe with end caps, timer and LED,
        /// or Poly Haven's wine bottle (CC0) filled with fuel and stoppered with a rag.
        /// </summary>
        public static GameObject Model(Kind kind, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(Label(kind));
            root.layer = 2; // ignore raycast: bullets and the crosshair pass through it
            root.transform.SetPositionAndRotation(position, rotation);
            var collider = root.AddComponent<CapsuleCollider>();
            if (kind == Kind.PipeBomb)
            {
                collider.radius = 0.035f;
                collider.height = 0.2f;
                var steel = Lit("Pipe steel", new Color(0.42f, 0.43f, 0.44f), 0.9f, 0.55f);
                var cap = Lit("Pipe cap", new Color(0.25f, 0.24f, 0.23f), 0.85f, 0.4f);
                var tape = Lit("Duct tape", new Color(0.3f, 0.3f, 0.29f), 0.1f, 0.35f);
                Part(root, PrimitiveType.Cylinder, "Pipe", Vector3.zero, new Vector3(0.056f, 0.08f, 0.056f), steel);
                Part(root, PrimitiveType.Cylinder, "Cap top", Vector3.up * 0.085f, new Vector3(0.068f, 0.012f, 0.068f), cap);
                Part(root, PrimitiveType.Cylinder, "Cap bottom", Vector3.down * 0.085f, new Vector3(0.068f, 0.012f, 0.068f), cap);
                Part(root, PrimitiveType.Cylinder, "Tape", Vector3.zero, new Vector3(0.06f, 0.028f, 0.06f), tape);
                Part(root, PrimitiveType.Cube, "Timer", new Vector3(0f, 0.005f, 0.032f), new Vector3(0.026f, 0.036f, 0.012f),
                    Lit("Timer", new Color(0.06f, 0.06f, 0.06f), 0.2f, 0.6f));
                Part(root, PrimitiveType.Cylinder, "Wire red", new Vector3(0.008f, 0.05f, 0.03f), new Vector3(0.004f, 0.03f, 0.004f),
                    Lit("Wire red", new Color(0.6f, 0.05f, 0.04f), 0f, 0.5f));
                Part(root, PrimitiveType.Cylinder, "Wire blue", new Vector3(-0.008f, 0.05f, 0.03f), new Vector3(0.004f, 0.03f, 0.004f),
                    Lit("Wire blue", new Color(0.05f, 0.12f, 0.5f), 0f, 0.5f));
                var led = Part(root, PrimitiveType.Sphere, "LED", new Vector3(0f, 0.016f, 0.039f), Vector3.one * 0.009f,
                    new Material(Lit("LED", new Color(0.4f, 0.02f, 0.02f), 0f, 0.9f)));
                return root;
            }

            collider.radius = 0.042f;
            collider.height = BottleHeight;
            var mesh = BottleMesh();
            if (mesh != null)
            {
                var bottle = new GameObject("Bottle");
                bottle.layer = 2;
                bottle.transform.SetParent(root.transform, false);
                // Keep the file's own axis fix-up (Blender exports lie on their side without it).
                Vector3 upright = bottleTurn * mesh.bounds.size;
                float scale = BottleHeight / Mathf.Abs(upright.y);
                bottle.transform.localRotation = bottleTurn;
                bottle.transform.localScale = Vector3.one * scale;
                bottle.transform.localPosition = -(bottleTurn * mesh.bounds.center) * scale;
                bottle.AddComponent<MeshFilter>().sharedMesh = mesh;
                bottle.AddComponent<MeshRenderer>().sharedMaterial = Glass();
                // Fuel inside, seen through the glass.
                Part(root, PrimitiveType.Cylinder, "Fuel", Vector3.down * 0.06f, new Vector3(0.05f, 0.06f, 0.05f),
                    Lit("Fuel", new Color(0.3f, 0.19f, 0.04f), 0f, 0.95f));
            }
            else
            {
                Part(root, PrimitiveType.Capsule, "Bottle", Vector3.zero, new Vector3(0.08f, 0.15f, 0.08f), Glass());
            }
            var rag = Part(root, PrimitiveType.Cylinder, "Rag", Vector3.up * (BottleHeight * 0.5f + 0.01f), new Vector3(0.026f, 0.03f, 0.026f),
                Lit("Rag", new Color(0.58f, 0.5f, 0.38f), 0f, 0.1f));
            rag.transform.localRotation = Quaternion.Euler(14f, 30f, 8f);
            return root;
        }

        static GameObject Part(GameObject root, PrimitiveType shape, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            Destroy(part.GetComponent<Collider>());
            part.name = name;
            part.layer = 2;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        static Mesh BottleMesh()
        {
            if (bottleLoaded)
                return bottleMesh;
            bottleLoaded = true;
            var source = Resources.Load<GameObject>("ResidualSignal/Throwables/wine_bottles_01_1k");
            // The file holds a row of bottles; any one of them will do.
            var filter = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
            bottleMesh = filter != null ? filter.sharedMesh : null;
            if (filter != null)
            {
                bottleTurn = filter.transform.localRotation;
                Debug.Log($"THROWABLE BOTTLE {filter.name} bounds {bottleMesh.bounds.size} turn {bottleTurn.eulerAngles}");
            }
            return bottleMesh;
        }

        static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            if (materials.TryGetValue(name, out var cached))
                return cached;
            var material = new Material(Shader.Find("HDRP/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            materials[name] = material;
            return material;
        }

        static Material Glass()
        {
            if (materials.TryGetValue("Glass", out var cached))
                return cached;
            var material = Lit("Glass", new Color(0.16f, 0.3f, 0.12f, 0.72f), 0f, 0.97f);
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            HDMaterial.ValidateMaterial(material);
            return material;
        }

        void OnDestroy()
        {
            lures.Remove(this);
            if (wick != null)
                Destroy(wick.gameObject);
        }

        void Update()
        {
            if (burning)
            {
                Burn();
                return;
            }
            if (kind != Kind.PipeBomb)
            {
                glow.intensity = 130f + Mathf.PerlinNoise(Time.time * 9f, 0f) * 90f;
                return;
            }
            float age = Time.time - armedAt;
            if (age >= FuseSeconds)
            {
                Explode();
                return;
            }
            // Beeps quicken toward the blast, like the original.
            if (Time.time >= nextBeep)
            {
                float left = FuseSeconds - age;
                nextBeep = Time.time + Mathf.Lerp(0.12f, 0.6f, left / FuseSeconds);
                glow.intensity = 260f;
                voice.PlayOneShot(beep ??= ShotFeedback.Tick("Pipe bomb beep", 1750f, 0.08f, 0.6f), 0.8f);
                // Keep the crowd awake and walking to it.
                if (authoritative)
                    StationInfected.Noise(transform.position, LureRadius);
            }
            glow.intensity = Mathf.MoveTowards(glow.intensity, 0f, Time.deltaTime * 1400f);
            if (led != null)
                led.sharedMaterial.SetColor("_EmissiveColor", new Color(1f, 0.05f, 0.03f) * (glow.intensity / 260f * LedNits));
        }

        void OnCollisionEnter(Collision hit)
        {
            if (kind != Kind.Molotov || burning || Time.time - armedAt < 0.05f)
                return;
            Ignite(hit.GetContact(0).point);
        }

        void Explode()
        {
            Vector3 center = transform.position;
            if (authoritative)
            {
                foreach (var infected in StationInfected.Living.ToArray())
                {
                    if (infected == null || infected.Dead)
                        continue;
                    Vector3 to = infected.transform.position + Vector3.up - center;
                    if (to.magnitude > BlastRadius)
                        continue;
                    // Full damage up close; the Tank and the Witch only take a share.
                    float falloff = Mathf.Lerp(1f, 0.35f, to.magnitude / BlastRadius);
                    int damage = Mathf.RoundToInt(BlastDamage * falloff);
                    if (infected.Kind == InfectedKind.Brute || infected.IsWeeper)
                        damage = Mathf.RoundToInt(damage * 0.25f);
                    infected.Hit(damage, infected.transform.position + Vector3.up, to.normalized + Vector3.up * 0.6f);
                }
                StationInfected.Noise(center, 30f);
            }
            Flash(center + Vector3.up * 0.5f, new Color(1f, 0.72f, 0.4f), 12000f, 20f, 0.5f);
            CarAlarm.CheckBlast(center, BlastRadius);
            OneShot(ShotFeedback.Burst("Blast fireball", new Color(1f, 0.55f, 0.18f), new Color(1f, 0.85f, 0.5f),
                0.45f, 0.5f, 1.4f, 2f, 7f, -0.2f, false, additive: true), center, 70);
            OneShot(ShotFeedback.Burst("Blast debris", new Color(1f, 0.7f, 0.3f), new Color(1f, 0.9f, 0.6f),
                0.6f, 0.02f, 0.05f, 7f, 15f, 1.2f, true), center, 45);
            OneShot(Smoke("Blast smoke", 2.8f, 0.9f, 2.2f, 0.6f, 2.4f), center + Vector3.up * 0.4f, 28);
            ShotFeedback.Scorch(center, 1.6f);
            PlayBoom(center, 0.42f, 1f);
            StationTrial.Local?.FeelExplosion(center, BlastRadius * 2f);
            Destroy(gameObject);
        }

        /// <summary>Dark smoke that swells as it climbs and thins away at the top.</summary>
        static ParticleSystem Smoke(string name, float life, float minSize, float maxSize, float minSpeed, float maxSpeed)
        {
            var swell = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.65f, 1.4f), new Keyframe(1f, 0f));
            return ShotFeedback.Burst(name, Color.black, Color.black, life, minSize, maxSize, minSpeed, maxSpeed, -0.06f, false,
                tint: new Color(0.07f, 0.065f, 0.06f, 0.5f), sizeCurve: swell);
        }

        static void OneShot(ParticleSystem system, Vector3 at, int count)
        {
            system.transform.position = at;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            system.Emit(count);
            Destroy(system.gameObject, 4f);
        }

        void Ignite(Vector3 point)
        {
            burning = true;
            burnUntil = Time.time + FireSeconds;
            // Find the floor under the shatter so the fire lies flat.
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                point = floor.point;
            if (wick != null)
                Destroy(wick.gameObject);
            Destroy(body);
            Destroy(GetComponent<Collider>());
            foreach (var part in GetComponentsInChildren<Renderer>())
                if (part.GetComponent<ParticleSystem>() == null)
                    part.enabled = false;
            transform.SetPositionAndRotation(point, Quaternion.identity);
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 1.3f;
            glow.range = FireRadius * 3f;
            glow.shadows = LightShadows.Soft;
            // Tongues of flame, a soft glowing body, and smoke rolling off the top.
            fire.Add(Pool(ShotFeedback.Burst("Molotov tongues", new Color(1f, 0.42f, 0.08f), new Color(1f, 0.75f, 0.3f),
                0.6f, 0.08f, 0.2f, 1.2f, 3f, -0.4f, true), FlameRate, 0.14f));
            fire.Add(Pool(ShotFeedback.Burst("Molotov body", new Color(0.9f, 0.3f, 0.05f, 0.6f), new Color(1f, 0.55f, 0.15f, 0.5f),
                0.7f, 0.45f, 0.9f, 0.2f, 0.7f, -0.2f, false, additive: true), 45f, 0f));
            fire.Add(Pool(Smoke("Molotov smoke", 3.2f, 0.7f, 1.6f, 0.6f, 1.3f), 12f, 0f, 1.4f));
            OneShot(ShotFeedback.Burst("Glass shards", new Color(0.5f, 0.7f, 0.5f), new Color(0.8f, 0.9f, 0.8f),
                0.4f, 0.01f, 0.025f, 2f, 5f, 1.5f, true), point + Vector3.up * 0.1f, 25);
            ShotFeedback.Scorch(point, FireRadius);
            PlayBoom(point, 1.6f, 0.7f);
            if (authoritative)
                StationInfected.Noise(point, 18f);
        }

        ParticleSystem Pool(ParticleSystem system, float rate, float stretch, float height = 0f)
        {
            system.transform.SetParent(transform, false);
            system.transform.SetLocalPositionAndRotation(Vector3.up * height, Quaternion.LookRotation(Vector3.up));
            // A thin box facing up: flames rise from the whole pool, not one point.
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(FireRadius * 1.5f, FireRadius * 1.5f, 0.05f);
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            if (stretch > 0f)
                system.GetComponent<ParticleSystemRenderer>().velocityScale = stretch;
            return system;
        }

        void Burn()
        {
            float left = burnUntil - Time.time;
            glow.intensity = (260f + Mathf.PerlinNoise(Time.time * 7f, 3f) * 220f) * Mathf.Clamp01(left);
            if (left <= -3f)
            {
                Destroy(gameObject);
                return;
            }
            if (left < 1.2f)
                foreach (var system in fire)
                {
                    var emission = system.emission;
                    emission.enabled = left > 0f;
                }
            if (left <= 0f || Time.time < nextBurn)
                return;
            nextBurn = Time.time + FireTick;
            Vector3 center = transform.position;
            if (authoritative)
                foreach (var infected in StationInfected.Living.ToArray())
                {
                    if (infected == null || infected.Dead)
                        continue;
                    Vector3 offset = infected.transform.position - center;
                    if (Mathf.Abs(offset.y) < 2f && Vector3.ProjectOnPlane(offset, Vector3.up).magnitude < FireRadius)
                        infected.Hit(FireDamagePerTick, infected.transform.position + Vector3.up * 0.4f, Vector3.up);
                }
            // Survivors who stand in it get burned too, as friendly fire. Each peer burns its own player.
            var player = StationTrial.Local;
            if (player != null && Difficulty.FriendlyFire > 0f)
            {
                Vector3 offset = player.transform.position - center;
                if (Mathf.Abs(offset.y) < 2f && Vector3.ProjectOnPlane(offset, Vector3.up).magnitude < FireRadius)
                    player.TakeHit(Mathf.Max(1, Mathf.RoundToInt(10 * Difficulty.FriendlyFire)));
            }
        }

        void PlayBoom(Vector3 point, float pitch, float volume)
        {
            if (boom == null)
                return;
            var source = new GameObject("Throwable boom").AddComponent<AudioSource>();
            source.transform.position = point;
            source.spatialBlend = 0.6f;
            source.pitch = pitch;
            source.maxDistance = 60f;
            source.PlayOneShot(boom, volume);
            Destroy(source.gameObject, 3f);
        }

        static void Flash(Vector3 point, Color color, float intensity, float range, float seconds)
        {
            var lamp = new GameObject("Blast flash").AddComponent<Light>();
            lamp.transform.position = point;
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.range = range;
            lamp.intensity = intensity;
            lamp.gameObject.AddComponent<FadeLight>().seconds = seconds;
        }
    }

    /// <summary>Blast flash that dims to nothing and removes itself.</summary>
    sealed class FadeLight : MonoBehaviour
    {
        public float seconds;
        Light lamp;
        float start;
        float peak;

        void Start()
        {
            lamp = GetComponent<Light>();
            start = Time.time;
            peak = lamp.intensity;
        }

        void Update()
        {
            float t = (Time.time - start) / seconds;
            lamp.intensity = peak * Mathf.Clamp01(1f - t);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
