using UnityEngine;

namespace ResidualSignal
{
    public sealed class StationWitness : MonoBehaviour
    {
        [SerializeField] Transform leftLeg;
        [SerializeField] Transform rightLeg;
        [SerializeField] float walkSpeed = 3.2f;
        /// <summary>Legacy rig holding "idle" and "walk" clips; when set it replaces the leg-swing fallback.</summary>
        [SerializeField] Animation rig;
        Vector3 destination;
        StationTrial rescuer;
        float stride;
        float nextCoverCheck;
        bool takingCover;

        Vector3 restRigPosition;
        Transform hips;

        public bool HasArrived { get; private set; }

        void Awake()
        {
            if (rig == null)
                return;
            restRigPosition = rig.transform.localPosition;
            hips = rig.GetComponentInChildren<SkinnedMeshRenderer>()?.rootBone;
        }

        /// <summary>Keep the walk clip's hips over her position so she doesn't slide ahead and snap back.</summary>
        void LateUpdate()
        {
            if (hips == null)
                return;
            rig.transform.localPosition = restRigPosition;
            rig.transform.position -= Vector3.ProjectOnPlane(hips.position - transform.position, Vector3.up);
        }
        public bool Walking { get; private set; }

        /// <summary>Network clients: play what the host says she is doing.</summary>
        public void Mirror(bool walking) => PoseLegs(walking ? 1 : 0);

        public void EvacuateWith(StationTrial player, Transform exit) => EvacuateWith(player, exit.position);

        public void EvacuateWith(StationTrial player, Vector3 exit)
        {
            HasArrived = false;
            rescuer = player;
            destination = exit;
            nextCoverCheck = 0f;
            takingCover = false;
        }

        void Update()
        {
            if (rescuer == null || HasArrived)
            {
                PoseLegs(0);
                return;
            }
            // She follows whoever of the squad is closest, not only the player who started the escort.
            var escort = SquadMember.Nearest(transform.position);
            float escortDistance = escort != null ? Vector3.Distance(escort.body.position, transform.position) : Mathf.Infinity;
            bool freightEscape = destination.z > 90f;
            if (escortDistance > (freightEscape ? 8f : 5f))
            {
                PoseLegs(0);
                return;
            }
            if (Time.time >= nextCoverCheck)
            {
                nextCoverCheck = Time.time + 0.18f;
                takingCover = rescuer.EscortPathThreatened(transform.position);
            }
            if (takingCover)
            {
                PoseLegs(0);
                return;
            }
            Vector3 nextPoint = destination;
            nextPoint.y = transform.position.y;
            Vector3 direction = nextPoint - transform.position;
            if (direction.sqrMagnitude < 0.035f)
            {
                HasArrived = true;
                PoseLegs(0);
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                340f * Time.deltaTime);
            float speed = freightEscape && escortDistance > 2.5f ? Mathf.Max(walkSpeed, 4.65f) : walkSpeed;
            transform.position = Vector3.MoveTowards(transform.position, nextPoint, speed * Time.deltaTime);
            stride += Time.deltaTime * 11f;
            PoseLegs(Mathf.Sin(stride) * 25f);
        }
        void PoseLegs(float angle)
        {
            Walking = angle != 0;
            if (rig != null)
            {
                string clip = angle == 0 ? "idle" : "walk";
                if (!rig.IsPlaying(clip))
                    rig.CrossFade(clip, 0.25f);
                return;
            }
            leftLeg.localRotation = Quaternion.Euler(angle, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-angle, 0, 0);
        }
    }
}
