using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Platform03.unity";
        const string SettingsFolder = "Assets/Rendering";

        [MenuItem("잔류신호/승강장 씬 생성")]
        public static void Build()
        {
            Directory.CreateDirectory(SettingsFolder);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            PlayerSettings.productName = "잔류신호";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(SettingsFolder + "/StationHDRP.asset");
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, SettingsFolder + "/StationHDRP.asset");
            }
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_RenderPipelineSettings.supportSSR").boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Platform 03").transform;
            var tile = Surface("Floor - damp porcelain", new Color(0.76f, 0.71f, 0.64f), 0.92f, new Vector2(3, 10), maps: "Floor");
            var wall = Surface("Wall - glazed tile", new Color(0.82f, 0.77f, 0.68f), 0.59f, new Vector2(13, 2.2f), maps: "Wall");
            var concrete = Surface("Ceiling - cast concrete", new Color(0.68f, 0.70f, 0.65f), 0.38f, new Vector2(8, 22), maps: "Concrete");
            var steel = Surface("Steel - powder coat", new Color(0.30f, 0.36f, 0.37f), 0.59f);
            var warning = Surface("Platform edge - ochre", new Color(0.77f, 0.53f, 0.18f), 0.30f);
            var strip = Surface("Fluorescent diffuser", new Color(0.9f, 0.95f, 0.91f), 0.24f, emission: 110f);
            var amber = Surface("Power indicator", new Color(1f, 0.48f, 0.14f), 0.34f, emission: 22f);
            var teal = Surface("Line 3 - weathered green", new Color(0.18f, 0.29f, 0.22f), 0.45f);
            var darkFixture = Surface("Unpowered diffuser", new Color(0.35f, 0.37f, 0.33f), 0.32f);

            Box(root, "Platform floor", new(0, -0.24f, 17), new(11.58f, 0.48f, 40), tile);
            Box(root, "Ceiling", new(0, 4.25f, 17), new(12, 0.38f, 40), concrete);
            BuildForecourt(root, concrete, steel);
            for (int side = -1; side <= 1; side += 2)
            {
                if (side > 0)
                    Box(root, "Side wall", new(5.9f, 2, 17), new(0.38f, 4, 40), wall);
                else
                    Box(root, "Track boundary", new(-7.24f, 2f, 17), new(0.38f, 4f, 40f), concrete);
                Box(root, "Rubber skirting", new(side * 5.68f, 0.14f, 17), new(0.08f, 0.25f, 39), steel, false);
                Box(root, "Tactile warning strip", new(side * 5.22f, 0.008f, 16), new(0.32f, 0.018f, 36), warning, false);
                Box(root, "Service conduit", new(side * 4.7f, 3.93f, 17), new(0.12f, 0.15f, 39), steel, false);
                Box(root, "Service wall", new(side * 4.12f, 2, 30), new(3.6f, 4, 0.50f), concrete);
            }

            Box(root, "Entrance bulkhead", new(0, 3.65f, 1.2f), new(11.6f, 0.85f, 1.2f), concrete);
            Box(root, "Transfer junction header", new(0, 3.52f, 9.0f), new(11.6f, 0.70f, 0.8f), concrete);
            Box(root, "Transfer junction sign mount", new(0, 3.3f, 8.55f), new(3.7f, 0.75f, 0.09f), steel, false);
            Box(root, "Line 3 wall band", new(5.68f, 2.48f, 17), new(0.04f, 0.24f, 38), teal, false);
            Box(root, "Service fascia", new(0, 3.57f, 29.82f), new(4.67f, 0.68f, 0.22f), steel, false);
            for (int z = 0; z <= 28; z += 4)
            {
                Box(root, "Fixture housing", new(0, 3.99f, z), new(2.38f, 0.11f, 0.28f), steel, false);
                Box(root, "Fluorescent tube A", new(-0.57f, 3.918f, z), new(1.07f, 0.05f, 0.085f), z == 12 || z == 16 ? darkFixture : strip, false);
                Box(root, "Fluorescent tube B", new(0.57f, 3.918f, z), new(1.07f, 0.05f, 0.085f), z == 12 || z == 16 ? darkFixture : strip, false);
                if (z is 0 or 4 or 8 or 20 or 24 or 28)
                    StationLight(root, new(0, 3.74f, z), new Color(0.94f, 0.96f, 0.85f),
                        z is 4 or 20 ? 4700 : 3250, 10);
                Box(root, "Column", new(-5.58f, 2, z - 1.55f), new(0.52f, 4, 0.57f), concrete);
                Box(root, "Column", new(5.58f, 2, z - 1.55f), new(0.52f, 4, 0.57f), concrete);
            }
            FinishStationArchitecture(root, concrete, steel, warning);

            var barricade = Pivot(root, "Abandoned evacuation barrier", new(-4.55f, 0, 3.5f));
            LoadProp("concrete_road_barrier_02", barricade, 0.77f, 24f);
            Box(barricade, "Concrete barrier collision", new(0, 0.37f, 0), new(1.35f, 0.74f, 0.42f), concrete)
                .GetComponent<Renderer>().enabled = false;

            var terminal = Pivot(root, "Emergency power cabinet", new(3.75f, 0, 16.8f));
            LoadProp("utility_box_01", terminal, 1.44f, 0);
            var terminalCollision = terminal.gameObject.AddComponent<BoxCollider>();
            terminalCollision.center = Vector3.up * 0.72f;
            terminalCollision.size = new Vector3(0.76f, 1.44f, 0.45f);
            Box(root, "Power cabinet indicator", new(3.75f, 1.11f, 16.557f), new(0.24f, 0.10f, 0.037f), amber, false);
            Sign(root, "Power", new(3.75f, 1.65f, 16.52f), 1.15f, steel);
            Sign(root, "Platform", new(0, 3.30f, 8.49f), 2.95f, steel);
            Sign(root, "Exit", new(0, 3.57f, 29.66f), 1.7f, steel);
            StationLight(root, new(4.82f, 2.03f, 16.2f), new Color(1f, 0.56f, 0.29f), 1600, 7.5f);
            StationLight(root, new(-4.65f, 2.0f, 17.4f), new Color(0.72f, 0.86f, 0.92f), 1900, 8f);
            // Wall bounce brings faces out of silhouette across the main firing lane.
            StationLight(root, new(2.4f, 2.85f, 18.0f), new Color(0.99f, 0.96f, 0.88f), 7100, 12f).shadows = LightShadows.None;
            StationLight(root, new(-2.2f, 2.8f, 24.3f), new Color(0.83f, 0.91f, 1f), 6200, 11f).shadows = LightShadows.None;
            var shutter = Shutter(root, "Security shutter", new(0, 1.59f, 30), new(4.68f, 3.18f, 0.23f));
            var exitLight = StationLight(root, new(0, 2.98f, 28.70f), new Color(1f, 0.56f, 0.30f), 1850, 9);
            StationLight(root, new(0, 3.1f, 33.3f), new Color(0.52f, 0.74f, 0.81f), 2150, 9);
            AddDefensePositions(root, concrete);
            ConfigureVolume();

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0.4f, 0.025f, ForecourtSpawnZ);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.78f;
            controller.radius = 0.33f;
            controller.center = new Vector3(0, 0.89f, 0);
            var camera = new GameObject("FPS Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.gameObject.tag = "MainCamera";
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.65f, 0);
            camera.fieldOfView = 66;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 100f;
            var cameraData = camera.GetComponent<HDAdditionalCameraData>();
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            cameraData.backgroundColorHDR = new Color(0.02f, 0.026f, 0.033f);
            cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.customRenderingSettings = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.MotionBlur, false);
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.MotionBlur] = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.SSR, true);
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.SSR] = true;

            var game = new SerializedObject(player.AddComponent<StationTrial>());
            game.FindProperty("player").objectReferenceValue = controller;
            game.FindProperty("eyes").objectReferenceValue = camera;
            game.FindProperty("terminal").objectReferenceValue = terminal;
            game.FindProperty("shutter").objectReferenceValue = shutter;
            game.FindProperty("exitLight").objectReferenceValue = exitLight;
            CreateCombatAssets(game, camera);
            CreateStory(root, game);
            BuildServiceYard(root, game, tile, concrete, steel);
            BuildFreightPassage(root, game);
            CreateRoutes(root, game, camera);
            CreateNetwork(game);
            PlaceLoot(root);
            BakeNavMesh(root, player);
            game.ApplyModifiedPropertiesWithoutUndo();
            BuildMaterialPass(root);
            var trial = Object.FindFirstObjectByType<StationTrial>();
            AdjustWeaponGrips(trial);
            var guns = new SerializedObject(trial).FindProperty("arsenal");
            for (int i = 0; i < guns.arraySize; i++)
                FinishRescueArms((Transform)guns.GetArrayElementAtIndex(i).FindPropertyRelative("view").objectReferenceValue);
            ApplyCharacterMaterials();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PLATFORM03 SCENE READY");
        }

        static void AddDefensePositions(Transform root, Material concrete)
        {
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 point = new(side * 3.8f, 0f, side < 0 ? 20.8f : 24.1f);
                var position = Pivot(root, side < 0 ? "Track-side defense cover" : "Wall-side defense cover", point);
                LoadProp("concrete_road_barrier_02", position, 0.82f, 0f);
                var collision = Box(position, "Defense barricade collision", new(0, 0.36f, 0), new(1.42f, 0.72f, 0.42f), concrete);
                collision.GetComponent<Renderer>().enabled = false;
            }
        }

        static void ConfigureVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SettingsFolder + "/StationVolume.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, SettingsFolder + "/StationVolume.asset");
            }
            profile.components.RemoveAll(component => component == null);
            if (!profile.TryGet(out Exposure exposure))
            {
                exposure = profile.Add<Exposure>(true);
                AssetDatabase.AddObjectToAsset(exposure, profile);
                exposure.mode.Override(ExposureMode.Fixed);

                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
                AssetDatabase.AddObjectToAsset(tone, profile);
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.12f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                EditorUtility.SetDirty(profile);
            }
            if (!profile.TryGet(out ScreenSpaceReflection reflection))
            {
                reflection = profile.Add<ScreenSpaceReflection>();
                reflection.enabled.Override(true);
                reflection.enabledTransparent.Override(false);
                AssetDatabase.AddObjectToAsset(reflection, profile);
                EditorUtility.SetDirty(profile);
            }
            var fog = StationVolumeSetting<Fog>(profile);
            fog.enabled.Override(false);
            EditorUtility.SetDirty(fog);
            var indirect = StationVolumeSetting<IndirectLightingController>(profile);
            indirect.indirectDiffuseLightingMultiplier.Override(0.43f);
            EditorUtility.SetDirty(indirect);
            var grade = StationVolumeSetting<ColorAdjustments>(profile);
            grade.contrast.Override(0f);
            grade.saturation.Override(-12f);
            grade.colorFilter.Override(new Color(1f, 0.94f, 0.855f));
            EditorUtility.SetDirty(grade);
            exposure.fixedExposure.Override(5.5f);
            EditorUtility.SetDirty(exposure);
            InstallNightSky(profile);
            InstallInteriorSkyOverride();
            ApplyCinematicLook(profile);
            EditorUtility.SetDirty(profile);
            AddInteriorAmbience();
            AddMoonlight();
            var volume = new GameObject("Station volume", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static T StationVolumeSetting<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T setting))
                return setting;
            setting = profile.Add<T>();
            AssetDatabase.AddObjectToAsset(setting, profile);
            EditorUtility.SetDirty(profile);
            return setting;
        }

        static Light StationLight(Transform root, Vector3 point, Color tint, float lumens, float reach)
        {
            var node = new GameObject("Station light");
            node.transform.SetParent(root);
            node.transform.position = point;
            node.AddHDLight(LightType.Point);
            var light = node.GetComponent<Light>();
            light.color = tint;
            light.range = reach;
            light.shadows = LightShadows.Soft;
            light.lightUnit = LightUnit.Lumen;
            light.intensity = LightUnitUtils.ConvertIntensity(light, lumens, LightUnit.Lumen, LightUnit.Candela);
            return light;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 dimensions, Material material, bool collision = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = dimensions;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
                Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            return box;
        }

        static void Sign(Transform root, string design, Vector3 point, float width, Material backing)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Signs/{design}.png");
            float height = width * texture.height / texture.width;
            Box(root, design + " sign frame", point + Vector3.forward * 0.033f,
                new(width + 0.055f, height + 0.055f, 0.06f), backing, false);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = design + " sign face";
            face.transform.SetParent(root, false);
            face.transform.localPosition = point;
            face.transform.localScale = new(width, height, 1f);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            string path = $"{SettingsFolder}/{design} sign.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_UnlitColorMap", texture);
            material.SetColor("_UnlitColor", new Color(0.71f, 0.74f, 0.69f));
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            face.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material Surface(string name, Color color, float smoothness, Vector2 tiling = default, float emission = 0, string maps = null)
        {
            string path = $"{SettingsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (tiling != Vector2.zero)
            {
                material.SetTexture("_BaseColorMap", maps == null ? TileTexture() : MaterialTexture(maps + "Color.jpg"));
                if (maps != null)
                {
                    material.SetTexture("_NormalMap", MaterialTexture(maps + "Normal.jpg", normal: true));
                    material.SetTexture("_MaskMap", MaterialTexture(maps + "Mask.png", linear: true));
                    material.SetFloat("_NormalScale", maps == "Floor" ? 0.34f : 0.42f);
                    material.SetFloat("_SmoothnessRemapMin", 0.02f);
                    material.SetFloat("_SmoothnessRemapMax", smoothness);
                }
                material.SetTextureScale("_BaseColorMap", tiling);
            }
            if (emission > 0)
                material.SetColor("_EmissiveColor", color * emission);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D MaterialTexture(string filename, bool normal = false, bool linear = false)
        {
            string path = "Assets/Art/Materials/" + filename;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = !linear && !normal;
            if (importer.textureType != textureType || importer.sRGBTexture != srgb)
            {
                importer.textureType = textureType;
                importer.sRGBTexture = srgb;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D TileTexture()
        {
            const string path = SettingsFolder + "/Porcelain.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                float speckle = ((x * 73 ^ y * 151 ^ x * y * 17) & 31) / 31f;
                float shade = edge < 2 ? 0.34f : edge < 4 ? 0.84f :
                    0.87f + Mathf.PerlinNoise(x * 0.059f, y * 0.048f) * 0.095f + speckle * 0.035f;
                texture.SetPixel(x, y, new Color(shade, shade, shade));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
