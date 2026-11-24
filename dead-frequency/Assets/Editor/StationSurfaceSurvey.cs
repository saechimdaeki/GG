using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Logs which materials cover the most surface in Platform03 (bounds-area estimate). Observation only.</summary>
    public static class StationSurfaceSurvey
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var area = new Dictionary<Material, float>();
            var count = new Dictionary<Material, int>();
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                var size = renderer.bounds.size;
                float surface = 2f * (size.x * size.y + size.y * size.z + size.x * size.z) / Mathf.Max(1, renderer.sharedMaterials.Length);
                foreach (var material in renderer.sharedMaterials.Where(m => m != null))
                {
                    area[material] = area.GetValueOrDefault(material) + surface;
                    count[material] = count.GetValueOrDefault(material) + 1;
                }
            }
            foreach (var pair in area.OrderByDescending(p => p.Value).Take(30))
            {
                var m = pair.Key;
                string map = m.HasProperty("_BaseColorMap") && m.GetTexture("_BaseColorMap") != null ? m.GetTexture("_BaseColorMap").name : "-";
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta;
                Debug.Log($"SURVEY {pair.Value,8:F0} m2  x{count[m],-4} {m.name} | map {map} | color {c.r:F2},{c.g:F2},{c.b:F2} | {AssetDatabase.GetAssetPath(m)}");
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Lists colliders inside the box given by -at x,y,z -half x,y,z. Observation only.</summary>
        public static void CollidersAt()
        {
            string[] a = System.Environment.GetCommandLineArgs();
            Vector3 Read(string key) { var v = a[System.Array.IndexOf(a, key) + 1].Split(','); return new Vector3(float.Parse(v[0]), float.Parse(v[1]), float.Parse(v[2])); }
            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            Physics.SyncTransforms();
            foreach (var c in Physics.OverlapBox(Read("-at"), Read("-half"), Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                string path = c.name;
                for (var t = c.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                Debug.Log($"COLLIDER {path} | trigger {c.isTrigger} | bounds {c.bounds.center} {c.bounds.size}");
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Lists free-standing primitive cubes (not walls/floors/ceilings): the boxy props worth replacing. Observation only.</summary>
        public static void Boxes()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var groups = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude)
                .Where(f => f.sharedMesh != null && f.sharedMesh.name == "Cube" && f.GetComponent<Renderer>().enabled)
                .Select(f => (f, b: f.GetComponent<Renderer>().bounds))
                .Where(x => x.b.size.y > 0.3f && x.b.size.y < 3.4f && x.b.min.y < 0.6f && Mathf.Max(x.b.size.x, x.b.size.z) > 0.4f
                            && Mathf.Max(x.b.size.x, x.b.size.z) < 8f && x.b.center.z > -3f)
                .GroupBy(x => x.f.name);
            foreach (var g in groups.OrderByDescending(g => g.Count()))
            {
                var x = g.First();
                Debug.Log($"BOX x{g.Count(),-3} {g.Key} | {x.b.size.x:F1}x{x.b.size.y:F1}x{x.b.size.z:F1} at {x.b.center} | " +
                          $"{x.f.GetComponent<Renderer>().sharedMaterial?.name} | parent {x.f.transform.parent?.name}");
            }
            EditorApplication.Exit(0);
        }
    }
}
