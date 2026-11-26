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
        public int startFloors = 3, maxFloors = 10, tenantsPerFloor = 2, startTypes = 6, typesPerFloor = 2;
        public int upgradeBase = 10000;   // cost of the next floor = upgradeBase * floors²
        public float rentMin = 25f, rentMax = 45f;  // seconds between a tenant's rent coins
        public int maxCoins = 40;
        public float lineChance = 0.35f;  // chance of idle chatter when a tenant changes activity
        public float chatCooldown = 20f;
        public float jumpChance = 0.25f;  // per second while walking, for jumper types
        public float jumpUp = 160f, jumpReach = 220f;
        public int crowdedWindows = 14;
        public float specialChance = 0.35f;   // idle → signature action (phone, pushups, ...)
        public float sitChance = 0.35f;       // reaching an edge → sit with legs dangling
        public float danceChance = 0.4f;      // a chat turns into a dance
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
        public int roofCapacity = 2;
        public float roofJumpOff = 0.6f;      // chance to hop down on reaching the roof's edge
        public float personalSpace = 22f;     // tenants on one edge keep at least this far apart
        public float eventMin = 60f, eventMax = 120f; // seconds between random events
        public float drillDuration = 12f;
        public int antennaFloors = 4, helipadFloors = 5;
        public int bonusCoin = 2000;          // value of a coin from the bonus shower
        public float rainMin = 180f, rainMax = 420f, rainDuration = 22f;
        public float gravity = 1800f;
        public float walkSpeed = 28f;
        public float holdSpeed = 500f;    // window moving faster than this (pt/s) throws tenants off
        public float flingScale = 0.5f;   // share of the window's velocity a thrown tenant keeps
        public float throwMax = 1600f;
        public float jumpOffChance = 0.25f;
        public float sleepChance = 0.2f;
        public float idleMin = 1.5f, idleMax = 4f;
        public float walkMin = 3f, walkMax = 9f;
        public float sleepMin = 6f, sleepMax = 14f;

        public static Tuning Load()
        {
            var t = new Tuning();
            var a = Resources.Load<TextAsset>("tuning");
            if (a) JsonUtility.FromJsonOverwrite(a.text, t);
            return t;
        }
    }
}
