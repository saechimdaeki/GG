using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run-and-observe capture: Game.exe --scene Shop --screenshot C:/abs/shop.png --after 60
public class ScreenshotOnArg : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Arg("--screenshot") == null) return;
        Application.runInBackground = true; // macOS: an unfocused player stops updating, so frames never advance
        var go = new GameObject(nameof(ScreenshotOnArg));
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotOnArg>();
    }

    static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    IEnumerator Start()
    {
        var scene = Arg("--scene");
        if (!string.IsNullOrEmpty(scene)) yield return SceneManager.LoadSceneAsync(scene);
        // --at <seconds> beats --after <frames> when the moment is time-based (animations; 60 vs 120 Hz displays).
        // real time, so a paused game (timeScale 0) can still be captured
        if (float.TryParse(Arg("--at"), out var sec)) yield return new WaitForSecondsRealtime(sec);
        else
        {
            int after = int.TryParse(Arg("--after"), out var n) ? n : 60;
            for (int i = 0; i < after; i++) yield return null;
        }
        string destination = Arg("--screenshot");
        int frames = int.TryParse(Arg("--capture-frames"), out var count) ? Mathf.Clamp(count, 1, 120) : 1;
        if (frames > 1) Time.captureFramerate = 12;
        for (int frame = 0; frame < frames; frame++)
        {
            yield return new WaitForEndOfFrame();
            string path = frames == 1 ? destination : Path.ChangeExtension(destination, $".{frame:D3}.png");
            ScreenCapture.CaptureScreenshot(path);
        }
        for (int i = 0; i < 3; i++) yield return null;   // the file is written after the frame
        Application.Quit();
    }
}
