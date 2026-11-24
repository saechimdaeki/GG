using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>
    /// Local-only shot feedback that has no gameplay effect: wall impacts (sparks, dust, bullet holes)
    /// and the short UI ticks for hits, headshots and kills. Everything is built at runtime and pooled.
    /// </summary>
    public static class ShotFeedback
    {
        const int HoleCount = 96;
        static ParticleSystem sparks;
        static ParticleSystem dust;
        static Transform[] holes;
        static int nextHole;
        static Material holeMaterial;
        static Transform holeRoot;
        static Mesh muzzleFlareMesh;
        static Material muzzleFlareMaterial;

        public static void ShapeMuzzleFlash(GameObject muzzle)
        {
            var original = muzzle.GetComponentInChildren<MeshRenderer>(includeInactive: true);
            if (original == null)
                return;
            original.enabled = false;
            if (muzzleFlareMesh == null)
            {
                muzzleFlareMesh = new Mesh { name = "Muzzle flash planes" };
                muzzleFlareMesh.vertices = new[]
                {
                    new Vector3(-0.068f, 0f, 0f), new Vector3(0.068f, 0f, 0f),
                    new Vector3(0.068f, 0f, 0.20f), new Vector3(-0.068f, 0f, 0.20f),
                    new Vector3(0f, -0.068f, 0f), new Vector3(0f, 0.068f, 0f),
                    new Vector3(0f, 0.068f, 0.20f), new Vector3(0f, -0.068f, 0.20f)
                };
                muzzleFlareMesh.uv = new[]
                {
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up
                };
                muzzleFlareMesh.triangles = new[]
                {
                    0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0,
                    4, 5, 6, 4, 6, 7, 6, 5, 4, 7, 6, 4
                };
                muzzleFlareMesh.RecalculateBounds();
            }
            if (muzzleFlareMaterial == null)
            {
                var mask = Radial(64, (radius, angle) =>
                {
                    float edge = 0.62f + 0.22f * Mathf.Cos(angle * 5f) + 0.1f * Mathf.Sin(angle * 9f);
                    float light = Mathf.Clamp01(1f - radius / edge);
                    return Mathf.Pow(light, 1.7f);
                });
                muzzleFlareMaterial = Unlit(mask, new Color(5.8f, 2.9f, 0.85f), transparent: true);
            }
            var flare = new GameObject("Textured muzzle flare");
            flare.transform.SetParent(muzzle.transform, false);
            flare.AddComponent<MeshFilter>().sharedMesh = muzzleFlareMesh;
            var renderer = flare.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = muzzleFlareMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static LineRenderer tracer;
        static Material tracerMaterial;
        static float tracerUntil;

        public static void Trace(Vector3 muzzle, Vector3 end)
        {
            Vector3 travel = end - muzzle;
            if (travel.sqrMagnitude < 0.25f)
                return;
            if (tracer == null)
            {
                tracer = new GameObject("Shot trace").AddComponent<LineRenderer>();
                tracer.positionCount = 2;
                tracer.startWidth = 0.019f;
                tracer.endWidth = 0.008f;
                tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (tracerMaterial == null)
                    tracerMaterial = Unlit(Texture2D.whiteTexture, new Color(2.8f, 1.65f, 0.65f));
                tracer.sharedMaterial = tracerMaterial;
            }
            Vector3 endOfStreak = muzzle + travel.normalized * Mathf.Min(16f, travel.magnitude);
            tracer.SetPosition(0, endOfStreak - travel.normalized * Mathf.Min(1.3f, travel.magnitude));
            tracer.SetPosition(1, endOfStreak);
            tracer.enabled = true;
            tracerUntil = Time.time + 0.038f;
        }

        public static void UpdateTrace()
        {
            if (tracer != null && tracer.enabled && Time.time >= tracerUntil)
                tracer.enabled = false;
        }
        static ParticleSystem shells;
        static ParticleSystem bloodMist;

        /// <summary>Kick a brass casing out to the right. Lives in the viewmodel lens so it lines up with the gun on screen.</summary>
        public static void EjectShell(Transform lens, Vector3 port)
        {
            if (shells == null || shells.transform.parent != lens)
            {
                if (shells != null)
                    Object.Destroy(shells.gameObject);
                shells = CreateShells(lens);
            }
            var shot = new ParticleSystem.EmitParams
            {
                position = lens.InverseTransformPoint(port),
                velocity = new Vector3(Random.Range(1.1f, 1.6f), Random.Range(0.9f, 1.4f), Random.Range(-0.4f, 0.1f)),
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), 90f),
                applyShapeToPosition = false
            };
            shells.Emit(shot, 1);
        }

        static ParticleSystem CreateShells(Transform lens)
        {
            var node = new GameObject("Shell casings");
            node.transform.SetParent(lens, false);
            node.layer = lens.parent.gameObject.layer;
            var system = node.AddComponent<ParticleSystem>();
            system.Stop();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSize3D = true;
            main.startSizeX = main.startSizeZ = 0.009f;
            main.startSizeY = 0.013f;
            main.startRotation3D = true;
            main.gravityModifier = 0.9f;
            main.maxParticles = 24;
            // ponytail: local space, so casings follow a fast turn; fine for a half-second arc.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission;
            emission.enabled = false;
            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-25f, 25f);
            spin.z = new ParticleSystem.MinMaxCurve(-25f, 25f);
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            renderer.mesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(cylinder);
            var brass = new Material(Shader.Find("HDRP/Lit"));
            brass.SetColor("_BaseColor", new Color(0.78f, 0.56f, 0.24f));
            brass.SetFloat("_Metallic", 1f);
            brass.SetFloat("_Smoothness", 0.72f);
            HDMaterial.ValidateMaterial(brass);
            renderer.sharedMaterial = brass;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        /// <summary>A hanging red mist where a round went into flesh; bigger for heavier hits.</summary>
        public static void BloodMist(Vector3 point, Vector3 direction, int damage)
        {
            if (bloodMist == null)
                bloodMist = Burst("Blood mist", new Color(0.32f, 0.02f, 0.015f, 0.75f), new Color(0.18f, 0.01f, 0.01f, 0.55f),
                    0.7f, 0.12f, 0.3f, 0.3f, 1.4f, 0.05f, false);
            bloodMist.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-direction.normalized));
            bloodMist.Emit(Mathf.Clamp(damage / 12, 3, 12));
            // Behind the body along the shot, and on the floor below: L4D-style walls that remember the fight.
            Vector3 travel = direction.normalized;
            PlaceSplat(point, travel + Random.insideUnitSphere * 0.15f, 2.6f, damage >= 60 ? 1.6f : 1f);
            PlaceSplat(point, Vector3.down + travel * 0.4f, 2.2f, 1.4f);
        }

        const int SplatCount = 48;
        static Transform[] splats;
        static int nextSplat;
        static readonly RaycastHit[] splatHits = new RaycastHit[8];

        static void PlaceSplat(Vector3 from, Vector3 direction, float reach, float size)
        {
            int count = Physics.RaycastNonAlloc(from, direction.normalized, splatHits, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit? surface = null;
            for (int index = 0; index < count; index++)
            {
                var hit = splatHits[index];
                if (hit.collider is CharacterController || hit.collider.attachedRigidbody != null ||
                    hit.collider.GetComponentInParent<StationInfected>() != null)
                    continue;
                if (surface == null || hit.distance < surface.Value.distance)
                    surface = hit;
            }
            if (surface == null)
                return;
            if (splats == null)
            {
                splats = new Transform[SplatCount];
                var material = Unlit(SplatTexture(), new Color(0.34f, 0.02f, 0.014f), transparent: true);
                var root = new GameObject("Blood splats").transform;
                for (int index = 0; index < SplatCount; index++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(quad.GetComponent<Collider>());
                    var renderer = quad.GetComponent<Renderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    quad.layer = 2;
                    quad.transform.SetParent(root, false);
                    quad.SetActive(false);
                    splats[index] = quad.transform;
                }
            }
            var splat = splats[nextSplat];
            nextSplat = (nextSplat + 1) % SplatCount;
            splat.gameObject.SetActive(true);
            // Stagger the offset so overlapping splats don't z-fight.
            splat.SetPositionAndRotation(surface.Value.point + surface.Value.normal * (0.005f + nextSplat * 0.0001f),
                Quaternion.LookRotation(-surface.Value.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            splat.localScale = Vector3.one * Random.Range(0.35f, 0.6f) * size;
        }

        /// <summary>Blotch with a ragged rim and flung droplets around it.</summary>
        static Texture2D SplatTexture()
        {
            var drops = new Vector3[14];
            for (int index = 0; index < drops.Length; index++)
            {
                float angle = index * 2.4f, distance = 0.45f + (index * 0.37f % 0.45f);
                drops[index] = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0.03f + (index % 4) * 0.018f);
            }
            return Radial(128, (r, angle) =>
            {
                float rim = 0.36f + 0.08f * Mathf.Sin(angle * 5f) + 0.05f * Mathf.Sin(angle * 11f + 2f) + 0.03f * Mathf.Sin(angle * 23f);
                if (r < rim)
                    return 0.85f;
                float u = r * Mathf.Cos(angle), v = r * Mathf.Sin(angle);
                foreach (var drop in drops)
                    if ((u - drop.x) * (u - drop.x) + (v - drop.y) * (v - drop.y) < drop.z * drop.z)
                        return 0.8f;
                return 0f;
            });
        }
        static AudioClip hitTick, headTick, killTick, ricochet;

        public static AudioClip HitTick => hitTick ??= Tick("Hit tick", 2400f, 0.035f, 0.5f);
        public static AudioClip HeadTick => headTick ??= Tick("Head tick", 3300f, 0.06f, 0.7f);
        public static AudioClip KillTick => killTick ??= Tick("Kill tick", 900f, 0.11f, 0.85f);

        /// <summary>A round struck level geometry: sparks, a dust puff and a hole left on the surface.</summary>
        public static void Impact(Vector3 point, Vector3 normal)
        {
            if (sparks == null)
                sparks = Burst("Impact sparks", new Color(1f, 0.72f, 0.35f), new Color(1f, 0.9f, 0.6f), 0.15f, 0.012f, 0.028f, 3f, 9f, 2.2f, true);
            if (dust == null)
                dust = Burst("Impact dust", new Color(0.42f, 0.4f, 0.37f, 0.55f), new Color(0.3f, 0.29f, 0.27f, 0.4f), 0.55f, 0.08f, 0.2f, 0.4f, 1.6f, -0.05f, false);
            var facing = Quaternion.LookRotation(normal);
            sparks.transform.SetPositionAndRotation(point + normal * 0.02f, facing);
            sparks.Emit(Random.Range(6, 11));
            dust.transform.SetPositionAndRotation(point + normal * 0.03f, facing);
            dust.Emit(Random.Range(3, 6));
            if (Random.value < 0.25f)
                AudioSource.PlayClipAtPoint(ricochet ??= Tick("Ricochet", 5200f, 0.09f, 0.35f, sweep: -0.55f), point, 0.4f);
            PlaceHole(point, normal);
        }

        static void PlaceHole(Vector3 point, Vector3 normal)
        {
            if (holeRoot == null)
            {
                holes = new Transform[HoleCount];
                nextHole = 0;
                if (holeMaterial == null)
                    holeMaterial = Unlit(HoleTexture(), new Color(0.05f, 0.045f, 0.04f));
                holeRoot = new GameObject("Bullet holes").transform;
                for (int index = 0; index < HoleCount; index++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(quad.GetComponent<Collider>());
                    quad.GetComponent<Renderer>().sharedMaterial = holeMaterial;
                    quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    quad.layer = 2;
                    quad.transform.SetParent(holeRoot, false);
                    quad.SetActive(false);
                    holes[index] = quad.transform;
                }
            }
            var hole = holes[nextHole];
            nextHole = (nextHole + 1) % HoleCount;
            hole.gameObject.SetActive(true);
            // Quad faces -Z, so look along -normal; random spin so holes don't repeat.
            hole.SetPositionAndRotation(point + normal * 0.004f,
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            hole.localScale = Vector3.one * Random.Range(0.07f, 0.11f);
        }

        static ParticleSystem Burst(string name, Color a, Color b, float life, float minSize, float maxSize,
            float minSpeed, float maxSpeed, float gravity, bool streak)
        {
            var node = new GameObject(name);
            var system = node.AddComponent<ParticleSystem>();
            system.Stop();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = streak ? 38f : 25f;
            shape.radius = 0.01f;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, streak ? AnimationCurve.Linear(0, 1, 1, 0.2f) : AnimationCurve.EaseInOut(0, 0.6f, 1, 1.8f));
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            if (streak)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.035f;
                renderer.lengthScale = 1f;
            }
            renderer.sharedMaterial = streak ? Additive(a) : Unlit(SoftDot(), Color.white, transparent: true);
            system.Play();
            return system;
        }

        static Material Unlit(Texture2D texture, Color color, bool transparent = false)
        {
            var material = new Material(Shader.Find("HDRP/Unlit"));
            material.SetColor("_UnlitColor", color);
            material.SetTexture("_UnlitColorMap", texture);
            if (transparent)
            {
                material.SetFloat("_SurfaceType", 1f);
                material.SetFloat("_BlendMode", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_AlphaCutoffEnable", 1f);
                material.SetFloat("_AlphaCutoff", 0.35f);
                material.EnableKeyword("_ALPHATEST_ON");
            }
            HDMaterial.ValidateMaterial(material);
            return material;
        }

        static Material Additive(Color color)
        {
            var material = Unlit(SoftDot(), color * 6f, transparent: true);
            material.SetFloat("_BlendMode", 1f);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            HDMaterial.ValidateMaterial(material);
            return material;
        }

        static Texture2D SoftDot() => Radial(32, (r, _) => Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r));

        /// <summary>Dark core with a ragged scorched rim.</summary>
        static Texture2D HoleTexture() => Radial(64, (r, angle) =>
        {
            float rim = 0.55f + 0.18f * Mathf.Sin(angle * 7f) * Mathf.Sin(angle * 3f + 1f);
            return r < 0.28f ? 1f : r < rim ? 0.75f * (1f - (r - 0.28f) / (rim - 0.28f)) + 0.3f : 0f;
        });

        static Texture2D Radial(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float shade = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 0.6f);
                byte value = (byte)(255 * shade);
                pixels[y * size + x] = new Color32(value, value, value, (byte)(255 * alpha(Mathf.Sqrt(u * u + v * v), Mathf.Atan2(v, u))));
            }
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        /// <summary>Short decaying tone; sweep bends the pitch over its length (ricochet whine).</summary>
        static AudioClip Tick(string name, float frequency, float seconds, float gain, float sweep = 0f)
        {
            const int rate = 44100;
            int length = Mathf.CeilToInt(rate * seconds);
            var samples = new float[length];
            float phase = 0;
            for (int index = 0; index < length; index++)
            {
                float t = index / (float)length;
                phase += 2f * Mathf.PI * frequency * (1f + sweep * t) / rate;
                float envelope = Mathf.Exp(-t * 6f) * Mathf.Min(1f, index / 40f);
                samples[index] = gain * envelope * (0.8f * Mathf.Sin(phase) + 0.2f * Mathf.Sin(phase * 2.01f));
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
