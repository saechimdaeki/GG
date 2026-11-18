using System;

namespace ReelDungeon.Core
{
    /// <summary>All tuning values, loaded from Assets/Resources/tuning.json.</summary>
    [Serializable]
    public class TuningConfig
    {
        public int playerMaxHp;
        public int rerollsPerTurn;
        public float pairMultiplier;
        public float tripleMultiplier;
        public int restHeal;
        public int chapterHeal;
        public int skillCharge;
        public int maxParty;
        public int startingGold;
        public int treasureGold;
        public float mimicChance;
        public int mapRows;
        /// <summary>Node types for the middle rows, picked uniformly; repeat a name to weight it.</summary>
        public string[] nodePool;
        public Prices prices;
        public string[] startingParty;
        public SymbolDef[] symbols;
        public CompanionDef[] companions;
        public ChapterDef[] chapters;
        public EnemyDef mimic;
        public ItemDef[] items;
        public EventDef[] events;
    }

    [Serializable]
    public class Prices
    {
        public int addSymbol;
        public int removeCurse;
        public int heal;
        public int healAmount;
        public int recruit;
        public int equip;
        public int relic;
        /// <summary>Symbol upgrade: upgrade + level * upgradeStep.</summary>
        public int upgrade;
        public int upgradeStep;
        /// <summary>Remove one ordinary symbol from a reel.</summary>
        public int trim;
        /// <summary>Restock the shop: reroll + timesRerolled * rerollStep (per run).</summary>
        public int reroll;
        public int rerollStep;
    }

    /// <summary>One reel face. Values are per symbol before combo multipliers.</summary>
    [Serializable]
    public class SymbolDef
    {
        public string id;
        public string name;
        public string color;
        public string icon;
        public int damage;
        public int block;
        public int heal;
        public int selfDamage;
        public int weaken;
        public int gold;
        /// <summary>Poison stacks put on the enemy; it loses that much HP each turn, then the stack shrinks by 1.</summary>
        public int poison;
        public bool curse;
    }

    /// <summary>
    /// A party member. Each member brings one reel; landing its <see cref="signature"/> symbol
    /// charges its skill gauge. Skill ids: shieldBash, volley, freeze, rage, prayer.
    /// </summary>
    [Serializable]
    public class CompanionDef
    {
        public string id;
        public string name;
        public string sprite;
        public string signature;
        /// <summary>How the screen animates this member's hit: melee, arrow, magic, leap or beam.</summary>
        public string attack;
        public string skill;
        public string skillName;
        public string skillDesc;
        public int skillValue;
        public string[] reel;
    }

    /// <summary>One chapter: its look, its enemy pools and its boss.</summary>
    [Serializable]
    public class ChapterDef
    {
        public string name;
        public string floorTint;
        public EnemyDef[] enemies;
        public EnemyDef[] elites;
        public EnemyDef boss;
    }

    /// <summary>
    /// An enemy; <see cref="moves"/> repeats as its turn pattern, each "kind:value":
    /// attack:N, guard:N (party damage -N this turn), curse:N (N damage + a skull on a reel for this fight),
    /// enrage:N (later attacks +N), drain:N (N damage, heals by what got through).
    /// </summary>
    [Serializable]
    public class EnemyDef
    {
        public string name;
        public string sprite;
        public int hp;
        public int gold;
        public string[] moves;
    }

    /// <summary>
    /// Equipment (slot "equip", one per member) or a relic (slot "relic", party-wide).
    /// When a reel it covers shows <see cref="symbol"/>, <see cref="stat"/> gains <see cref="value"/>.
    /// Stats: damage, block, heal, gold, weaken, poison, charge (extra gauge on the signature face),
    /// and relic-only noSelfDamage, reroll, maxHp, battleCharge, regen (heal each turn), firstStrike
    /// (damage on a fight's first turn), spikes (damage to an enemy that attacks), maxParty, which ignore the symbol.
    /// </summary>
    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string desc;
        public string icon;
        public string slot;
        public string symbol;
        public string stat;
        public int value;
    }

    /// <summary>A "?" map stop: a short scene with choices.</summary>
    [Serializable]
    public class EventDef
    {
        public string id;
        public string title;
        public string text;
        public EventOption[] options;
    }

    [Serializable]
    public class EventOption
    {
        public string label;
        public EventEffect[] effects;
    }

    /// <summary>Kinds: hp, gold, relic, equip, curse, removeCurse, recruit, gamble, fight, slot, upgrade, charge, dupe.</summary>
    [Serializable]
    public class EventEffect
    {
        public string kind;
        public int value;
    }
}
