class_name Waves
## The ten waves of a run. heroes: [kind, count] groups spawned in order;
## interval: seconds between spawns; hp_mult: hero HP scaling for the wave.

const WAVES := [
	{"heroes": [["warrior", 2]], "interval": 1.5, "hp_mult": 1.0},
	{"heroes": [["warrior", 3]], "interval": 1.5, "hp_mult": 1.0},
	{"heroes": [["warrior", 3], ["archer", 1]], "interval": 1.4, "hp_mult": 1.1},
	{"heroes": [["warrior", 4], ["archer", 2]], "interval": 1.3, "hp_mult": 1.2},
	{"heroes": [["warrior", 5], ["archer", 2]], "interval": 1.2, "hp_mult": 1.3},
	{"heroes": [["warrior", 5], ["archer", 3]], "interval": 1.1, "hp_mult": 1.45},
	{"heroes": [["warrior", 6], ["archer", 4]], "interval": 1.0, "hp_mult": 1.6},
	{"heroes": [["warrior", 7], ["archer", 4]], "interval": 0.9, "hp_mult": 1.8},
	{"heroes": [["warrior", 8], ["archer", 5]], "interval": 0.8, "hp_mult": 2.0},
	{"heroes": [["boss", 1], ["warrior", 4], ["archer", 2]], "interval": 1.0, "hp_mult": 1.5},
]
