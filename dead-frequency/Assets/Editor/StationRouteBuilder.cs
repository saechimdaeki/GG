using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        static void CreateRoutes(Transform root, SerializedObject mission, Camera camera)
        {
            var wall = Surface("Route - concrete", new Color(0.69f, 0.67f, 0.58f), 0.33f, new(2, 2), maps: "Concrete");
            var steel = Surface("Route - dark steel", new Color(0.24f, 0.28f, 0.26f), 0.53f);
            var oil = Surface("Route - standing water", new Color(0.19f, 0.24f, 0.23f), 0.99f, new(2, 4), maps: "Floor");
            var teal = Surface("Route - maintenance green", new Color(0.25f, 0.48f, 0.37f), 0.36f);
            var led = Surface("Route - selector lamp", new Color(0.96f, 0.59f, 0.19f), 0.36f, emission: 45f);
            var emergency = Surface("Route - emergency red", new Color(0.83f, 0.26f, 0.14f), 0.36f, emission: 33f);
            var white = Surface("Route - lettering paint", new Color(0.79f, 0.82f, 0.74f), 0.42f);
            Box(root, "Transfer control block", new(0, 2.01f, 10.3f), new(2.24f, 4.02f, 6.4f), wall);
            Box(root, "Transfer control fascia", new(0, 2.68f, 7.035f), new(2.30f, 0.46f, 0.11f), teal, false);
            Box(root, "Transfer distribution panel", new(0, 1.18f, 7.045f), new(0.76f, 1.23f, 0.15f), steel, false);
            Box(root, "Utility conduit", new(0.43f, 2.8f, 6.99f), new(0.10f, 2.24f, 0.12f), steel, false);
            Box(root, "Utility conduit", new(0.68f, 2.6f, 6.99f), new(0.08f, 2.56f, 0.12f), steel, false);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 3.40f;
                Box(root, "Route lintel", new(x, 3.30f, 8.5f), new(4.57f, 1.43f, 0.34f), wall);
                Box(root, "Selector light", new(x, 2.59f, 8.25f), new(0.55f, 0.06f, 0.08f),
                    side < 0 ? emergency : led, false);
            }
            Box(root, "Seepage in front of drain gate", new(-3.35f, 0.012f, 7.89f),
                new(3.3f, 0.012f, 1.1f), oil, false);
            LoadProp("plastic_crate_02", Pivot(root, "Staff gear beside doorway", new(4.83f, 0, 7.67f)), 0.43f, -8f);
            StationLight(root, new(-4.63f, 2.1f, 7.96f), new Color(0.42f, 0.69f, 0.81f), 1450, 4.8f);
            StationLight(root, new(4.55f, 2.47f, 7.74f), new Color(0.97f, 0.87f, 0.65f), 2450, 4.8f);
            var drain = Shutter(root, "Drain gate", new(-3.40f, 1.29f, 8.50f), new(4.6f, 2.58f, 0.24f));
            var staff = Shutter(root, "Staff gate", new(3.40f, 1.29f, 8.50f), new(4.6f, 2.58f, 0.24f));
            Box(root, "Water over bypass", new(-3.34f, 0.008f, 11.3f), new(4.35f, 0.014f, 5.4f), oil, false);
            Box(root, "Raised drainage curb", new(-1.32f, 0.09f, 11.18f), new(0.22f, 0.18f, 5.0f), wall, false);
            StationLight(root, new(-4.3f, 1.95f, 11.13f), new Color(1f, 0.52f, 0.34f), 1350, 8f);
            StationLight(root, new(4.55f, 2.41f, 11.0f), new Color(0.78f, 0.93f, 0.80f), 1650, 8f);

            Box(root, "Evacuation ticket counter", new(4.75f, 0.52f, 1.12f), new(1.45f, 1.04f, 2.06f), teal);
            Box(root, "Counter steel ledge", new(4.69f, 1.06f, 1.12f), new(1.61f, 0.07f, 2.15f), steel, false);
            Box(root, "Closed ticket grille", new(5.56f, 1.78f, 1.12f), new(0.10f, 1.48f, 1.75f), steel, false);
            for (int slat = 0; slat < 10; slat++)
                Box(root, "Ticket grille rib", new(5.50f, 1.13f + slat * 0.14f, 1.12f),
                    new(0.075f, 0.035f, 1.75f), teal, false);
            for (int row = 0; row < 2; row++)
            {
                float z = 16.25f + row * 8.95f;
                Box(root, "Platform column", new(-1.20f, 2.0f, z), new(0.52f, 4f, 0.61f), wall);
                Box(root, "Platform column sleeve", new(-1.2f, 0.52f, z), new(0.575f, 1.04f, 0.67f), teal, false);
            }
            var chair = Pivot(root, "Abandoned rescue wheelchair", new(3.4f, 0, 34.0f));
            LoadProp("wheelchair_01", chair, 1.08f, -38f);
            var generator = Pivot(root, "Emergency power generator", new(4.76f, 0, 18.45f));
            LoadProp("portable_generator", generator, 0.73f, 18f);
            Box(root, "Fallback supply pallet", new(3.82f, 0.13f, 21.5f), new(1.19f, 0.26f, 0.95f), steel);
            Box(root, "Stacked evacuation case", new(3.92f, 0.49f, 21.5f), new(1.02f, 0.45f, 0.79f), teal);
            Box(root, "Stacked evacuation case lid", new(3.92f, 0.73f, 21.5f), new(1.06f, 0.08f, 0.81f), steel, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(root, "Evacuation stripe", new(side * 1.82f, 0.012f, 32.2f), new(0.13f, 0.023f, 5.7f), white, false);
                Box(root, "Lift frame", new(-1.6f + side * 1.04f, 1.52f, 36.4f), new(0.15f, 3.04f, 0.19f), teal, false);
            }
            Sign(root, "Exit", new(-1.6f, 2.77f, 36.22f), 1.55f, steel);

            var torchNode = new GameObject("Rescue flashlight");
            torchNode.AddHDLight(LightType.Spot);
            var torch = torchNode.GetComponent<Light>();
            torch.transform.SetParent(camera.transform, false);
            torch.transform.localPosition = new Vector3(0.2f, -0.06f, 0.78f); // ahead of the hands so the gloves stay unlit
            torch.type = LightType.Spot;
            torch.lightUnit = LightUnit.Candela;
            torch.intensity = 480;
            torch.color = new Color(0.93f, 0.96f, 1f);
            torch.range = 17f;
            torch.spotAngle = 52f;
            torch.innerSpotAngle = 24f;
            torch.shadows = LightShadows.Soft;
            torch.enabled = true;

            var ambience = root.gameObject.AddComponent<AudioSource>();
            ambience.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/StationAmbience.wav");
            ambience.loop = true;
            ambience.playOnAwake = true;
            ambience.spatialBlend = 0f;
            ambience.volume = 0.44f;
            mission.FindProperty("stepSound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/StationStep.wav");
            mission.FindProperty("drainGate").objectReferenceValue = drain;
            mission.FindProperty("staffGate").objectReferenceValue = staff;
            mission.FindProperty("flashlight").objectReferenceValue = torch;
        }

        static GameObject LoadProp(string asset, Transform marker, float height, float rotation)
        {
            var model = ImportedStationProp(asset, marker);
            FitModel(model, height);
            marker.localRotation = Quaternion.Euler(0, rotation, 0);
            return model;
        }

        static GameObject ImportedStationProp(string asset, Transform parent)
        {
            string path = $"Assets/Art/Models/{asset}/{asset}";
            var material = Surface("Imported - " + asset, new Color(0.91f, 0.91f, 0.88f), 0.42f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_diff_1k.jpg"));
            if (asset == "rollershutter_door")
            {
                material.SetColor("_BaseColor", new Color(0.49f, 0.53f, 0.52f));
                material.SetFloat("_Metallic", 0.38f);
            }
            material.SetTexture("_NormalMap", ImportNormal(path + "_nor_gl_1k.exr"));
            material.SetFloat("_NormalScale", 0.72f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return ImportModel(path + "_1k.fbx", parent, material);
        }

        static Transform Shutter(Transform root, string name, Vector3 position, Vector3 opening)
        {
            var panel = Pivot(root, name, position);
            panel.gameObject.AddComponent<BoxCollider>().size = opening;
            var model = ImportedStationProp("rollershutter_door", panel);
            var renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.name.Contains("graffiti", System.StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;
            }
            Bounds bounds = ShutterBounds(renderers);
            if (bounds.size.z > bounds.size.x)
            {
                model.transform.localRotation = Quaternion.Euler(0, 90, 0);
                bounds = ShutterBounds(renderers);
            }
            Vector3 resize = new(opening.x / bounds.size.x, opening.y / bounds.size.y,
                opening.z / Mathf.Max(bounds.size.z, 0.03f));
            var sizing = Pivot(panel, "Shutter fit", Vector3.zero);
            model.transform.SetParent(sizing, false);
            sizing.localScale = resize;
            sizing.localPosition = Vector3.Scale(position - bounds.center, resize);
            return panel;
        }

        static Bounds ShutterBounds(Renderer[] renderers)
        {
            Bounds total = default;
            bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                    continue;
                if (found)
                    total.Encapsulate(renderer.bounds);
                else
                    total = renderer.bounds;
                found = true;
            }
            return total;
        }

    }
}
