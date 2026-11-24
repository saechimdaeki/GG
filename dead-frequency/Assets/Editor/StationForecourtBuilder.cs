using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const float ForecourtSpawnZ = -18.8f;

        static void BuildForecourt(Transform root, Material concrete, Material steel)
        {
            var plaza = Pivot(root, "Haerim station night forecourt", Vector3.zero);
            var ground = Surface("Forecourt - damp granite", new Color(0.54f, 0.58f, 0.55f),
                0.91f, new(7, 8), maps: "Floor");
            var masonry = Surface("Forecourt - soot stained facade", new Color(0.45f, 0.47f, 0.42f),
                0.42f, new(11, 3), maps: "Concrete");
            var darkMetal = Surface("Forecourt - oxidized dark steel", new Color(0.15f, 0.19f, 0.16f), 0.46f);
            var fascia = Surface("Forecourt - enamel green", new Color(0.14f, 0.23f, 0.17f), 0.51f);
            var window = Surface("Forecourt - dark ticket glass", new Color(0.22f, 0.26f, 0.23f), 0.89f);
            var whiteLamp = Surface("Forecourt - warm glass", new Color(1, 0.84f, 0.58f), 0.44f, emission: 210f);
            var redLamp = Surface("Forecourt - red indicator", new Color(0.90f, 0.26f, 0.092f), 0.55f, emission: 170f);
            var greenLamp = Surface("Forecourt - olive indicator", new Color(0.61f, 0.83f, 0.37f), 0.55f, emission: 130f);
            var tactile = Surface("Forecourt - aged guidance strip", new Color(0.58f, 0.43f, 0.18f), 0.53f);
            Box(plaza, "Outdoor granite paving", new(0, -0.235f, -13.88f), new(24, 0.47f, 22.0f), ground);
            LayTactileStrip(plaza, new(2.53f, 0, -22.8f), new(2.53f, 0, -8.9f), tactile);
            LayTactileStrip(plaza, new(2.53f, 0, -8.9f), new(0.82f, 0, -6.32f), tactile);
            LayTactileStrip(plaza, new(0.82f, 0, -6.32f), new(0.82f, 0, -2.9f), tactile);
            Box(plaza, "Entrance header", new(0, 3.95f, -2.88f), new(13.4f, 1.9f, 0.63f), masonry);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(plaza, "Closed facade side return", new(side * 7.48f, 1.53f, -2.88f),
                    new(1.67f, 3.06f, 0.63f), masonry);
                Box(plaza, "Entrance facade wing", new(side * 4.45f, 1.52f, -2.88f), new(4.45f, 3.04f, 0.62f), masonry);
                Box(plaza, "Door jamb", new(side * 2.19f, 1.58f, -3.25f), new(0.14f, 3.16f, 0.27f), steel, false);
                Box(plaza, "Low facade plinth", new(side * 4.41f, 0.39f, -3.18f), new(4.27f, 0.76f, 0.17f), concrete, false);
                for (int bay = 0; bay < 3; bay++)
                {
                    float x = side * (2.96f + bay * 1.21f);
                    Box(plaza, "Dark window recess", new(x, 1.84f, -3.21f), new(1.08f, 1.74f, 0.13f), darkMetal, false);
                    Box(plaza, "Glazed pane", new(x, 1.84f, -3.286f), new(0.92f, 1.47f, 0.038f), window, false);
                    Box(plaza, "Window sash", new(x, 1.37f, -3.315f), new(1.03f, 0.05f, 0.06f), steel, false);
                }
                Box(plaza, "Covered walkway column", new(side * 4.62f, 1.70f, -7.41f), new(0.26f, 3.4f, 0.33f), darkMetal);
                Box(plaza, "Column foot", new(side * 4.62f, 0.25f, -7.41f), new(0.43f, 0.5f, 0.46f), concrete, false);
                Box(plaza, "Retaining wall", new(side * 7.65f, 0.69f, -12.5f), new(0.68f, 1.38f, 19.8f), masonry);
                var perimeter = Box(plaza, "Side movement boundary", new(side * 8.30f, 2.9f, -12.5f),
                    new(0.35f, 5.8f, 22.0f), masonry);
                perimeter.GetComponent<Renderer>().enabled = false;
                Box(plaza, "Planting bed", new(side * 5.58f, 0.23f, -11.75f), new(2.4f, 0.46f, 3.6f), concrete);
                Box(plaza, "Planter soil", new(side * 5.58f, 0.46f, -11.75f), new(2.16f, 0.025f, 3.37f), darkMetal, false);
                AddPlanterShrubs(plaza, side);
                var post = Shape(plaza, PrimitiveType.Cylinder, "Pavement bollard", new(side * 2.33f, 0.42f, -8.96f),
                    new(0.13f, 0.43f, 0.13f), darkMetal);
                Shape(post.parent, PrimitiveType.Cylinder, "Bollard reflection band",
                    new(side * 2.33f, 0.73f, -8.96f), new(0.139f, 0.056f, 0.139f), tactile);
            }
            var rear = Box(plaza, "Street barricade boundary", new(0, 1.75f, -24.45f),
                new(17.0f, 3.5f, 0.3f), masonry);
            rear.GetComponent<Renderer>().enabled = false;
            Box(plaza, "Stainless entrance canopy", new(0, 3.49f, -5.25f), new(10.4f, 0.29f, 4.87f), darkMetal);
            Box(plaza, "Canopy front fascia", new(0, 3.46f, -7.71f), new(10.43f, 0.45f, 0.18f), fascia, false);
            for (int rib = 0; rib < 7; rib++)
                Box(plaza, "Canopy underside joist", new(-4.60f + rib * 1.53f, 3.28f, -5.36f),
                    new(0.10f, 0.18f, 4.46f), steel, false);
            for (int lightIndex = 0; lightIndex < 3; lightIndex++)
                Box(plaza, "Entrance working tube", new(-3.18f + lightIndex * 3.18f, 3.19f, -6.48f),
                    new(1.34f, 0.059f, 0.078f), whiteLamp, false);
            ForecourtSign(plaza, steel);
            AddStreetSilhouette(plaza, masonry, window, whiteLamp, darkMetal);
            DressForecourt(plaza, darkMetal, whiteLamp, redLamp, greenLamp);
            DressCordon(plaza, masonry, window, darkMetal, whiteLamp);
            AddForecourtPractical(plaza);
            BuildStreetPhotography(plaza, masonry, window, darkMetal);

            Spotlight(plaza, "Entrance light pool", new(0f, 3.09f, -6.54f), new(0, 0, -8.6f),
                new Color(1f, 0.78f, 0.48f), 3600, 100f, 15f);
            Spotlight(plaza, "Cold street lights", new(-6.35f, 4.2f, -12.7f), new(-3.4f, 0.12f, -10.5f),
                new Color(0.49f, 0.76f, 0.85f), 5400, 82f, 16f);
            StationLight(plaza, new(2.6f, 2.68f, -7.1f), new Color(1f, 0.70f, 0.42f), 5200, 9.8f);
            StationLight(plaza, new(-2.88f, 2.35f, -4.4f), new Color(0.83f, 0.92f, 0.77f), 3100, 10f);
            StationLight(plaza, new(4.4f, 2.6f, -12.2f), new Color(0.48f, 0.73f, 0.85f), 2200, 12f);
            StationLight(plaza, new(-3.4f, 2.85f, -9.6f), new Color(0.66f, 0.86f, 0.95f), 9600, 13f);
            StationLight(plaza, new(3.3f, 2.90f, -8.4f), new Color(1, 0.77f, 0.51f), 9200, 12f);

        }

        static void AddPlanterShrubs(Transform plaza, int side)
        {
            const string path = "Assets/Art/Models/shrub_02/";
            var leaf = Surface("Forecourt - dry leaves", new Color(0.53f, 0.58f, 0.45f), 0.28f);
            leaf.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "Leaves.png"));
            leaf.SetTexture("_NormalMap", ImportNormal(path + "shrub_02_nor_gl_1k.exr"));
            leaf.SetFloat("_AlphaCutoffEnable", 1f);
            leaf.SetFloat("_AlphaCutoff", 0.43f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.ValidateMaterial(leaf);
            EditorUtility.SetDirty(leaf);
            for (int index = 0; index < 3; index++)
            {
                var planter = Pivot(plaza, "Leaves and branches", new(side * (5.58f + index * 0.13f), 0.48f, -10.9f - index * 0.75f));
                var shrub = ImportModel(path + "shrub_02_1k.fbx", planter, leaf);
                FitModel(shrub, 1.07f + index * 0.20f);
                planter.localRotation = Quaternion.Euler(0, side * (19 + index * 67), 0);
            }
        }

        static void DressForecourt(Transform plaza, Material darkMetal,
            Material bulb, Material red, Material green)
        {
            var barrier = Pivot(plaza, "Forecourt evacuation barricade", new(-2.22f, 0, -11.85f));
            LoadProp("concrete_road_barrier_02", barrier, 0.69f, -18f);
            var collider = barrier.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.34f, 0);
            collider.size = new Vector3(1.42f, 0.68f, 0.48f);
            var notice = Pivot(plaza, "Discarded refuse", new(4.55f, 0, -9.2f));
            LoadProp("trashbag", notice, 0.44f, 33f);
            LoadProp("trashbag", Pivot(plaza, "Discarded refuse", new(5.0f, 0, -9.6f)), 0.37f, -48f);
            LoadProp("painted_wooden_bench", Pivot(plaza, "Outdoor bench", new(-4.85f, 0, -8.76f)), 0.46f, -9f);

            Shape(plaza, PrimitiveType.Cylinder, "Street lamp mast", new(-6.4f, 2.42f, -12.9f),
                new(0.13f, 2.42f, 0.13f), darkMetal);
            PipeBetween(plaza, "Street lamp outreach", new(-6.4f, 4.72f, -12.9f),
                new(-5.88f, 4.72f, -12.25f), 0.055f, darkMetal);
            Box(plaza, "Street lamp diffuser", new(-5.88f, 4.67f, -12.25f), new(0.44f, 0.068f, 0.21f), bulb, false);
            for (int strand = 0; strand < 2; strand++)
            {
                float z = -9.10f - strand * 4.8f;
                for (int index = 0; index < 18; index++)
                {
                    Vector3 a = FestoonPoint(index / 18f, z, strand);
                    Vector3 b = FestoonPoint((index + 1) / 18f, z, strand);
                    PipeBetween(plaza, "Overhead wiring", a, b, 0.014f, darkMetal);
                    if (index % 2 == 0)
                    {
                        Material color = index % 6 == 0 ? red : index % 6 == 2 ? green : bulb;
                        Shape(plaza, PrimitiveType.Sphere, "String light", a - Vector3.up * 0.044f,
                            new(0.049f, 0.075f, 0.048f), color);
                    }
                }
            }
        }

        static void DressCordon(Transform plaza, Material masonry, Material glazing, Material steel, Material light)
        {
            var car = Pivot(plaza, "Abandoned car at the evacuation cordon", new(4.96f, 0, -14.95f));
            var carModel = LoadProp("covered_car", car, 1.50f, -32f);
            var bounds = new Bounds(carModel.transform.position, Vector3.zero);
            foreach (var renderer in carModel.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            // Keep the visual and the collision in the same footprint, with clearance for the central path.
            var obstacle = car.gameObject.AddComponent<BoxCollider>();
            obstacle.center = new Vector3(0, 0.54f, 0);
            obstacle.size = bounds.size.x > bounds.size.z ? new Vector3(3.4f, 1.08f, 1.38f) : new Vector3(1.38f, 1.08f, 3.4f);

            var lamp = Pivot(plaza, "Abandoned rescue searchlight", new(-3.2f, 0, -13.98f));
            LoadProp("portable_searchlight", lamp, 1.38f, 22f);
            LoadProp("portable_generator", Pivot(plaza, "Generator at cordon", new(-4.73f, 0, -15.42f)), 0.63f, -15f);
            LoadProp("plastic_crate_02", Pivot(plaza, "Evacuation equipment crate", new(-3.90f, 0, -14.70f)), 0.33f, -12f);
            LoadProp("plastic_crate_02", Pivot(plaza, "Overturned equipment crate", new(-4.40f, 0, -14.22f)), 0.29f, 63f);
            PipeBetween(plaza, "Generator cable", new(-4.48f, 0.025f, -15.08f), new(-3.21f, 0.025f, -14.02f), 0.024f, steel);
            Spotlight(plaza, "Searchlight across the cordon", new(-3.2f, 1.28f, -13.90f),
                new(0.3f, 0.56f, -10.4f), new Color(1f, 0.89f, 0.71f), 4200f, 78f, 13f);
            StationLight(plaza, new(3.6f, 2.35f, -11.9f), new Color(1f, 0.75f, 0.51f), 3600f, 8.5f);

            var desk = Pivot(plaza, "Open equipment shelter", new(-1.07f, 0, -11.45f));
            Box(desk, "Fabric canopy", new(0, 2.39f, 0), new(2.85f, 0.15f, 2.1f), steel, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(desk, "Canopy front post", new(side * 1.30f, 1.17f, -0.91f), new(0.10f, 2.34f, 0.12f), steel);
                Box(desk, "Canopy rear post", new(side * 1.30f, 1.17f, 0.91f), new(0.10f, 2.34f, 0.12f), steel);
            }
            Box(desk, "Triage worktop", new(0, 0.83f, -0.12f), new(2.36f, 0.13f, 0.72f), steel);
            for (int side = -1; side <= 1; side += 2)
                Box(desk, "Workbench support", new(side * 0.98f, 0.40f, -0.12f),
                    new(0.13f, 0.80f, 0.62f), steel);
            LoadProp("plastic_crate_02", Pivot(desk, "Open equipment crate", new(-0.70f, 0.90f, -0.09f)), 0.28f, -9f);
            LoadProp("medical_box", Pivot(desk, "Medical case on workbench", new(0.24f, 0.90f, -0.13f)), 0.26f, 17f);
            LoadProp("plastic_crate_02", Pivot(plaza, "Equipment ready to be collected", new(-2.62f, 0, -11.94f)), 0.31f, 9f);
            Box(desk, "Shelter light", new(-0.32f, 2.27f, -0.38f), new(1.25f, 0.06f, 0.11f), light, false);
            Spotlight(plaza, "Worktop pool", new(-1.5f, 2.23f, -12f), new(-2.12f, 0.53f, -12.06f),
                new Color(0.75f, 0.94f, 1f), 3550f, 91f, 6.4f);
            Spotlight(plaza, "Warm light following the station walkway", new(3.14f, 3.1f, -8.45f),
                new(2.55f, 0.14f, -13.75f), new Color(1f, 0.76f, 0.47f), 4900f, 58f, 10.5f);

            // Occupied street edge before the skyline: recessed arcade and powered storefront strips.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 10.1f;
                Box(plaza, "Concrete arcade wall", new(x, 2.35f, -4.4f), new(4.1f, 4.7f, 4.2f), masonry, false);
                Box(plaza, "Arcade overhang", new(x, 3.78f, -6.65f), new(4.55f, 0.28f, 1.28f), steel, false);
                Box(plaza, "Closed storefront glazing", new(x, 1.65f, -6.54f), new(3.48f, 2.24f, 0.07f), glazing, false);
                for (int post = -1; post <= 1; post++)
                    Box(plaza, "Storefront mullion", new(x + post * 1.2f, 1.67f, -6.615f),
                        new(0.09f, 2.30f, 0.12f), steel, false);
                Box(plaza, "Powered storefront diffuser", new(x, 3.54f, -6.7f), new(2.6f, 0.10f, 0.13f), light, false);
                StationLight(plaza, new(x, 3.25f, -7.6f), new Color(0.66f, 0.83f, 0.96f), 3500f, 11f).shadows = LightShadows.None;
            }
        }

        static void LayTactileStrip(Transform plaza, Vector3 start, Vector3 end, Material tactile)
        {
            Vector3 axis = end - start;
            var strip = Box(plaza, "Continuous station tactile paving", (start + end) * 0.5f + Vector3.up * 0.013f,
                new(0.37f, 0.022f, axis.magnitude), tactile, false);
            strip.transform.localRotation = Quaternion.LookRotation(axis, Vector3.up);
            for (int index = 0; index < Mathf.FloorToInt(axis.magnitude / 0.43f); index++)
            {
                var rib = Box(plaza, "Tactile paving raised ribs", start + axis.normalized * (index * 0.43f + 0.20f) + Vector3.up * 0.030f,
                    new(0.29f, 0.013f, 0.055f), tactile, false);
                rib.transform.localRotation = strip.transform.localRotation;
            }
        }

        static Vector3 FestoonPoint(float t, float z, int strand) =>
            new(Mathf.Lerp(-7.1f, 7.1f, t), Mathf.Lerp(5.7f, 6.08f, t) - Mathf.Sin(t * Mathf.PI) * (0.92f + strand * 0.18f),
                z + (t - 0.5f) * 2.8f);

        static void PipeBetween(Transform root, string label, Vector3 start, Vector3 end, float diameter, Material material)
        {
            var segment = Shape(root, PrimitiveType.Cylinder, label, (start + end) * 0.5f,
                new(diameter, Vector3.Distance(start, end) * 0.5f, diameter), material);
            segment.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }

        static void AddStreetSilhouette(Transform plaza, Material masonry, Material window,
            Material lit, Material dark)
        {
            for (int index = 0; index < 5; index++)
            {
                int side = index % 2 == 0 ? -1 : 1;
                float x = side * (7.1f + index * 1.81f);
                float height = 7.6f + index % 3 * 2.1f;
                float z = 3.75f + index * 2.14f;
                Box(plaza, "Night apartment silhouette", new(x, height / 2, z), new(4.5f, height, 5.5f), masonry, false);
                for (int floor = 0; floor < 3; floor++)
                for (int bay = 0; bay < 3; bay++)
                {
                    var glass = (floor * 7 + bay * 2 + index) % 8 == 2 ? lit : window;
                    Box(plaza, "Apartment window", new(x - 1.31f + bay * 1.31f, 2.75f + floor * 1.83f, z - 2.77f),
                        new(0.64f, 0.84f, 0.06f), glass, false);
                }
                Box(plaza, "Rooftop equipment", new(x + 0.54f, height + 0.6f, z),
                    new(1.4f, 1.2f, 1.8f), dark, false);
            }
        }

        static void ForecourtSign(Transform plaza, Material backing)
        {
            Vector3 center = new(0, 4.42f, -7.57f);
            Box(plaza, "Enamel station sign backing", center, new(8.78f, 1.99f, 0.28f), backing, false);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "Illuminated original Haerim station lettering";
            plate.transform.SetParent(plaza, false);
            plate.transform.localPosition = center - Vector3.forward * 0.148f;
            plate.transform.localScale = new(8.5f, 1.85f, 1);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            const string materialPath = SettingsFolder + "/Forecourt illuminated sign.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Signs/Forecourt.png");
            material.SetTexture("_UnlitColorMap", image);
            material.SetColor("_UnlitColor", new Color(1.0f, 0.97f, 0.86f));
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            plate.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void InstallInteriorSkyOverride()
        {
            const string indoorPath = SettingsFolder + "/Underground environment.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(indoorPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, indoorPath);
            }
            var environment = StationVolumeSetting<VisualEnvironment>(profile);
            environment.skyType.Override(0);
            EditorUtility.SetDirty(environment);
            var exposure = StationVolumeSetting<Exposure>(profile);
            exposure.fixedExposure.Override(UndergroundExposure);
            EditorUtility.SetDirty(exposure);
            var local = new GameObject("Underground lighting volume", typeof(BoxCollider), typeof(Volume));
            local.transform.position = new Vector3(0, 1.7f, 34f);
            var boundary = local.GetComponent<BoxCollider>();
            boundary.size = new Vector3(13.4f, 6.0f, 40.0f);
            boundary.isTrigger = true;
            var volume = local.GetComponent<Volume>();
            volume.priority = 5;
            volume.blendDistance = 0.45f;
            volume.sharedProfile = profile;
        }

        static void InstallNightSky(VolumeProfile profile)
        {
            const string source = "Assets/Art/Sky/HaerimNight.hdr";
            var importer = (TextureImporter)AssetImporter.GetAtPath(source);
            if (importer.textureShape != TextureImporterShape.TextureCube)
            {
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                importer.SaveAndReimport();
            }
            var environment = StationVolumeSetting<VisualEnvironment>(profile);
            environment.skyType.Override((int)SkyType.HDRI);
            EditorUtility.SetDirty(environment);
            var sky = StationVolumeSetting<HDRISky>(profile);
            sky.hdriSky.Override(AssetDatabase.LoadAssetAtPath<Cubemap>(source));
            sky.rotation.Override(92f);
            sky.exposure.Override(3.7f);
            EditorUtility.SetDirty(sky);
        }
    }
}
