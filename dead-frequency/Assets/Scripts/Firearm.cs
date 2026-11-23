using System;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>One carried gun: its first-person view parts and its tuning. Runtime ammo lives here too.</summary>
    [Serializable]
    public sealed class Firearm
    {
        public string label = "권총";
        public Transform view;
        public Transform magazine;
        public Transform slide;
        public Transform supportHand;
        public GameObject muzzle;
        public AudioClip shot;
        public float pitch = 1f;
        public int capacity = 9;
        public int startingReserve = 72;
        public float fireInterval = 0.15f;
        public float reloadDuration = 1.2f;
        public int damage = 50;
        public int pellets = 1;
        /// <summary>Cone half-angle in degrees for each pellet.</summary>
        public float spread;
        public bool automatic;
        /// <summary>Round keeps going through infected (stops at walls).</summary>
        public bool pierces;
        public float kick = 1f;

        [NonSerialized] public bool owned;
        [NonSerialized] public int rounds;
        [NonSerialized] public int reserve;
        [NonSerialized] public Vector3 rest;
        [NonSerialized] public Quaternion restRotation;
        [NonSerialized] public Vector3 magazineRest;
        [NonSerialized] public Vector3 slideRest;
        [NonSerialized] public Vector3 supportRest;

        public void Prepare()
        {
            rest = view.localPosition;
            restRotation = view.localRotation;
            if (magazine != null) magazineRest = magazine.localPosition;
            if (slide != null) slideRest = slide.localPosition;
            if (supportHand != null) supportRest = supportHand.localPosition;
            rounds = capacity;
            reserve = startingReserve;
        }
    }
}
