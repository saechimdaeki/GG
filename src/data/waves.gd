class_name Waves
## The ten waves of a run. heroes: [kind, count] groups spawned in order;
## interval: seconds between spawns; hp_mult: hero HP scaling for the wave;
## dmg_bonus: flat damage added to every hero this wave.

const WAVES := [
	{"heroes": [["warrior", 2]], "interval": 1.5, "hp_mult": 1.0, "dmg_bonus": 0},
	{"heroes": [["warrior", 3], ["archer", 1]], "interval": 1.5, "hp_mult": 1.0, "dmg_bonus": 0},
	{"heroes": [["warrior", 3], ["archer", 2], ["rogue", 1]], "interval": 1.4, "hp_mult": 1.1, "dmg_bonus": 0},
	{"heroes": [["warrior", 3], ["cleric", 1], ["archer", 2], ["rogue", 1]], "interval": 1.3, "hp_mult": 1.2, "dmg_bonus": 1},
	{"heroes": [["guardian", 1], ["warrior", 3], ["mage", 1], ["archer", 2]], "interval": 1.2, "hp_mult": 1.3, "dmg_bonus": 1},
	{"heroes": [["guardian", 1], ["warrior", 3], ["cleric", 1], ["rogue", 2], ["mage", 1]], "interval": 1.1, "hp_mult": 1.45, "dmg_bonus": 1},
	{"heroes": [["guardian", 2], ["warrior", 3], ["mage", 2], ["cleric", 1], ["archer", 2]], "interval": 1.0, "hp_mult": 1.6, "dmg_bonus": 2},
	{"heroes": [["rogue", 3], ["warrior", 4], ["archer", 3], ["cleric", 1], ["mage", 2]], "interval": 0.9, "hp_mult": 1.8, "dmg_bonus": 2},
	{"heroes": [["guardian", 2], ["warrior", 4], ["mage", 2], ["archer", 3], ["cleric", 2], ["rogue", 2]], "interval": 0.8, "hp_mult": 2.0, "dmg_bonus": 2},
	{"heroes": [["boss", 1], ["guardian", 2], ["warrior", 3], ["cleric", 1], ["mage", 1]], "interval": 1.0, "hp_mult": 1.5, "dmg_bonus": 1},
]
