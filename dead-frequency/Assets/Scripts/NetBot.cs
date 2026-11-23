using System;
using Unity.Netcode;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Test-only client bot (-netBot, with -netJoin): reconnects until the host is up, reads the register once the
    /// host has answered the phone, and shoots the nearest infected. Prints NETBOT lines for the host-side check.
    /// </summary>
    public sealed class NetBot : MonoBehaviour
    {
        float nextConnect = 5f;
        int seenPhase = -1;
        int seenInfected = -1;
        bool walkedToRegister;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-netBot") >= 0)
                new GameObject("Net bot").AddComponent<NetBot>();
        }

        void Update()
        {
            var network = NetworkManager.Singleton;
            if (!network.IsConnectedClient)
            {
                // Relay joins are driven by the session; only direct-IP tests need the reconnect loop.
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-relayJoin") < 0 && Time.time > nextConnect)
                {
                    nextConnect = Time.time + 4f;
                    network.Shutdown();
                    network.StartClient();
                    Debug.Log("NETBOT connecting");
                }
                return;
            }
            var trial = StationTrial.Local;
            if (trial == null)
                return;
            if (trial.PhaseIndex != seenPhase)
            {
                seenPhase = trial.PhaseIndex;
                Debug.Log($"NETBOT PHASE {seenPhase}");
            }
            var controller = trial.GetComponent<CharacterController>();
            if (seenPhase == 1 && !walkedToRegister)
            {
                walkedToRegister = true;
                controller.Move(Vector3.forward * (0.25f - controller.transform.position.z));
                controller.Move(new Vector3(-2.85f, 0, 1.35f));
                controller.Move(Vector3.forward * 3.75f);
                Debug.Log($"NETBOT REGISTER {trial.TryInteract()} at {controller.transform.position} avatar {SurvivorAvatar.Mine?.transform.position} owner {SurvivorAvatar.Mine?.IsOwner}");
            }
            var infected = FindObjectsByType<StationInfected>(FindObjectsSortMode.None);
            if (infected.Length != seenInfected)
            {
                seenInfected = infected.Length;
                Debug.Log($"NETBOT SEES {seenInfected}");
            }
            StationInfected nearest = null;
            foreach (var candidate in infected)
                if (!candidate.Dead && (nearest == null || Distance(candidate, trial) < Distance(nearest, trial)))
                    nearest = candidate;
            if (nearest == null)
                return;
            var eyes = Camera.main.transform;
            Vector3 aim = nearest.transform.position + Vector3.up * 1.2f - eyes.position;
            trial.transform.rotation = Quaternion.LookRotation(new Vector3(aim.x, 0, aim.z));
            eyes.rotation = Quaternion.LookRotation(aim);
            if (trial.TryFire())
                Debug.Log("NETBOT FIRED");
            else if (trial.Rounds == 0)
                trial.TryReload();
        }

        static float Distance(StationInfected infected, StationTrial trial) =>
            Vector3.Distance(infected.transform.position, trial.transform.position);
    }
}
