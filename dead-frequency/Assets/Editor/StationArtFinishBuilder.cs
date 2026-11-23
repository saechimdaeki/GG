using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        static void FinishStationArchitecture(Transform root, Material concrete, Material steel,
            Material warning)
        {
            var aluminum = Surface("Platform - brushed frame", new Color(0.39f, 0.40f, 0.34f), 0.57f);
            aluminum.SetFloat("_Metallic", 0.46f);
            EditorUtility.SetDirty(aluminum);
            var rubber = Surface("Platform - glazing gasket", new Color(0.077f, 0.080f, 0.074f), 0.25f);
            var glass = Surface("Platform - track glazing", new Color(0.49f, 0.55f, 0.52f, 0.20f), 0.86f);
            glass.SetFloat("_SurfaceType", 1);
            glass.SetFloat("_DoubleSidedEnable", 1);
            HDMaterial.ValidateMaterial(glass);
            EditorUtility.SetDirty(glass);
            var rail = Surface("Platform - service rail", new Color(0.25f, 0.24f, 0.19f), 0.66f);
            rail.SetFloat("_Metallic", 0.62f);
            EditorUtility.SetDirty(rail);
            var rust = Surface("Platform - rail bed stain", new Color(0.33f, 0.26f, 0.17f), 0.24f, new(2, 20), maps: "Concrete");
            var copper = Surface("Platform - conduit brown", new Color(0.26f, 0.19f, 0.12f), 0.34f);
            var lightTrim = Surface("Platform - lamp milk glass", new Color(0.92f, 0.91f, 0.76f), 0.28f, emission: 68f);
            var warningRed = Surface("Platform - emergency beacon red", new Color(0.91f, 0.26f, 0.075f), 0.35f, emission: 36f);

            Box(root, "Track bed", new(-6.52f, -0.54f, 17), new(1.50f, 0.20f, 40), rust, false);
            for (int side = 0; side < 2; side++)
                Box(root, "Track running rail", new(-6.26f - side * 0.58f, -0.38f, 17),
                    new(0.075f, 0.13f, 40), rail, false);
            for (int sleeper = 0; sleeper < 28; sleeper++)
                Box(root, "Track sleeper", new(-6.54f, -0.46f, 0.3f + sleeper * 1.33f),
                    new(1.31f, 0.12f, 0.18f), rust, false);
            var trackBarrier = Box(root, "Platform screen door collision", new(-5.63f, 1.55f, 17),
                new(0.14f, 3.1f, 40f), steel);
            trackBarrier.GetComponent<Renderer>().enabled = false;
            for (int bay = 0; bay < 10; bay++)
            {
                float z = -1.35f + 3.86f * bay;
                Box(root, "Door motor housing", new(-5.62f, 2.91f, z), new(0.28f, 0.30f, 3.72f), aluminum, false);
                Box(root, "Black guide channel", new(-5.47f, 2.81f, z), new(0.03f, 0.055f, 3.61f), rubber, false);
                Box(root, "Sill and closed tracks", new(-5.60f, 0.075f, z), new(0.34f, 0.15f, 3.72f), aluminum, false);
                for (int leaf = -1; leaf <= 1; leaf += 2)
                {
                    float center = z + leaf * 0.88f;
                    Box(root, "Glass leaf", new(-5.61f, 1.55f, center), new(0.016f, 2.45f, 1.70f), glass, false);
                    Box(root, "Glazed door kickplate", new(-5.58f, 0.30f, center), new(0.09f, 0.35f, 1.70f), steel, false);
                    Box(root, "Caution ribbon", new(-5.57f, 1.07f, center), new(0.040f, 0.065f, 1.69f), warning, false);
                    Box(root, "Rubber door seal", new(-5.60f, 1.50f, center + leaf * 0.84f),
                        new(0.09f, 2.72f, 0.061f), rubber, false);
                    Box(root, "Extruded aluminum jamb", new(-5.59f, 1.50f, center - leaf * 0.83f),
                        new(0.12f, 2.70f, 0.056f), aluminum, false);
                }
                if (bay % 2 == 0)
                    Box(root, "Door position lamp", new(-5.46f, 2.91f, z + 0.57f), new(0.02f, 0.043f, 0.17f),
                        warningRed, false);
            }

            for (int run = 0; run < 3; run++)
            {
                float x = 4.14f + run * 0.185f;
                var pipe = Shape(root, PrimitiveType.Cylinder, "Long ceiling conduit", new(x, 3.70f + run * 0.09f, 18f),
                    new(0.095f, 18.5f, 0.095f), run == 1 ? copper : aluminum);
                pipe.localRotation = Quaternion.Euler(90, 0, 0);
                for (int clamp = 0; clamp < 12; clamp++)
                {
                    var hanger = Shape(root, PrimitiveType.Cylinder, "Conduit saddle",
                        new(x, 3.70f + run * 0.09f, 0.3f + clamp * 3.2f), new(0.132f, 0.035f, 0.132f), steel);
                    hanger.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }

            float[] benchLocations = { 12.6f, 19.8f, 23.3f };
            for (int index = 0; index < benchLocations.Length; index++)
            {
                var bench = Pivot(root, "Worn station bench", new(5.18f, 0, benchLocations[index]));
                LoadProp("painted_wooden_bench", bench, 0.47f, index == 1 ? -81f : -90f);
            }

            AddFireEquipment(root);
            Box(root, "Sodium service fixture", new(2.57f, 3.56f, 14.15f), new(0.52f, 0.25f, 0.31f), steel, false);
            Box(root, "Sodium service diffuser", new(2.57f, 3.41f, 14.15f), new(0.43f, 0.07f, 0.23f), lightTrim, false);
            Spotlight(root, "Amber cabinet key", new(2.57f, 3.35f, 14.15f), new(3.75f, 0.5f, 17.4f),
                new Color(1, 0.49f, 0.21f), 2350, 82, 12);
            Spotlight(root, "Platform backlight", new(-4.8f, 3.70f, 25.75f), new(-0.65f, 0.2f, 22.4f),
                new Color(0.59f, 0.95f, 0.82f), 1150, 75, 13);
            Spotlight(root, "Red crossover warning", new(-3.8f, 3.62f, 10.9f), new(-3.05f, 0.04f, 13.25f),
                new Color(1, 0.28f, 0.13f), 410, 66, 10);
            Spotlight(root, "Entrance fluorescents", new(-0.2f, 3.65f, 2.6f), new(-2f, 0.2f, 3.8f),
                new Color(0.87f, 1f, 0.84f), 430, 94, 10);
            StationLight(root, new(3.4f, 2.45f, 15.30f), new Color(1f, 0.61f, 0.33f), 4200, 5.5f);
            StationLight(root, new(-3.7f, 0.95f, 23.2f), new Color(0.44f, 0.84f, 0.67f), 3000, 7f);
            Spotlight(root, "Stalled evacuation beam", new(0.2f, 3.64f, 29.1f), new(0.1f, 0.2f, 25.5f),
                new Color(0.77f, 0.94f, 0.87f), 390, 72, 11);
        }

        static void AddFireEquipment(Transform root)
        {
            var alarm = Pivot(root, "Platform alarm", new(5.43f, 1.64f, 16.9f));
            LoadProp("fire_alarm", alarm, 0.31f, -90f);
            var cabinet = Pivot(root, "Korean fire extinguisher", new(5.32f, 0, 17.52f));
            const string path = "Assets/Art/Models/korean_fire_extinguisher_01/korean_fire_extinguisher_01";
            var red = Surface("Fire extinguisher - PBR body", Color.white, 0.51f);
            red.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_body_diff_1k.png"));
            red.SetTexture("_NormalMap", ImportNormal(path + "_body_nor_gl_1k.png"));
            HDMaterial.ValidateMaterial(red);
            EditorUtility.SetDirty(red);
            var model = ImportModel(path + "_1k.fbx", cabinet, red);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string piece = renderer.name.ToLowerInvariant();
                string channel = piece.Contains("paper") ? "paper" : piece.Contains("glass") ? "glass" : null;
                if (channel == null)
                    continue;
                var detail = Surface("Fire extinguisher - " + channel, Color.white, 0.43f);
                detail.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + $"_{channel}_diff_1k.png"));
                detail.SetTexture("_NormalMap", ImportNormal(path + $"_{channel}_nor_gl_1k.png"));
                HDMaterial.ValidateMaterial(detail);
                EditorUtility.SetDirty(detail);
                renderer.sharedMaterial = detail;
            }
            FitModel(model, 0.68f);
            cabinet.localRotation = Quaternion.Euler(0, 34, 0);
        }

        static void Spotlight(Transform root, string name, Vector3 position, Vector3 target, Color color,
            float candela, float angle, float range)
        {
            var source = new GameObject(name);
            source.AddHDLight(LightType.Spot);
            var light = source.GetComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            light.type = LightType.Spot;
            light.lightUnit = LightUnit.Candela;
            light.intensity = candela;
            light.color = color;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.44f;
            light.shadows = LightShadows.Soft;
        }
    }
}
