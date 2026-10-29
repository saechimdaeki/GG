class_name Rooms
## Room themes and shapes for the dungeon generator (DungeonGrid._generate).
## floor: base floor color. decor: tidy props lined along the top wall ("" = none).
## feature: pillars | lava_channel | pool | altar | "" — the room's one terrain idea.
## weight: how often the theme is picked (each theme at most twice per map).

const THEMES := {
	"hall": {"name": "석실", "floor": Color(0.46, 0.42, 0.4), "decor": "", "feature": "pillars", "weight": 3},
	"prison": {"name": "감옥", "floor": Color(0.36, 0.4, 0.47), "decor": "bars", "feature": "", "weight": 2},
	"library": {"name": "서고", "floor": Color(0.5, 0.38, 0.28), "decor": "shelf", "feature": "", "weight": 2},
	"crypt": {"name": "납골당", "floor": Color(0.33, 0.4, 0.34), "decor": "coffin", "feature": "", "weight": 2},
	"forge": {"name": "용광로", "floor": Color(0.5, 0.33, 0.29), "decor": "", "feature": "lava_channel", "weight": 2},
	"flooded": {"name": "수몰실", "floor": Color(0.37, 0.43, 0.45), "decor": "", "feature": "pool", "weight": 2},
	"shrine": {"name": "제단실", "floor": Color(0.42, 0.35, 0.52), "decor": "", "feature": "altar", "weight": 1},
}

## Regions picked on 새 게임. weights override THEMES weights (missing = theme's own),
## max_per_theme caps repeats, modulate is the scene darkness tint, hero_hp / elite scale the
## heroes, souls scales the run's soul reward.
const BIOMES := {
	"dungeon": {"name": "지하 감옥", "desc": "기본 던전. 무난한 시작", "weights": {}, "max_per_theme": 2,
		"modulate": Color(0.62, 0.6, 0.7), "hero_hp": 1.0, "elite": 1.0, "souls": 1.0},
	"volcano": {"name": "화산 요새", "desc": "용광로투성이. 용사 HP +15% · 영혼석 x1.3",
		"weights": {"forge": 5, "hall": 2, "shrine": 1, "library": 0, "flooded": 0, "crypt": 0, "prison": 1},
		"max_per_theme": 5, "modulate": Color(0.74, 0.56, 0.52), "hero_hp": 1.15, "elite": 1.0, "souls": 1.3},
	"necropolis": {"name": "망자의 묘역", "desc": "납골당과 수몰실. 엘리트 2배 · 영혼석 x1.5",
		"weights": {"crypt": 5, "flooded": 3, "prison": 2, "shrine": 1, "forge": 0, "library": 0, "hall": 1},
		"max_per_theme": 5, "modulate": Color(0.5, 0.58, 0.7), "hero_hp": 1.0, "elite": 2.0, "souls": 1.5},
}

## The core room (throne) and corridors.
const CORE_FLOOR := Color(0.45, 0.3, 0.42)
const CORRIDOR_FLOOR := Color(0.4, 0.37, 0.36)

## Room masks: rect, rounded (corners cut), cross (plus sign), oval.
const SHAPES := ["rect", "rect", "rounded", "cross", "oval"]

## Path cost per terrain: heroes walk around water and cross lava only on bridges if they can.
const TERRAIN_COST := {"water": 3.0, "lava": 8.0}

## Iron gates placed across corridors: how many per map, and how far from entrances/core
## (tiles). A closed gate blocks heroes (they reroute); the demon lord passes freely.
const GATES := {"count": 3, "min_spacing": 8, "keep_clear": 5}

## Floor pattern drawn over each theme's color: planks | flags | tiles | carpet | "" (stone texture).
const PATTERNS := {"library": "planks", "prison": "flags", "crypt": "tiles", "shrine": "carpet"}

## Altar: the demon lord and monsters within radius (tiles) heal `heal` HP every `interval` s
## during raids.
const ALTAR := {"radius": 2.5, "heal": 1, "interval": 2.0}
