using System;
using UnityEngine;

namespace CatChaos
{
    /// <summary>
    /// Every sound is synthesized once at startup (no audio files): meows, thuds, clinks,
    /// shattering, paper, scratching, keys and the door.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly System.Random R = new(7);
        static AudioSource[] _pool;
        static int _next;
        static AudioSource _scratchLoop;
        static AudioClip[] _meows;
        public static AudioClip Hum;
        static AudioClip _beep;
        static AudioClip _thud, _clink, _shatter, _swipe, _scratch, _paper, _tick, _keys, _door, _ding, _pop, _jump, _soil;

        public static void Init(Transform host)
        {
            _pool = new AudioSource[20];
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = new GameObject("sfx").AddComponent<AudioSource>();
                s.transform.SetParent(host, false);
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 16f;
                _pool[i] = s;
            }
            _meows = new[] { Meow(0.55f, 520, 860), Meow(0.42f, 620, 940), Meow(0.75f, 470, 780), Meow(0.3f, 700, 1000) };
            _thud = Make(0.3f, t => (Sin(t, 75 - 30 * t) * Exp(t, 22) + N() * Exp(t, 70) * 0.35f) * 0.9f);
            _clink = Make(0.35f, t => (Sin(t, 2350) + Sin(t, 3190) * 0.7f + Sin(t, 4420) * 0.5f) * Exp(t, 16) * 0.22f + N() * Exp(t, 200) * 0.2f);
            _shatter = Shatter();
            float lp = 0;
            _swipe = Make(0.18f, t => { lp += (N() - lp) * (0.08f + t * 2f); return lp * Mathf.Sin(Mathf.PI * t / 0.18f) * 1.6f; });
            float hp = 0, prev = 0;
            _scratch = Make(0.5f, t => { float n = N(); hp = n - prev; prev = n; float g = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 14 * t); return hp * g * g * 0.35f; });
            float pl = 0;
            _paper = Make(0.28f, t => { pl += (N() - pl) * 0.3f; return pl * (R.NextDouble() < 0.02 ? 2.5f : 0.6f) * Mathf.Sin(Mathf.PI * t / 0.28f) * 0.8f; });
            _tick = Make(0.05f, t => (Sin(t, 1900) * 0.5f + N() * 0.5f) * Exp(t, 120) * 0.5f);
            _keys = KeysClip();
            _door = Make(1.1f, t => { float f = 140 + 40 * Mathf.Sin(t * 9) + 30 * Mathf.Sin(t * 23); return (Saw(t, f) * 0.25f + N() * 0.05f) * Mathf.Sin(Mathf.PI * t / 1.1f) * 0.6f; });
            _ding = Make(0.45f, t => (t < 0.12f ? Sin(t, 880) : Sin(t, 1320)) * Exp(t < 0.12f ? t : t - 0.12f, 9) * 0.3f);
            _pop = Make(0.1f, t => Sin(t, 420 + 5000 * t) * Exp(t, 30) * 0.4f);
            _jump = Make(0.12f, t => Sin(t, 300 + 2200 * t) * Mathf.Sin(Mathf.PI * t / 0.12f) * 0.15f);
            float sl = 0;
            _soil = Make(0.4f, t => { sl += (N() - sl) * 0.12f; return sl * Exp(t, 8) * 1.2f; });

            float hl = 0;
            Hum = Make(1f, t => { hl += (N() - hl) * 0.05f; return (Sin(t, 120) * 0.25f + Sin(t, 240) * 0.1f + hl * 0.6f) * 0.5f; });
            _beep = Make(0.3f, t => (Mathf.Sign(Sin(t, t < 0.12f ? 1500 : 1100)) * 0.12f) * (t % 0.15f < 0.1f ? 1 : 0));

            _scratchLoop = host.gameObject.AddComponent<AudioSource>();
            _scratchLoop.clip = _scratch;
            _scratchLoop.loop = true;
            _scratchLoop.volume = 0.6f;
            _scratchLoop.playOnAwake = false;
        }

        // ------------------------------------------------------------------ public cues
        public static void Meow(Vector3 at) => Play(_meows[R.Next(_meows.Length)], at, 0.9f, 0.9f + (float)R.NextDouble() * 0.25f, 0.2f);
        public static void Swipe(Vector3 at) => Play(_swipe, at, 0.5f, 0.9f + (float)R.NextDouble() * 0.3f, 0.3f);
        public static void Jump(Vector3 at) => Play(_jump, at, 0.5f, 1f, 0.2f);
        public static void Shatter(Vector3 at) => Play(_shatter, at, 1f, 0.9f + (float)R.NextDouble() * 0.2f);
        public static void Paper(Vector3 at) => Play(_paper, at, 0.5f, 0.8f + (float)R.NextDouble() * 0.4f);
        public static void Soil(Vector3 at) => Play(_soil, at, 0.8f, 1f);
        public static void Beep(Vector3 at, float pitch = 1f) => Play(_beep, at, 0.6f, pitch, 0.4f);
        public static void Tick() => Play(_tick, Vector3.zero, 0.5f, 1f, 0f);
        public static void Keys() => Play(_keys, Vector3.zero, 0.9f, 1f, 0f);
        public static void Door() => Play(_door, Vector3.zero, 0.8f, 1f, 0f);
        public static void Ding() => Play(_ding, Vector3.zero, 0.7f, 1f, 0f);
        public static void Pop(float pitch = 1f) => Play(_pop, Vector3.zero, 0.5f, pitch, 0f);

        /// <summary>Collision sound: heavier things sound lower, fragile things clink.</summary>
        public static void Impact(Vector3 at, float speed, float mass, bool fragile)
        {
            float vol = Mathf.Clamp01(speed / 4f);
            if (fragile) Play(_clink, at, vol * 0.8f, 0.85f + (float)R.NextDouble() * 0.4f);
            else Play(_thud, at, vol, Mathf.Clamp(1.6f - mass * 0.12f, 0.55f, 1.8f) + (float)R.NextDouble() * 0.1f);
        }

        public static void Scratching(bool on)
        {
            if (on && !_scratchLoop.isPlaying) _scratchLoop.Play();
            else if (!on && _scratchLoop.isPlaying) _scratchLoop.Stop();
        }

        static void Play(AudioClip c, Vector3 at, float vol, float pitch, float spatial = 0.55f)
        {
            if (_pool == null || vol < 0.03f) return;
            var s = _pool[_next = (_next + 1) % _pool.Length];
            s.transform.position = at;
            s.spatialBlend = spatial;
            s.pitch = pitch;
            s.PlayOneShot(c, vol);
        }

        // ------------------------------------------------------------------ synthesis
        static float N() => (float)(R.NextDouble() * 2 - 1);
        static float Sin(float t, float f) => Mathf.Sin(2 * Mathf.PI * f * t);
        static float Saw(float t, float f) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
        static float Exp(float t, float k) => Mathf.Exp(-t * k);

        static AudioClip Make(float sec, Func<float, float> f)
        {
            int n = (int)(sec * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var c = AudioClip.Create("sfx", n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        /// <summary>"mi-aaa-ow": pitch rises to a peak and sags, a moving formant opens the vowel.</summary>
        static AudioClip Meow(float dur, float f0, float peak)
        {
            float phase = 0;
            return Make(dur, t =>
            {
                float x = t / dur;
                float f = f0 + (peak - f0) * Mathf.Sin(Mathf.Min(1f, x * 1.8f) * Mathf.PI * 0.5f) - (peak - f0) * 0.9f * Mathf.Max(0f, x - 0.5f) * 2f;
                f *= 1f + 0.025f * Mathf.Sin(2 * Mathf.PI * 7 * t);
                phase += f / Rate;
                float formant = x < 0.12f ? 500f : Mathf.Lerp(1500f, 800f, (x - 0.12f) / 0.88f);
                float s = 0;
                for (int k = 1; k <= 9; k++)
                {
                    float w = Mathf.Exp(-Mathf.Pow((k * f - formant) / 450f, 2)) + 0.25f / k;
                    s += w * Mathf.Sin(2 * Mathf.PI * k * phase);
                }
                float env = Mathf.SmoothStep(0, 1, x / 0.08f) * Mathf.SmoothStep(0, 1, (1 - x) / 0.3f);
                if (x < 0.12f) env *= 0.45f;   // the closed-mouth "m"
                return s * env * 0.3f;
            });
        }

        static AudioClip Shatter()
        {
            const float dur = 0.8f;
            var tinkles = new (float at, float f)[14];
            for (int i = 0; i < tinkles.Length; i++) tinkles[i] = ((float)R.NextDouble() * 0.45f, 2000 + (float)R.NextDouble() * 4500);
            float prev = 0;
            return Make(dur, t =>
            {
                float n = N(), hp = n - prev; prev = n;
                float s = hp * Exp(t, 10) * 0.5f;
                foreach (var (at, f) in tinkles)
                    if (t > at) s += Sin(t - at, f) * Exp(t - at, 25) * 0.12f;
                return s;
            });
        }

        static AudioClip KeysClip()
        {
            var hits = new float[9];
            for (int i = 0; i < hits.Length; i++) hits[i] = (float)R.NextDouble() * 0.7f;
            return Make(0.9f, t =>
            {
                float s = 0;
                foreach (float at in hits)
                    if (t > at) s += (Sin(t - at, 3100) + Sin(t - at, 4700) * 0.6f) * Exp(t - at, 35) * 0.12f;
                return s;
            });
        }
    }
}
