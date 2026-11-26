using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Batch-mode project setup and macOS build:
///   Unity -batchmode -quit -projectPath . -executeMethod Setup.BuildMac
/// The scene is empty; DesktopTenants.Desk boots itself at runtime.
/// </summary>
public static class Setup
{
    const string Scene = "Assets/Scenes/Main.unity";

    [MenuItem("Desktop Tenants/Setup")]
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, Scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        PlayerSettings.productName = "Desktop Tenants";
        PlayerSettings.companyName = "GG";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.GG.DesktopTenants");
        PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 800;
        PlayerSettings.defaultScreenHeight = 600;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = false;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SplashScreen.show = false;
        AssetDatabase.SaveAssets();
    }

    public static void BuildMac()
    {
        Run();
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = "Builds/Mac/DesktopTenants.app",
            target = BuildTarget.StandaloneOSX,
        });
        UnityEngine.Debug.Log("[build] " + r.summary.result + " " + r.summary.totalSize / 1024 / 1024 + "MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
