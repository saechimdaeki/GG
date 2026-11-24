using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Batch self-check for PacingDirector: -executeMethod ResidualSignal.PacingDirectorCheck.Run</summary>
    public static class PacingDirectorCheck
    {
        public static void Run()
        {
            string failure = Check();
            if (failure == null)
                Debug.Log("PACING DIRECTOR CHECK PASS");
            else
                Debug.LogError("PACING DIRECTOR CHECK FAIL: " + failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        static string Check()
        {
            const float step = 0.1f;
            var director = new PacingDirector(10f, 10f, 20f, 4f, 0.05f, seed: 7);

            // Build-up: one mob after the 10 s clock, none while blocked.
            int mobs = 0;
            for (float t = 0; t < 9.9f; t += step)
                mobs += director.Tick(step, false, false) ? 1 : 0;
            if (mobs != 0) return $"mob before its clock ({mobs})";
            for (float t = 0; t < 30f; t += step)
                mobs += director.Tick(step, false, true) ? 1 : 0;
            if (mobs != 0) return "mob sent while blocked";
            for (float t = 0; t < 0.3f; t += step)
                mobs += director.Tick(step, false, false) ? 1 : 0;
            if (mobs != 1) return $"expected one mob after the clock, got {mobs}";

            // Stress decays only with no enemies near.
            director.AddStress(0.5f);
            director.Tick(1f, true, true);
            if (!Mathf.Approximately(director.Intensity, 0.5f)) return "intensity decayed during a fight";
            director.Tick(1f, false, true);
            if (!Mathf.Approximately(director.Intensity, 0.45f)) return $"intensity did not decay ({director.Intensity})";

            // Full stress: peak, then relax with no mobs, then back to build-up.
            director.AddStress(1f);
            director.Tick(step, true, false);
            if (director.State != PacingDirector.Mode.Peak) return "full intensity did not peak";
            for (float t = 0; t < 4.2f; t += step)
                director.Tick(step, true, false);
            if (director.State != PacingDirector.Mode.Relax) return "peak did not fall into relax";
            for (float t = 0; t < 60f; t += step)
                mobs += director.Tick(step, true, false) ? 1 : 0;
            if (director.State != PacingDirector.Mode.Relax) return "relax ended while enemies were still near";
            for (float t = 0; t < 20.2f; t += step)
                mobs += director.Tick(step, false, false) ? 1 : 0;
            if (mobs != 1) return "mob sent during peak or relax";
            if (director.State != PacingDirector.Mode.BuildUp) return "relax did not return to build-up";
            return null;
        }
    }
}
