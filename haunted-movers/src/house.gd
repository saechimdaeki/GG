extends Node3D
## The haunted house: 2 floors, a narrow inner door, a ramp staircase and a roof.
## Front door faces -z, toward the truck. Interior: x -6..6, z 0..10, floors at y 0 and 3.
## Collision is the same boxes as always; looks come from spatial_env_surface (wallpaper,
## wainscot, siding, planks, grass) plus mesh-only trim: windows, door frames, trees.

const Models = preload("res://src/models.gd")

var wall := Models.surface(1, Color("#8c6f62"), Color("#4a3328"))
var floor_wood := Models.surface(0, Color("#6e4a32"), Color.BLACK)
var grass := Models.surface(2, Color("#3d5233"), Color("#56663a"))
var ramp_wood := Models.grain(Color("#6b4a33"))
var roof := Models.matte(Color("#2b2228"))
var trim := Models.grain(Color("#3a2618"))
const BULB_ENERGY := 0.55
var _bulbs: Array[OmniLight3D] = []


func _ready() -> void:
	_box(Vector3(60, 0.2, 60), Vector3(0, -0.1, 0), grass)                  # yard
	_box(Vector3(11.8, 0.02, 9.8), Vector3(0, 0.01, 5), floor_wood)          # wooden floor inside

	# Outer walls, 2 storeys. Front (z=0) has a 1.6 m x 2.2 m door.
	_box(Vector3(5.2, 6, 0.2), Vector3(-3.4, 3, 0), wall)
	_box(Vector3(5.2, 6, 0.2), Vector3(3.4, 3, 0), wall)
	_box(Vector3(1.6, 3.8, 0.2), Vector3(0, 4.1, 0), wall)
	_box(Vector3(12.2, 6, 0.2), Vector3(0, 3, 10), wall)
	_box(Vector3(0.2, 6, 10), Vector3(-6, 3, 5), wall)
	_box(Vector3(0.2, 6, 10), Vector3(6, 3, 5), wall)
	_box(Vector3(12.6, 0.2, 10.6), Vector3(0, 6.1, 5), roof)

	# Ground-floor inner wall at z=5.5 with a narrow 1.2 m door (sofa must be turned).
	_box(Vector3(2.4, 3, 0.2), Vector3(-4.8, 1.5, 5.5), wall)
	_box(Vector3(8.4, 3, 0.2), Vector3(1.8, 1.5, 5.5), wall)
	_box(Vector3(1.2, 0.8, 0.2), Vector3(-3.0, 2.6, 5.5), wall)

	# Second floor slab, leaving a stairwell at x 4.2..6, z 0..5.5.
	_box(Vector3(10.2, 0.2, 10), Vector3(-0.9, 2.9, 5), floor_wood)
	_box(Vector3(1.8, 0.2, 4.5), Vector3(5.1, 2.9, 7.75), floor_wood)

	# Stair ramp along the right wall: y 0 at z 0.5 up to y 3 at z 5.4 (~31°).
	var run := 4.9
	var rise := 3.0
	var ramp := _box(Vector3(1.6, 0.2, sqrt(run * run + rise * rise)), Vector3(5.1, 1.4, 0.5 + run / 2), ramp_wood)
	ramp.rotation.x = -atan2(rise, run)
	_steps(Vector3(5.1, 0, 0.5), run, rise)

	_door_frame(Vector3(0, 0, 0), 1.6, 2.2, Vector3.RIGHT)
	_door_frame(Vector3(-3.0, 0, 5.5), 1.2, 2.2, Vector3.RIGHT)
	# Windows: front, back and sides, both storeys (glass glows with the dusk outside).
	for y in [1.6, 4.6]:
		for x in [-3.6, 3.0]:
			_window(Vector3(x, y, 0), Vector3.RIGHT)
			_window(Vector3(x, y, 10), Vector3.RIGHT)
		for z in [2.5, 7.5]:
			_window(Vector3(-6, y, z), Vector3.BACK)
	_window(Vector3(6, 4.6, 2.5), Vector3.BACK)
	_window(Vector3(6, 1.6, 7.5), Vector3.BACK)

	for t in [Vector3(-9, 0, -2), Vector3(-11, 0, 6), Vector3(9.5, 0, 4), Vector3(12, 0, -6),
			Vector3(-8, 0, 13), Vector3(7, 0, 14), Vector3(-14, 0, -8), Vector3(15, 0, 10)]:
		_tree(t)

	for p in [Vector3(0, 2.5, 3), Vector3(-3, 2.5, 8), Vector3(3, 2.5, 8), Vector3(0, 5.5, 5)]:
		var lamp := OmniLight3D.new()
		lamp.position = p
		lamp.omni_range = 8.0
		lamp.light_color = Color("#ffc58a")
		lamp.light_energy = BULB_ENERGY
		lamp.shadow_enabled = true
		add_child(lamp)
		_bulbs.append(lamp)
		# a bare bulb on a cord, so the light has a source
		var bulb := StandardMaterial3D.new()
		bulb.emission_enabled = true
		bulb.emission = Color("#ffd9a0")
		bulb.emission_energy_multiplier = 3.0
		var b := Models.cylinder(self, 0.06, 0.06, 0.12, p + Vector3(0, 0.15, 0), bulb)
		b.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		Models.cylinder(self, 0.01, 0.01, 0.3, p + Vector3(0, 0.36, 0), roof)


## Old wiring: bulbs hum, and now and then one browns out for a moment.
## Local only (each machine flickers on its own clock) — cosmetic, so not synced.
func _process(_delta: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0
	for i in _bulbs.size():
		var hum := 0.9 + 0.1 * sin(t * 13.0 + i) * sin(t * 7.3 + i * 2.0)
		var out := 0.15 if sin(t * 0.7 + i * 1.9) > 0.97 else 1.0
		_bulbs[i].light_energy = BULB_ENERGY * hum * out


## Visual treads on the ramp (collision stays the smooth ramp).
func _steps(foot: Vector3, run: float, rise: float) -> void:
	var n := 14
	for i in n:
		var t := (i + 0.5) / n
		var m := Models.box(self, Vector3(1.6, 0.05, run / n), Vector3(foot.x, rise * t + 0.03, foot.z + run * t), trim)
		m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## Frame around a doorway in a wall running along `along` (RIGHT = wall along x).
func _door_frame(base: Vector3, width: float, height: float, along: Vector3) -> void:
	var thick := Vector3(0.1, 0.1, 0.3) if along == Vector3.RIGHT else Vector3(0.3, 0.1, 0.1)
	for s in [-1, 1]:
		var post := Vector3(thick.x, height, thick.z)
		Models.box(self, post, base + along * (s * (width / 2 + 0.05)) + Vector3(0, height / 2, 0), trim)
	Models.box(self, Vector3(width + 0.2, 0.12, thick.z) if along == Vector3.RIGHT else Vector3(thick.x, 0.12, width + 0.2),
		base + Vector3(0, height + 0.06, 0), trim)


## A mesh-only window in the wall at `c`; `along` is the wall direction.
func _window(c: Vector3, along: Vector3) -> void:
	var w := 1.1
	var h := 1.3
	var on_x := along == Vector3.RIGHT
	var frame_size := Vector3(w + 0.16, h + 0.16, 0.26) if on_x else Vector3(0.26, h + 0.16, w + 0.16)
	Models.box(self, frame_size, c, trim)
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color("#1b1626")
	glass.emission_enabled = true
	glass.emission = Color("#d9785a").lerp(Color("#5b4a8a"), clampf((c.y - 1.0) / 4.0, 0.0, 1.0))
	glass.emission_energy_multiplier = 0.7
	glass.roughness = 0.1
	Models.box(self, Vector3(w, h, 0.28) if on_x else Vector3(0.28, h, w), c, glass)
	Models.box(self, Vector3(w, 0.05, 0.3) if on_x else Vector3(0.3, 0.05, w), c, trim)      # mullions
	Models.box(self, Vector3(0.05, h, 0.3) if on_x else Vector3(0.3, h, 0.05), c, trim)


func _tree(at: Vector3) -> void:
	var bark := Models.grain(Color("#3b2a20"))
	var leaves := Models.matte(Color("#2c3b2c"))
	var s := 0.8 + fmod(absf(at.x * 0.37 + at.z * 0.21), 0.6)  # deterministic size variation
	Models.cylinder(self, 0.12 * s, 0.18 * s, 1.6 * s, at + Vector3(0, 0.8 * s, 0), bark)
	for i in 3:
		Models.cylinder(self, 0.0, (1.3 - i * 0.3) * s, 1.4 * s, at + Vector3(0, (1.8 + i * 0.8) * s, 0), leaves)


func _box(size: Vector3, pos: Vector3, mat: Material) -> StaticBody3D:
	var sb := StaticBody3D.new()
	sb.position = pos
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	sb.add_child(col)
	Models.box(sb, size, Vector3.ZERO, mat)
	add_child(sb)
	return sb
