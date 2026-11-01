class_name World
extends RefCounted
## One zone's state: tiles, loose pickups, eels, the pump and the water surface.
## Pure logic — the game scene only draws it — so the smoke check can play it headless.

enum Cell { OPEN, WALL, CRACKED, RUBBLE }

const MATERIALS := ["scrap", "pipe", "gear"]

var width := 0
var height := 0
var cells: Array[int] = []
var damage := {}            # Vector2i -> hits taken
var pickups: Array = []     # {pos: Vector2, kind: String}
var eels: Array[Eel] = []
var air_plants: Array[Vector2] = []
var start := Vector2.ZERO
var pump := Vector2.ZERO
var pump_fixed := false
var surface_y := 0.0        # water surface in px; below it is water
var drained_y := 0.0
var _drain_from := 0.0
var _drain_t := -1.0        # < 0: not draining


func _init(map: Array = Zone1.MAP, surface_row := Zone1.SURFACE_ROW, drained_row := Zone1.DRAINED_ROW) -> void:
	height = map.size()
	width = map[0].length()
	surface_y = surface_row * Tuning.TILE
	drained_y = drained_row * Tuning.TILE
	for y in height:
		for x in width:
			var ch: String = map[y][x] if x < map[y].length() else "#"
			var center := Vector2(x + 0.5, y + 0.5) * Tuning.TILE
			match ch:
				"#": cells.append(Cell.WALL)
				"x": cells.append(Cell.CRACKED)
				"r": cells.append(Cell.RUBBLE)
				_: cells.append(Cell.OPEN)
			match ch:
				"S": start = center
				"P": pump = center
				"O": air_plants.append(center)
				"E": eels.append(Eel.new(center))
				"g": pickups.append({"pos": center, "kind": "gear"})
				"p": pickups.append({"pos": center, "kind": "pipe"})


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


## Hits a breakable tile. Returns true when it broke; rubble drops scrap.
func hit(c: Vector2i) -> bool:
	var kind := cell(c)
	if kind != Cell.CRACKED and kind != Cell.RUBBLE:
		return false
	var hp := Tuning.CRACKED_HP if kind == Cell.CRACKED else Tuning.RUBBLE_HP
	damage[c] = damage.get(c, 0) + 1
	if damage[c] < hp:
		return false
	cells[c.y * width + c.x] = Cell.OPEN
	damage.erase(c)
	if kind == Cell.RUBBLE:
		pickups.append({"pos": (Vector2(c) + Vector2(0.5, 0.5)) * Tuning.TILE, "kind": "scrap"})
	return true


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
func step(dt: float) -> void:
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
