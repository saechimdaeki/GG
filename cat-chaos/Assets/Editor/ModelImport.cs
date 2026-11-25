using UnityEditor;
using UnityEngine;

/// <summary>
/// The Quaternius cat imports with legacy clips (Idle/Walk/Run/Jump_Loop/Idle_Eating loop);
/// Kenney furniture keeps readable meshes so static pieces can get exact mesh colliders.
/// </summary>
public class ModelImport : AssetPostprocessor
{
    static readonly string[] Loops = { "|Idle", "|Walk", "|Run", "|Jump_Loop", "|Idle_Eating" };

    void OnPreprocessModel()
    {
        var mi = (ModelImporter)assetImporter;
        if (assetPath.Contains("/Art/Cat/"))
        {
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
        }
        else if (assetPath.Contains("/Art/Furniture/"))
        {
            mi.isReadable = true;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
        }
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.Contains("/Art/Cat/")) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            bool loop = System.Array.Exists(Loops, l => c.name.EndsWith(l));
            c.loopTime = loop;
            c.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        }
        mi.clipAnimations = clips;
    }
}
