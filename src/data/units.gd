class_name Units
## Tuning data for monsters, heroes, traps and build costs. All gameplay numbers live here.
## Unit keys: hp, damage, range (extra reach beyond both radii; shooting distance if ranged),
## speed, windup, cooldown, ranged, aggro (target search radius), radius, color.

const MONSTERS := {
	"slime": {
		"sprite": "slime", "name": "슬라임", "cost": 20, "hp": 14, "damage": 1, "range": 4.0, "speed": 35.0,
		"windup": 0.5, "cooldown": 1.0, "ranged": false, "aggro": 60.0, "leash": 40.0,
		"radius": 7.0, "color": Color(0.3, 0.8, 0.4),
	},
	"goblin": {
		"sprite": "goblin", "name": "고블린", "cost": 35, "hp": 7, "damage": 2, "range": 4.0, "speed": 75.0,
		"windup": 0.3, "cooldown": 0.7, "ranged": false, "aggro": 80.0, "leash": 56.0,
		"radius": 6.0, "color": Color(0.5, 0.65, 0.2),
	},
	"skeleton": {
		"sprite": "skeleton", "name": "해골궁수", "cost": 45, "hp": 4, "damage": 1, "range": 90.0, "speed": 40.0,
		"windup": 0.6, "cooldown": 1.4, "ranged": true, "aggro": 110.0, "leash": 24.0,
		"radius": 6.0, "color": Color(0.85, 0.85, 0.8),
	},
}

## Summoned by the "imps" skill and spawned by nests; not buildable.
const SUMMONS := {
	"imp": {
		"sprite": "imp", "name": "임프", "hp": 4, "damage": 1, "range": 4.0, "speed": 85.0,
		"windup": 0.25, "cooldown": 0.6, "ranged": false, "aggro": 90.0, "leash": 9999.0,
		"radius": 5.0, "color": Color(1.0, 0.45, 0.3),
	},
}

## Buildable objects. lure: heroes detour to loot it (loot_time s each, once per hero).
## nest: spawns a weak slime every spawn_every s during raids, up to max_alive.
const OBJECTS := {
	"lure": {"name": "미끼 보물", "cost": 30, "loot_time": 2.5, "reach": 10, "color": Color(1.0, 0.85, 0.2)},
	"nest": {"name": "슬라임 둥지", "cost": 60, "spawn_every": 7.0, "max_alive": 2, "color": Color(0.4, 0.9, 0.5)},
}

## Terrain from dungeon generation. water: hero speed multiplier. lava: damage per tick to heroes.
const TERRAIN := {
	"water": {"slow": 0.45},
	"lava": {"damage": 1, "interval": 0.7},
}

## gold: reward on kill. core_damage: damage dealt when the hero reaches the core.
## Optional: skill (+skill_cd) — heal | fireball | boss; armor (damage taken x (1-armor));
## dodge (chance to ignore a hit); fallback + tint: sprite to borrow (tinted) until its own exists.
const HEROES := {
	"warrior": {
		"sprite": "warrior", "hp": 8, "damage": 1, "range": 4.0, "speed": 60.0, "windup": 0.22, "cooldown": 0.8,
		"ranged": false, "aggro": 75.0, "radius": 7.0, "color": Color(0.85, 0.75, 0.3),
		"gold": 8, "core_damage": 2, "name": "전사",
	},
	"archer": {
		"sprite": "archer", "hp": 5, "damage": 1, "range": 80.0, "speed": 50.0, "windup": 0.35, "cooldown": 1.1,
		"ranged": true, "aggro": 110.0, "radius": 6.0, "color": Color(0.4, 0.7, 0.95),
		"gold": 10, "core_damage": 1, "name": "궁수",
	},
	"cleric": {
		"sprite": "cleric", "fallback": "warrior", "tint": Color(1.3, 1.25, 0.85), "hp": 6, "damage": 1,
		"range": 4.0, "speed": 45.0, "windup": 0.3, "cooldown": 1.0, "ranged": false, "aggro": 60.0,
		"radius": 6.0, "color": Color(1.0, 0.95, 0.6), "gold": 14, "core_damage": 1, "name": "성직자",
		"skill": "heal", "skill_cd": 4.0,
	},
	"mage": {
		"sprite": "mage", "fallback": "archer", "tint": Color(0.7, 0.8, 1.6), "hp": 5, "damage": 2,
		"range": 90.0, "speed": 42.0, "windup": 0.45, "cooldown": 1.5, "ranged": true, "aggro": 130.0,
		"radius": 6.0, "color": Color(0.5, 0.6, 1.0), "gold": 16, "core_damage": 1, "name": "마법사",
		"skill": "fireball", "skill_cd": 4.5,
	},
	"rogue": {
		"sprite": "rogue", "fallback": "archer", "tint": Color(0.9, 0.6, 1.3), "hp": 5, "damage": 2,
		"range": 4.0, "speed": 95.0, "windup": 0.15, "cooldown": 0.5, "ranged": false, "aggro": 60.0,
		"radius": 6.0, "color": Color(0.7, 0.4, 0.9), "gold": 12, "core_damage": 3, "name": "도적",
		"dodge": 0.35,
	},
	"guardian": {
		"sprite": "guardian", "fallback": "warrior", "tint": Color(0.75, 0.85, 1.2), "hp": 22, "damage": 2,
		"range": 4.0, "speed": 35.0, "windup": 0.35, "cooldown": 1.0, "ranged": false, "aggro": 70.0,
		"radius": 8.0, "color": Color(0.6, 0.7, 0.9), "gold": 20, "core_damage": 3, "name": "방패병",
		"armor": 0.5,
	},
	"boss": {
		"sprite": "boss_hero", "hp": 120, "damage": 2, "range": 100.0, "speed": 30.0, "windup": 0.5, "cooldown": 1.2,
		"ranged": true, "burst": 10, "aggro": 170.0, "radius": 12.0, "color": Color(0.95, 0.4, 0.2),
		"gold": 0, "core_damage": 10, "name": "용사 보스", "skill": "boss", "skill_cd": 3.5, "armor": 0.2,
	},
}

## Elite heroes (from wave 4): chance grows per wave; stat and reward multipliers.
const ELITE_CHANCE_PER_WAVE := 0.05
const ELITE := {"hp": 2.2, "damage": 1.5, "speed": 1.15, "gold": 3, "scale": 1.25}

## Monster growth: XP needed for Lv2 / Lv3 (1 XP per hero killed nearby), stat gain per level,
## and the price of the 강화 tool (cost x current level).
const MONSTER_XP := [3, 8]
const MONSTER_LEVEL_HP := 1.45
const MONSTER_LEVEL_DMG := 1.35
const MONSTER_MAX_LEVEL := 3
const UPGRADE_COST := 35

const TRAPS := {
	"spike": {"sprite": "spike", "name": "가시", "cost": 25, "damage": 1, "interval": 0.8, "color": Color(0.65, 0.65, 0.7)},
	"fire": {"sprite": "fire_trap", "name": "화염", "cost": 40, "damage": 3, "interval": 1.2, "color": Color(1.0, 0.45, 0.1)},
}

const WALL_COST := 5
const START_GOLD := 150
const WAVE_CLEAR_GOLD := 40
const CORE_HP := 20
const LORD_RESPAWN_TIME := 3.0
const BOLT_SPEED_ENEMY := 230.0

## Build-phase shop. price grows by step each purchase (price + step * times_bought).
const SHOP := {
	"repair": {"name": "코어 수리 +5", "price": 40, "step": 0},
	"lord_atk": {"name": "마왕 공격 +1", "price": 50, "step": 30},
	"lord_hp": {"name": "마왕 HP +3", "price": 45, "step": 25},
	"bolts": {"name": "마법탄 +1", "price": 55, "step": 40},
	"traps": {"name": "함정 데미지 +1", "price": 60, "step": 40},
}
