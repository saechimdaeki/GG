class_name World
extends RefCounted
## One zone's state: tiles, loose pickups, eels, the pump, the water surface and the
## village that grows once it drains.
## Pure logic — the game scene only draws it — so the smoke check can play it headless.

enum Cell { OPEN, WALL, CRACKED, RUBBLE, HARD }

const MATERIALS := ["scrap", "pipe", "gear"]

var width := 0
var height := 0
var cells: Array[int] = []
var damage := {}            # Vector2i -> hits taken
var revision := 0           # bumps whenever a tile changes, so views know to rebuild
var pickups: Array = []     # {pos: Vector2, kind: String}
var eels: Array[Eel] = []
var air_plants: Array[Vector2] = []
var start := Vector2.ZERO
var pump := Vector2.ZERO
var pump_fixed := false
var gate := Vector2.ZERO
var gate_found := false
var village: Village
var pressure_y := 0.0
var _regrow := {}           # Vector2i -> seconds until broken rubble silts back
var surface_y := 0.0        # water surface in px; below it is water
var drained_y := 0.0
var _drain_from := 0.0
var _drain_t := -1.0        # < 0: not draining


func _init(map: Array = Zone1.MAP, surface_row := Zone1.SURFACE_ROW, drained_row := Zone1.DRAINED_ROW) -> void:
	height = map.size()
	width = map[0].length()
	surface_y = surface_row * Tuning.TILE
	drained_y = drained_row * Tuning.TILE
	pressure_y = Zone1.PRESSURE_ROW * Tuning.TILE
	var plot_positions: Array[Vector2] = []
	for y in height:
		for x in width:
			var ch: String = map[y][x] if x < map[y].length() else "#"
			var center := Vector2(x + 0.5, y + 0.5) * Tuning.TILE
			match ch:
				"#": cells.append(Cell.WALL)
				"x": cells.append(Cell.CRACKED)
				"r": cells.append(Cell.RUBBLE)
				"h": cells.append(Cell.HARD)
				_: cells.append(Cell.OPEN)
			match ch:
				"S": start = center
				"P": pump = center
				"O": air_plants.append(center)
				"E": eels.append(Eel.new(center))
				"g": pickups.append({"pos": center, "kind": "gear"})
				"p": pickups.append({"pos": center, "kind": "pipe"})
				"B": plot_positions.append(center)
				"G": gate = center
	village = Village.new(plot_positions)


func cell(c: Vector2i) -> int:
	if c.x < 0 or c.y < 0 or c.x >= width or c.y >= height:
		return Cell.WALL
	return cells[c.y * width + c.x]


func is_solid(c: Vector2i) -> bool:
	return cell(c) != Cell.OPEN


func solid_at(p: Vector2) -> bool:
	return is_solid(Vector2i(floori(p.x / Tuning.TILE), floori(p.y / Tuning.TILE)))


func in_water(p: Vector2) -> bool:
	return p.y > surface_y


## True when any tile overlaps the box (center, size).
func box_hits(center: Vector2, size: Vector2) -> bool:
	var a := center - size / 2
	var b := center + size / 2 - Vector2(0.01, 0.01)
	for ty in range(floori(a.y / Tuning.TILE), floori(b.y / Tuning.TILE) + 1):
		for tx in range(floori(a.x / Tuning.TILE), floori(b.x / Tuning.TILE) + 1):
			if is_solid(Vector2i(tx, ty)):
				return true
	return false


## Hits a breakable tile with the pick's power (1 + pick level). Hard walls need
## power 2. Returns true when it broke; rubble drops scrap and regrows later.
func hit(c: Vector2i, power := 1) -> bool:
	var kind := cell(c)
	var hp: int = {Cell.CRACKED: Tuning.CRACKED_HP, Cell.RUBBLE: Tuning.RUBBLE_HP, Cell.HARD: Tuning.HARD_HP}.get(kind, 0)
	if hp == 0 or (kind == Cell.HARD and power < 2):
		return false
	damage[c] = damage.get(c, 0) + power
	if damage[c] < hp:
		return false
	cells[c.y * width + c.x] = Cell.OPEN
	damage.erase(c)
	revision += 1
	if kind == Cell.RUBBLE:
		pickups.append({"pos": (Vector2(c) + Vector2(0.5, 0.5)) * Tuning.TILE, "kind": "scrap"})
		_regrow[c] = VillageData.RUBBLE_REGROW
	return true


## Where the diver wakes up after fainting or returning: the start pocket, then the pump once the town exists.
func respawn() -> Vector2:
	return pump + Vector2(-Tuning.TILE, 0) if pump_fixed else start


func crushing(p: Vector2) -> bool:
	return in_water(p) and p.y > pressure_y and village.levels["suit"] == 0


## Materials still missing for the pump repair, as {kind: count}.
static func missing(inventory: Dictionary) -> Dictionary:
	var out := {}
	for k in Tuning.PUMP_COST:
		var short: int = Tuning.PUMP_COST[k] - inventory.get(k, 0)
		if short > 0:
			out[k] = short
	return out


## Pays the cost and starts draining. Returns false if something is missing.
func repair_pump(inventory: Dictionary) -> bool:
	if pump_fixed or not missing(inventory).is_empty():
		return false
	for k in Tuning.PUMP_COST:
		inventory[k] -= Tuning.PUMP_COST[k]
	pump_fixed = true
	_drain_from = surface_y
	_drain_t = 0.0
	return true


func draining() -> bool:
	return _drain_t >= 0.0


## Lowers the water while draining; eels left above the surface die and drop scrap.
## Broken rubble regrows unless the diver (at diver_pos) is close enough to see it.
func step(dt: float, diver_pos := Vector2(-1000, -1000)) -> void:
	for c in _regrow.keys():
		_regrow[c] -= dt
		var center := (Vector2(c) + Vector2(0.5, 0.5)) * Tuning.TILE
		var blocked := center.distance_to(diver_pos) < Tuning.TILE * 6 \
			or eels.any(func(e): return e.alive and center.distance_to(e.pos) < Tuning.TILE * 1.5)
		if _regrow[c] <= 0 and not blocked:
			cells[c.y * width + c.x] = Cell.RUBBLE
			_regrow.erase(c)
			revision += 1
	if draining():
		_drain_t = minf(_drain_t + dt, Tuning.DRAIN_TIME)
		surface_y = lerpf(_drain_from, drained_y, smoothstep(0.0, 1.0, _drain_t / Tuning.DRAIN_TIME))
		if _drain_t >= Tuning.DRAIN_TIME:
			_drain_t = -1.0
	for e in eels:
		if e.alive and not in_water(e.pos):
			e.alive = false
			pickups.append({"pos": e.pos, "kind": "scrap"})
		elif e.alive:
			e.step(self, dt)
