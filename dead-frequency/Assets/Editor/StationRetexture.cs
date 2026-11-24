using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>
    /// Gives each large surface family its own Poly Haven 2K PBR set, mapped world-space triplanar so stretched
    /// box walls and pillars keep a true scale. Run after Build (Build rewrites these materials).
    /// Batch: -executeMethod ResidualSignal.StationSceneBuilder.RetextureStation
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const string PolyHavenTextures = "Assets/Art/Textures/PolyHaven";
        const float UndergroundExposure = 4.45f;

        // material, texture set, metres covered by one repeat, tint, metallic
        static readonly (string material, string texture, float metres, Color tint, float metallic)[] Retexture =
        {
            ("Station - damp repair panels", "dirty_tiles", 1.6f, new Color(0.92f, 0.94f, 0.9f), 0f),
            ("Ceiling - cast concrete", "concrete_wall_006", 3f, new Color(0.75f, 0.75f, 0.72f), 0f),
            ("Annex - workshop concrete", "concrete_floor_worn_001", 3f, new Color(0.9f, 0.9f, 0.86f), 0f),
            ("Forecourt - soot stained facade", "dirty_concrete", 3.5f, new Color(0.85f, 0.85f, 0.8f), 0f),
            ("Floor - damp porcelain", "worn_tile_floor", 2f, Color.white, 0f),
            ("Steel - powder coat", "metal_plate_02", 1.5f, new Color(0.62f, 0.68f, 0.66f), 0.45f),
            ("Forecourt - oxidized dark steel", "rusty_metal_02", 1.5f, new Color(0.7f, 0.7f, 0.68f), 0.5f),
        };

        [MenuItem("잔류신호/표면 텍스처 교체 (Poly Haven)")]
        public static void RetextureStation()
        {
            foreach (var (name, texture, metres, tint, metallic) in Retexture)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Rendering/{name}.mat");
                if (material == null)
                {
                    Debug.LogWarning("Retexture: missing material " + name);
                    continue;
                }
                string folder = $"{PolyHavenTextures}/{texture}/{texture}";
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_diff.jpg"));
                material.SetTexture("_NormalMap", ImportNormal(folder + "_nor_gl.png"));
                material.SetTexture("_MaskMap", LinearMask(folder + "_mask.png"));
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_NormalScale", 1f);
                material.SetFloat("_MetallicRemapMax", metallic);
                material.SetFloat("_SmoothnessRemapMin", 0f);
                material.SetFloat("_SmoothnessRemapMax", 1f);
                material.SetFloat("_AORemapMin", 0f);
                material.SetFloat("_AORemapMax", 1f);
                // 5 = Triplanar in HDRP Lit's UV Base enum; world position * scale gives the repeat.
                material.SetFloat("_UVBase", 5f);
                material.SetFloat("_TexWorldScale", 1f / metres);
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
            }
            // Lower EV = brighter: let the new wall detail read underground without washing out the dark.
            var underground = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>($"{SettingsFolder}/Underground environment.asset");
            if (underground != null && underground.TryGet(out Exposure exposure))
            {
                exposure.fixedExposure.Override(UndergroundExposure);
                EditorUtility.SetDirty(exposure);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Retextured station surfaces.");
        }

        static Texture2D LinearMask(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
