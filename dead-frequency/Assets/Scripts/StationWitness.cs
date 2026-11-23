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

        public bool HasArrived { get; private set; }
        public bool Walking { get; private set; }

        /// <summary>Network clients: play what the host says she is doing.</summary>
        public void Mirror(bool walking) => PoseLegs(walking ? 1 : 0);

        public void EvacuateWith(StationTrial player, Transform exit) => EvacuateWith(player, exit.position);

        public void EvacuateWith(StationTrial player, Vector3 exit)
        {
            HasArrived = false;
            rescuer = player;
            destination = exit;
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
            if (escort == null || Vector3.Distance(escort.body.position, transform.position) > 5f)
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
            transform.position = Vector3.MoveTowards(transform.position, nextPoint, walkSpeed * Time.deltaTime);
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
