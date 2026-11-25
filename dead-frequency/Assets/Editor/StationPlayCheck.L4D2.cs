using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Checks for the Left 4 Dead 2 additions: the spitter, charger and jockey, and the gear slots.</summary>
    public static partial class StationPlayCheck
    {
        static StationInfected mutant;
        static float mutantAt;
        static Vector3 mutantStart;
        static bool mutantShot;
        static string mutantLog = "";
        static readonly Vector3 MutantStand = new(0f, 0.09f, 15.2f);

        static string ShotPath(string suffix) => SessionState.GetString(RunningKey + ".path", "").Replace(".png", suffix);

        /// <summary>Photograph the player from outside: behind and above the shoulder, looking at <paramref name="focus"/>.</summary>
        static void CaptureFromOutside(Transform player, Vector3 focus, string suffix, float back = 3.2f, float side = 1.4f)
        {
            var view = Camera.main.transform;
            Vector3 restPosition = view.localPosition;
            Quaternion restRotation = view.localRotation;
            var hidden = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in view)
                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    hidden.Add(child.gameObject);
                }
            Vector3 lens = player.position - player.forward * back + player.right * side + Vector3.up * 2.1f;
            view.SetPositionAndRotation(lens, Quaternion.LookRotation(focus - lens));
            Capture(Camera.main, ShotPath(suffix));
            view.SetLocalPositionAndRotation(restPosition, restRotation);
            foreach (var child in hidden)
                child.SetActive(true);
        }

        /// <summary>-mutantCheck: acid lands on and burns a player who stands still; a charger carries and pins;
        /// a jockey rides and steers. Killing each frees the player.</summary>
        static void CheckMutants(StationTrial trial, CharacterController controller)
        {
            if (step == 0)
            {
                Teleport(controller, MutantStand);
                controller.transform.rotation = Quaternion.identity;
                step = 600;
            }
            float t = Time.time - mutantAt;
            switch (step)
            {
                case 600:
                    trial.Heal(100);
                    mutant = trial.SpawnInfected(0f, 26f, false, InfectedKind.Spitter);
                    healthBefore = trial.Health;
                    mutantAt = Time.time;
                    mutantShot = false;
                    step = 601;
                    break;
                case 601:
                    if (AcidPool.Active > 0)
                        AimAtPoint(controller.transform.position + controller.transform.forward * 2.5f);
                    RecordDemoFrame("spitter");
                    if (AcidPool.Active > 0 && !mutantShot && t > 0.5f)
                    {
                        mutantShot = true;
                        mutantAt = Time.time;
                        break;
                    }
                    if (mutantShot && trial.Health < healthBefore)
                    {
                        // Looking down at the acid at your feet, as the player would.
                        AimAtPoint(controller.transform.position + controller.transform.forward * 0.7f);
                        Capture(Camera.main, ShotPath(".acid.png"));
                        mutantLog += $"acid {healthBefore}->{trial.Health}; ";
                        mutant.Hit(9999);
                        step = 610;
                        mutantAt = Time.time;
                        break;
                    }
                    if (t > 9f)
                        Finish(false, $"Spitter acid never hurt the player (pools {AcidPool.Active}, spat {mutantShot}, health {trial.Health}, {mutant.MutantDebug}).");
                    break;
                case 610:
                    if (t < 0.5f || AcidPool.Active > 0)
                    {
                        // Out of the acid, then wait for it to dry up before the next special.
                        Teleport(controller, MutantStand + Vector3.forward * 5f);
                        if (t > 10f)
                            Finish(false, "Acid pool never dried up.");
                        break;
                    }
                    Teleport(controller, MutantStand);
                    trial.Heal(100);
                    mutant = trial.SpawnInfected(0f, 27f, false, InfectedKind.Charger);
                    mutantStart = controller.transform.position;
                    mutantAt = Time.time;
                    step = 611;
                    break;
                case 611:
                    RecordDemoFrame("charger");
                    if (trial.IsHeldBy(mutant))
                    {
                        mutantAt = Time.time;
                        healthBefore = trial.Health;
                        step = 612;
                        break;
                    }
                    if (t > 10f)
                        Finish(false, $"Charger never pinned the player (charger at {mutant.transform.position}, dead {mutant.Dead}).");
                    break;
                case 612:
                {
                    float carried = Vector3.Distance(Vector3.ProjectOnPlane(controller.transform.position - mutantStart, Vector3.up), Vector3.zero);
                    AimAtPoint(mutant.transform.position + Vector3.up * 1.4f);
                    RecordDemoFrame("charger");
                    if (t > 1.6f && !System.IO.File.Exists(ShotPath(".charger.png")))
                        Capture(Camera.main, ShotPath(".charger.png"));
                    if (t < 2.6f)
                        break;
                    trial.StruggleFree();
                    for (int i = 0; i < 10; i++)
                        trial.StruggleFree();
                    if (!trial.IsHeldBy(mutant))
                    {
                        Finish(false, "Struggling broke a charger's pin; only teammates should.");
                        return;
                    }
                    if (carried < 1.0f || trial.Health >= healthBefore)
                    {
                        Finish(false, $"Charger did not carry and pound (carried {carried:F1} m, health {healthBefore}->{trial.Health}).");
                        return;
                    }
                    mutantLog += $"charger carried {carried:F1}m, {healthBefore}->{trial.Health}; ";
                    mutant.Hit(9999);
                    mutantAt = Time.time;
                    step = 613;
                    break;
                }
                case 613:
                    if (t < 0.3f)
                        break;
                    if (trial.Held)
                    {
                        Finish(false, "Killing the charger did not free the player.");
                        return;
                    }
                    Teleport(controller, MutantStand);
                    controller.transform.rotation = Quaternion.identity;
                    trial.Heal(100);
                    mutant = trial.SpawnInfected(0f, 20f, false, InfectedKind.Jockey);
                    mutantAt = Time.time;
                    step = 620;
                    break;
                case 620:
                    RecordDemoFrame("jockey");
                    if (trial.IsHeldBy(mutant))
                    {
                        mutantStart = controller.transform.position;
                        mutantAt = Time.time;
                        step = 621;
                        break;
                    }
                    if (t > 10f)
                        Finish(false, $"Jockey never rode the player (held {trial.Held}, downed {trial.Downed}, {mutant.MutantDebug}).");
                    break;
                case 621:
                {
                    RecordDemoFrame("jockey");
                    if (t > 1.5f && !System.IO.File.Exists(ShotPath(".jockey-eyes.png")))
                    {
                        Capture(Camera.main, ShotPath(".jockey-eyes.png"));
                    }
                    if (t < 3.5f)
                        break;
                    float steered = Vector3.ProjectOnPlane(controller.transform.position - mutantStart, Vector3.up).magnitude;
                    float above = mutant.transform.position.y - controller.transform.position.y;
                    if (steered < 1.5f || above < 0.6f)
                    {
                        Finish(false, $"Jockey did not ride and steer (steered {steered:F1} m, riding {above:F2} m up).");
                        return;
                    }
                    mutantLog += $"jockey steered {steered:F1}m; ";
                    mutant.Hit(9999);
                    mutantAt = Time.time;
                    step = 622;
                    break;
                }
                case 622:
                    if (t < 0.3f)
                        break;
                    if (trial.Held)
                    {
                        Finish(false, "Killing the jockey did not free the player.");
                        return;
                    }
                    Capture(Camera.main, SessionState.GetString(RunningKey + ".path", ""));
                    Finish(true, "PLATFORM03 MUTANT CHECK PASS (" + mutantLog + ")");
                    return;
            }
        }

        static float gearAt;
        static StationInfected gearTarget;

        /// <summary>-gearCheck: pills and adrenaline give temporary health that soaks hits first; a fire axe
        /// cuts down a common in one swing. Close-ups of every pickup kind.</summary>
        static void CheckGear(StationTrial trial, CharacterController controller)
        {
            if (step == 0)
                step = 700;
            float t = Time.time - gearAt;
            switch (step)
            {
                case 700:
                {
                    // One close-up of each kind lying on the floor.
                    var seen = new System.Collections.Generic.HashSet<Gear>();
                    var view = Camera.main.transform;
                    Vector3 restPosition = view.localPosition;
                    Quaternion restRotation = view.localRotation;
                    foreach (var pickup in GearPickup.All)
                    {
                        if (!seen.Add(pickup.gear))
                            continue;
                        Vector3 lens = pickup.transform.position + new Vector3(0.45f, 0.45f, -0.55f);
                        view.SetPositionAndRotation(lens, Quaternion.LookRotation(pickup.transform.position - lens));
                        Capture(Camera.main, ShotPath($".pickup-{pickup.gear}.png"));
                    }
                    view.SetLocalPositionAndRotation(restPosition, restRotation);
                    if (seen.Count < 8)
                    {
                        Finish(false, $"Only {seen.Count}/8 gear kinds placed in the station.");
                        return;
                    }
                    if (!Take(trial, controller, Gear.Pills))
                        return;
                    trial.TakeHit(40);
                    int hurt = trial.Health;
                    if (!trial.TryUseMedicine() || trial.TempHealth < 39f)
                    {
                        Finish(false, $"Pills gave {trial.TempHealth:F1} temporary health at {hurt}.");
                        return;
                    }
                    trial.TakeHit(10);
                    if (trial.Health != hurt || trial.TempHealth > 31f)
                    {
                        Finish(false, $"Temporary health did not soak the hit first (health {hurt}->{trial.Health}, temp {trial.TempHealth:F1}).");
                        return;
                    }
                    gearAt = Time.time;
                    step = 701;
                    break;
                }
                case 701:
                {
                    if (t < 2f)
                        break;
                    float before = trial.TempHealth;
                    if (!Take(trial, controller, Gear.Adrenaline))
                        return;
                    if (!trial.TryUseMedicine() || !trial.Adrenaline)
                    {
                        Finish(false, "Adrenaline did not take effect.");
                        return;
                    }
                    Debug.Log($"PLATFORM03 GEAR temp health decays to {before:F1} after 2s, adrenaline on");
                    if (!Take(trial, controller, Gear.FireAxe) || !trial.MeleeOut)
                    {
                        Finish(false, $"Picking up the fire axe did not bring it out (melee {trial.MeleeGear}).");
                        return;
                    }
                    Teleport(controller, MutantStand);
                    controller.transform.rotation = Quaternion.identity;
                    AimAtPoint(MutantStand + new Vector3(0f, 1.5f, 6f));
                    gearAt = Time.time;
                    step = 704;
                    break;
                }
                case 704:
                    AimAtPoint(MutantStand + new Vector3(0f, 1.5f, 6f));
                    if (t < 0.8f)
                        break;
                    Capture(Camera.main, ShotPath(".melee-rest.png"));
                    gearTarget = trial.SpawnInfected(0f, 17f, false, InfectedKind.Common, calm: true);
                    gearAt = Time.time;
                    step = 702;
                    break;
                case 702:
                    AimAtPoint(gearTarget.transform.position + Vector3.up * 1.2f);
                    RecordDemoFrame("fire-axe");
                    if (t < 0.4f)
                        break;
                    if (!trial.TrySwing())
                    {
                        Finish(false, "Fire axe would not swing.");
                        return;
                    }
                    gearAt = Time.time;
                    step = 703;
                    break;
                case 703:
                    AimAtPoint(gearTarget.transform.position + Vector3.up * 1.2f);
                    RecordDemoFrame("fire-axe");
                    if (t > 0.165f && !System.IO.File.Exists(ShotPath(".melee-swing.png")))
                        Capture(Camera.main, ShotPath(".melee-swing.png"));
                    if (t < 0.8f)
                        break;
                    if (!gearTarget.Dead)
                    {
                        Finish(false, $"One fire-axe swing left a common standing ({gearTarget.RemainingHealth} health).");
                        return;
                    }
                    Finish(true, $"PLATFORM03 GEAR CHECK PASS (pills temp {trial.TempHealth:F1}, adrenaline {trial.Adrenaline}, axe kill)");
                    return;
            }
        }

        static bool Take(StationTrial trial, CharacterController controller, Gear gear)
        {
            GearPickup pickup = null;
            foreach (var candidate in GearPickup.All)
                if (candidate.gear == gear)
                    pickup = candidate;
            if (pickup == null)
            {
                Finish(false, $"No {gear} on the floor.");
                return false;
            }
            Teleport(controller, pickup.transform.position + Vector3.back * 0.3f);
            Physics.SyncTransforms();
            for (int press = 0; press < 4; press++)
            {
                if (trial.CarriedMedicine == gear || trial.CarriedThrowable == gear || trial.MeleeGear == gear)
                    return true;
                trial.TryInteract();
            }
            if (trial.CarriedMedicine == gear || trial.CarriedThrowable == gear || trial.MeleeGear == gear)
                return true;
            Finish(false, $"E beside the {gear} did not pick it up.");
            return false;
        }

        static CarAlarm alarm;
        static float alarmAt;
        static int alarmThreats;

        /// <summary>-alarmCheck: shooting the armed car at the cordon sets off the siren and calls a horde.</summary>
        static void CheckAlarm(StationTrial trial, CharacterController controller)
        {
            float t = Time.time - alarmAt;
            switch (step)
            {
                case 0:
                    if (CarAlarm.All.Count == 0)
                    {
                        Finish(false, "No alarmed car in the station.");
                        return;
                    }
                    alarm = CarAlarm.All[0];
                    Vector3 car = alarm.transform.position;
                    Teleport(controller, new Vector3(car.x - 5.5f, 0.09f, car.z + 1.5f));
                    controller.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(car - controller.transform.position, Vector3.up));
                    alarmAt = Time.time;
                    step = 800;
                    break;
                case 800:
                    AimAtPoint(alarm.transform.position + Vector3.up * 0.6f);
                    RecordDemoFrame("car-alarm");
                    if (t < 0.8f || Mathf.Repeat(Time.time, 1.4f) > 0.1f)
                        break;
                    Capture(Camera.main, ShotPath(".alarm-armed.png"));
                    alarmThreats = trial.Threats.Count;
                    if (!trial.TryFire() || !alarm.Tripped || trial.CrescendoCount != 1)
                    {
                        Finish(false, $"Shooting the car did not trip its alarm (tripped {alarm.Tripped}, crescendos {trial.CrescendoCount}).");
                        return;
                    }
                    alarmAt = Time.time;
                    step = 801;
                    break;
                case 801:
                    AimAtPoint(alarm.transform.position + Vector3.up * 0.6f);
                    RecordDemoFrame("car-alarm");
                    if (t > 0.08f && !System.IO.File.Exists(ShotPath(".alarm.png")))
                        Capture(Camera.main, ShotPath(".alarm.png"));
                    int arrived = trial.Threats.Count - alarmThreats;
                    if (arrived >= 8)
                    {
                        Capture(Camera.main, SessionState.GetString(RunningKey + ".path", ""));
                        Finish(true, $"PLATFORM03 ALARM CHECK PASS ({arrived} infected answered the siren in {t:F1}s)");
                        return;
                    }
                    if (t > 20f)
                        Finish(false, $"Only {arrived} infected answered the car alarm in 20 s.");
                    break;
            }
        }
    }
}
