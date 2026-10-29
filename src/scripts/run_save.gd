class_name RunSave
## Mid-run save: written automatically at the start of every build phase, cleared when the run
## ends. The title screen offers 이어하기 while it exists. The map is rebuilt from its seed,
## then the player's changes (walls, traps, gates, monsters, objects) are replayed on top.

## Overridable so tests do not touch the real file.
static var save_path := "user://run.cfg"
## Set by the title screen; Game reads and resets it in _ready.
static var load_next := false


static func exists() -> bool:
	return FileAccess.file_exists(save_path)


static func clear() -> void:
	if exists():
		DirAccess.remove_absolute(save_path)


static func save(game: Game) -> void:
	var g := game.grid
	var lord := game.lord
	var walls: Array[Vector2i] = []
	for y in DungeonGrid.H:
		for x in DungeonGrid.W:
			if g.is_wall(Vector2i(x, y)) and not g.was_wall(Vector2i(x, y)):
				walls.append(Vector2i(x, y))
	var monsters: Array = []
	for c: Vector2i in game.monster_cells():
		var m: Monster = game.monster_cells()[c]
		monsters.append([c, m.kind, m.level, m.xp])
	var objects: Array = []
	for c: Vector2i in game.object_cells():
		objects.append([c, (game.object_cells()[c] as BuildObject).kind])
	var gates: Array[bool] = []
	for gate in g.gates:
		gates.append(bool(gate.closed))
	var cfg := ConfigFile.new()
	var run := {
		"seed": g.seed_value, "biome": game.biome, "wave": game.wave, "gold": game.gold,
		"core_hp": game.core_hp, "core_max": game.core_max,
		"unlocked_traps": game.unlocked_traps, "monster_mult": game.monster_mult,
		"bought": game.bought(), "lord_level": game.lord_level, "lord_xp": game.lord_xp,
		"walls": walls, "traps": g.traps, "trap_bonus": g.trap_bonus, "gates": gates,
		"monsters": monsters, "objects": objects,
		"lord_max_health": lord.max_health, "melee_bonus": lord.melee_bonus,
		"max_bolts": lord.max_bolts, "dash_cooldown": lord.dash_cooldown,
		"weapon": lord.weapon_id, "skills": lord.skills,
	}
	for k: String in run:
		cfg.set_value("run", k, run[k])
	cfg.save(save_path)


## Saved run as a dictionary, or {} when there is none.
static func read() -> Dictionary:
	var cfg := ConfigFile.new()
	if cfg.load(save_path) != OK:
		return {}
	var out := {}
	for k in cfg.get_section_keys("run"):
		out[k] = cfg.get_value("run", k)
	return out


## Puts a saved run back into a freshly built game (its grid must use data.seed).
static func restore(game: Game, data: Dictionary) -> void:
	var g := game.grid
	game.wave = int(data.wave)
	game.gold = int(data.gold)
	game.core_hp = int(data.core_hp)
	game.core_max = int(data.core_max)
	game.unlocked_traps.assign(data.unlocked_traps)
	game.monster_mult = data.monster_mult
	game.set_bought(data.bought)
	game.lord_level = int(data.lord_level)
	game.lord_xp = int(data.lord_xp)
	g.apply_walls(data.walls)
	for c: Vector2i in data.traps:
		g.place_trap(c, data.traps[c])
	g.trap_bonus = int(data.trap_bonus)
	for i in mini(g.gates.size(), (data.gates as Array).size()):
		if data.gates[i] and not g.gates[i].closed:
			g.toggle_gate(i)
	for m: Array in data.monsters:
		var mon := game.place_monster(m[0], m[1])
		for k in int(m[2]) - 1:
			mon.level_up()
		mon.xp = int(m[3])
	for o: Array in data.objects:
		game.place_object(o[0], o[1])
	var lord := game.lord
	lord.max_health = int(data.lord_max_health)
	lord.health = lord.max_health
	lord.melee_bonus = int(data.melee_bonus)
	lord.max_bolts = int(data.max_bolts)
	lord.bolts = lord.max_bolts
	lord.dash_cooldown = float(data.dash_cooldown)
	lord.equip(String(data.weapon))
	lord.skills.assign(data.skills)
	g.refresh_preview()
