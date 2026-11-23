using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ResidualSignal
{
    public sealed partial class StationTrial : MonoBehaviour
    {
        [SerializeField] CharacterController player;
        [SerializeField] Camera eyes;
        [SerializeField] Transform terminal;
        [SerializeField] Transform radio;
        [SerializeField] Transform roster;
        [SerializeField] Transform supply;
        [SerializeField] StationWitness witness;
        [SerializeField] Transform evacuationPoint;
        [SerializeField] Transform servicePump;
        [SerializeField] Transform serviceShutter;
        [SerializeField] Transform loadingExtraction;
        [SerializeField] float serviceTransferSeconds = 14f;
        [SerializeField] AudioClip signalSound;
        [SerializeField] AudioClip stepSound;
        [SerializeField] Transform shutter;
        [SerializeField] Transform drainGate;
        [SerializeField] Transform staffGate;
        [SerializeField] Light flashlight;
        [SerializeField] Light exitLight;
        [SerializeField] bool acceptLocalInput = true;
        [SerializeField] float movementSpeed = 4.4f;
        [SerializeField] float mouseSensitivity = 2.1f;
        [SerializeField] float gravity = 18f;
        /// <summary>0 is the sidearm (always carried); the rest are primaries found in the world.</summary>
        [SerializeField] Firearm[] arsenal;
        [SerializeField] WeaponPickup[] pickups;
        [Tooltip("Special infected body (leaper, dragger, bloater, brute).")]
        [SerializeField] StationInfected infectedPrefab;
        [SerializeField] StationInfected bossPrefab;
        StationInfected yardBoss;
        [Tooltip("Ordinary infected civilians; one is picked at random per spawn.")]
        [SerializeField] StationInfected[] commonPrefabs;
        [SerializeField] AudioClip shoveSound;
        [SerializeField] AudioClip fleshSound;
        [SerializeField] AudioClip hordeSound;
        [SerializeField] int maxInfected = 26;
        [Tooltip("Solo play: AI teammates that follow, fight and revive.")]
        [SerializeField] SurvivorBot botPrefab;
        [SerializeField] AudioSource weaponAudio;
        [SerializeField] AudioClip reloadSound;
        [SerializeField] float shutterDelay = 38f;
        [SerializeField] float waveInterval = 10f;
        [SerializeField] float preparationSeconds = 6f;
        [SerializeField] float crossingPressureDelay = 18f;
        [SerializeField] float range = 42f;
        [SerializeField] float shutterTravel = 3.4f;
        [SerializeField] float shutterSpeed = 2.2f;
        [SerializeField] bool spawnSpecials = true;
        [SerializeField] int escapePresses = 6;

        enum Phase { ReceiveSignal, ReadRoster, SelectRoute, FindTerminal, Opening, FindWitness, Evacuate, Complete, Failed, Maintenance, TransferDefense, LoadingEscort }
        Phase phase;
        Vector3 closedPosition;
        Transform activeGate;
        Vector3 raisedGatePosition;
        bool drainSelected;
        bool routeEncounterStarted;
        float hitUntil;
        int kills;
        GUIStyle smallLabel;
        GUIStyle titleLabel;
        GUIStyle ammoLabel;
        float verticalSpeed;
        float pitch;
        float countdown;
        GUIStyle caption;
        GUIStyle statusStyle;
        readonly RaycastHit[] aimHits = new RaycastHit[16];
        bool aimedAtInfected;
        StationInfected focusTarget;
        float focusUntil;
        public StationInfected FocusTarget => Time.time < focusUntil && focusTarget != null && !focusTarget.Dead ? focusTarget : null;

        Vector3 warningPoint;
        float warningUntil;
        int equipped;
        int primary = -1;
        float raise;
        float weaponKick;
        float stride;
        float stepDistance;
        float strideWeight;
        bool isWalking;
        float flashUntil;
        float nextShotAt;
        float reloadUntil;
        float nextWave;
        float damageFlash;
        int wave;
        int health;
        bool tookSupply;
        string radioMessage;
        float radioUntil;
        readonly List<StationInfected> threats = new();
        StationInfected pinnedBy;
        StationInfected draggedBy;
        int escapeCount;
        float holdDamage;
        float blindUntil;
        Vector3 knock;
        int specialTurn;
        float specialAttackAvailableAt;
        bool workshopEntered;
        bool loadingReinforcements;
        Vector3 closedServiceShutter;
        public IReadOnlyList<StationInfected> Threats => threats;
        public int Rounds => Current.rounds;
        public string Equipped => Current.label;
        Firearm Current => arsenal[equipped];
        public int Health => health;
        public bool InCombat => phase is not (Phase.Complete or Phase.Failed);

        public bool Complete => phase == Phase.Complete;
        public bool AnnexUnlocked => phase == Phase.Maintenance;
        public bool Held => pinnedBy != null || draggedBy != null;
        /// <summary>The player on this machine.</summary>
        public static StationTrial Local { get; private set; }
        /// <summary>True when this machine runs the mission: solo, or the session host.</summary>
        public static bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening ||
            NetworkManager.Singleton.IsServer;
        public static bool Networked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public bool Downed { get; private set; }
        public float Countdown => countdown;
        public int PhaseIndex => (int)phase;
        public bool DrainRoute => drainSelected;
        public StationWitness Witness => witness;
        SquadMember self;
        float reviveProgress;
        float shoveReadyAt;
        float shoveStarted = -10f;
        bool ambientSpawned;
        bool cordonDisturbed;
        public bool OpeningStaging => !cordonDisturbed && phase == Phase.ReceiveSignal;
        float crossingPressureAt;
        bool crossingSurgeQueued;
        static readonly Color HudSurface = new(0.027f, 0.054f, 0.06f, 0.91f);
        static readonly Color HudAccent = new(1f, 0.76f, 0.38f);
        GUIStyle shadowStyle;
        const float ReviveSeconds = 2.5f;

        static SurvivorAvatar DownedTeammate()
        {
            var me = Local.transform.position;
            foreach (var survivor in FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None))
                if (survivor != SurvivorAvatar.Mine && survivor.Down && Near(me, survivor.transform.position, 1.8f))
                    return survivor;
            return null;
        }

        static SurvivorBot DownedBot()
        {
            foreach (var bot in SurvivorBot.All)
                if (bot.Down && Near(Local.transform.position, bot.transform.position, 1.8f))
                    return bot;
            return null;
        }

        /// <summary>Pressing E next to a downed teammate starts the hold-to-revive.</summary>
        static bool TryRevive() => DownedTeammate() != null || DownedBot() != null;

        /// <summary>A teammate's line, shown as a named subtitle.</summary>
        public void Say(string speaker, string line) => Signal($"{speaker}: {line}", 3.5f);

        /// <summary>First aid from a kit (yours or a teammate's).</summary>
        public void Heal(int amount) => health = Mathf.Min(100, health + amount);

        /// <summary>Running short: less than two magazines left for the gun in hand.</summary>
        public bool AmmoLow => Current.reserve < Current.capacity * 2;

        /// <summary>An ammo box: refill every gun you carry.</summary>
        public void GiveAmmo()
        {
            foreach (var gun in arsenal)
                if (gun.owned)
                    gun.reserve += gun.capacity * 3;
        }

        LootItem NearbyLoot()
        {
            LootItem closest = null;
            float nearest = 1.5f * 1.5f;
            foreach (var item in LootItem.All)
            {
                if (item.kind == LootItem.Kind.Medkit && health >= 100)
                    continue;
                float distance = (item.transform.position - transform.position).sqrMagnitude;
                if (distance > nearest)
                    continue;
                nearest = distance;
                closest = item;
            }
            return closest;
        }

        bool TryLoot()
        {
            var item = NearbyLoot();
            if (item == null)
                return false;
            if (item.kind == LootItem.Kind.Medkit)
            {
                Heal(60);
                Signal("구급상자를 썼다", 2.5f);
            }
            else
            {
                GiveAmmo();
                Signal("탄약을 채웠다", 2.5f);
            }
            item.Take();
            return true;
        }

        /// <summary>Is this infected pinning or dragging the player?</summary>
        public bool IsHeldBy(StationInfected infected) => pinnedBy == infected || draggedBy == infected;

        /// <summary>Solo with bots: the mission fails only when the player and every bot are down.</summary>
        public void CheckSoloWipe()
        {
            if (Networked || !Downed)
                return;
            foreach (var bot in SurvivorBot.All)
                if (!bot.Down)
                    return;
            SquadWiped();
        }

        void Start()
        {
            closedPosition = shutter.position;
            closedServiceShutter = serviceShutter.position;
            foreach (var gun in arsenal)
            {
                gun.Prepare();
                gun.view.gameObject.SetActive(false);
            }
            arsenal[0].owned = true;
            Equip(0);
            health = 100;
            Local = this;
            self = new SquadMember
            {
                body = transform,
                hurt = TakeHit,
                knock = Knockback,
                warn = WarnOfAttack,
                pin = TryPin,
                drag = TryDrag,
                release = ReleaseFrom,
                blind = Blind,
            };
            SquadMember.All.Add(self);
        }

        void Update()
        {
            if (acceptLocalInput && !Application.isBatchMode && !ProcessLocalInput())
                return;
            UpdateWeapon();
            UpdateHolds();
            aimedAtInfected = InCombat && FindAimTarget(out _, out _);
            if (activeGate != null)
                activeGate.position = Vector3.MoveTowards(activeGate.position, raisedGatePosition, shutterSpeed * Time.deltaTime);
            if (Authority)
            {
                if (spawnSpecials)
                    RunDirector();
                DeliverReinforcements();
            }
            if (Authority && phase == Phase.FindTerminal && !routeEncounterStarted && SquadFurthestZ() > 9.4f)
            {
                routeEncounterStarted = true;
                QueueReinforcements(1 + HumanPlayerCount(), new Vector3(drainSelected ? -3.5f : 3.5f, 0, 15f), true,
                    spawnSpecials ? InfectedKind.Leaper : InfectedKind.Common);
                crossingPressureAt = Time.time + crossingPressureDelay;
            }

            if (phase == Phase.Opening && Authority)
            {
                countdown -= Time.deltaTime;
                nextWave -= Time.deltaTime;
                if (nextWave <= 0 && countdown > 5f)
                    SpawnWave();
                if (countdown <= 0)
                {
                    routes.Clear();
                    SetPhase(Phase.FindWitness);
                }
            }
            if (phase is Phase.FindWitness or Phase.Evacuate or Phase.Maintenance or Phase.TransferDefense or Phase.LoadingEscort)
            {
                Vector3 openPosition = closedPosition + Vector3.up * shutterTravel;
                shutter.position = Vector3.MoveTowards(shutter.position, openPosition, shutterSpeed * Time.deltaTime);
                if (Authority && phase == Phase.Evacuate && witness.HasArrived && SquadAt(evacuationPoint))
                    SetPhase(Phase.Maintenance);
            }
            UpdateServiceYard();
        }

        void UpdateServiceYard()
        {
            if (phase == Phase.Maintenance && !workshopEntered && SquadFurthestZ() > 40f && Authority)
            {
                workshopEntered = true;
                QueueReinforcements(3 + HumanPlayerCount(), new Vector3(4.2f, 0, 47f), true,
                    spawnSpecials ? InfectedKind.Caller : InfectedKind.Common);
            }
            if (phase == Phase.TransferDefense && Authority)
            {
                countdown -= Time.deltaTime;
                if (countdown <= 0f)
                    SetPhase(Phase.LoadingEscort);
            }
            if (phase != Phase.LoadingEscort)
                return;
            serviceShutter.position = Vector3.MoveTowards(serviceShutter.position,
                closedServiceShutter + Vector3.up * 3.38f, shutterSpeed * Time.deltaTime);
            if (!Authority)
                return;
            if (!loadingReinforcements && SquadFurthestZ() > 55f)
            {
                loadingReinforcements = true;
                if (yardBoss != null && !yardBoss.Dead)
                {
                    yardBoss.BeginStagedEncounter();
                    Signal("서이경: 거대체다! 적재장 기둥 뒤로!", 4f);
                }
                QueueReinforcements(3 + HumanPlayerCount(), new Vector3(4.5f, 0, 60f), true);
            }
            if (witness.HasArrived && (yardBoss == null || yardBoss.Dead) && SquadAt(loadingExtraction))
                SetPhase(Phase.Complete);
        }

        public bool TryReserveSpecial(SquadMember target)
        {
            if (target == null || !target.Available || Time.time < specialAttackAvailableAt)
                return false;
            foreach (var member in SquadMember.All)
                if (member.held && member.body != null)
                    return false;
            specialAttackAvailableAt = Time.time + (SquadUnderPressure() ? 4.5f : 2.8f);
            return true;
        }

        public void CallNearbyInfected(Vector3 at)
        {
            if (!InCombat || SquadUnderPressure() || routes.Count >= 2)
                return;
            StationInfected.Noise(at, 20f);
            QueueReinforcements(2 + HumanPlayerCount(), at, false);
        }

        static readonly Vector3[] Wanderers =
        {
            new(-3.6f, 0, 0.65f), new(3.55f, 0, 1.55f), new(-3.2f, 0, 6f), new(2.4f, 0, 9.5f), new(-1f, 0, 17f), new(3.5f, 0, 21f),
            new(-3.8f, 0, 24f), new(0.5f, 0, 26f), new(3.4f, 0, 13.5f), new(-3.4f, 0, 13.5f), new(1f, 0, 33.5f),
        };

        /// <summary>Populate the search area and allow one extra pressure beat if the squad stalls at the crossing.</summary>
        void RunDirector()
        {
            if (!Networked && SessionMenu.Blocking && !initialPopulationPrepared)
            {
                initialPopulationPrepared = true;
                foreach (var point in Wanderers)
                    SpawnInfected(point.x, point.z, false, InfectedKind.Common, calm: true);
            }
            if (SessionMenu.Blocking)
                return;
            if (!ambientSpawned)
            {
                ambientSpawned = true;
                if (!Networked && botPrefab != null)
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Vector3 offset = SurvivorBot.OpeningOffset(slot);
                        Vector3 spawn = transform.position + offset;
                        if (UnityEngine.AI.NavMesh.SamplePosition(spawn, out var landing, 2f, UnityEngine.AI.NavMesh.AllAreas))
                            spawn = landing.position;
                        else
                            spawn = transform.position;
                        var bot = Instantiate(botPrefab, spawn, transform.rotation);
                        bot.Join(this, slot);
                    }
                if (!initialPopulationPrepared)
                    QueueReinforcements(5, transform.position, false, calm: true);
            }
            if (!cordonDisturbed && phase == Phase.ReceiveSignal && SquadFurthestZ() > -7.3f)
            {
                cordonDisturbed = true;
                var cordon = new Vector3(0f, 1.2f, 0.65f);
                StationInfected.Noise(cordon, 5.6f);
                if (hordeSound != null)
                    AudioSource.PlayClipAtPoint(hordeSound, cordon, 0.32f);
            }
            if (phase != Phase.FindTerminal || !routeEncounterStarted || crossingSurgeQueued || Time.time < crossingPressureAt)
                return;
            if (routes.Count > 0 || NearbyPressure() >= 2 || SquadUnderPressure())
                return;
            crossingSurgeQueued = true;
            QueueReinforcements(3 + HumanPlayerCount(), new Vector3(0, 0, 14f), true);
        }

        bool SquadAt(Transform destination)
        {
            int arrived = 0;
            foreach (var member in SquadMember.All)
            {
                if (!member.Available || member.body.GetComponent<SurvivorBot>() != null)
                    continue;
                if (!Near(member.body.position, destination.position, 2.4f))
                    return false;
                arrived++;
            }
            return arrived > 0;
        }

        internal static int HumanPlayerCount()
        {
            int count = 0;
            foreach (var member in SquadMember.All)
                if (member.body != null && member.body.GetComponent<SurvivorBot>() == null)
                    count++;
            return Mathf.Clamp(count, 1, 4);
        }

        bool SquadUnderPressure()
        {
            int humans = 0, standing = 0;
            foreach (var member in SquadMember.All)
            {
                if (member.body == null || member.body.GetComponent<SurvivorBot>() != null)
                    continue;
                humans++;
                if (!member.down && !member.held)
                    standing++;
            }
            return (humans > 0 && standing * 2 <= humans) || (!Networked && health < 35);
        }

        int NearbyPressure()
        {
            int count = 0;
            foreach (var threat in threats)
                if (threat != null && !threat.Dead && threat.Alerted && SquadNear(threat.transform.position, 12f))
                    count++;
            return count;
        }

        float PreparationTime => Mathf.Min(preparationSeconds, shutterDelay * 0.25f);
        public bool PreparingDefense => phase == Phase.Opening && countdown > shutterDelay - PreparationTime;


        /// <summary>The scream that tells you a horde is coming.</summary>
        public void PlayHorde(Vector3 entry)
        {
            if (hordeSound != null)
                AudioSource.PlayClipAtPoint(hordeSound, entry + Vector3.up, 0.9f);
            WarnOfAttack(entry);
        }

        public bool TryFocusFire()
        {
            if (Networked || Downed || !InCombat || SurvivorBot.All.Count == 0 ||
                !FindAimTarget(out var target, out _) || Vector3.Distance(transform.position, target.transform.position) > 17f)
                return false;
            focusTarget = target;
            focusUntil = Time.time + 5f;
            Signal("분대: 표시한 표적에 집중 사격.", 2f);
            return true;
        }

        /// <summary>Right click: shove nearby infected and release teammates they are holding.</summary>
        public bool TryShove()
        {
            if (pinnedBy != null || Downed || Time.time < shoveReadyAt || phase is Phase.Complete or Phase.Failed)
                return false;
            shoveReadyAt = Time.time + 0.7f;
            shoveStarted = Time.time;
            weaponAudio.pitch = 1f;
            weaponAudio.PlayOneShot(shoveSound, 0.7f);
            Vector3 forward = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up).normalized;
            bool connected = false;
            var shoved = new HashSet<StationInfected>();
            foreach (var hit in Physics.OverlapSphere(transform.position + forward * 0.9f + Vector3.up, 1.15f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var infected = hit.GetComponentInParent<StationInfected>();
                if (infected == null || infected.Dead || !shoved.Add(infected))
                    continue;
                Vector3 to = infected.transform.position - transform.position;
                to.y = 0;
                if (Vector3.Angle(forward, to) > 80f)
                    continue;
                infected.Shove(to.normalized);
                connected = true;
            }
            if (connected && fleshSound != null)
                weaponAudio.PlayOneShot(fleshSound, 0.5f);
            return true;
        }

        static float SquadFurthestZ()
        {
            float furthest = float.MinValue;
            foreach (var member in SquadMember.All)
                if (member.Available && member.body.GetComponent<SurvivorBot>() == null)
                    furthest = Mathf.Max(furthest, member.body.position.z);
            return furthest;
        }

        static bool SquadNear(Vector3 point, float radius)
        {
            foreach (var member in SquadMember.All)
                if (member.body != null && Near(member.body.position, point, radius))
                    return true;
            return false;
        }

        bool ProcessLocalInput()
        {
            if (SessionMenu.Blocking)
                return true;
            bool ended = phase is Phase.Complete or Phase.Failed;
            if (ended && Input.GetKeyDown(KeyCode.Return))
            {
                SceneManager.LoadScene(gameObject.scene.buildIndex);
                return false;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCapture(false);
            else if (Input.GetMouseButtonDown(0) && !ended && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCapture(true);
            }
            else if (!ended && Cursor.lockState == CursorLockMode.Locked)
            {
                Look();
                if (pinnedBy != null)
                {
                    if (Input.GetKeyDown(KeyCode.E))
                        StruggleFree();
                    return true;
                }
                var teammate = Input.GetKey(KeyCode.E) && !Downed ? DownedTeammate() : null;
                var downedBot = Input.GetKey(KeyCode.E) && !Downed && teammate == null ? DownedBot() : null;
                reviveProgress = teammate != null || downedBot != null ? reviveProgress + Time.deltaTime : 0f;
                if (reviveProgress >= ReviveSeconds)
                {
                    if (teammate != null) teammate.ReviveRpc();
                    if (downedBot != null) downedBot.Revive();
                    reviveProgress = 0f;
                }
                if (draggedBy == null && !Downed && teammate == null && downedBot == null)
                    Move();
                if (Input.GetKeyDown(KeyCode.E))
                    TryInteract();
                if (Input.GetMouseButtonDown(1))
                    TryShove();
                if (Input.GetKeyDown(KeyCode.Q))
                    TryFocusFire();
                if (Input.GetKeyDown(KeyCode.F))
                    flashlight.enabled = !flashlight.enabled;
                if (Input.GetKeyDown(KeyCode.R))
                    TryReload();
                if (Current.automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0))
                    TryFire();
                if (Input.GetKeyDown(KeyCode.Alpha1) && primary >= 0)
                    Equip(primary);
                if (Input.GetKeyDown(KeyCode.Alpha2))
                    Equip(0);
                if (Input.mouseScrollDelta.y != 0 && primary >= 0)
                    Equip(equipped == 0 ? primary : 0);
            }
            return true;
        }

        void Look()
        {
            player.transform.Rotate(Vector3.up, Input.GetAxisRaw("Mouse X") * mouseSensitivity);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, -83f, 83f);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Move()
        {
            Vector2 input = Vector2.ClampMagnitude(new Vector2(
                Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            Vector3 horizontal = player.transform.right * input.x + player.transform.forward * input.y;
            verticalSpeed = player.isGrounded ? -2f : verticalSpeed - gravity * Time.deltaTime;
            bool wading = drainSelected && transform.position.x < -1.1f &&
                transform.position.z >= 8.9f && transform.position.z <= 13.95f;
            float pace = wading ? 0.72f : 1f;
            Vector3 before = transform.position;
            player.Move((horizontal * (movementSpeed * pace) + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 travel = transform.position - before;
            travel.y = 0;
            isWalking = player.isGrounded && travel.sqrMagnitude > 0.00005f;
            if (isWalking)
            {
                stepDistance += travel.magnitude;
                stride += travel.magnitude * 6.8f;
                if (stepDistance > 0.96f)
                {
                    stepDistance = 0;
                    weaponAudio.PlayOneShot(stepSound, wading ? 0.21f : 0.36f);
                }
            }
        }

        /// <summary>E key: the shared mission step first (asked of the host when networked), then personal actions.</summary>
        public bool TryInteract()
        {
            if (phase is Phase.Complete or Phase.Failed || Downed)
                return false;
            if (MissionInteract(transform.position, apply: false))
            {
                if (Authority)
                    MissionInteract(transform.position, apply: true);
                else
                    MissionSync.Instance?.RequestInteractRpc(transform.position);
                return true;
            }
            if (TryRevive() || TryLoot() || TryPickUp())
                return true;
            if (tookSupply || !Nearby(supply))
                return false;
            tookSupply = true;
            foreach (var gun in arsenal)
                gun.reserve += gun.capacity * 2;
            health = Mathf.Min(100, health + 30);
            supply.gameObject.SetActive(false);
            Signal("보급함 · 탄약 보충, 응급처치", 4f);
            return true;
        }

        /// <summary>Checks (and with <paramref name="apply"/>, performs on the authority) the shared mission step for a survivor at <paramref name="at"/>.</summary>
        public bool MissionInteract(Vector3 at, bool apply)
        {
            Phase next = phase switch
            {
                Phase.ReceiveSignal when Near(at, radio.position) => Phase.ReadRoster,
                Phase.ReadRoster when Near(at, roster.position) => Phase.SelectRoute,
                Phase.SelectRoute when Near(at, drainGate.position, 2.8f) || Near(at, staffGate.position, 2.8f) => Phase.FindTerminal,
                Phase.FindTerminal when Near(at, terminal.position) => Phase.Opening,
                Phase.FindWitness when Near(at, witness.transform.position) => Phase.Evacuate,
                Phase.Maintenance when Near(at, servicePump.position) => Phase.TransferDefense,
                _ => phase
            };
            if (next == phase)
                return false;
            if (apply)
            {
                if (next == Phase.FindTerminal)
                    drainSelected = Near(at, drainGate.position, 2.8f);
                SetPhase(next);
            }
            return true;
        }

        /// <summary>Authority only: move the mission on and mirror it to every client.</summary>
        void SetPhase(Phase next)
        {
            if (MissionSync.Instance != null)
                MissionSync.Instance.Publish((int)next, drainSelected);
            ApplyPhase(next);
        }

        /// <summary>Runs on a client when the host's mission phase changes.</summary>
        public void ApplyPhase(int value, bool drain)
        {
            drainSelected = drain;
            var next = (Phase)value;
            if (next is Phase.Opening or Phase.FindWitness or Phase.Evacuate or Phase.Maintenance or Phase.TransferDefense or Phase.LoadingEscort && activeGate == null)
                ChooseRoute(drainSelected ? drainGate : staffGate, drainSelected);
            ApplyPhase(next);
        }

        void ApplyPhase(Phase next)
        {
            if (next == phase)
                return;
            phase = next;
            switch (next)
            {
                case Phase.ReadRoster:
                    Signal("서이경: 3번 승강장이에요. 열한 명이 있어요. 다음 안내방송 전에 와 주세요.");
                    break;
                case Phase.SelectRoute:
                    Signal("대피 명부: 열한 명 모두 대피 완료. 열차 기록은 없다. 게이트는 하나만 열린다.");
                    break;
                case Phase.FindTerminal:
                    ChooseRoute(drainSelected ? drainGate : staffGate, drainSelected);
                    break;
                case Phase.Opening:
                    countdown = shutterDelay;
                    if (Authority)
                    {
                        routes.Clear();
                        wave = 0;
                        nextWave = PreparationTime;
                    }
                    Signal("전력 복구 중. 양쪽 엄폐물에서 보급하고 셔터를 지켜라.");
                    break;
                case Phase.FindWitness:
                    OpenShutter();
                    break;
                case Phase.Evacuate:
                    Signal("서이경: 나머지는 환기구로 갔어요. 생존자는 모두 승강기로!");
                    if (Authority)
                    {
                        witness.EvacuateWith(this, evacuationPoint);
                        routes.Clear();
                        int pursuit = 2 + HumanPlayerCount();
                        QueueReinforcements(pursuit, new Vector3(0, 0, 24f), true);
                    }
                    break;
                case Phase.Maintenance:
                    Signal("서이경: 승강기에 전기가 없어요. 정비동에서 적재장으로 갈 수 있어요.");
                    if (Authority)
                    {
                        routes.Clear();
                        witness.EvacuateWith(this, serviceShutter.position - Vector3.forward * 2.2f);
                    }
                    break;
                case Phase.TransferDefense:
                    countdown = serviceTransferSeconds;
                    Signal("유압 펌프 가동. 셔터가 열릴 때까지 버텨요.");
                    if (Authority)
                    {
                        routes.Clear();
                        if (spawnSpecials && yardBoss == null)
                        {
                            yardBoss = SpawnInfected(3.25f, 64.25f, false, InfectedKind.Brute, calm: true);
                            yardBoss.StageEncounter();
                        }
                        QueueReinforcements(3 + HumanPlayerCount(), new Vector3(-4.3f, 0, 42.8f),
                            true, spawnSpecials && !SquadUnderPressure() ? InfectedKind.Dragger : InfectedKind.Common);
                    }
                    break;
                case Phase.LoadingEscort:
                    Signal("서이경: 셔터가 열렸어요. 초록 조명 아래로 가요.");
                    if (Authority)
                    {
                        routes.Clear();
                        witness.EvacuateWith(this, loadingExtraction);
                    }
                    break;
                case Phase.Complete:
                    Signal("구조 완료 · 다음 신호: 7번 환기구", 12f);
                    SetCapture(false);
                    break;
                case Phase.Failed:
                    SetCapture(false);
                    break;
            }
        }

        /// <summary>Clients mirror the host's countdown for the HUD.</summary>
        public void MirrorCountdown(float value) => countdown = value;

        void ChooseRoute(Transform gate, bool flooded)
        {
            activeGate = gate;
            raisedGatePosition = gate.position + Vector3.up * 2.75f;
            if (flooded)
                Signal("A 침수 통로 · 물 때문에 느려진다. 물속에 감염체가 있다.");
            else
                Signal("B 직원 통로 · 오른쪽 보급함을 챙겨라.");
        }

        void Signal(string message, float seconds = 5f)
        {
            radioMessage = message;
            radioUntil = Time.time + seconds;
            weaponAudio.PlayOneShot(signalSound, 0.55f);
        }

        public bool TryFire()
        {
            var gun = Current;
            if (pinnedBy != null || phase is Phase.Complete or Phase.Failed || gun.rounds == 0 || reloadUntil > 0 || raise > 0.35f ||
                Time.time < nextShotAt)
                return false;
            gun.rounds--;
            nextShotAt = Time.time + gun.fireInterval;
            flashUntil = Time.time + 0.048f;
            weaponKick = gun.kick;
            weaponAudio.pitch = gun.pitch * Random.Range(0.96f, 1.04f);
            weaponAudio.PlayOneShot(gun.shot);
            if (Authority)
                StationInfected.Noise(transform.position, 24f);
            if (gun.pellets == 1 && !gun.pierces)
            {
                if (FindAimTarget(out StationInfected infected, out Vector3 hitPoint))
                    Damage(infected, hitPoint, gun.damage);
                return true;
            }
            for (int pellet = 0; pellet < gun.pellets; pellet++)
            {
                Vector2 cone = Random.insideUnitCircle * Mathf.Tan(gun.spread * Mathf.Deg2Rad);
                Vector3 aim = (eyes.transform.forward + eyes.transform.right * cone.x + eyes.transform.up * cone.y).normalized;
                int count = Physics.RaycastNonAlloc(eyes.transform.position, aim, aimHits, range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(aimHits, 0, count, HitDistance);
                for (int index = 0; index < count; index++)
                {
                    var infected = aimHits[index].collider.GetComponent<StationInfected>();
                    if (infected == null)
                        break; // wall or prop stops the round
                    if (infected.Dead)
                        continue;
                    Damage(infected, aimHits[index].point, gun.damage);
                    if (!gun.pierces)
                        break;
                }
            }
            return true;
        }

        static readonly IComparer<RaycastHit> HitDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        void Damage(StationInfected infected, Vector3 point, int damage)
        {
            bool head = point.y - infected.transform.position.y >= 1.49f;
            if (fleshSound != null)
                AudioSource.PlayClipAtPoint(fleshSound, point, head ? 0.9f : 0.6f);
            infected.Hit(head ? damage * 2 : damage, point, point - eyes.transform.position);
            hitUntil = Time.time + 0.23f;
            if (infected.Dead)
                kills++;
        }

        /// <summary>Switch to an owned gun by arsenal index (0 = pistol).</summary>
        public void Equip(int index)
        {
            if (!arsenal[index].owned || (index == equipped && arsenal[index].view.gameObject.activeSelf))
                return;
            Current.view.gameObject.SetActive(false);
            Current.muzzle.SetActive(false);
            equipped = index;
            reloadUntil = 0;
            raise = 1f;
            Current.view.gameObject.SetActive(true);
        }

        bool TryPickUp()
        {
            foreach (var pickup in pickups)
            {
                if (!pickup.gameObject.activeSelf || !Nearby(pickup.transform, 1.6f))
                    continue;
                if (primary >= 0)
                {
                    arsenal[primary].owned = false;
                    foreach (var other in pickups)
                        if (other.arsenalIndex == primary)
                            other.gameObject.SetActive(true);
                }
                primary = pickup.arsenalIndex;
                arsenal[primary].owned = true;
                pickup.gameObject.SetActive(false);
                Equip(primary);
                Signal($"{arsenal[primary].label} 획득 · 1 주무기 / 2 권총", 3.5f);
                return true;
            }
            return false;
        }

        /// <summary>The pickup the player could take right now, if any.</summary>
        WeaponPickup NearbyPickup()
        {
            foreach (var pickup in pickups)
                if (pickup.gameObject.activeSelf && Nearby(pickup.transform, 1.6f))
                    return pickup;
            return null;
        }

        bool FindAimTarget(out StationInfected target, out Vector3 point)
        {
            Vector3 origin = eyes.transform.position;
            Vector3 aim = eyes.transform.forward;
            target = null;
            point = default;
            float obstruction = range;
            if (Physics.Raycast(origin, aim, out RaycastHit direct, range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                target = direct.collider.GetComponent<StationInfected>();
                if (target != null && !target.Dead)
                {
                    point = direct.point;
                    return true;
                }
                target = null;
                obstruction = direct.distance;
            }

            // A little tolerance at the silhouette, with a separate line-of-sight check so cover still works.
            int count = Physics.SphereCastNonAlloc(origin, 0.15f, aim, aimHits, obstruction,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = obstruction;
            for (int index = 0; index < count; index++)
            {
                RaycastHit contact = aimHits[index];
                var candidate = contact.collider.GetComponent<StationInfected>();
                if (candidate == null || candidate.Dead || contact.distance >= nearest)
                    continue;
                Vector3 visiblePoint = candidate.transform.position + Vector3.up * Mathf.Clamp(contact.point.y -
                    candidate.transform.position.y, 0.38f, 1.55f);
                if (Physics.Linecast(origin, visiblePoint, out RaycastHit blocked, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) && blocked.collider != contact.collider)
                    continue;
                target = candidate;
                point = visiblePoint;
                nearest = contact.distance;
            }
            return target != null;
        }

        /// <summary>A leaper lands on the player: no moving or shooting until it dies or the player struggles free.</summary>
        public bool TryPin(StationInfected source)
        {
            if (!InCombat || Held)
                return false;
            pinnedBy = source;
            escapeCount = 0;
            Signal("덮쳤다! E를 연타해 떨쳐내라", 3f);
            return true;
        }

        /// <summary>A dragger hooks the player and reels them in; shooting it breaks the hold.</summary>
        public bool TryDrag(StationInfected source)
        {
            if (!InCombat || Held)
                return false;
            draggedBy = source;
            Signal("끌려간다! 끌개를 쏴라", 3f);
            return true;
        }

        public void ReleaseFrom(StationInfected source)
        {
            if (pinnedBy == source) pinnedBy = null;
            if (draggedBy == source) draggedBy = null;
        }

        /// <summary>Player-side struggle against a pin; also callable by tests.</summary>
        public void StruggleFree()
        {
            if (pinnedBy != null && ++escapeCount >= escapePresses)
                pinnedBy.ReleaseHold();
        }

        public void Knockback(Vector3 push, int damage)
        {
            knock = push + Vector3.up * 2.5f;
            TakeHit(damage);
        }

        /// <summary>Bloater burst: blinds anyone close and calls a crowd.</summary>
        public void Burst(Vector3 point, float radius)
        {
            if (!InCombat)
                return;
            foreach (var member in SquadMember.All)
                if (member.body != null && Vector3.Distance(point, member.body.position) <= radius)
                    member.blind(4.5f);
            if (Authority)
                QueueReinforcements(4, point, true);
        }

        /// <summary>Bile in the eyes: screen goes green for a few seconds.</summary>
        public void Blind(float seconds)
        {
            blindUntil = Time.time + seconds;
            Signal("담즙을 뒤집어썼다 · 무리가 몰려온다", 3f);
        }

        void UpdateHolds()
        {
            if (pinnedBy != null && pinnedBy.Dead) pinnedBy = null;
            if (draggedBy != null && draggedBy.Dead) draggedBy = null;
            if (Held)
            {
                holdDamage += Time.deltaTime * (pinnedBy != null ? 9f : 3f);
                if (holdDamage >= 1f)
                {
                    TakeHit(Mathf.FloorToInt(holdDamage));
                    holdDamage %= 1f;
                }
            }
            if (draggedBy != null)
            {
                Vector3 pull = draggedBy.transform.position - transform.position;
                pull.y = 0;
                if (pull.magnitude > 1.1f)
                    player.Move((pull.normalized * 2.4f + Vector3.down * 2f) * Time.deltaTime);
            }
            // Pinned players are knocked flat: the eye drops toward the floor and eases back up after.
            Vector3 eye = eyes.transform.localPosition;
            eye.y = Mathf.MoveTowards(eye.y, pinnedBy != null || Downed ? 0.55f : 1.65f, Time.deltaTime * 4f);
            eyes.transform.localPosition = eye;
            if (knock.sqrMagnitude > 0.01f)
            {
                player.Move(knock * Time.deltaTime);
                knock = Vector3.MoveTowards(knock, Vector3.zero, Time.deltaTime * 16f);
            }
        }

        public void WarnOfAttack(Vector3 position)
        {
            warningPoint = position;
            warningUntil = Time.time + 0.70f;
        }

        public bool TryReload()
        {
            var gun = Current;
            if (phase is Phase.Complete or Phase.Failed || gun.rounds == gun.capacity || gun.reserve == 0 || reloadUntil > 0)
                return false;
            reloadUntil = Time.time + gun.reloadDuration;
            weaponAudio.PlayOneShot(reloadSound);
            return true;
        }

        void UpdateWeapon()
        {
            var gun = Current;
            weaponKick = Mathf.MoveTowards(weaponKick, 0, Time.deltaTime * 7.5f);
            raise = Mathf.MoveTowards(raise, 0, Time.deltaTime * 3.2f);
            gun.muzzle.SetActive(Time.time < flashUntil);
            float drop = 0;
            if (reloadUntil > 0)
            {
                if (Time.time >= reloadUntil)
                {
                    int transfer = Mathf.Min(gun.reserve, gun.capacity - gun.rounds);
                    gun.rounds += transfer;
                    gun.reserve -= transfer;
                    reloadUntil = 0;
                }
                else
                {
                    float progress = 1f - (reloadUntil - Time.time) / gun.reloadDuration;
                    drop = Mathf.Sin(progress * Mathf.PI);
                }
            }
            strideWeight = Mathf.MoveTowards(strideWeight, isWalking ? 1 : 0, Time.deltaTime * 6f);
            float bob = Mathf.Sin(stride) * 0.012f * strideWeight;
            float sway = Mathf.Sin(stride * 0.5f) * 0.011f * strideWeight;
            drop = Mathf.Max(drop, raise);
            float shove = Mathf.Clamp01(1f - (Time.time - shoveStarted) / 0.35f);
            shove = Mathf.Sin(shove * Mathf.PI) * (shove > 0 ? 1 : 0);
            gun.view.localPosition = gun.rest + new Vector3(sway - drop * 0.08f - shove * 0.1f, bob - drop * 0.15f + shove * 0.03f,
                -weaponKick * 0.06f + shove * 0.16f);
            gun.view.localRotation = gun.restRotation * Quaternion.Euler(-weaponKick * 5.5f + drop * 17f, -shove * 25f,
                drop * -18f + shove * 35f);
            if (gun.magazine != null)
                gun.magazine.localPosition = gun.magazineRest + Vector3.down * drop * 0.105f;
            if (gun.slide != null)
                gun.slide.localPosition = gun.slideRest + gun.slide.parent.InverseTransformVector(-gun.view.forward * (weaponKick * 0.020f));
            if (gun.supportHand != null)
                gun.supportHand.localPosition = gun.supportRest + new Vector3(-drop * 0.035f, -drop * 0.095f, -drop * 0.13f);
            damageFlash = Mathf.MoveTowards(damageFlash, 0, Time.deltaTime * 2.2f);
        }

        void SpawnWave()
        {
            bool underPressure = SquadUnderPressure();
            if (wave > 0 && (routes.Count > 0 || NearbyPressure() >= (underPressure ? 2 : 4)))
            {
                nextWave = underPressure ? 3f : 1.5f;
                return;
            }

            int count = Mathf.Clamp(3 + HumanPlayerCount() + wave - (underPressure ? 2 : 0), 2, 10);
            nextWave = waveInterval + (underPressure ? 3f : 0f);
            bool fromFarSide = wave % 2 == 0;
            var special = spawnSpecials && !underPressure && wave >= 1
                ? (InfectedKind)(1 + specialTurn++ % 3)
                : InfectedKind.Common;
            wave++;
            QueueReinforcements(count, new Vector3(0, 0, fromFarSide ? 28f : 13f), true, special);
        }

        /// <summary>Spawn one infected; public so tests can stage a specific special.</summary>
        public StationInfected SpawnInfected(float x, float z, bool runner, InfectedKind kind = InfectedKind.Common,
            bool calm = false)
        {
            var prefab = kind == InfectedKind.Brute && bossPrefab != null ? bossPrefab :
                kind == InfectedKind.Common && commonPrefabs != null && commonPrefabs.Length > 0
                    ? commonPrefabs[Random.Range(0, commonPrefabs.Length)] : infectedPrefab;
            var infected = Instantiate(prefab, new Vector3(x, 0.03f, z), Quaternion.Euler(0, Random.Range(0, 360f), 0));
            infected.Follow(this, runner, kind, calm);
            if (Networked)
                infected.NetworkObject.Spawn(true);
            threats.Add(infected);
            return infected;
        }

        public void TakeHit(int damage)
        {
            if (!InCombat)
                return;
            if (Downed)
                return;
            health = Mathf.Max(0, health - damage);
            damageFlash = 0.38f;
            if (health > 0)
                return;
            if (!Networked && SurvivorBot.All.Count == 0)
            {
                SetPhase(Phase.Failed);
                return;
            }
            // Co-op: fall down and wait for a teammate; the host fails the mission only when everyone is down.
            Downed = true;
            if (pinnedBy != null) pinnedBy.ReleaseHold();
            if (draggedBy != null) draggedBy.ReleaseHold();
            Signal("쓰러졌다 · 팀원이 일으켜 줄 때까지 버텨라", 6f);
            SurvivorAvatar.Mine?.ReportDowned(true);
            CheckSoloWipe();
        }

        /// <summary>A teammate finished picking this player up.</summary>
        public void Revive()
        {
            if (!Downed)
                return;
            Downed = false;
            health = 40;
            Signal("다시 일어났다", 3f);
            SurvivorAvatar.Mine?.ReportDowned(false);
        }

        /// <summary>Authority: everybody is down.</summary>
        public void SquadWiped()
        {
            if (phase is not (Phase.Complete or Phase.Failed))
                SetPhase(Phase.Failed);
        }

        bool Nearby(Transform marker, float radius = 2.2f) => Near(player.transform.position, marker.position, radius);

        static bool Near(Vector3 at, Vector3 marker, float radius = 2.2f)
        {
            Vector3 offset = at - marker;
            offset.y = 0;
            return offset.sqrMagnitude <= radius * radius;
        }

        void OpenShutter()
        {
            exitLight.color = new Color(0.52f, 1f, 0.73f);
            Signal("서이경: 초록 비상등이 보여요. 셔터 너머 통로에 있어요.");
        }

        static void SetCapture(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        void OnDestroy()
        {
            SetCapture(false);
            SquadMember.All.Remove(self);
        }

        /// <summary>The E action available right here, if any.</summary>
        string Prompt()
        {
            if (Downed || phase is Phase.Complete or Phase.Failed)
                return null;
            if (DownedTeammate() != null || DownedBot() != null) return "E 길게 · 팀원 일으키기";
            string mission = phase switch
            {
                Phase.ReceiveSignal when Nearby(radio) => "E · 비상전화 받기",
                Phase.ReadRoster when Nearby(roster) => "E · 대피 명부 읽기",
                Phase.SelectRoute when Nearby(drainGate, 2.8f) => "E · 셔터 열기",
                Phase.SelectRoute when Nearby(staffGate, 2.8f) => "E · 셔터 열기",
                Phase.FindTerminal when Nearby(terminal) => "E · 비상 전력 켜기",
                Phase.FindWitness when Nearby(witness.transform) => "E · 서이경 구조",
                Phase.Maintenance when Nearby(servicePump) => "E · 유압 펌프 가동",
                _ => null
            };
            if (mission != null) return mission;
            var loot = NearbyLoot();
            if (loot != null) return $"E · {loot.Label}";
            var pickup = NearbyPickup();
            if (pickup != null) return $"E · {arsenal[pickup.arsenalIndex].label} 줍기";
            if (!tookSupply && Nearby(supply)) return "E · 보급함 열기";
            if (InCombat && reloadUntil <= 0 && Current.rounds == 0 && Current.reserve > 0) return "R · 재장전";
            if (aimedAtInfected && !Networked && SurvivorBot.All.Count > 0 && FocusTarget == null)
                return "Q · 분대 집중 사격";
            return null;
        }

        void Text(Rect area, string text, GUIStyle style, float alpha = 1f)
        {
            Color original = style.normal.textColor;
            shadowStyle.fontSize = style.fontSize;
            shadowStyle.alignment = style.alignment;
            shadowStyle.fontStyle = style.fontStyle;
            shadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f * alpha);
            GUI.Label(new Rect(area.x + 1.5f, area.y + 1.5f, area.width, area.height), text, shadowStyle);
            style.normal.textColor = new Color(original.r, original.g, original.b, alpha);
            GUI.Label(area, text, style);
            style.normal.textColor = original;
        }

        void OnGUI()
        {
            if (Application.isBatchMode || SessionMenu.Blocking)
                return;
            caption ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.93f, 0.95f, 0.92f) }
            };
            shadowStyle ??= new GUIStyle(caption);
            statusStyle ??= new GUIStyle(caption) { alignment = TextAnchor.MiddleLeft, fontSize = 15 };
            titleLabel ??= new GUIStyle(statusStyle) { fontSize = 30, fontStyle = FontStyle.Bold };
            smallLabel ??= new GUIStyle(caption) { fontSize = 15 };
            ammoLabel ??= new GUIStyle(caption) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            float scale = Mathf.Min(Screen.height / 720f, Screen.width / 920f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            DrawHud(Screen.width / scale, Screen.height / scale);
            GUI.matrix = previous;
        }

        void DrawHud(float width, float height)
        {
            // Full-screen states first.
            if (Time.time < blindUntil)
                Panel(new Rect(0, 0, width, height), new Color(0.32f, 0.42f, 0.12f, Mathf.Clamp01((blindUntil - Time.time) / 2f) * 0.82f));
            if (damageFlash > 0)
                Panel(new Rect(0, 0, width, height), new Color(0.49f, 0.065f, 0.035f, damageFlash));
            if (Downed)
                Panel(new Rect(0, 0, width, height), new Color(0.35f, 0.02f, 0.02f, 0.3f));
            if (Time.time < warningUntil)
                DrawThreatEdge(width, height);

            if (phase is Phase.Complete or Phase.Failed)
            {
                float center = width * 0.5f, top = height * 0.5f - 100;
                Panel(new Rect(0, 0, width, height), new Color(0, 0.02f, 0.025f, 0.73f));
                Panel(new Rect(center - 218, top, 436, 204), HudSurface);
                Panel(new Rect(center - 218, top, 436, 3), HudAccent);
                Text(new Rect(center - 194, top + 18, 388, 28), "열한 번째 이름  /  3번 승강장", smallLabel);
                Text(new Rect(center - 194, top + 53, 388, 54), phase == Phase.Complete ? "구조 완료" : "구조 실패", new GUIStyle(caption) { fontSize = 42, fontStyle = FontStyle.Bold });
                Text(new Rect(center - 194, top + 115, 388, 26), phase == Phase.Complete ? $"서이경 이송 완료   ·   내 처치 {kills}" : $"구조팀 통신 두절   ·   내 처치 {kills}", smallLabel);
                Text(new Rect(center - 194, top + 157, 388, 27), "Enter  ·  브리핑으로", smallLabel);
                return;
            }

            // Centre: crosshair and whatever is happening to you.
            DrawCrosshair(width * 0.5f, height * 0.5f);
            string alert = pinnedBy != null ? $"E 연타!  {escapeCount}/{escapePresses}" :
                draggedBy != null ? "끌개를 쏴라!" : Downed ? "쓰러짐 · 팀원을 기다려라" : null;
            if (alert != null)
                Text(new Rect(0, height * 0.5f - 90, width, 34), alert, new GUIStyle(caption) { fontSize = 24, fontStyle = FontStyle.Bold });
            if (reviveProgress > 0)
            {
                Panel(new Rect(width * 0.5f - 110, height * 0.5f + 60, 220, 6), new Color(0, 0, 0, 0.6f));
                Panel(new Rect(width * 0.5f - 110, height * 0.5f + 60, 220 * reviveProgress / ReviveSeconds, 6), new Color(0.75f, 0.9f, 0.75f));
            }
            string prompt = Cursor.lockState != CursorLockMode.Locked && acceptLocalInput ? "클릭 · 조작 재개" : Prompt();
            if (prompt != null)
                DrawPrompt(width * 0.5f, height * 0.5f + 76, prompt);

            if (phase is Phase.Opening or Phase.TransferDefense)
                DrawShutterTimer();

            if (Time.time < radioUntil && alert == null)
            {
                float lineWidth = Mathf.Clamp(smallLabel.CalcSize(new GUIContent(radioMessage)).x + 32, 110, width - 420);
                float x = (width - lineWidth) * 0.5f;
                Panel(new Rect(x, height - 106, lineWidth, 35), HudSurface);
                Text(new Rect(x + 9, height - 106, lineWidth - 18, 35), radioMessage, smallLabel);
            }

            float y = height - 86;
            Panel(new Rect(24, y, 194, 62), HudSurface);
            Text(new Rect(36, y + 4, 64, 32), $"{health}", titleLabel);
            Text(new Rect(106, y + 7, 82, 26), "체력", statusStyle);
            Panel(new Rect(36, y + 46, 170, 5), new Color(0.22f, 0.28f, 0.28f));
            Panel(new Rect(36, y + 46, 170 * Mathf.Clamp01(health / 100f), 5), health < 35 ? new Color(0.9f, 0.3f, 0.2f) :
                health < 70 ? HudAccent : new Color(0.55f, 0.85f, 0.65f));
            int mate = 0;
            foreach (var survivor in FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None))
            {
                if (survivor == SurvivorAvatar.Mine)
                    continue;
                mate++;
                Text(new Rect(30, y - 26 * mate - 6, 220, 24), survivor.Down ? $"팀원 {mate} · 쓰러짐" : $"팀원 {mate}", statusStyle);
            }
            for (int slot = 0; slot < SurvivorBot.All.Count; slot++)
            {
                var bot = SurvivorBot.All[slot];
                float rowY = y - (SurvivorBot.All.Count - slot) * 24 - 6;
                Panel(new Rect(24, rowY, 194, 22), HudSurface);
                string status = bot.Down || bot.IsHeld ? $"  {bot.ActionLabel}" : "";
                Text(new Rect(34, rowY, 130, 22), bot.Label + status, statusStyle);
                Panel(new Rect(155, rowY + 9, 51, 4), new Color(0.24f, 0.30f, 0.30f));
                Panel(new Rect(155, rowY + 9, 51 * Mathf.Clamp01(bot.Health / 150f), 4), bot.Down ? HudAccent : new Color(0.48f, 0.82f, 0.76f));
            }

            // Bottom right: ammo.
            Panel(new Rect(width - 218, y, 194, 62), HudSurface);
            Text(new Rect(width - 206, y + 1, 170, 38), $"{Current.rounds} / {Current.reserve}", ammoLabel);
            Text(new Rect(width - 206, y + 37, 170, 20), reloadUntil > 0 ? "재장전 중" : Current.label,
                new GUIStyle(smallLabel) { alignment = TextAnchor.MiddleRight });

            DrawFocusMarker(width, height);
            DrawNameTags(width, height);
        }

        void DrawPrompt(float center, float y, string prompt)
        {
            int separator = prompt.IndexOf(" · ", System.StringComparison.Ordinal);
            if (separator < 0)
                return;
            string key = prompt[..separator];
            string action = prompt[(separator + 3)..];
            float keyWidth = smallLabel.CalcSize(new GUIContent(key)).x + 20;
            float width = keyWidth + smallLabel.CalcSize(new GUIContent(action)).x + 38;
            float x = center - width * 0.5f;
            Panel(new Rect(x, y, width, 38), HudSurface);
            Panel(new Rect(x + 5, y + 5, keyWidth, 28), new Color(0.29f, 0.22f, 0.12f));
            Text(new Rect(x + 5, y, keyWidth, 38), key, smallLabel);
            Text(new Rect(x + keyWidth + 12, y, width - keyWidth - 17, 38), action, smallLabel);
        }

        void DrawShutterTimer()
        {
            float seconds = Mathf.Max(0, countdown);
            float shown = PreparingDefense ? seconds - (shutterDelay - PreparationTime) : seconds;
            Panel(new Rect(24, 24, 142, 38), HudSurface);
            Text(new Rect(36, 25, 124, 33), $"{(PreparingDefense ? "준비" : "셔터")} {Mathf.CeilToInt(shown)}초", statusStyle);
            Panel(new Rect(36, 55, 118 * Mathf.Clamp01(1f - seconds / (phase == Phase.TransferDefense ? serviceTransferSeconds : shutterDelay)), 2), HudAccent);
        }

        /// <summary>Teammate names over their heads, like a squad shooter.</summary>
        void DrawNameTags(float width, float height)
        {
            var tag = new GUIStyle(smallLabel) { fontSize = 14 };
            foreach (var bot in SurvivorBot.All)
            {
                if (!bot.Down && !bot.IsHeld)
                    continue;
                Vector3 head = bot.transform.position + Vector3.up * 2.05f;
                Vector3 screen = eyes.WorldToViewportPoint(head);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1)
                    continue;
                if (Physics.Linecast(eyes.transform.position, head, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !hit.transform.IsChildOf(bot.transform))
                    continue;
                float x = screen.x * width, y = (1 - screen.y) * height;
                Color tint = bot.Down ? new Color(1f, 0.45f, 0.35f) : new Color(0.75f, 0.9f, 1f);
                tag.normal.textColor = tint;
                const float labelWidth = 100f;
                Panel(new Rect(x - labelWidth / 2, y - 10, labelWidth, 20), HudSurface);
                Text(new Rect(x - labelWidth / 2, y - 10, labelWidth, 20), $"{bot.Label} · {bot.ActionLabel}", tag);
            }
        }

        void DrawThreatEdge(float width, float height)
        {
            Vector3 local = eyes.transform.InverseTransformPoint(warningPoint);
            float alpha = Mathf.Clamp01((warningUntil - Time.time) / 0.7f) * 0.55f;
            var red = new Color(0.8f, 0.05f, 0.03f, alpha);
            if (local.z < -0.6f) Panel(new Rect(0, height - 26, width, 26), red);
            else if (local.x < -0.55f) Panel(new Rect(0, 0, 26, height), red);
            else if (local.x > 0.55f) Panel(new Rect(width - 26, 0, 26, height), red);
            else Panel(new Rect(0, 0, width, 18), red);
        }

        void DrawCrosshair(float x, float y)
        {
            bool hit = Time.time < hitUntil;
            Color ink = aimedAtInfected ? new Color(1f, 0.7f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
            float gap = 5f + weaponKick * 6f;
            Panel(new Rect(x - gap - 7, y - 1, 7, 2), ink);
            Panel(new Rect(x + gap, y - 1, 7, 2), ink);
            Panel(new Rect(x - 1, y - gap - 7, 2, 7), ink);
            Panel(new Rect(x - 1, y + gap, 2, 7), ink);
            if (!hit)
                return;
            // Hit marker: four short diagonal ticks.
            var tick = new Color(1f, 0.35f, 0.25f, 0.95f);
            for (int corner = 0; corner < 4; corner++)
            {
                float dx = corner % 2 == 0 ? -1 : 1, dy = corner < 2 ? -1 : 1;
                for (int step = 0; step < 4; step++)
                    Panel(new Rect(x + dx * (9 + step * 2) - 1, y + dy * (9 + step * 2) - 1, 2, 2), tick);
            }
        }

        void DrawFocusMarker(float width, float height)
        {
            var target = FocusTarget;
            if (target == null)
                return;
            Vector3 point = target.transform.position + Vector3.up * (1.28f * target.transform.lossyScale.y);
            if (Physics.Linecast(eyes.transform.position, point, out var obstruction, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && !obstruction.transform.IsChildOf(target.transform))
                return;
            Vector3 label = eyes.WorldToViewportPoint(point + Vector3.up * 0.83f);
            if (label.z <= 0 || label.x < 0.05f || label.x > 0.95f || label.y < 0.06f || label.y > 0.94f)
                return;
            float x = label.x * width, y = (1f - label.y) * height;
            Panel(new Rect(x - 25, y - 18, 50, 22), HudSurface);
            Panel(new Rect(x - 25, y + 3, 50, 1), HudAccent);
            Text(new Rect(x - 25, y - 18, 50, 22), "집중", smallLabel);
        }

        static void Panel(Rect area, Color tint)
        {
            Color original = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = original;
        }
    }
}
