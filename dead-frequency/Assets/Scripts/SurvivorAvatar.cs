using Unity.Netcode;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// The networked body of one survivor (the session's player prefab). The owner drives it from their local
    /// first-person player; everyone else sees the rigged model. On the host, remote bodies become infected targets
    /// whose hits and holds are forwarded to the owning client.
    /// </summary>
    public sealed class SurvivorAvatar : NetworkBehaviour
    {
        public static SurvivorAvatar Mine { get; private set; }

        [SerializeField] Animation rig;
        [SerializeField] AnimationClip idleClip;
        [SerializeField] AnimationClip runClip;

        readonly NetworkVariable<bool> down = new(writePerm: NetworkVariableWritePermission.Owner);
        SquadMember member;
        Vector3 lastPosition;
        Quaternion restRotation;
        Vector3 restRigPosition;
        Transform hips;
        float smoothedSpeed;

        public bool Down => down.Value;

        public override void OnNetworkSpawn()
        {
            SquadCollision.Register(GetComponent<Collider>());
            restRotation = rig.transform.localRotation;
            restRigPosition = rig.transform.localPosition;
            hips = rig.GetComponentInChildren<SkinnedMeshRenderer>()?.rootBone;
            rig.AddClip(idleClip, "idle");
            rig.AddClip(runClip, "run");
            rig.wrapMode = WrapMode.Loop;
            if (IsOwner)
            {
                Mine = this;
                foreach (var part in GetComponentsInChildren<Renderer>())
                    part.enabled = false; // first-person: never see your own body
            }
            if (IsServer && !IsOwner)
            {
                member = new SquadMember
                {
                    body = transform,
                    hurt = damage => HurtRpc(damage),
                    knock = (push, damage) => KnockRpc(push, damage),
                    warn = point => WarnRpc(point),
                    pin = source => { PinRpc(source.NetworkObject); return true; },
                    drag = source => { DragRpc(source.NetworkObject); return true; },
                    release = source => ReleaseRpc(source.NetworkObject),
                    blind = seconds => BlindRpc(seconds),
                };
                SquadMember.All.Add(member);
                down.OnValueChanged += (_, value) => { member.down = value; CheckWipe(); };
            }
            if (IsServer && IsOwner)
                down.OnValueChanged += (_, _) => CheckWipe();
        }

        public override void OnNetworkDespawn()
        {
            SquadCollision.Unregister(GetComponent<Collider>());
            if (member != null)
                SquadMember.All.Remove(member);
            if (Mine == this)
                Mine = null;
        }

        static void CheckWipe()
        {
            foreach (var survivor in FindObjectsByType<SurvivorAvatar>(FindObjectsSortMode.None))
                if (!survivor.Down)
                    return;
            StationTrial.Local?.SquadWiped();
        }

        /// <summary>Owner: tell everyone whether this survivor is on the floor.</summary>
        public void ReportDowned(bool isDown) => down.Value = isDown;

        /// <summary>A teammate finished picking this survivor up.</summary>
        [Rpc(SendTo.Owner)]
        public void ReviveRpc() => StationTrial.Local?.Revive();

        [Rpc(SendTo.Owner)] void HurtRpc(int damage) => StationTrial.Local?.TakeHit(damage);
        [Rpc(SendTo.Owner)] void KnockRpc(Vector3 push, int damage) => StationTrial.Local?.Knockback(push, damage);
        [Rpc(SendTo.Owner)] void WarnRpc(Vector3 point) => StationTrial.Local?.WarnOfAttack(point);
        [Rpc(SendTo.Owner)] void BlindRpc(float seconds) => StationTrial.Local?.Blind(seconds);

        [Rpc(SendTo.Owner)]
        void PinRpc(NetworkObjectReference source)
        {
            if (source.TryGet(out NetworkObject infected))
                StationTrial.Local?.TryPin(infected.GetComponent<StationInfected>());
        }

        [Rpc(SendTo.Owner)]
        void DragRpc(NetworkObjectReference source)
        {
            if (source.TryGet(out NetworkObject infected))
                StationTrial.Local?.TryDrag(infected.GetComponent<StationInfected>());
        }

        [Rpc(SendTo.Owner)]
        void ReleaseRpc(NetworkObjectReference source)
        {
            if (source.TryGet(out NetworkObject infected))
                StationTrial.Local?.ReleaseFrom(infected.GetComponent<StationInfected>());
        }

        void Update()
        {
            if (!IsSpawned)
                return;
            if (IsOwner && StationTrial.Local != null)
            {
                var body = StationTrial.Local.transform;
                transform.SetPositionAndRotation(body.position, body.rotation);
                return;
            }
            // Remote view: lie flat when down, otherwise run or idle from how far the body moved.
            rig.transform.localRotation = Quaternion.Euler(Down ? -80f : 0f, 0f, 0f) * restRotation;
            float speed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = transform.position;
            // Network updates arrive in steps; smooth so the gait doesn't flicker between run and idle.
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            string clip = smoothedSpeed > (rig.IsPlaying("run") ? 0.4f : 0.8f) && !Down ? "run" : "idle";
            if (!rig.IsPlaying(clip))
                rig.CrossFade(clip, 0.2f);
        }

        /// <summary>The run clip carries the hips 2.4 m per loop; keep them over the networked body.</summary>
        void LateUpdate()
        {
            if (!IsSpawned || IsOwner || hips == null || Down)
                return;
            rig.transform.localPosition = restRigPosition;
            Vector3 drift = Vector3.ProjectOnPlane(hips.position - transform.position, Vector3.up);
            rig.transform.position -= drift;
        }
    }
}
