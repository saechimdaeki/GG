using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ResidualSignal
{
    [InitializeOnLoad]
    public static class StationPlayCheck
    {
        const string RunningKey = "ResidualSignal.StationPlayCheck";
        const string CaptureArgument = "-stationCapture";
        static double startedAt;
        static int step;
        static float stepTime;
        static StationInfected target;
        static int startingRounds;
        static bool useDrain;
        static bool specialsMode;
        static bool netMode;
        static StationInfected netTarget;
        static bool netSettled;
        static StationInfected special;
        static int healthBefore;
        static int lootAtStart = -1;
        static bool botAhead;
        static StationInfected botFirstTarget;
        static StationInfected botSecondTarget;
        static bool botFirstKilled;
        static bool botSecondKilled;
        static StationInfected squadFocus;
        static bool squadFollowedFocus;
        static SurvivorBot posedRescuer;
        static bool campaignMode;
        static bool liveDefenseMode;
        static int preparationThreats;
        static Vector3 approachStart;
        static string demoFrameFolder;
        static int demoFrame;
        static float nextDemoFrame;
        static bool demoBossWalked;
        static bool bossThrowCheck;
        static bool bossDodgeCheck;
        static bool yardBossCheck;
        static bool yardBotEngaged;
        static float nextSquadBurst;
        static readonly StationInfected[] ShoveTargets = new StationInfected[3];
        static readonly Vector3[] ShoveOrigins = new Vector3[3];
        static bool yardSawDebris;
        static int yardShots;
        static bool bossCoverCheck;
        static bool capturedBossLift;
        static bool sawBossChunk;
        static bool bossDodged;


        static StationPlayCheck()
        {
            if (SessionState.GetBool(RunningKey, false))
                EditorApplication.update += Check;
        }

        public static void Run()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int demo = Array.IndexOf(arguments, "-demoFrames");
            if (demo >= 0)
            {
                string directory = Path.GetFullPath(arguments[demo + 1]);
                Directory.CreateDirectory(directory);
                SessionState.SetString(RunningKey + ".demo", directory);
            }
            else
                SessionState.EraseString(RunningKey + ".demo");
            int index = Array.IndexOf(arguments, CaptureArgument);
            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("A PNG output path is required after " + CaptureArgument);

            string path = arguments[index + 1];
            if (File.Exists(path))
                throw new IOException("Capture output already exists: " + path);

            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            if (Array.IndexOf(arguments, "-botCheck") < 0)
            {
                var settings = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<StationTrial>());
                settings.FindProperty("spawnSpecials").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetString(RunningKey + ".path", path);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
            EditorApplication.EnterPlaymode();
        }

        static void Check()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (startedAt == 0)
            {
                Application.runInBackground = true;
                startedAt = EditorApplication.timeSinceStartup;
                demoFrameFolder = SessionState.GetString(RunningKey + ".demo", "");
                // Scripted checks need a quiet station: no director until a mode turns it on.
                var quiet = UnityEngine.Object.FindFirstObjectByType<StationTrial>();
                if (quiet != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-botCheck") < 0)
                {
                    var director = new SerializedObject(quiet);
                    director.FindProperty("spawnSpecials").boolValue = false;
                    director.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            double sinceStart = EditorApplication.timeSinceStartup - startedAt;
            var trial = UnityEngine.Object.FindFirstObjectByType<StationTrial>();
            var controller = trial != null ? trial.GetComponent<CharacterController>() : null;
            if (controller == null)
            {
                Finish(false, "Player controller did not load.");
                return;
            }
            if (sinceStart > (netMode ? 240 : yardBossCheck ? 115 : 75))
            {
                var shutter = GameObject.Find("Security shutter");
                Finish(false, $"Station check timed out at step {step}, player {controller.transform.position}, shutter {shutter.transform.position.y:F2}, health {trial.Health}, clock {Time.time:F2}/{stepTime:F2}.");
                return;
            }
            switch (step)
            {
                case 0 when sinceStart >= 2:
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-openingPlate") >= 0)
                    {
                        CaptureOpeningPlate(Camera.main, SessionState.GetString(RunningKey + ".path", ""));
                        Finish(true, "PLATFORM03 OPENING PLATE READY");
                        return;
                    }
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gripCheck") >= 0)
                    {
                        var profile = new SerializedObject(trial);
                        var prefab = (SurvivorBot)profile.FindProperty("botPrefab").objectReferenceValue;
                        posedRescuer = UnityEngine.Object.Instantiate(prefab, new Vector3(1.15f, 0.03f, -9.75f), Quaternion.identity);
                        posedRescuer.Join(trial, 0);
                        posedRescuer.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
                        stepTime = Time.time;
                        step = 88;
                        break;
                    }
                    if (trial.TryInteract())
                    {
                        Finish(false, "Cabinet accepted power from outside interaction range.");
                        return;
                    }
                    var settings = new SerializedObject(trial);
                    settings.FindProperty("acceptLocalInput").boolValue = false;
                    liveDefenseMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-liveDefenseCheck") >= 0;
                    campaignMode = liveDefenseMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-campaignCheck") >= 0;
                    if (!campaignMode)
                    {
                        settings.FindProperty("shutterDelay").floatValue = 5.8f;
                        settings.FindProperty("serviceTransferSeconds").floatValue = 4.6f;
                    }
                    settings.FindProperty("spawnSpecials").boolValue = false;
                    specialsMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-specialsCheck") >= 0;
                    netMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-netHost") >= 0;
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-shoveCheck") >= 0)
                    {
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        controller.enabled = false;
                        controller.transform.position = new Vector3(-2.4f, 0.08f, 57f);
                        controller.enabled = true;
                        for (int i = 0; i < ShoveTargets.Length; i++)
                        {
                            var at = controller.transform.position + new Vector3((i - 1) * 0.88f, 0f, 1.40f);
                            ShoveTargets[i] = trial.SpawnInfected(at.x, at.z, false);
                            ShoveOrigins[i] = ShoveTargets[i].transform.position;
                        }
                        Physics.SyncTransforms();
                        AimAtPoint(controller.transform.position + new Vector3(0f, 1.45f, 6f));
                        if (!trial.TryShove())
                        {
                            Finish(false, "Right-click shove was not accepted.");
                            return;
                        }
                        stepTime = Time.time;
                        step = 99;
                        break;
                    }
                    bossThrowCheck = Array.IndexOf(Environment.GetCommandLineArgs(), "-bossThrowCheck") >= 0;
                    yardBossCheck = Array.IndexOf(Environment.GetCommandLineArgs(), "-yardBossCheck") >= 0;
                    bossDodgeCheck = Array.IndexOf(Environment.GetCommandLineArgs(), "-bossDodgeCheck") >= 0;
                    bossCoverCheck = Array.IndexOf(Environment.GetCommandLineArgs(), "-bossCoverCheck") >= 0;
                    if (bossThrowCheck || bossDodgeCheck || bossCoverCheck || Array.IndexOf(Environment.GetCommandLineArgs(), "-bossModelCheck") >= 0)
                    {
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        controller.enabled = false;
                        controller.transform.position = new Vector3(-1.6f, 0.08f, 59.0f);
                        controller.enabled = true;
                        bool rangedBossTest = bossThrowCheck || bossDodgeCheck || bossCoverCheck ||
                            !string.IsNullOrEmpty(demoFrameFolder);
                        target = trial.SpawnInfected(-1.6f, rangedBossTest ? 65.8f : 62.65f, false, InfectedKind.Brute);
                        approachStart = target.transform.position;
                        stepTime = Time.time;
                        step = 96;
                        if (bossThrowCheck || bossDodgeCheck || bossCoverCheck)
                        {
                            target.StageEncounter();
                            target.Hit(9999);
                            if (target.Dead)
                            {
                                Finish(false, "The staged boss can be killed before the arena opens.");
                                return;
                            }
                            target.BeginStagedEncounter();
                            target.Hit(900);
                            healthBefore = trial.Health;
                            step = 97;
                        }
                        break;
                    }
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-callerCheck") >= 0)
                    {
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        controller.Move(Vector3.right * 2.2f);
                        controller.Move(Vector3.forward * 1.9f);
                        target = trial.SpawnInfected(2.6f, -9.15f, false, InfectedKind.Caller);
                        stepTime = Time.time;
                        step = 94;
                        break;
                    }
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-botCheck") >= 0)
                    {
                        settings.FindProperty("spawnSpecials").boolValue = true;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        bool quiet = trial.OpeningStaging && trial.Health == 100 && SurvivorBot.All.Count == 3;
                        foreach (var companion in SurvivorBot.All)
                            quiet &= companion.AimedTarget == null && companion.Health == 150 &&
                                companion.transform.position.z < controller.transform.position.z + 0.25f;
                        if (!quiet)
                        {
                            Finish(false, "Squad entered combat or passed the player before leaving the staging area.");
                            return;
                        }
                        Debug.Log("PLATFORM03 QUIET START CHECK PASS");
                        stepTime = Time.time;
                        step = 59;
                        break;
                    }
                    if (netMode)
                    {
                        step = 40;
                        break;
                    }
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    string launchCapture = SessionState.GetString(RunningKey + ".path", "");
                    Capture(Camera.main, Path.ChangeExtension(launchCapture, ".forecourt.png"));
                    MovePastTriage(controller);
                    if (controller.transform.position.z < 0.12f)
                    {
                        Finish(false, "The station entrance blocked the route from the forecourt.");
                        return;
                    }
                    controller.Move(new Vector3(2.4f, 0, 1.6f));
                    if (!trial.TryInteract() || trial.Equipped != "산탄총")
                    {
                        Finish(false, $"Gun rack pickup failed (holding {trial.Equipped}).");
                        return;
                    }
                    Capture(Camera.main, Path.ChangeExtension(launchCapture, ".shotgun.png"));
                    controller.Move(Vector3.forward * 3.31f);
                    if (!trial.TryInteract() || trial.Equipped != "반자동 카빈" || trial.Rounds != 20)
                    {
                        Finish(false, $"Carbine pickup failed (holding {trial.Equipped}, rounds {trial.Rounds}).");
                        return;
                    }
                    Capture(Camera.main, Shot(".carbine.png"));
                    controller.Move(Vector3.back * 3.31f);
                    trial.Equip(0);
                    controller.Move(new Vector3(-2.4f, 0, -1.6f));
                    controller.Move(new Vector3(-2.85f, 0, 1.35f));
                    if (!trial.TryInteract() || trial.TryInteract())
                    {
                        Finish(false, "Emergency line interaction failed or progressed twice.");
                        return;
                    }
                    controller.Move(Vector3.forward * 3.75f);
                    if (!trial.TryInteract())
                    {
                        Finish(false, "Evacuation register could not be read.");
                        return;
                    }
                    useDrain = Array.IndexOf(Environment.GetCommandLineArgs(), "-drainRoute") >= 0;
                    controller.Move(Vector3.forward * 0.65f);
                    if (!useDrain)
                        controller.Move(Vector3.right * 6.0f);
                    if (!trial.TryInteract())
                    {
                        Finish(false, "Could not select the service gate.");
                        return;
                    }
                    stepTime = Time.time;
                    step = specialsMode ? 20 : 6;
                    break;
                case 59:
                    RecordDemoFrame();
                    Vector3 routeStep = TriagePathStep(controller.transform.position, 2.5f);
                    float remaining = Vector3.Distance(controller.transform.position, routeStep);
                    controller.Move(Vector3.ClampMagnitude(routeStep - controller.transform.position, Time.deltaTime * 3.5f));
                    if (controller.transform.position.z < 2.38f)
                        remaining = Mathf.Max(remaining, 0.2f);
                    if (remaining < 0.12f)
                    {
                        stepTime = Time.time;
                        step = 60;
                    }
                    else if (Time.time > stepTime + 12f)
                        Finish(false, $"Squad test could not walk through the forecourt (player at {controller.transform.position}).");
                    break;
                case 60:
                {
                    RecordDemoFrame();
                    int bots = SurvivorBot.All.Count;
                    if (lootAtStart < 0 && bots > 0)
                    {
                        lootAtStart = LootItem.All.Count;
                        // Deterministic targets for the squad test; the campaign director has its own check.
                        botFirstTarget = trial.SpawnInfected(-0.65f, 6.30f, false);
                        botSecondTarget = trial.SpawnInfected(1.10f, 6.36f, false);
                    }
                    if (squadFocus == null && !squadFollowedFocus && botSecondTarget != null && !botSecondTarget.Dead)
                    {
                        AimAt(botSecondTarget);
                        if (trial.TryFocusFire())
                            squadFocus = trial.FocusTarget;
                    }
                    foreach (var bot in SurvivorBot.All)
                    {
                        if (squadFocus != null && bot.AimedTarget == squadFocus)
                            squadFollowedFocus = true;
                        if (Vector3.Dot(controller.transform.forward, (bot.transform.position - controller.transform.position).normalized) > 0.3f)
                            botAhead = true;
                    }
                    int looted = lootAtStart - LootItem.All.Count;
                    botFirstKilled |= botFirstTarget != null && botFirstTarget.Dead;
                    botSecondKilled |= botSecondTarget != null && botSecondTarget.Dead;
                    int dead = (botFirstKilled ? 1 : 0) + (botSecondKilled ? 1 : 0);
                    if (Time.time > stepTime + 12f && bots == 3 && dead >= 2 && botAhead && looted >= 1 && squadFollowedFocus)
                    {
                        Vector3 middle = Vector3.zero;
                        foreach (var bot in SurvivorBot.All)
                            middle += bot.transform.position / bots;
                        Transform view = Camera.main.transform;
                        view.position = controller.transform.position + Vector3.up * 1.65f + (controller.transform.position - middle).normalized * 1.5f;
                        AimAtPoint(middle + Vector3.up * 1.2f);
                        Capture(Camera.main, Shot(".bots.png"));
                        Finish(true, $"PLATFORM03 BOT CHECK PASS ({dead} infected down, {looted} loot taken, a bot walked ahead and followed the marked target, player health {trial.Health})");
                        return;
                    }
                    if (Time.time > stepTime + 25f)
                    {
                        string positions = "";
                        foreach (var bot in SurvivorBot.All)
                            positions += $" {bot.Label} {bot.transform.position} {bot.ActionLabel}";
                        Finish(false, $"Squad at {controller.transform.position}: {bots} bots, {dead} infected down, {looted} loot taken, ahead={botAhead}, followed focus={squadFollowedFocus}.{positions}");
                    }
                    break;
                }
                case 40:
                    if (UnityEngine.Object.FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None).Length < 2 ||
                        SquadMember.All.Count < 2)
                        break;
                    Debug.Log("NETCHECK client joined");
                    MovePastTriage(controller);
                    controller.Move(new Vector3(-2.85f, 0, 1.35f));
                    if (!trial.TryInteract() || trial.PhaseIndex != 1)
                    {
                        Finish(false, "Host could not answer the phone in a session.");
                        return;
                    }
                    stepTime = Time.time;
                    step = 41;
                    break;
                case 41:
                    if (!netSettled && trial.PhaseIndex == 2)
                    {
                        netSettled = true;
                        stepTime = Time.time;
                        Debug.Log("NETCHECK client read the register through the host");
                    }
                    else if (netSettled && Time.time > stepTime + 1.5f)
                    {
                        controller.Move(Vector3.forward * 4.4f);
                        controller.Move(Vector3.right * 6.0f);
                        if (!trial.TryInteract() || trial.PhaseIndex != 3)
                        {
                            Finish(false, $"Host could not open the staff gate (at {controller.transform.position}, phase {trial.PhaseIndex}).");
                            return;
                        }
                        Transform remote = null;
                        foreach (var survivor in UnityEngine.Object.FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None))
                            if (survivor != SurvivorAvatar.Mine)
                                remote = survivor.transform;
                        Debug.Log($"NETCHECK remote avatar at {remote.position}, renderers {remote.GetComponentsInChildren<Renderer>().Length} enabled {remote.GetComponentInChildren<Renderer>()?.enabled}");
                        netTarget = trial.SpawnInfected(remote.position.x, remote.position.z - 3.5f, false);
                        trial.SpawnInfected(remote.position.x + 1.2f, remote.position.z - 4.5f, false);
                        AimAtPoint(remote.position + Vector3.up * 1.2f);
                        stepTime = Time.time;
                        step = 42;
                    }
                    else if (!netSettled && Time.time > stepTime + 40f)
                    {
                        Finish(false, "The client's register interaction never reached the host.");
                        return;
                    }
                    break;
                case 42:
                    if (netTarget == null || netTarget.Dead)
                    {
                        foreach (var survivor in UnityEngine.Object.FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None))
                            if (survivor != SurvivorAvatar.Mine)
                                AimAtPoint(survivor.transform.position + Vector3.up * 1.2f);
                        Capture(Camera.main, Shot(".net.png"));
                        foreach (var member in SquadMember.All)
                            if (member.body != trial.transform)
                                member.hurt(500);
                        stepTime = Time.time;
                        step = 43;
                        return;
                    }
                    if (Time.time > stepTime + 30f)
                        Finish(false, "Client shots never killed the host's infected.");
                    break;
                case 43:
                {
                    SurvivorAvatar teammate = null;
                    foreach (var survivor in UnityEngine.Object.FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None))
                        if (survivor != SurvivorAvatar.Mine)
                            teammate = survivor;
                    if (teammate.Down && stepTime > 0)
                    {
                        Debug.Log("NETCHECK teammate is down");
                        Capture(Camera.main, Shot(".down.png"));
                        teammate.ReviveRpc();
                        stepTime = -Time.time;
                    }
                    else if (stepTime < 0 && !teammate.Down)
                    {
                        Finish(true, "PLATFORM03 NET CHECK PASS");
                        return;
                    }
                    else if (Time.time > Mathf.Abs(stepTime) + 10f)
                        Finish(false, $"Down/revive did not round-trip (down={teammate.Down}).");
                    break;
                }
                case 20 when Time.time >= stepTime + 1.4f:
                    controller.Move(Vector3.left * controller.transform.position.x);
                    controller.transform.rotation = Quaternion.Euler(0, 180f, 0);
                    special = trial.SpawnInfected(0, controller.transform.position.z - 5f, false, InfectedKind.Leaper);
                    stepTime = Time.time;
                    step++;
                    break;
                case 21:
                    if (trial.Held && healthBefore == 0)
                    {
                        healthBefore = 1;
                        stepTime = Time.time;
                    }
                    else if (trial.Held && Time.time > stepTime + 0.45f)
                    {
                        Capture(Camera.main, Shot(".leaper.png"));
                        for (int press = 0; press < 6; press++)
                            trial.StruggleFree();
                        if (trial.Held)
                        {
                            Finish(false, "Struggling did not free the player from the leaper.");
                            return;
                        }
                        special.Hit(9999);
                        special = trial.SpawnInfected(0, controller.transform.position.z - 6f, false, InfectedKind.Dragger);
                        stepTime = Time.time;
                        step++;
                    }
                    else if (!trial.Held && Time.time > stepTime + 6f)
                    {
                        Finish(false, $"Leaper never pinned the player (distance {Vector3.Distance(special.transform.position, controller.transform.position):F1}).");
                        return;
                    }
                    break;
                case 22:
                    if (trial.Held)
                    {
                        Capture(Camera.main, Shot(".dragger.png"));
                        special.Hit(50);
                        if (trial.Held)
                        {
                            Finish(false, "Shooting the dragger did not break its hold.");
                            return;
                        }
                        special.Hit(9999);
                        healthBefore = trial.Health;
                        special = trial.SpawnInfected(0, controller.transform.position.z - 4f, false, InfectedKind.Bloater);
                        stepTime = Time.time;
                        step++;
                    }
                    else if (Time.time > stepTime + 6f)
                    {
                        Finish(false, "Dragger never grabbed the player.");
                        return;
                    }
                    break;
                case 23:
                    if (special.Dead)
                    {
                        Capture(Camera.main, Shot(".bloater.png"));
                        if (trial.Threats.Count < 4)
                        {
                            Finish(false, "Bloater burst did not call a crowd.");
                            return;
                        }
                        foreach (var threat in trial.Threats)
                            if (threat != null && !threat.Dead)
                                threat.Hit(9999);
                        healthBefore = trial.Health;
                        special = trial.SpawnInfected(0, controller.transform.position.z - 7f, false, InfectedKind.Brute);
                        stepTime = Time.time;
                        step++;
                    }
                    else if (Time.time > stepTime + 8f)
                    {
                        Finish(false, "Bloater never reached the player.");
                        return;
                    }
                    break;
                case 24:
                    if (Time.time > stepTime + 0.9f && healthBefore > 0)
                    {
                        Capture(Camera.main, Shot(".brute.png"));
                        healthBefore = -healthBefore;
                    }
                    if (trial.Health <= Mathf.Abs(healthBefore) - 25)
                    {
                        Finish(true, "PLATFORM03 SPECIALS CHECK PASS");
                        return;
                    }
                    if (Time.time > stepTime + 8f)
                        Finish(false, $"Brute charge never connected (health {trial.Health}).");
                    break;
                case 6 when Time.time >= stepTime + 1.35f:
                    string gateName = useDrain ? "Drain gate" : "Staff gate";
                    string closedGateName = useDrain ? "Staff gate" : "Drain gate";
                    if (GameObject.Find(gateName).transform.position.y < 3.5f ||
                        GameObject.Find(closedGateName).transform.position.y > 1.4f)
                    {
                        Finish(false, "Power was not routed exclusively to the chosen gate.");
                        return;
                    }
                    string screenshot = SessionState.GetString(RunningKey + ".path", "");
                    Capture(Camera.main, Path.ChangeExtension(screenshot, ".route.png"));
                    if (!useDrain)
                        CaptureFacilities(Path.ChangeExtension(screenshot, ".facility.png"));
                    Physics.SyncTransforms();
                    controller.Move(Vector3.forward * 4.31f);
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-directorCheck") >= 0)
                    {
                        stepTime = Time.time;
                        step = 80;
                        break;
                    }
                    stepTime = Time.time;
                    step = 7;
                    break;
                case 7 when Time.time >= stepTime + 0.35f:
                    if (!useDrain && !trial.TryInteract())
                    {
                        Finish(false, "Staff passage supply could not be collected.");
                        return;
                    }
                    float innerLane = useDrain ? -2.05f : 2.05f;
                    controller.Move(Vector3.right * (innerLane - controller.transform.position.x));
                    controller.Move(Vector3.forward * (15.52f - controller.transform.position.z));
                    if (useDrain)
                        controller.Move(Vector3.right * (2.3f - controller.transform.position.x));
                    controller.Move(Vector3.forward * 0.38f);
                    if (liveDefenseMode)
                    {
                        var liveSettings = new SerializedObject(trial);
                        liveSettings.FindProperty("spawnSpecials").boolValue = true;
                        liveSettings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (!trial.TryInteract() || trial.Countdown <= 0)
                    {
                        Finish(false, $"Power and holdout failed after the route encounter at {controller.transform.position}.");
                        return;
                    }
                    controller.Move(Vector3.left * controller.transform.position.x);
                    controller.Move(Vector3.forward * 2.5f);
                    Vector3 visible = controller.transform.position + Vector3.forward * 7f;
                    if (trial.IsConcealedEntry(visible))
                    {
                        Finish(false, "Director accepted an exposed position in the open platform.");
                        return;
                    }
                    if (campaignMode)
                    {
                        if (!trial.PreparingDefense)
                        {
                            Finish(false, "Power cabinet did not start a separate preparation beat.");
                            return;
                        }
                        preparationThreats = trial.Threats.Count;
                        stepTime = Time.time;
                        step = 82;
                        break;
                    }
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-crowdCheck") >= 0)
                    {
                        approachStart = controller.transform.position;
                        var blocker = trial.SpawnInfected(approachStart.x, approachStart.z + 3.2f, false, calm: true);
                        blocker.enabled = false;
                        target = trial.SpawnInfected(approachStart.x, approachStart.z + 6.5f, false);
                        stepTime = Time.time;
                        step = 86;
                        break;
                    }
                    // Stage a deterministic hit/reload target independently of the live director.
                    target = trial.SpawnInfected(0.0f, controller.transform.position.z + 7f, false);
                    stepTime = Time.time;
                    step = 1;
                    break;
                case 80:
                    if (trial.Threats.Count == 0)
                    {
                        if (Time.time > stepTime + 3.5f)
                            Finish(false, "Director found no covered route after entering the transfer passage.");
                        break;
                    }
                    target = trial.Threats[0];
                    approachStart = target.transform.position;
                    if (Vector3.Distance(approachStart, controller.transform.position) < 5.5f)
                    {
                        Finish(false, $"Reinforcement appeared too close: {approachStart}.");
                        return;
                    }
                    stepTime = Time.time;
                    step = 81;
                    break;
                case 81 when Time.time >= stepTime + 2.0f:
                    float travel = Vector3.Distance(approachStart, target.transform.position);
                    if (travel < 0.8f)
                    {
                        Finish(false, $"Reinforcement did not advance from {approachStart} (travel={travel:F1}m).");
                        return;
                    }
                    Finish(true, $"PLATFORM03 COVERED ENTRY CHECK PASS (origin {approachStart}, travel {travel:F1}m)");
                    break;
                case 82 when Time.time >= stepTime + 3f:
                    if (!trial.PreparingDefense || trial.Threats.Count > preparationThreats)
                    {
                        Finish(false, "A new wave arrived during the defense preparation.");
                        return;
                    }
                    step = 83;
                    break;
                case 83:
                    int arrivals = trial.Threats.Count - preparationThreats;
                    if (!trial.PreparingDefense && arrivals >= (liveDefenseMode ? 14 : 1))
                    {
                        if (liveDefenseMode)
                            AimAtPoint(trial.Threats[trial.Threats.Count - 1].transform.position + Vector3.up * 1.25f);
                        Capture(Camera.main, Shot(".defense.png"));
                        Finish(true, $"PLATFORM03 CAMPAIGN PACING CHECK PASS (quiet preparation, then {arrivals} covered reinforcements)");
                        return;
                    }
                    if (Time.time > stepTime + 13f)
                        Finish(false, $"Only {arrivals} opening defense reinforcements arrived after preparation.");
                    break;
                case 86:
                    float approachDistance = Vector3.Distance(approachStart, target.transform.position);
                    if (approachDistance < 2.3f)
                    {
                        Finish(true, $"PLATFORM03 CROWD PURSUIT PASS (closed from 6.5m to {approachDistance:F1}m with an infected in the lane)");
                        return;
                    }
                    if (Time.time > stepTime + 3.5f)
                        Finish(false, $"Infected stalled behind its own crowd ({approachDistance:F1}m from player).");
                    break;
                case 88 when Time.time >= stepTime + 0.25f:
                    CaptureGripPose(posedRescuer);
                    break;
                case 1 when Time.time >= stepTime + 1.1f:
                    var rig = target.GetComponentInChildren<Animation>();
                    string moving = rig.IsPlaying("run") ? "run" : "walk";
                    if (!rig.IsPlaying(moving) || rig[moving].normalizedTime <= 0)
                    {
                        Finish(false, "The imported infected movement animation did not advance.");
                        return;
                    }
                    AimAt(target);
                    Capture(Camera.main, SessionState.GetString(RunningKey + ".path", ""));
                    var torch = GameObject.Find("Rescue flashlight").GetComponent<Light>();
                    torch.enabled = false;
                    Capture(Camera.main, Path.ChangeExtension(SessionState.GetString(RunningKey + ".path", ""), ".dark.png"));
                    torch.enabled = true;
                    startingRounds = trial.Rounds;
                    bool fired = trial.TryFire();
                    if (!fired || trial.Rounds != startingRounds - 1 || target.Dead ||
                        !target.GetComponent<InfectedBody>().IsFlinching)
                    {
                        Finish(false, $"First shot result: fired={fired}, rounds={startingRounds}->{trial.Rounds}, target dead={target.Dead}, health={trial.Health}.");
                        return;
                    }
                    stepTime = Time.time;
                    step = 85;
                    break;
                case 85 when Time.time >= stepTime + 0.055f:
                    float hitAngle = target.GetComponent<InfectedBody>().RenderedHitAngle;
                    if (hitAngle < 18f)
                    {
                        Finish(false, $"Nonlethal hit did not visibly bend the rendered pose ({hitAngle:F1} degrees).");
                        return;
                    }
                    Debug.Log($"PLATFORM03 RENDERED HIT POSE {hitAngle:F1} degrees");
                    Capture(Camera.main, Shot(".reaction.png"));
                    step = 2;
                    break;
                case 2 when Time.time >= stepTime + 0.23f:
                    AimAt(target);
                    if (!trial.TryFire() || !target.Dead || !target.GetComponent<InfectedBody>().Ragdolled ||
                        !trial.TryReload() || trial.TryFire())
                    {
                        Finish(false, "Hit detection, enemy defeat, or reload lockout failed.");
                        return;
                    }
                    stepTime = Time.time;
                    step++;
                    break;
                case 3 when Time.time >= stepTime + 1.4f:
                    AimAt(target);
                    Capture(Camera.main, Shot(".corpse.png"));
                    if (trial.Rounds != startingRounds)
                    {
                        Finish(false, "Reload did not refill the magazine.");
                        return;
                    }
                    controller.Move(Vector3.right * 2.8f);
                    controller.Move(Vector3.forward * Mathf.Max(0f, 27f - controller.transform.position.z));
                    controller.Move(Vector3.left * controller.transform.position.x);
                    controller.Move(Vector3.forward * 5f);
                    float playerZ = controller.transform.position.z;
                    if (playerZ < 29f || playerZ >= 30f || trial.Complete)
                    {
                        Finish(false, $"Expected to stop at the closed shutter (z={playerZ:F2}).");
                        return;
                    }
                    step++;
                    break;
                case 4:
                    foreach (var pursuing in trial.Threats)
                    {
                        if (pursuing != null && !pursuing.Dead && Vector3.Distance(
                            pursuing.transform.position, controller.transform.position) < 3.3f)
                        {
                            AimAt(pursuing);
                            if (!trial.TryFire() && trial.Rounds == 0)
                                trial.TryReload();
                            break;
                        }
                    }
                    Physics.SyncTransforms();
                    controller.Move(Vector3.forward * 0.11f);
                    if (controller.transform.position.z < 33.05f)
                        break;
                    CaptureWitness(Path.ChangeExtension(SessionState.GetString(RunningKey + ".path", ""), ".witness.png"));
                    if (trial.Complete || !trial.TryInteract())
                    {
                        Finish(false, "A player passed the shutter but could not start the required witness rescue.");
                        return;
                    }
                    step++;
                    break;
                case 5:
                    Vector3 toLift = new Vector3(-1.6f, controller.transform.position.y, 35.1f) -
                        controller.transform.position;
                    controller.Move(Vector3.ClampMagnitude(toLift, 0.11f));
                    if (trial.AnnexUnlocked && Vector3.Distance(controller.transform.position, new Vector3(-1.6f, 0, 35.1f)) < 0.18f)
                    {
                        if (trial.Complete)
                        {
                            Finish(false, "Mission ended before the maintenance annex.");
                            return;
                        }
                        stepTime = Time.time;
                        step = 90;
                    }
                    break;
                case 90:
                    ClearAnnexTestThreats(trial, controller);
                    MoveToward(controller, new Vector3(-1.6f, 0, 48.22f));
                    if (controller.transform.position.z < 48.18f)
                        break;
                    step = 91;
                    break;
                case 91:
                    ClearAnnexTestThreats(trial, controller);
                    MoveToward(controller, new Vector3(2.4f, 0, 48.22f));
                    if (controller.transform.position.x < 2.25f)
                        break;
                    AimAtPoint(new Vector3(3.15f, 0.95f, 48.4f));
                    Capture(Camera.main, Shot(".workshop.png"));
                    if (yardBossCheck)
                    {
                        var yardSettings = new SerializedObject(trial);
                        yardSettings.FindProperty("spawnSpecials").boolValue = true;
                        yardSettings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (!trial.TryInteract() || trial.Countdown <= 0)
                    {
                        Finish(false, "Annex pump did not start transfer defense.");
                        return;
                    }
                    step = 92;
                    break;
                case 92:
                    ClearAnnexTestThreats(trial, controller);
                    MoveToward(controller, new Vector3(-1.6f, 0, 50.1f));
                    if (trial.Countdown > 0)
                        break;
                    if (controller.transform.position.z >= 52.2f)
                    {
                        Finish(false, "Player crossed the loading gate ahead of the pump.");
                        return;
                    }
                    step = 93;
                    break;
                case 94:
                    if (!target.PreparingCall)
                    {
                        if (Time.time > stepTime + 5f)
                            Finish(false, "Caller did not telegraph a call.");
                        break;
                    }
                    if (trial.TryReserveSpecial(SquadMember.All[0]))
                    {
                        Finish(false, "Two specials were allowed to start together.");
                        return;
                    }
                    target.Hit(8);
                    if (target.PreparingCall)
                    {
                        Finish(false, "Caller kept charging after a hit.");
                        return;
                    }
                    stepTime = Time.time;
                    step = 95;
                    break;
                case 95 when Time.time > stepTime + 1.65f:
                    Finish(trial.Threats.Count == 1, "PLATFORM03 CALLER INTERRUPTION AND SPECIAL SPACING CHECK");
                    break;
                case 96:
                    var bossRig = target.GetComponentInChildren<Animation>();
                    var bossMove = bossRig["walk"];
                    var bossCharge = bossRig["slam"];
                    if (bossMove == null || bossCharge == null)
                    {
                        Finish(false, "The imported loading-yard boss clips are missing.");
                        return;
                    }
                    if (!string.IsNullOrEmpty(demoFrameFolder))
                    {
                        demoBossWalked |= bossMove.normalizedTime > 0.02f &&
                            Mathf.Abs(65.8f - target.transform.position.z) > 0.12f;
                        AimAtPoint(target.transform.position + Vector3.up * 1.30f);
                        RecordDemoFrame();
                        float elapsed = Time.time - stepTime;
                        if (elapsed > 0.6f && elapsed < 2.55f && !target.Dead)
                            trial.TryFire();
                        if (elapsed > 2.55f && !target.Dead)
                            target.Hit(9999);
                        if (elapsed > 3.8f)
                            Finish(demoBossWalked && target.Dead, "PLATFORM03 BOSS GAMEPLAY CAPTURE READY");
                        break;
                    }
                    if (bossMove.normalizedTime <= 0.02f && Time.time < stepTime + 5f)
                        break;
                    AimAtPoint(target.transform.position + Vector3.up * 1.36f);
                    Capture(Camera.main, Shot(".boss.png"));
                    float bossTravel = Mathf.Abs(approachStart.z - target.transform.position.z);
                    bool animate = bossMove.normalizedTime > 0.02f && bossTravel > 0.12f;
                    Debug.Log($"Loading boss motion: {bossMove.clip.name} at {bossMove.normalizedTime:F2}, travel {bossTravel:F2}m, {bossCharge.clip.name}.");
                    target.Hit(9999);
                    Finish(animate && target.Dead && target.GetComponent<InfectedBody>().Ragdolled,
                        "PLATFORM03 BOSS MODEL, ANIMATION AND RAGDOLL CHECK");
                    break;
                case 97:
                    if (target.Dead)
                    {
                        Finish(false, "The boss fell to 900 damage before throwing.");
                        return;
                    }
                    AimAtPoint(target.transform.position + Vector3.up * 2.2f);
                    var chunk = GameObject.Find("Concrete chunk");
                    if (chunk != null && !sawBossChunk)
                    {
                        sawBossChunk = true;
                        stepTime = Time.time;
                    }
                    if (chunk != null && !capturedBossLift && Time.time > stepTime + 0.86f)
                    {
                        Capture(Camera.main, Shot(".lift.png"));
                        capturedBossLift = true;
                        if (bossDodgeCheck)
                        {
                            controller.Move(Vector3.left * 2.8f);
                            bossDodged = true;
                        }
                        if (bossCoverCheck)
                        {
                            var cover = new GameObject("Temporary throw test cover");
                            cover.transform.position = controller.transform.position + Vector3.forward * 1.1f + Vector3.up * 1.5f;
                            cover.AddComponent<BoxCollider>().size = new Vector3(2.4f, 3f, 0.3f);
                            Physics.SyncTransforms();
                        }
                    }
                    if (chunk == null && sawBossChunk)
                    {
                        int damage = healthBefore - trial.Health;
                        Capture(Camera.main, Shot(".throw.png"));
                        bool protectedPlayer = bossDodgeCheck ? bossDodged : bossCoverCheck;
                        Finish(protectedPlayer ? damage == 0 : damage >= 25,
                            $"PLATFORM03 BOSS THROW CHECK (dodge {bossDodgeCheck}, cover {bossCoverCheck}, damage {damage})");
                    }
                    else if (Time.time > stepTime + 6f)
                        Finish(false, "The concrete throw did not complete.");
                    break;
                case 99 when Time.time >= stepTime + 0.35f:
                    int pushed = 0;
                    for (int i = 0; i < ShoveTargets.Length; i++)
                        if (Vector3.Distance(ShoveTargets[i].transform.position, ShoveOrigins[i]) >= 0.6f &&
                            ShoveTargets[i].transform.position.z > ShoveOrigins[i].z)
                            pushed++;
                    bool followup = trial.TryFire();
                    Finish(pushed == 3 && followup, $"PLATFORM03 SHOVE CHECK (pushed {pushed}/3, follow-up shot {followup})");
                    break;
                case 98:
                    ClearAnnexTestThreats(trial, controller);
                    if (target == null || target.Dead)
                    {
                        int attacks = target != null ? target.BossAttacksCommitted : 0;
                        bool success = attacks >= 2 && yardBotEngaged && yardSawDebris && yardShots >= 10 &&
                            Time.time - stepTime >= 30f;
                        Finish(success, $"PLATFORM03 LOADING BOSS CHECK (attacks {attacks}, threw debris {yardSawDebris}, bots engaged {yardBotEngaged}, shots {yardShots}, lasted {Time.time - stepTime:F1}s)");
                        break;
                    }
                    if (Time.time > stepTime + 42f)
                    {
                        Finish(false, $"Loading boss alive after 42s, {target.BossAttacksCommitted} attacks, {yardShots} shots, {target.RemainingHealth} boss HP at {target.transform.position}, player at {controller.transform.position}");
                        break;
                    }
                    foreach (var ally in SurvivorBot.All)
                        yardBotEngaged |= ally.AimedTarget == target;
                    if (target.BossDebrisInFlight && !yardSawDebris)
                    {
                        yardSawDebris = true;
                        Capture(Camera.main, Shot(".yard-throw.png"));
                    }
                    // Batch play skips the input-driven local gravity and movement.
                    controller.Move(Vector3.down * (9f * Time.deltaTime));
                    float lane = -2.45f + Mathf.Sin((Time.time - stepTime) * 1.45f) * 0.95f;
                    MoveToward(controller, new Vector3(lane, 0, 57.25f));
                    AimAtPoint(target.transform.position + Vector3.up * 1.6f);
                    if (trial.TryFire())
                        yardShots++;
                    else if (trial.Rounds == 0)
                        trial.TryReload();
                    // Four headshot AK streams at full cyclic rate, without reload gaps.
                    if (Time.time >= nextSquadBurst)
                    {
                        nextSquadBurst = Time.time + 0.105f;
                        for (int gun = 0; gun < 4; gun++)
                            target.Hit(68);
                    }
                    if (trial.Health < 75 && !trial.Downed)
                        trial.Heal(60);
                    break;
                case 93:
                    if (yardBossCheck)
                    {
                        MoveToward(controller, new Vector3(-1.6f, 0, 55.6f));
                        if (controller.transform.position.z < 55.5f)
                            break;
                        foreach (var infected in trial.Threats)
                            if (infected != null && infected.Kind == InfectedKind.Brute)
                                target = infected;
                        if (target == null || target.Dead)
                        {
                            Finish(false, "Production loading bay did not spawn a live boss.");
                            return;
                        }
                        var arenaSettings = new SerializedObject(trial);
                        var prefab = (SurvivorBot)arenaSettings.FindProperty("botPrefab").objectReferenceValue;
                        for (int slot = 0; slot < 3; slot++)
                        {
                            var position = controller.transform.position + SurvivorBot.OpeningOffset(slot);
                            if (UnityEngine.AI.NavMesh.SamplePosition(position, out var landing, 2f, UnityEngine.AI.NavMesh.AllAreas))
                                position = landing.position;
                            var ally = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                            ally.Join(trial, slot);
                        }
                        trial.Equip(4);
                        trial.GiveAmmo();
                        stepTime = Time.time;
                        step = 98;
                        break;
                    }
                    ClearAnnexTestThreats(trial, controller);
                    MoveToward(controller, new Vector3(-1.6f, 0, 66.8f));
                    if (controller.transform.position.z < 66.45f || !trial.Complete)
                        break;
                    AimAtPoint(new Vector3(-1.6f, 1.8f, 68.3f));
                    Capture(Camera.main, Shot(".loading.png"));
                    Finish(true, "PLATFORM03 MISSION PLAY CHECK PASS (annex pump, loading bay escort and extraction)");
                    break;
            }
        }

        static void ClearAnnexTestThreats(StationTrial trial, CharacterController controller)
        {
            foreach (var infected in trial.Threats)
                if (infected != null && !infected.Dead && (!yardBossCheck || infected.Kind != InfectedKind.Brute) &&
                    Vector3.Distance(infected.transform.position, controller.transform.position) < 5f)
                    infected.Hit(1800);
        }

        static void MoveToward(CharacterController controller, Vector3 point)
        {
            point.y = controller.transform.position.y;
            controller.Move(Vector3.ClampMagnitude(point - controller.transform.position, Time.deltaTime * 4f));
        }

        static Vector3 TriagePathStep(Vector3 at, float finish, float lane = 2.55f)
        {
            if (at.z < -14.3f && Mathf.Abs(at.x - lane) > 0.12f)
                return new Vector3(lane, at.y, at.z);
            if (at.z < -9.0f)
                return new Vector3(lane, at.y, -8.9f);
            if (Mathf.Abs(at.x - 0.4f) > 0.12f && at.z < -4.0f)
                return new Vector3(0.4f, at.y, at.z);
            return new Vector3(0.4f, at.y, finish);
        }

        static void MovePastTriage(CharacterController controller)
        {
            float lane = Array.IndexOf(Environment.GetCommandLineArgs(), "-triageLeft") >= 0 ? -3.78f : 2.55f;
            for (int i = 0; i < 4; i++)
                controller.Move(TriagePathStep(controller.transform.position, 0.25f, lane) - controller.transform.position);
        }

        static void CaptureGripPose(SurvivorBot bot)
        {
            Transform right = null, left = null, trigger = null, fore = null;
            foreach (var joint in bot.GetComponentsInChildren<Transform>())
            {
                switch (joint.name)
                {
                    case "Bip01 R Hand": right = joint; break;
                    case "Bip01 L Hand": left = joint; break;
                    case "Trigger hand contact": trigger = joint; break;
                    case "Support hand under fore-end": fore = joint; break;
                }
            }
            if (right == null || left == null || trigger == null || fore == null)
            {
                Finish(false, "Rescuer grip sockets or rig bones were missing.");
                return;
            }

            float triggerGap = Vector3.Distance(right.position, trigger.position);
            float supportGap = Vector3.Distance(left.position, fore.position);
            Camera camera = Camera.main;
            Vector3 center = (trigger.position + fore.position) * 0.5f + Vector3.up * 0.13f;
            camera.transform.position = center + bot.transform.right * 1.43f + bot.transform.forward * 0.95f + Vector3.up * 0.05f;
            AimAtPoint(center);
            foreach (Transform child in camera.transform)
                child.gameObject.SetActive(false);
            Capture(camera, Shot(".grip.png"));
            bool aligned = triggerGap < 0.11f && supportGap < 0.11f;
            Finish(aligned, $"PLATFORM03 GRIP {(aligned ? "PASS" : "FAIL")} (trigger gap {triggerGap:F3} m, support gap {supportGap:F3} m)");
        }

        static void AimAt(StationInfected infected)
        {
            Physics.SyncTransforms();
            Camera camera = Camera.main;
            Vector3 direction = infected.transform.position + Vector3.up * 1.17f - camera.transform.position;
            camera.transform.rotation = Quaternion.LookRotation(direction);
        }

        static void CaptureOpeningPlate(Camera camera, string path)
        {
            camera.transform.SetPositionAndRotation(new Vector3(-0.9f, 1.82f, -20.45f), Quaternion.Euler(0f, 4f, 0f));
            camera.fieldOfView = 57f;
            foreach (Transform child in camera.transform)
                child.gameObject.SetActive(false);
            Capture(camera, path);
        }

        /// <summary>Edit-mode render of each viewmodel in turn: -stationCapture prefix → prefix.N.png.</summary>
        public static void CaptureArsenal()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string prefix = arguments[Array.IndexOf(arguments, CaptureArgument) + 1];
            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            Camera camera = Camera.main;
            camera.transform.parent.SetPositionAndRotation(new Vector3(1.4f, 0, 5.5f), Quaternion.Euler(0, 160f, 0));
            var views = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in camera.transform)
                if (child.name.EndsWith("viewmodel"))
                    views.Add(child);
            for (int index = 0; index < views.Count; index++)
            {
                foreach (var view in views)
                    view.gameObject.SetActive(view == views[index]);
                Capture(camera, $"{prefix}.{index}.png");
            }
            EditorApplication.Exit(0);
        }

        static void AimAtPoint(Vector3 point)
        {
            Transform view = Camera.main.transform;
            view.rotation = Quaternion.LookRotation(point - view.position);
        }

        static string Shot(string suffix) =>
            Path.ChangeExtension(SessionState.GetString(RunningKey + ".path", ""), suffix);

        static void CaptureWitness(string path)
        {
            var witness = GameObject.Find("Seo I-gyeong").transform;
            Transform view = Camera.main.transform;
            Vector3 savedPosition = view.position;
            Quaternion savedRotation = view.rotation;
            Vector3 point = witness.position + witness.forward * 1.9f + witness.right * 0.5f + Vector3.up * 1.6f;
            view.SetPositionAndRotation(point, Quaternion.LookRotation(witness.position + Vector3.up * 1.25f - point));
            Capture(Camera.main, path);
            view.SetPositionAndRotation(savedPosition, savedRotation);
        }

        static void CaptureFacilities(string path)
        {
            Camera camera = Camera.main;
            Transform view = camera.transform;
            Vector3 savedPosition = view.position;
            Quaternion savedRotation = view.rotation;
            try
            {
                Vector3 point = new(2.3f, 1.54f, 13.85f);
                view.SetPositionAndRotation(point, Quaternion.LookRotation(new Vector3(3.90f, 1.10f, 17.0f) - point));
                Capture(camera, path);
            }
            finally
            {
                view.SetPositionAndRotation(savedPosition, savedRotation);
            }
        }

        static void RecordDemoFrame()
        {
            if (string.IsNullOrEmpty(demoFrameFolder) || demoFrame >= 114 || Time.time < nextDemoFrame)
                return;
            nextDemoFrame = Time.time + 0.10f;
            Capture(Camera.main, Path.Combine(demoFrameFolder, $"frame-{demoFrame++:D4}.png"), 720, 405);
        }

        static void Capture(Camera camera, string path, int width = 1280, int height = 720)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var original = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = original;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Check;
            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(RunningKey + ".path");
            SessionState.EraseString(RunningKey + ".demo");
            if (passed)
                Debug.Log(message);
            else
                Debug.LogError(message);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
