using UnityEditor;
using UnityEngine;

/// <summary>Keeps pixel art under Assets/Resources/Art crisp: point filter, no compression, no mipmaps.</summary>
class PixelArtImporter : AssetPostprocessor
{
    /// <summary>Pixel fonts render hard-edged (no anti-aliasing) so Galmuri stays crisp.</summary>
    void OnPreprocessAsset()
    {
        if (assetPath.StartsWith("Assets/Resources/Fonts/") && assetImporter is TrueTypeFontImporter font)
            font.fontRenderingMode = FontRenderingMode.HintedRaster;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
    }
}
