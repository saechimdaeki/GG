using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ResidualSignal
{
    /// <summary>Common infected plus four specials; each special has one job that breaks the squad's rhythm.</summary>
    public enum InfectedKind { Common, Leaper, Dragger, Bloater, Brute, Caller }

    [RequireComponent(typeof(CharacterController))]
    public sealed partial class StationInfected : NetworkBehaviour
    {
        [SerializeField] Animation rig;
        [Tooltip("Shamble while calm (specials: their only movement clip).")]
        [SerializeField] AnimationClip walkClip;
        [Tooltip("Optional sprint once alerted.")]
        [SerializeField] AnimationClip runClip;
        [Tooltip("Optional standing loop while calm.")]
        [SerializeField] AnimationClip idleClip;
        [Tooltip("Optional swing; without it the arms are swung procedurally.")]
        [SerializeField] AnimationClip attackClip;
        [SerializeField] AnimationClip bossSlamClip;
        [SerializeField] bool bespokeBoss;
        [SerializeField] float speed = 2.6f;
        [Tooltip("Alerted commons sprint at this speed (m/s).")]
        [SerializeField] float sprintSpeed = 4.6f;
        [SerializeField] AudioClip[] groans;
        [SerializeField] AudioClip[] screams;
        [SerializeField] int maximumHealth = 100;
        [SerializeField] int attackDamage = 15;
        [SerializeField] float attackInterval = 1.45f;
        [SerializeField] AudioSource warningAudio;
        [SerializeField] AudioClip attackCue;

        [Header("Reach (centre to centre, metres)")]
        [Tooltip("Walks until this close before swinging. Two body radii are ~0.67 m, so this is nearly touching.")]
        [SerializeField] float closeIn = 1.05f;
        [Tooltip("The swing only lands if the survivor is still within this distance and in front.")]
        [SerializeField] float reach = 1.3f;

        [Header("Specials")]
        [SerializeField] float leapRange = 7f;
        [SerializeField] float leapSpeed = 11f;
        [SerializeField] float dragRange = 13f;
        [SerializeField] float burstRadius = 4.2f;
        [SerializeField] float chargeSpeed = 9.5f;

        CharacterController body;
        InfectedKind kind;
        float specialCooldown = 0.8f;
        float callWindup;
        float chargeWindup;
        bool stagedEncounter;
        float leaping;
        Vector3 lunge;
        bool holding;
        LineRenderer tether;
        SquadMember target;
        StationTrial mission;
        float retarget;
        readonly NetworkVariable<byte> netKind = new();
        readonly NetworkVariable<bool> netRunner = new();
        readonly NetworkVariable<bool> netDead = new();
        readonly NetworkVariable<byte> netAttacks = new();
        readonly NetworkVariable<Vector3> netKillDirection = new();
        readonly NetworkVariable<bool> netAlerted = new();
        static readonly System.Collections.Generic.List<StationInfected> Living = new();
        internal static System.Collections.Generic.IReadOnlyList<StationInfected> ActiveInfected => Living;
        bool alerted;
        float nextGroan;
        Vector3 shoveVelocity;
        Vector3 wanderPoint;
        float wanderUntil;
        Vector3 lastPosition;
        InfectedBody physique;
        byte seenAttacks;
        /// <summary>The host (or a solo game) runs the AI; network clients only mirror it.</summary>
        bool Authority => !IsSpawned || IsServer;
        bool runner;
        int health;
        float attackCooldown;
        float stagger;
        float windup;
        Vector3 restPosition;
        Transform hips;
        Transform bossLeftFoot;
        Transform bossRightFoot;
        float bossGroundOffset;
        bool bossGroundedPose;
        Quaternion restRotation;
        public bool Dead => Authority ? health <= 0 : netDead.Value;
        public InfectedKind Kind => kind;
        public int RemainingHealth => health;
        public bool PreparingCall => callWindup > 0f && !Dead;
        /// <summary>Hunting the squad (as opposed to wandering, unaware).</summary>
        public bool Alerted => Authority ? alerted : netAlerted.Value;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            health = maximumHealth;
            restPosition = rig.transform.localPosition;
            restRotation = rig.transform.localRotation;
            hips = rig.GetComponentInChildren<SkinnedMeshRenderer>().rootBone;
            if (bespokeBoss)
                foreach (var bone in rig.GetComponentsInChildren<Transform>())
                {
                    if (bone.name == "mixamorigLeftToeBase") bossLeftFoot = bone;
                    if (bone.name == "mixamorigRightToeBase") bossRightFoot = bone;
                    FindBossThrowBone(bone);
                }
            physique = GetComponent<InfectedBody>();
            if (physique == null)
                physique = gameObject.AddComponent<InfectedBody>();
            rig.AddClip(walkClip, "walk");
            rig["walk"].wrapMode = WrapMode.Loop;
            if (runClip != null)
            {
                rig.AddClip(runClip, "run");
                rig["run"].wrapMode = WrapMode.Loop;
            }
            if (idleClip != null)
            {
                rig.AddClip(idleClip, "idle");
                rig["idle"].wrapMode = WrapMode.Loop;
            }
            if (attackClip != null)
            {
                rig.AddClip(attackClip, "attack");
                rig["attack"].wrapMode = WrapMode.Once;
            }
            if (bossSlamClip != null)
            {
                rig.AddClip(bossSlamClip, "slam");
                rig["slam"].wrapMode = WrapMode.Once;
            }
            nextGroan = Time.time + Random.Range(1f, 6f);
        }

        void OnEnable()
        {
            preferredFlank = Living.Count % 2 == 0 ? -1f : 1f;
            Living.Add(this);
        }
        void OnDisable()
        {
            Living.Remove(this);
            ClearBossDebris();
        }

        /// <summary>Gunfire and screams: every calm infected within <paramref name="radius"/> wakes up.</summary>
        public static void Noise(Vector3 point, float radius)
        {
            foreach (var infected in Living)
                if (!infected.alerted && infected.Authority && (infected.transform.position - point).sqrMagnitude < radius * radius)
                    infected.Alert();
        }

        void Alert()
        {
            if (alerted)
                return;
            alerted = true;
            if (IsSpawned)
                netAlerted.Value = true;
            Voice(screams, 0.9f);
        }

        void Voice(AudioClip[] pool, float volume)
        {
            if (pool == null || pool.Length == 0 || Dead)
                return;
            warningAudio.pitch = Random.Range(0.88f, 1.12f);
            warningAudio.PlayOneShot(pool[Random.Range(0, pool.Length)], volume);
        }

        /// <summary>Right-click shove: knocked back and stunned for a moment; cancels a swing.</summary>
        public void Shove(Vector3 direction)
        {
            if (!Authority)
            {
                physique.React(transform.position + Vector3.up * 1.2f, direction, 25);
                ShoveRpc(direction);
                return;
            }
            if (Dead || kind == InfectedKind.Brute)
                return;
            Alert();
            stagger = 0.95f;
            windup = 0;
            shoveVelocity = Vector3.ProjectOnPlane(direction, Vector3.up).normalized * 5.6f;
            physique.React(transform.position + Vector3.up * 1.2f, direction, 30);
            if (IsSpawned)
                ReactRpc(transform.position + Vector3.up * 1.2f, direction, 30);
            if (kind == InfectedKind.Leaper || kind == InfectedKind.Dragger)
                Release();
        }

        [Rpc(SendTo.Server)]
        void ShoveRpc(Vector3 direction) => Shove(direction);

        /// <summary>Start hunting. A calm common wanders and groans until it sees, hears or feels the squad.</summary>
        public void Follow(StationTrial trial, bool isRunner, InfectedKind special = InfectedKind.Common, bool calm = false)
        {
            mission = trial;
            if (!calm)
            {
                alerted = true;
                netAlerted.Value = true;
            }
            netKind.Value = (byte)special;
            netRunner.Value = isRunner;
            Dress(isRunner, special);
            health = kind switch
            {
                InfectedKind.Leaper => 220,
                InfectedKind.Dragger => 260,
                InfectedKind.Bloater => 60,
                InfectedKind.Brute => bespokeBoss ? 4500 + 750 * (StationTrial.HumanPlayerCount() - 1) : 850,
                InfectedKind.Caller => 95,
                _ => maximumHealth
            };
        }

        public void StageEncounter() => stagedEncounter = true;

        public void BeginStagedEncounter()
        {
            stagedEncounter = false;
            if (bespokeBoss)
            {
                bossOpeningUntil = Time.time + 8f;
                bossOpeningFloor = health / 2;
                specialCooldown = 0.45f;
            }
            Alert();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                Dress(netRunner.Value, (InfectedKind)netKind.Value);
        }

        NavMeshPath pursuitPath;
        Vector3[] pursuitCorners;
        int pursuitCorner;
        float nextPursuit;
        readonly RaycastHit[] sightHits = new RaycastHit[16];
        float blockedTime;
        float preferredFlank;
        Vector3 hitRecoil;

        Vector3 PursueAroundWall(Vector3 fallback)
        {
            if (Time.time >= nextPursuit)
            {
                nextPursuit = Time.time + 0.65f;
                pursuitPath ??= new NavMeshPath();
                pursuitCorners = null;
                if (NavMesh.SamplePosition(transform.position, out var origin, 1.2f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(target.body.position, out var goal, 1.4f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(origin.position, goal.position, NavMesh.AllAreas, pursuitPath) &&
                    pursuitPath.status == NavMeshPathStatus.PathComplete)
                {
                    pursuitCorners = pursuitPath.corners;
                    pursuitCorner = 1;
                }
            }

            while (pursuitCorners != null && pursuitCorner < pursuitCorners.Length)
            {
                Vector3 toward = Vector3.ProjectOnPlane(pursuitCorners[pursuitCorner] - transform.position, Vector3.up);
                if (toward.sqrMagnitude > 0.09f)
                    return toward.normalized;
                pursuitCorner++;
            }
            return fallback;
        }

        bool ClearAttackLine(float height, out RaycastHit obstruction) => ClearAttackLine(target, height, out obstruction);

        bool ClearAttackLine(SquadMember member, float height, out RaycastHit obstruction)
        {
            Vector3 from = transform.position + Vector3.up * height;
            Vector3 ray = member.body.position + Vector3.up * height - from;
            int hits = Physics.RaycastNonAlloc(from, ray.normalized, sightHits, ray.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            obstruction = default;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                var hit = sightHits[i];
                if (hit.transform.root == member.body.root || hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<StationInfected>() != null ||
                    (bespokeBoss && hit.collider is CharacterController) || hit.distance >= closest)
                    continue;
                closest = hit.distance;
                obstruction = hit;
            }
            return closest == float.PositiveInfinity;
        }

        void Locomotion(float pace)
        {
            if ((attackClip != null && rig.IsPlaying("attack")) || (bossSlamClip != null && rig.IsPlaying("slam")))
                return;
            string clip = pace < 0.25f && idleClip != null ? "idle" :
                runClip != null && pace > 2.1f && Alerted ? "run" : "walk";
            if (!rig.IsPlaying(clip))
                rig.CrossFade(clip, 0.12f);
        }

        void PlayAttack()
        {
            if (attackClip != null)
                rig.Play("attack");
            else
                physique.Swing(runner ? 0.32f : 0.44f);
            if (IsSpawned)
                netAttacks.Value++;
        }

        void Dress(bool isRunner, InfectedKind special)
        {
            runner = isRunner;
            kind = special;
            // Distinct silhouettes from the shared rig until each special gets its own model.
            (Vector3 scale, Color tint) look = kind switch
            {
                InfectedKind.Leaper => (new Vector3(0.88f, 0.84f, 0.88f), new Color(0.75f, 0.8f, 0.95f)),
                InfectedKind.Dragger => (new Vector3(0.82f, 1.22f, 0.82f), new Color(0.85f, 0.78f, 0.7f)),
                InfectedKind.Bloater => (new Vector3(1.55f, 1.0f, 1.45f), new Color(0.7f, 0.95f, 0.55f)),
                InfectedKind.Brute => (new Vector3(1.5f, 1.38f, 1.5f), new Color(0.95f, 0.72f, 0.66f)),
                InfectedKind.Caller => (new Vector3(0.74f, 1.24f, 0.75f), new Color(0.94f, 0.71f, 0.38f)),
                _ => (Vector3.one, Color.white)
            };
            // Scale the whole body (collider included): the rig pivot sits far from its mesh, so scaling it alone shifts the model.
            transform.localScale = bespokeBoss ? new Vector3(1.34f, 1.32f, 1.34f) : look.scale;
            if (kind != InfectedKind.Common && !bespokeBoss)
            {
                var tintBlock = new MaterialPropertyBlock();
                tintBlock.SetColor("_BaseColor", look.tint);
                foreach (var part in rig.GetComponentsInChildren<Renderer>())
                    part.SetPropertyBlock(tintBlock);
            }

            rig["walk"].speed = runClip != null ? 1f : runner ? 1.9f : 1.25f;
            if (runClip != null)
                rig["run"].speed = runner ? 1.15f : 1f;
            rig.Play(idleClip != null ? "idle" : "walk");
        }

        void Update()
        {
            RestoreBossArms();
            physique.RestoreAnimatedPose();
            if (!Authority)
            {
                MirrorAnimation();
                return;
            }
            if (Dead || mission == null || stagedEncounter || SessionMenu.Blocking ||
                (mission.OpeningStaging && transform.position.z < 4f && !alerted))
                return;
            if (Time.time > nextGroan)
            {
                nextGroan = Time.time + Random.Range(3.5f, 9f);
                Voice(alerted ? screams : groans, alerted ? 0.45f : 0.6f);
            }
            if (!alerted)
            {
                Wander();
                return;
            }
            if (!mission.InCombat)
                return;
            if (bespokeBoss && MoveOutFromBehindScreen(Time.deltaTime))
                return;
            retarget -= Time.deltaTime;
            if (!holding && leaping <= 0 && chargeWindup <= 0f && throwWindup <= 0f && bossSweep <= 0f && (target == null || !target.Available || retarget <= 0))
            {
                target = bespokeBoss ? PickBossTarget() : SquadMember.Nearest(transform.position);
                retarget = 0.5f;
            }
            if (target == null)
                return;

            float delta = Time.deltaTime;
            if (kind != InfectedKind.Common && UpdateSpecial(delta))
                return;
            Vector3 direction = target.body.position - transform.position;
            direction.y = 0;
            float distance = direction.magnitude;
            if (distance > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction), delta * 9f);
            attackCooldown -= delta;
            stagger = Mathf.Max(0, stagger - delta);
            if (shoveVelocity.sqrMagnitude > 0.01f)
            {
                body.Move((shoveVelocity + Vector3.down * 3f) * delta);
                shoveVelocity = Vector3.MoveTowards(shoveVelocity, Vector3.zero, delta * 12f);
                Locomotion(0f);
                return;
            }
            bool hasLineOfSight = ClearAttackLine(0.9f, out RaycastHit obstruction);
            float pace = kind switch
            {
                InfectedKind.Leaper => speed * 1.7f,
                InfectedKind.Bloater => speed * 0.6f,
                InfectedKind.Brute => speed * 1.25f,
                _ when runClip != null => runner ? sprintSpeed * 1.15f : sprintSpeed,
                _ => runner ? speed * 1.75f : speed
            };
            Vector3 move = Vector3.zero;
            if (windup > 0f)
            {
                windup -= delta;
                if (windup <= 0f && InReach(direction, distance) && hasLineOfSight)
                {
                    if (kind == InfectedKind.Brute)
                        target.knock(direction.normalized * 7.5f + Vector3.up * 4.3f, 30);
                    else
                        target.hurt(runner ? 19 : attackDamage);
                }
                if (hasLineOfSight && distance > Scaled(closeIn) * 0.78f)
                    move = direction.normalized * (pace * 0.56f);
            }
            else if (hasLineOfSight && distance <= Scaled(closeIn) + 0.1f && stagger <= 0f && attackCooldown <= 0f)
            {
                attackCooldown = attackInterval;
                PlayAttack();
                windup = runner ? 0.32f : 0.44f;
                warningAudio.PlayOneShot(attackCue, 0.75f);
                target.warn(transform.position);
            }
            else if (!hasLineOfSight || distance > Scaled(closeIn) || attackCooldown > 0.3f)
            {
                Vector3 heading = direction.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, heading);
                float flank = preferredFlank;
                if (!hasLineOfSight)
                {
                    flank = Vector3.Dot(side, obstruction.normal) >= 0 ? 1f : -1f;
                    heading = PursueAroundWall((heading * 0.4f + side * flank).normalized);
                }
                else if (blockedTime > 0.42f)
                {
                    heading = PursueAroundWall((heading * 0.35f + side * flank).normalized);
                }
                else if (blockedTime > 0.12f || distance <= Scaled(closeIn))
                {
                    heading = (heading * 0.22f + side * flank).normalized;
                }
                float tempo = distance <= Scaled(closeIn) ? 0.32f : 1f;
                float reaction = stagger > 0f ? 0.46f : 1f;
                move = heading * (pace * tempo * reaction);
                if (!hasLineOfSight && heading.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), delta * 10f);
            }

            Vector3 before = transform.position;
            MoveInCombat((move + hitRecoil + Vector3.down * 3f) * delta);
            hitRecoil = Vector3.MoveTowards(hitRecoil, Vector3.zero, 13f * delta);
            float measured = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / Mathf.Max(delta, 0.0001f);
            blockedTime = move.sqrMagnitude > 1f && measured < 0.25f ? blockedTime + delta : Mathf.Max(0f, blockedTime - delta);
            Locomotion(measured);

        }

        /// <summary>Calm: stand or shamble about, and wake when a survivor comes into view.</summary>
        void Wander()
        {
            var nearest = SquadMember.Nearest(transform.position);
            if (nearest != null)
            {
                Vector3 to = nearest.body.position - transform.position;
                bool close = to.sqrMagnitude < 4f * 4f;
                bool seen = to.sqrMagnitude < 13f * 13f && Vector3.Dot(transform.forward, to.normalized) > 0.2f &&
                    (!Physics.Linecast(transform.position + Vector3.up * 1.6f, nearest.body.position + Vector3.up * 1.4f,
                        out RaycastHit block, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        block.transform.root == nearest.body.root);
                if (close || seen)
                {
                    Alert();
                    return;
                }
            }
            if (Time.time > wanderUntil)
            {
                wanderUntil = Time.time + Random.Range(3f, 8f);
                wanderPoint = Random.value < 0.5f ? transform.position :
                    transform.position + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(1.5f, 4f);
            }
            Vector3 step = wanderPoint - transform.position;
            step.y = 0;
            if (step.magnitude > 0.3f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(step), Time.deltaTime * 2f);
                body.Move((step.normalized * 0.7f + Vector3.down * 3f) * Time.deltaTime);
                if (!rig.IsPlaying("walk"))
                    rig.CrossFade("walk", 0.3f);
            }
            else if (idleClip != null && !rig.IsPlaying("idle"))
                rig.CrossFade("idle", 0.4f);
        }

        void LateUpdate()
        {
            if (physique.Ragdolled)
                return;
            // Keep simulation movement on the controller, independent of imported root motion. The source clips move
            // the hips bone, so re-centre the hips over the collider every frame (vertical bob kept).
            rig.transform.SetLocalPositionAndRotation(restPosition, restRotation);
            Vector3 drift = hips.position - transform.position;
            drift.y = 0;
            rig.transform.position += physique.HitOffset - drift;
            if (bossLeftFoot != null && bossRightFoot != null)
            {
                float feet = Mathf.Min(bossLeftFoot.position.y, bossRightFoot.position.y);
                float offset = Mathf.Clamp(feet - transform.position.y - 0.045f, -1.8f, 1.8f);
                if (!bossGroundedPose || !rig.IsPlaying("slam"))
                    bossGroundOffset = bossGroundedPose ? Mathf.MoveTowards(bossGroundOffset, offset, Time.deltaTime * 5f) : offset;
                bossGroundedPose = true;
                rig.transform.position -= Vector3.up * bossGroundOffset;
            }
            bool sprinting = runClip != null && rig.IsPlaying("run") && Alerted && windup <= 0 && !physique.IsFlinching;
            physique.SetFrenzy(sprinting ? 1f : 0f);
            physique.ApplyFlinch();
            PoseBossThrowArms();
        }

        /// <summary>Special behaviour; returns true when it took over movement this frame.</summary>
        bool UpdateSpecial(float delta)
        {
            Vector3 toTarget = target.body.position - transform.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;
            specialCooldown -= delta;
            bool clear = !Physics.Linecast(transform.position + Vector3.up * 1.2f, target.body.position + Vector3.up * 1.2f,
                out RaycastHit block, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                block.transform.root == target.body.root;
            switch (kind)
            {
                case InfectedKind.Leaper:
                    if (holding)
                    {
                        Vector3 face = target.body.forward;
                        transform.SetPositionAndRotation(target.body.position + face * 0.75f - Vector3.up * 0.2f,
                            Quaternion.LookRotation(-face));
                        return true;
                    }
                    if (leaping > 0)
                    {
                        leaping -= delta;
                        body.Move((lunge + Vector3.down * 1.5f) * delta);
                        if (distance < 1.2f && target.TryPin(this))
                        {
                            holding = true;
                            leaping = 0;
                            // The attack clip drifts its hips sideways; the in-place walk reads as clawing at close range.
                            rig["walk"].speed = 2.2f;
                            rig.Play("walk");
                        }
                        return true;
                    }
                    if (clear && distance < leapRange && specialCooldown <= 0 && mission.TryReserveSpecial(target))
                    {
                        specialCooldown = 5f;
                        leaping = distance / leapSpeed + 0.15f;
                        lunge = toTarget.normalized * leapSpeed + Vector3.up * 1.5f;
                        warningAudio.pitch = 1.35f;
                        warningAudio.PlayOneShot(attackCue, 1f);
                        target.warn(transform.position);
                        return true;
                    }
                    return false;
                case InfectedKind.Dragger:
                    if (holding)
                    {
                        tether.SetPosition(0, transform.position + Vector3.up * 1.6f);
                        tether.SetPosition(1, target.body.position + Vector3.up * 1.1f);
                        if (!clear || distance < 1.2f)
                        {
                            if (distance < 1.2f)
                                target.hurt(Mathf.CeilToInt(10 * delta * 6f));
                            if (!clear)
                                Release();
                        }
                        return true;
                    }
                    if (clear && distance < dragRange && distance > 3f && specialCooldown <= 0 && mission.TryReserveSpecial(target))
                    {
                        specialCooldown = 7f;
                        if (target.TryDrag(this))
                        {
                            holding = true;
                            tether = tether != null ? tether : CreateTether();
                            tether.enabled = true;
                            PlayAttack();
                            warningAudio.pitch = 0.7f;
                            warningAudio.PlayOneShot(attackCue, 1f);
                        }
                        return true;
                    }
                    // Keeps its distance: hangs back at range instead of closing in.
                    return clear && distance < dragRange;
                case InfectedKind.Bloater:
                    if (distance < 1.6f)
                    {
                        Hit(health);
                        return true;
                    }
                    return false;
                case InfectedKind.Caller:
                    if (callWindup > 0f)
                    {
                        callWindup -= delta;
                        if (callWindup <= 0f && clear)
                            mission.CallNearbyInfected(transform.position);
                        return true;
                    }
                    if (clear && distance > 2.7f && distance < 12f && specialCooldown <= 0 && mission.TryReserveSpecial(target))
                    {
                        specialCooldown = 16f;
                        callWindup = 1.5f;
                        PlayAttack();
                        warningAudio.pitch = 1.55f;
                        if (attackCue != null)
                            warningAudio.PlayOneShot(attackCue, 1f);
                        target.warn(transform.position);
                        return true;
                    }
                    return false;
                case InfectedKind.Brute:
                    if (bespokeBoss)
                    {
                        clear = ClearAttackLine(1.2f, out _);
                        if (UpdateBossThrow(toTarget, distance, delta) || UpdateBossSweep(distance, clear, delta))
                            return true;
                    }
                    if (chargeWindup > 0f)
                    {
                        chargeWindup -= delta;
                        if (chargeWindup <= 0f)
                        {
                            leaping = 1.12f;
                            BossAttacksCommitted++;
                        }
                        return true;
                    }
                    if (leaping > 0)
                    {
                        leaping -= delta;
                        CollisionFlags collision = MoveInCombat((lunge + Vector3.down * 3f) * delta);
                        if (distance < Scaled(reach) && clear && Vector3.Dot(lunge.normalized, toTarget.normalized) > 0.25f)
                        {
                            target.knock(lunge.normalized * 12f + Vector3.up * 5.1f, bespokeBoss ? 35 : 26);
                            leaping = 0;
                        }
                        if ((collision & CollisionFlags.Sides) != 0)
                            leaping = 0;
                        if (leaping <= 0f)
                            bossRecovery = 0.7f;
                        else
                            Locomotion(chargeSpeed);
                        return true;
                    }
                    if (clear && distance > 4f && distance < 14f && specialCooldown <= 0 && (bespokeBoss || mission.TryReserveSpecial(target)))
                    {
                        specialCooldown = 4.2f;
                        chargeWindup = 0.78f;
                        lunge = toTarget.normalized * chargeSpeed;
                        PlayBossSlam();
                        if (IsSpawned)
                            BossChargeCueRpc();
                        warningAudio.pitch = 0.55f;
                        warningAudio.PlayOneShot(attackCue, 1f);
                        target.warn(transform.position);
                        return true;
                    }
                    return false;
            }
            return false;
        }

        /// <summary>Bigger bodies reach further: brute and bloater scale with their collider.</summary>
        float Scaled(float metres) => metres + body.radius * (transform.lossyScale.x - 1f);

        /// <summary>Melee lands only at arm's length and inside a forward cone.</summary>
        bool InReach(Vector3 toTarget, float distance) =>
            distance <= Scaled(reach) && Vector3.Dot(transform.forward, toTarget.normalized) > 0.5f;

        LineRenderer CreateTether()
        {
            var line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.widthMultiplier = 0.06f;
            line.material = new Material(Shader.Find("HDRP/Unlit"));
            line.material.SetColor("_UnlitColor", new Color(0.32f, 0.06f, 0.05f));
            return line;
        }

        /// <summary>Player broke free; forwarded to the host when this copy is a mirror.</summary>
        public void ReleaseHold()
        {
            if (Authority)
                Release();
            else
                ReleaseRpc();
        }

        [Rpc(SendTo.Server)]
        void ReleaseRpc() => Release();

        void Remove()
        {
            if (IsSpawned)
                NetworkObject.Despawn();
            else
                Destroy(gameObject);
        }

        /// <summary>Let go of the player (pin or drag).</summary>
        public void Release()
        {
            if (!holding)
                return;
            holding = false;
            stagger = 0.22f;
            specialCooldown = 4f;
            if (tether != null)
                tether.enabled = false;
            target.Release(this);
        }

        void MirrorAnimation()
        {
            UpdateHeldDebris();
            if (netDead.Value)
            {
                CancelBossThrow();
                if (!physique.Ragdolled)
                    physique.Ragdoll(transform.position + Vector3.up * 1.1f, netKillDirection.Value, 80);
                return;
            }
            if (seenAttacks != netAttacks.Value)
            {
                seenAttacks = netAttacks.Value;
                if (attackClip != null)
                    rig.Play("attack");
                else
                    physique.Swing(0.5f);
            }
            if (Time.time > nextGroan)
            {
                nextGroan = Time.time + Random.Range(3.5f, 9f);
                Voice(netAlerted.Value ? screams : groans, 0.5f);
            }
            float pace = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = transform.position;
            Locomotion(pace);
        }

        [Rpc(SendTo.Server)]
        void RequestHitRpc(int damage, Vector3 point, Vector3 direction) => Hit(damage, point, direction);

        [Rpc(SendTo.NotServer)]
        void ReactRpc(Vector3 point, Vector3 direction, int damage) => physique.React(point, direction, damage);

        /// <summary>Apply damage with no known wound (scripted kills, tests).</summary>
        public void Hit(int damage) => Hit(damage, transform.position + Vector3.up * 1.1f, -transform.forward);

        /// <summary>Apply a bullet: flinch and blood where it struck. On a network client it is forwarded to the host.</summary>
        public void Hit(int damage, Vector3 point, Vector3 direction)
        {
            if (!Authority)
            {
                stagger = 0.22f;
                physique.React(point, direction, damage);
                RequestHitRpc(damage, point, direction);
                return;
            }
            if (Dead || stagedEncounter)
                return;
            int openingFloor = bespokeBoss && BossAttacksCommitted == 0 && Time.time < bossOpeningUntil
                ? bossOpeningFloor : 0;
            health = Mathf.Max(Mathf.Max(openingFloor, BossArmorFloor), health - damage);
            Alert();
            physique.React(point, direction, damage);
            if (IsSpawned)
                ReactRpc(point, direction, damage);
            stagger = kind == InfectedKind.Brute ? 0f : damage >= 60 ? 0.33f : 0.2f;
            if (!Dead && kind != InfectedKind.Brute)
                hitRecoil = Vector3.ProjectOnPlane(direction, Vector3.up).normalized * Mathf.Clamp(damage * 0.056f, 1.3f, 4.1f);
            if (kind != InfectedKind.Brute)
                windup = 0;
            callWindup = 0;
            // Any hit on a holder breaks the grip (solo players can still save themselves).
            if (kind == InfectedKind.Dragger || (kind == InfectedKind.Leaper && Dead))
                Release();
            if (Dead && kind == InfectedKind.Bloater)
                mission.Burst(transform.position, burstRadius);
            if (Dead)
            {
                CancelBossThrow();
                body.enabled = false;
                if (IsSpawned)
                {
                    netKillDirection.Value = direction;
                    netDead.Value = true;
                }
                physique.Ragdoll(point, direction, damage);
                Invoke(nameof(Remove), 12f);
            }
        }
    }
}
