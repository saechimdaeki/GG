using System;
using UnityEngine;

namespace CatChaos
{
    /// <summary>Gameplay numbers, loaded from Resources/tuning.json so balance changes need no code.</summary>
    [Serializable]
    public sealed class Tuning
    {
        public float roundSeconds = 240f, ownerWarnSeconds = 30f, hideHintSeconds = 20f;
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
        public float vacuumFirst = 35f, vacuumEvery = 55f, vacuumSpeed = 0.8f;
        public int vacuumMax = 2, vacuumHits = 3, ptsVacuumFlip = 500, cleanPenalty = 10;
        /// <summary>"주인이 아끼는 물건": a highlighted prop worth a bonus if wrecked in time.</summary>
        public float targetFirst = 12f, targetEvery = 8f, targetSeconds = 25f;
        public int ptsTarget = 400;
        /// <summary>Catnip fever: faster cat, harder paw, doubled points.</summary>
        public float catnipFirst = 25f, catnipEvery = 45f, feverSeconds = 12f, feverSpeed = 1.4f, feverPush = 1.7f;
        public int feverMult = 2;
        public float butterflyFirst = 45f, butterflyEvery = 50f, butterflyLife = 40f;
        public int ptsButterfly = 300, ptsSplash = 200, ptsTrash = 80;
        /// <summary>Grab-and-pull: tablecloth slide before it comes off; laundry, boxes.</summary>
        public float clothPull = 0.45f;
        public int ptsCloth = 250, ptsLaundry = 80, ptsBox = 200;
        /// <summary>Pet cam: mischief while it records costs camPenalty; a meow into it pays ptsCamCute, a clean watch ptsCamGood.</summary>
        public float camFirst = 55f, camEvery = 50f, camSeconds = 9f;
        public int ptsCamCute = 250, ptsCamGood = 300, camPenalty = 60;
        /// <summary>Points multiplier once the owner texts they are on the way.</summary>
        public float rushMult = 1.5f;
        /// <summary>How many of the mischief goals are drawn for a round.</summary>
        public int goalCount = 8;
        public int[] grades = { 1500, 3500, 7000, 12000 };

        public static Tuning Load()
        {
            var a = Resources.Load<TextAsset>("tuning");
            return a ? JsonUtility.FromJson<Tuning>(a.text) : new Tuning();
        }
    }
}
