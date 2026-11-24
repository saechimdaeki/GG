using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// A failing lamp: holds steady for a few seconds, then stutters off and on in a short burst.
    /// Visual only and local to each client, so it needs no network sync.
    /// </summary>
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] Light target;
        [SerializeField] Renderer glow;
        [SerializeField] Material offMaterial;
        [SerializeField] float steadyMin = 2f;
        [SerializeField] float steadyMax = 7f;
        [SerializeField, Range(0f, 1f)] float dimLevel = 0.06f;

        float baseIntensity;
        Material onMaterial;
        float nextBurst, nextToggle;
        int flickers;
        bool lit = true;

        /// <summary>Wires the lamp parts; used by the editor pass that places the lamp.</summary>
        public void Configure(Light light, Renderer lens, Material off)
        {
            target = light;
            glow = lens;
            offMaterial = off;
        }

        void Awake()
        {
            baseIntensity = target.intensity;
            if (glow != null)
                onMaterial = glow.sharedMaterial;
            nextBurst = Time.time + Random.Range(steadyMin, steadyMax);
        }

        void Update()
        {
            if (flickers == 0)
            {
                if (Time.time < nextBurst)
                    return;
                flickers = Random.Range(2, 7);
                nextToggle = Time.time;
            }
            if (Time.time < nextToggle)
                return;
            Set(!lit);
            nextToggle = Time.time + Random.Range(0.03f, 0.14f);
            if (lit && --flickers == 0)
                nextBurst = Time.time + Random.Range(steadyMin, steadyMax);
        }

        void Set(bool on)
        {
            lit = on;
            target.intensity = on ? baseIntensity : baseIntensity * dimLevel;
            if (glow != null && offMaterial != null)
                glow.sharedMaterial = on ? onMaterial : offMaterial;
        }
    }
}
