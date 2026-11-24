using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Physical feel of an infected body: per-bone hit flinch on top of the animation, blood spray at the
    /// wound, and a ragdoll that takes over on death (pushed along the killing shot).
    /// </summary>
    public sealed class InfectedBody : MonoBehaviour
    {
        const float FlinchTime = 0.47f;
        const int IgnoreRaycast = 2; // corpses must not block bullets

        Transform pelvis, chest, head;
        Transform[] limbs;
        Transform flinchBone;
        Vector3 flinchAxis;
        Vector3 hitDirection;
        Transform[] poseBones;
        Quaternion[] animatedPose;
        bool poseApplied;
        float flinchAngle;
        float flinchUntil;
        static ParticleSystem blood;
        Transform leftArm, rightArm;
        float swingStart, swingLength, swingSide = 1f;
        float frenzy;
        float frenzyPhase;
        InfectedKind posture;
        float poseOffset;
        float poseTension;

        public void SetPoseTension(float tension) => poseTension = tension;

        public void SetPosture(InfectedKind kind) => posture = kind;

        /// <summary>0..1: how feral the sprint looks (hunched, head low, arms reaching and flailing).</summary>
        public void SetFrenzy(float target) => frenzy = Mathf.MoveTowards(frenzy, target, Time.deltaTime * 4f);

        public bool Ragdolled { get; private set; }
        public bool IsFlinching => !Ragdolled && flinchBone != null && Time.time < flinchUntil;
        public float RenderedHitAngle { get; private set; }
        float HitWeight
        {
            get
            {
                if (!IsFlinching)
                    return 0f;
                float elapsed = FlinchTime - (flinchUntil - Time.time);
                return elapsed < 0.065f ? Mathf.SmoothStep(0.28f, 1f, elapsed / 0.065f) :
                    Mathf.SmoothStep(1f, 0f, (elapsed - 0.065f) / (FlinchTime - 0.065f));
            }
        }
        public Vector3 HitOffset => (hitDirection * 0.16f + Vector3.down * 0.075f) * HitWeight;

        void Awake()
        {
            pelvis = Find("Bip01 Pelvis");
            chest = Find("Bip01 Spine2");
            head = Find("Bip01 Head");
            leftArm = Find("Bip01 L UpperArm");
            rightArm = Find("Bip01 R UpperArm");
            limbs = new[]
            {
                Find("Bip01 L UpperArm"), Find("Bip01 L Forearm"), Find("Bip01 R UpperArm"), Find("Bip01 R Forearm"),
                Find("Bip01 L Thigh"), Find("Bip01 L Calf"), Find("Bip01 R Thigh"), Find("Bip01 R Calf"),
            };
            poseOffset = Mathf.Repeat(transform.position.x * 2.71f + transform.position.z * 0.79f, 6.28f);
            poseBones = new[] { pelvis, chest, head, leftArm, rightArm, limbs[4], limbs[6], limbs[1], limbs[3] };
            animatedPose = new Quaternion[poseBones.Length];
        }

        public void RestoreAnimatedPose()
        {
            if (!poseApplied || Ragdolled)
                return;
            for (int i = 0; i < poseBones.Length; i++)
                if (poseBones[i] != null)
                    poseBones[i].localRotation = animatedPose[i];
            poseApplied = false;
        }

        Transform Find(string bone)
        {
            string mixamo = bone switch
            {
                "Bip01 Pelvis" => "mixamorigHips",
                "Bip01 Spine2" => "mixamorigSpine2",
                "Bip01 Head" => "mixamorigHead",
                "Bip01 L UpperArm" => "mixamorigLeftArm",
                "Bip01 R UpperArm" => "mixamorigRightArm",
                "Bip01 L Forearm" => "mixamorigLeftForeArm",
                "Bip01 R Forearm" => "mixamorigRightForeArm",
                "Bip01 L Thigh" => "mixamorigLeftUpLeg",
                "Bip01 R Thigh" => "mixamorigRightUpLeg",
                "Bip01 L Calf" => "mixamorigLeftLeg",
                "Bip01 R Calf" => "mixamorigRightLeg",
                _ => bone
            };
            foreach (var node in GetComponentsInChildren<Transform>())
                if (node.name == bone || node.name.Replace(":", "") == mixamo)
                    return node;
            return null;
        }

        /// <summary>Snap the struck part of the body away from the shot; heavier hits bend further.</summary>
        public void React(Vector3 point, Vector3 direction, int damage)
        {
            Spray(point, direction, damage);
            if (Ragdolled)
            {
                var nearest = NearestBody(point);
                if (nearest != null)
                    nearest.AddForceAtPosition(direction.normalized * Mathf.Min(damage, 120) * 0.04f, point, ForceMode.Impulse);
                return;
            }
            Vector3 localHit = transform.InverseTransformPoint(point);
            bool left = localHit.x < 0;
            flinchBone = localHit.y >= 1.46f ? head :
                localHit.y < 0.86f ? limbs[left ? 4 : 6] :
                Mathf.Abs(localHit.x) >= 0.23f ? (left ? leftArm : rightArm) : chest;
            flinchBone ??= chest != null ? chest : pelvis;
            hitDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            flinchAxis = Vector3.Cross(Vector3.up, hitDirection).normalized;
            if (flinchAxis.sqrMagnitude < 0.01f)
                flinchAxis = transform.right;
            flinchAngle = Mathf.Clamp(19f + damage * 0.55f, 24f, 56f) * (flinchBone == head ? 1.12f : 1f);
            swingLength = 0f;
            flinchUntil = Time.time + FlinchTime;
        }

        /// <summary>Claw swing for rigs without an attack clip: both arms rise, then slam down over <paramref name="windup"/> seconds.</summary>
        public void Swing(float windup)
        {
            swingStart = Time.time;
            swingLength = windup + 0.25f;
            swingSide = -swingSide;
        }

        void ApplyFrenzy()
        {
            if (frenzy <= 0.01f || chest == null)
                return;
            frenzyPhase += Time.deltaTime * 11f;
            var side = transform.right;
            chest.rotation = Quaternion.AngleAxis(24f * frenzy, side) * chest.rotation;
            if (head != null)
                head.rotation = Quaternion.AngleAxis(-12f * frenzy, side) * head.rotation; // keep eyes on the prey
            if (leftArm == null || rightArm == null)
                return;
            float flail = Mathf.Sin(frenzyPhase) * 22f;
            leftArm.rotation = Quaternion.AngleAxis((-55f + flail) * frenzy, side) * leftArm.rotation;
            rightArm.rotation = Quaternion.AngleAxis((-55f - flail) * frenzy, side) * rightArm.rotation;
        }

        void ApplySwing()
        {
            float t = (Time.time - swingStart) / Mathf.Max(swingLength, 0.01f);
            if (swingLength <= 0 || t < 0 || t > 1 || leftArm == null || rightArm == null)
                return;
            // Raise forward to above the shoulders, hold, then strike down and recover.
            float raise = t < 0.6f ? Mathf.SmoothStep(0, 1, t / 0.6f) : 1f - Mathf.SmoothStep(0, 1, (t - 0.6f) / 0.4f);
            float angle = -105f * raise - (t > 0.6f ? 35f * Mathf.Sin((t - 0.6f) / 0.4f * Mathf.PI) : 0f);
            float leading = swingSide < 0f ? 1f : 0.55f;
            float following = swingSide > 0f ? 1f : 0.55f;
            if (chest != null)
                chest.rotation = Quaternion.AngleAxis(swingSide * raise * 17f, transform.up) *
                    Quaternion.AngleAxis(raise * 18f, transform.right) * chest.rotation;
            leftArm.rotation = Quaternion.AngleAxis(angle * leading, transform.right) * leftArm.rotation;
            rightArm.rotation = Quaternion.AngleAxis(angle * following, transform.right) * rightArm.rotation;
        }

        void ApplyCharacterPosture()
        {
            if (chest == null || posture == InfectedKind.Brute)
                return;
            float breath = Mathf.Sin(Time.time * 2.4f + poseOffset);
            (float lean, float twist, float reach, float tilt) = posture switch
            {
                InfectedKind.Leaper => (22f, -8f, 21f, -11f),
                InfectedKind.Dragger => (15f, 19f, 25f + poseTension * 18f, -7f),
                InfectedKind.Bloater => (12f, -6f, 12f, 5f),
                InfectedKind.Caller => (-5f - poseTension * 21f, 9f, 9f, 8f - poseTension * 12f),
                _ => (4f, 3f * Mathf.Cos(poseOffset), 6f, 3f * Mathf.Sin(poseOffset))
            };
            float locomotionWeight = 1f - frenzy * 0.65f;
            chest.rotation = Quaternion.AngleAxis((twist + breath * 1.4f) * locomotionWeight, transform.up) *
                Quaternion.AngleAxis((lean + breath * 2f) * locomotionWeight, transform.right) * chest.rotation;
            if (head != null)
                head.rotation = Quaternion.AngleAxis(tilt * locomotionWeight, transform.forward) *
                    Quaternion.AngleAxis(-lean * 0.42f * locomotionWeight, transform.right) * head.rotation;

            bool favorLeft = posture == InfectedKind.Dragger || Mathf.Sin(poseOffset) > 0f;
            BendArm(leftArm, limbs[1], -reach * (favorLeft ? 1f : 0.30f) * locomotionWeight);
            BendArm(rightArm, limbs[3], -reach * (favorLeft ? 0.30f : 1f) * locomotionWeight);
        }

        void BendArm(Transform upper, Transform lower, float reach)
        {
            if (upper != null)
                upper.rotation = Quaternion.AngleAxis(reach, transform.right) * upper.rotation;
            if (lower != null)
                lower.rotation = Quaternion.AngleAxis(reach * 0.42f, transform.right) * lower.rotation;
        }

        /// <summary>Runs after the animation pose (called from the infected's LateUpdate).</summary>
        public void ApplyFlinch()
        {
            RenderedHitAngle = 0f;
            if (Ragdolled)
                return;

            for (int i = 0; i < poseBones.Length; i++)
                if (poseBones[i] != null)
                    animatedPose[i] = poseBones[i].localRotation;
            poseApplied = true;

            if (!IsFlinching)
            {
                ApplyCharacterPosture();
                ApplyFrenzy();
                ApplySwing();
                return;
            }

            float weight = HitWeight;
            Quaternion before = flinchBone.rotation;
            flinchBone.rotation = Quaternion.AngleAxis(flinchAngle * weight, flinchAxis) * before;
            RenderedHitAngle = Quaternion.Angle(before, flinchBone.rotation);
            if (flinchBone != chest && chest != null)
                chest.rotation = Quaternion.AngleAxis(24f * weight, flinchAxis) * chest.rotation;
            if (head != null && flinchBone != head)
                head.rotation = Quaternion.AngleAxis(-13f * weight, transform.right) * head.rotation;
            if (leftArm != null && rightArm != null)
            {
                leftArm.rotation = Quaternion.AngleAxis(-29f * weight, transform.right) * leftArm.rotation;
                rightArm.rotation = Quaternion.AngleAxis(22f * weight, transform.right) * rightArm.rotation;
            }

        }

        /// <summary>Go limp and fall along the killing shot.</summary>
        public void Ragdoll(Vector3 point, Vector3 direction, int damage)
        {
            if (Ragdolled)
                return;
            Ragdolled = true;
            var animation = GetComponentInChildren<Animation>();
            if (animation != null)
                animation.enabled = false;
            float scale = transform.lossyScale.y;
            var pelvisBody = Part(pelvis, null, 0.16f * scale, 11f);
            var chestBody = Part(chest, pelvisBody, 0.17f * scale, 9f);
            Part(head, chestBody, 0.11f * scale, 4f);
            for (int index = 0; index < limbs.Length; index += 2)
            {
                bool arm = index < 4;
                var upper = Part(limbs[index], arm ? chestBody : pelvisBody, (arm ? 0.055f : 0.08f) * scale, arm ? 2f : 6f);
                Part(limbs[index + 1], upper, (arm ? 0.045f : 0.065f) * scale, arm ? 1.5f : 4f);
            }
            var hit = NearestBody(point) ?? pelvisBody;
            float push = Mathf.Clamp(damage, 30, 160) * 0.05f;
            hit.AddForceAtPosition(direction.normalized * push + Vector3.up * 0.6f, point, ForceMode.Impulse);
        }

        Rigidbody Part(Transform bone, Rigidbody parent, float radius, float mass)
        {
            if (bone == null)
                return parent;
            bone.gameObject.layer = IgnoreRaycast;
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.4f;
            body.angularDamping = 2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = radius / Mathf.Max(bone.lossyScale.x, 0.0001f);
            Transform child = bone.childCount > 0 ? bone.GetChild(0) : null;
            if (child != null)
            {
                Vector3 local = bone.InverseTransformPoint(child.position);
                collider.direction = Mathf.Abs(local.x) > Mathf.Abs(local.y) && Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0
                    : Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2;
                collider.center = local * 0.5f;
                collider.height = local.magnitude + collider.radius;
            }
            if (parent != null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.enableProjection = true;
                joint.lowTwistLimit = new SoftJointLimit { limit = -30f };
                joint.highTwistLimit = new SoftJointLimit { limit = 30f };
                joint.swing1Limit = new SoftJointLimit { limit = 40f };
                joint.swing2Limit = new SoftJointLimit { limit = 25f };
            }
            return body;
        }

        Rigidbody NearestBody(Vector3 point)
        {
            Rigidbody best = null;
            float bestDistance = float.MaxValue;
            foreach (var body in GetComponentsInChildren<Rigidbody>())
            {
                float distance = (body.worldCenterOfMass - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = body;
                }
            }
            return best;
        }

        static void Spray(Vector3 point, Vector3 direction, int damage)
        {
            if (blood == null)
                blood = CreateBlood();
            blood.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-direction.normalized + Random.insideUnitSphere * 0.3f));
            blood.Emit(Mathf.Clamp(damage / 3, 14, 40));
            blood.transform.rotation = Quaternion.LookRotation(direction.normalized);
            blood.Emit(Mathf.Clamp(damage / 6, 6, 20));
            ShotFeedback.BloodMist(point, direction, damage);
        }

        static Texture2D DropletMask()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - size * 0.5f) / (size * 0.42f);
                float v = (y + 0.5f - size * 0.5f) / (size * 0.46f);
                byte alpha = (byte)(255f * Mathf.Clamp01((1f - u * u - v * v) * 2.5f));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        static ParticleSystem CreateBlood()
        {
            var node = new GameObject("Blood spray");
            var system = node.AddComponent<ParticleSystem>();
            system.Stop();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.06f, 0.05f), new Color(1f, 0.25f, 0.22f));
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.02f;
            var material = new Material(Shader.Find("HDRP/Unlit"));
            // Unlit, so keep it dark: bright red reads as a glowing sticker in a dim station.
            material.SetColor("_UnlitColor", new Color(0.26f, 0.012f, 0.01f));
            material.SetTexture("_UnlitColorMap", DropletMask());
            material.SetFloat("_AlphaCutoffEnable", 1f);
            material.SetFloat("_AlphaCutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            node.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return system;
        }
    }
}
