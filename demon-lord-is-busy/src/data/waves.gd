class_name Waves
## The ten waves of a run. heroes: [kind, count] groups spawned in order;
## interval: seconds between spawns; hp_mult: hero HP scaling for the wave;
## dmg_bonus: flat damage added to every hero this wave.

## 2026-10-29: harder after "너무 쉽다" — ~1.6x heroes, steeper HP curve, faster spawns.
const WAVES := [
	{"heroes": [["warrior", 3], ["archer", 1]], "interval": 1.2, "hp_mult": 1.2, "dmg_bonus": 0},
	{"heroes": [["warrior", 4], ["archer", 2], ["rogue", 1]], "interval": 1.1, "hp_mult": 1.3, "dmg_bonus": 0},
	{"heroes": [["warrior", 4], ["archer", 3], ["rogue", 2]], "interval": 1.0, "hp_mult": 1.45, "dmg_bonus": 1},
	{"heroes": [["warrior", 5], ["cleric", 1], ["archer", 3], ["rogue", 2]], "interval": 0.95, "hp_mult": 1.6, "dmg_bonus": 1},
	{"heroes": [["guardian", 2], ["warrior", 4], ["mage", 2], ["archer", 3]], "interval": 0.9, "hp_mult": 1.8, "dmg_bonus": 1},
	{"heroes": [["guardian", 2], ["warrior", 5], ["cleric", 2], ["rogue", 3], ["mage", 2]], "interval": 0.85, "hp_mult": 2.0, "dmg_bonus": 2},
	{"heroes": [["guardian", 3], ["warrior", 5], ["mage", 3], ["cleric", 2], ["archer", 3]], "interval": 0.8, "hp_mult": 2.3, "dmg_bonus": 2},
	{"heroes": [["rogue", 5], ["warrior", 6], ["archer", 4], ["cleric", 2], ["mage", 3]], "interval": 0.7, "hp_mult": 2.6, "dmg_bonus": 2},
	{"heroes": [["guardian", 4], ["warrior", 6], ["mage", 3], ["archer", 4], ["cleric", 3], ["rogue", 3]], "interval": 0.65, "hp_mult": 3.0, "dmg_bonus": 3},
	{"heroes": [["boss", 1], ["guardian", 3], ["warrior", 5], ["cleric", 2], ["mage", 2], ["rogue", 2]], "interval": 0.8, "hp_mult": 2.4, "dmg_bonus": 2},
]
