using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.SceneManagement;

/// <summary>
/// Batch builds.
/// Local:  Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildMac   (or BuildWindows)
/// CI:     GameCI unity-builder with buildMethod: BuildScript.BuildCI (reads -customBuildTarget / -customBuildPath)
/// </summary>
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/Mac/ReelDungeon.app");

    /// <summary>Needs the "Windows Build Support (Mono)" module in Unity Hub.</summary>
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/ReelDungeon.exe");

    /// <summary>Entry point for GameCI, which passes the target and output path on the command line.</summary>
    public static void BuildCI()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), Arg("-customBuildTarget") ?? "StandaloneOSX");
        var path = Arg("-customBuildPath") ?? (target == BuildTarget.StandaloneOSX ? "Builds/Mac/ReelDungeon.app" : "Builds/Windows/ReelDungeon.exe");
        Build(target, path);
    }

    static void Build(BuildTarget target, string path)
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects), ScenePath);
        }
        // what players see: app name, version, icon, window
        PlayerSettings.productName = "Reel Dungeon";
        PlayerSettings.companyName = "GG";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.gg.reeldungeon");
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon/icon.png");
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, path, target, BuildOptions.None);
        Debug.Log($"Build {target} → {path}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
