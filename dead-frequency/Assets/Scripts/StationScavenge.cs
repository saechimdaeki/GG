using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ResidualSignal
{
    /// <summary>
    /// 연료 수집: L4D2 Scavenge on platform 3. Jerrycans lie around the concourse and platform; carry each one
    /// (no shooting, slower) to the emergency generator beside the terminal. 90 s on the clock, +20 s per can.
    /// Solo only, like the other alternate missions.
    /// </summary>
    public sealed partial class StationTrial
    {
        [Header("Scavenge")]
        [SerializeField] int scavengeCans = 8;
        [SerializeField] float scavengeStartSeconds = 90f;
        [SerializeField] float scavengeSecondsPerCan = 20f;

        static readonly Vector3[] CanSpots =
        {
            new(-3.6f, 0, 0.65f), new(3.55f, 0, 1.55f), new(-3.2f, 0, 6f), new(-3.8f, 0, 24f), new(0.5f, 0, 26f),
            new(-3.4f, 0, 13.5f), new(3.5f, 0, -12f), new(-3.5f, 0, -12f), new(2.4f, 0, 9.5f), new(3.5f, 0, 21f),
        };

        readonly List<GameObject> cans = new();
        GameObject carrying;
        Transform generator;
        int poured;
        int cansNeeded;
        bool scavengeStarted;

        public bool Scavenging => Difficulty.Selected == Difficulty.Mission.Scavenge && scavengeStarted;
        public int FuelPoured => poured;
        public int FuelNeeded => cansNeeded;
        public bool CarryingFuel => carrying != null;
        public IReadOnlyList<GameObject> FuelCans => cans;
        public Vector3 GeneratorPosition => generator != null ? generator.position : terminal.position;

        void BeginScavenge()
        {
            var found = GameObject.Find("Emergency power generator");
            generator = found != null ? found.transform : terminal;
            var template = GameObject.Find("metal_jerrycan");
            // ponytail: no path check; the staff gate is raised at start, so every spot joins the platform mesh.
            foreach (var spot in CanSpots)
            {
                if (cans.Count >= scavengeCans)
                    break;
                if (!NavMesh.SamplePosition(spot, out var ground, 2f, NavMesh.AllAreas))
                    continue;
                cans.Add(MakeCan(template, ground.position));
            }
            cansNeeded = cans.Count;
            poured = 0;
            countdown = scavengeStartSeconds;
            scavengeStarted = true;
            Signal($"연료 수집 · 연료통 {cansNeeded}개를 발전기에 부어라 · 통마다 +{scavengeSecondsPerCan:0}초", 6f);
        }

        static GameObject MakeCan(GameObject template, Vector3 at)
        {
            GameObject can;
            if (template != null)
            {
                can = Instantiate(template, template.transform.parent);
                can.transform.SetParent(null, true);
            }
            else
            {
                // ponytail: fallback block if the clutter jerrycan is ever removed from the scene.
                can = GameObject.CreatePrimitive(PrimitiveType.Cube);
                can.transform.localScale = new Vector3(0.18f, 0.45f, 0.34f);
            }
            can.name = "Scavenge fuel can";
            can.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            // A red marker lamp so cans read in the dark, like the glowing cans of the original mode.
            var glow = new GameObject("Fuel glow").AddComponent<Light>();
            glow.transform.SetParent(can.transform, false);
            glow.transform.localPosition = Vector3.up * 0.6f;
            glow.color = new Color(1f, 0.45f, 0.2f);
            glow.range = 2.2f;
            glow.intensity = 60f;
            glow.shadows = LightShadows.None;
            return can;
        }

        /// <summary>E: pick up a can, or pour the carried one at the generator.</summary>
        bool TryScavenge()
        {
            if (!Scavenging)
                return false;
            if (carrying != null)
            {
                if (!Near(transform.position, GeneratorPosition, 2.4f))
                    return false;
                Pour();
                return true;
            }
            var can = NearestCan(1.7f);
            if (can == null)
                return false;
            carrying = can;
            foreach (var collider in can.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            can.transform.SetParent(eyes.transform, false);
            can.transform.localPosition = new Vector3(0.24f, -0.42f, 0.62f);
            can.transform.localRotation = Quaternion.Euler(0f, 100f, 8f);
            Current.view.gameObject.SetActive(false);
            Signal("연료통을 들었다 · 사격 불가, 발전기로!", 3f);
            return true;
        }

        void Pour()
        {
            cans.Remove(carrying);
            Destroy(carrying);
            carrying = null;
            Current.view.gameObject.SetActive(true);
            poured++;
            countdown += scavengeSecondsPerCan;
            if (poured >= cansNeeded)
            {
                Signal("발전기 가동 · 승강장 전력 복구!", 8f);
                SetPhase(Phase.Complete);
                return;
            }
            Signal($"연료 {poured}/{cansNeeded} · +{scavengeSecondsPerCan:0}초", 3f);
            // Every pour makes noise; the station answers.
            StationInfected.Noise(GeneratorPosition, 18f);
        }

        void DropCan()
        {
            if (carrying == null)
                return;
            carrying.transform.SetParent(null, true);
            Vector3 feet = transform.position + transform.forward * 0.6f;
            if (NavMesh.SamplePosition(feet, out var ground, 2f, NavMesh.AllAreas))
                feet = ground.position;
            carrying.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            foreach (var collider in carrying.GetComponentsInChildren<Collider>())
                collider.enabled = true;
            carrying = null;
            Current.view.gameObject.SetActive(true);
        }

        GameObject NearestCan(float radius)
        {
            GameObject best = null;
            float closest = radius * radius;
            foreach (var can in cans)
            {
                if (can == null || can == carrying)
                    continue;
                float distance = (can.transform.position - transform.position).sqrMagnitude;
                if (distance < closest)
                {
                    closest = distance;
                    best = can;
                }
            }
            return best;
        }

        /// <summary>Inside the platform hold: the clock is the fuel; waves come slower than the shutter defence.</summary>
        void UpdateScavenge()
        {
            if (carrying != null && (Downed || Held))
                DropCan();
            if (carrying != null)
                Current.view.gameObject.SetActive(false);
            nextWave += Time.deltaTime * 0.35f;
            if (countdown <= 0f)
            {
                DropCan();
                Signal("시간 초과 · 발전기가 멈췄다", 6f);
                SetPhase(Phase.Failed);
            }
        }

        string ScavengePrompt()
        {
            if (!Scavenging)
                return null;
            if (carrying != null)
                return Near(transform.position, GeneratorPosition, 2.4f) ? "E · 연료 붓기" : null;
            return NearestCan(1.7f) != null ? "E · 연료통 들기" : null;
        }

        void DrawScavengeTimer()
        {
            float seconds = Mathf.Max(0f, countdown);
            Panel(new Rect(24, 24, 196, 38), HudSurface);
            Text(new Rect(36, 25, 180, 33), $"연료 {poured}/{cansNeeded}  ·  {(int)seconds / 60}:{(int)seconds % 60:00}", statusStyle);
            Panel(new Rect(36, 55, 172 * (cansNeeded > 0 ? (float)poured / cansNeeded : 0f), 2), HudAccent);
            if (carrying != null)
                Text(new Rect(36, 64, 220, 22), "연료통 운반 중 · 발전기로", statusStyle);
        }
    }
}
