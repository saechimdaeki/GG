extends Node3D
## Truck bed (open at the house side) and the depart button. Pure geometry plus
## `evaluate`, which turns what is on the bed into a payout via payout.gd.

const Tuning = preload("res://src/tuning.gd")
const Payout = preload("res://src/payout.gd")

var bed := AABB()


func _ready() -> void:
	var t: Dictionary = Tuning.data.truck
	bed = AABB(Vector3(t.bed_min[0], t.bed_min[1], t.bed_min[2]), Vector3(t.bed_size[0], t.bed_size[1], t.bed_size[2]))
	var c := bed.get_center()
	var s := bed.size
	_wall(Vector3(0.2, s.y, s.z), Vector3(bed.position.x - 0.1, c.y, c.z))   # left
	_wall(Vector3(0.2, s.y, s.z), Vector3(bed.end.x + 0.1, c.y, c.z))        # right
	_wall(Vector3(s.x + 0.4, s.y, 0.2), Vector3(c.x, c.y, bed.position.z - 0.1))  # cab side

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
	var label := Label3D.new()
	label.text = "출발"
	label.font_size = 48
	label.position = Vector3(0, 0.4, 0)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	button.add_child(label)


## Payout for every piece of furniture whose centre is on the bed.
func evaluate(bodies: Array) -> Dictionary:
	var t: Dictionary = Tuning.data.truck
	var items := []
	for b in bodies:
		if bed.has_point(b.global_position):
			items.append({value = b.value, durability = b.durability, aabb = b.world_aabb()})
	return Payout.compute(items, t.cell, t.bonus_rate)


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
