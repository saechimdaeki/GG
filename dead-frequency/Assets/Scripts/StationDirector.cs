using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Feeds the pacing director from the mission and turns its mob calls into hordes.</summary>
    public sealed partial class StationTrial
    {
        [Header("Pacing director")]
        [SerializeField] float mobIntervalMin = 40f;
        [SerializeField] float mobIntervalMax = 70f;
        [SerializeField] float relaxSeconds = 30f;
        [SerializeField] float peakSeconds = 4f;
        [SerializeField] float stressDecayPerSecond = 0.05f;
        [SerializeField] int mobBaseSize = 6;

        PacingDirector pacing;

        public PacingDirector.Mode PacingState => pacing?.State ?? PacingDirector.Mode.BuildUp;
        public float PacingIntensity => pacing?.Intensity ?? 0f;

        void RunPacing()
        {
            if (SessionMenu.Blocking)
                return;
            pacing ??= new PacingDirector(mobIntervalMin * Difficulty.MobInterval, mobIntervalMax * Difficulty.MobInterval,
                relaxSeconds * Difficulty.MobInterval, peakSeconds,
                stressDecayPerSecond, System.Environment.TickCount);
            if (SquadUnderPressure())
                pacing.AddStress(1f);
            // Mobs fill the gaps between set pieces: never before the route is chosen, never on top of a hold-out.
            bool travelling = phase is Phase.FindTerminal or Phase.FindWitness or Phase.Evacuate or Phase.Maintenance
                or Phase.LoadingEscort or Phase.FreightPower or Phase.FreightRelease or Phase.FreightEscape;
            bool blocked = !travelling || routes.Count > 0 || NearbyPressure() >= 4;
            if (!pacing.Tick(Time.deltaTime, NearbyPressure() > 0, blocked))
                return;
            // Ahead or behind the lead survivor; reinforcement delivery picks a hidden pocket near this point.
            float ahead = Random.value < 0.6f ? 14f : -10f;
            QueueReinforcements(mobBaseSize + 2 * HumanPlayerCount(), new Vector3(0f, 0f, SquadFurthestZ() + ahead), true);
        }

        void AddPacingStress(float amount) => pacing?.AddStress(amount);
    }
}
