using System;
using UnityEngine;

namespace LastTrain
{
    /// <summary>Gameplay values, read once from Resources/tuning.json.</summary>
    [Serializable]
    public class Tuning
    {
        [Serializable] public class PlayerCfg
        {
            public float speed, sprintMult, crouchMult, hp, reviveRadius, reviveSeconds, downedSeconds, reviveHp, teamDamagePerMate;
            public float hungerMax, hungerPerSec, starveDps, foodValue, bandageHeal, sleepHealPerSec;
        }
        [Serializable] public class ClockCfg { public float daySeconds, nightSeconds, evacRadius, sleepSpeed; }
        [Serializable] public class SpawnCfg
        {
            public float dayPerSec, nightPerSec, evacPerSec, growthPerDay, ringMin, ringMax, hpGrowthPerDay, runnerDay, runnerNight, brute;
            public int dayStart, maxAlive, bruteFromDay;
        }
        [Serializable] public class SensesCfg
        {
            public float sightDay, sightNight, flashlightBonus, sightAngle, crouchSight, touch;
            public float noiseWalk, noiseSprint, noiseCrouch, noiseSearch, forgetSeconds, stealthRange;
        }
        [Serializable] public class ZombieCfg { public string id; public float hp, speed, chaseSpeed, dps, radius, scale; }
        [Serializable] public class ShelterCfg { public float barricadeHp, repairPerScrap, searchSeconds; }
        [Serializable] public class WeaponCfg
        {
            public string id, name, kind;
            public float damage, cooldown, range, arc, noise, knockback, speed, radius, flight;
            public int durability, pierce;
        }
        [Serializable] public class LootCfg { public string item; public int weight, min, max; }
        [Serializable] public class LootTableCfg { public string name; public LootCfg[] items; }
        [Serializable] public class ShopCfg { public string type, name, color; public int count, shelves; }
        [Serializable] public class StationCfg { public int fusesNeeded; public float poweredSpawnMult, poweredEvacSeconds; }
        [Serializable] public class TradeCfg { public string give, get; public int giveN, getN; }
        [Serializable] public class AirdropCfg { public float earliest, latest, minDistance, maxDistance, flareNoise, flareEvery; public int rolls; }

        [Serializable] public class StreetEventsCfg
        {
            public float beaconSeconds, beaconRadius, blackoutAfter;
            public int blackoutDay, blackoutScrap;
        }

        [Serializable] public class BagCfg
        {
            public int capacity, food, scrap, bandage, nails, fireworks, fuse;
            public int Weight(string item) => item switch
            {
                "food" => food, "scrap" => scrap, "bandage" => bandage,
                "nails" => nails, "fireworks" => fireworks, "fuse" => fuse, _ => 0,
            };
        }

        [Serializable] public class EncounterCfg
        {
            public float requestChance, rescueChance, minDistance, maxDistance, requestAt, rescueAt, rescueWindow;
            public int requestDay, rescueDay, attackers;
        }

        [Serializable] public class StoryCfg
        {
            public int marketGuards, stationGuards;
            public float guardTriggerDistance;
        }

        [Serializable] public class EscapeCfg
        {
            public int roadScrap;
            public float roadSeconds, drainSeconds, drainRadius, roadSpawn, drainSpawn, retreatDistance;
        }

        public EscapeCfg escape;
        public StoryCfg story;
        public BagCfg bag;
        public EncounterCfg encounters;
        public StreetEventsCfg streetEvents;
        public PlayerCfg player;
        public ClockCfg clock;
        public SpawnCfg spawn;
        public SensesCfg senses;
        public ZombieCfg[] zombies;
        public ShelterCfg shelter;
        public WeaponCfg[] weapons;
        public LootTableCfg[] lootTables;
        public ShopCfg[] shops;
        public StationCfg station;
        public TradeCfg[] trades;
        public AirdropCfg airdrop;
        public int containers, slots;

        static Tuning _data;
        public static Tuning Data => _data ??= Load();

        public static Tuning Load() => JsonUtility.FromJson<Tuning>(Resources.Load<TextAsset>("tuning").text);

        public WeaponCfg Weapon(string id) => Array.Find(weapons, w => w.id == id);
        public bool IsWeapon(string id) => Weapon(id) != null;
        public LootCfg[] Loot(string table) => (Array.Find(lootTables, t => t.name == table) ?? lootTables[0]).items;
    }
}
