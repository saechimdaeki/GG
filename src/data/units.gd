class_name Units
## Tuning data for monsters, heroes, traps and build costs. All gameplay numbers live here.
## Unit keys: hp, damage, range (extra reach beyond both radii; shooting distance if ranged),
## speed, windup, cooldown, ranged, aggro (target search radius), radius, color.

const MONSTERS := {
	"slime": {
		"name": "슬라임", "cost": 20, "hp": 14, "damage": 1, "range": 4.0, "speed": 35.0,
		"windup": 0.5, "cooldown": 1.0, "ranged": false, "aggro": 60.0, "leash": 40.0,
		"radius": 7.0, "color": Color(0.3, 0.8, 0.4),
	},
	"goblin": {
		"name": "고블린", "cost": 35, "hp": 7, "damage": 2, "range": 4.0, "speed": 75.0,
		"windup": 0.3, "cooldown": 0.7, "ranged": false, "aggro": 80.0, "leash": 56.0,
		"radius": 6.0, "color": Color(0.5, 0.65, 0.2),
	},
	"skeleton": {
		"name": "해골궁수", "cost": 45, "hp": 4, "damage": 1, "range": 90.0, "speed": 40.0,
		"windup": 0.6, "cooldown": 1.4, "ranged": true, "aggro": 110.0, "leash": 24.0,
		"radius": 6.0, "color": Color(0.85, 0.85, 0.8),
	},
}

## gold: reward on kill. core_damage: damage dealt when the hero reaches the core.
const HEROES := {
	"warrior": {
		"hp": 8, "damage": 1, "range": 4.0, "speed": 55.0, "windup": 0.4, "cooldown": 1.0,
		"ranged": false, "aggro": 50.0, "radius": 7.0, "color": Color(0.85, 0.75, 0.3),
		"gold": 8, "core_damage": 2,
	},
	"archer": {
		"hp": 5, "damage": 1, "range": 80.0, "speed": 50.0, "windup": 0.6, "cooldown": 1.5,
		"ranged": true, "aggro": 90.0, "radius": 6.0, "color": Color(0.4, 0.7, 0.95),
		"gold": 10, "core_damage": 1,
	},
	"boss": {
		"hp": 120, "damage": 2, "range": 100.0, "speed": 30.0, "windup": 0.8, "cooldown": 1.6,
		"ranged": true, "burst": 10, "aggro": 140.0, "radius": 12.0, "color": Color(0.95, 0.4, 0.2),
		"gold": 0, "core_damage": 10,
	},
}

const TRAPS := {
	"spike": {"name": "가시", "cost": 25, "damage": 1, "interval": 0.8, "color": Color(0.65, 0.65, 0.7)},
	"fire": {"name": "화염", "cost": 40, "damage": 3, "interval": 1.2, "color": Color(1.0, 0.45, 0.1)},
}

const DIG_COST := 10
const WALL_COST := 5
const START_GOLD := 120
const WAVE_CLEAR_GOLD := 40
const CORE_HP := 20
const LORD_RESPAWN_TIME := 3.0
const BOLT_SPEED_ENEMY := 160.0
