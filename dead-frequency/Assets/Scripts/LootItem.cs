using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Something worth picking up lying in the station: an ammo box or a first-aid kit.</summary>
    public sealed class LootItem : MonoBehaviour
    {
        public enum Kind { Ammo, Medkit }

        public static readonly List<LootItem> All = new();

        public Kind kind;
        /// <summary>A bot is on its way to take this.</summary>
        [System.NonSerialized] public SurvivorBot claimedBy;

        public bool Available => gameObject.activeSelf;
        public string Label => kind == Kind.Ammo ? "탄약 상자" : "구급상자";

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Take()
        {
            claimedBy = null;
            gameObject.SetActive(false);
        }
    }
}
