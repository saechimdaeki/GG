using UnityEngine;
using UnityEngine.AI;

namespace ResidualSignal
{
    /// <summary>
    /// Mission select on top of the one station: the full campaign, a second chapter that starts at the elevator
    /// (정비동 돌파), and an L4D2-style survival hold on platform 3 that never opens the shutter.
    /// </summary>
    public sealed partial class StationTrial
    {
        [Header("Survival")]
        [Tooltip("Seconds between brute (tank) arrivals in survival; the first comes at this mark.")]
        [SerializeField] float survivalBruteEvery = 180f;
        [Tooltip("Brute health in survival, before the difficulty multiplier.")]
        [SerializeField] int survivalBruteHealth = 1600;

        float survivalStartedAt = -1f;
        float survivalEndedAt = -1f;
        int survivalBrutes;

        /// <summary>Survival hold is running (or just ended) on platform 3.</summary>
        public bool Surviving => Difficulty.Selected == Difficulty.Mission.Survival && survivalStartedAt >= 0f;

        public float SurvivalSeconds => survivalStartedAt < 0f ? 0f :
            (survivalEndedAt >= 0f ? survivalEndedAt : Time.time) - survivalStartedAt;

        string SurvivalClock => $"{(int)SurvivalSeconds / 60}:{(int)SurvivalSeconds % 60:00}";

        /// <summary>Move the player to the chapter start before the AI squad spawns beside them.</summary>
        void PlaceForMission()
        {
            if (Networked)
                return;
            Vector3 start = Difficulty.Selected switch
            {
                Difficulty.Mission.Survival or Difficulty.Mission.Scavenge => terminal.position + new Vector3(-1.9f, 0f, 0.6f),
                Difficulty.Mission.Annex => evacuationPoint.position + new Vector3(0.6f, 0f, -1.2f),
                _ => transform.position,
            };
            if (start == transform.position)
                return;
            if (NavMesh.SamplePosition(start, out var landing, 2.5f, NavMesh.AllAreas))
                start = landing.position;
            player.enabled = false;
            transform.position = start + Vector3.up * 0.05f;
            player.enabled = true;
            if (Difficulty.Selected == Difficulty.Mission.Annex)
            {
                witness.transform.position = evacuationPoint.position;
                // Facing up the annex corridor.
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            else
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        void BeginMission()
        {
            if (Networked)
                return;
            switch (Difficulty.Selected)
            {
                case Difficulty.Mission.Annex:
                    // The platform is behind us: shutter open, the rescue done, the annex ahead.
                    ChooseRoute(staffGate, false);
                    SetPhase(Phase.Maintenance);
                    break;
                case Difficulty.Mission.Scavenge:
                    ChooseRoute(staffGate, false);
                    activeGate.position = raisedGatePosition;
                    SetPhase(Phase.Opening);
                    BeginScavenge();
                    break;
                case Difficulty.Mission.Survival:
                    ChooseRoute(staffGate, false);
                    SetPhase(Phase.Opening);
                    survivalStartedAt = Time.time;
                    Signal($"승강장 사수 · {Difficulty.LevelName} · 동메달 4분, 은메달 7분, 금메달 10분", 6f);
                    break;
            }
        }

        /// <summary>Authority, inside the platform hold: escalate forever and send a brute every few minutes.</summary>
        void UpdateSurvival()
        {
            if (survivalEndedAt >= 0f)
                return;
            // The shutter never opens; keep the hold clock away from zero so waves keep coming.
            countdown = shutterDelay;
            float seconds = SurvivalSeconds;
            // After three minutes the wave clock runs up to twice as fast.
            nextWave -= Time.deltaTime * Mathf.Clamp01((seconds - 180f) / 300f);
            if (seconds >= survivalBruteEvery * (survivalBrutes + 1))
            {
                survivalBrutes++;
                var brute = SpawnInfected(-3.4f, 27.2f, true, InfectedKind.Brute);
                brute.OverrideHealth(Difficulty.InfectedHealth(survivalBruteHealth + 400 * (survivalBrutes - 1)));
                PlayHorde(brute.transform.position);
                Signal("거대체가 온다! 기둥 뒤로!", 4f);
            }
        }

        void EndSurvival()
        {
            if (Surviving && survivalEndedAt < 0f)
                survivalEndedAt = Time.time;
        }

        void DrawSurvivalTimer()
        {
            Panel(new Rect(24, 24, 176, 38), HudSurface);
            Text(new Rect(36, 25, 160, 33), $"생존 {SurvivalClock}  ·  {Difficulty.Medal(SurvivalSeconds)}", statusStyle);
            float next = SurvivalSeconds >= 600f ? 1f : SurvivalSeconds >= 420f ? (SurvivalSeconds - 420f) / 180f :
                SurvivalSeconds >= 240f ? (SurvivalSeconds - 240f) / 180f : SurvivalSeconds / 240f;
            Panel(new Rect(36, 55, 152 * Mathf.Clamp01(next), 2), HudAccent);
        }
    }
}
