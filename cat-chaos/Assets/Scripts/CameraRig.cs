using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CatChaos
{
    /// <summary>
    /// Third-person orbit camera. Instead of pushing in against walls it keeps its distance and hides
    /// whatever wall or tall furniture is between it and the cat, so corners stay readable.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public float Yaw = 20f, Pitch = 24f, Distance = 2.3f;
        public float MouseSensitivity = 0.12f;
        public bool Orbit;   // intro: slow automatic orbit
        /// <summary>When set, the camera eases to this fixed shot (the owner at the door).</summary>
        public Vector3? ShotPos, ShotLook;
        /// <summary>Floor area (x, z) the camera may move in.</summary>
        public Rect Bounds = new(-100, -100, 200, 200);

        float _dist;
        Vector3 _pivot;
        readonly HashSet<Renderer> _hidden = new(), _now = new();
        readonly Dictionary<Renderer, ShadowCastingMode> _mode = new();
        readonly RaycastHit[] _hits = new RaycastHit[32];

        void Start() { _dist = Distance; if (Target) _pivot = Target.position; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (ShotPos.HasValue)
            {
                Reveal();
                transform.position = Vector3.Lerp(transform.position, ShotPos.Value, 1 - Mathf.Exp(-dt * 3f));
                var look = Quaternion.LookRotation(ShotLook.Value - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1 - Mathf.Exp(-dt * 4f));
                return;
            }
            if (!Target) return;

            var mouse = Mouse.current;
            if (Orbit) Yaw += dt * 8f;
            else if (mouse != null && Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0)
            {
                var d = mouse.delta.ReadValue();
                Yaw += d.x * MouseSensitivity;
                Pitch = Mathf.Clamp(Pitch - d.y * MouseSensitivity, 0f, 80f);
                Distance = Mathf.Clamp(Distance - mouse.scroll.ReadValue().y * 0.002f, 1.0f, 4.5f);
            }

            _pivot = Vector3.Lerp(_pivot, Target.position + Vector3.up * 0.32f, 1 - Mathf.Exp(-dt * 14f));
            var dir = Quaternion.Euler(Pitch, Yaw, 0) * Vector3.back;
            // Only the ceiling limits the distance; walls get hidden instead.
            float want = Distance;
            if (dir.y > 0.01f) want = Mathf.Min(want, (2.45f - _pivot.y) / dir.y);
            _dist = Mathf.Lerp(_dist, want, 1 - Mathf.Exp(-dt * 8f));
            var pos = _pivot + dir * _dist;
            pos.y = Mathf.Max(pos.y, 0.12f);
            // Stay inside the flat so the outside never shows; inner walls just get hidden.
            var flat = new Vector2(pos.x - _pivot.x, pos.z - _pivot.z);
            pos.x = Mathf.Clamp(pos.x, Bounds.xMin, Bounds.xMax);
            pos.z = Mathf.Clamp(pos.z, Bounds.yMin, Bounds.yMax);
            // Pushed in by an outer wall: climb instead, so the shot keeps its distance and looks down on the cat.
            float lost = flat.sqrMagnitude - new Vector2(pos.x - _pivot.x, pos.z - _pivot.z).sqrMagnitude;
            if (lost > 0) pos.y = Mathf.Min(2.45f, pos.y + Mathf.Sqrt(lost) * 0.8f);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(_pivot - pos);
            Cutaway(pos);
        }

        /// <summary>Hides every CamBlock between the camera and the cat (a little past the camera too, so it is never inside a wall).</summary>
        void Cutaway(Vector3 cam)
        {
            _now.Clear();
            var to = cam - _pivot;
            float len = to.magnitude + 0.1f;
            int n = Physics.SphereCastNonAlloc(_pivot, 0.18f, to.normalized, _hits, len, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider;
                var block = c.GetComponentInParent<CamBlock>();
                if (!block) continue;
                foreach (var r in block.GetComponentsInChildren<Renderer>()) _now.Add(r);
            }
            foreach (var r in _hidden)
                if (r && !_now.Contains(r)) Restore(r);
            foreach (var r in _now)
                if (!_hidden.Contains(r))
                {
                    _mode.TryAdd(r, r.shadowCastingMode);
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            _hidden.Clear();
            _hidden.UnionWith(_now);
        }

        void Restore(Renderer r) => r.shadowCastingMode = _mode.TryGetValue(r, out var m) ? m : ShadowCastingMode.On;

        void Reveal()
        {
            foreach (var r in _hidden) if (r) Restore(r);
            _hidden.Clear();
        }
    }
}
