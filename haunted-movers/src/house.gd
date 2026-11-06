extends Node3D
## The haunted house: 2 floors, a narrow inner door, a ramp staircase and a roof.
## Front door faces -z, toward the truck. Interior: x -6..6, z 0..10, floors at y 0 and 3.
## ponytail: hand-placed boxes; swap for modelled rooms once the layout is proven fun.

const WALL := "#bfae95"
const OUTER := "#8f7f6c"
const FLOOR := "#6b5444"


func _ready() -> void:
	_box(Vector3(40, 0.2, 40), Vector3(0, -0.1, 0), "#4a5a3a")       # yard / ground floor
	_box(Vector3(11.8, 0.02, 9.8), Vector3(0, 0.01, 5), FLOOR)       # wooden floor inside

	# Outer walls, 2 storeys. Front (z=0) has a 1.6 m x 2.2 m door.
	_box(Vector3(5.2, 6, 0.2), Vector3(-3.4, 3, 0), OUTER)
	_box(Vector3(5.2, 6, 0.2), Vector3(3.4, 3, 0), OUTER)
	_box(Vector3(1.6, 3.8, 0.2), Vector3(0, 4.1, 0), OUTER)
	_box(Vector3(12.2, 6, 0.2), Vector3(0, 3, 10), OUTER)
	_box(Vector3(0.2, 6, 10), Vector3(-6, 3, 5), OUTER)
	_box(Vector3(0.2, 6, 10), Vector3(6, 3, 5), OUTER)
	_box(Vector3(12.2, 0.2, 10.2), Vector3(0, 6.1, 5), "#3a2f2a")   # roof

	# Ground-floor inner wall at z=5.5 with a narrow 1.2 m door (sofa must be turned).
	_box(Vector3(2.4, 3, 0.2), Vector3(-4.8, 1.5, 5.5), WALL)
	_box(Vector3(8.4, 3, 0.2), Vector3(1.8, 1.5, 5.5), WALL)
	_box(Vector3(1.2, 0.8, 0.2), Vector3(-3.0, 2.6, 5.5), WALL)

	# Second floor slab, leaving a stairwell at x 4.2..6, z 0..5.5.
	_box(Vector3(10.2, 0.2, 10), Vector3(-0.9, 2.9, 5), FLOOR)
	_box(Vector3(1.8, 0.2, 4.5), Vector3(5.1, 2.9, 7.75), FLOOR)

	# Stair ramp along the right wall: y 0 at z 0.5 up to y 3 at z 5.4 (~31°).
	var run := 4.9
	var rise := 3.0
	var ramp := _box(Vector3(1.6, 0.2, sqrt(run * run + rise * rise)), Vector3(5.1, 1.4, 0.5 + run / 2), "#7a5a40")
	ramp.rotation.x = -atan2(rise, run)

	for p in [Vector3(0, 2.5, 3), Vector3(-3, 2.5, 8), Vector3(3, 2.5, 8), Vector3(0, 5.5, 5)]:
		var lamp := OmniLight3D.new()
		lamp.position = p
		lamp.omni_range = 8.0
		lamp.light_color = Color("#ffc58a")
		lamp.light_energy = 1.2
		lamp.shadow_enabled = true
		add_child(lamp)


func _box(size: Vector3, pos: Vector3, color: String) -> StaticBody3D:
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
