extends Node3D
## The mansion from outside: a closed, two-storey shell with a porch, a gable roof, dark
## windows and a side fire door. Its inside is interior.gd (the doors there move you in).
## Front faces -z, toward the truck. Footprint: x -9..9, z 0..14. Collision is one solid box.

const Models = preload("res://src/models.gd")
const Tuning = preload("res://src/tuning.gd")

const W := 18.0
const D := 14.0
const H := 7.5
const PORCH_ENERGY := 1.2

var grass := Models.pbr("leafy_grass", Color(0.55, 0.6, 0.5), 3.0)
var siding := Models.pbr("exterior_wall_cladding", Color(0.6, 0.62, 0.6), 3.0)
var roof := Models.pbr("grey_roof_tiles", Color(0.5, 0.5, 0.55), 2.0)
var trim := Models.grain(Color("#2a2420"))
var stone := Models.pbr("castle_brick_07", Color(0.55, 0.52, 0.5), 2.0)
var _shutter := Models.matte(Color("#1e2a24"))
var _dark_glass := Models.matte(Color("#0c0c12"))
var _turret_glass := _lit_glass()
var _porch := OmniLight3D.new()
var _porch_mat := StandardMaterial3D.new()


func _ready() -> void:
	_box(Vector3(200, 0.2, 200), Vector3(0, -0.1, 0), grass)                  # yard
	_box(Vector3(W, H, D), Vector3(0, H / 2, D / 2), siding)                  # the shell
	_trimmings()
	# Gable roof: two slabs meeting over the middle (mesh only).
	var rise := 3.2
	var half := D / 2 + 0.6
	var slope := sqrt(half * half + rise * rise)
	for sz in [-1, 1]:
		var r := Models.box(self, Vector3(W + 1.0, 0.25, slope), Vector3(0, H + rise / 2, D / 2 + sz * half / 2), roof)
		r.rotation.x = sz * atan2(rise, half)
	for sx in [-1, 1]:  # gable ends
		var g := Models.box(self, Vector3(0.2, rise, D), Vector3(sx * (W / 2 - 0.1), H + rise / 2 - 0.4, D / 2), siding)
		g.scale = Vector3(1, 1, 0.6)
	Models.box(self, Vector3(W + 0.3, 0.3, D + 0.3), Vector3(0, H, D / 2), trim)   # eaves band
	# Chimney.
	_box(Vector3(1.0, 4.0, 1.0), Vector3(5.5, H + 2.0, 9.0), Models.matte(Color("#4a2e26")))
	# Porch: pillars, roof, steps (steps are mesh only; the door is at ground level).
	for sx in [-1, 1]:
		_box(Vector3(0.3, 3.2, 0.3), Vector3(sx * 2.6, 1.6, -2.2), trim)
	Models.box(self, Vector3(6.0, 0.25, 2.6), Vector3(0, 3.3, -1.2), roof)
	Models.box(self, Vector3(4.0, 0.06, 2.2), Vector3(0, 0.03, -1.1), Models.grain(Color("#3a2c22")))
	# Windows on every side, both storeys; a few have a faint glow somebody left on.
	var n := 0
	for y in [1.8, 5.0]:
		for x in [-6.6, -3.8, 3.8, 6.6]:
			_window(Vector3(x, y, -0.02), Vector3.RIGHT, n % 5 == 2)
			_window(Vector3(x, y, D + 0.02), Vector3.RIGHT, false)
			n += 1
		for z in [3.0, 7.0, 11.0]:
			_window(Vector3(-W / 2 - 0.02, y, z), Vector3.BACK, n % 4 == 1)
			if not (y < 3.0 and z > 9.0):  # leave room for the fire door
				_window(Vector3(W / 2 + 0.02, y, z), Vector3.BACK, false)
			n += 1
	_window(Vector3(0, 5.0, -0.02), Vector3.RIGHT, true)  # over the door
	# Gravel path from the truck to the porch.
	Models.box(self, Vector3(2.2, 0.02, 12.0), Vector3(0, 0.01, -7.0), Models.pbr("gravel", Color(0.7, 0.7, 0.7), 1.5))
	# Porch lantern: dies with the blackout.
	_porch_mat.albedo_color = Color("#2a241e")
	_porch_mat.emission_enabled = true
	_porch_mat.emission = Color("#ffc880")
	_porch_mat.emission_energy_multiplier = 3.0
	Models.box(self, Vector3(0.2, 0.3, 0.2), Vector3(1.0, 2.7, -0.15), _porch_mat)
	_porch.position = Vector3(1.0, 2.6, -0.6)
	_porch.light_color = Color("#ffc880")
	_porch.light_energy = PORCH_ENERGY
	_porch.omni_range = 7.0
	_porch.shadow_enabled = true
	_porch.light_volumetric_fog_energy = 2.0
	add_child(_porch)
	# Dead trees and a crooked fence around the lot.
	for t in [Vector3(-14, 0, -2), Vector3(-16, 0, 8), Vector3(14, 0, 4), Vector3(17, 0, -6),
			Vector3(-12, 0, 18), Vector3(11, 0, 20), Vector3(-20, 0, -10), Vector3(21, 0, 14), Vector3(-9, 0, -19), Vector3(10, 0, -21)]:
		_tree(t)
	for i in 16:
		var x := -20.0 + i * 2.6
		if absf(x) > 3.5:
			var post := Models.box(self, Vector3(0.12, 1.1, 0.12), Vector3(x, 0.55, -22.0), trim)
			post.rotation.z = sin(i * 2.7) * 0.12


## Breaks up the box: stone foundation, corner boards and a storey band, a round corner
## turret with a witch-hat roof, two roof dormers, porch steps and a railing.
func _trimmings() -> void:
	Models.box(self, Vector3(W + 0.3, 0.8, D + 0.3), Vector3(0, 0.4, D / 2), stone)        # foundation
	Models.box(self, Vector3(W + 0.25, 0.22, D + 0.25), Vector3(0, 3.6, D / 2), trim)     # storey band
	for sx in [-1, 1]:
		for z in [0.0, D]:
			Models.box(self, Vector3(0.3, H, 0.3), Vector3(sx * W / 2, H / 2, z), trim)  # corner boards
	# Turret on the front-right corner (solid, so you can't walk through it).
	var tc := Vector3(W / 2 - 0.4, 0, 0.4)
	var r := 2.1
	var th := H + 1.6
	var body := StaticBody3D.new()
	body.position = tc + Vector3(0, th / 2, 0)
	var col := CollisionShape3D.new()
	var cyl := CylinderShape3D.new()
	cyl.radius = r
	cyl.height = th
	col.shape = cyl
	body.add_child(col)
	add_child(body)
	Models.cylinder(self, r, r, th, tc + Vector3(0, th / 2, 0), siding)
	Models.cylinder(self, r + 0.15, r + 0.15, 0.8, tc + Vector3(0, 0.4, 0), stone)
	Models.cylinder(self, r + 0.12, r + 0.12, 0.2, tc + Vector3(0, 3.6, 0), trim)
	Models.cylinder(self, 0.0, r + 0.5, 4.2, tc + Vector3(0, th + 2.1, 0), roof)          # witch-hat roof
	Models.cylinder(self, 0.04, 0.04, 1.0, tc + Vector3(0, th + 4.6, 0), Models.metal())  # finial
	for i in 4:  # turret windows facing out
		var a := -PI / 2 - 0.9 + i * 0.6
		var win := Models.box(self, Vector3(0.7, 1.3, 0.1), tc + Vector3(cos(a) * r, 5.0, sin(a) * r), _turret_glass if i == 1 else _dark_glass)
		win.rotation.y = -a + PI / 2
	# Dormers on the front slope.
	for sx in [-1, 1]:
		var dc := Vector3(sx * 4.5, H + 1.1, 1.6)
		Models.box(self, Vector3(1.7, 1.6, 1.8), dc, siding)
		Models.box(self, Vector3(0.8, 0.9, 0.1), dc + Vector3(0, 0, -0.92), _dark_glass)
		for sz in [-1, 1]:
			var dr := Models.box(self, Vector3(0.15, 0.12, 2.0), dc + Vector3(sz * 0.55, 1.05, 0), roof)
			dr.scale = Vector3(8.0, 1, 1)
			dr.rotation.z = sz * -0.6
	# Porch steps and railing (railing leaves the middle open for the door).
	Models.box(self, Vector3(2.6, 0.12, 0.4), Vector3(0, 0.06, -2.6), trim)  # porch step (mesh only)
	for sx in [-1, 1]:
		Models.box(self, Vector3(1.6, 0.08, 0.1), Vector3(sx * 1.85, 1.0, -2.3), trim)       # rail
		for k in 6:
			Models.box(self, Vector3(0.05, 0.9, 0.05), Vector3(sx * (1.15 + k * 0.28), 0.5, -2.3), trim)


## Porch lantern flickers and goes out at the blackout (derived from the synced clock).
func _process(_delta: float) -> void:
	var run: Dictionary = get_parent().run if get_parent() and "run" in get_parent() else {}
	var cfg: Dictionary = Tuning.data.run
	var dark: bool = run.get("phase", "") == "day" and cfg.day_seconds - run.time_left >= cfg.blackout_after
	var t := Time.get_ticks_msec() / 1000.0
	_porch.visible = not dark
	_porch_mat.emission_enabled = not dark
	_porch.light_energy = PORCH_ENERGY * (0.15 if sin(t * 0.8) > 0.97 else 1.0)


## A mesh-only window on the shell at `c`; `along` is the wall direction.
func _window(c: Vector3, along: Vector3, lit: bool) -> void:
	var w := 1.2
	var h := 1.6
	var on_x := along == Vector3.RIGHT
	Models.box(self, Vector3(w + 0.2, h + 0.2, 0.12) if on_x else Vector3(0.12, h + 0.2, w + 0.2), c, trim)
	if on_x and c.z < 0.0:  # louvred shutters on the front windows, one hanging crooked
		for sx in [-1, 1]:
			var sh := Models.box(self, Vector3(0.42, h + 0.1, 0.06), c + Vector3(sx * (w / 2 + 0.33), 0, -0.04), _shutter)
			sh.rotation.z = 0.08 if (sx > 0 and int(c.x) % 3 == 0) else 0.0
			for k in 5:
				Models.box(self, Vector3(0.36, 0.03, 0.03), c + Vector3(sx * (w / 2 + 0.33), -h / 2 + 0.25 + k * 0.3, -0.08), trim)
		Models.box(self, Vector3(w + 0.4, 0.1, 0.22), c + Vector3(0, -h / 2 - 0.12, -0.08), trim)  # sill
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color("#0c0c12")
	glass.roughness = 0.1
	glass.metallic = 0.3
	if lit:
		glass.emission_enabled = true
		glass.emission = Color("#c8783a")
		glass.emission_energy_multiplier = 0.6
	Models.box(self, Vector3(w, h, 0.14) if on_x else Vector3(0.14, h, w), c, glass)
	Models.box(self, Vector3(w, 0.05, 0.16) if on_x else Vector3(0.16, 0.05, w), c, trim)
	Models.box(self, Vector3(0.05, h, 0.16) if on_x else Vector3(0.16, h, 0.05), c, trim)


static func _lit_glass() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color("#0c0c12")
	m.emission_enabled = true
	m.emission = Color("#c8783a")
	m.emission_energy_multiplier = 0.8
	return m


func _tree(at: Vector3) -> void:
	var bark := Models.grain(Color("#2a2420"))
	var s := 0.9 + fmod(absf(at.x * 0.37 + at.z * 0.21), 0.6)  # deterministic size variation
	Models.cylinder(self, 0.1 * s, 0.22 * s, 4.0 * s, at + Vector3(0, 2.0 * s, 0), bark)
	for i in 4:  # bare branches
		var b := Models.cylinder(self, 0.02 * s, 0.07 * s, 1.6 * s, at + Vector3(0, (2.4 + i * 0.5) * s, 0), bark)
		b.rotation = Vector3(0.9, i * 1.7 + at.x, 0)
		b.position += Vector3(sin(i * 1.7 + at.x), 0, cos(i * 1.7 + at.x)) * 0.5 * s


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
