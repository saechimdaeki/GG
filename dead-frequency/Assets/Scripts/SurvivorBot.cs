using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ResidualSignal
{
    /// <summary>
    /// Solo-play teammate with a life of its own: roams around the player (ahead and to the sides too), keeps
    /// watch, scavenges ammo and first-aid kits, shares them, calls things out, fights what is coming for the
    /// squad, frees a pinned player and picks the player up.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SurvivorBot : MonoBehaviour
    {
        [SerializeField] Animation rig;
        [SerializeField] Material[] alternateCoats;
        [SerializeField] Material[] alternateHelmets;
        [SerializeField] AnimationClip idleClip;
        [SerializeField] AnimationClip walkClip;
        [SerializeField] AnimationClip runClip;
        [Tooltip("Per squad slot: a Mixamo character that replaces the default rescuer model.")]
        [SerializeField] GameObject[] survivorModels;
        [SerializeField] AnimationClip rifleIdle;
        [SerializeField] AnimationClip rifleWalk;
        [SerializeField] AnimationClip rifleRun;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip shot;
        [SerializeField] Light muzzle;
        [Tooltip("Rifle pivot carried at the shoulder; hands are solved onto it every frame.")]
        [SerializeField] Transform rifle;
        [SerializeField] Transform stock;
        [SerializeField] Transform triggerGrip;
        [SerializeField] Transform supportGrip;
        [Tooltip("Only infected this close are engaged; the player gets the first look at everything further out.")]
        [SerializeField] float range = 12f;
        [SerializeField] float fireInterval = 0.38f;
        [SerializeField] int damage = 22;
        [Range(0, 1)] [SerializeField] float accuracy = 0.45f;
        [Tooltip("Seconds a target must stay in view before the bot opens fire.")]
        [SerializeField] float reaction = 0.6f;
        [SerializeField] int magazine = 30;

        static readonly string[] Names = { "민호", "수아", "태식" };
        static float nextAnyCallout;

        public static readonly List<SurvivorBot> All = new();
        public bool Down { get; private set; }
        public bool IsHeld => heldBy != null;
        public string Label { get; private set; }
        public int Health => health;
        public StationInfected AimedTarget => aim;
        public string ActionLabel => Down ? "쓰러짐" : heldBy != null ? "붙잡힘" : task switch
        {
            Task.Revive => "팀원 구조",
            Task.Heal => "치료 중",
            Task.Loot => "보급 수색",
            _ => aim != null ? "교전" : agent != null && agent.velocity.sqrMagnitude > 0.25f ? "이동" : "경계",
        };

        public static Vector3 FormationOffset(int slot) => slot switch
        {
            0 => new(-2.7f, 0, 4.2f),
            1 => new(2.9f, 0, 2.4f),
            _ => new(-2.6f, 0, -1.2f),
        };

        public static Vector3 OpeningOffset(int slot) => slot switch
        {
            0 => new(-1.65f, 0, -0.85f),
            1 => new(1.65f, 0, -1.1f),
            _ => new(0.2f, 0, -2.4f),
        };

        enum Task { Roam, Loot, Heal, Revive }
        Task task;
        NavMeshAgent agent;
        StationTrial leader;
        SquadMember member;
        StationInfected aim;
        LootItem loot;
        float taskTimer;
        float nextRoam;
        float nextLook;
        float aimSince;
        float nextShot;
        float nextShove;
        float lootDeadline;
        float scanPhase;
        float reloadUntil;
        int rounds;
        float flashUntil;
        float calmSince;
        float nextCallout;
        Vector3 formationOffset;
        Vector3 lastLeaderPosition;
        Vector3 heading = Vector3.forward;
        NavMeshPath formationPath;
        bool hasMedkit;
        int health = 150;
        StationInfected heldBy;
        readonly HashSet<StationInfected> calledOut = new();
        Transform leftArm, rightArm, leftFore, rightFore, leftHand, rightHand, leftKnuckle, rightKnuckle;
        Quaternion restRotation;
        Quaternion aimedRifleRotation;
        Vector3 restRigPosition;
        Transform hips;
        Vector3 impactVelocity;
        float impactHeight;
        float impactUntil;
        float gaitSpeed;
        float readyWeight;
        int squadSlot;
        Transform[] armBones;
        Quaternion[] animatedArmPose;
        bool armPoseApplied;
        string gait = "idle";

        public void Join(StationTrial player, int slot)
        {
            leader = player;
            Label = Names[slot % Names.Length];
            agent = GetComponent<NavMeshAgent>();
            squadSlot = slot;
            if (survivorModels != null && slot < survivorModels.Length && survivorModels[slot] != null)
                SwapModel(survivorModels[slot]);
            else if (slot > 0 && slot <= alternateCoats.Length && slot <= alternateHelmets.Length)
                ApplyUniform(alternateCoats[slot - 1], alternateHelmets[slot - 1]);
            formationOffset = FormationOffset(slot);
            scanPhase = slot * 1.8f;
            lastLeaderPosition = leader.transform.position;
            formationPath = new NavMeshPath();
            agent.speed = 4.4f;
            agent.updateRotation = false;
            agent.acceleration = 7.5f;
            agent.stoppingDistance = 0.72f;
            agent.avoidancePriority = 36 + slot * 8;
            agent.radius = 0.3f;
            rounds = magazine;
            restRotation = rig.transform.localRotation;
            restRigPosition = rig.transform.localPosition;
            aimedRifleRotation = rifle != null ? rifle.rotation : transform.rotation;
            hips = rig.GetComponentInChildren<SkinnedMeshRenderer>()?.rootBone;
            foreach (var bone in rig.GetComponentsInChildren<Transform>())
            {
                switch (bone.name)
                {
                    case "Bip01 L UpperArm": leftArm = bone; break;
                    case "Bip01 R UpperArm": rightArm = bone; break;
                    case "Bip01 L Forearm": leftFore = bone; break;
                    case "Bip01 R Forearm": rightFore = bone; break;
                    case "Bip01 L Hand": leftHand = bone; break;
                    case "Bip01 R Hand": rightHand = bone; break;
                    case "Bip01 L Finger2": leftKnuckle = bone; break;
                    case "Bip01 R Finger2": rightKnuckle = bone; break;
                    // Mixamo rig equivalents.
                    case "mixamorig:LeftArm": leftArm = bone; break;
                    case "mixamorig:RightArm": rightArm = bone; break;
                    case "mixamorig:LeftForeArm": leftFore = bone; break;
                    case "mixamorig:RightForeArm": rightFore = bone; break;
                    case "mixamorig:LeftHand": leftHand = bone; break;
                    case "mixamorig:RightHand": rightHand = bone; break;
                    case "mixamorig:LeftHandMiddle1": leftKnuckle = bone; break;
                    case "mixamorig:RightHandMiddle1": rightKnuckle = bone; break;
                }
            }
            armBones = new[] { rightArm, rightFore, rightHand, leftArm, leftFore, leftHand };
            animatedArmPose = new Quaternion[armBones.Length];
            rig.AddClip(idleClip, "idle");
            rig.AddClip(walkClip, "walk");
            rig.AddClip(runClip, "run");
            rig.wrapMode = WrapMode.Loop;
            rig.Play("idle");
            member = new SquadMember
            {
                body = transform,
                hurt = Hurt,
                knock = TakeImpact,
                warn = _ => { },
                pin = Grab,
                drag = Grab,
                release = _ => heldBy = null,
                blind = _ => Say("앞이 안 보여!"),
            };
            SquadMember.All.Add(member);
            SquadCollision.Register(GetComponent<Collider>());
            All.Add(this);
            nextRoam = Time.time + slot * 0.25f;
        }

        /// <summary>Replaces the default rescuer body with a Mixamo survivor carrying rifle locomotion clips.</summary>
        void SwapModel(GameObject model)
        {
            Transform old = rig.transform;
            while (old.parent != transform)
                old = old.parent;
            var body = Instantiate(model, transform);
            body.transform.SetLocalPositionAndRotation(old.localPosition, Quaternion.identity);
            Destroy(old.gameObject);
            rig = body.GetComponent<Animation>();
            if (rifleIdle != null) idleClip = rifleIdle;
            if (rifleWalk != null) walkClip = rifleWalk;
            if (rifleRun != null) runClip = rifleRun;
        }

                void ApplyUniform(Material coat, Material helmet)
        {
            foreach (var renderer in rig.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                        continue;
                    if (materials[i].name == "Rescuer - turnout coat")
                        materials[i] = coat;
                    else if (materials[i].name == "Rescuer - helmet")
                        materials[i] = helmet;
                }
                renderer.sharedMaterials = materials;
            }
        }

        void OnDestroy()
        {
            ReleaseLoot();
            SquadMember.All.Remove(member);
            SquadCollision.Unregister(GetComponent<Collider>());
            All.Remove(this);
        }

        bool Grab(StationInfected source)
        {
            heldBy = source;
            Say(source.Kind == InfectedKind.Leaper ? "덮쳤어! 도와줘!" : "끌려간다!", true);
            return true;
        }

        void TakeImpact(Vector3 push, int amount)
        {
            if (Down)
                return;
            impactVelocity = push;
            impactHeight = 0f;
            impactUntil = Time.time + Mathf.Max(0.46f, 2f * push.y / 18f + 0.15f);
            Hurt(amount);
        }

        void Hurt(int amount) => Wound(Difficulty.Hurt(amount));

        float nextFriendlyComplaint;

        /// <summary>The player shot this bot (Advanced/Expert friendly fire); damage is already scaled.</summary>
        public void TakeFriendlyFire(int amount)
        {
            if (Time.time >= nextFriendlyComplaint)
            {
                nextFriendlyComplaint = Time.time + 4f;
                Say("아군이야! 조준 똑바로 해!", true);
            }
            Wound(amount);
        }

        void Wound(int amount)
        {
            if (Down)
                return;
            health = Mathf.Max(0, health - amount);
            if (health > 0)
                return;
            Down = true;
            member.down = true;
            if (heldBy != null)
                heldBy.ReleaseHold();
            ReleaseLoot();
            if (agent.isOnNavMesh)
                agent.ResetPath();
            Say("쓰러졌어! 일으켜 줘!", true);
            leader.CheckSoloWipe();
        }

        /// <summary>The player held E next to this downed bot.</summary>
        public void Revive()
        {
            Down = false;
            member.down = false;
            health = 60;
            Say("고마워, 살았다.", true);
        }

        /// <summary>Speak a line (shown as a subtitle with the speaker's name). Urgent lines skip the cooldown.</summary>
        void Say(string line, bool urgent = false)
        {
            if (!urgent && (Time.time < nextCallout || Time.time < nextAnyCallout))
                return;
            nextCallout = Time.time + 7f;
            nextAnyCallout = Time.time + 2.5f;
            leader.Say(Label, line);
        }

        void Update()
        {
            if (leader == null || !agent.isOnNavMesh)
                return;
            if (armPoseApplied)
            {
                for (int i = 0; i < armBones.Length; i++)
                    if (armBones[i] != null)
                        armBones[i].localRotation = animatedArmPose[i];
                armPoseApplied = false;
            }
            TrackLeaderMovement();
            muzzle.enabled = Time.time < flashUntil;
            rig.transform.localRotation = Quaternion.Euler(Down ? -80f : 0f, 0f, 0f) * restRotation;
            if (Time.time < impactUntil)
            {
                agent.isStopped = true;
                float delta = Time.deltaTime;
                Vector3 slide = Vector3.ProjectOnPlane(impactVelocity, Vector3.up);
                agent.Move(slide * delta);
                impactHeight = Mathf.Max(0f, impactHeight + impactVelocity.y * delta);
                impactVelocity = Vector3.MoveTowards(slide, Vector3.zero, delta * 12f) +
                    Vector3.up * (impactVelocity.y - 18f * delta);
                rig.transform.localRotation = Quaternion.Euler(Down ? -80f : -38f, 0f, 11f) * restRotation;
                Play("idle");
                return;
            }
            impactHeight = 0f;
            if (Down)
            {
                agent.isStopped = true;
                Play("idle");
                return;
            }
            agent.isStopped = false;
            if (heldBy != null && heldBy.Dead)
                heldBy = null;
            if (heldBy != null)
            {
                agent.ResetPath();
                if (heldBy.Kind == InfectedKind.Dragger)
                {
                    Vector3 pull = Vector3.ProjectOnPlane(heldBy.transform.position - transform.position, Vector3.up);
                    if (pull.sqrMagnitude > 1f)
                        agent.Move(pull.normalized * (2.8f * Time.deltaTime));
                }
                rig.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f) * restRotation;
                Play("idle");
                return;
            }

            if (Time.time > nextLook)
            {
                nextLook = Time.time + 0.25f;
                var next = PickTarget();
                if (next != aim)
                    aimSince = Time.time;
                aim = next;
                if (aim != null && aim.Kind != InfectedKind.Common && calledOut.Add(aim))
                    Say(aim.Kind switch
                    {
                        InfectedKind.Leaper => "덮치개다!",
                        InfectedKind.Dragger => "끌개 조심해!",
                        InfectedKind.Bloater => "부풀이다, 가까이서 쏘지 마!",
                        InfectedKind.Caller => "울음꾼이다. 소리 지르기 전에 막아!",
                        _ => "거대체다! 흩어져!",
                    }, true);
            }
            if (aim != null)
                calmSince = Time.time;

            // Recover from a missed doorway only once the squad has moved well beyond the bot.
            if (Vector3.Distance(transform.position, leader.transform.position) > 26f &&
                !Visible(leader.transform.position + Vector3.up) &&
                NavMesh.SamplePosition(leader.transform.position - heading * 3f, out var recovery, 2f, agent.areaMask))
                agent.Warp(recovery.position);

            ChooseTask();
            switch (task)
            {
                case Task.Revive: GoTo(leader.transform.position, 1.0f); break;
                case Task.Heal: GoTo(leader.transform.position, 1.2f); break;
                case Task.Loot when loot != null: GoTo(loot.transform.position, 0.8f); break;
                default: Roam(); break;
            }
            Act();
            Animate();
        }

        void ChooseTask()
        {
            if (leader.Downed)
            {
                if (task != Task.Revive)
                {
                    ReleaseLoot();
                    taskTimer = 0;
                    Say("버텨! 지금 간다!", true);
                }
                task = Task.Revive;
                return;
            }
            if (task == Task.Revive)
                task = Task.Roam;
            if (hasMedkit && leader.Health < 45 && aim == null)
            {
                if (task != Task.Heal)
                {
                    ReleaseLoot();
                    taskTimer = 0;
                    Say("치료해 줄게, 가만히 있어.");
                }
                task = Task.Heal;
                return;
            }
            if (task == Task.Heal && (!hasMedkit || leader.Health >= 45))
                task = Task.Roam;
            if (task == Task.Loot && (loot == null || !loot.Available || aim != null || Time.time > lootDeadline))
            {
                ReleaseLoot();
                task = Task.Roam;
                calmSince = Time.time;
            }
            // Scavenge only when things are quiet and the item is near the squad.
            if (task == Task.Roam && aim == null && !leader.OpeningStaging && Time.time > calmSince + 3f)
            {
                LootItem best = null;
                float bestDistance = 9f;
                foreach (var item in LootItem.All)
                {
                    if (item.claimedBy != null || (item.kind == LootItem.Kind.Medkit && hasMedkit))
                        continue;
                    float distance = Vector3.Distance(item.transform.position, transform.position);
                    if (distance < bestDistance && Vector3.Distance(item.transform.position, leader.transform.position) < 12f &&
                        agent.CalculatePath(item.transform.position, formationPath) && formationPath.status == NavMeshPathStatus.PathComplete)
                    {
                        bestDistance = distance;
                        best = item;
                    }
                }
                if (best != null)
                {
                    loot = best;
                    loot.claimedBy = this;
                    task = Task.Loot;
                    taskTimer = 0;
                    lootDeadline = Time.time + 8f;
                }
            }
        }

        void ReleaseLoot()
        {
            if (loot != null && loot.claimedBy == this)
                loot.claimedBy = null;
            loot = null;
        }

        void TrackLeaderMovement()
        {
            Vector3 delta = leader.transform.position - lastLeaderPosition;
            if (delta.x * delta.x + delta.z * delta.z < 0.45f * 0.45f)
                return;
            delta.y = 0;
            heading = Vector3.Slerp(heading, delta.normalized, 0.52f).normalized;
            lastLeaderPosition = leader.transform.position;
        }

        void Roam()
        {
            float fromLeader = Vector3.Distance(transform.position, leader.transform.position);
            if (aim != null && fromLeader < 9f)
            {
                Vector3 away = Vector3.ProjectOnPlane(transform.position - aim.transform.position, Vector3.up);
                if (Time.time >= nextRoam && away.sqrMagnitude > 0.01f && away.sqrMagnitude < 8.4f)
                {
                    nextRoam = Time.time + 0.85f;
                    Vector3 lateral = Vector3.Cross(Vector3.up, away.normalized) * Mathf.Sign(formationOffset.x);
                    SetFormationPath(transform.position + away.normalized * 2.35f + lateral * 1.55f, 1.2f);
                }
                return;
            }
            if (!agent.hasPath && aim == null)
            {
                float sweep = Mathf.Sin(Time.time * 0.7f + scanPhase) * 20f;
                float sector = formationOffset.z < 0 ? 150f : Mathf.Sign(formationOffset.x) * 37f;
                Vector3 watch = Quaternion.Euler(0, sector + sweep, 0) * heading;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(watch), Time.deltaTime * 2.8f);
            }
            if (Time.time < nextRoam)
                return;
            nextRoam = Time.time + 0.62f;
            Vector3 leaderPosition = leader.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            Vector3 offset = leader.OpeningStaging ? OpeningOffset(squadSlot) : formationOffset;
            Vector3 point = leaderPosition + side * offset.x + heading * offset.z;
            if (!SetFormationPath(point, 1.5f))
                SetFormationPath(leaderPosition - heading * 1.5f + side * Mathf.Sign(formationOffset.x) * 0.65f, 1.6f);
        }

        bool SetFormationPath(Vector3 point, float radius)
        {
            if (!NavMesh.SamplePosition(point, out var hit, radius, agent.areaMask) ||
                !agent.CalculatePath(hit.position, formationPath) || formationPath.status != NavMeshPathStatus.PathComplete)
                return false;

            if (Vector3.Distance(transform.position, hit.position) < 0.95f)
            {
                if (agent.hasPath)
                    agent.ResetPath();
                return true;
            }
            if (!agent.hasPath || Vector3.Distance(agent.destination, hit.position) > 0.85f)
                agent.SetPath(formationPath);
            return true;
        }

        void GoTo(Vector3 point, float within)
        {
            if (Vector3.Distance(transform.position, point) > within)
                agent.SetDestination(point);
            else if (agent.hasPath)
                agent.ResetPath();
        }

        void Act()
        {
            bool arrived = !agent.hasPath || agent.remainingDistance < 0.5f;
            switch (task)
            {
                case Task.Revive when arrived && Vector3.Distance(transform.position, leader.transform.position) < 1.6f:
                    taskTimer += Time.deltaTime;
                    if (taskTimer >= 3f)
                    {
                        taskTimer = 0;
                        leader.Revive();
                        Say("일어나! 아직 안 끝났어.", true);
                        task = Task.Roam;
                    }
                    return;
                case Task.Heal when arrived && Vector3.Distance(transform.position, leader.transform.position) < 1.8f:
                    taskTimer += Time.deltaTime;
                    if (taskTimer >= 2.5f)
                    {
                        taskTimer = 0;
                        hasMedkit = false;
                        leader.MedkitHeal(50);
                        Say("됐어, 붕대 감았어.", true);
                        task = Task.Roam;
                    }
                    return;
                case Task.Loot when loot != null && Vector3.Distance(transform.position, loot.transform.position) < 1.2f:
                    taskTimer += Time.deltaTime;
                    Vector3 toward = Vector3.ProjectOnPlane(loot.transform.position - transform.position, Vector3.up);
                    if (toward.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toward), Time.deltaTime * 6f);
                    if (taskTimer >= 1.3f)
                        TakeLoot();
                    return;
            }
            if (aim != null)
                Fight();
        }

        void TakeLoot()
        {
            var item = loot;
            loot = null;
            task = Task.Roam;
            taskTimer = 0;
            item.Take();
            if (item.kind == LootItem.Kind.Medkit)
            {
                if (health < 90)
                {
                    health = Mathf.Min(150, health + 60);
                    Say("구급상자 썼어.");
                }
                else
                {
                    hasMedkit = true;
                    Say("구급상자 챙겼어. 다치면 말해.");
                }
                return;
            }
            if (leader.AmmoLow)
            {
                leader.GiveAmmo();
                Say("탄약 찾았어, 받아!", true);
            }
            else
                Say("탄약 챙겼어.");
        }

        void Fight()
        {
            Vector3 look = aim.transform.position - transform.position;
            look.y = 0;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 10f);
            if (look.magnitude < 1.25f && Time.time > nextShove)
            {
                nextShove = Time.time + 1.45f;
                nextShot = Mathf.Max(nextShot, Time.time + 0.14f);
                aim.Shove(look.normalized);
                return;
            }
            if (Time.time < reloadUntil)
                return;
            if (rounds == 0)
            {
                rounds = magazine;
                reloadUntil = Time.time + 2f;
                Say("재장전!");
                return;
            }
            float distance = look.magnitude;
            float response = distance < 3.6f ? reaction * 0.42f : reaction;
            if (Time.time > nextShot && Time.time > aimSince + response && Vector3.Angle(transform.forward, look) < 28f)
                Fire(distance);
        }

        void Animate()
        {
            float delta = Time.deltaTime;
            Vector3 velocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
            gaitSpeed = Mathf.MoveTowards(gaitSpeed, velocity.magnitude, delta * 8f);
            float leaderDistance = Vector3.Distance(transform.position, leader.transform.position);
            float catchUp = Mathf.InverseLerp(3.6f, 8.2f, leaderDistance);
            agent.speed = task is Task.Revive or Task.Heal ? 4f : Mathf.Lerp(1.85f, 4.8f, catchUp);
            if (aim != null && Vector3.Distance(transform.position, aim.transform.position) < 3.2f)
                agent.speed = Mathf.Max(2.55f, agent.speed);
            if (aim == null && velocity.sqrMagnitude > 0.19f)
            {
                Vector3 look = Vector3.Slerp(velocity.normalized, agent.desiredVelocity.normalized, 0.3f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 280f * delta);
            }

            float forwardPace = Vector3.Dot(transform.forward, velocity);
            bool backpedaling = aim != null && forwardPace < -0.28f;
            bool canRunForward = !backpedaling && (aim == null || forwardPace > gaitSpeed * 0.78f);
            string next = canRunForward && gaitSpeed > (gait == "run" ? 2.05f : 2.65f) ? "run" :
                gaitSpeed > (gait == "idle" ? 0.36f : 0.19f) ? "walk" : "idle";
            Play(next);
            if (next != "idle")
            {
                var clip = rig[next];
                clip.speed = Mathf.Clamp(gaitSpeed / (next == "walk" ? 1.7f : 3.55f), 0.6f, 1.19f) * (backpedaling ? -1f : 1f);
            }
        }

        void LateUpdate()
        {
            if (leader == null)
                return;
            rig.transform.localPosition = restRigPosition + Vector3.up * impactHeight;
            if (!Down && hips != null)
            {
                Vector3 drift = Vector3.ProjectOnPlane(hips.position - transform.position, Vector3.up);
                rig.transform.position -= Vector3.ClampMagnitude(drift, 0.40f);
            }
            if (rifle == null || stock == null || triggerGrip == null || supportGrip == null)
                return;
            if (rifle.gameObject.activeSelf == Down)
                rifle.gameObject.SetActive(!Down);
            if (Down || rightArm == null || rightFore == null || rightHand == null ||
                leftArm == null || leftFore == null || leftHand == null)
                return;

            for (int i = 0; i < armBones.Length; i++)
                animatedArmPose[i] = armBones[i].localRotation;
            armPoseApplied = true;
            bool aiming = aim != null && !aim.Dead && Time.time >= reloadUntil && Time.time >= impactUntil;
            readyWeight = Mathf.MoveTowards(readyWeight, aiming ? 1f : 0f, Time.deltaTime * 5f);
            Vector3 outward = transform.right, up = transform.up, forward = transform.forward;
            Vector3 shoulder = rightArm.position + outward * Mathf.Lerp(-0.065f, 0.012f, readyWeight)
                - up * Mathf.Lerp(0.19f, 0.09f, readyWeight) + forward * Mathf.Lerp(0.055f, 0.025f, readyWeight);
            Vector3 lowReady = forward - outward * 0.83f - up * (task == Task.Loot ? 0.72f : 0.38f);
            Vector3 aimDirection = aiming ? aim.transform.position + up * 1.20f - shoulder : lowReady;
            Quaternion goal = Quaternion.LookRotation(aimDirection, up);
            aimedRifleRotation = Quaternion.Slerp(aimedRifleRotation, goal, 1f - Mathf.Exp(-Time.deltaTime * 12f));
            rifle.SetPositionAndRotation(shoulder - aimedRifleRotation * stock.localPosition, aimedRifleRotation);
            // Trim the support hand along the real fore-end within reach when the animation twists the torso.
            Vector3 supportPoint = supportGrip.position;
            float reach = Vector3.Distance(leftArm.position, leftFore.position) + Vector3.Distance(leftFore.position, leftHand.position);
            Vector3 push = supportPoint - leftArm.position;
            float overreach = push.magnitude - (reach - 0.015f);
            if (overreach > 0f)
            {
                float along = Vector3.Dot(push.normalized, rifle.forward);
                if (along > 0.12f)
                    supportPoint -= rifle.forward * Mathf.Min(0.088f, overreach / along);
                push = supportPoint - leftArm.position;
                float correction = Mathf.Clamp(push.magnitude - (reach - 0.012f), 0f, 0.085f);
                rifle.position -= push.normalized * correction;
                supportPoint -= push.normalized * correction;
            }
            Reach(rightArm, rightFore, rightHand, triggerGrip.position + rifle.up * 0.038f + rifle.right * 0.019f,
                shoulder - up * 0.45f + outward * 0.25f);
            Reach(leftArm, leftFore, leftHand, supportPoint - rifle.right * 0.029f,
                leftArm.position - up * 0.40f - outward * 0.15f);
            TurnPalm(rightHand, rightKnuckle, rifle.forward * 0.81f - rifle.up * 0.42f - rifle.right * 0.18f);
            TurnPalm(leftHand, leftKnuckle, rifle.right * 0.70f + rifle.up * 0.32f + rifle.forward * 0.30f);
        }

        static void TurnPalm(Transform hand, Transform knuckle, Vector3 direction)
        {
            if (knuckle == null)
                return;
            Vector3 palm = knuckle.position - hand.position;
            if (palm.sqrMagnitude > 0.00001f)
                hand.rotation = Quaternion.RotateTowards(hand.rotation,
                    Quaternion.FromToRotation(palm, direction) * hand.rotation, 42f);
        }

        /// <summary>Analytic two-bone IK: put the hand on <paramref name="target"/>, elbow bending toward <paramref name="hint"/>.</summary>
        static void Reach(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hint)
        {
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            Vector3 toTarget = target - upper.position;
            if (toTarget.sqrMagnitude < 0.0001f || a < 0.01f || b < 0.01f)
                return;
            float c = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 0.005f, a + b - 0.003f);
            Vector3 line = toTarget.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(hint - upper.position, line).normalized;
            if (pole.sqrMagnitude < 0.001f)
                pole = Vector3.ProjectOnPlane(Vector3.down, line).normalized;
            float along = (a * a + c * c - b * b) / (2f * c);
            float drop = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 elbow = upper.position + line * along + pole * drop;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, upper.position + line * c - lower.position) * lower.rotation;
        }

        StationInfected PickTarget()
        {
            StationInfected best = null;
            float bestScore = float.MaxValue;
            foreach (var infected in leader.Threats)
            {
                if (infected == null || infected.Dead)
                    continue;
                // Leave unaware wanderers to the player; react to what is coming for the squad.
                bool focused = infected == leader.FocusTarget;
                bool captor = leader.IsHeldBy(infected);
                foreach (var ally in All)
                    captor |= ally.heldBy == infected;
                if (!infected.Alerted && !captor && !focused)
                    continue;
                float fromMe = Vector3.Distance(infected.transform.position, transform.position);
                if (fromMe > (focused || captor ? range + 4f : range) || !Visible(infected.transform.position + Vector3.up * 1.2f, infected.transform))
                    continue;
                float toLeader = Vector3.Distance(infected.transform.position, leader.transform.position);
                float score = Mathf.Min(fromMe, toLeader) - (captor ? 100f : 0f) -
                    (infected.Kind != InfectedKind.Common ? 4f : 0f) - (focused ? 25f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = infected;
                }
            }
            return best;
        }

        bool Visible(Vector3 point, Transform target = null)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            if (!Physics.Linecast(eye, point, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.transform.IsChildOf(target != null ? target : leader.transform);
        }

        void Fire(float distance)
        {
            if (aim.Dead || !Visible(aim.transform.position + Vector3.up * 1.2f, aim.transform))
                return;
            nextShot = Time.time + fireInterval * Random.Range(0.85f, 1.3f);
            flashUntil = Time.time + 0.05f;
            rounds--;
            voice.pitch = Random.Range(0.95f, 1.05f);
            if (shot != null)
                voice.PlayOneShot(shot, 0.8f);
            StationInfected.Noise(transform.position, 20f);
            float effectiveness = 1f - Mathf.InverseLerp(2.8f, 8f, distance);
            if (Random.value > Mathf.Lerp(accuracy, 0.87f, effectiveness))
                return;
            Vector3 point = aim.transform.position + Vector3.up * Random.Range(1.0f, 1.6f);
            float damageScale = aim.Kind == InfectedKind.Brute ? 0.45f : 1f;
            aim.Hit(Mathf.RoundToInt(damage * damageScale * Difficulty.BotDamage * Mathf.Lerp(1f, 1.35f, effectiveness)), point,
                point - (transform.position + Vector3.up * 1.5f));
        }

        void Play(string clip)
        {
            if (gait == clip)
                return;
            gait = clip;
            rig.CrossFade(clip, 0.24f);
        }
    }
}
