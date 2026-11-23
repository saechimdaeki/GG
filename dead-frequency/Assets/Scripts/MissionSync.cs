using Unity.Netcode;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Host-owned mirror of the shared mission: phase, chosen route, shutter countdown and Seo I-gyeong's pose.
    /// Spawned by the host once the session starts; absent in solo play.
    /// </summary>
    public sealed class MissionSync : NetworkBehaviour
    {
        public static MissionSync Instance { get; private set; }

        readonly NetworkVariable<int> phase = new();
        readonly NetworkVariable<bool> drain = new();
        readonly NetworkVariable<float> countdown = new();
        readonly NetworkVariable<Vector3> witnessPosition = new();
        readonly NetworkVariable<float> witnessYaw = new();
        readonly NetworkVariable<bool> witnessWalking = new();
        float nextPush;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
                return;
            phase.OnValueChanged += (_, value) => StationTrial.Local?.ApplyPhase(value, drain.Value);
            // Late joiners catch up with whatever already happened.
            if (phase.Value != 0)
                StationTrial.Local?.ApplyPhase(phase.Value, drain.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Host: announce a new phase. Route goes first so clients open the right gate.</summary>
        public void Publish(int value, bool drainRoute)
        {
            drain.Value = drainRoute;
            phase.Value = value;
        }

        /// <summary>A client pressed E somewhere; the host decides whether it moves the mission on.</summary>
        [Rpc(SendTo.Server)]
        public void RequestInteractRpc(Vector3 at) => StationTrial.Local.MissionInteract(at, apply: true);

        /// <summary>Host started a horde: everyone hears the scream.</summary>
        [Rpc(SendTo.NotServer)]
        public void HordeRpc(Vector3 entry) => StationTrial.Local?.PlayHorde(entry);

        void Update()
        {
            var trial = StationTrial.Local;
            var witness = trial != null ? trial.Witness : null;
            if (trial == null || witness == null)
                return;
            if (IsServer)
            {
                if (Time.time < nextPush)
                    return;
                nextPush = Time.time + 0.05f;
                countdown.Value = trial.Countdown;
                witnessPosition.Value = witness.transform.position;
                witnessYaw.Value = witness.transform.eulerAngles.y;
                witnessWalking.Value = witness.Walking;
                return;
            }
            trial.MirrorCountdown(countdown.Value);
            witness.transform.SetPositionAndRotation(
                Vector3.Lerp(witness.transform.position, witnessPosition.Value, Time.deltaTime * 12f),
                Quaternion.Euler(0, witnessYaw.Value, 0));
            witness.Mirror(witnessWalking.Value);
        }
    }
}
