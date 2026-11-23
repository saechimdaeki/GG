using System;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Test hook: -shot path [-shotDelay s] saves a real screenshot (HUD included) and quits.</summary>
    public sealed class DevShot : MonoBehaviour
    {
        string path;
        float at;
        bool taken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-shot");
            if (index < 0)
                return;
            int delay = Array.IndexOf(arguments, "-shotDelay");
            var shot = new GameObject("Dev shot").AddComponent<DevShot>();
            shot.path = arguments[index + 1];
            shot.at = delay >= 0 ? float.Parse(arguments[delay + 1]) : 6f;
        }

        void Update()
        {
            if (taken && Time.time > at + 1.5f)
                Application.Quit();
            if (taken || Time.time < at)
                return;
            taken = true;
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
