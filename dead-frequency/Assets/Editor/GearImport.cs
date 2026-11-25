using UnityEditor;

namespace ResidualSignal
{
    /// <summary>Melee and throwable scans are measured at runtime (long axis, heavy end), so their meshes stay readable.</summary>
    sealed class GearImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath.Contains("/ResidualSignal/Melee/") || assetPath.Contains("/ResidualSignal/Throwables/"))
                ((ModelImporter)assetImporter).isReadable = true;
        }
    }
}
