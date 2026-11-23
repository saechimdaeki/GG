using UnityEngine;

namespace ResidualSignal
{
    /// <summary>A gun lying in the world. Interacting swaps it into the primary slot.</summary>
    public sealed class WeaponPickup : MonoBehaviour
    {
        /// <summary>Index into the player's arsenal.</summary>
        public int arsenalIndex;
    }
}
