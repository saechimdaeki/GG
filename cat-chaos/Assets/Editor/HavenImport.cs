using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Poly Haven assets (CC0): normal maps import as normal maps, data maps stay linear, and
/// every model gets URP Lit materials built from the textures next to it
/// (diff / nor_gl / rough / metal / arm), so nothing renders flat or pink.
/// </summary>
public class HavenImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Art/Haven/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = assetPath.Contains("/Models/") ? 512 : 1024;  // props are small on screen
        string n = Path.GetFileNameWithoutExtension(assetPath);
        if (n.Contains("_nor_") || n.EndsWith("_n")) ti.textureType = TextureImporterType.NormalMap;
        else if (n.Contains("_rough") || n.Contains("_metal") || n.Contains("_arm") || n.EndsWith("_arm")) ti.sRGBTexture = false;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Art/Haven/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.bakeAxisConversion = true;
    }

    /// <summary>One URP Lit material per model folder, saved beside it as "Lit.mat".</summary>
    public static void BuildMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var file in Directory.GetFiles("Assets/Resources/Art/Haven/Models", "*.*", SearchOption.AllDirectories).Where(f => f.Contains("/textures/") && !f.EndsWith(".meta")))
            if (AssetImporter.GetAtPath(file) is TextureImporter ti && ti.maxTextureSize > 512) { ti.maxTextureSize = 512; ti.SaveAndReimport(); }
        foreach (var dir in Directory.GetDirectories("Assets/Resources/Art/Haven/Models"))
        {
            string id = Path.GetFileName(dir);
            string tex = Path.Combine(dir, "textures");
            if (!Directory.Exists(tex)) continue;
            var files = Directory.GetFiles(tex).Where(f => !f.EndsWith(".meta")).ToArray();
            Texture2D Find(params string[] keys) => files.Where(f => keys.Any(k => Path.GetFileName(f).Contains(k)))
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t);
            var m = new Material(lit) { name = id };
            var diff = Find("_diff_");
            if (diff) m.SetTexture("_BaseMap", diff);
            var nor = Find("_nor_gl_");
            if (nor) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
            bool metal = Find("_metal_", "_metallic_") != null;
            m.SetFloat("_Metallic", metal ? 0.6f : 0f);
            m.SetFloat("_Smoothness", metal ? 0.45f : 0.25f);
            if (files.Any(f => Path.GetFileName(f).Contains("_alpha_"))) m.SetFloat("_AlphaClip", 1);
            string path = Path.Combine(dir, "Lit.mat");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }
        AssetDatabase.SaveAssets();
    }
}
