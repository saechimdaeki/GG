using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    /// <summary>
    /// Combat sounds: recorded Kenney impact samples (random take, small pitch spread) for blows,
    /// injury and footsteps, layered over short synthesized whooshes, nail shots and blasts.
    /// </summary>
    public sealed class CombatAudio : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> _clips = new();
        readonly Dictionary<string, AudioClip[]> _samples = new();
        AudioSource _speaker;

        public void Build()
        {
            _speaker = gameObject.AddComponent<AudioSource>();
            _speaker.playOnAwake = false;
            _speaker.spatialBlend = 0f;
            Add("swing", 0.11f, 175f, 0.80f, 32f, 0.18f);
            Add("impact", 0.16f, 95f, 0.55f, 27f, 0.22f);
            Add("stealth", 0.13f, 67f, 0.25f, 39f, 0.2f);
            Add("hurt", 0.24f, 57f, 0.38f, 17f, 0.3f);
            Add("nail", 0.12f, 220f, 0.63f, 45f, 0.32f);
            Add("scream", 0.54f, 410f, 0.36f, 3.8f, 0.24f);
            Add("blast", 0.30f, 53f, 0.62f, 15f, 0.44f);
            Load("impact", "impactPunch_heavy");
            Load("metal", "impactMetal_medium");
            Load("stealth", "impactSoft_heavy");
            Load("hurt", "impactPunch_medium");
            Load("step", "footstep_concrete");
        }

        void Load(string key, string file)
        {
            var takes = new List<AudioClip>();
            for (int i = 0; i < 5; i++)
            {
                var c = Resources.Load<AudioClip>($"Audio/{file}_{i:000}");
                if (c) takes.Add(c);
            }
            if (takes.Count > 0) _samples[key] = takes.ToArray();
        }

        void Add(string key, float duration, float tone, float grain, float decay, float volume)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(duration * rate)];
            var random = new System.Random(119);
            float smoothNoise = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                smoothNoise = Mathf.Lerp(smoothNoise, noise, 0.32f);
                float envelope = Mathf.Min(1f, t * 700f) * Mathf.Exp(-t * decay) * (1f - t / duration);
                float bass = Mathf.Sin(2f * Mathf.PI * tone * t * (1f - t * 1.9f));
                samples[i] = volume * envelope * (bass * (1f - grain) + smoothNoise * grain);
            }
            var clip = AudioClip.Create(key, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            _clips.Add(key, clip);
        }

        public void Play(string key, float volume = 1f)
        {
            _speaker.pitch = Random.Range(0.9f, 1.1f);
            if (_clips.TryGetValue(key, out var synth)) _speaker.PlayOneShot(synth, volume);
            if (_samples.TryGetValue(key, out var takes)) _speaker.PlayOneShot(takes[Random.Range(0, takes.Length)], volume);
        }

        void OnDestroy()
        {
            foreach (AudioClip clip in _clips.Values) Destroy(clip);
        }
    }
}
