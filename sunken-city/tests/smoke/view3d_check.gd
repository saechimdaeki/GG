extends SceneTree
## View-only regressions: nested model bounds, flipped pivot, material isolation,
## cutout fallback and half-second environment transition. No gameplay edits.
var _fails := 0


func _initialize() -> void:
	call_deferred("_run")


func _expect(ok: bool, message: String) -> void:
	if not ok:
		_fails += 1
		push_error(message)


func _run() -> void:
	var view := SeaView.new()
	root.add_child(view)
	var original := StandardMaterial3D.new()
	original.albedo_color = Color(0.8, 0.6, 0.4)
	var model := Node3D.new()
	model.position = Vector3(3, 4, 2)
	var parent := Node3D.new()
	parent.position = Vector3(-1, 2, -3)
	parent.rotation.y = 0.4
	parent.scale = Vector3(2, 1, 1)
	model.add_child(parent)
	parent.owner = model
	var mesh := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(2, 3, 4)
	box.material = original
	mesh.mesh = box
	mesh.position = Vector3(4, -2, 1)
	parent.add_child(mesh)
	mesh.owner = model
	var scene := PackedScene.new()
	scene.pack(model)
	var base := Vector3(12, -6, -4)
	for flip in [false, true]:
		var result := view._stage_model(scene, 9.0, base, Color(0.5, 0.7, 0.9), flip)
		_expect(result != null, "valid mesh failed to instantiate")
		var bounds := _bounds(result)
		_expect(is_equal_approx(bounds.size.x, 9.0), "model width is not nine tiles")
		_expect(is_equal_approx(bounds.position.y, base.y), "model bottom missed base")
		_expect(bounds.get_center().is_equal_approx(Vector3(base.x, bounds.get_center().y, base.z)), "model was not centered at base")
		var imported := result.get_child(0).get_child(0).get_child(0) as MeshInstance3D
		var material := imported.get_active_material(0) as BaseMaterial3D
		# an untextured model is brightened by STAGE_MODEL_LIFT on top of the tint (matches the cutouts)
		var want: Color = original.albedo_color * Color(0.5, 0.7, 0.9) * SeaView.STAGE_MODEL_LIFT
		_expect(material.albedo_color.is_equal_approx(want), "model tint mismatch")
		_expect(material.metallic == 0.0 and material.roughness == 1.0, "model should be matte")
		_expect(original.albedo_color.is_equal_approx(Color(0.8, 0.6, 0.4)), "shared material mutated")
		result.free()
	model.free()
	var cutout := view._stage_piece("rock_small", 2.0, base)
	_expect(cutout is Sprite3D, "missing GLB did not fall back to cutout")
	_expect(is_equal_approx(float(cutout.get_meta("stage_base_y")), base.y), "cutout lost life base metadata")
	cutout.free()
	# Real setup checks the waterline and environment without moving the diver.
	var world := World.new()
	view.setup(world, {})
	var diver := Diver.new(world.start)
	_expect(not world.in_water(diver.pos), "expected dry start for sky test")
	for i in 15:
		view.sync(diver, 0.0, 1.0 / 60.0)
	_expect(is_equal_approx(view._air_blend, 0.5), "air transition should be half complete at .25s")
	for i in 16:
		view.sync(diver, 1.0, 1.0 / 60.0)
	_expect(is_equal_approx(view._air_blend, 1.0), "air transition did not finish in .5s")
	_expect(view._env.background_mode == Environment.BG_SKY, "air must use a sky")
	_expect(view._env.fog_density <= 0.0005, "air fog too dense")
	_expect(is_zero_approx(view._env.fog_sky_affect), "fog flattened the sky gradient")
	_expect(is_zero_approx(view._env.volumetric_fog_density), "underwater volumetric fog leaked into air")
	diver.pos = Vector2(50.5, 14.5) * Tuning.TILE
	for i in 31:
		view.sync(diver, 0.0, 1.0 / 60.0)
	_expect(is_zero_approx(view._air_blend), "water transition did not finish")
	_expect(view._env.background_mode == Environment.BG_COLOR, "underwater mode changed")
	var depth := clampf((SeaView.to3(diver.pos).y + world.surface_y * SeaView.PX) / -20.0, 0.0, 1.0)
	_expect(is_equal_approx(view._env.fog_density, lerpf(0.003, 0.012, depth)), "underwater fog density changed")
	_expect(view._env.fog_light_color.is_equal_approx(SeaView.WATER_FOG.lerp(SeaView.WATER_DEEP, depth)), "underwater fog color changed")
	view.free()
	print("VIEW3D PASS" if _fails == 0 else "VIEW3D FAIL %d" % _fails)
	quit(0 if _fails == 0 else 1)


func _bounds(node: Node) -> AABB:
	var boxes: Array[AABB] = []
	_collect(node, boxes)
	var result := boxes[0]
	for box in boxes.slice(1):
		result = result.merge(box)
	return result


func _collect(node: Node, boxes: Array[AABB]) -> void:
	if node is MeshInstance3D:
		var mesh := node as MeshInstance3D
		boxes.append(mesh.global_transform * mesh.get_aabb())
	for child in node.get_children():
		_collect(child, boxes)
