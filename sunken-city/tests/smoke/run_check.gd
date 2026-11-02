extends SceneTree
## Smoke check for M1+M2: the zone is completable (pump materials reachable, and the
## map holds enough for every building, upgrade and request), swimming/walking/oxygen/
## faint/eel/tool rules hold, repairing the pump drains the water, and the village
## builds, upgrades, pays out requests, unlocks hard walls and pressure. Prints PASS or FAIL n.
##   godot --headless --path . -s res://tests/smoke/run_check.gd

const DT := 1.0 / 60.0
const O2: float = VillageData.TANK_MAX[0]
const OPEN_WATER := Vector2(50.5, 14.5) * Tuning.TILE  # open sea, clear of eel lanes and walls
const IN_TOWER := Vector2(18.5, 33.5) * Tuning.TILE    # a room inside the first tower: a floor right below, above the drained line
const DEEP := Vector2(60.5, 52.5) * Tuning.TILE        # crushing cavern below the pressure line

var _fails := 0


func _initialize() -> void:
	_check_map()
	_check_swim_and_walk()
	_check_oxygen_and_faint()
	_check_eel()
	_check_tool()
	_check_pump_and_drain()
	_check_village()
	_check_hard_wall_and_pressure()
	_check_rubble_regrows()
	_check_economy()
	print("PASS" if _fails == 0 else "FAIL %d" % _fails)
	quit(0 if _fails == 0 else 1)


## Rows are equal width, and a flood fill from the start (breakable tiles count as
## passable) reaches the pump and enough of every material.
func _check_map() -> void:
	for row in Zone1.MAP:
		_expect(row.length() == Zone1.MAP[0].length(), "map row width %d" % row.length())
	var w := World.new()
	var t := Tuning.TILE
	var start := Vector2i(w.start / t)
	var seen := {start: true}
	var queue := [start]
	while not queue.is_empty():
		var c: Vector2i = queue.pop_back()
		for d in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
			var n: Vector2i = c + d
			if not seen.has(n) and w.cell(n) != World.Cell.WALL:
				seen[n] = true
				queue.append(n)
	_expect(seen.has(Vector2i(w.pump / t)), "pump unreachable")
	var have := {"scrap": 0, "pipe": 0, "gear": 0}
	for p in w.pickups:
		if seen.has(Vector2i(p["pos"] / t)):
			have[p["kind"]] += 1
	for c in seen:
		if w.cell(c) == World.Cell.RUBBLE:
			have["scrap"] += 1
	_expect(World.missing(have).is_empty(), "not enough reachable materials: %s" % have)


func _check_swim_and_walk() -> void:
	var w := World.new()
	var d := Diver.new(OPEN_WATER)
	_expect(w.in_water(d.pos), "test spot should be underwater")
	var y0 := d.pos.y
	_run(d, w, Vector2.UP, 0.5)
	_expect(d.pos.y < y0 - 20, "swimming up did not rise")
	_expect(d.vel.length() <= Tuning.SWIM_MAX + 1, "swim speed over max")
	# above the surface in the start pocket: gravity puts the diver on the floor
	var dry := Diver.new(w.start + Vector2(0, -8))
	_run(dry, w, Vector2.ZERO, 1.0)
	_expect(dry.on_floor, "diver did not land in the dry start pocket")
	_expect(not w.solid_at(dry.pos), "diver ended inside a wall")


func _check_oxygen_and_faint() -> void:
	var w := World.new()
	var d := Diver.new(OPEN_WATER)
	d.inventory = {"scrap": 3, "pipe": 1, "gear": 1}
	var fainted := [false]
	d.fainted.connect(func(): fainted[0] = true)
	_run(d, w, Vector2.ZERO, 10.0)
	_expect(d.oxygen < O2 - 9, "oxygen did not drain underwater")
	for i in int(O2 / DT):
		d.step(w, Vector2.ZERO, false, false, DT)
		if fainted[0]:
			break
	_expect(fainted[0], "no faint when oxygen ran out")
	_expect(d.pos == w.start and d.oxygen == O2, "faint did not reset to start")
	_expect(d.inventory == {"scrap": 1, "pipe": 1, "gear": 1}, "faint should halve scrap only: %s" % d.inventory)
	# an air plant refills
	var a := Diver.new(w.air_plants[1])
	a.oxygen = 5.0
	a.step(w, Vector2.ZERO, false, false, DT)
	_expect(a.oxygen == O2, "air plant did not refill")


func _check_eel() -> void:
	var w := World.new()
	var e: Eel = w.eels[0]
	var d := Diver.new(e.pos)
	d.step(w, Vector2.ZERO, false, false, DT)
	var after_bite := d.oxygen
	_expect(after_bite < O2 - Tuning.EEL_BITE + 0.5, "eel did not bite")
	d.pos = e.pos
	d.step(w, Vector2.ZERO, false, false, DT)
	_expect(d.oxygen > after_bite - 1, "bitten twice inside the invulnerability window")


func _check_tool() -> void:
	var w := World.new()
	var rubble := Vector2i(-1, -1)
	for i in w.cells.size():
		if w.cells[i] == World.Cell.RUBBLE:
			rubble = Vector2i(i % w.width, i / w.width)
			break
	var before := w.pickups.size()
	_expect(w.hit(rubble), "rubble did not break in one hit")
	_expect(w.pickups.size() == before + 1, "rubble dropped no scrap")
	var cracked := Vector2i(-1, -1)
	for i in w.cells.size():
		if w.cells[i] == World.Cell.CRACKED:
			cracked = Vector2i(i % w.width, i / w.width)
			break
	_expect(not w.hit(cracked) and w.hit(cracked), "cracked wall should take exactly 2 hits")
	_expect(not w.hit(Vector2i(0, 0), 9), "plain wall broke")
	# the diver's tool hits the tile it faces, with a cooldown
	var w2 := World.new()
	var target := Vector2i(rubble.x, rubble.y)
	var d := Diver.new((Vector2(target) + Vector2(-0.5, 0.5)) * Tuning.TILE)
	d.facing = Vector2i.RIGHT
	d.step(w2, Vector2.ZERO, false, true, DT)
	_expect(w2.cell(target) == World.Cell.OPEN, "diver tool missed the faced tile")


## Collect everything, repair, drain: the surface reaches the drained row, eels left
## dry are gone, and a diver in the old water now falls and lands.
func _check_pump_and_drain() -> void:
	var w := World.new()
	var inv := {"scrap": 2, "pipe": 1, "gear": 1}
	_expect(not w.repair_pump(inv), "repaired without enough scrap")
	_expect(World.missing(inv) == {"scrap": 1}, "missing list wrong: %s" % World.missing(inv))
	inv["scrap"] = 3
	_expect(w.repair_pump(inv), "repair failed with full materials")
	_expect(inv == {"scrap": 0, "pipe": 0, "gear": 0}, "repair did not consume materials")
	var d := Diver.new(IN_TOWER)
	for i in int((Tuning.DRAIN_TIME + 1.0) / DT):
		w.step(DT)
		d.step(w, Vector2.ZERO, false, false, DT)
	_expect(is_equal_approx(w.surface_y, w.drained_y), "water did not drain to the target")
	_expect(not w.draining(), "still draining after DRAIN_TIME")
	_expect(not w.in_water(d.pos) and d.on_floor, "diver is not standing on dry ground after the drain")
	for e in w.eels:
		_expect(not e.alive or w.in_water(e.pos), "eel alive out of water")


func _check_village() -> void:
	var w := World.new()
	var v := w.village
	_expect(v.plots.size() == 3, "expected 3 building plots, got %d" % v.plots.size())
	var inv := {"scrap": 1, "pipe": 0, "gear": 0}
	_expect(not v.build(0, "workshop", inv) and inv["scrap"] == 1, "built without enough scrap")
	inv = {"scrap": 20, "pipe": 3, "gear": 3}
	_expect(v.build(0, "workshop", inv) and inv["scrap"] == 18, "workshop build/cost")
	_expect(not v.build(1, "workshop", inv), "second workshop allowed")
	_expect(not v.build(0, "lodge", inv), "built on an occupied plot")
	_expect(v.residents().is_empty(), "residents before the lodge")
	_expect(v.build(1, "lodge", inv) and v.residents().size() == 2, "lodge brings two residents")
	_expect(v.upgrade("pick", inv) and v.levels["pick"] == 1, "pick upgrade")
	_expect(v.upgrade_cost("pick").is_empty() and not v.upgrade("pick", inv), "pick should max at Lv1")
	_expect(v.upgrade("tank", inv) and v.upgrade("tank", inv) and v.levels["tank"] == 2, "tank to Lv2")
	var d := Diver.new(w.start)
	_expect(d.oxygen_max(w) == VillageData.TANK_MAX[2], "tank level does not raise oxygen max")
	var before := inv.duplicate()
	_expect(v.fulfil(0, inv), "resident request not fulfilled")
	_expect(inv["pipe"] == before["pipe"] - 1 and inv["scrap"] == before["scrap"] + 5, "request trade wrong: %s" % inv)
	_expect(not v.fulfil(0, inv), "request paid twice")
	var no_shop := World.new().village
	_expect(not no_shop.upgrade("suit", {"scrap": 9, "pipe": 9, "gear": 9}), "upgrade without a workshop")


func _check_hard_wall_and_pressure() -> void:
	var w := World.new()
	var plug := Vector2i(-1, -1)
	for i in w.cells.size():
		if w.cells[i] == World.Cell.HARD:
			plug = Vector2i(i % w.width, i / w.width)
			break
	_expect(w.cell(plug) == World.Cell.HARD, "deep plug should be a hard wall")
	for i in 5:
		w.hit(plug, 1)
	_expect(w.cell(plug) == World.Cell.HARD, "hard wall broke with the starter pick")
	_expect(not w.hit(plug, 2) and w.hit(plug, 2), "hard wall should take 2 hits with the upgraded pick")
	var deep := DEEP
	_expect(w.crushing(deep), "deep water should crush without the suit")
	var d := Diver.new(deep)
	_run(d, w, Vector2.ZERO, 1.0)
	_expect(d.oxygen < O2 - VillageData.PRESSURE_DRAIN + 0.5, "pressure did not speed up oxygen loss")
	w.village.levels["suit"] = 1
	_expect(not w.crushing(deep), "suit should stop the crushing")
	var g := Diver.new(w.gate)
	g.step(w, Vector2.ZERO, false, false, DT)
	_expect(w.gate_found, "reaching the floodgate did not register")


func _check_rubble_regrows() -> void:
	var w := World.new()
	var c := Vector2i(-1, -1)
	for i in w.cells.size():
		if w.cells[i] == World.Cell.RUBBLE:
			c = Vector2i(i % w.width, i / w.width)
			break
	w.hit(c)
	var near := (Vector2(c) + Vector2(0.5, 0.5)) * Tuning.TILE
	w.step(VillageData.RUBBLE_REGROW + 1, near)
	_expect(w.cell(c) == World.Cell.OPEN, "rubble regrew under the diver's nose")
	w.step(DT)
	_expect(w.cell(c) == World.Cell.RUBBLE, "rubble did not regrow once the diver left")


## Pipes and gears never regrow, so the map plus request rewards must cover every
## pipe/gear the pump, buildings, upgrades and requests ask for.
func _check_economy() -> void:
	var w := World.new()
	var have := {"pipe": 0, "gear": 0}
	for p in w.pickups:
		if have.has(p["kind"]):
			have[p["kind"]] += 1
	var need := {"pipe": 0, "gear": 0}
	var costs: Array = [Tuning.PUMP_COST]
	for b in VillageData.BUILDINGS.values():
		costs.append(b["cost"])
	for u in VillageData.UPGRADES.values():
		costs.append_array(u["costs"])
	for r in VillageData.RESIDENTS:
		costs.append(r["need"])
		for k in have:
			have[k] += r["reward"].get(k, 0)
	for c in costs:
		for k in need:
			need[k] += c.get(k, 0)
	for k in need:
		_expect(have[k] >= need[k], "%s: map+rewards give %d, everything costs %d" % [k, have[k], need[k]])


func _run(d: Diver, w: World, move: Vector2, seconds: float) -> void:
	for i in int(seconds / DT):
		d.step(w, move, false, false, DT)


func _expect(ok: bool, what: String) -> void:
	if not ok:
		_fails += 1
		print("FAIL: " + what)
