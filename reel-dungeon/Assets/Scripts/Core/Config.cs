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
        /// <summary>Companion and relic ids that start locked; finished runs open them in this order (see Run.UnlocksEarned).</summary>
        public string[] unlocks;
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
        /// <summary>Adds this much to the main stat of the faces in the neighbouring slots (left and right in party order).</summary>
        public int neighborBuff;
        /// <summary>Extra damage when a neighbouring slot shows this same symbol.</summary>
        public int twinBonus;
    }

    /// <summary>
    /// A party member. Each member brings one reel; landing its <see cref="signature"/> symbol
    /// charges its skill gauge. Skill ids: shieldBash, volley, freeze, rage, prayer, backstab, transmute, bless, thorns,
    /// allIn (spend skillValue% of gold as extra damage), boneBurst (skulls stop hurting and deal skillValue each).
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
        public bool recruitSignatureBonus;
        public string name;
        public string floorTint;
        public EnemyDef[] enemies;
        public EnemyDef[] elites;
        public EnemyDef boss;
    }

    /// <summary>
    /// An enemy; <see cref="moves"/> repeats as its turn pattern, each "kind:value":
    /// attack:N, guard:N (party damage -N this turn), curse:N (N damage + a skull on a reel for this fight),
    /// enrage:N (later attacks +N), drain:N (N damage, heals by what got through),
    /// steal:N (N damage, takes gold equal to what got through; returned on a kill), seal:N (N damage, no reroll that turn),
    /// heal:N (no attack, heals N), jam:N (N damage, one reel's face counts for nothing that turn),
    /// sap:N (N damage, empties the fullest skill gauge), reflect:P (no attack, P% of the damage it takes hits the party).
    /// <see cref="phase2"/>, when set, replaces moves once the enemy drops to half HP.
    /// </summary>
    [Serializable]
    public class EnemyDef
    {
        public string name;
        public string sprite;
        /// <summary>Optional "#rrggbb" multiplied over the sprite, so one sprite can be several enemies.</summary>
        public string tint;
        public int hp;
        public int gold;
        public string[] moves;
        public string[] phase2;
        /// <summary>Shown when phase 2 starts, e.g. "광폭화".</summary>
        public string phase2Name;
    }

    /// <summary>
    /// Equipment (slot "equip", one per member) or a relic (slot "relic", party-wide).
    /// When a reel it covers shows <see cref="symbol"/>, <see cref="stat"/> gains <see cref="value"/>.
    /// Stats: damage, block, heal, gold, weaken, poison, charge (extra gauge on the signature face),
    /// and relic-only noSelfDamage, reroll, maxHp, battleCharge, regen (heal each turn), firstStrike
    /// (damage on a fight's first turn), spikes (damage to an enemy that attacks), maxParty, lifesteal (heal value% of damage),
    /// curseDamage (each skull showing hits the enemy), comboBonus (damage per combo), rerollGold (gold per reroll), which ignore the symbol.
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
        public int chapter; // 0 means any chapter, otherwise 1-based
        public string title;
        public string text;
        public EventOption[] options;
    }

    [Serializable]
    public class EventOption
    {
        public string label;
        public EventEffect[] effects;
        public int victoryUpgradeChoices; // replaces normal loot when this option starts a fight
        public bool victoryRelic; // a won fight offers relic / equipment / signature upgrade instead
    }

    /// <summary>Kinds: hp, gold, relic, equip, curse, removeCurse, recruit, gamble, fight, slot, upgrade, charge, dupe, trim (remove the weakest ordinary symbol, value times),
    /// mirror (fight the party's reflection), shadow (fight this chapter's boss at value% HP), coinflip (relic or skull),
    /// hpFull, goldAll (lose all gold), transform (target "from>to"), fuse (melt a doubled face, +2 levels, value times), relicId (target).</summary>
    [Serializable]
    public class EventEffect
    {
        public string kind;
        public int value;
        public string target;
    }
}
