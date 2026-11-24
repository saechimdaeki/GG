using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>
    /// Left 4 Dead 2 attrition rules: going down twice leaves you "black and white" (grey screen, heartbeat warning);
    /// a third fall is death until a medkit resets the count. Friendly fire on Advanced/Expert, and an endless
    /// ammo pile for the survival and scavenge holds.
    /// </summary>
    public sealed partial class StationTrial
    {
        [Header("Attrition")]
        [Tooltip("Seconds before the platform supply box refills again in survival and scavenge.")]
        [SerializeField] float supplyRefillSeconds = 25f;

        int downs;
        float supplyReadyAt;
        Volume greyVolume;

        /// <summary>Two falls without a medkit: the next one is fatal.</summary>
        public bool BlackAndWhite => downs >= 2;

        bool EndlessSupply => Difficulty.Selected is Difficulty.Mission.Survival or Difficulty.Mission.Scavenge;

        bool SupplyReady => !tookSupply || (EndlessSupply && Time.time >= supplyReadyAt);

        /// <summary>Platform supply box: one-off in the campaign, an ammo pile with a cooldown in the holds.</summary>
        bool TrySupply()
        {
            if (!SupplyReady || !Nearby(supply))
                return false;
            bool first = !tookSupply;
            tookSupply = true;
            supplyReadyAt = Time.time + supplyRefillSeconds;
            foreach (var gun in arsenal)
                gun.reserve = Mathf.Max(gun.reserve, gun.capacity * 4);
            if (first)
                health = Mathf.Min(100, health + 30);
            if (!EndlessSupply)
                supply.gameObject.SetActive(false);
            Signal(first ? "보급함 · 탄약 보충, 응급처치" : $"탄약 보충 · {supplyRefillSeconds:0}초 뒤 다시", 3f);
            return true;
        }

        /// <summary>The player has just gone down. Returns true when that fall is fatal.</summary>
        bool FallIsFatal()
        {
            if (Networked)
                return false;
            if (BlackAndWhite)
                return true;
            downs++;
            return false;
        }

        void OnRevived()
        {
            if (BlackAndWhite)
                Signal("흑백 상태 · 한 번 더 쓰러지면 끝이다. 구급상자를 찾아라", 6f);
        }

        void OnMedkit()
        {
            downs = 0;
        }

        /// <summary>A teammate used their medkit on the player: heals and clears black and white.</summary>
        public void MedkitHeal(int amount)
        {
            Heal(amount);
            OnMedkit();
        }

        /// <summary>Grey the screen while black and white; run every frame.</summary>
        void UpdateBlackAndWhite()
        {
            if (greyVolume == null)
            {
                if (!BlackAndWhite)
                    return;
                var node = new GameObject("Black and white grade");
                greyVolume = node.AddComponent<Volume>();
                greyVolume.isGlobal = true;
                greyVolume.priority = 50f;
                greyVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var grade = greyVolume.profile.Add<ColorAdjustments>();
                grade.saturation.Override(-100f);
                grade.contrast.Override(18f);
                var edge = greyVolume.profile.Add<Vignette>();
                edge.intensity.Override(0.55f);
            }
            float pulse = 0.85f + Mathf.Sin(Time.time * 7.5f) * 0.05f;
            greyVolume.weight = Mathf.MoveTowards(greyVolume.weight, BlackAndWhite && !Downed ? pulse : 0f,
                Time.deltaTime * 1.5f);
        }

        /// <summary>Advanced/Expert: rounds stop in the first teammate they meet and hurt them.</summary>
        bool TryFriendlyFire(Vector3 origin, Vector3 aim, int damage, out Vector3 hitPoint)
        {
            hitPoint = default;
            if (Difficulty.FriendlyFire <= 0f ||
                !Physics.Raycast(origin, aim, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            var bot = hit.collider.GetComponentInParent<SurvivorBot>();
            if (bot == null || bot.Down)
                return false;
            hitPoint = hit.point;
            bot.TakeFriendlyFire(Mathf.Max(1, Mathf.RoundToInt(damage * Difficulty.FriendlyFire)));
            return true;
        }
    }
}
