extends Node3D
## Truck bed (open at the house side) and the depart lever. Pure geometry: main.gd counts
## the scrap on the bed (`truck_value`).

const Tuning = preload("res://src/tuning.gd")
const Models = preload("res://src/models.gd")

var bed := AABB()


func _ready() -> void:
	var t: Dictionary = Tuning.data.truck
	bed = AABB(Vector3(t.bed_min[0], t.bed_min[1], t.bed_min[2]), Vector3(t.bed_size[0], t.bed_size[1], t.bed_size[2]))
	var c := bed.get_center()
	var s := bed.size
	_wall(Vector3(0.2, s.y, s.z), Vector3(bed.position.x - 0.1, c.y, c.z), "#e8e0cc")   # left
	_wall(Vector3(0.2, s.y, s.z), Vector3(bed.end.x + 0.1, c.y, c.z), "#e8e0cc")        # right
	_wall(Vector3(s.x + 0.4, s.y, 0.2), Vector3(c.x, c.y, bed.position.z - 0.1), "#e8e0cc")  # cab side
	_dress(c, s)

	var pad := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(s.x, s.z)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color("#e07a2e")
	pm.material = mat
	pad.mesh = pm
	pad.position = Vector3(c.x, bed.position.y + 0.01, c.z)
	add_child(pad)

	var button := _wall(Vector3(0.4, 0.4, 0.2), Vector3(bed.end.x + 0.6, 1.1, bed.end.z + 0.3), "#d0302a")
	button.name = "DepartButton"
	var lever := MeshInstance3D.new()  # a stubby lever with a red knob
	var lm := CylinderMesh.new()
	lm.top_radius = 0.03
	lm.bottom_radius = 0.03
	lm.height = 0.4
	lever.mesh = lm
	lever.position = Vector3(0, 0.3, -0.05)
	lever.rotation.x = -0.4
	button.add_child(lever)
	var glow := OmniLight3D.new()
	glow.light_color = Color("#ff4030")
	glow.light_energy = 0.6
	glow.omni_range = 2.0
	glow.position = Vector3(0, 0.3, -0.3)
	button.add_child(glow)


## Mesh-only: cargo roof, cab, wheels, bumper, lights and the company name.
func _dress(c: Vector3, s: Vector3) -> void:
	var body := Models.matte(Color("#e8e0cc"))
	var paint := Models.matte(Color("#d0612a"))
	var tire := Models.matte(Color("#1a1a1c"))
	var chrome := Models.metal()
	Models.box(self, Vector3(s.x + 0.4, 0.12, s.z + 0.2), Vector3(c.x, bed.end.y + 0.06, c.z), body)   # cargo roof
	Models.box(self, Vector3(s.x + 0.44, 0.3, s.z + 0.2), Vector3(c.x, bed.end.y - 0.15, c.z), paint)  # stripe
	var cab_z := bed.position.z - 1.15
	Models.box(self, Vector3(s.x + 0.2, 1.4, 2.0), Vector3(c.x, 1.0, cab_z), paint)                    # cab lower
	Models.box(self, Vector3(s.x + 0.1, 0.9, 1.5), Vector3(c.x, 2.1, cab_z + 0.2), paint)             # cab upper
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color("#22303a")
	glass.roughness = 0.05
	glass.metallic = 0.4
	Models.box(self, Vector3(s.x - 0.2, 0.7, 0.05), Vector3(c.x, 2.1, cab_z - 0.56), glass)          # windshield
	for sx in [-1, 1]:
		Models.box(self, Vector3(0.05, 0.6, 1.0), Vector3(sx * (s.x / 2 + 0.06), 2.1, cab_z + 0.2), glass)
		var lamp := StandardMaterial3D.new()
		lamp.emission_enabled = true
		lamp.emission = Color("#fff1c4")
		lamp.emission_energy_multiplier = 2.0
		Models.box(self, Vector3(0.35, 0.2, 0.05), Vector3(sx * (s.x / 2 - 0.3), 0.75, cab_z - 1.01), lamp)  # headlights
		for z in [cab_z, c.z - s.z / 2 + 0.7, c.z + s.z / 2 - 0.7]:
			var wheel := Models.cylinder(self, 0.45, 0.45, 0.32, Vector3(sx * (s.x / 2 + 0.3), 0.32, z), tire)
			wheel.rotation.z = PI / 2
			var hub := Models.cylinder(self, 0.2, 0.2, 0.34, Vector3(sx * (s.x / 2 + 0.3), 0.32, z), chrome)
			hub.rotation.z = PI / 2
		var name_tag := Label3D.new()
		name_tag.text = "유령 이삿짐센터\nGHOST MOVERS"
		name_tag.font_size = 72
		name_tag.outline_size = 0
		name_tag.modulate = Color("#5a2a1a")
		name_tag.position = Vector3(sx * (s.x / 2 + 0.21), c.y + 0.3, c.z)
		name_tag.rotation.y = sx * PI / 2
		add_child(name_tag)
	Models.box(self, Vector3(s.x + 0.3, 0.25, 0.2), Vector3(c.x, 0.45, cab_z - 1.05), chrome)        # bumper


func _wall(size: Vector3, pos: Vector3, color := "#3d6e8f") -> StaticBody3D:
	var sb := StaticBody3D.new()
	sb.position = pos
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	sb.add_child(col)
	var mesh := BoxMesh.new()
	mesh.size = size
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(color)
	mesh.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	sb.add_child(mi)
	add_child(sb)
	return sb
