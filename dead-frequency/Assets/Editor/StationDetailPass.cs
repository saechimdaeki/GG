using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>
    /// Built-up ceiling and walls for every enclosed stretch of the route: steel cross beams, pipe runs on hangers
    /// (rusted main, yellow gas line, red sprinkler line), a cable tray, hanging wayfinding signs and
    /// survivor graffiti / paper posters as decals. Measures ceiling and walls by raycast from the walkable
    /// centre line, so it follows whatever the builders produced. Deterministic; re-running replaces the pass.
    /// Batch: -executeMethod ResidualSignal.StationSceneBuilder.InstallArchitectureDetail
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const string DetailName = "Architecture detail pass";
        const float DetailStep = 0.5f;

        struct Section
        {
            public bool valid;
            public float x, z, floor, ceiling, left, right;
        }

        // side, distance from wall, drop below ceiling, diameter, material
        static readonly (int side, float inset, float drop, float diameter, string material)[] PipeLines =
        {
            (-1, 0.72f, 0.56f, 0.22f, "Forecourt - oxidized dark steel"),
            (-1, 1.02f, 0.52f, 0.09f, "Pipe - gas yellow paint"),
            (1, 1.12f, 0.54f, 0.11f, "Pipe - sprinkler red paint"),
        };

        static readonly string[] HangingSigns = { "Hanging platform", "Hanging exit", "Hanging exit 2" };
        static readonly string[] Graffiti = { "Graffiti survivors", "Graffiti bitten", "Graffiti quiet", "Graffiti no rescue", "Graffiti alive" };
        static readonly string[] Posters = { "Poster missing", "Poster evacuation" };

        [MenuItem("잔류신호/천장 배관·보·표지판 배치")]
        public static void InstallArchitectureDetail()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var old = GameObject.Find(DetailName);
            if (old != null)
                Object.DestroyImmediate(old);
            Physics.SyncTransforms();

            var root = new GameObject(DetailName).transform;
            var sections = SurveySections();
            // Existing ceiling fittings (lights, signs, fixtures) that new steel must not cut through.
            var fittings = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => !r.transform.IsChildOf(root) && r.bounds.size.magnitude < 12f).Select(r => r.bounds).ToList();
            var steel = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Steel - powder coat.mat");
            var random = new System.Random(20261125);

            int beams = 0, pipes = 0, signs = 0;
            float lastBeam = float.MinValue;
            foreach (var section in sections.Where(s => s.valid))
            {
                if (section.z - lastBeam < 4f)
                    continue;
                var size = new Vector3(section.right - section.left, 0.36f, 0.22f);
                var centre = new Vector3((section.left + section.right) * 0.5f, section.ceiling - 0.18f, section.z);
                if (!Clear(centre, size * 0.5f - Vector3.one * 0.03f, fittings, 0.25f))
                    continue;
                Shape(root, PrimitiveType.Cube, "Ceiling cross beam", centre, size, steel);
                // Bottom flange reads as an I-beam from below.
                Shape(root, PrimitiveType.Cube, "Beam flange", centre + Vector3.down * 0.17f, new Vector3(size.x, 0.03f, 0.32f), steel);
                lastBeam = section.z;
                if (beams % 3 == 1 && section.ceiling - section.floor > 3.3f)
                    HangSign(root, HangingSigns[signs++ % HangingSigns.Length], new Vector3((section.left + section.right) * 0.5f, section.ceiling - 0.36f, section.z), section.floor, steel);
                beams++;
            }

            foreach (var line in PipeLines)
                pipes += PipeRuns(root, sections, line, fittings);
            pipes += CableTray(root, sections, fittings, steel);
            int decals = WallStories(root, sections, random);
            // Two annex ceiling lights are failing too: the deeper rooms feel worse kept.
            foreach (float z in new[] { 45f, 58f })
            {
                var failing = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                    .Where(l => l.type == LightType.Point && !l.transform.IsChildOf(root))
                    .OrderBy(l => Mathf.Abs(l.transform.position.z - z) + Mathf.Abs(l.transform.position.x)).FirstOrDefault();
                if (failing != null && failing.GetComponent<LightFlicker>() == null)
                    failing.gameObject.AddComponent<LightFlicker>().Configure(failing, null, null);
            }

            int livedIn = LivedIn(root, sections, fittings, random);
            Debug.Log($"Architecture detail: {beams} beams, {pipes} pipe/tray runs, {signs} hanging signs, {decals} wall decals, {livedIn} lived-in pieces " +
                      $"over {sections.Count(s => s.valid)}/{sections.Count} enclosed samples.");
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Floor, ceiling and both walls every half metre along the route; valid only where enclosed.</summary>
        static List<Section> SurveySections()
        {
            var list = new List<Section>();
            for (float z = -1.5f; z <= 64f; z += DetailStep)
            {
                var section = new Section { z = z };
                if (NavMesh.SamplePosition(new Vector3(0f, 0f, z), out var hit, 4f, NavMesh.AllAreas) && Mathf.Abs(hit.position.z - z) < 0.6f)
                {
                    Vector3 origin = new Vector3(hit.position.x, hit.position.y + 1.6f, z);
                    section.floor = hit.position.y;
                    section.x = hit.position.x;
                    if (Physics.Raycast(origin, Vector3.up, out var up, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        up.normal.y < -0.9f)
                    {
                        section.ceiling = up.point.y;
                        Vector3 high = new Vector3(hit.position.x, section.ceiling - 0.3f, z);
                        bool left = Physics.Raycast(high, Vector3.left, out var l, 9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        bool right = Physics.Raycast(high, Vector3.right, out var r, 9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        section.left = l.point.x;
                        section.right = r.point.x;
                        float height = section.ceiling - section.floor;
                        section.valid = left && right && height > 2.9f && height < 6f && section.right - section.left > 5f;
                    }
                }
                list.Add(section);
            }
            return list;
        }

        /// <summary>True when the box touches no collider and no small fitting (within margin).</summary>
        static bool Clear(Vector3 centre, Vector3 halfExtents, List<Bounds> fittings, float margin)
        {
            if (Physics.CheckBox(centre, halfExtents, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            var box = new Bounds(centre, halfExtents * 2f + Vector3.one * margin * 2f);
            return !fittings.Any(f => f.Intersects(box));
        }

        static int PipeRuns(Transform root, List<Section> sections, (int side, float inset, float drop, float diameter, string material) line,
            List<Bounds> fittings)
        {
            var material = PipeMaterial(line.material);
            var steel = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Steel - powder coat.mat");
            int runs = 0;
            var run = new List<Vector3>();
            float wall = 0f, ceiling = 0f;

            void Close()
            {
                if (run.Count * DetailStep >= 3f)
                {
                    Vector3 a = run[0], b = run[^1];
                    PipeBetween(root, "Ceiling pipe", a, b, line.diameter, material);
                    foreach (var end in new[] { a, b })
                    {
                        // Each run rises into the slab at both ends, with a flange where it turns.
                        PipeBetween(root, "Pipe riser", end, new Vector3(end.x, ceiling + 0.05f, end.z), line.diameter, material);
                        Shape(root, PrimitiveType.Cylinder, "Pipe flange", end, new Vector3(line.diameter * 1.6f, 0.025f, line.diameter * 1.6f), material)
                            .localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    for (float z = a.z + 1.2f; z < b.z - 0.6f; z += 2.4f)
                    {
                        var at = new Vector3(a.x, a.y, z);
                        PipeBetween(root, "Pipe hanger", at, new Vector3(a.x, ceiling + 0.02f, z), 0.018f, steel);
                        Shape(root, PrimitiveType.Cylinder, "Pipe clamp", at, new Vector3(line.diameter * 1.25f, 0.02f, line.diameter * 1.25f), steel)
                            .localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    runs++;
                }
                run.Clear();
            }

            foreach (var section in sections)
            {
                float sectionWall = line.side < 0 ? section.left : section.right;
                bool continues = run.Count > 0 && section.valid && Mathf.Abs(section.ceiling - ceiling) < 0.2f &&
                                 // A column standing proud of the wall (< 0.6 m) does not end the run; a new wall plane does.
                                 line.side * (sectionWall - wall) > -0.6f && line.side * (sectionWall - wall) < 0.2f;
                if (run.Count > 0 && !continues)
                    Close();
                if (!section.valid)
                    continue;
                if (run.Count == 0)
                {
                    wall = sectionWall;
                    ceiling = section.ceiling;
                }
                var point = new Vector3(wall - line.side * line.inset, ceiling - line.drop, section.z);
                float r = line.diameter * 0.5f;
                if (!Clear(point, new Vector3(r, r, DetailStep * 0.5f), fittings, 0.05f))
                {
                    Close();
                    continue;
                }
                run.Add(point);
            }
            Close();
            return runs;
        }

        /// <summary>Open steel tray with a few loose cables, right side.</summary>
        static int CableTray(Transform root, List<Section> sections, List<Bounds> fittings, Material steel)
        {
            var rubber = PipeMaterial("Cable - black rubber");
            int runs = 0;
            float start = float.NaN, last = 0f, wall = 0f, ceiling = 0f;

            void Close()
            {
                if (!float.IsNaN(start) && last - start >= 3f)
                {
                    float y = ceiling - 0.46f, x = wall - 0.78f, length = last - start, mid = (start + last) * 0.5f;
                    Shape(root, PrimitiveType.Cube, "Cable tray base", new Vector3(x, y, mid), new Vector3(0.45f, 0.012f, length), steel);
                    foreach (float edge in new[] { -0.22f, 0.22f })
                        Shape(root, PrimitiveType.Cube, "Cable tray lip", new Vector3(x + edge, y + 0.04f, mid), new Vector3(0.012f, 0.08f, length), steel);
                    for (int i = 0; i < 3; i++)
                        PipeBetween(root, "Cable", new Vector3(x - 0.12f + i * 0.11f, y + 0.03f, start), new Vector3(x - 0.12f + i * 0.11f, y + 0.03f, last),
                            0.035f + i * 0.01f, rubber);
                    for (float z = start + 1f; z < last; z += 2f)
                        PipeBetween(root, "Tray hanger", new Vector3(x, y, z), new Vector3(x, ceiling + 0.02f, z), 0.018f, steel);
                    // A cable sags out of the tray halfway along: the station has been looted for copper.
                    PipeBetween(root, "Hanging cable", new Vector3(x + 0.2f, y + 0.02f, mid), new Vector3(x + 0.35f, y - 0.9f, mid + 0.4f), 0.04f, rubber);
                    runs++;
                }
                start = float.NaN;
            }

            foreach (var section in sections)
            {
                bool continues = !float.IsNaN(start) && section.valid && Mathf.Abs(section.ceiling - ceiling) < 0.2f &&
                                 section.right - wall > -0.6f && section.right - wall < 0.2f;
                if (!float.IsNaN(start) && !continues)
                    Close();
                if (!section.valid)
                    continue;
                if (float.IsNaN(start))
                {
                    wall = section.right;
                    ceiling = section.ceiling;
                }
                if (!Clear(new Vector3(wall - 0.78f, ceiling - 0.43f, section.z), new Vector3(0.24f, 0.05f, DetailStep * 0.5f), fittings, 0.05f))
                {
                    Close();
                    continue;
                }
                if (float.IsNaN(start))
                    start = section.z;
                last = section.z;
            }
            Close();
            return runs;
        }

        static void HangSign(Transform root, string design, Vector3 beamBottom, float floor, Material steel)
        {
            const float width = 2.3f, height = width / 4f;
            float y = Mathf.Max(beamBottom.y - 0.26f - height * 0.5f, floor + 2.45f + height * 0.5f);
            var centre = new Vector3(beamBottom.x, y, beamBottom.z);
            Shape(root, PrimitiveType.Cube, design + " box", centre, new Vector3(width + 0.06f, height + 0.06f, 0.1f), steel);
            foreach (float x in new[] { -width * 0.35f, width * 0.35f })
                PipeBetween(root, "Sign rod", centre + new Vector3(x, height * 0.5f, 0f), new Vector3(centre.x + x, beamBottom.y, centre.z), 0.016f, steel);
            var face = SignFace(design);
            foreach (float facing in new[] { 0f, 180f })
            {
                var quad = Shape(root, PrimitiveType.Quad, design + " face", centre + Quaternion.Euler(0, facing, 0) * Vector3.back * 0.052f,
                    new Vector3(width, height, 1f), face);
                quad.localRotation = Quaternion.Euler(0f, facing, 0f);
            }
            if (design == "Hanging exit")
                StationLight(root, centre + Vector3.down * 0.3f, new Color(0.45f, 1f, 0.55f), 180, 3.2f).shadows = LightShadows.None;
        }

        static Material SignFace(string design)
        {
            string path = $"{SettingsFolder}/{design} sign.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_UnlitColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Signs/{design}.png"));
            // Back-lit boxes: brighter than the painted wall signs (0.71).
            float glow = design == "Hanging exit" ? 1.25f : 0.95f;
            material.SetColor("_UnlitColor", new Color(glow, glow, glow));
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Tinted copies of the triplanar steel plate for painted service lines; plain black for rubber.</summary>
        static Material PipeMaterial(string name)
        {
            string path = $"{SettingsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && name.StartsWith("Forecourt"))
                return material;
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Steel - powder coat.mat");
                material = name.StartsWith("Cable") ? new Material(Shader.Find("HDRP/Lit")) : new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            if (name.Contains("yellow"))
                material.SetColor("_BaseColor", new Color(0.8f, 0.6f, 0.1f));
            else if (name.Contains("red"))
                material.SetColor("_BaseColor", new Color(0.55f, 0.07f, 0.05f));
            else
            {
                material.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.03f));
                material.SetFloat("_Smoothness", 0.35f);
            }
            if (!name.StartsWith("Cable"))
                material.SetFloat("_MetallicRemapMax", 0.15f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Survivor graffiti and taped-up posters on long wall stretches, spaced apart.</summary>
        static int WallStories(Transform root, List<Section> sections, System.Random random)
        {
            int placed = 0, graffiti = 0, posters = 0;
            var lastBySide = new Dictionary<int, float> { [-1] = float.MinValue, [1] = float.MinValue };
            foreach (var section in sections.Where(s => s.valid))
            foreach (int side in new[] { -1, 1 })
            {
                if (section.z - lastBySide[side] < 6f || random.NextDouble() > 0.5)
                    continue;
                Vector3 origin = new Vector3(section.x, section.floor + 1.7f, section.z);
                if (!Physics.Raycast(origin, Vector3.right * side, out var wall, 9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    Mathf.Abs(wall.normal.y) > 0.2f || Blocked(new Vector3(wall.point.x, section.floor, wall.point.z), wall.collider))
                    continue;
                // Big flat stretch only: no pillars, doors or windows under the paint.
                var extent = wall.collider.bounds.size;
                var surface = wall.collider.GetComponent<Renderer>();
                if (Mathf.Max(extent.x, extent.z) < 3f || surface == null || !surface.enabled ||
                    wall.collider.name.ToLowerInvariant() is var n && (n.Contains("window") || n.Contains("glass") || n.Contains("door")))
                    continue;
                // The whole painted width must land on this one flat wall: no column or fitting in front of any part.
                Vector3 alongWall = Vector3.Cross(wall.normal, Vector3.up).normalized;
                bool flat = Enumerable.Range(-5, 11).Select(i => i * 0.25f).All(offset =>
                    Physics.Raycast(wall.point + wall.normal + alongWall * offset, -wall.normal, out var probe, 1.3f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    probe.collider == wall.collider && Mathf.Abs(probe.distance - 1f) < 0.05f);
                if (!flat)
                    continue;
                var facing = Quaternion.LookRotation(-wall.normal);
                if (placed % 2 == 0)
                {
                    string design = Graffiti[graffiti++ % Graffiti.Length];
                    Decal(root, design, StoryDecal(design), wall.point + wall.normal * 0.15f + Vector3.up * (float)random.NextDouble() * 0.3f,
                        facing * Quaternion.Euler(0, 0, (float)random.NextDouble() * 8f - 4f), new Vector3(2.6f, 1.3f, 0.4f));
                }
                else
                {
                    // Two or three sheets overlapping, as if taped up in a hurry.
                    int sheets = 2 + random.Next(2);
                    Vector3 along = Vector3.Cross(wall.normal, Vector3.up).normalized;
                    for (int i = 0; i < sheets; i++)
                    {
                        string design = Posters[posters++ % Posters.Length];
                        Decal(root, design, StoryDecal(design),
                            wall.point + wall.normal * 0.15f + along * (i * 0.48f - 0.4f) + Vector3.down * (float)random.NextDouble() * 0.25f,
                            facing * Quaternion.Euler(0, 0, (float)random.NextDouble() * 10f - 5f), new Vector3(0.46f, 0.65f, 0.4f));
                    }
                }
                // A caged wall lamp above each story so it reads in the dark; every third one is failing.
                WallLamp(root, wall.point + Vector3.up * 1.15f, wall.normal, placed % 3 == 1);
                lastBySide[side] = section.z;
                placed++;
            }
            return placed;
        }

        static Material lampLens, lampOff;

        /// <summary>Caged bulkhead lamp on a wall: housing, warm lens, three cage bars and a soft unshadowed light.</summary>
        static void WallLamp(Transform root, Vector3 wallPoint, Vector3 normal, bool failing)
        {
            var steel = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Steel - powder coat.mat");
            lampLens ??= Surface("Bulkhead lamp lens", new Color(1f, 0.82f, 0.58f), 0.6f, emission: 60f);
            lampOff ??= Surface("Bulkhead lamp lens off", new Color(0.32f, 0.3f, 0.26f), 0.6f);
            var lamp = new GameObject("Caged wall lamp").transform;
            lamp.SetParent(root, false);
            lamp.SetPositionAndRotation(wallPoint + normal * 0.01f, Quaternion.LookRotation(normal));
            var housing = Shape(lamp, PrimitiveType.Cube, "Lamp housing", new Vector3(0f, 0f, 0.05f), new Vector3(0.3f, 0.2f, 0.1f), steel);
            var lens = Shape(lamp, PrimitiveType.Cube, "Lamp lens", new Vector3(0f, 0f, 0.11f), new Vector3(0.22f, 0.13f, 0.04f), lampLens);
            foreach (float x in new[] { -0.08f, 0f, 0.08f })
                Shape(lamp, PrimitiveType.Cube, "Lamp cage bar", new Vector3(x, 0f, 0.14f), new Vector3(0.012f, 0.17f, 0.012f), steel);
            var light = StationLight(lamp, wallPoint + normal * 0.35f, new Color(1f, 0.78f, 0.52f), 700, 5.5f);
            light.shadows = LightShadows.None;
            if (failing)
                lamp.gameObject.AddComponent<LightFlicker>().Configure(light, lens.GetComponent<Renderer>(), lampOff);
        }

        /// <summary>
        /// Observation only: renders six fixed interior views to &lt;dir&gt;/view-N.png.
        /// Args: -detailViews &lt;dir&gt; [-withoutDetail] (hides the pass for a before shot).
        /// </summary>
        public static void CaptureDetailViews()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            string folder = arguments[System.Array.IndexOf(arguments, "-detailViews") + 1];
            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            if (arguments.Contains("-withoutDetail"))
                GameObject.Find(DetailName)?.SetActive(false);
            var camera = Camera.main;
            foreach (Transform child in camera.transform)
                child.gameObject.SetActive(false);
            var views = DetailViews.Select(v => (v.eye, v.angles)).ToArray();
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            for (int i = 0; i < views.Length; i++)
            {
                camera.transform.SetPositionAndRotation(views[i].Item1, Quaternion.Euler(views[i].Item2));
                // Several renders: exposure, temporal history and the decal atlas fill over frames.
                for (int pass = 0; pass < 6; pass++)
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                System.IO.File.WriteAllBytes($"{folder}/view-{i}.png", image.EncodeToPNG());
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Fixed review views of the enclosed route, shared with the play-mode -detailTour check.</summary>
        public static readonly (Vector3 eye, Vector3 angles)[] DetailViews =
        {
            (new Vector3(0.4f, 1.67f, -0.5f), new Vector3(-10f, 0f, 0f)),
            (new Vector3(0f, 1.67f, 10f), new Vector3(-6f, 45f, 0f)),
            (new Vector3(1.5f, 1.67f, 27f), new Vector3(-10f, 190f, 0f)),
            (new Vector3(0f, 1.67f, 38f), new Vector3(-10f, 0f, 0f)),
            (new Vector3(-1.5f, 1.67f, 46f), new Vector3(-6f, -50f, 0f)),
            (new Vector3(2f, 1.67f, 57f), new Vector3(-6f, 50f, 0f)),
            (new Vector3(1.5f, 1.67f, 14f), new Vector3(-4f, 60f, 0f)),
            (new Vector3(1f, 1.67f, 21f), new Vector3(-4f, 55f, 0f)),
            (new Vector3(-0.5f, 1.67f, -0.5f), new Vector3(12f, 55f, 0f)),
            (new Vector3(0.5f, 1.67f, 10.5f), new Vector3(10f, -130f, 0f)),
            (new Vector3(0f, 1.67f, 53.5f), new Vector3(0f, 18f, 0f)),
        };

        static readonly Dictionary<string, Material> storyDecals = new();

        static Material StoryDecal(string design)
        {
            if (storyDecals.TryGetValue(design, out var cached) && cached != null)
                return cached;
            string texturePath = $"Assets/Art/Decals/{design}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            string path = $"{SettingsFolder}/Decal - {design}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Decal"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", design.StartsWith("Poster") || design.StartsWith("Paper") ? new Color(0.8f, 0.8f, 0.78f, 1f) : Color.white);
            // No mask map, so smoothness comes from the remap: wet blood and water shine, paper and paint stay matte.
            float gloss = design.StartsWith("Blood") ? 0.82f : design.StartsWith("Wet") ? 0.95f : design.StartsWith("Graffiti") ? 0.35f : 0.15f;
            material.SetFloat("_SmoothnessRemapMin", gloss);
            material.SetFloat("_SmoothnessRemapMax", gloss);
            material.SetFloat("_AffectMetal", 0f);
            material.SetFloat("_AffectSmoothness", 1f);
            material.SetFloat("_AffectAlbedo", 1f);
            material.SetFloat("_AffectNormal", 0f);
            // HDRP decals default to half opacity; paint and paper should cover the wall.
            material.SetFloat("_DecalBlend", 1f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            storyDecals[design] = material;
            return material;
        }
    }
}
