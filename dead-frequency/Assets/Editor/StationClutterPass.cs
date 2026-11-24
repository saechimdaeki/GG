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
    /// Lived-in debris along every wall: Poly Haven CC0 props (barrels, crates, cardboard, cement bags, bins...)
    /// placed flush against walls found by raycasting out from the walkable centre line. Skips doors, pickups and
    /// anything already standing there. Deterministic; re-running replaces the previous pass.
    /// Batch: -executeMethod ResidualSignal.StationSceneBuilder.InstallClutter
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const string ClutterName = "Clutter pass";
        const string PolyHavenRoot = "Assets/Art/Models/PolyHaven";

        // id, height (m), solid
        static readonly (string id, float height, bool solid)[] ClutterProps =
        {
            ("barrel_03", 0.9f, true), ("barrel_stove", 0.85f, true), ("cardboard_box_01", 0.45f, false),
            ("cement_bag", 0.18f, false), ("metal_trash_can", 0.95f, true), ("plastic_crate_01", 0.32f, false),
            ("plastic_crate_03", 0.36f, false), ("metal_jerrycan", 0.45f, false), ("old_military_crate", 0.5f, true),
            ("wooden_crate_02", 0.62f, true), ("compost_bags", 0.45f, false), ("can_rusted", 0.12f, false),
            ("plastic_monobloc_chair_01", 0.8f, false), ("tool_cart", 0.95f, true), ("utility_box_02", 1.0f, true),
        };

        [MenuItem("잔류신호/벽 주변 잡동사니 배치")]
        public static void InstallClutter()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var old = GameObject.Find(ClutterName);
            if (old != null)
                Object.DestroyImmediate(old);
            Physics.SyncTransforms();

            var prefabs = ClutterProps.ToDictionary(prop => prop.id, prop => AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PolyHavenRoot}/{prop.id}/{prop.id}_1k.fbx"));
            var materials = new Dictionary<string, Material>();
            var root = new GameObject(ClutterName).transform;
            var random = new System.Random(20261124);
            int placed = 0;
            for (float z = -9f; z <= 64f; z += 1.7f)
            {
                if (!NavMesh.SamplePosition(new Vector3(0f, 0f, z), out var centre, 4f, NavMesh.AllAreas))
                    continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 origin = centre.position + Vector3.up * 0.5f;
                    if (!Physics.Raycast(origin, Vector3.right * side, out RaycastHit wall, 9f, Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore) || Vector3.Dot(wall.normal, Vector3.up) > 0.3f ||
                        // Real walls only: a table or counter side is not a wall to pile junk against.
                        wall.collider.bounds.size.y < 2.2f)
                        continue;
                    // Grime creeping up from the floor on most wall stretches.
                    if (random.NextDouble() < 0.7)
                        Decal(root, "Wall grime", GrimeMaterial(), new Vector3(wall.point.x, centre.position.y + 0.75f, wall.point.z) + wall.normal * 0.2f,
                            Quaternion.LookRotation(-wall.normal), new Vector3(1.8f + (float)random.NextDouble() * 1.8f, 1.5f, 0.6f));
                    if (random.NextDouble() < 0.3)
                        Decal(root, "Floor stain", GrimeMaterial(), centre.position + new Vector3(side * (float)random.NextDouble() * 2f, 0.3f,
                            (float)random.NextDouble()), Quaternion.Euler(90f, (float)random.NextDouble() * 360f, 0f),
                            new Vector3(2.2f, 2.2f, 0.8f));
                    if (random.NextDouble() > 0.62)
                        continue;
                    // A small cluster: one anchor prop, sometimes a smaller one beside it.
                    int count = random.NextDouble() < 0.45 ? 2 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        var prop = ClutterProps[random.Next(ClutterProps.Length)];
                        Vector3 along = Vector3.Cross(wall.normal, Vector3.up).normalized * (i * 0.75f);
                        Vector3 spot = new Vector3(wall.point.x, centre.position.y, wall.point.z) + wall.normal * 0.42f + along;
                        if (Blocked(spot, wall.collider))
                            continue;
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prop.id], root);
                        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        model.name = prop.id;
                        // Keep the FBX's own axis fix (Z-up sources import with a -90° X root rotation).
                        Quaternion upright = model.transform.localRotation;
                        model.transform.SetPositionAndRotation(spot,
                            Quaternion.LookRotation(-wall.normal) * Quaternion.Euler(0, (float)random.NextDouble() * 50f - 25f, 0) * upright);
                        FitModel(model, prop.height * (0.9f + (float)random.NextDouble() * 0.2f));
                        // Keep the back edge off the wall after scaling.
                        var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
                            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                        model.transform.position += wall.normal * Mathf.Max(0f, Vector3.Dot(wall.point - bounds.center, wall.normal) +
                            Mathf.Abs(Vector3.Dot(bounds.extents, wall.normal)) + 0.02f);
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                            renderer.sharedMaterials = renderer.sharedMaterials
                                .Select(slot => PolyHavenMaterial(prop.id, slot != null ? slot.name : prop.id, materials)).ToArray();
                        if (prop.solid)
                        {
                            var box = model.AddComponent<BoxCollider>();
                            var local = model.GetComponentsInChildren<MeshFilter>().First().sharedMesh.bounds;
                            box.center = local.center;
                            box.size = local.size;
                        }
                        GameObjectUtility.SetStaticEditorFlags(model, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
                        Physics.SyncTransforms();
                        placed++;
                    }
                }
            }
            Debug.Log($"Clutter pass placed {placed} props.");
            EditorSceneManager.SaveScene(scene);
        }

        static void Decal(Transform root, string name, Material material, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var node = new GameObject(name);
            node.transform.SetParent(root, false);
            node.transform.SetPositionAndRotation(position, rotation);
            var projector = node.AddComponent<DecalProjector>();
            projector.material = material;
            projector.size = size;
            projector.pivot = Vector3.zero;
        }

        static Material grime;

        /// <summary>Soot and damp: fBm blotches, streaked vertically and fading upward. Generated once as an asset.</summary>
        static Material GrimeMaterial()
        {
            if (grime != null)
                return grime;
            const string texturePath = "Assets/Rendering/Grime streaks.png";
            if (!System.IO.File.Exists(texturePath))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float noise = 0f, amplitude = 0.5f, frequency = 3f;
                    for (int octave = 0; octave < 5; octave++, amplitude *= 0.5f, frequency *= 2.1f)
                        noise += amplitude * Mathf.PerlinNoise(u * frequency + 17.3f, v * frequency * 0.35f + 4.1f);
                    float streak = Mathf.PerlinNoise(u * 38f, 2.7f) * 0.35f;
                    float edge = Mathf.SmoothStep(0f, 0.18f, u) * Mathf.SmoothStep(0f, 0.18f, 1f - u);
                    float rise = Mathf.Pow(1f - v, 1.6f);
                    float alpha = Mathf.Clamp01((noise + streak - 0.42f) * 2.2f) * rise * edge;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * alpha));
                }
                texture.SetPixels32(pixels);
                System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            const string materialPath = "Assets/Rendering/Station - grime decal.mat";
            grime = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (grime == null)
            {
                grime = new Material(Shader.Find("HDRP/Decal"));
                AssetDatabase.CreateAsset(grime, materialPath);
            }
            grime.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            grime.SetColor("_BaseColor", new Color(0.07f, 0.06f, 0.05f, 0.85f));
            grime.SetFloat("_AffectAlbedo", 1f);
            grime.SetFloat("_AffectNormal", 0f);
            HDMaterial.ValidateMaterial(grime);
            EditorUtility.SetDirty(grime);
            return grime;
        }

        /// <summary>Anything gameplay-related or already solid near the spot keeps it clear.</summary>
        static bool Blocked(Vector3 spot, Collider wall)
        {
            foreach (var other in Physics.OverlapSphere(spot + Vector3.up * 0.5f, 1.1f, Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                if (other == wall)
                    continue;
                bool gameplay = other.GetComponentsInParent<MonoBehaviour>(true)
                    .Any(component => component.GetType().Namespace == "ResidualSignal");
                bool door = other.name.ToLowerInvariant() is var n && (n.Contains("door") || n.Contains("gate") ||
                    n.Contains("shutter") || n.Contains("stair"));
                // Floors sit below the spot; walls and pillars rise past head height. Anything in between is furniture.
                bool furniture = other.bounds.max.y > spot.y + 0.15f && other.bounds.max.y < spot.y + 2.4f;
                if (gameplay || door || (other.isTrigger && other.bounds.size.magnitude < 6f) || furniture)
                    return true;
            }
            return false;
        }

        static Material PolyHavenMaterial(string asset, string slot, Dictionary<string, Material> cache)
        {
            string key = asset + "/" + slot;
            if (cache.TryGetValue(key, out var cached))
                return cached;
            string folder = $"{PolyHavenRoot}/{asset}/textures/";
            string Map(string kind)
            {
                foreach (string prefix in new[] { slot, asset })
                foreach (string extension in new[] { "jpg", "png", "exr" })
                {
                    string path = $"{folder}{prefix}_{kind}_1k.{extension}";
                    if (System.IO.File.Exists(path))
                        return path;
                }
                return null;
            }
            string path = $"Assets/Rendering/PolyHaven - {asset} {slot}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            string diffuse = Map("diff");
            if (diffuse != null)
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            string normal = Map("nor_gl");
            if (normal != null)
                material.SetTexture("_NormalMap", ImportNormal(normal));
            bool metal = Map("metal") != null && (asset.Contains("barrel") || asset.Contains("metal") || asset.Contains("jerrycan") ||
                asset.Contains("cart") || asset.Contains("utility") || asset.Contains("can"));
            material.SetFloat("_Metallic", metal ? 0.55f : 0f);
            material.SetFloat("_Smoothness", metal ? 0.38f : 0.22f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            cache[key] = material;
            return material;
        }
    }
}
