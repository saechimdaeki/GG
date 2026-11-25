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

        // Off the main walking lines, but where a careless flashlight finds her.
        // Off the platform: there the power cabinet's flashlight line and the defence's stray rounds woke her every run.
        static readonly Vector3 CampaignWeeper = new(-3.6f, 0f, 45.5f);
        static readonly Vector3 AnnexWeeper = new(-3.6f, 0f, 45.5f);
        static readonly Vector3 ScavengeWeeper = new(0f, 0f, 3.5f);

        StationInfected weeper;
        bool weeperHinted;

        void BeginMission()
        {
            if (Networked)
                return;
            switch (Difficulty.Selected)
            {
                case Difficulty.Mission.Campaign:
                    SpawnWeeper(CampaignWeeper);
                    break;
                case Difficulty.Mission.Annex:
                    // The platform is behind us: shutter open, the rescue done, the annex ahead.
                    ChooseRoute(staffGate, false);
                    SetPhase(Phase.Maintenance);
                    SpawnWeeper(AnnexWeeper);
                    break;
                case Difficulty.Mission.Scavenge:
                    ChooseRoute(staffGate, false);
                    activeGate.position = raisedGatePosition;
                    SetPhase(Phase.Opening);
                    BeginScavenge();
                    SpawnWeeper(ScavengeWeeper);
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

        /// <summary>One sobbing infected per mission, off the director's books until she is startled.</summary>
        public StationInfected SpawnWeeper(Vector3 at)
        {
            if (NavMesh.SamplePosition(at, out var ground, 2.5f, NavMesh.AllAreas))
                at = ground.position;
            var prefab = commonPrefabs != null && commonPrefabs.Length > 0 ? commonPrefabs[commonPrefabs.Length - 1] : infectedPrefab;
            weeper = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            weeper.MarkWeeper();
            weeper.Follow(this, false, InfectedKind.Common, calm: true);
            weeper.BeginWeeping(Difficulty.InfectedHealth(1000));
            weeperHinted = false;
            return weeper;
        }

        /// <summary>A sob within earshot gets one radio warning, like hearing the Witch before you see her.</summary>
        void UpdateWeeperHint()
        {
            if (weeperHinted || weeper == null || !weeper.Weeping)
                return;
            if ((weeper.transform.position - transform.position).sqrMagnitude > 14f * 14f)
                return;
            weeperHinted = true;
            Signal("흐느끼는 소리 · 불빛을 비추지 말고 조용히 지나가라", 5f);
        }

        string lastSpawnKind = "none";
        float lastSpawnAt;

        static readonly bool PerfLog = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perfLog") >= 0;
        float perfWindowStart = -1f;
        int perfFrames;
        float perfWorst;

        /// <summary>-perfLog: every 10 s write average FPS and the worst frame to the player log.</summary>
        void UpdatePerfLog()
        {
            if (!PerfLog)
                return;
            if (perfWindowStart < 0f)
                perfWindowStart = Time.unscaledTime;
            perfFrames++;
            if (Time.unscaledDeltaTime > 0.2f)
                Debug.Log($"RS HITCH {Time.unscaledDeltaTime * 1000f:F0} ms at {Time.time:F1}s, last spawn {lastSpawnKind} {Time.time - lastSpawnAt:F2}s ago, " +
                    $"gc {System.GC.CollectionCount(0)}, infected {threats.Count}");
            perfWorst = Mathf.Max(perfWorst, Time.unscaledDeltaTime);
            float window = Time.unscaledTime - perfWindowStart;
            if (window < 10f)
                return;
            int alive = 0;
            foreach (var threat in threats)
                if (threat != null && !threat.Dead)
                    alive++;
            Debug.Log($"RS PERF {perfFrames / window:F1} fps, worst {perfWorst * 1000f:F0} ms, infected {alive}, phase {phase}, " +
                $"screen {Screen.width}x{Screen.height} at {UnityEngine.Rendering.DynamicResolutionHandler.instance.GetCurrentScale() * 100f:F0}%");
            perfWindowStart = Time.unscaledTime;
            perfFrames = 0;
            perfWorst = 0f;
        }

        public void WarnWeeper() => Signal("흐느끼는 자가 깼다! 쏴라!", 3f);

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
