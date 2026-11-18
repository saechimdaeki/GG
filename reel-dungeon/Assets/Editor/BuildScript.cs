using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.SceneManagement;

/// <summary>Batch build: Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildMac</summary>
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    public static void BuildMac()
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

        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Mac/ReelDungeon.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
