using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LastTrain
{
    /// <summary>Crossfades preallocated clips and keeps each model on its simulation root.</summary>
    sealed class HumanoidMotion
    {
        readonly PlayableGraph _graph;
        readonly AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _clips;
        readonly float[] _weights;
        int _target = -1;
        float _speed = 1f;

        public HumanoidMotion(Animator animator, AnimationClip[] clips, bool manual = false)
        {
            animator.applyRootMotion = false;
            _graph = PlayableGraph.Create(animator.name + " motion");
            if (manual) _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, clips.Length);
            _clips = new AnimationClipPlayable[clips.Length];
            _weights = new float[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                var playable = AnimationClipPlayable.Create(_graph, clips[i]);
                playable.SetApplyFootIK(true);
                playable.Pause();
                _graph.Connect(playable, 0, _mixer, i);
                _clips[i] = playable;
            }

            var output = AnimationPlayableOutput.Create(_graph, "Humanoid pose", animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
        }

        public void Blend(int target, float speed, float dt, float response, bool carryPhase = false)
        {
            if (target != _target)
            {
                double time = 0;
                if (carryPhase && _target >= 0)
                {
                    var current = _clips[_target];
                    double phase = current.GetTime() / current.GetAnimationClip().length % 1.0;
                    time = phase * _clips[target].GetAnimationClip().length;
                }
                if (_weights[target] < 0.01f) _clips[target].SetTime(time);
                _clips[target].Play();
                if (_target < 0) _weights[target] = 1f;
                _speed = speed;
                _target = target;
            }

            float blend = 1f - Mathf.Exp(-response * dt);
            _speed = Mathf.Lerp(_speed, speed, blend);
            _clips[target].SetSpeed(_speed);
            for (int i = 0; i < _clips.Length; i++)
            {
                _weights[i] = Mathf.Lerp(_weights[i], i == target ? 1f : 0f, blend);
                _mixer.SetInputWeight(i, _weights[i]);
                if (i != target && _weights[i] < 0.001f) _clips[i].Pause();
            }
        }

        public void Evaluate(float dt) => _graph.Evaluate(dt);

        public float Phase => _target < 0 ? 0f :
            (float)(_clips[_target].GetTime() / _clips[_target].GetAnimationClip().length % 1.0);

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
