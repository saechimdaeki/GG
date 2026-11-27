using System;
using UnityEngine;

namespace DesktopTenants
{
    /// <summary>Gameplay knobs, overridden by Resources/tuning.json. Distances in screen points.</summary>
    [Serializable]
    public class Tuning
    {
        public int fps = 30;
        public float pixel = 3f;          // points per art pixel
        public float spawnEvery = 2.5f;
        public int startFloors = 3, maxFloors = 16, tenantsPerFloor = 2, startTypes = 6, typesPerFloor = 2;
        public int upgradeBase = 50000;       // cost of the first extra floor (from startFloors)
        public float upgradeGrowth = 2.3f;    // each further floor costs this many times the last
        public int towerStartFloor = 10;
        public float towerUpgradeGrowth = 1.12f;
        public float shopUpgradeGrowth = 2.5f; // shop level n→n+1 costs lot price × growth^n
        public int birthdayGift = 1000, marathonPrize = 3000;
        public int neighborhoodReward = 2400;
        public int interestCap = 10000;
        public float rentMin = 40f, rentMax = 70f;  // seconds between a tenant's rent coins
        public int maxCoins = 40;
        public float collectRadius = 34f;     // mouse this close to a coin picks it up
        public float autoCollect = 45f;       // coins left this long collect themselves
        public float lineChance = 0.35f;  // chance of idle chatter when a tenant changes activity
        public float chatCooldown = 20f;
        public float jumpChance = 0.25f;  // per second while walking, for jumper types
        public float jumpUp = 160f, jumpReach = 220f;
        public int crowdedWindows = 14;
        public float specialChance = 0.35f;   // idle → signature action (phone, pushups, ...)
        public float sitChance = 0.35f;       // reaching an edge → sit with legs dangling
        public float danceChance = 0.12f;      // a chat turns into a dance
        public float dizzySpeed = 1100f;      // landing faster than this (pt/s) → dizzy
        public int pokeLimit = 3;             // pokes within a few seconds before a tenant gets angry and runs
        public float sharedLineChance = 0.45f; // chatter about the real weekday / time instead of own lines
        public float climbChance = 0.12f;     // per second while walking (doubled on the floor): go up a ladder
        public float climbSpeed = 100f;
        public float hopChance = 0.15f;       // per second while walking: non-jumpers hop up a short way
        public float hopUp = 90f;
        public float quirkChance = 0.3f;      // idle → small action (stretch, sneeze, zoomies ...)
        public float peekChance = 0.3f;       // reaching an edge → look down
        public float tripChance = 0.01f;      // per second while walking
        public float highfiveChance = 0.2f, argueChance = 0.15f; // chat outcomes besides dancing
        public float rpsChance = 0.12f, hugChance = 0.08f, tagChance = 0.1f, shareChance = 0.08f; // more chat outcomes
        public float petChance = 0.5f;        // a person meeting a cat or dog pets it
        public int roofCapacity = 2;
        public float roofJumpOff = 0.6f;      // chance to hop down on reaching the roof's edge
        public float personalSpace = 22f;     // tenants on one edge keep at least this far apart
        public float eventMin = 60f, eventMax = 120f; // seconds between random events
        public float drillDuration = 12f;
        public int antennaFloors = 4, helipadFloors = 5, neonFloors = 7, tickerFloors = 9;
        public float petDelay = 1f;
        public float tipChance = 0.25f;
        public float interestRate = 0.002f;   // bank interest per payout, per level, on current money       // a petted tenant leaves a small tip           // cursor resting on a tenant this long pets them
        public float visitChance = 0.25f;     // idle on the floor → go into a shop / the park
        public float coffeeBoost = 30f;       // seconds of faster walking after the cafe
        public int bonusCoin = 600;
        public int boxValue = 1200;           // what a delivery-event parcel turns into          // value of a coin from the bonus shower
        public float hotCpu = 85f;            // CPU % that makes tenants sweat (sustained ~6s)
        public float fullMemory = 0.9f;       // memory use share that makes tenants complain
        public float gravity = 1800f;
        public float walkSpeed = 28f;
        public float holdSpeed = 500f;    // window moving faster than this (pt/s) throws tenants off
        public float flingScale = 0.5f;   // share of the window's velocity a thrown tenant keeps
        public float throwMax = 1600f;
        public float jumpOffChance = 0.25f;
        public float sleepChance = 0.2f;
        public float idleMin = 1.5f, idleMax = 4f;
        public float displayTourChance = 0.22f, displayTourCooldown = 45f;
        public float walkMin = 3f, walkMax = 9f;
        public float sleepMin = 6f, sleepMax = 14f;

        /// <summary>Price of adding the next floor to a building that has <paramref name="floors"/>.</summary>
        public int ApartmentCost(int floors)
        {
            int regularUpgrades = Mathf.Min(floors, towerStartFloor - 1) - startFloors;
            int towerUpgrades = Mathf.Max(0, floors - towerStartFloor + 1);
            return Round(upgradeBase * Mathf.Pow(upgradeGrowth, regularUpgrades)
                * Mathf.Pow(towerUpgradeGrowth, towerUpgrades));
        }

        /// <summary>Price of taking a shop bought for <paramref name="lotCost"/> from <paramref name="level"/> to the next.</summary>
        public int ShopUpgradeCost(int lotCost, int level) => Round(lotCost * Mathf.Pow(shopUpgradeGrowth, level));

        static int Round(float won) => Mathf.Max(1000, Mathf.RoundToInt(won / 1000f) * 1000);

        public static Tuning Load()
        {
            var t = new Tuning();
            var a = Resources.Load<TextAsset>("tuning");
            if (a) JsonUtility.FromJsonOverwrite(a.text, t);
            return t;
        }
    }
}
