using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ResidualSignal
{
    public static class StationTrialBuild
    {
        public static void Mac()
        {
            const string argument = "-trialOutput";
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, argument);
            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("Specify a destination .app path with " + argument);

            string output = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, output,
                BuildTarget.StandaloneOSX, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"macOS player build failed: {report.summary.result}");
            UnityEngine.Debug.Log($"MACOS TRIAL BUILD READY {output}");
        }
    }
}
