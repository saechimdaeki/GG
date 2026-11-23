using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        static void BuildServiceYard(Transform root, SerializedObject game, Material tile, Material concrete, Material steel)
        {
            var yard = Pivot(root, "Maintenance annex and loading bay", Vector3.zero);
            var amber = Surface("Annex - amber equipment lamps", new Color(1f, 0.57f, 0.24f), 0.38f, emission: 62f);
            var green = Surface("Annex - exit canopy lamps", new Color(0.51f, 0.95f, 0.73f), 0.42f, emission: 72f);
            var floor = Surface("Annex - workshop concrete", new Color(0.62f, 0.63f, 0.58f), 0.58f, new(9, 20), maps: "Concrete");
            Box(yard, "Annex floor", new(0, -0.24f, 53f), new(13.5f, 0.48f, 32.4f), floor);
            Box(yard, "Annex overhead slab", new(0, 4.65f, 53f), new(13.5f, 0.4f, 32.4f), concrete);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(yard, "Annex outer wall", new(side * 6.56f, 2.1f, 53f), new(0.35f, 4.7f, 32.5f), concrete);
                for (int bay = 0; bay < 5; bay++)
                {
                    float z = 40f + 5.8f * bay;
                    Box(yard, "Annex wall rib", new(side * 6.28f, 2.2f, z), new(0.33f, 4.4f, 0.49f), steel);
                    Box(yard, "Annex ceiling batten", new(side * 3.2f, 4.31f, z), new(1.8f, 0.11f, 0.15f), bay == 4 ? green : amber, false);
                    StationLight(yard, new(side * 3.2f, 4.18f, z),
                        bay == 4 ? new(0.58f, 0.93f, 0.84f) : new(0.95f, 0.86f, 0.71f), 5100f, 9f).shadows = LightShadows.None;
                }
            }
            Box(yard, "Service lobby left wall", new(-4.76f, 2f, 36.82f), new(3.42f, 4.0f, 0.35f), concrete);
            Box(yard, "Service lobby right wall", new(3.25f, 2f, 36.82f), new(6.52f, 4.0f, 0.35f), concrete);
            Box(yard, "Pump workshop divider", new(0.92f, 2.2f, 44.8f), new(1.2f, 4.4f, 5.3f), concrete);
            Box(yard, "Loading bay stack wall", new(2.2f, 1.55f, 58.8f), new(2.6f, 3.1f, 1.1f), concrete);
            Box(yard, "Opaque maintenance screen hiding mutant", new(2.1f, 1.65f, 61.25f),
                new(4.15f, 3.3f, 0.44f), concrete);
            Box(yard, "Far wall", new(0, 2.2f, 69.06f), new(13.5f, 4.6f, 0.38f), concrete);
            Box(yard, "Dry service footpath", new(-1.95f, 0.008f, 53f), new(0.38f, 0.014f, 32f), tile, false);
            Box(yard, "Transfer wall left", new(-5.18f, 2.25f, 52.4f), new(2.75f, 4.5f, 0.45f), concrete);
            Box(yard, "Transfer wall right", new(3.10f, 2.25f, 52.4f), new(6.9f, 4.5f, 0.45f), concrete);
            Box(yard, "Transfer header", new(-2.1f, 3.91f, 52.4f), new(3.45f, 1.22f, 0.45f), concrete);
            var shutter = Shutter(yard, "Service transfer shutter", new(-2.1f, 1.64f, 52.4f), new(3.45f, 3.28f, 0.25f));

            var pump = Pivot(yard, "Hydraulic pump cabinet", new(3.15f, 0, 48.4f));
            LoadProp("utility_box_01", pump, 1.30f, 0f);
            LoadProp("portable_generator", Pivot(yard, "Pump generator", new(4.18f, 0, 48.65f)), 0.68f, -18f);
            Box(pump, "Pump amber lamp", new(0, 1.04f, -0.27f), new(0.18f, 0.11f, 0.03f), amber, false);
            Spotlight(yard, "Pump cabinet spotlight", new(2.3f, 3.18f, 46.8f), new(3.15f, 0.9f, 48.4f),
                new(1f, 0.72f, 0.38f), 3600f, 56f, 10f);
            LoadProp("plastic_crate_02", Pivot(yard, "Workshop supply crate", new(-4.4f, 0, 43.5f)), 0.45f, 16f);
            LoadProp("medical_box", Pivot(yard, "Loading bay first-aid kit", new(4.8f, 0, 65.5f)), 0.23f, -12f)
                .AddComponent<LootItem>().kind = LootItem.Kind.Medkit;
            LoadProp("ammo_box", Pivot(yard, "Workshop ammo", new(-3.35f, 0, 47.8f)), 0.25f, 18f)
                .AddComponent<LootItem>().kind = LootItem.Kind.Ammo;
            LoadProp("concrete_road_barrier_02", Pivot(yard, "Loading bay cover", new(4.45f, 0, 61.95f)), 0.72f, -8f);
            LoadProp("portable_searchlight", Pivot(yard, "Loading zone floodlight", new(3.3f, 0, 65.45f)), 1.14f, 15f);
            Box(yard, "Covered pickup awning", new(-1.6f, 3.15f, 67.9f), new(5.1f, 0.2f, 2.1f), steel, false);
            Box(yard, "Pickup light pipe", new(-1.6f, 3.0f, 67.08f), new(2.8f, 0.11f, 0.17f), green, false);
            StationLight(yard, new(-1.6f, 2.9f, 67.0f), new(0.60f, 0.92f, 0.76f), 10000f, 11f);
            Spotlight(yard, "Loading bay neutral face light", new(-3.2f, 3.3f, 61.0f), new(0.4f, 1.2f, 65.8f),
                new Color(1f, 0.90f, 0.79f), 5450f, 73f, 12f);
            var exitDoor = Shutter(yard, "Loading access roll-up door", new(-1.6f, 1.43f, 68.75f), new(4.42f, 2.86f, 0.24f));
            Box(exitDoor, "Door surrounding amber strip", new(-2.28f, 0.03f, -0.18f), new(0.11f, 2.85f, 0.11f), amber, false);
            Box(exitDoor, "Door surrounding green strip", new(2.28f, 0.03f, -0.18f), new(0.11f, 2.85f, 0.11f), green, false);
            LoadProp("covered_car", Pivot(yard, "Emergency transport under a sheet", new(3.6f, 0, 65.60f)), 1.45f, -18f);
            var destination = Pivot(yard, "Loading bay extraction", new(-1.6f, 0, 66.8f));
            game.FindProperty("servicePump").objectReferenceValue = pump;
            game.FindProperty("serviceShutter").objectReferenceValue = shutter;
            game.FindProperty("loadingExtraction").objectReferenceValue = destination;
        }
    }
}
