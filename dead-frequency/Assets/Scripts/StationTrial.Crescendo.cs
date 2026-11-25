using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Crescendo events: noises the squad sets off that call a horde, like Left 4 Dead's alarmed cars.</summary>
    public sealed partial class StationTrial
    {
        /// <summary>Cars that carry an armed alarm, by their scene names.</summary>
        static readonly string[] AlarmedCars = { "Abandoned car at the evacuation cordon", "Abandoned freight transport" };

        public int CrescendoCount { get; private set; }

        void ArmCarAlarms()
        {
            foreach (var name in AlarmedCars)
            {
                var car = GameObject.Find(name);
                if (car != null && car.GetComponentInChildren<Collider>() != null)
                    CarAlarm.Arm(car.transform);
            }
        }

        /// <summary>Something loud went off at <paramref name="at"/>: wake everything around it and, on the host,
        /// send a horde in from both sides out of sight.</summary>
        public void TriggerCrescendo(Vector3 at, string message)
        {
            if (!InCombat)
                return;
            CrescendoCount++;
            Signal(message, 4f);
            AddPacingStress(1.5f);
            if (!Authority)
                return;
            StationInfected.Noise(at, 45f);
            int half = CarAlarm.HordeSize / 2;
            QueueReinforcements(half, at + Vector3.forward * 8f, true);
            QueueReinforcements(half, at + Vector3.back * 8f, true);
        }
    }
}
