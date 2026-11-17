## Low-poly furniture, scrap and prop meshes. Furniture with a `model` (a CC0 Kenney GLB under
## assets/models/) is that model scaled to fit; the rest are built from primitives.
## Every model fits inside its piece's collision box (`size`, centred on the origin),
## with the front facing -z, so physics and gameplay are unchanged.

const SURFACE := preload("res://src/spatial_env_surface.gdshader")


## Visual for a furniture entry: a Node3D of MeshInstances inside `size`. With `model` (a GLB
## path) it's that model, uniformly scaled to fit `size` and stood on its floor.
static func furniture(kind: String, size: Vector3, color: Color, model := "", yaw := 0.0) -> Node3D:
	if model != "":
		return _fitted(model, size, yaw, kind == "lamp")
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
		"bookshelf":
			box(root, Vector3(size.x, size.y, 0.04), Vector3(0, 0, h.z - 0.02), dark)              # back panel
			for sx in [-1, 1]:
				box(root, Vector3(0.05, size.y, size.z), Vector3(sx * (h.x - 0.025), 0, 0), wood)  # sides
			var spines := [Color("#7a2e2e"), Color("#2e4a6a"), Color("#6a5a2e"), Color("#3a5a3a"), Color("#5a3a5a")]
			for i in 5:
				var y := -h.y + i * size.y / 4.0
				box(root, Vector3(size.x - 0.1, 0.04, size.z), Vector3(0, y + 0.02, 0), wood)       # shelves
				if i < 4:
					for b in 6:  # a row of book spines, irregular heights
						var bh := 0.22 + fmod(b * 0.37 + i * 0.21, 0.12)
						box(root, Vector3(0.1, bh, size.z - 0.1), Vector3(-h.x + 0.12 + b * 0.13, y + 0.04 + bh / 2, -0.02),
							matte(spines[(b + i) % spines.size()]))
		"tv":
			var plastic := matte(Color("#2a2622"))
			box(root, Vector3(size.x, size.y, size.z - 0.15), Vector3(0, 0, 0.075), plastic)          # deep CRT body
			box(root, Vector3(size.x - 0.04, size.y - 0.04, 0.15), Vector3(0, 0, -h.z + 0.075), plastic)
			var glass := StandardMaterial3D.new()
			glass.albedo_color = Color("#1a2420")
			glass.emission_enabled = true
			glass.emission = Color("#3a5a50")  # faint static glow
			glass.emission_energy_multiplier = 0.6
			box(root, Vector3(size.x * 0.72, size.y * 0.7, 0.02), Vector3(-size.x * 0.08, 0.02, -h.z), glass)
			for i in 2:
				cylinder(root, 0.025, 0.025, 0.03, Vector3(h.x - 0.08, 0.1 - i * 0.12, -h.z - 0.01), metal()).rotation.x = PI / 2
		"bed":
			var sheet := matte(Color("#d8d0bc"))
			var blanket := matte(color)
			box(root, Vector3(size.x, 0.25, size.z), Vector3(0, -h.y + 0.25, 0), wood)              # frame
			box(root, Vector3(size.x - 0.06, 0.18, size.z - 0.06), Vector3(0, -h.y + 0.46, 0), sheet)  # mattress
			box(root, Vector3(size.x * 0.62, 0.06, size.z - 0.02), Vector3(-size.x * 0.18, -h.y + 0.57, 0), blanket)
			box(root, Vector3(0.35, 0.1, size.z - 0.3), Vector3(h.x - 0.3, -h.y + 0.6, 0), sheet)   # pillow
			box(root, Vector3(0.06, size.y, size.z), Vector3(h.x - 0.03, 0, 0), wood)               # headboard
			for sx in [-1, 1]:
				for sz in [-1, 1]:
					box(root, Vector3(0.08, 0.14, 0.08), Vector3(sx * (h.x - 0.06), -h.y + 0.07, sz * (h.z - 0.06)), dark)
		"box":
			var card := matte(color)
			box(root, size, Vector3.ZERO, card)
			var tape := matte(Color("#d9c8a0"))
			box(root, Vector3(0.1, 0.012, size.z + 0.01), Vector3(0, h.y, 0), tape)          # tape over the lid
			box(root, Vector3(size.x + 0.01, 0.012, 0.01), Vector3(0, h.y * 0.4, -h.z), matte(color.darkened(0.25)))  # lid seam
		"vase":
			var glaze := StandardMaterial3D.new()
			glaze.albedo_color = color
			glaze.roughness = 0.2
			cylinder(root, h.x * 0.55, h.x * 0.7, size.y * 0.25, Vector3(0, -h.y + size.y * 0.125, 0), glaze)
			cylinder(root, h.x, h.x * 0.55, size.y * 0.45, Vector3(0, -h.y + size.y * 0.475, 0), glaze)   # belly
			cylinder(root, h.x * 0.35, h.x, size.y * 0.3, Vector3(0, h.y - size.y * 0.15, 0), glaze)       # neck
			cylinder(root, h.x * 0.95, h.x * 0.95, 0.03, Vector3(0, -h.y + size.y * 0.42, 0), matte(Color("#e8d27a")))  # gold band
		"painting":
			box(root, size, Vector3.ZERO, grain(color))                                           # gilt frame
			box(root, Vector3(size.x - 0.16, size.y - 0.16, 0.02), Vector3(0, 0, -h.z), matte(Color("#3a2a30")))
			box(root, Vector3(0.3, 0.38, 0.02), Vector3(0, 0.02, -h.z - 0.01), matte(Color("#c9a68a")))  # pale sitter
		"safe":
			var steel := metal()
			box(root, size, Vector3.ZERO, matte(color))
			cylinder(root, 0.12, 0.12, 0.04, Vector3(0, 0.05, -h.z - 0.02), steel).rotation.x = PI / 2    # dial
			box(root, Vector3(0.04, 0.25, 0.05), Vector3(h.x - 0.12, 0.05, -h.z - 0.02), steel)            # handle
		"crate":
			box(root, size, Vector3.ZERO, wood)
			for sx in [-1, 1]:
				box(root, Vector3(0.06, size.y + 0.01, size.z + 0.01), Vector3(sx * (h.x - 0.04), 0, 0), dark)
			box(root, Vector3(size.x + 0.01, 0.06, size.z + 0.01), Vector3(0, 0, 0), dark)
		_:
			box(root, size, Vector3.ZERO, wood)
	return root


## Collision box of each kind of scrap (the model fits inside, front -z).
const SCRAP_SIZE := {
	candlestick = Vector3(0.18, 0.45, 0.18), teapot = Vector3(0.3, 0.24, 0.22), doll = Vector3(0.2, 0.38, 0.14),
	music_box = Vector3(0.3, 0.2, 0.22), silver_tray = Vector3(0.5, 0.06, 0.36), vase = Vector3(0.26, 0.5, 0.26),
	portrait = Vector3(0.6, 0.75, 0.08), typewriter = Vector3(0.45, 0.22, 0.4), pocket_watch = Vector3(0.12, 0.03, 0.14),
	crystal_ball = Vector3(0.26, 0.32, 0.26), gramophone = Vector3(0.5, 0.75, 0.5), safe = Vector3(0.55, 0.6, 0.55),
}


## Visual for one piece of scrap, centred on the origin inside SCRAP_SIZE[kind].
static func scrap(kind: String, color: Color) -> Node3D:
	var root := Node3D.new()
	root.name = "Model"
	var size: Vector3 = SCRAP_SIZE.get(kind, Vector3.ONE * 0.3)
	var h := size / 2.0
	var shiny := StandardMaterial3D.new()
	shiny.albedo_color = color
	shiny.metallic = 0.6
	shiny.roughness = 0.3
	var gold := metal()
	var dark := matte(color.darkened(0.5))
	match kind:
		"candlestick":
			cylinder(root, 0.08, 0.09, 0.03, Vector3(0, -h.y + 0.015, 0), shiny)
			cylinder(root, 0.02, 0.03, 0.3, Vector3(0, -h.y + 0.18, 0), shiny)
			cylinder(root, 0.05, 0.03, 0.04, Vector3(0, h.y - 0.12, 0), shiny)
			cylinder(root, 0.025, 0.025, 0.09, Vector3(0, h.y - 0.05, 0), matte(Color("#efe6cc")))  # candle
		"teapot":
			var china := matte(color)
			_sphere(root, 0.1, Vector3(0, -0.02, 0), china)
			cylinder(root, 0.02, 0.03, 0.12, Vector3(0.12, 0.0, 0), china).rotation.z = -0.9      # spout
			_sphere(root, 0.03, Vector3(0, 0.09, 0), matte(Color("#3a5a8a")))                      # lid knob
		"doll":
			var dress := matte(color)
			cylinder(root, 0.04, 0.09, 0.2, Vector3(0, -h.y + 0.1, 0), dress)
			_sphere(root, 0.07, Vector3(0, h.y - 0.1, 0), matte(Color("#f0dcc8")))
			for sx in [-1, 1]:
				_sphere(root, 0.012, Vector3(sx * 0.025, h.y - 0.09, -0.065), matte(Color.BLACK))  # button eyes
		"music_box":
			box(root, size - Vector3(0, 0.04, 0), Vector3(0, -0.02, 0), grain(color))
			box(root, Vector3(size.x, 0.04, size.z), Vector3(0, h.y - 0.02, 0), grain(color.lightened(0.15)))
			cylinder(root, 0.015, 0.015, 0.08, Vector3(h.x + 0.03, 0, 0), gold).rotation.z = PI / 2  # crank
		"silver_tray":
			cylinder(root, h.x, h.x * 0.9, size.y, Vector3.ZERO, shiny).scale = Vector3(1, 1, size.z / size.x)
		"vase":
			var glaze := StandardMaterial3D.new()
			glaze.albedo_color = color
			glaze.roughness = 0.2
			cylinder(root, h.x * 0.6, h.x * 0.7, size.y * 0.25, Vector3(0, -h.y + size.y * 0.125, 0), glaze)
			cylinder(root, h.x, h.x * 0.6, size.y * 0.45, Vector3(0, -h.y + size.y * 0.475, 0), glaze)
			cylinder(root, h.x * 0.35, h.x, size.y * 0.3, Vector3(0, h.y - size.y * 0.15, 0), glaze)
			cylinder(root, h.x * 0.95, h.x * 0.95, 0.03, Vector3(0, -h.y + size.y * 0.42, 0), gold)
		"portrait":
			box(root, size, Vector3.ZERO, grain(color))
			box(root, Vector3(size.x - 0.1, size.y - 0.1, 0.02), Vector3(0, 0, -h.z), matte(Color("#2a2228")))
			_sphere(root, 0.12, Vector3(0, 0.08, -h.z - 0.01), matte(Color("#c9a68a"))).scale = Vector3(1, 1.25, 0.1)
		"typewriter":
			box(root, Vector3(size.x, size.y * 0.6, size.z), Vector3(0, -h.y + size.y * 0.3, 0), matte(color))
			box(root, Vector3(size.x * 0.9, 0.05, 0.08), Vector3(0, h.y - 0.03, h.z - 0.06), matte(color))  # carriage
			box(root, Vector3(size.x * 0.7, 0.12, 0.01), Vector3(0, h.y, h.z - 0.02), matte(Color("#efe9d8")))  # paper
			for i in 8:
				_sphere(root, 0.015, Vector3(-0.15 + i * 0.043, -h.y + size.y * 0.62, -h.z + 0.08), matte(Color("#d8d0bc")))
		"pocket_watch":
			cylinder(root, h.x, h.x, size.y, Vector3.ZERO, shiny)
			cylinder(root, h.x * 0.8, h.x * 0.8, 0.005, Vector3(0, h.y, 0), matte(Color("#f4ecd8")))
			cylinder(root, 0.015, 0.015, 0.03, Vector3(0, 0, -h.z), shiny).rotation.x = PI / 2
		"crystal_ball":
			var glass := StandardMaterial3D.new()
			glass.albedo_color = Color(color, 0.6)
			glass.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			glass.emission_enabled = true
			glass.emission = color
			glass.emission_energy_multiplier = 0.8
			glass.roughness = 0.05
			_sphere(root, 0.12, Vector3(0, 0.03, 0), glass)
			cylinder(root, 0.08, 0.11, 0.08, Vector3(0, -h.y + 0.04, 0), gold)
		"gramophone":
			box(root, Vector3(0.4, 0.2, 0.4), Vector3(0, -h.y + 0.1, 0), grain(color))
			cylinder(root, 0.16, 0.16, 0.01, Vector3(0, -h.y + 0.205, 0), matte(Color("#111111")))  # record
			cylinder(root, 0.015, 0.015, 0.3, Vector3(0.12, -h.y + 0.35, 0.1), gold)
			var horn := cylinder(root, 0.25, 0.03, 0.4, Vector3(0.05, h.y - 0.2, -0.05), gold)  # brass horn
			horn.rotation.x = -0.7
		"safe":
			box(root, size, Vector3.ZERO, matte(color))
			cylinder(root, 0.09, 0.09, 0.04, Vector3(0, 0.05, -h.z - 0.02), gold).rotation.x = PI / 2
			box(root, Vector3(0.04, 0.2, 0.05), Vector3(h.x - 0.1, 0.05, -h.z - 0.02), gold)
		_:
			box(root, size, Vector3.ZERO, dark)
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


## A GLB scaled uniformly to fit inside `size` (centred, bottom on the floor of the box),
## turned by `yaw`. `glow` adds a warm bulb near the top (floor lamps).
static func _fitted(path: String, size: Vector3, yaw: float, glow: bool) -> Node3D:
	var root := Node3D.new()
	root.name = "Model"
	var m: Node3D = (load(path) as PackedScene).instantiate()
	var box := AABB()
	for mi: MeshInstance3D in m.find_children("*", "MeshInstance3D", true, false):
		var b: AABB = _local_xform(mi, m) * mi.get_aabb()
		box = b if box.size == Vector3.ZERO else box.merge(b)
	var s := minf(size.x / box.size.x, minf(size.y / box.size.y, size.z / box.size.z))
	m.scale = Vector3.ONE * s
	m.position = -(box.position + box.size * Vector3(0.5, 0, 0.5)) * s - Vector3(0, size.y / 2, 0)
	for mi: MeshInstance3D in m.find_children("*", "MeshInstance3D", true, false):
		for i in mi.mesh.get_surface_count():  # bright toy colours → dusty, faded, old
			var src := mi.mesh.surface_get_material(i) as StandardMaterial3D
			if src:
				var aged := src.duplicate() as StandardMaterial3D
				var c := src.albedo_color
				aged.albedo_color = Color.from_hsv(c.h, c.s * 0.45, c.v * 0.6)
				aged.roughness = 0.9
				mi.set_surface_override_material(i, aged)
	var pivot := Node3D.new()
	pivot.rotation.y = PI + yaw  # Kenney models face +z; ours face -z
	pivot.add_child(m)
	root.add_child(pivot)
	if glow:
		var bulb := OmniLight3D.new()
		bulb.position = Vector3(0, size.y / 2 - 0.25, 0)
		bulb.light_color = Color("#ffcf7a")
		bulb.light_energy = 0.6
		bulb.omni_range = 2.5
		root.add_child(bulb)
	return root


static func _local_xform(n: Node3D, top: Node3D) -> Transform3D:
	var t := Transform3D.IDENTITY
	while n != top:
		t = n.transform * t
		n = n.get_parent()
	return t
