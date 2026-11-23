using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace ResidualSignal
{
    /// <summary>Scavengeable ammo and first-aid kits, and the walkable navmesh the bots path on.</summary>
    public static partial class StationSceneBuilder
    {
        static readonly (LootItem.Kind kind, Vector3 point)[] LootSpots =
        {
            (LootItem.Kind.Ammo, new(-3.3f, 0, -12.0f)), (LootItem.Kind.Medkit, new(3.6f, 0, -13.5f)),
            (LootItem.Kind.Ammo, new(1.0f, 0, 3.6f)), (LootItem.Kind.Medkit, new(1.5f, 0, 7.5f)),
            (LootItem.Kind.Ammo, new(-3.5f, 0, 19.9f)), (LootItem.Kind.Ammo, new(3.5f, 0, 23.2f)),
            (LootItem.Kind.Medkit, new(-2.6f, 0, 19.9f)), (LootItem.Kind.Medkit, new(2.6f, 0, 23.2f)),
            (LootItem.Kind.Medkit, new(-2.5f, 0, 27f)), (LootItem.Kind.Ammo, new(0.6f, 0, 32.6f)),
        };

        static void PlaceLoot(Transform root)
        {
            int index = 0;
            foreach (var (kind, point) in LootSpots)
            {
                var spot = Pivot(root, kind == LootItem.Kind.Ammo ? "Ammo box" : "First-aid kit", point);
                LoadProp(kind == LootItem.Kind.Ammo ? "ammo_box" : "medical_box", spot, kind == LootItem.Kind.Ammo ? 0.26f : 0.22f, index++ * 47f);
                spot.gameObject.AddComponent<LootItem>().kind = kind;
            }
        }

        /// <summary>Bake walkable ground from colliders (player excluded); gates and the shutter carve it while closed.</summary>
        static void BakeNavMesh(Transform root, GameObject player)
        {
            var blockers = new System.Collections.Generic.List<GameObject>();
            foreach (var name in new[] { "Drain gate", "Staff gate", "Security shutter", "Service transfer shutter" })
            {
                var blocker = GameObject.Find(name);
                if (blocker == null)
                    continue;
                var obstacle = blocker.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = blocker.GetComponent<BoxCollider>().size;
                blockers.Add(blocker);
                blocker.SetActive(false); // bake the doorway open; the obstacle closes it at runtime
            }
            player.SetActive(false);
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            const string path = "Assets/Scenes/Platform03NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            player.SetActive(true);
            foreach (var blocker in blockers)
                blocker.SetActive(true);
        }
    }
}
