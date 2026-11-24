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
    }
}
