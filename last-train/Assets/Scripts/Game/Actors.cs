using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    /// <summary>
    /// Animated bodies for the simulation: adult-shaped characters on the Quaternius skeletons and their legacy
    /// clips (Idle, Walk, Punch, RecieveHit, Defeat, PickUp). Zombies are re-dressed in rotten
    /// palettes with glowing faces, hunch and reach after the clip, flinch when hit and fall with
    /// Defeat. The survivor's weapon swings are hand-animated on top of the clips: a wind-up, a fast
    /// arc (horizontal, backhand or overhead in a 3-hit chain), a follow-through and a torso twist;
    /// heavy weapons are swung with both hands.
    /// </summary>
    public class Actors : MonoBehaviour
    {
        /// <summary>
        /// A bone we pose on top of the clip. Some clips do not key every bone; when the clip did
        /// not write it this frame, put back the pre-pose rotation so offsets never pile up.
        /// </summary>
        class Posed
        {
            public Transform t;
            Quaternion _pre, _written;
            bool _valid;
            public Posed(Transform bone) { t = bone; }
            public void Begin()
            {
                if (!t) return;
                if (_valid && t.localRotation == _written) t.localRotation = _pre;
                _pre = t.localRotation;
            }
            public void End()
            {
                if (!t) return;
                _written = t.localRotation;
                _valid = true;
            }
        }

        const string Walk = "CharacterArmature|Walk";
        const string Idle = "CharacterArmature|Idle";
        const string Punch = "CharacterArmature|Punch";
        const string Defeat = "CharacterArmature|Defeat";
        const string Hit = "CharacterArmature|RecieveHit";
        const string Pick = "CharacterArmature|PickUp";
        const string Shoot = "CharacterArmature|Shoot_OneHanded";

        class View
        {
            public GameObject go;
            public Animation anim;
            public StoreZombie storeZombie;
            public Renderer[] renderers;
            public int kind;
            public GameObject template;
            public float yaw, dieT, lean, tilt, reachL, reachR, twitch, seed, speed, hitT, attackPose;
            public string clip = "";
            public Vector3 lastPos;
            public bool flashing, lunging, aware;
            public Posed torso, head, armL, armR;
        }

        readonly Dictionary<int, View> _live = new();
        readonly List<View> _dying = new();
        readonly Dictionary<GameObject, Stack<View>> _pool = new();
        List<GameObject>[] _templates;
        MaterialPropertyBlock _mpb, _screamerTint, _armoredTint;
        readonly HashSet<int> _seen = new();
        Material _plain;

        public GameObject Player { get; private set; }
        Animation _playerAnim;
        ScavengerBody _scavenger;
        Transform _pTorso, _pArmR, _pForeR, _pFistR, _pArmL, _pForeL, _pFistL;
        Posed[] _playerPosed;
        Vector2 _attackFacing = Vector2.up;

        public void Init()
        {
            _mpb = new MaterialPropertyBlock();
            _mpb.SetColor("_BaseColor", new Color(1f, 0.79f, 0.63f));
            _plain = Resources.Load<Material>("Materials/Plain");
            _screamerTint = new MaterialPropertyBlock();
            _screamerTint.SetColor("_BaseColor", new Color(0.83f, 0.43f, 0.56f));
            _armoredTint = new MaterialPropertyBlock();
            _armoredTint.SetColor("_BaseColor", new Color(0.48f, 0.69f, 0.88f));
            var storeA = StoreTemplate(3);
            var storeB = StoreTemplate(2);
            if (storeA && storeB)
            {
                _templates = new[]
                {
                    new List<GameObject> { storeA, storeB },
                    new List<GameObject> { storeB },
                    new List<GameObject> { storeA },
                    new List<GameObject> { storeB },
                    new List<GameObject> { storeA }
                };
                Debug.Log("[art] Pxltiger zombie models active");
            }
            else
            {
                if (storeA) Destroy(storeA);
                if (storeB) Destroy(storeB);
                var male = Resources.Load<GameObject>("Art/Characters/Zombie_Male");
                var female = Resources.Load<GameObject>("Art/Characters/Zombie_Female");
                _templates = new[]
                {
                    new List<GameObject> { Template(male, "walker", 0), Template(male, "walker", 1), Template(male, "walker", 2), Template(female, "walker", 3) },
                    new List<GameObject> { Template(female, "runner", 1), Template(female, "runner", 0), Template(male, "runner", 3) },
                    new List<GameObject> { Template(male, "brute", 4) },
                    new List<GameObject> { Template(female, "screamer", 3) },
                    new List<GameObject> { Template(male, "armored", 2) },
                };
            }
            var playerRig = Resources.Load<GameObject>("Art/Characters/Worker_Female");
            Player = Spawn(Template(playerRig, "player", -1), "player");
            var originalSkins = Player.GetComponentsInChildren<SkinnedMeshRenderer>();
            _scavenger = ScavengerBody.Attach(Player, playerRig);
            if (_scavenger != null)
            {
                Debug.Log("[art] Free Scavenger model active");
                foreach (var skin in originalSkins) skin.enabled = false;
                Player.GetComponentInChildren<Animation>().cullingType = AnimationCullingType.AlwaysAnimate;
            }
            _playerAnim = Player.GetComponentInChildren<Animation>();
            if (!_playerAnim || _playerAnim[Punch] == null) Debug.LogError("[actors] character clips missing: re-run Setup");
            foreach (var t in Player.GetComponentsInChildren<Transform>())
                switch (t.name)
                {
                    case "Torso": _pTorso = t; break;
                    case "UpperArm.R": _pArmR = t; break;
                    case "LowerArm.R": _pForeR = t; break;
                    case "Fist.R": _pFistR = t; break;
                    case "UpperArm.L": _pArmL = t; break;
                    case "LowerArm.L": _pForeL = t; break;
                    case "Fist.L": _pFistL = t; break;
                }
            _playerPosed = new[] { new Posed(_pTorso), new Posed(_pArmR), new Posed(_pForeR), new Posed(_pArmL), new Posed(_pForeL) };
            BuildWeapons();
        }

        GameObject StoreTemplate(int variant)
        {
            if (!StoreZombie.Ready) return null;
            var source = Resources.Load<GameObject>("Art/Pxltiger/FBXs/Zombie" + variant);
            if (!source) return null;

            var template = new GameObject("Pxltiger-zombie-" + variant);
            template.transform.SetParent(transform, false);
            template.SetActive(false);
            var model = Instantiate(source, template.transform);
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            if (!animator.avatar || !animator.avatar.isValid)
            {
                var rig = Resources.Load<GameObject>("Art/Pxltiger/FBXs/Zombie1");
                if (rig && rig.TryGetComponent<Animator>(out var sourceAnimator))
                    animator.avatar = sourceAnimator.avatar;
            }
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.isHuman)
            {
                Destroy(template);
                return null;
            }

            NormalizeHeight(model, 1.75f);
            model.AddComponent<StoreZombie>();
            return template;
        }

        // Rotten outfits: [skin, clothes, dark clothes, pants]. Index 4 is the bloated brute.
        static readonly Color[][] Outfits =
        {
            new[] { new Color(0.50f, 0.62f, 0.42f), new Color(0.30f, 0.30f, 0.34f), new Color(0.07f, 0.07f, 0.08f), new Color(0.18f, 0.22f, 0.32f) },
            new[] { new Color(0.62f, 0.68f, 0.52f), new Color(0.62f, 0.55f, 0.42f), new Color(0.12f, 0.1f, 0.09f), new Color(0.25f, 0.21f, 0.16f) },
            new[] { new Color(0.44f, 0.56f, 0.52f), new Color(0.20f, 0.32f, 0.45f), new Color(0.06f, 0.07f, 0.09f), new Color(0.13f, 0.14f, 0.18f) },
            new[] { new Color(0.58f, 0.62f, 0.48f), new Color(0.48f, 0.12f, 0.10f), new Color(0.09f, 0.05f, 0.05f), new Color(0.16f, 0.16f, 0.19f) },
            new[] { new Color(0.46f, 0.52f, 0.40f), new Color(0.18f, 0.15f, 0.13f), new Color(0.05f, 0.04f, 0.04f), new Color(0.12f, 0.11f, 0.11f) },
        };

        /// <summary>
        /// An inactive copy with its own materials. outfit -1 keeps the model's colors (survivor, NPCs,
        /// with an orange hard hat for the player); otherwise a rotten palette, wet guts and a glowing face.
        /// </summary>
        GameObject Template(GameObject src, string name, int outfit)
        {
            var t = new GameObject(name + "-template");
            t.transform.SetParent(transform);
            var model = Instantiate(src, t.transform);
            NormalizeHeight(model, 1.75f);
            var bones = model.GetComponentsInChildren<Transform>().ToDictionarySafe();
            if (bones.TryGetValue("UpperArm.R", out var right) && bones.TryGetValue("UpperArm.L", out var left) &&
                right.position.x < left.position.x)
                model.transform.Rotate(0f, 180f, 0f, Space.World);

            bool player = name == "player";
            Color coat = player ? new Color(0.40f, 0.33f, 0.24f)
                : outfit >= 0 ? Outfits[outfit][1] * 0.82f : new Color(0.28f, 0.34f, 0.32f);
            Color pants = outfit >= 0 ? Outfits[outfit][3] : new Color(0.20f, 0.24f, 0.23f);
            CharacterBodies.Replace(model, player, outfit, coat, pants);
            t.SetActive(false);
            return t;
        }

        /// <summary>Scales a model so it stands `height` meters tall (idle pose bones) with its feet at y=0.</summary>
        static void NormalizeHeight(GameObject model, float height)
        {
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var anim = model.GetComponentInChildren<Animation>();
            if (anim && anim[Idle] != null)
            {
                var st = anim[Idle];
                st.enabled = true; st.weight = 1; st.time = 0;
                anim.Sample();
                st.enabled = false;
            }
            Bounds BoneBounds()
            {
                var b = new Bounds(model.transform.position, Vector3.zero);
                bool first = true;
                foreach (var smr in smrs)
                foreach (var bone in smr.bones)
                {
                    if (!bone) continue;
                    if (first) { b = new Bounds(bone.position, Vector3.zero); first = false; }
                    else b.Encapsulate(bone.position);
                }
                return b;
            }
            var bb = BoneBounds();
            float bonesTall = Mathf.Max(bb.size.y, 0.001f) * 1.12f;  // the head mesh sits above the last bone
            model.transform.localScale *= height / bonesTall;
            bb = BoneBounds();
            model.transform.position += Vector3.down * (bb.min.y - 0.02f);
            bb = BoneBounds();
            foreach (var smr in smrs)
            {
                var t = smr.transform;
                var lb = new Bounds(t.InverseTransformPoint(bb.center), Vector3.zero);
                foreach (var c in new[] { bb.min, bb.max, new Vector3(bb.min.x, bb.max.y, bb.min.z), new Vector3(bb.max.x, bb.min.y, bb.max.z) })
                    lb.Encapsulate(t.InverseTransformPoint(c));
                lb.Expand(lb.size.magnitude * 0.6f);
                smr.localBounds = lb;
            }
        }

        GameObject Spawn(GameObject src, string name)
        {
            var go = Instantiate(src, transform);
            go.name = name;
            go.SetActive(true);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = false;
            var a = go.GetComponentInChildren<Animation>();
            if (a) a.cullingType = AnimationCullingType.BasedOnRenderers;
            return go;
        }

        View Get(int kind)
        {
            var list = _templates[kind];
            var template = list[Random.Range(0, list.Count)];
            if (_pool.TryGetValue(template, out var stack) && stack.Count > 0)
            {
                var v = stack.Pop();
                v.go.SetActive(true);
                Roll(v);
                return v;
            }
            var go = Spawn(template, "zombie");
            go.transform.localScale = Vector3.one * Tuning.Data.zombies[kind].scale * Random.Range(0.9f, 1.1f);
            var view = new View { go = go, template = template, anim = go.GetComponentInChildren<Animation>(), renderers = go.GetComponentsInChildren<Renderer>(), kind = kind, storeZombie = go.GetComponentInChildren<StoreZombie>() };
            foreach (var r in view.renderers)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var tr in go.GetComponentsInChildren<Transform>())
                switch (tr.name)
                {
                    case "Torso": view.torso = new Posed(tr); break;
                    case "Head": view.head = new Posed(tr); break;
                    case "UpperArm.L": view.armL = new Posed(tr); break;
                    case "UpperArm.R": view.armR = new Posed(tr); break;
                }
            Roll(view);
            return view;
        }

        /// <summary>Each zombie gets its own hunch, head tilt and reaching arms.</summary>
        static void Roll(View v)
        {
            v.lean = Random.Range(14f, 30f) * (v.kind == 1 ? 1.25f : 1f);
            v.tilt = Random.Range(-22f, 22f);
            v.reachL = Random.value < 0.7f ? Random.Range(25f, 50f) : 5f;
            v.reachR = Random.value < 0.7f ? Random.Range(25f, 50f) : 5f;
            v.twitch = Random.Range(0.5f, 1.6f);
            v.seed = Random.Range(0f, 100f);
            v.lunging = false;
            v.hitT = 0;
            v.flashing = true;  // forces the first SyncHorde to set the eye glow
        }

        void LateUpdate()
        {
            float t = Time.time;
            foreach (var v in _live.Values)
            {
                if (v.torso == null) continue;
                v.torso.Begin(); v.head?.Begin(); v.armL?.Begin(); v.armR?.Begin();
                if (v.hitT <= 0)
                {
                    var root = v.go.transform;
                    var right = root.right;
                    var fwd = root.forward;
                    float sway = Mathf.Sin(t * 2.3f + v.seed) * 6f;
                    v.torso.t.rotation = Quaternion.AngleAxis(v.lean + (v.lunging ? 15f : 0f), right) * Quaternion.AngleAxis(sway, fwd) * v.torso.t.rotation;
                    if (v.head?.t)
                    {
                        float jerk = Mathf.PerlinNoise(v.seed, t * 3f * v.twitch) > 0.78f ? Mathf.Sin(t * 40f + v.seed) * 18f : 0f;
                        v.head.t.rotation = Quaternion.AngleAxis(v.tilt + jerk, fwd) * Quaternion.AngleAxis(-v.lean * 0.6f, right) * v.head.t.rotation;
                    }
                    if (!v.lunging)  // the punch animation drives the arms; otherwise they reach and sway a little
                    {
                        float swing = Mathf.Sin(t * 3.1f + v.seed) * 8f;
                        if (v.armL?.t) v.armL.t.rotation = Quaternion.AngleAxis(-v.reachL - swing, right) * v.armL.t.rotation;
                        if (v.armR?.t) v.armR.t.rotation = Quaternion.AngleAxis(-v.reachR + swing, right) * v.armR.t.rotation;
                    }
                }
                v.torso.End(); v.head?.End(); v.armL?.End(); v.armR?.End();
            }
            foreach (var b in _playerPosed) b.Begin();
            AnimateSwing();
            foreach (var b in _playerPosed) b.End();
            _scavenger?.UpdatePose();
            HoldWeapon();
        }

        /// <summary>
        /// Moves every body to its zombie. The walk cycle follows the real speed, aware zombies lunge
        /// with Punch when close, a fresh hit plays RecieveHit, and the body turns toward its way.
        /// </summary>
        public void SyncHorde(Horde h, float dt, Vector2? rescueTarget, HashSet<int> windingZombies)
        {
            _seen.Clear();
            for (int i = 0; i < h.Count; i++)
            {
                int id = h.Id[i];
                _seen.Add(id);
                var p = h.Pos[i];
                var pos = new Vector3(p.x, 0, p.y);
                if (!_live.TryGetValue(id, out var v))
                {
                    v = Get(h.Kind[i]);
                    _live[id] = v;
                    v.lastPos = pos;
                    SetFlash(v, false);
                    v.speed = 0;
                    v.attackPose = 0;
                    v.clip = "";
                    v.go.transform.rotation = Quaternion.identity;
                    if (v.anim) v.anim.enabled = true;
                    v.yaw = Mathf.Atan2(h.Heading[i].x, h.Heading[i].y) * Mathf.Rad2Deg;
                }
                var moved = pos - v.lastPos;
                v.lastPos = pos;
                float inst = dt > 0 ? moved.magnitude / dt : 0;
                v.speed = Mathf.Lerp(v.speed, Mathf.Min(inst, 8f), 1 - Mathf.Exp(-dt * 8f));

                Vector3 attackTarget = Player.transform.position;
                bool attackingRescue = rescueTarget.HasValue && (h.Goal[i] - rescueTarget.Value).sqrMagnitude < 0.04f;
                if (attackingRescue) attackTarget = Fx.V3(rescueTarget.Value);
                bool aware = h.IsAware(i);
                bool lunge = aware && (pos - attackTarget).sqrMagnitude < 1.3f * 1.3f;
                bool winding = windingZombies.Contains(id);
                v.attackPose = winding ? 0.18f : Mathf.Max(0, v.attackPose - dt);
                if (h.Flash[i] > 0) v.attackPose = 0;
                if (v.storeZombie) lunge = winding || v.attackPose > 0 || (attackingRescue && lunge);
                v.lunging = lunge;
                bool flash = h.Flash[i] > 0;
                if (flash && !v.flashing && v.anim && v.anim[Hit] != null)
                {
                    v.hitT = 0.35f;
                    v.anim[Hit].speed = 1.6f;
                    v.anim.Play(Hit);
                    v.anim[Hit].time = 0.05f;
                    v.clip = Hit;
                }
                v.hitT -= dt;
                string clip = v.hitT > 0 ? Hit : lunge ? Punch : v.speed < 0.18f ? Idle : Walk;
                if (v.anim && clip != v.clip && v.anim[clip] != null)
                {
                    if (clip == Punch) v.anim[clip].speed = Random.Range(1.1f, 1.5f);
                    v.anim.CrossFade(clip, 0.2f);
                    v.clip = clip;
                }
                if (v.anim && clip == Walk && v.anim[Walk] != null)
                    v.anim[Walk].speed = Mathf.Clamp(v.speed / 1.25f, 0.35f, 2.6f) / Tuning.Data.zombies[v.kind].scale;

                if (v.storeZombie)
                {
                    bool running = v.kind == 1 && v.speed > 1.8f;
                    var motion = lunge ? StoreZombie.Motion.Attack : v.speed < 0.16f ? StoreZombie.Motion.Idle
                        : running ? StoreZombie.Motion.Run : StoreZombie.Motion.Walk;
                    float speed = motion == StoreZombie.Motion.Walk || motion == StoreZombie.Motion.Run
                        ? Mathf.Clamp(v.speed / (motion == StoreZombie.Motion.Run ? 2.5f : 1.25f), 0.4f, 2f) : 1f;
                    v.storeZombie.Play(motion, speed, dt);
                }

                var face = v.speed > 0.3f && moved.sqrMagnitude > 1e-6f ? new Vector2(moved.x, moved.z) : h.Heading[i];
                if (lunge) face = new Vector2(attackTarget.x - p.x, attackTarget.z - p.y);
                float want = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                v.yaw = Mathf.MoveTowardsAngle(v.yaw, want, (aware ? 300f : 120f) * dt);
                v.go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, v.yaw, 0));

                if (flash != v.flashing || aware != v.aware)
                {
                    v.flashing = flash;
                    v.aware = aware;
                    SetFlash(v, flash);
                }
            }
            if (_live.Count > h.Count)
            {
                var gone = new List<int>();
                foreach (var kv in _live) if (!_seen.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var id in gone) Kill(id, Vector2.zero);
            }
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                var v = _dying[i];
                v.dieT += dt;
                if (v.storeZombie) v.storeZombie.Play(StoreZombie.Motion.Fall, 1.5f, dt);
                var tr = v.go.transform;
                if (v.dieT < 0.2f) tr.position += tr.forward * -dt * 2.4f;  // thrown back by the blow
                else if (v.dieT > CorpseTime) tr.position += Vector3.down * dt * 0.35f;
                if (v.dieT < CorpseTime + 2f) continue;
                v.go.SetActive(false);
                if (!_pool.TryGetValue(v.template, out var stack)) _pool[v.template] = stack = new Stack<View>();
                stack.Push(v);
                _dying.RemoveAt(i);
            }
        }

        /// <summary>What the survivor's body is doing right now (screenshot QA).</summary>
        internal Vector3 PlayerVisualCenter => _scavenger?.HipsPosition ?? Player.transform.position;

        public string PlayerState => $"clip={_playerClip} playing={(_playerAnim && _playerAnim.isPlaying)} swing={(_swingT >= 0 ? _swingT / _swingLen : -1):0.00}";

        /// <summary>Largest lean of a living zombie, hips-to-head against vertical, in degrees (screenshot QA).</summary>
        public float MaxLiveTilt()
        {
            float worst = 0;
            foreach (var v in _live.Values)
                if (v.head?.t && v.hitT <= 0)
                {
                    var up = v.head.t.position - v.go.transform.position;
                    worst = Mathf.Max(worst, Vector3.Angle(up, Vector3.up));
                }
            return worst;
        }

        float CorpseTime => _dying.Count > 70 ? 2.5f : 7f;

        /// <summary>The zombie with this id dies: it turns into the blow and plays Defeat, then sinks away.</summary>
        public void Kill(int id, Vector2 push)
        {
            if (!_live.Remove(id, out var v)) return;
            v.flashing = false;
            v.lunging = false;
            v.hitT = 1f;  // no hunch/reach pose on the corpse
            v.aware = true;
            SetFlash(v, false);
            v.dieT = 0;
            var tr = v.go.transform;
            if (push.sqrMagnitude > 1e-4f) tr.rotation = Quaternion.LookRotation(-new Vector3(push.x, 0, push.y));
            if (v.anim && v.anim[Defeat] != null)
            {
                v.anim[Defeat].speed = 1.5f;
                v.anim[Defeat].wrapMode = WrapMode.ClampForever;
                v.anim.CrossFade(Defeat, 0.05f);
            }
            _dying.Add(v);
        }

        void SetFlash(View v, bool on)
        {
            foreach (var r in v.renderers)
                if (r.enabled) r.SetPropertyBlock(on ? _mpb : v.kind == Horde.Screamer ? _screamerTint
                    : v.kind == Horde.Armored ? _armoredTint : null);
        }

        // ---------------------------------------------------------------- survivor

        string _playerClip = "";
        float _swingT = -1, _swingLen, _comboGap, _aimHold;
        int _combo, _swingStyle;
        bool _twoHanded, _windingMelee;
        readonly Dictionary<string, GameObject> _weapons = new();
        string _shownWeapon;

        public bool Searching;

        /// <summary>A standing, idling survivor NPC (traders, contacts).</summary>
        public GameObject SpawnNpc(string model, Vector2 at, float yaw)
        {
            var go = Spawn(Template(Resources.Load<GameObject>("Art/Characters/" + model), "npc", -1), "npc");
            go.transform.SetPositionAndRotation(new Vector3(at.x, 0, at.y), Quaternion.Euler(0, yaw, 0));
            var a = go.GetComponentInChildren<Animation>();
            if (a && a[Idle] != null) a.Play(Idle);
            return go;
        }

        /// <summary>Simple weapon props; the shaft runs along local +Y from the grip at the origin.</summary>
        void BuildWeapons()
        {
            GameObject Prop(string id, params (PrimitiveType shape, Vector3 pos, Vector3 size, Color c, float glow)[] parts)
            {
                var root = new GameObject("weapon-" + id);
                root.transform.SetParent(Player.transform, false);
                foreach (var p in parts)
                {
                    var g = GameObject.CreatePrimitive(p.shape);
                    Destroy(g.GetComponent<Collider>());
                    g.transform.SetParent(root.transform, false);
                    g.transform.localPosition = p.pos;
                    g.transform.localScale = p.size;
                    var m = new Material(_plain);
                    m.SetColor("_BaseColor", p.c);
                    m.SetFloat("_Smoothness", 0.5f);
                    if (p.glow > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", p.c * p.glow); }
                    g.GetComponent<Renderer>().sharedMaterial = m;
                }
                root.SetActive(false);
                return root;
            }
            var steel = new Color(0.62f, 0.64f, 0.68f);
            var wood = new Color(0.62f, 0.4f, 0.22f);
            var grip = new Color(0.12f, 0.12f, 0.13f);
            _weapons["pipe"] = Prop("pipe", (PrimitiveType.Cylinder, new Vector3(0, 0.36f, 0), new Vector3(0.06f, 0.5f, 0.06f), steel, 0));
            _weapons["bat"] = Prop("bat", (PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0), new Vector3(0.05f, 0.2f, 0.05f), grip, 0),
                (PrimitiveType.Cylinder, new Vector3(0, 0.56f, 0), new Vector3(0.1f, 0.28f, 0.1f), wood, 0));
            _weapons["knife"] = Prop("knife", (PrimitiveType.Cube, new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.14f, 0.04f), grip, 0),
                (PrimitiveType.Cube, new Vector3(0, 0.22f, 0), new Vector3(0.015f, 0.25f, 0.06f), new Color(0.88f, 0.9f, 0.93f), 0.3f));
            _weapons["axe"] = Prop("axe", (PrimitiveType.Cylinder, new Vector3(0, 0.36f, 0), new Vector3(0.05f, 0.48f, 0.05f), wood, 0),
                (PrimitiveType.Cube, new Vector3(0, 0.72f, 0.1f), new Vector3(0.035f, 0.2f, 0.26f), new Color(0.85f, 0.15f, 0.1f), 0));
            _weapons["nailgun"] = Prop("nailgun", (PrimitiveType.Cube, new Vector3(0, 0.05f, 0.1f), new Vector3(0.08f, 0.14f, 0.3f), new Color(0.98f, 0.72f, 0.12f), 0));
            _weapons["fireworks"] = Prop("fireworks", (PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0), new Vector3(0.07f, 0.1f, 0.07f), new Color(0.95f, 0.18f, 0.12f), 1.2f));
        }

        void ShowWeapon(string id)
        {
            if (id == _shownWeapon) return;
            _shownWeapon = id;
            foreach (var kv in _weapons) kv.Value.SetActive(kv.Key == id);
        }

        static float EaseOut(float x) => 1 - (1 - x) * (1 - x) * (1 - x);

        /// <summary>
        /// The swing arc in the survivor's own frame: an aim direction for the weapon arm over time.
        /// 0-0.32 wind-up (slow, pulled back), 0.32-0.55 strike (fast), 0.55-1 follow-through and
        /// recovery back to the clip pose. Returns the arm direction and how strongly to apply it.
        /// </summary>
        (Vector3 dir, float weight, float twist) SwingPose(float k)
        {
            Vector3 from, to;
            switch (_swingStyle)
            {
                case 0:  // horizontal: from the right, low behind, across to the left front
                    from = new Vector3(1f, 0.25f, -0.55f);
                    to = new Vector3(-0.9f, -0.05f, 0.75f);
                    break;
                case 1:  // backhand: from the left front, out to the right
                    from = new Vector3(-0.8f, 0.35f, 0.3f);
                    to = new Vector3(1f, -0.1f, 0.55f);
                    break;
                default:  // overhead chop: high behind the head, down in front
                    from = new Vector3(0.15f, 1f, -0.45f);
                    to = new Vector3(0.05f, -0.75f, 0.85f);
                    break;
            }
            float strike = k < 0.32f ? 0f : k < 0.55f ? EaseOut((k - 0.32f) / 0.23f) : 1f;
            var front = new Vector3(0f, _swingStyle == 2 ? -0.15f : 0.17f, 1f).normalized;
            var dir = strike < 0.38f
                ? Vector3.Slerp(from.normalized, front, strike / 0.38f)
                : Vector3.Slerp(front, to.normalized, (strike - 0.38f) / 0.62f);
            float weight = k < 0.12f ? k / 0.12f : k > 0.7f ? 1 - (k - 0.7f) / 0.3f : 1f;
            float twist = Mathf.Lerp(_swingStyle == 1 ? -35f : 45f, _swingStyle == 1 ? 40f : -50f, strike) * (_swingStyle == 2 ? 0.3f : 1f);
            return (dir, Mathf.Clamp01(weight), twist);
        }

        static void Aim(Transform upper, Transform lower, Transform end, Vector3 dir, float weight)
        {
            if (!upper || !lower || !end || weight <= 0) return;
            var cur = (lower.position - upper.position).normalized;
            upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, dir), weight) * upper.rotation;
            var cur2 = (end.position - lower.position).normalized;
            lower.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur2, dir), weight) * lower.rotation;
        }

        /// <summary>
        /// Swings ride on the Punch clip (which carries the body); on top, the torso twists with the arc
        /// and the weapon arm is pulled halfway toward the arc so the prop sweeps through it.
        /// </summary>
        void AnimateSwing()
        {
            if (_swingT < 0 || !Player) return;
            float k = _swingT / _swingLen;
            if (k >= 1) { _swingT = -1; return; }
            var body = Player.transform;
            var (local, weight, twist) = SwingPose(k);
            var dir = body.TransformDirection(local).normalized;
            if (_pTorso) _pTorso.rotation = Quaternion.AngleAxis(twist * 0.5f * weight, Vector3.up) * _pTorso.rotation;
            Aim(_pArmR, _pForeR, _pFistR, dir, weight * 0.55f);
            if (_twoHanded && _pFistR)
            {
                var toGrip = (_pFistR.position - (_pArmL ? _pArmL.position : body.position)).normalized;
                Aim(_pArmL, _pForeL, _pFistL, toGrip, weight * 0.6f);
            }
        }

        /// <summary>
        /// The prop sits in the right fist. At rest it is held up at the shoulder, ready; during a swing
        /// it points along the arc so the sweep reads clearly from above.
        /// </summary>
        void HoldWeapon()
        {
            if (_shownWeapon == null || !_weapons.TryGetValue(_shownWeapon, out var w) || !_pFistR) return;
            var body = Player.transform;
            Vector3 shaft;
            if (_swingT >= 0)
            {
                var (local, weight, _) = SwingPose(_swingT / _swingLen);
                shaft = Vector3.Slerp(body.TransformDirection(new Vector3(0.25f, 0.8f, 0.4f)), body.TransformDirection(local), weight).normalized;
            }
            else shaft = body.TransformDirection(_shownWeapon is "nailgun" or "fireworks" ? new Vector3(0, -0.2f, 1f) : new Vector3(0.25f, 0.8f, 0.4f)).normalized;
            var across = Vector3.Cross(shaft, body.up).sqrMagnitude > 1e-4f ? Vector3.Cross(body.up, shaft).normalized : body.right;
            w.transform.SetPositionAndRotation((_scavenger?.RightHand ?? _pFistR).position, Quaternion.LookRotation(Vector3.Cross(across, shaft).normalized, shaft));
        }

        public void BeginMelee(Vector2 direction, string weapon, float cooldown, float windup)
        {
            _windingMelee = true;
            _attackFacing = direction;
            _aimHold = cooldown * 0.7f + windup;
            Player.transform.rotation = Quaternion.LookRotation(Fx.V3(direction));
            ShowWeapon(weapon);
            if (_comboGap <= 0f) _combo = 0;
            _swingStyle = weapon == "fists" ? 0 : _combo % 3;
            _twoHanded = weapon is "bat" or "axe" or "pipe";
            _swingLen = windup / 0.40f;
            _swingT = 0f;
        }

        public void CancelMelee()
        {
            _windingMelee = false;
            _swingT = -1;
            _aimHold = 0f;
        }

        void OnDestroy() => _scavenger?.Dispose();

        public void SyncPlayer(Survivor s, bool moving, float dt, bool recoiling)
        {
            var pos = new Vector3(s.Pos.x, 0, s.Pos.y);
            var step = pos - Player.transform.position;
            Player.transform.position = pos;
            // Collision can stop movement even while the walk key remains held.
            float speed = moving && dt > 0 && step.magnitude < 1f ? step.magnitude / dt : 0f;
            _aimHold -= dt;
            if (_aimHold > 0f || speed > 0.10f)
            {
                var face = _aimHold > 0f ? _attackFacing : new Vector2(step.x, step.z);
                float yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                Player.transform.rotation = Quaternion.RotateTowards(Player.transform.rotation, Quaternion.Euler(0, yaw, 0), 630f * dt);
            }

            if (_scavenger == null || !_scavenger.HasLocomotion)
                Player.transform.localScale = new Vector3(1, s.Crouching ? 0.82f : 1f, 1);
            SyncPlayerAction(s, speed > 0.10f, dt, recoiling);
            bool action = _swingT >= 0f || !s.Alive || recoiling || Searching ||
                (_playerClip is Punch or Shoot && _playerAnim.IsPlaying(_playerClip));
            _scavenger?.SetMotion(speed, s.Crouching, action, !s.Alive || (action && speed < 0.25f), dt);
        }

        void SyncPlayerAction(Survivor s, bool moving, float dt, bool recoiling)
        {
            if (!_playerAnim) return;
            var cfg = s.HeldCfg;
            if (_aimHold <= 0f) ShowWeapon(s.Held.id);
            if (_swingT >= 0) _swingT += dt;
            _comboGap -= dt;

            if (s.Attacked != null)
            {
                string attackWeapon = s.Attacked;
                s.Attacked = null;
                cfg = Tuning.Data.Weapon(attackWeapon);
                if (_windingMelee && cfg.kind == "melee")
                {
                    _windingMelee = false;
                    _swingT = 0.40f * _swingLen;
                    _combo++;
                    _comboGap = cfg.cooldown + 0.4f;
                    return;
                }
                _attackFacing = s.LastAttackDirection;
                _aimHold = cfg.cooldown * 0.7f;
                ShowWeapon(attackWeapon);
                Player.transform.rotation = Quaternion.LookRotation(Fx.V3(_attackFacing));
                if (_comboGap <= 0) _combo = 0;
                _comboGap = cfg.cooldown + 0.5f;
                if (cfg.kind == "melee" && attackWeapon != "fists")
                {
                    _swingStyle = _combo % 3;
                    _twoHanded = attackWeapon is "bat" or "axe" or "pipe";
                    _swingLen = Mathf.Clamp(cfg.cooldown * 0.95f, 0.38f, 0.75f);
                    _swingT = 0.44f * _swingLen;  // the hit lands on click: start at the strike, skip most of the wind-up
                    if (_playerAnim[Punch] != null)
                    {
                        _playerAnim[Punch].speed = Mathf.Clamp(1.1f / _swingLen, 1.4f, 2.6f);
                        _playerAnim.Play(Punch);
                        _playerAnim[Punch].time = 0f;
                        _playerClip = Punch;
                    }
                }
                else
                {
                    string clip = cfg.kind == "gun" ? Shoot : Punch;
                    if (_playerAnim[clip] != null)
                    {
                        _playerAnim[clip].speed = cfg.kind == "gun" ? 2.2f : 2.4f;
                        _playerAnim.Play(clip);
                        _playerAnim[clip].time = 0f;
                        _playerClip = clip;
                    }
                    _swingT = -1;
                }
                _combo++;
                return;
            }
            if (_playerClip == Punch || _playerClip == Shoot)
                if (_playerAnim.IsPlaying(_playerClip) && _playerAnim[_playerClip].normalizedTime < 0.85f) return;

            string want = !s.Alive ? Defeat : recoiling ? Hit : Searching ? Pick : moving ? Walk : Idle;
            if (want == _playerClip || _playerAnim[want] == null) return;
            _playerClip = want;
            _playerAnim[want].speed = want == Walk ? (s.Crouching ? 0.9f : s.Sprinting ? 2.3f : 1.7f) : want == Hit ? 1.8f : 1f;
            if (want == Pick) _playerAnim[want].wrapMode = WrapMode.PingPong;
            if (want == Defeat) _playerAnim[want].wrapMode = WrapMode.ClampForever;
            _playerAnim.CrossFade(want, want == Hit ? 0.05f : 0.12f);
        }
    }
}
