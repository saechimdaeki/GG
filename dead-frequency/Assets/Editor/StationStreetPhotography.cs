using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string StreetPhotographyName = "Haerim street depth and practical lighting";

        [MenuItem("잔류신호/야간 교전 조명 갱신")]
        public static void UpdateNightArt()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var plaza = GameObject.Find("Haerim station night forecourt").transform;
            var previous = plaza.Find(StreetPhotographyName);
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);
            Material Existing(string asset) => AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/{asset}.mat");
            BuildStreetPhotography(plaza, Existing("Forecourt - soot stained facade"),
                Existing("Forecourt - dark ticket glass"), Existing("Forecourt - oxidized dark steel"));

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{SettingsFolder}/StationVolume.asset");
            if (profile.TryGet(out HDRISky sky))
            {
                sky.exposure.Override(3.7f);
                EditorUtility.SetDirty(sky);
            }
            ApplyCinematicLook(profile);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void BuildStreetPhotography(Transform plaza, Material masonry, Material glazing, Material metal)
        {
            var street = Pivot(plaza, StreetPhotographyName, Vector3.zero);
            // The apartment blocks sit behind both sides of the forecourt and never affect the NavMesh.
            BuildApartment(street, new(-9.6f, 0f, 0.8f), 5, 3, true, masonry, glazing, metal);
            BuildApartment(street, new(11.5f, 0f, 2.9f), 4, 4, false, masonry, glazing, metal);
            BuildApartment(street, new(-15.6f, 0f, 10.8f), 7, 4, true, masonry, glazing, metal);
            BuildApartment(street, new(15.4f, 0f, 14.7f), 6, 3, false, masonry, glazing, metal);

            // Localized lighting keeps the street dark while revealing faces and moving limbs at the entrance.
            Spotlight(street, "Weathered canopy fluorescent spill", new(-3.85f, 3.42f, -4.8f),
                new(-0.75f, 1.30f, -8.6f), new Color(0.81f, 0.89f, 0.87f), 3600f, 76f, 11f);
            Spotlight(street, "Across the entrance pavement", new(4.0f, 2.91f, -8.4f),
                new(0.35f, 1.38f, -4.55f), new Color(1f, 0.83f, 0.65f), 2850f, 75f, 10f);
            Spotlight(street, "Apartment sodium spill", new(-8.7f, 5.2f, -6.0f),
                new(-10.4f, 7.3f, -0.98f), new Color(1f, 0.73f, 0.48f), 7200f, 70f, 15f);
            Spotlight(street, "Reflected street light on the right frontage", new(9.6f, 5.4f, -3.8f),
                new(11.3f, 6.3f, 1.10f), new Color(0.68f, 0.80f, 0.84f), 2400f, 78f, 13f);
            Spotlight(street, "Ticket hall fluorescent falloff", new(2.65f, 3.02f, 1.2f),
                new(-0.8f, 1.20f, 5.3f), new Color(0.86f, 0.94f, 0.88f), 2550f, 68f, 10f);
            Spotlight(street, "Defence aisle fluorescent spill", new(1.9f, 3.46f, 20.8f),
                new(-0.65f, 1.33f, 16.9f), new Color(0.84f, 0.92f, 0.89f), 3950f, 83f, 11f);
            Spotlight(street, "Defence return path bounce", new(-3.3f, 2.85f, 16.1f),
                new(-1.45f, 1.32f, 19.95f), new Color(1f, 0.87f, 0.71f), 2450f, 84f, 9f);
            Spotlight(street, "Workshop side of the maintenance gate", new(-2.1f, 3.16f, 44.25f),
                new(0.1f, 1.38f, 49.3f), new Color(0.79f, 0.87f, 0.90f), 3400f, 76f, 12f);
            Spotlight(street, "Loading bay reflected exit light", new(2.4f, 3.22f, 63.2f),
                new(-0.65f, 1.38f, 59.05f), new Color(1f, 0.87f, 0.72f), 2650f, 74f, 11f);
        }

        static void BuildApartment(Transform street, Vector3 origin, int floors, int bays, bool hasBalconies,
            Material concrete, Material glass, Material metal)
        {
            var block = Pivot(street, "Haerim residential frontage", origin);
            const float floorHeight = 2.45f;
            const float bayWidth = 1.54f;
            float width = bays * bayWidth + 0.6f;
            float height = floors * floorHeight;
            const float face = -1.72f;
            Box(block, "Textured apartment shell", new(0f, height * 0.5f, 0f),
                new(width, height, 3.44f), concrete, false);
            Box(block, "Concrete roof coping", new(0f, height + 0.12f, -0.03f),
                new(width + 0.23f, 0.27f, 3.63f), concrete, false);
            Box(block, "Rooftop service housing", new(-width * 0.24f, height + 0.58f, 0.44f),
                new(1.36f, 1.16f, 1.04f), metal, false);
            Box(block, "Vertical storm drain", new(width * 0.46f, height * 0.49f, face - 0.14f),
                new(0.095f, height * 0.98f, 0.10f), metal, false);

            for (int floor = 0; floor < floors; floor++)
            {
                float sill = 0.86f + floor * floorHeight;
                for (int bay = 0; bay < bays; bay++)
                {
                    float x = (bay - (bays - 1) * 0.5f) * bayWidth;
                    Box(block, "Recessed window frame", new(x, sill + 0.58f, face - 0.035f),
                        new(1.07f, 1.17f, 0.09f), metal, false);
                    Box(block, "Weathered glazing", new(x, sill + 0.59f, face - 0.09f),
                        new(0.91f, 1.01f, 0.022f), glass, false);
                    Box(block, "Window mullion", new(x + 0.14f, sill + 0.58f, face - 0.11f),
                        new(0.052f, 1.07f, 0.038f), metal, false);
                    if (!hasBalconies || floor == 0 || (bay + floor) % 3 != 0)
                        continue;
                    Box(block, "Service balcony slab", new(x, sill - 0.1f, face - 0.36f),
                        new(1.33f, 0.19f, 0.86f), concrete, false);
                    Box(block, "Service balcony railing", new(x, sill + 0.35f, face - 0.77f),
                        new(1.36f, 0.76f, 0.063f), metal, false);
                }
            }
        }
    }
}
