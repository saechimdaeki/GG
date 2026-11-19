using UnityEditor;
using UnityEngine;

/// <summary>
/// Quaternius characters import with legacy animation (no Animator controllers needed;
/// hundreds of zombies just call Animation.Play). Walk/Idle loop, the rest play once.
/// </summary>
public class ModelImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Art/Characters/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Legacy;
        mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.Contains("/Art/Characters/")) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            bool loop = c.name.EndsWith("|Walk") || c.name.EndsWith("|Idle");
            c.loopTime = loop;
            c.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        }
        mi.clipAnimations = clips;
    }
}
