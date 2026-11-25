using UnityEngine;
using UnityEngine.InputSystem;

namespace CatChaos
{
    /// <summary>Third-person orbit camera that slides in front of walls instead of clipping through them.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public float Yaw = 20f, Pitch = 22f, Distance = 1.8f;
        public float MouseSensitivity = 0.12f;
        public bool Orbit;   // intro: slow automatic orbit
        /// <summary>When set, the camera eases to this fixed shot (the owner at the door).</summary>
        public Vector3? ShotPos, ShotLook;

        float _dist, _lift;
        Vector3 _pivot;

        /// <summary>How far back the camera can sit at this pitch before a wall or big furniture is in the way.</summary>
        float Clear(float pitch)
        {
            var dir = Quaternion.Euler(pitch, Yaw, 0) * Vector3.back;
            float want = Distance;
            // Loose props are ignored, or the camera would jitter.
            foreach (var h in Physics.SphereCastAll(_pivot, 0.12f, dir, Distance, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponent<CamBlock>() && h.distance < want) want = Mathf.Max(0.35f, h.distance - 0.05f);
            return want;
        }

        void Start() { _dist = Distance; if (Target) _pivot = Target.position; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (ShotPos.HasValue)
            {
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
                Pitch = Mathf.Clamp(Pitch - d.y * MouseSensitivity, -5f, 70f);
                Distance = Mathf.Clamp(Distance - mouse.scroll.ReadValue().y * 0.002f, 0.9f, 4f);
            }

            _pivot = Vector3.Lerp(_pivot, Target.position + Vector3.up * 0.32f, 1 - Mathf.Exp(-dt * 14f));
            // In tight rooms the camera rises over the cat instead of pressing into its back.
            float want = Clear(Pitch);
            float pitch = Pitch;
            if (want < Distance * 0.75f)
            {
                _lift = Mathf.MoveTowards(_lift, 1f, dt * 2f);
            }
            else _lift = Mathf.MoveTowards(_lift, 0f, dt * 1f);
            if (_lift > 0)
            {
                pitch = Mathf.Lerp(Pitch, 62f, _lift);
                want = Clear(pitch);
            }
            var dir = Quaternion.Euler(pitch, Yaw, 0) * Vector3.back;
            _dist = want < _dist ? want : Mathf.Lerp(_dist, want, 1 - Mathf.Exp(-dt * 5f));
            var pos = _pivot + dir * _dist;
            pos.y = Mathf.Min(pos.y, 2.45f);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(_pivot - pos);
        }
    }
}
