using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot project setup and build, runnable in batch mode:
///   Unity -batchmode -quit -projectPath . -executeMethod Setup.Run
///   Unity -batchmode -quit -projectPath . -executeMethod Setup.BuildMac
/// Saves the shared materials (so their shaders ship), the Main scene with the Game object,
/// and registers the scene.
/// </summary>
public static class Setup
{
    const string Mats = "Assets/Resources/Materials";

    [MenuItem("Cat Chaos/Setup")]
    public static void Run()
    {
        Directory.CreateDirectory(Mats);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        HavenImport.BuildMaterials();
        Save(Toon("plank_flooring_02", 2.4f, new Color(0.78f, 0.58f, 0.42f), 0.45f), "Planks");
        Save(Toon("plank_flooring_02", 2.4f, new Color(0.92f, 0.8f, 0.66f), 0.4f), "PlanksLight");
        Save(Toon("floor_tiles_06", 1.6f, new Color(0.95f, 0.93f, 0.88f), 0.6f), "FloorTile");
        Save(Toon("floor_tiles_06", 1.2f, new Color(0.78f, 0.9f, 0.92f), 0.7f), "BathTile");
        Save(Toon("plastered_wall_04", 3f, new Color(0.97f, 0.93f, 0.86f), 0.2f), "Plaster");
        var paper = new Material(lit);
        paper.SetColor("_BaseColor", new Color(0.98f, 0.98f, 0.96f));
        paper.SetFloat("_Smoothness", 0.1f);
        paper.SetFloat("_Cull", 0);
        paper.EnableKeyword("_EMISSION");
        paper.SetColor("_EmissionColor", new Color(0.35f, 0.35f, 0.34f));
        Save(paper, "Paper");
        var sky = new Material(lit);
        sky.SetColor("_BaseColor", new Color(0.7f, 0.86f, 1f));
        sky.EnableKeyword("_EMISSION");
        sky.SetColor("_EmissionColor", new Color(0.75f, 0.9f, 1f) * 1.3f);
        sky.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Save(sky, "Sky");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Game").AddComponent<CatChaos.Game>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        PlayerSettings.productName = "냥난리";
        PlayerSettings.companyName = "GG";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.GG.CatChaos");
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[setup] done");
    }

    public static void BuildMac()
    {
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/Mac/CatChaos.app",
            target = BuildTarget.StandaloneOSX,
        });
        Debug.Log("[build] " + r.summary.result + " " + r.summary.totalSize / 1024 / 1024 + "MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    /// <summary>
    /// World-tiled cartoon surface from a Poly Haven texture set: a flat bright base colour
    /// with the photo kept as soft detail.
    /// </summary>
    static Material Toon(string id, float tile, Color tint, float smooth)
    {
        const string dir = "Assets/Resources/Art/Haven/Surfaces/";
        var m = new Material(Shader.Find("CatChaos/Triplanar"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + id + "_d.jpg"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + id + "_n.jpg"));
        m.SetTexture("_ArmMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + id + "_arm.jpg"));
        m.SetFloat("_Tile", tile);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Wet", 0);
        m.SetFloat("_Gain", 2f);
        m.SetFloat("_TexAmount", 0.4f);
        m.enableInstancing = true;
        return m;
    }

    static void Save(Material m, string name)
    {
        var path = $"{Mats}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }
}
