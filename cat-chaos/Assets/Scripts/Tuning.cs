using System;
using UnityEngine;

namespace CatChaos
{
    /// <summary>Gameplay numbers, loaded from Resources/tuning.json so balance changes need no code.</summary>
    [Serializable]
    public sealed class Tuning
    {
        public float roundSeconds = 600f, ownerWarnSeconds = 60f, hideHintSeconds = 30f;
        public float walkSpeed = 1.5f, runSpeed = 3.4f, groundAccel = 18f, airAccel = 7f, turnDegPerSec = 720f;
        public float jumpSpeed = 4.2f, airJumpSpeed = 3.6f, gravity = -14f;
        public int airJumps = 1;
        public float pawReach = 0.3f, pawRadius = 0.3f, pawPush = 2.6f, pawLift = 0.35f, pawCooldown = 0.3f;
        public float comboWindow = 2.5f;
        public int comboMax = 5;
        public int ptsTip = 30, ptsBigTip = 120, ptsDrop = 50, ptsTvDrop = 400, ptsBreak = 120, ptsSoil = 80, ptsGoal = 300;
        public float ptsTissuePerMeter = 25f, ptsScratchPerSecond = 40f;
        public float breakSpeed = 2.2f, tipAngle = 55f, dropDistance = 0.4f, tissueLength = 10f;
        public float hideBonus = 1.5f, bedRadius = 0.55f;
        public float vacuumFirst = 75f, vacuumEvery = 120f, vacuumSpeed = 0.8f;
        public int vacuumMax = 2, vacuumHits = 3, ptsVacuumFlip = 500, cleanPenalty = 10;
        public int[] grades = { 1500, 4000, 8000, 14000 };

        public static Tuning Load()
        {
            var a = Resources.Load<TextAsset>("tuning");
            return a ? JsonUtility.FromJson<Tuning>(a.text) : new Tuning();
        }
    }
}
