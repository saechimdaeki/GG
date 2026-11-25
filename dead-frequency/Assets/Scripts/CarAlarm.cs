using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Left 4 Dead's alarmed car: a blinking red light under the dash warns it is armed. Shoot it, hit it or blow
    /// something up beside it and the siren wails, the lights flash and a horde pours in from out of sight.
    /// </summary>
    public sealed class CarAlarm : MonoBehaviour
    {
        public const float SirenSeconds = 22f;
        public const int HordeSize = 16;

        static readonly List<CarAlarm> all = new();
        static AudioClip siren;

        Light armed;
        Light[] flashers;
        AudioSource voice;
        float trippedAt = -1f;

        public bool Tripped => trippedAt >= 0f;
        public static IReadOnlyList<CarAlarm> All => all;

        /// <summary>Arm the car under <paramref name="car"/>: its colliders now set the alarm off when struck.</summary>
        public static CarAlarm Arm(Transform car)
        {
            var alarm = car.gameObject.AddComponent<CarAlarm>();
            var bounds = new Bounds(car.position, Vector3.zero);
            foreach (var renderer in car.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            // Just above the sheet, so the blink shows as a red glow on the cloth.
            alarm.armed = Lamp(car, "Alarm LED", bounds.center + Vector3.up * (bounds.extents.y * 1.15f), new Color(1f, 0.05f, 0.02f), 1.8f, 0f);
            // Hazard lights at the four corners.
            alarm.flashers = new Light[4];
            int index = 0;
            foreach (var corner in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
                alarm.flashers[index++] = Lamp(car, "Hazard light", bounds.center + Vector3.Scale(bounds.extents * 1.08f, corner) + Vector3.down * bounds.extents.y * 0.35f,
                    index <= 2 ? new Color(1f, 0.92f, 0.75f) : new Color(1f, 0.55f, 0.1f), 7f, 0f);
            alarm.voice = car.gameObject.AddComponent<AudioSource>();
            alarm.voice.spatialBlend = 1f;
            alarm.voice.maxDistance = 70f;
            alarm.voice.rolloffMode = AudioRolloffMode.Linear;
            alarm.voice.loop = true;
            all.Add(alarm);
            return alarm;
        }

        static Light Lamp(Transform car, string name, Vector3 at, Color color, float range, float intensity)
        {
            var lamp = new GameObject(name).AddComponent<Light>();
            lamp.transform.SetParent(car, true);
            lamp.transform.position = at;
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.range = range;
            lamp.intensity = intensity;
            lamp.shadows = LightShadows.None;
            return lamp;
        }

        void OnDestroy() => all.Remove(this);

        /// <summary>A shot along this ray: trips the first alarmed car it strikes before anything else.</summary>
        public static void CheckShot(Vector3 origin, Vector3 direction, float range)
        {
            if (all.Count == 0)
                return;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                hit.collider.GetComponentInParent<CarAlarm>()?.Trip();
        }

        /// <summary>A blast at <paramref name="point"/> sets off any alarmed car within <paramref name="radius"/>.</summary>
        public static void CheckBlast(Vector3 point, float radius)
        {
            foreach (var alarm in all.ToArray())
                if (Vector3.Distance(alarm.transform.position, point) <= radius + 1.5f)
                    alarm.Trip();
        }

        /// <summary>Sound the alarm. The host also calls the horde; a client tells the host.</summary>
        public void Trip(bool fromNetwork = false)
        {
            if (Tripped)
                return;
            trippedAt = Time.time;
            voice.clip = siren ??= Siren();
            voice.Play();
            armed.intensity = 0f;
            var trial = StationTrial.Local;
            if (trial != null)
                trial.TriggerCrescendo(transform.position, "자동차 경보! 무리가 몰려온다");
            if (!fromNetwork && MissionSync.Instance != null)
                MissionSync.Instance.CarAlarmRpc(all.IndexOf(this));
        }

        void Update()
        {
            if (!Tripped)
            {
                // Armed: a slow red blink under the sheet.
                armed.intensity = Mathf.Repeat(Time.time, 1.4f) < 0.15f ? 40f : 0f;
                return;
            }
            float age = Time.time - trippedAt;
            bool on = age < SirenSeconds && Mathf.Repeat(age, 0.5f) < 0.25f;
            foreach (var lamp in flashers)
                lamp.intensity = on ? 900f : 0f;
            if (age >= SirenSeconds && voice.isPlaying)
                voice.Stop();
        }

        /// <summary>A rising and falling two-tone wail, two seconds long, looped.</summary>
        static AudioClip Siren()
        {
            const int rate = 22050;
            var samples = new float[rate * 2];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float frequency = Mathf.Repeat(t, 1f) < 0.5f ? 960f : 720f;
                phase += 2f * Mathf.PI * frequency / rate;
                // Square-ish horn: odd harmonics, slightly clipped.
                float tone = Mathf.Sin(phase) + 0.33f * Mathf.Sin(3f * phase) + 0.2f * Mathf.Sin(5f * phase);
                samples[i] = Mathf.Clamp(tone * 0.45f, -0.5f, 0.5f);
            }
            var clip = AudioClip.Create("Car alarm", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
