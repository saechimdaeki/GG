using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Replaces the look of the plain grey/green box stand-ins (desks, gun tables, counters, crate stacks) with
    /// Poly Haven CC0 models filling the same footprint. The box keeps its collider, so cover, navigation and
    /// anything resting on top stay exactly where they were; only its renderer is switched off.
    /// </summary>
    public static partial class StationSceneBuilder
    {
        // box name -> model, cells across (x or z long axis), cells high
        static readonly (string box, string model, int across, int high)[] PropSwaps =
        {
            ("Station desk", "metal_office_desk", 1, 1),
            ("Carbine table", "wooden_table_02", 1, 1),
            ("Rack table", "wooden_table_02", 1, 1),
            ("Evacuation ticket counter", "drawer_cabinet", 2, 1),
            ("Stacked evacuation case", "wooden_military_crate", 1, 2),
            ("Platform column sleeve", "wooden_military_crate", 1, 2),
            ("Loading bay stack wall", "wooden_crate_01", 2, 3),
        };

        static int SwapBoxProps(Transform root, System.Random random)
        {
            var parent = new GameObject("Prop swaps").transform;
            parent.SetParent(root, false);
            var materials = new Dictionary<string, Material>();
            int placed = 0;
            foreach (var swap in PropSwaps)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{PolyHavenRoot}/{swap.model}/{swap.model}_1k.fbx");
                if (source == null)
                {
                    Debug.LogWarning("Prop swap: missing model " + swap.model);
                    continue;
                }
                foreach (var box in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude)
                             .Where(f => f.name == swap.box && f.sharedMesh != null && f.sharedMesh.name == "Cube"))
                {
                    var renderer = box.GetComponent<Renderer>();
                    renderer.enabled = true; // measure with it on, then hide
                    var bounds = renderer.bounds;
                    renderer.enabled = false;
                    bool alongX = bounds.size.x >= bounds.size.z;
                    Vector3 cell = new Vector3(
                        alongX ? bounds.size.x / swap.across : bounds.size.x,
                        bounds.size.y / swap.high,
                        alongX ? bounds.size.z : bounds.size.z / swap.across);
                    for (int a = 0; a < swap.across; a++)
                    for (int h = 0; h < swap.high; h++)
                    {
                        Vector3 min = bounds.min + new Vector3(alongX ? a * cell.x : 0f, h * cell.y, alongX ? 0f : a * cell.z);
                        var holder = new GameObject(swap.model).transform;
                        holder.SetParent(parent, false);
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, holder);
                        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        // Long side of the model along the long side of the cell; small jitter on stacked crates.
                        Quaternion upright = model.transform.localRotation;
                        float jitter = swap.high > 1 ? (float)random.NextDouble() * 8f - 4f : 0f;
                        model.transform.localRotation = Quaternion.Euler(0f, jitter, 0f) * upright;
                        var size = Bounds(model).size;
                        bool modelAlongX = size.x >= size.z;
                        if (modelAlongX != (cell.x >= cell.z))
                            model.transform.localRotation = Quaternion.Euler(0f, 90f + jitter, 0f) * upright;
                        var fit = Bounds(model);
                        holder.localScale = new Vector3(cell.x / fit.size.x, cell.y / fit.size.y, cell.z / fit.size.z);
                        fit = Bounds(model);
                        holder.position += min - fit.min;
                        foreach (var part in model.GetComponentsInChildren<Renderer>())
                            part.sharedMaterials = part.sharedMaterials
                                .Select(slot => PolyHavenMaterial(swap.model, slot != null ? slot.name : swap.model, materials)).ToArray();
                        GameObjectUtility.SetStaticEditorFlags(model, StaticEditorFlags.BatchingStatic);
                        placed++;
                    }
                }
            }
            return placed;
        }

        static Bounds Bounds(GameObject model) => model.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
    }
}
