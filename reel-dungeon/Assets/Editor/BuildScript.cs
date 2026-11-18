using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
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
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Mac/ReelDungeon.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
