using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatChaos
{
    /// <summary>
    /// The player cat: walks and runs relative to the camera, double-jumps, swats with a paw,
    /// sharpens claws on furniture and meows. Body is a rigidbody on the Ignore Raycast layer.
    /// </summary>
    public sealed class Cat : MonoBehaviour
    {
        const float BodyHeight = 0.42f;
        /// <summary>Yaw that turns the imported model to face +Z.</summary>
        public static float ModelYaw = 0f;

        public Rigidbody Body;
        public bool Controllable;
        public bool Scratching { get; private set; }
        public bool Grounded { get; private set; }

        // screenshot runs drive the cat without input
        public Vector3 AutoMove;
        public bool AutoPaw, AutoScratch;

        Tuning T;
        Animation _anim;
        Vector3 _move;
        bool _run, _scratchHeld;
        float _jumpBuffer, _pawCd, _pawAnim, _decalCd, _autoPawCd;
        int _airJumpsLeft;
        string _clip;
        readonly Dictionary<string, string> _clips = new();
        readonly Collider[] _hits = new Collider[32];
        readonly HashSet<Rigidbody> _pushed = new();

        /// <summary>Teleports the body (setting the transform alone is undone by interpolation).</summary>
        public void Place(Vector3 at)
        {
            Body.position = at;
            transform.position = at;
            Body.linearVelocity = Vector3.zero;
        }

        public static Cat Spawn(Vector3 at, Tuning t)
        {
            var go = new GameObject("Cat") { layer = 2 };
            go.transform.position = at;
            var cat = go.AddComponent<Cat>();
            cat.T = t;
            var rb = cat.Body = go.AddComponent<Rigidbody>();
            rb.mass = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.13f;
            col.height = 0.5f;
            col.center = new Vector3(0, 0.15f, 0);
            col.sharedMaterial = new PhysicsMaterial("cat") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum };

            var prefab = Resources.Load<GameObject>("Art/Cat/Cat");
            var model = Instantiate(prefab, go.transform, false);
            model.name = "Model";
            foreach (var t2 in model.GetComponentsInChildren<Transform>()) t2.gameObject.layer = 2;
            cat.FitModel(model);
            cat._anim = model.GetComponentInChildren<Animation>();
            if (cat._anim)
            {
                cat._anim.playAutomatically = false;
                cat._anim.Stop();
                // clip names come in as "AnimalArmature|AnimalArmature|...|Idle"; key them by the last part
                foreach (AnimationState st in cat._anim) cat._clips[st.name.Substring(st.name.LastIndexOf('|') + 1)] = st.name;
                cat._clip = null;
            }
            return cat;
        }

        /// <summary>Scales the model from its baked mesh (skinned bounds are unreliable) so it stands BodyHeight tall, feet on 0.</summary>
        void FitModel(GameObject model)
        {
            model.transform.localRotation = Quaternion.Euler(0, ModelYaw, 0);
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                foreach (var v in baked.vertices)
                {
                    var w = transform.InverseTransformPoint(smr.transform.TransformPoint(v));
                    min = Vector3.Min(min, w);
                    max = Vector3.Max(max, w);
                }
                Destroy(baked);
                smr.updateWhenOffscreen = true;
            }
            if (float.IsInfinity(min.x)) return;
            float s = BodyHeight / (max.y - min.y);
            model.transform.localScale *= s;
            model.transform.localPosition = new Vector3(-(min.x + max.x) * 0.5f * s, -min.y * s, -(min.z + max.z) * 0.5f * s);
            Debug.Log($"[cat] model size {(max - min)} scaled by {s:0.###}");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _pawCd -= dt;
            _pawAnim -= dt;
            _jumpBuffer -= dt;
            _decalCd -= dt;
            if (Controllable) ReadInput();
            else { _move = AutoMove; _run = AutoMove.sqrMagnitude > 0.5f; _scratchHeld = AutoScratch; }
            if (AutoPaw && (_autoPawCd -= dt) < 0) { _autoPawCd = 0.6f; Paw(); }
            Scratch(dt);
            Animate();
        }

        void ReadInput()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k == null || m == null) return;
            var v = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            var cam = Game.I.Rig.transform;
            var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var r = Vector3.Cross(Vector3.up, f);
            _move = Vector3.ClampMagnitude(f * v.y + r * v.x, 1f);
            _run = k.leftShiftKey.isPressed;
            if (k.spaceKey.wasPressedThisFrame) Jump();
            if (m.leftButton.wasPressedThisFrame) Paw();
            _scratchHeld = m.rightButton.isPressed;
            if (k.qKey.wasPressedThisFrame) Meow();
        }

        public void Jump() => _jumpBuffer = 0.15f;

        void FixedUpdate()
        {
            Grounded = Physics.CheckSphere(transform.position + Vector3.up * 0.08f, 0.1f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                       && Body.linearVelocity.y < 1.5f;
            if (Grounded) _airJumpsLeft = T.airJumps;

            float speed = Scratching ? 0f : (_run ? T.runSpeed : T.walkSpeed) * (Game.I.FeverLeft > 0 ? T.feverSpeed : 1f);
            var want = _move * speed;
            var vel = Body.linearVelocity;
            var flat = new Vector3(vel.x, 0, vel.z);
            flat = Vector3.MoveTowards(flat, want, (Grounded ? T.groundAccel : T.airAccel) * Time.fixedDeltaTime);
            vel = new Vector3(flat.x, vel.y, flat.z);

            if (_jumpBuffer > 0 && (Grounded || _airJumpsLeft > 0))
            {
                if (!Grounded) _airJumpsLeft--;
                vel.y = Grounded ? T.jumpSpeed : T.airJumpSpeed;
                _jumpBuffer = 0;
                Sfx.Jump(transform.position);
            }
            Body.linearVelocity = vel;

            if (_move.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(new Vector3(_move.x, 0, _move.z));
                Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, face, T.turnDegPerSec * Time.fixedDeltaTime));
            }
        }

        /// <summary>Swat: shoves every loose thing in front of the paw, at the point it was hit so tall things topple.</summary>
        public void Paw()
        {
            if (_pawCd > 0) return;
            _pawCd = T.pawCooldown;
            _pawAnim = 0.4f;
            var center = transform.position + transform.forward * T.pawReach + Vector3.up * 0.2f;
            Sfx.Swipe(center);
            int n = Physics.OverlapSphereNonAlloc(center, T.pawRadius, _hits, ~(1 << 2), QueryTriggerInteraction.Ignore);
            _pushed.Clear();
            var dir = (transform.forward + Vector3.up * T.pawLift).normalized;
            for (int i = 0; i < n; i++)
            {
                var rb = _hits[i].attachedRigidbody;
                if (!rb || rb.isKinematic || !_pushed.Add(rb)) continue;
                float dv = T.pawPush * (Game.I.FeverLeft > 0 ? T.feverPush : 1f) / Mathf.Sqrt(Mathf.Max(0.2f, rb.mass));
                rb.AddForceAtPosition(dir * dv * rb.mass, _hits[i].ClosestPoint(center), ForceMode.Impulse);
                if (rb.TryGetComponent<Vacuum>(out var vac)) vac.Hit(transform.position);
            }
            for (int i = Butterfly.All.Count - 1; i >= 0; i--)
                if ((Butterfly.All[i].transform.position - center).sqrMagnitude < (T.pawRadius + 0.12f) * (T.pawRadius + 0.12f)) Butterfly.All[i].Catch();
            if (_pushed.Count > 0) Game.I.Hud.Pop("냥!", center + Vector3.up * 0.2f, new Color(1f, 0.85f, 0.95f), 20);
        }

        public void Meow()
        {
            Sfx.Meow(transform.position);
            Game.I.Hud.Pop(Random.value < 0.5f ? "냐옹~" : "애옹!", transform.position + Vector3.up * 0.6f, Color.white, 24);
        }

        void Scratch(float dt)
        {
            Scratching = false;
            if (_scratchHeld && Grounded)
            {
                var from = transform.position + Vector3.up * 0.2f;
                if (Physics.SphereCast(from, 0.1f, transform.forward, out var hit, 0.45f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    var s = hit.collider.GetComponentInParent<Scratchable>();
                    if (s)
                    {
                        Scratching = true;
                        Game.I.OnScratch(s, dt, hit.point);
                        if (_decalCd < 0) { _decalCd = 0.3f; Claw(hit); }
                    }
                }
            }
            Sfx.Scratching(Scratching);
        }

        /// <summary>Three thin claw lines left on the furniture.</summary>
        static void Claw(RaycastHit hit)
        {
            var root = new GameObject("claw").transform;
            root.position = hit.point + hit.normal * 0.004f + Random.insideUnitSphere * 0.05f;
            root.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(-25f, 25f));
            root.SetParent(hit.collider.transform, true);
            var m = Mats.Flat(new Color(0.95f, 0.92f, 0.85f), 0.1f);
            for (int i = -1; i <= 1; i++)
            {
                var q = Mats.Shape(PrimitiveType.Quad, root, new Vector3(i * 0.025f, 0, 0), Vector3.one, m, new Vector3(0, 180, 0));
                q.transform.localScale = new Vector3(0.008f / root.lossyScale.x, 0.12f / root.lossyScale.y, 1);
                Mats.NoShadows(q);
            }
        }

        void Animate()
        {
            if (!_anim) return;
            float speed = new Vector3(Body.linearVelocity.x, 0, Body.linearVelocity.z).magnitude;
            string want = Scratching ? "Idle_Eating"
                : _pawAnim > 0 ? "Headbutt"
                : !Grounded ? "Jump_Loop"
                : speed > T.walkSpeed * 1.2f ? "Run"
                : speed > 0.15f ? "Walk"
                : "Idle";
            if (!_clips.TryGetValue(want, out var full)) return;
            if (want != _clip)
            {
                _clip = want;
                if (want == "Headbutt") _anim.Rewind(full);
                _anim.CrossFade(full, want == "Headbutt" ? 0.05f : 0.15f);
            }
            var st = _anim[full];
            if (want == "Walk" || want == "Run") st.speed = Mathf.Clamp(speed / (want == "Run" ? T.runSpeed : T.walkSpeed), 0.6f, 1.6f);
            else if (want == "Headbutt") st.speed = 1.8f;
        }
    }
}
