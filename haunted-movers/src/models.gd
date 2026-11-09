## Low-poly furniture and prop meshes built from primitives — no model files.
## Every model fits inside its piece's collision box (`size`, centred on the origin),
## with the front facing -z, so physics and gameplay are unchanged.

const SURFACE := preload("res://src/spatial_env_surface.gdshader")


## Visual for a furniture entry: a Node3D of MeshInstances inside `size`.
static func furniture(kind: String, size: Vector3, color: Color) -> Node3D:
	var root := Node3D.new()
	root.name = "Model"
	var h := size / 2.0
	var wood := grain(color)
	var dark := grain(color.darkened(0.45))
	match kind:
		"sofa":
			var fabric := matte(color)
			var cushion := matte(color.lightened(0.12))
			var legs_h := 0.1
			box(root, Vector3(size.x, 0.32, size.z), Vector3(0, -h.y + legs_h + 0.16, 0), fabric)             # base
			box(root, Vector3(size.x - 0.3, 0.42, 0.22), Vector3(0, h.y - 0.21 - 0.02, h.z - 0.11), fabric)   # back
			for sx in [-1, 1]:
				box(root, Vector3(0.18, 0.5, size.z), Vector3(sx * (h.x - 0.09), -h.y + legs_h + 0.25, 0), fabric)  # arms
				box(root, Vector3((size.x - 0.4) / 2 - 0.02, 0.14, size.z - 0.26),
					Vector3(sx * (size.x - 0.4) / 4, -h.y + legs_h + 0.39, -0.1), cushion)                    # seat cushions
				for sz in [-1, 1]:
					box(root, Vector3(0.07, legs_h, 0.07), Vector3(sx * (h.x - 0.1), -h.y + legs_h / 2, sz * (h.z - 0.1)), dark)
		"chair":
			var seat_y := -h.y + 0.45
			box(root, Vector3(size.x, 0.06, size.z), Vector3(0, seat_y, 0), wood)
			for sx in [-1, 1]:
				for sz in [-1, 1]:
					box(root, Vector3(0.05, 0.45, 0.05), Vector3(sx * (h.x - 0.04), -h.y + 0.225, sz * (h.z - 0.04)), dark)
				box(root, Vector3(0.05, h.y * 2 - 0.45, 0.05), Vector3(sx * (h.x - 0.04), seat_y + (h.y - seat_y) / 2, h.z - 0.04), dark)
			for i in 3:
				box(root, Vector3(size.x - 0.08, 0.05, 0.03), Vector3(0, seat_y + 0.15 + i * 0.12, h.z - 0.04), wood)
		"lamp":
			cylinder(root, 0.16, 0.18, 0.05, Vector3(0, -h.y + 0.025, 0), dark)                  # base
			cylinder(root, 0.02, 0.02, size.y - 0.4, Vector3(0, -h.y + (size.y - 0.4) / 2, 0), metal())  # pole
			var shade := StandardMaterial3D.new()
			shade.albedo_color = color
			shade.emission_enabled = true
			shade.emission = Color("#ffcf7a")
			shade.emission_energy_multiplier = 1.4
			cylinder(root, 0.1, h.x, 0.32, Vector3(0, h.y - 0.2, 0), shade)                        # shade
			var bulb := OmniLight3D.new()
			bulb.position = Vector3(0, h.y - 0.25, 0)
			bulb.light_color = Color("#ffcf7a")
			bulb.light_energy = 0.6
			bulb.omni_range = 2.5
			root.add_child(bulb)
		"fridge":
			var enamel := StandardMaterial3D.new()
			enamel.albedo_color = color
			enamel.roughness = 0.25
			box(root, size - Vector3(0.02, 0, 0.02), Vector3.ZERO, enamel)
			box(root, Vector3(size.x - 0.04, 0.012, 0.012), Vector3(0, h.y * 0.3, -h.z), matte(Color("#55606a")))  # door gap
			for y in [h.y * 0.6, -h.y * 0.1]:
				box(root, Vector3(0.04, 0.32, 0.05), Vector3(h.x - 0.12, y, -h.z - 0.03), metal())                 # handles
		"piano":
			var lacquer := StandardMaterial3D.new()
			lacquer.albedo_color = color
			lacquer.roughness = 0.15
			box(root, Vector3(size.x, size.y, size.z - 0.25), Vector3(0, 0, 0.125), lacquer)                   # cabinet
			box(root, Vector3(size.x, 0.08, 0.25), Vector3(0, -h.y + 0.72, -h.z + 0.125), lacquer)             # key bed
			box(root, Vector3(size.x - 0.12, 0.03, 0.18), Vector3(0, -h.y + 0.775, -h.z + 0.1), matte(Color("#f2ecdf")))  # white keys
			var black := matte(Color("#111111"))
			for i in 18:
				if i % 7 in [2, 6]:
					continue  # gaps between black-key groups
				box(root, Vector3(0.035, 0.03, 0.1), Vector3(-h.x + 0.12 + i * 0.072, -h.y + 0.80, -h.z + 0.14), black)
			for sx in [-1, 1]:
				box(root, Vector3(0.06, 0.7, 0.06), Vector3(sx * (h.x - 0.08), -h.y + 0.35, -h.z + 0.05), lacquer)  # front legs
		"wardrobe":
			box(root, Vector3(size.x, size.y - 0.08, size.z), Vector3(0, -0.04, 0), wood)
			box(root, Vector3(size.x + 0.06, 0.08, size.z + 0.06), Vector3(0, h.y - 0.04, 0), dark)          # crown
			for sx in [-1, 1]:
				box(root, Vector3(h.x - 0.08, size.y - 0.4, 0.02), Vector3(sx * h.x / 2, -0.05, -h.z - 0.005), dark)  # door panels
				box(root, Vector3(0.03, 0.12, 0.03), Vector3(sx * 0.06, 0, -h.z - 0.03), metal())                     # handles
		"box":
			var card := matte(color)
			box(root, size, Vector3.ZERO, card)
			var tape := matte(Color("#d9c8a0"))
			box(root, Vector3(0.1, 0.012, size.z + 0.01), Vector3(0, h.y, 0), tape)          # tape over the lid
			box(root, Vector3(size.x + 0.01, 0.012, 0.01), Vector3(0, h.y * 0.4, -h.z), matte(color.darkened(0.25)))  # lid seam
		_:
			box(root, size, Vector3.ZERO, wood)
	return root


## A mover in overalls and a cap (`color` = this player's colour). Origin at the feet,
## facing -z. Head parts sit under a child "HeadVis" (pivot at the neck) so it can nod.
static func mover(color: Color) -> Node3D:
	var root := Node3D.new()
	root.name = "Body"
	var overalls := matte(color)
	var shirt := matte(Color("#e9e2cf"))
	var skin := matte(Color("#e8b48f"))
	var boots := matte(Color("#3a2a22"))
	# Limbs hang from pivots at the hip/shoulder (named LegL/LegR/ArmL/ArmR) so player.gd
	# can swing them by rotating the pivot on x.
	for sx in [-1, 1]:
		var side := "L" if sx < 0 else "R"
		var hip := Node3D.new()
		hip.name = "Leg" + side
		hip.position = Vector3(sx * 0.13, 0.82, 0)
		root.add_child(hip)
		_capsule(hip, 0.11, 0.8, Vector3(0, -0.4, 0), overalls)                           # leg
		box(hip, Vector3(0.18, 0.12, 0.3), Vector3(0, -0.76, -0.04), boots)                 # boot
		var shoulder := Node3D.new()
		shoulder.name = "Arm" + side
		shoulder.position = Vector3(sx * 0.31, 1.4, -0.04)
		shoulder.rotation.z = sx * 0.12
		root.add_child(shoulder)
		_capsule(shoulder, 0.08, 0.62, Vector3(0, -0.3, 0), shirt)                         # arm
		_sphere(shoulder, 0.08, Vector3(0, -0.62, -0.03), skin)                             # hand
	_capsule(root, 0.27, 0.75, Vector3(0, 1.1, 0), overalls)                              # torso
	box(root, Vector3(0.3, 0.22, 0.05), Vector3(0, 1.2, -0.25), overalls)                 # bib
	for sx in [-1, 1]:
		box(root, Vector3(0.05, 0.3, 0.04), Vector3(sx * 0.12, 1.38, -0.24), overalls)    # straps
	var head := Node3D.new()
	head.name = "HeadVis"
	head.position.y = 1.5
	root.add_child(head)
	_sphere(head, 0.2, Vector3(0, 0.08, 0), skin)
	cylinder(head, 0.2, 0.21, 0.12, Vector3(0, 0.22, 0), overalls)                        # cap crown
	box(head, Vector3(0.3, 0.025, 0.18), Vector3(0, 0.17, -0.22), overalls)              # cap brim
	var dark := matte(Color("#1a1218"))
	for sx in [-1, 1]:
		_sphere(head, 0.025, Vector3(sx * 0.07, 0.1, -0.19), dark)                       # eyes
	box(head, Vector3(0.08, 0.015, 0.01), Vector3(0, 0.0, -0.195), dark)                  # mouth
	return root


static func _capsule(parent: Node3D, radius: float, height: float, pos: Vector3, mat: Material) -> MeshInstance3D:
	var m := CapsuleMesh.new()
	m.radius = radius
	m.height = height
	m.radial_segments = 12
	m.rings = 4
	m.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.position = pos
	parent.add_child(mi)
	return mi


static func _sphere(parent: Node3D, radius: float, pos: Vector3, mat: Material) -> MeshInstance3D:
	var m := SphereMesh.new()
	m.radius = radius
	m.height = radius * 2
	m.radial_segments = 12
	m.rings = 6
	m.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.position = pos
	parent.add_child(mi)
	return mi


## Where the face goes: centre of the eyes, on the front surface of the model.
static func face_anchor(kind: String, size: Vector3) -> Vector3:
	var h := size / 2.0
	match kind:
		"sofa":
			return Vector3(0, h.y - 0.2, h.z - 0.22)
		"chair":
			return Vector3(0, h.y - 0.12, h.z - 0.06)
		"lamp":
			return Vector3(0, h.y - 0.2, -0.13)
		"piano":
			return Vector3(0, h.y * 0.45, -h.z + 0.25)
		"wardrobe":
			return Vector3(0, h.y * 0.55, -h.z - 0.016)
	return Vector3(0, h.y * 0.5, -h.z)


static func box(parent: Node3D, size: Vector3, pos: Vector3, mat: Material) -> MeshInstance3D:
	var m := BoxMesh.new()
	m.size = size
	m.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.position = pos
	parent.add_child(mi)
	return mi


static func cylinder(parent: Node3D, top: float, bottom: float, height: float, pos: Vector3, mat: Material) -> MeshInstance3D:
	var m := CylinderMesh.new()
	m.top_radius = top
	m.bottom_radius = bottom
	m.height = height
	m.radial_segments = 12
	m.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.position = pos
	parent.add_child(mi)
	return mi


## Wood-grain surface in `color` (spatial_env_surface mode 3).
static func grain(color: Color) -> ShaderMaterial:
	return surface(3, color, color.darkened(0.3))


static func surface(mode: int, base: Color, accent: Color, floor_y := 0.0) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = SURFACE
	m.set_shader_parameter("mode", mode)
	m.set_shader_parameter("base_color", base)
	m.set_shader_parameter("accent_color", accent)
	m.set_shader_parameter("floor_y", floor_y)
	return m


static func matte(color: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = 0.9
	return m


static func metal() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color("#b8a27a")
	m.metallic = 0.8
	m.roughness = 0.35
	return m
