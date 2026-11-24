using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string FreightName = "Haerim freight passage";

        [MenuItem("잔류신호/화물 연결로 적용")]
        public static void InstallFreightPassage()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var root = GameObject.Find("Platform 03").transform;
            var old = root.Find(FreightName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var trial = Object.FindFirstObjectByType<StationTrial>();
            var data = new SerializedObject(trial);
            BuildFreightPassage(root, data);
            data.ApplyModifiedPropertiesWithoutUndo();
            var surface = root.GetComponent<NavMeshSurface>();
            var original = surface.navMeshData;
            surface.BuildNavMesh();
            var rebuilt = surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(rebuilt, original);
            EditorUtility.SetDirty(original);
            surface.navMeshData = original;
            surface.AddData();
            Object.DestroyImmediate(rebuilt);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void BuildFreightPassage(Transform root, SerializedObject mission)
        {
            var oldWall = System.Array.Find(root.GetComponentsInChildren<Transform>(true),
                child => child.name == "Far wall");
            var oldDoor = GameObject.Find("Loading access roll-up door").transform;
            oldWall.gameObject.SetActive(false);
            MarkCarvingGate(oldDoor);
            var passage = Pivot(root, FreightName, Vector3.zero);
            Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/{name}.mat");
            var concrete = Existing("Annex - workshop concrete");
            var wall = Existing("Station - damp repair panels") ?? Existing("Ceiling - cast concrete");
            var steel = Existing("Steel - powder coat");
            var green = Existing("Annex - exit canopy lamps");
            var amber = Existing("Annex - amber equipment lamps");
            Box(passage, "Freight slab", new(-0.05f, -0.24f, 96.4f), new(14f, 0.48f, 55f), concrete);
            Box(passage, "Loading door west jamb", new(-5.20f, 2.2f, 69.06f), new(2.65f, 4.6f, 0.4f), wall);
            Box(passage, "Loading door east jamb", new(3.61f, 2.2f, 69.06f), new(5.65f, 4.6f, 0.4f), wall);
            Box(passage, "Freight hall back wall", new(0, 2.1f, 123.8f), new(14f, 4.6f, 0.4f), wall);
            for (int side = -1; side <= 1; side += 2)
                Box(passage, "Freight exterior wall", new(side * 6.55f, 2.05f, 96.4f),
                    new(0.38f, 4.6f, 54.8f), wall);
            for (int bay = 0; bay < 8; bay++)
            {
                float z = 73f + 6.45f * bay;
                float side = bay % 2 == 0 ? -1f : 1f;
                Box(passage, "Loading lane partition", new(side * 4.65f, 1.38f, z + 1.4f),
                    new(3.0f, 2.76f, 0.65f), wall);
                Box(passage, "Overhead cable beam", new(0f, 4.03f, z), new(13f, 0.27f, 0.29f), steel, false);
                Box(passage, "Freight task light", new(side * 3.7f, 3.85f, z),
                    new(1.75f, 0.085f, 0.13f), bay >= 6 ? green : amber, false);
                StationLight(passage, new(side * 3.7f, 3.68f, z),
                    bay >= 6 ? new Color(0.67f, 0.94f, 0.80f) : new Color(1f, 0.82f, 0.62f),
                    6400f, 10f).shadows = LightShadows.None;
            }
            Box(passage, "Freight gate west jamb", new(-5.18f, 2.04f, 89.2f), new(2.62f, 4.38f, 0.45f), wall);
            Box(passage, "Freight gate east jamb", new(3.64f, 2.04f, 89.2f), new(5.72f, 4.38f, 0.45f), wall);
            var gate = Shutter(passage, "Freight isolation shutter", new(-1.6f, 1.55f, 89.2f), new(4.4f, 3.1f, 0.25f));
            MarkCarvingGate(gate);
            var power = Pivot(passage, "Freight power cabinet", new(2.45f, 0, 84.5f));
            var release = Pivot(passage, "Service exit release", new(-4.2f, 0, 103.15f));
            LoadProp("utility_box_01", power, 1.18f, 0);
            LoadProp("utility_box_01", release, 1.18f, 35f);
            Box(power, "Power service lamp", new(0f, 1.1f, -0.25f), new(0.2f, 0.11f, 0.04f), amber, false);
            Box(release, "Release service lamp", new(0f, 1.1f, -0.25f), new(0.2f, 0.11f, 0.04f), green, false);
            LoadProp("concrete_road_barrier_02", Pivot(passage, "Freight shelter divider", new(3.65f, 0f, 76.6f)), 0.81f, 25f);
            LoadProp("covered_car", Pivot(passage, "Abandoned freight transport", new(3.80f, 0, 109.2f)), 1.40f, 28f);
            LoadProp("ammo_box", Pivot(passage, "Freight ammo reserves", new(1.90f, 0, 83.28f)), 0.28f, 14f)
                .AddComponent<LootItem>().kind = LootItem.Kind.Ammo;
            LoadProp("medical_box", Pivot(passage, "Freight repair kit", new(-3.37f, 0, 101.45f)), 0.24f, 14f)
                .AddComponent<LootItem>().kind = LootItem.Kind.Medkit;
            Box(passage, "Service exit lintel", new(-1.6f, 3.04f, 122.35f), new(4.2f, 0.13f, 0.28f), green, false);
            var exit = Pivot(passage, "Freight service exit", new(-1.6f, 0, 121.1f));
            mission.FindProperty("freightDoor").objectReferenceValue = oldDoor;
            mission.FindProperty("freightPower").objectReferenceValue = power;
            mission.FindProperty("freightGate").objectReferenceValue = gate;
            mission.FindProperty("freightRelease").objectReferenceValue = release;
            mission.FindProperty("freightExit").objectReferenceValue = exit;
        }

        static void MarkCarvingGate(Transform gate)
        {
            var obstacle = gate.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
                obstacle = gate.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = gate.GetComponent<BoxCollider>().size;
        }
    }
}
