using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ResidualSignal
{
    public sealed partial class StationInfected
    {
        const float ThrowTell = 1.25f;
        const float DebrisRadius = 0.38f;
        const float ImpactRadius = 1.8f;

        float throwWindup;
        float nextThrow;
        float bossRecovery;
        float throwStarted;
        Vector3 lockedThrowTarget;
        GameObject heldDebris;
        GameObject flyingDebris;
        static Mesh debrisMesh;
        [SerializeField] Material debrisMaterial;
        static Material fallbackDebrisMaterial;
        readonly Transform[] throwBones = new Transform[6];
        readonly Quaternion[] throwArmPose = new Quaternion[6];
        bool posedThrowArms;

        bool UpdateBossThrow(Vector3 towards, float distance, float delta)
        {
            if (throwWindup > 0f)
            {
                throwWindup -= delta;
                // Commit the aim early enough for the squad to sidestep after the lift.
                if (throwWindup > 0.55f && target.Available)
                    lockedThrowTarget = target.body.position + Vector3.up * 0.85f;
                Vector3 facing = Vector3.ProjectOnPlane(lockedThrowTarget - transform.position, Vector3.up);
                if (facing.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), delta * 12f);
                UpdateHeldDebris();
                if (throwWindup <= 0f)
                {
                    Vector3 origin = heldDebris.transform.position;
                    float duration = Mathf.Clamp(Vector3.Distance(origin, lockedThrowTarget) / 15f, 0.42f, 1.3f);
                    LaunchBossDebris(origin, lockedThrowTarget, duration);
                    if (IsSpawned)
                        BossThrowRpc(origin, lockedThrowTarget, duration);
                    nextThrow = Time.time + 6.5f;
                    specialCooldown = Mathf.Max(specialCooldown, 1.35f);
                    bossRecovery = 0.8f;
                }
                return true;
            }

            if (bossRecovery > 0f)
            {
                bossRecovery -= delta;
                Locomotion(0f);
                return true;
            }

            if (chargeWindup > 0f || leaping > 0f || windup > 0f || Time.time < nextThrow ||
                distance < 5.5f || distance > 21f || !ClearAttackLine(1.5f, out _))
                return false;

            lockedThrowTarget = target.body.position + Vector3.up * 0.85f;
            transform.rotation = Quaternion.LookRotation(towards);
            throwWindup = ThrowTell;
            BeginBossThrow();
            if (IsSpawned)
                BossThrowTellRpc();
            target.warn(transform.position);
            return true;
        }

        void PlayBossSlam()
        {
            if (bossSlamClip == null)
                return;
            rig["slam"].speed = 1f;
            rig.Play("slam");
        }

        [Rpc(SendTo.NotServer)]
        void BossChargeCueRpc()
        {
            PlayBossSlam();
            PlayBossSound(0.55f);
        }

        [Rpc(SendTo.NotServer)]
        void BossThrowTellRpc() => BeginBossThrow();

        [Rpc(SendTo.NotServer)]
        void BossThrowRpc(Vector3 origin, Vector3 destination, float duration) =>
            LaunchBossDebris(origin, destination, duration);

        [Rpc(SendTo.NotServer)]
        void BossImpactRpc(Vector3 center) => BossImpactVisual(center);

        void BeginBossThrow()
        {
            CancelBossThrow();
            throwStarted = Time.time;
            heldDebris = MakeBossDebris();
            if (idleClip != null)
                rig.CrossFade("idle", 0.1f);
            else
                rig.CrossFade("walk", 0.1f);
            PlayBossSound(0.68f);
            UpdateHeldDebris();
        }

        void PlayBossSound(float pitch)
        {
            if (warningAudio == null || attackCue == null)
                return;
            warningAudio.pitch = pitch;
            warningAudio.PlayOneShot(attackCue, 1f);
        }

        void UpdateHeldDebris()
        {
            if (heldDebris == null)
                return;
            float lift = Mathf.SmoothStep(0f, 1f, (Time.time - throwStarted) / (ThrowTell * 0.72f));
            heldDebris.transform.SetPositionAndRotation(
                transform.position + transform.forward * Mathf.Lerp(1.02f, 0.93f, lift) +
                Vector3.up * Mathf.Lerp(1.06f, 2.65f, lift),
                transform.rotation * Quaternion.Euler(24f, 18f, lift * 45f));
        }

        void FindBossThrowBone(Transform bone)
        {
            int index = bone.name switch
            {
                "mixamorigLeftArm" => 0,
                "mixamorigLeftForeArm" => 1,
                "mixamorigLeftHand" => 2,
                "mixamorigRightArm" => 3,
                "mixamorigRightForeArm" => 4,
                "mixamorigRightHand" => 5,
                _ => -1
            };
            if (index >= 0)
                throwBones[index] = bone;
        }

        void RestoreBossArms()
        {
            if (!posedThrowArms)
                return;
            for (int i = 0; i < throwBones.Length; i++)
                if (throwBones[i] != null)
                    throwBones[i].localRotation = throwArmPose[i];
            posedThrowArms = false;
        }

        void PoseBossThrowArms()
        {
            if (heldDebris == null)
                return;
            UpdateHeldDebris();
            for (int i = 0; i < throwBones.Length; i++)
                if (throwBones[i] != null)
                    throwArmPose[i] = throwBones[i].localRotation;
            posedThrowArms = true;
            float grip = Mathf.SmoothStep(0f, 1f, (Time.time - throwStarted) / 0.22f);
            PoseBossArm(0, -transform.right, grip);
            PoseBossArm(3, transform.right, grip);
        }

        void PoseBossArm(int index, Vector3 outside, float weight)
        {
            Transform arm = throwBones[index];
            Transform forearm = throwBones[index + 1];
            Transform hand = throwBones[index + 2];
            if (arm == null || forearm == null || hand == null)
                return;

            Vector3 handTarget = heldDebris.transform.position + outside * 0.42f - transform.forward * 0.1f;
            Vector3 reachVector = handTarget - arm.position;
            float upperLength = Vector3.Distance(arm.position, forearm.position);
            float lowerLength = Vector3.Distance(forearm.position, hand.position);
            if (upperLength < 0.01f || lowerLength < 0.01f || reachVector.sqrMagnitude < 0.001f)
                return;
            float reachDistance = Mathf.Clamp(reachVector.magnitude,
                Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            Vector3 aim = reachVector.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(outside + Vector3.down * 0.55f, aim).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + reachDistance * reachDistance)
                / (2f * reachDistance);
            Vector3 elbow = aim * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            arm.rotation = Quaternion.Slerp(arm.rotation,
                Quaternion.FromToRotation(forearm.position - arm.position, elbow) * arm.rotation, weight);
            forearm.rotation = Quaternion.Slerp(forearm.rotation,
                Quaternion.FromToRotation(hand.position - forearm.position, handTarget - forearm.position) * forearm.rotation,
                weight);
        }

        void CancelBossThrow()
        {
            if (heldDebris != null)
                Destroy(heldDebris);
            heldDebris = null;
        }

        void ClearBossDebris()
        {
            CancelBossThrow();
            if (flyingDebris != null)
                Destroy(flyingDebris);
            flyingDebris = null;
        }

        void LaunchBossDebris(Vector3 origin, Vector3 destination, float duration)
        {
            var debris = heldDebris != null ? heldDebris : MakeBossDebris();
            heldDebris = null;
            flyingDebris = debris;
            debris.transform.position = origin;
            if (attackClip != null)
                rig.CrossFade("attack", 0.08f);
            PlayBossSound(0.83f);
            StartCoroutine(FlyBossDebris(debris, origin, destination, duration));
        }

        IEnumerator FlyBossDebris(GameObject debris, Vector3 origin, Vector3 destination, float duration)
        {
            Vector3 previous = origin;
            for (float elapsed = 0f; debris != null && elapsed < duration;)
            {
                yield return null;
                if (debris == null)
                    yield break;
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                float progress = elapsed / duration;
                Vector3 next = Vector3.Lerp(origin, destination, progress) +
                    Vector3.up * (4f * progress * (1f - progress) * (0.85f + 0.5f * duration));
                Vector3 step = next - previous;
                if (Authority && BossObstruction(previous, DebrisRadius, step, out RaycastHit hit))
                {
                    previous += step.normalized * hit.distance;
                    break;
                }
                debris.transform.position = next;
                debris.transform.Rotate(220f * Time.deltaTime, 90f * Time.deltaTime, 130f * Time.deltaTime);
                previous = next;
            }

            if (!Authority)
                yield break; // The host sends the actual impact point, including early hits on cover.

            foreach (var member in SquadMember.All)
            {
                if (!member.Available)
                    continue;
                Vector3 toMember = member.body.position + Vector3.up * 0.9f - previous;
                float distance = toMember.magnitude;
                if (distance > ImpactRadius ||
                    (BossObstruction(previous, 0f, toMember, out RaycastHit cover) &&
                     cover.transform.root != member.body.root))
                    continue;
                Vector3 push = Vector3.ProjectOnPlane(toMember, Vector3.up);
                if (push.sqrMagnitude < 0.01f)
                    push = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
                member.knock(push.normalized * 6f, Mathf.RoundToInt(Mathf.Lerp(42f, 14f, distance / ImpactRadius)));
            }
            BossImpactVisual(previous);
            if (IsSpawned)
                BossImpactRpc(previous);
        }

        bool BossObstruction(Vector3 origin, float radius, Vector3 step, out RaycastHit closest)
        {
            float length = step.magnitude;
            int count = radius > 0f
                ? Physics.SphereCastNonAlloc(origin, radius, step.normalized, sightHits, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(origin, step.normalized, sightHits, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            closest = default;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.distance >= distance || hit.collider.GetComponentInParent<StationInfected>() != null)
                    continue;
                closest = hit;
                distance = hit.distance;
            }
            return distance < float.PositiveInfinity;
        }

        void BossImpactVisual(Vector3 center)
        {
            if (flyingDebris != null)
                Destroy(flyingDebris);
            flyingDebris = null;
            if (attackCue != null)
                AudioSource.PlayClipAtPoint(attackCue, center, 0.85f);
            // The smaller fragments are cosmetic and never become blockers.
            for (int i = 0; i < 7; i++)
            {
                var shard = MakeBossDebris();
                shard.name = "Concrete fragment";
                shard.transform.position = center;
                shard.transform.localScale *= Random.Range(0.14f, 0.36f);
                var motion = shard.AddComponent<Rigidbody>();
                motion.linearVelocity = Random.onUnitSphere * 3f + Vector3.up * 2f;
                motion.angularVelocity = Random.insideUnitSphere * 9f;
                Destroy(shard, 0.55f);
            }
        }

        GameObject MakeBossDebris()
        {
            if (debrisMesh == null)
            {
                // Rough, flat faces keep the silhouette distinct from a smooth projectile.
                debrisMesh = new Mesh { name = "Broken concrete" };
                var corners = new[]
                {
                    new Vector3(-0.46f, -0.19f, -0.27f), new Vector3(0.24f, -0.27f, -0.34f),
                    new Vector3(0.48f, -0.09f, 0.17f), new Vector3(-0.23f, -0.22f, 0.35f),
                    new Vector3(-0.31f, 0.27f, -0.19f), new Vector3(0.32f, 0.18f, -0.21f),
                    new Vector3(0.24f, 0.30f, 0.28f), new Vector3(-0.38f, 0.09f, 0.25f)
                };
                int[] faces = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1,
                    1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
                var vertices = new Vector3[faces.Length];
                var uv = new Vector2[faces.Length];
                for (int i = 0; i < faces.Length; i++)
                {
                    vertices[i] = corners[faces[i]] * 1.3f;
                    faces[i] = i;
                }
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    Vector3 origin = vertices[i];
                    Vector3 u = (vertices[i + 1] - origin).normalized;
                    Vector3 normal = Vector3.Cross(u, vertices[i + 2] - origin).normalized;
                    Vector3 v = Vector3.Cross(normal, u);
                    uv[i] = new Vector2(0.22f, 0.1f);
                    for (int j = 1; j < 3; j++)
                    {
                        Vector3 offset = vertices[i + j] - origin;
                        uv[i + j] = uv[i] + new Vector2(Vector3.Dot(u, offset), Vector3.Dot(v, offset)) * 0.78f;
                    }
                }
                debrisMesh.vertices = vertices;
                debrisMesh.uv = uv;
                debrisMesh.triangles = faces;
                debrisMesh.RecalculateNormals();
                debrisMesh.RecalculateTangents();
                debrisMesh.RecalculateBounds();
            }
            if (debrisMaterial == null && fallbackDebrisMaterial == null)
            {
                fallbackDebrisMaterial = new Material(Shader.Find("HDRP/Lit"));
                fallbackDebrisMaterial.SetColor("_BaseColor", new Color(0.37f, 0.34f, 0.28f));
                fallbackDebrisMaterial.SetFloat("_Smoothness", 0.12f);
            }
            var chunk = new GameObject("Concrete chunk");
            chunk.AddComponent<MeshFilter>().sharedMesh = debrisMesh;
            chunk.AddComponent<MeshRenderer>().sharedMaterial = debrisMaterial != null ? debrisMaterial : fallbackDebrisMaterial;
            return chunk;
        }
    }
}
