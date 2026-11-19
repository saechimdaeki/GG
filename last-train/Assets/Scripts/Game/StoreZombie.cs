using UnityEngine;

namespace LastTrain
{
    /// <summary>Plays the imported humanoid clips while the simulation owns position and heading.</summary>
    public sealed class StoreZombie : MonoBehaviour
    {
        public enum Motion { Idle, Walk, Run, Attack, Fall }

        static readonly string[] ClipNames = { "Z_Idle", "Z_Walk_InPlace", "Z_Run_InPlace", "Z_Attack", "Z_FallingBack" };
        static AnimationClip[] _clips;
        HumanoidMotion _motion;

        public static bool Ready
        {
            get
            {
                _clips ??= System.Array.ConvertAll(ClipNames, name => Resources.Load<AnimationClip>("Art/Pxltiger/Animations/" + name));
                return System.Array.TrueForAll(_clips, clip => clip != null);
            }
        }

        void OnEnable()
        {
            var animator = GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            _motion = new HumanoidMotion(animator, _clips);
            Play(Motion.Idle, 1f, 0f);
        }

        public void Play(Motion motion, float speed, float dt)
        {
            _motion?.Blend((int)motion, speed, dt, motion is Motion.Attack or Motion.Fall ? 22f : 13f);
        }

        void OnDisable()
        {
            _motion?.Dispose();
            _motion = null;
        }
    }
}
