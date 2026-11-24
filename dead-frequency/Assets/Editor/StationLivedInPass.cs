using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>
    /// The lived-in, fought-through layer on top of the architecture pass: wall services (breaker boxes, vents,
    /// fire alarms, conduit drops), caged lamps along every wall for pools of light, blood (smears, handprints,
    /// pools, drag trails), paper litter, standing water and dust motes under the lights. Called from
    /// InstallArchitectureDetail so one run rebuilds everything under the same root.
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const float FixtureSpacing = 3.2f;
        const float LampSpacing = 7f;

        static int LivedIn(Transform root, List<Section> sections, List<Bounds> fittings, System.Random random)
        {
            // Wall-mounted things need their own clearance list: solid walls are checked by raycast (FlatWall), and thin
            // panels or bands lying flat on a wall are fine to mount over. Everything this pass placed counts.
            fittings = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.GetComponent<Collider>() == null && !(r is ParticleSystemRenderer))
                .Select(r => r.bounds)
                .Where(b => b.size.magnitude < 12f && !(Mathf.Min(b.size.x, b.size.y, b.size.z) < 0.08f && Mathf.Max(b.size.x, b.size.y, b.size.z) > 1.2f))
                .ToList();
            int count = SwapBoxProps(root, random);
            count += WallStructure(root, sections, fittings);
            count += WallServices(root, sections, fittings, random);
            count += FloorStories(root, sections, random);
            count += DustUnderLights(root);
            return count;
        }

        /// <summary>One flat wall span of halfWidth either side, with nothing in front of it.</summary>
        static bool FlatWall(Vector3 point, Vector3 normal, Collider wall, float halfWidth, float halfHeight = 0f)
        {
            Vector3 along = Vector3.Cross(normal, Vector3.up).normalized;
            int steps = Mathf.Max(1, Mathf.CeilToInt(halfWidth / 0.25f));
            for (int i = -steps; i <= steps; i++)
            foreach (float up in halfHeight > 0f ? new[] { -halfHeight, halfHeight } : new[] { 0f })
            {
                Vector3 probe = point + along * (i * halfWidth / steps) + Vector3.up * up;
                if (!Physics.Raycast(probe + normal, -normal, out var hit, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    hit.collider != wall || Mathf.Abs(hit.distance - 1f) > 0.05f)
                    return false;
            }
            return true;
        }

        static bool WallAt(Section section, int side, float height, out RaycastHit wall)
        {
            Vector3 origin = new Vector3(section.x, section.floor + height, section.z);
            if (!Physics.Raycast(origin, Vector3.right * side, out wall, 9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                Mathf.Abs(wall.normal.y) > 0.2f)
                return false;
            var surface = wall.collider.GetComponent<Renderer>();
            string name = wall.collider.name.ToLowerInvariant();
            return surface != null && surface.enabled && !name.Contains("window") && !name.Contains("glass") && !name.Contains("door") &&
                   !name.Contains("shutter") && !name.Contains("gate");
        }

        static int WallServices(Transform root, List<Section> sections, List<Bounds> fittings, System.Random random)
        {
            var steel = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Steel - powder coat.mat");
            var red = PipeMaterial("Pipe - sprinkler red paint");
            var indicator = Surface("Power indicator", new Color(1f, 0.48f, 0.14f), 0.34f, emission: 22f);
            var grille = Surface("Vent grille - dark", new Color(0.05f, 0.05f, 0.05f), 0.2f);
            int placed = 0;
            foreach (int side in new[] { -1, 1 })
            {
                float lastFixture = float.MinValue, lastLamp = float.MinValue, lastBlood = float.MinValue;
                foreach (var section in sections.Where(s => s.valid))
                {
                    // Lamps first: the light pools along the route matter more than the props.
                    if (section.z - lastLamp >= LampSpacing && WallAt(section, side, 2.55f, out var lampWall) &&
                        // No second lamp beside one already there (story lamps, earlier lamps on this wall).
                        !Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Any(l => l.transform.IsChildOf(root) &&
                            Vector3.Distance(l.transform.position, lampWall.point) < 2.5f) &&
                        FlatWall(lampWall.point, lampWall.normal, lampWall.collider, 0.25f) &&
                        !fittings.Any(f => f.Intersects(new Bounds(lampWall.point, new Vector3(0.8f, 0.6f, 0.8f)))))
                    {
                        WallLamp(root, lampWall.point, lampWall.normal, random.NextDouble() < 0.22);
                        fittings.Add(new Bounds(lampWall.point, Vector3.one * 0.6f));
                        lastLamp = section.z;
                        placed++;
                    }
                    if (section.z - lastFixture >= FixtureSpacing && random.NextDouble() < 0.75)
                    {
                        int kind = random.Next(4);
                        float height = kind switch { 0 => 1.45f, 1 => 2.35f, 2 => 1.5f, _ => 1.2f };
                        if (WallAt(section, side, height, out var wall) && FlatWall(wall.point, wall.normal, wall.collider, 0.35f, 0.3f) &&
                            !fittings.Any(f => f.Intersects(new Bounds(wall.point, new Vector3(0.9f, 0.9f, 0.9f)))))
                        {
                            var fixture = new GameObject(kind switch { 0 => "Breaker box", 1 => "Wall vent", 2 => "Fire alarm", _ => "Conduit drop" }).transform;
                            fixture.SetParent(root, false);
                            fixture.SetPositionAndRotation(wall.point + wall.normal * 0.005f, Quaternion.LookRotation(wall.normal));
                            switch (kind)
                            {
                                case 0:
                                    Shape(fixture, PrimitiveType.Cube, "Box", new Vector3(0f, 0f, 0.07f), new Vector3(0.42f, 0.56f, 0.14f), steel);
                                    Shape(fixture, PrimitiveType.Cube, "Door seam", new Vector3(0f, 0f, 0.142f), new Vector3(0.38f, 0.52f, 0.004f), grille);
                                    Shape(fixture, PrimitiveType.Cube, "Lamp", new Vector3(0.14f, 0.2f, 0.145f), new Vector3(0.03f, 0.03f, 0.01f), indicator);
                                    PipeBetween(fixture, "Feed", new Vector3(0f, 0.28f, 0.05f), new Vector3(0f, section.ceiling - wall.point.y, 0.05f), 0.05f, steel);
                                    break;
                                case 1:
                                    Shape(fixture, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(0.7f, 0.42f, 0.04f), steel);
                                    for (int i = 0; i < 6; i++)
                                        Shape(fixture, PrimitiveType.Cube, "Slat", new Vector3(0f, -0.16f + i * 0.064f, 0.045f), new Vector3(0.64f, 0.025f, 0.02f), grille)
                                            .localRotation = Quaternion.Euler(-30f, 0f, 0f);
                                    break;
                                case 2:
                                    Shape(fixture, PrimitiveType.Cube, "Alarm box", new Vector3(0f, 0f, 0.04f), new Vector3(0.18f, 0.24f, 0.08f), red);
                                    Shape(fixture, PrimitiveType.Cylinder, "Bell", new Vector3(0f, 0.3f, 0.05f), new Vector3(0.2f, 0.03f, 0.2f), red)
                                        .localRotation = Quaternion.Euler(90f, 0f, 0f);
                                    break;
                                default:
                                    Shape(fixture, PrimitiveType.Cube, "Junction box", new Vector3(0f, 0f, 0.06f), new Vector3(0.22f, 0.22f, 0.12f), steel);
                                    PipeBetween(fixture, "Conduit", new Vector3(0f, 0.11f, 0.06f), new Vector3(0f, section.ceiling - wall.point.y, 0.06f), 0.04f, steel);
                                    PipeBetween(fixture, "Conduit", new Vector3(0f, -0.11f, 0.06f), new Vector3(0f, section.floor - wall.point.y, 0.06f), 0.04f, steel);
                                    break;
                            }
                            fittings.Add(new Bounds(wall.point, new Vector3(0.8f, 0.8f, 0.8f)));
                            lastFixture = section.z;
                            placed++;
                        }
                    }
                    // Blood on the walls: a fight happened here.
                    if (section.z - lastBlood >= 9f && random.NextDouble() < 0.18 && WallAt(section, side, 1.3f, out var bloodWall) &&
                        FlatWall(bloodWall.point, bloodWall.normal, bloodWall.collider, 0.6f))
                    {
                        bool smear = random.NextDouble() < 0.55;
                        string design = smear ? "Blood smear" : "Blood handprints";
                        Decal(root, design, StoryDecal(design), bloodWall.point + bloodWall.normal * 0.15f + Vector3.up * (smear ? 0.25f : 0f),
                            Quaternion.LookRotation(-bloodWall.normal), smear ? new Vector3(1.1f, 1.65f, 0.4f) : new Vector3(1.1f, 1.1f, 0.4f));
                        if (smear)
                            Decal(root, "Blood pool", StoryDecal("Blood pool"), new Vector3(bloodWall.point.x, section.floor + 0.3f, bloodWall.point.z) +
                                bloodWall.normal * 0.55f, Quaternion.Euler(90f, (float)random.NextDouble() * 360f, 0f), new Vector3(1.4f, 1.4f, 0.8f));
                        lastBlood = section.z;
                        placed++;
                    }
                }
            }
            return placed;
        }

        static int FloorStories(Transform root, List<Section> sections, System.Random random)
        {
            int placed = 0;
            float last = float.MinValue;
            foreach (var section in sections.Where(s => s.valid))
            {
                if (section.z - last < 2.5f)
                    continue;
                float x = Mathf.Lerp(section.left + 1f, section.right - 1f, (float)random.NextDouble());
                if (!NavMesh.SamplePosition(new Vector3(x, section.floor, section.z), out var ground, 0.5f, NavMesh.AllAreas))
                    continue;
                double roll = random.NextDouble();
                string design;
                Vector3 size;
                float yaw = (float)random.NextDouble() * 360f;
                if (roll < 0.3) { design = "Paper litter"; size = new Vector3(2.4f, 2.4f, 0.8f); }
                else if (roll < 0.48) { design = "Wet patch"; size = new Vector3(3.2f, 3.2f, 0.8f); }
                else if (roll < 0.6) { design = "Blood pool"; size = new Vector3(1.3f, 1.3f, 0.8f); }
                else if (roll < 0.7) { design = "Blood drag trail"; size = new Vector3(0.8f, 3.4f, 0.8f); yaw = (float)random.NextDouble() * 40f - 20f; }
                else continue;
                Decal(root, design, StoryDecal(design), ground.position + Vector3.up * 0.3f, Quaternion.Euler(90f, yaw, 0f), size);
                last = section.z;
                placed++;
            }
            return placed;
        }

        /// <summary>Slow dust drifting through the cone under every enclosed ceiling light.</summary>
        static int DustUnderLights(Transform root)
        {
            var material = DustMaterial();
            int placed = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                         .Where(l => l.type == LightType.Point && l.transform.position.y > 2.4f && l.transform.position.z > -2f)
                         .OrderBy(l => l.transform.position.z))
            {
                if (!Physics.Raycast(light.transform.position, Vector3.up, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                var node = new GameObject("Dust motes");
                node.transform.SetParent(root, false);
                node.transform.position = light.transform.position + Vector3.down * 1.2f;
                var system = node.AddComponent<ParticleSystem>();
                var main = system.main;
                main.loop = true;
                main.prewarm = true;
                main.startLifetime = 12f;
                main.startSpeed = 0.02f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
                main.startColor = new Color(1f, 0.9f, 0.75f, 0.5f);
                main.maxParticles = 60;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = system.emission;
                emission.rateOverTime = 4f;
                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(2.6f, 2f, 2.6f);
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = 0.05f;
                noise.frequency = 0.25f;
                var fade = system.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                node.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                placed++;
            }
            return placed;
        }

        static Material DustMaterial()
        {
            const string texturePath = "Assets/Art/Decals/Dust mote.png";
            if (!System.IO.File.Exists(texturePath))
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            string path = $"{SettingsFolder}/Dust motes.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_UnlitColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_UnlitColor", new Color(0.55f, 0.5f, 0.42f, 1f));
            material.SetFloat("_SurfaceType", 1f);  // transparent
            material.SetFloat("_BlendMode", 1f);    // additive
            material.SetFloat("_ZWrite", 0f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
