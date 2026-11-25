using Unity.Netcode;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// The Left 4 Dead 2 specials: the spitter (침뱉개) lobs acid that pools and burns, the charger (돌진개) rams a
    /// survivor into the wall and pounds them, and the jockey (올라타개) leaps onto a survivor's shoulders and steers them.
    /// </summary>
    public sealed partial class StationInfected
    {
        const float SpitRangeMin = 5f;
        const float SpitRange = 17f;
        const float SpitCooldown = 9f;
        const float ChargeRange = 16f;
        const float ChargerSpeed = 11f;
        const float ChargeSeconds = 1.15f;
        const float JockeyLeapRange = 6.5f;
        const float JockeyRideSpeed = 2.6f;

        float pummelAt;
        float carryUntil;
        Vector3 steer;
        float nextSteer;
        Transform chargerArm;

        public string MutantDebug => $"{kind} at {transform.position} alerted {alerted} target {(target != null ? target.body.name : "none")} cd {specialCooldown:F1} hold {holdUntil - Time.time:F1} stagger {stagger:F1} staged {stagedEncounter} wait {waitInCoverUntil - Time.time:F1} clear {debugClear} block {debugBlock} distance {debugDistance:F1} reserve {mission.TryReserveSpecial(target)}";

        /// <summary>Where a dragged survivor is pulled: the dragger itself, or wherever the jockey steers.</summary>
        public Vector3 PullPoint => kind == InfectedKind.Jockey && holding && target != null && target.body != null
            ? target.body.position + steer * 2f
            : transform.position;

        /// <summary>A charger pinning someone, or a jockey riding them: holds that only teammates can break.</summary>
        public bool CarryingOff => holding && kind is InfectedKind.Charger or InfectedKind.Jockey;

        static (Vector3 scale, Color tint)? MutantLook(InfectedKind kind) => kind switch
        {
            InfectedKind.Spitter => (new Vector3(0.9f, 1.12f, 0.9f), new Color(0.72f, 0.86f, 0.55f)),
            InfectedKind.Charger => (new Vector3(1.18f, 1.08f, 1.18f), new Color(0.9f, 0.7f, 0.62f)),
            InfectedKind.Jockey => (new Vector3(0.74f, 0.7f, 0.74f), new Color(0.82f, 0.76f, 0.8f)),
            _ => null
        };

        static int MutantHealth(InfectedKind kind) => kind switch
        {
            InfectedKind.Spitter => 100,
            // Below Left 4 Dead's 600/325: this squad's guns hit softer, and the AI teammates must free you alone.
            InfectedKind.Charger => 420,
            InfectedKind.Jockey => 250,
            _ => 0
        };

        /// <summary>The charger's one swollen arm is its silhouette, as in the original.</summary>
        void GrowChargerArm()
        {
            if (kind != InfectedKind.Charger || chargerArm != null)
                return;
            foreach (var bone in rig.GetComponentsInChildren<Transform>())
                if (bone.name.EndsWith("RightArm"))
                {
                    chargerArm = bone;
                    bone.localScale = Vector3.one * 1.55f;
                    break;
                }
        }

        float MutantPace() => kind switch
        {
            InfectedKind.Spitter => speed * 1.3f,
            InfectedKind.Charger => speed * 1.15f,
            InfectedKind.Jockey => speed * 1.95f,
            _ => speed
        };

        bool UpdateMutant(Vector3 toTarget, float distance, bool clear, float delta) => kind switch
        {
            InfectedKind.Spitter => UpdateSpitter(toTarget, distance, clear),
            InfectedKind.Charger => UpdateCharger(toTarget, distance, clear, delta),
            InfectedKind.Jockey => UpdateJockey(toTarget, distance, clear, delta),
            _ => false
        };

        bool debugClear;
        string debugBlock = "";
        float debugDistance;

        bool UpdateSpitter(Vector3 toTarget, float distance, bool clear)
        {
            debugClear = clear;
            debugDistance = distance;
            if (Time.time < holdUntil)
                return true;
            if (clear && distance > SpitRangeMin && distance < SpitRange && specialCooldown <= 0 && mission.TryReserveSpecial(target))
            {
                specialCooldown = SpitCooldown * Difficulty.SpecialCooldown;
                // Aim where the survivor stands now: moving off the spot is the counter.
                Vector3 landing = target.body.position;
                Vector3 mouth = transform.position + Vector3.up * 1.55f + transform.forward * 0.3f;
                float flight = Mathf.Clamp(distance / 12f, 0.55f, 1.2f);
                AcidPool.Lob(mouth, landing, flight, mission, authoritative: true);
                if (IsSpawned)
                    SpitRpc(mouth, landing, flight);
                PlayAttack();
                holdUntil = Time.time + 0.8f;
                warningAudio.pitch = 1.8f;
                if (attackCue != null)
                    warningAudio.PlayOneShot(attackCue, 0.9f);
                transform.rotation = Quaternion.LookRotation(toTarget);
                return true;
            }
            // Keeps its distance once in range, like the spitter hanging back behind the horde.
            return clear && distance < SpitRange * 0.8f;
        }

        [Rpc(SendTo.NotServer)]
        void SpitRpc(Vector3 mouth, Vector3 landing, float flight) => AcidPool.Lob(mouth, landing, flight, mission, authoritative: false);

        bool UpdateCharger(Vector3 toTarget, float distance, bool clear, float delta)
        {
            if (holding)
            {
                if (Time.time < carryUntil)
                {
                    // Carry the victim along until the charge hits something.
                    CollisionFlags hit = MoveInCombat((lunge + Vector3.down * 3f) * delta);
                    Locomotion(ChargerSpeed);
                    if ((hit & CollisionFlags.Sides) != 0)
                        carryUntil = 0f;
                    return true;
                }
                // Pound them into the floor.
                Vector3 at = target.body.position - transform.forward * 0.8f;
                body.Move(Vector3.ProjectOnPlane(at - transform.position, Vector3.up) * Mathf.Min(1f, delta * 8f) + Vector3.down * 3f * delta);
                if (toTarget.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(toTarget);
                if (Time.time >= pummelAt)
                {
                    pummelAt = Time.time + 0.85f;
                    PlayAttack();
                }
                Locomotion(0f);
                return true;
            }
            if (chargeWindup > 0f)
            {
                chargeWindup -= delta;
                Locomotion(0f);
                if (chargeWindup <= 0f)
                    leaping = ChargeSeconds;
                return true;
            }
            if (leaping > 0f)
            {
                leaping -= delta;
                CollisionFlags collision = MoveInCombat((lunge + Vector3.down * 3f) * delta);
                Locomotion(ChargerSpeed);
                // Bowl over anyone else in the way.
                foreach (var member in SquadMember.All)
                {
                    if (member == target || !member.Available || member.held)
                        continue;
                    Vector3 offset = Vector3.ProjectOnPlane(member.body.position - transform.position, Vector3.up);
                    if (offset.magnitude < 1.1f)
                    {
                        Vector3 aside = Vector3.Cross(Vector3.up, lunge).normalized * Mathf.Sign(Vector3.Dot(offset, Vector3.Cross(Vector3.up, lunge)) + 0.001f);
                        member.knock(aside * 6.5f + lunge.normalized * 2f + Vector3.up * 3.6f, 10);
                    }
                }
                if (distance < 1.3f && Vector3.Dot(lunge.normalized, toTarget.normalized) > 0f && target.TryPin(this))
                {
                    holding = true;
                    leaping = 0f;
                    carryUntil = Time.time + 0.85f;
                    pummelAt = Time.time + 0.9f;
                    target.knock(lunge.normalized * ChargerSpeed + Vector3.up * 1.2f, 10);
                    return true;
                }
                // Missed or hit a wall: stagger, as the original does after a wasted charge.
                if ((collision & CollisionFlags.Sides) != 0 || Vector3.Dot(lunge, toTarget) < -1f || leaping <= 0f)
                {
                    leaping = 0f;
                    stagger = 1.1f;
                    holdUntil = Time.time + 1.1f;
                }
                return true;
            }
            if (clear && distance > 4.5f && distance < ChargeRange && specialCooldown <= 0 && mission.TryReserveSpecial(target))
            {
                specialCooldown = 8f * Difficulty.SpecialCooldown;
                chargeWindup = 0.65f;
                lunge = toTarget.normalized * ChargerSpeed;
                transform.rotation = Quaternion.LookRotation(toTarget);
                PlayAttack();
                warningAudio.pitch = 0.62f;
                if (attackCue != null)
                    warningAudio.PlayOneShot(attackCue, 1f);
                target.warn(transform.position);
                return true;
            }
            return false;
        }

        bool UpdateJockey(Vector3 toTarget, float distance, bool clear, float delta)
        {
            debugClear = clear;
            debugDistance = distance;
            if (holding)
            {
                // Ride on the shoulders, and yank them somewhere new every so often — away from the squad if possible.
                if (Time.time >= nextSteer)
                {
                    nextSteer = Time.time + Random.Range(0.9f, 1.6f);
                    Vector3 away = Vector3.zero;
                    foreach (var member in SquadMember.All)
                        if (member != target && member.body != null)
                            away += Vector3.ProjectOnPlane(target.body.position - member.body.position, Vector3.up);
                    Vector3 wander = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) *
                        (away.sqrMagnitude > 0.01f ? away.normalized : target.body.forward);
                    steer = wander.normalized;
                }
                // Behind the head, not on it: the victim's own eyes must not end up inside its body.
                transform.SetPositionAndRotation(target.body.position + Vector3.up * 0.95f - target.body.forward * 0.5f,
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(target.body.forward, Vector3.up)));
                if (Time.time >= pummelAt)
                {
                    pummelAt = Time.time + 0.6f;
                    PlayAttack();
                }
                Locomotion(0f);
                return true;
            }
            if (leaping > 0f)
            {
                leaping -= delta;
                body.Move((lunge + Vector3.down * 1.5f) * delta);
                if (distance < 1.2f && target.TryDrag(this))
                {
                    holding = true;
                    leaping = 0f;
                    body.enabled = false; // sits on them: their capsule must not push against ours
                    nextSteer = 0f;
                    pummelAt = Time.time + 0.3f;
                    if (warningAudio != null && attackCue != null)
                        warningAudio.PlayOneShot(attackCue, 1f);
                }
                return true;
            }
            // Leaps from range, or springs straight up onto someone already within reach.
            if (clear && distance < JockeyLeapRange && specialCooldown <= 0 && mission.TryReserveSpecial(target))
            {
                specialCooldown = 6f * Difficulty.SpecialCooldown;
                leaping = distance / leapSpeed + 0.2f;
                lunge = toTarget.normalized * leapSpeed + Vector3.up * 2.2f;
                warningAudio.pitch = 1.6f;
                if (attackCue != null)
                    warningAudio.PlayOneShot(attackCue, 1f);
                target.warn(transform.position);
                return true;
            }
            return false;
        }

        /// <summary>Hold ended (killed, shoved off or broken free): the jockey drops to the floor behind them.</summary>
        void LetGoOfRide()
        {
            if (kind != InfectedKind.Jockey || body.enabled)
                return;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                transform.position = floor.point + Vector3.up * 0.03f - transform.forward * 0.6f;
            body.enabled = true;
        }
    }
}
