extends Node3D
## Truck bed (open at the house side) and the depart lever. Pure geometry: main.gd counts
## the scrap on the bed (`truck_value`).

const Tuning = preload("res://src/tuning.gd")
const Models = preload("res://src/models.gd")

var bed := AABB()


var _panel := _enamel(Color("#e8e0cc"))
var _paint := _enamel(Color("#d0612a"))
var _steel := Models.pbr("metal_plate", Color(0.7, 0.7, 0.72), 1.0)
var _deck := Models.pbr("dark_wooden_planks", Color(0.8, 0.7, 0.6), 1.5)
var _tire := Models.matte(Color("#141416"))
var _chrome := Models.metal()


static func _enamel(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.roughness = 0.45
	m.metallic = 0.15
	return m


func _ready() -> void:
	var t: Dictionary = Tuning.data.truck
	bed = AABB(Vector3(t.bed_min[0], t.bed_min[1], t.bed_min[2]), Vector3(t.bed_size[0], t.bed_size[1], t.bed_size[2]))
	var c := bed.get_center()
	var s := bed.size
	_wall(Vector3(0.2, s.y, s.z), Vector3(bed.position.x - 0.1, c.y, c.z), _panel)   # left
	_wall(Vector3(0.2, s.y, s.z), Vector3(bed.end.x + 0.1, c.y, c.z), _panel)        # right
	_wall(Vector3(s.x + 0.4, s.y, 0.2), Vector3(c.x, c.y, bed.position.z - 0.1), _panel)  # cab side
	_dress(c, s)
	_lever()


## The depart lever on a post by the open back: a steel box, a long lever, a red knob, a sign.
func _lever() -> void:
	var button := _wall(Vector3(0.35, 0.5, 0.25), Vector3(bed.end.x + 0.55, 1.05, bed.end.z + 0.3), _steel)
	button.name = "DepartButton"
	Models.box(button, Vector3(0.08, 1.05, 0.08), Vector3(0, -0.78, 0), _steel)      # post
	var arm := Models.cylinder(button, 0.025, 0.025, 0.55, Vector3(0, 0.3, -0.12), _chrome)
	arm.rotation.x = -0.5
	var knob := StandardMaterial3D.new()
	knob.albedo_color = Color("#d0302a")
	knob.emission_enabled = true
	knob.emission = Color("#ff3020")
	knob.emission_energy_multiplier = 1.5
	var k := MeshInstance3D.new()
	var sm := SphereMesh.new()
	sm.radius = 0.07
	sm.height = 0.14
	k.mesh = sm
	k.material_override = knob
	k.position = Vector3(0, 0.54, -0.26)
	button.add_child(k)
	var sign := Label3D.new()
	sign.text = "출발 레버"
	sign.font_size = 40
	sign.outline_size = 8
	sign.modulate = Color("#f0e2b6")
	sign.position = Vector3(0, 0.0, -0.13)
	sign.rotation.y = PI
	button.add_child(sign)
	var glow := OmniLight3D.new()
	glow.light_color = Color("#ff4030")
	glow.light_energy = 0.6
	glow.omni_range = 2.0
	glow.position = Vector3(0, 0.5, -0.3)
	button.add_child(glow)


## Mesh-only: a box van. Ribbed cargo box with rounded roof edges and an orange stripe, wooden
## deck with tie-down rails and a hazard edge, a cab with a sloped hood, raked windscreen and
## real headlight beams, wheels in arches, mirrors, tail lights and the company name.
func _dress(c: Vector3, s: Vector3) -> void:
	var top := bed.end.y
	var back := bed.end.z            # open end, toward the house
	var front := bed.position.z      # cab side
	# Cargo box: roof, rounded roof edges, stripe, inner ribs and rails, deck.
	Models.box(self, Vector3(s.x + 0.4, 0.1, s.z + 0.2), Vector3(c.x, top + 0.05, c.z), _panel)
	for sx in [-1, 1]:
		var edge := Models.cylinder(self, 0.12, 0.12, s.z + 0.2, Vector3(sx * (s.x / 2 + 0.1), top, c.z), _panel)
		edge.rotation.x = PI / 2
		Models.box(self, Vector3(0.03, 0.35, s.z + 0.15), Vector3(sx * (s.x / 2 + 0.215), top - 0.45, c.z), _paint)  # stripe
		Models.box(self, Vector3(0.03, 0.08, s.z + 0.15), Vector3(sx * (s.x / 2 + 0.215), top - 0.75, c.z), _paint)
		for k in 7:  # ribs inside
			Models.box(self, Vector3(0.05, s.y - 0.2, 0.06), Vector3(sx * (s.x / 2 - 0.03), c.y, front + 0.3 + k * (s.z - 0.6) / 6.0), _steel)
		for y in [0.5, 1.3]:  # tie-down rails
			Models.box(self, Vector3(0.04, 0.05, s.z - 0.2), Vector3(sx * (s.x / 2 - 0.06), y, c.z), _chrome)
		var post := Models.box(self, Vector3(0.12, s.y + 0.1, 0.12), Vector3(sx * (s.x / 2 + 0.12), c.y, back + 0.04), _steel)  # back corner posts
		post.name = "Post"
	Models.box(self, Vector3(s.x, 0.06, s.z), Vector3(c.x, 0.03, c.z), _deck)                          # wooden deck
	var hazard := StandardMaterial3D.new()
	hazard.albedo_color = Color("#e8b020")
	for k in 8:  # yellow/black hazard edge at the open back
		var m: Material = hazard if k % 2 == 0 else _tire
		Models.box(self, Vector3(s.x / 8.0, 0.065, 0.18), Vector3(bed.position.x + (k + 0.5) * s.x / 8.0, 0.035, back - 0.09), m)
	var roller := Models.cylinder(self, 0.16, 0.16, s.x + 0.2, Vector3(c.x, top - 0.16, back - 0.05), _steel)  # rolled-up shutter
	roller.rotation.z = PI / 2
	# Chassis under the box.
	Models.box(self, Vector3(s.x - 0.4, 0.3, s.z + 3.0), Vector3(c.x, -0.05, c.z - 1.3), _tire)
	# Cab.
	var cab_z := front - 1.25
	Models.box(self, Vector3(s.x + 0.2, 1.3, 2.2), Vector3(c.x, 0.95, cab_z), _paint)                  # lower body
	var upper := Models.box(self, Vector3(s.x + 0.1, 0.95, 1.4), Vector3(c.x, 2.05, cab_z + 0.35), _paint)
	upper.name = "CabUpper"
	for sx in [-1, 1]:
		var re := Models.cylinder(self, 0.1, 0.1, 1.4, Vector3(sx * (s.x / 2 + 0.0), 2.5, cab_z + 0.35), _paint)
		re.rotation.x = PI / 2
	var hood := Models.box(self, Vector3(s.x + 0.15, 0.12, 0.9), Vector3(c.x, 1.62, cab_z - 0.7), _paint)
	hood.rotation.x = -0.12
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color("#1a2630")
	glass.roughness = 0.05
	glass.metallic = 0.5
	var ws := Models.box(self, Vector3(s.x - 0.15, 0.85, 0.05), Vector3(c.x, 2.05, cab_z - 0.38), glass)
	ws.rotation.x = 0.25  # raked windscreen
	Models.box(self, Vector3(s.x - 0.3, 0.42, 0.06), Vector3(c.x, 1.05, cab_z - 1.12), _steel)        # grille
	for k in 5:
		Models.box(self, Vector3(s.x - 0.35, 0.03, 0.08), Vector3(c.x, 0.88 + k * 0.08, cab_z - 1.14), _chrome)
	Models.box(self, Vector3(s.x + 0.35, 0.22, 0.25), Vector3(c.x, 0.5, cab_z - 1.18), _chrome)       # bumper
	var lamp := StandardMaterial3D.new()
	lamp.emission_enabled = true
	lamp.emission = Color("#fff1c4")
	lamp.emission_energy_multiplier = 3.0
	var amber := StandardMaterial3D.new()
	amber.emission_enabled = true
	amber.emission = Color("#ffa020")
	amber.emission_energy_multiplier = 2.0
	var red := StandardMaterial3D.new()
	red.emission_enabled = true
	red.emission = Color("#ff2010")
	red.emission_energy_multiplier = 2.5
	for sx in [-1, 1]:
		var hl := Models.cylinder(self, 0.13, 0.13, 0.08, Vector3(sx * (s.x / 2 - 0.25), 1.05, cab_z - 1.12), lamp)
		hl.rotation.x = PI / 2
		var beam := SpotLight3D.new()  # real headlights: they light the road and the fog
		beam.position = Vector3(sx * (s.x / 2 - 0.25), 1.05, cab_z - 1.25)
		beam.rotation = Vector3(-0.08, PI, 0)  # look along -z
		beam.light_color = Color("#fff1c4")
		beam.light_energy = 3.0
		beam.spot_range = 18.0
		beam.spot_angle = 30.0
		beam.light_volumetric_fog_energy = 2.0
		add_child(beam)
		Models.box(self, Vector3(0.12, 0.06, 0.04), Vector3(sx * (s.x / 2 + 0.05), top + 0.1, front - 0.15), amber)  # marker
		Models.box(self, Vector3(0.18, 0.3, 0.05), Vector3(sx * (s.x / 2 + 0.05), 0.75, back + 0.08), red)   # tail light
		var mirror := Models.box(self, Vector3(0.06, 0.35, 0.22), Vector3(sx * (s.x / 2 + 0.45), 2.0, cab_z - 0.3), _tire)
		Models.box(self, Vector3(0.35, 0.03, 0.03), Vector3(sx * (s.x / 2 + 0.25), 2.1, cab_z - 0.3), _chrome)
		mirror.name = "Mirror"
		Models.box(self, Vector3(0.02, 0.9, 1.0), Vector3(sx * (s.x / 2 + 0.11), 1.4, cab_z + 0.35), _steel)  # door seam
		Models.box(self, Vector3(0.04, 0.04, 0.18), Vector3(sx * (s.x / 2 + 0.13), 1.5, cab_z + 0.1), _chrome)  # handle
		Models.box(self, Vector3(0.3, 0.06, 0.5), Vector3(sx * (s.x / 2 + 0.2), 0.45, cab_z + 0.35), _steel)  # step
		for z in [cab_z - 0.5, c.z - s.z / 2 + 0.75, c.z + s.z / 2 - 0.75]:
			_wheel(Vector3(sx * (s.x / 2 + 0.38), 0.48, z), sx)  # outboard: the deck sits low
		var name_tag := Label3D.new()
		name_tag.text = "유령 이삿짐센터"
		name_tag.font_size = 110
		name_tag.outline_size = 14
		name_tag.outline_modulate = Color("#f4ecd8")
		name_tag.modulate = Color("#2a1a12")
		name_tag.position = Vector3(sx * (s.x / 2 + 0.24), c.y + 0.35, c.z)
		name_tag.rotation.y = sx * PI / 2
		add_child(name_tag)
		var sub := Label3D.new()
		sub.text = "GHOST MOVERS · 24시 출동 · 무서워도 옮깁니다"
		sub.font_size = 40
		sub.modulate = Color("#5a2a1a")
		sub.position = Vector3(sx * (s.x / 2 + 0.24), c.y - 0.15, c.z)
		sub.rotation.y = sx * PI / 2
		add_child(sub)


## A wheel on side `sx`: tyre, steel rim with lug nuts, and a mudguard over it.
func _wheel(at: Vector3, sx: int) -> void:
	var tyre := Models.cylinder(self, 0.48, 0.48, 0.34, at, _tire)
	tyre.rotation.z = PI / 2
	var rim := Models.cylinder(self, 0.26, 0.26, 0.36, at, _chrome)
	rim.rotation.z = PI / 2
	for k in 5:
		var a := k * TAU / 5.0
		var nut := Models.cylinder(self, 0.025, 0.025, 0.38, at + Vector3(0, sin(a), cos(a)) * 0.15, _steel)
		nut.rotation.z = PI / 2
	Models.box(self, Vector3(0.42, 0.06, 1.15), at + Vector3(-sx * 0.02, 0.56, 0), _tire)  # mudguard over it


func _wall(size: Vector3, pos: Vector3, mat: Material) -> StaticBody3D:
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
