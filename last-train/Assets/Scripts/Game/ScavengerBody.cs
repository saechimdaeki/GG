using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    /// <summary>Transfers the existing pose, including procedural swings, to the Humanoid skin.</summary>
    sealed class ScavengerBody
    {
        readonly List<(Transform source, Transform target, Quaternion correction, bool leg)> _joints = new();
        static readonly string[] MotionNames = { "unarmed_idle", "unarmed_walk_forward", "unarmed_run_forward", "crouch_idle", "crouched_walking" };
        HumanoidMotion _motion;
        int _gait;
        float _upperAction, _lowerAction, _poseDelta;
        Transform _model, _hips, _spine, _head;
        Leg _leftLeg, _rightLeg;

        sealed class Leg
        {
            public readonly Transform thigh, shin, foot;
            public readonly float upper, lower, floor, side;
            public readonly Quaternion footRest;

            public Leg(Animator animator, Transform body, bool right)
            {
                thigh = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg);
                shin = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg);
                foot = animator.GetBoneTransform(right ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
                upper = Vector3.Distance(thigh.position, shin.position);
                lower = Vector3.Distance(shin.position, foot.position);
                floor = Mathf.Clamp(foot.position.y - body.position.y, 0.055f, 0.13f);
                side = right ? 1f : -1f;
                footRest = Quaternion.Inverse(body.rotation) * foot.rotation;
            }
        }
        Vector3 _modelOffset;
        Quaternion _modelRotation;
        Transform _body, _leftArm, _leftElbow, _leftHand, _rightArm, _rightElbow;
        public bool HasLocomotion => _motion != null;
        public Transform RightHand { get; private set; }


        public static ScavengerBody Attach(GameObject player, GameObject sourceRig)
        {
            var prefab = Resources.Load<GameObject>("Art/Scavenger/FBX/Scavenger");
            if (!prefab) return null;
            var model = Object.Instantiate(prefab, player.transform, false);
            var animator = model.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.isHuman)
            {
                Object.Destroy(model);
                return null;
            }

            var result = new ScavengerBody { _body = player.transform };
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0)
            {
                Object.Destroy(model);
                return null;
            }
            var bounds = skins[0].bounds;
            foreach (var skin in skins) bounds.Encapsulate(skin.bounds);
            if (bounds.size.y < 0.01f)
            {
                Object.Destroy(model);
                return null;
            }
            model.transform.localScale *= 1.76f / bounds.size.y;
            bounds = skins[0].bounds;
            foreach (var skin in skins) bounds.Encapsulate(skin.bounds);
            model.transform.position += Vector3.up * (player.transform.position.y - bounds.min.y);

            var source = player.GetComponentsInChildren<Transform>().ToDictionarySafe();
            var rest = sourceRig.GetComponentsInChildren<Transform>().ToDictionarySafe();
            var inverseRoot = Quaternion.Inverse(player.transform.rotation);
            if (!rest.TryGetValue("UpperArm.L", out var restLeft) ||
                !rest.TryGetValue("UpperArm.R", out var restRight))
            {
                Object.Destroy(model);
                return null;
            }
            var restForward = Vector3.Cross(restRight.position - restLeft.position, Vector3.up);
            var restBasis = Quaternion.Inverse(Quaternion.LookRotation(restForward));

            void Bind(string name, HumanBodyBones bone)
            {
                var target = animator.GetBoneTransform(bone);
                if (!target || !source.TryGetValue(name, out var from) || !rest.TryGetValue(name, out var bind)) return;
                var correction = Quaternion.Inverse(restBasis * bind.rotation) * inverseRoot * target.rotation;
                result._joints.Add((from, target, correction, name.Contains("Leg") || name.StartsWith("Foot") || name == "Hips"));
                if (bone == HumanBodyBones.RightHand) result.RightHand = target;
            }

            Bind("Hips", HumanBodyBones.Hips);
            Bind("Torso", HumanBodyBones.Spine);
            Bind("Head", HumanBodyBones.Head);
            Bind("UpperArm.L", HumanBodyBones.LeftUpperArm);
            Bind("LowerArm.L", HumanBodyBones.LeftLowerArm);
            Bind("Fist.L", HumanBodyBones.LeftHand);
            Bind("UpperArm.R", HumanBodyBones.RightUpperArm);
            Bind("LowerArm.R", HumanBodyBones.RightLowerArm);
            Bind("Fist.R", HumanBodyBones.RightHand);
            Bind("UpperLeg.L", HumanBodyBones.LeftUpperLeg);
            Bind("LowerLeg.L", HumanBodyBones.LeftLowerLeg);
            Bind("Foot.L", HumanBodyBones.LeftFoot);
            Bind("UpperLeg.R", HumanBodyBones.RightUpperLeg);
            Bind("LowerLeg.R", HumanBodyBones.RightLowerLeg);
            Bind("Foot.R", HumanBodyBones.RightFoot);
            if (!result.RightHand || result._joints.Count < 15)
            {
                Object.Destroy(model);
                return null;
            }

            var clips = System.Array.ConvertAll(MotionNames, name =>
                System.Array.Find(Resources.LoadAll<AnimationClip>("Art/Locomotion/" + name), clip => clip.name == name));
            if (System.Array.TrueForAll(clips, clip => clip && clip.humanMotion))
            {
                result._leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                result._leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                result._leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                result._rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                result._rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                result._leftLeg = new Leg(animator, player.transform, false);
                result._rightLeg = new Leg(animator, player.transform, true);
                result._motion = new HumanoidMotion(animator, clips, manual: true);
                result._motion.Blend(0, 1f, 0f, 14f);
                Debug.Log("[motion] Free Scavenger humanoid locomotion active");
            }
            else animator.enabled = false;
            result._model = model.transform;
            result._hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            result._spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            result._head = animator.GetBoneTransform(HumanBodyBones.Head);
            result._modelOffset = model.transform.localPosition;
            result._modelRotation = model.transform.localRotation;
            model.name = "Free Scavenger";
            return result;
        }

        public void SetMotion(float speed, bool crouch, bool action, bool fullBody, float dt)
        {
            if (_motion == null) return;
            _poseDelta = dt;
            bool moving = speed > (_gait is 1 or 2 or 4 ? 0.12f : 0.25f);
            int gait = crouch ? (moving ? 4 : 3) : !moving ? 0 : speed > (_gait == 2 ? 3.3f : 3.8f) ? 2 : 1;
            float cadence = gait switch { 1 => speed / 2.4f, 2 => speed / 4.8f, 4 => speed / 2.0f, _ => 1f };
            _motion.Blend(gait, Mathf.Clamp(cadence, 0.45f, 1.55f), dt, 14f, _gait is 1 or 2 or 4 && gait is 1 or 2 or 4);
            _gait = gait;
            _upperAction = Mathf.MoveTowards(_upperAction, action ? 1f : 0f, dt * (action ? 20f : 9f));
            _lowerAction = Mathf.MoveTowards(_lowerAction, fullBody ? 1f : 0f, dt * 12f);
        }

        public void UpdatePose()
        {
            _motion?.Evaluate(_poseDelta);
            // Animation may move the imported root independently of the gameplay body.
            _model.SetLocalPositionAndRotation(_modelOffset, _modelRotation);
            // The imported cycle can also translate its hips below the locked model root.
            // Preserve the vertical motion and keep horizontal travel on the gameplay body.
            var rootPosition = _body.position;
            _hips.position = new Vector3(rootPosition.x, _hips.position.y, rootPosition.z);
            if (Trace && _motion != null)
            {
                var raw = _head.position - _hips.position;
                var toe = _leftLeg.foot.GetChild(0).position - _leftLeg.foot.position;
                Debug.Log($"[raw] lean {Mathf.Atan2(Vector3.Dot(raw, _body.forward), raw.y) * Mathf.Rad2Deg:F0} toeFwd {Vector3.Dot(toe.normalized, _body.forward):F2} modelFwd {Vector3.Dot(_model.forward, _body.forward):F2} hipsFwd {Vector3.Dot(_hips.forward, _body.forward):F2}");
            }
            if (_motion != null)
            {
                var torso = _head.position - _spine.position;
                float maxLean = _gait is 3 or 4 ? 28f : _gait == 2 ? 14f : 8f;
                var upright = Vector3.RotateTowards(Vector3.up, torso.normalized, maxLean * Mathf.Deg2Rad, 0f);
                _spine.rotation = Quaternion.Slerp(Quaternion.identity,
                    Quaternion.FromToRotation(torso, upright), 1f - _upperAction) * _spine.rotation;
                float swing = Mathf.Sin(_motion.Phase * Mathf.PI * 2f) * (_gait == 2 ? 0.40f : _gait is 1 or 4 ? 0.22f : 0f);
                RelaxArm(_leftArm, _leftElbow, _leftHand, -1f, swing);
                RelaxArm(_rightArm, _rightElbow, RightHand, 1f, -swing);
            }
            foreach (var joint in _joints)
            {
                float weight = _motion == null ? 1f : joint.leg ? _lowerAction : _upperAction;
                if (weight > 0f)
                    joint.target.rotation = Quaternion.Slerp(joint.target.rotation, joint.source.rotation * joint.correction, weight);
            }

            if (_motion != null && _lowerAction < 0.99f)
            {
                GroundLeg(_leftLeg, 1f - _lowerAction);
                GroundLeg(_rightLeg, 1f - _lowerAction);
            }
            if (Trace && _motion != null) TracePose();
        }

        void GroundLeg(Leg leg, float weight)
        {
            var local = _body.InverseTransformPoint(leg.foot.position);
            float reach = _gait == 2 ? 0.34f : _gait is 1 or 4 ? 0.25f : 0.10f;
            float lift = _gait == 2 ? 0.15f : _gait is 1 or 4 ? 0.10f : 0.015f;
            var foot = _body.TransformPoint(new Vector3(
                leg.side * Mathf.Clamp(leg.side * local.x, 0.075f, 0.18f),
                Mathf.Clamp(local.y, leg.floor, leg.floor + lift),
                Mathf.Clamp(local.z, -reach, reach)));
            var hip = leg.thigh.position;
            var axis = (foot - hip).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(hip, foot), Mathf.Abs(leg.upper - leg.lower) + 0.01f, leg.upper + leg.lower - 0.015f);
            float along = (leg.upper * leg.upper + distance * distance - leg.lower * leg.lower) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, leg.upper * leg.upper - along * along));
            var kneeDirection = Vector3.ProjectOnPlane(_body.forward, axis).normalized;
            var knee = hip + axis * along + kneeDirection * bend;
            foot = hip + axis * distance;
            leg.thigh.rotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(leg.shin.position - hip, knee - hip), weight) * leg.thigh.rotation;
            leg.shin.rotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(leg.foot.position - leg.shin.position, foot - leg.shin.position), weight) * leg.shin.rotation;
            leg.foot.rotation = Quaternion.Slerp(leg.foot.rotation, _body.rotation * leg.footRest, weight * 0.7f);
        }

        void RelaxArm(Transform shoulder, Transform elbow, Transform hand, float side, float swing)
        {
            // The imported unarmed clips use a high combat guard. Use relaxed arm carriage for travel.
            if (!shoulder || !elbow || !hand) return;
            float bend = _gait == 2 ? 0.65f : _gait is 3 or 4 ? 0.40f : 0.22f;
            var upper = _body.TransformDirection(new Vector3(side * 0.16f, -1f, swing));
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, upper) * shoulder.rotation;
            var lower = _body.TransformDirection(new Vector3(side * 0.08f, -1f, swing + bend));
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, lower) * elbow.rotation;
        }

        public static bool Trace;
        void TracePose()
        {
            var up = _head.position - _hips.position;
            float lean = Mathf.Atan2(Vector3.Dot(up, _body.forward), Vector3.Dot(up, Vector3.up)) * Mathf.Rad2Deg;
            var l = _body.InverseTransformPoint(_leftLeg.foot.position);
            var r = _body.InverseTransformPoint(_rightLeg.foot.position);
            var h = _body.InverseTransformPoint(_hips.position);
            Debug.Log($"[pose] gait {_gait} lean {lean:F0} hips {h.y:F2}/{h.z:F2} footL {l.y:F2}/{l.z:F2} footR {r.y:F2}/{r.z:F2} up {_upperAction:F1} low {_lowerAction:F1}");
        }

        public Vector3 HipsPosition => _hips.position;

        public void Dispose() => _motion?.Dispose();
    }
}
