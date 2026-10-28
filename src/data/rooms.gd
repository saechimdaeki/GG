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
