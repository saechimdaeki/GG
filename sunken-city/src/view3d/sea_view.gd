class_name SeaView
extends Node3D
## 2.5D view of a World: 3D rock built from the tile grid (marching squares, extruded,
## noise-bumped), sunken ruins fading into fog behind, light from the surface, a water
## volume, drifting marine snow, and every character/object as a pixel Sprite3D.
## Game rules stay in World/Diver; this only shows them. 1 tile = 1 unit, y flipped.

const PX := 1.0 / Tuning.TILE      # one game pixel in world units
const SPRITE_PX := PX / 2.0        # sprite textures are built at 2x (tools/art_build.py)
const FRONT_Z := 0.6               # rock front face, a little in front of the play plane
const BACK_Z := -3.0               # rock extrusion depth
const CAM_FOV := 18.0              # narrow lens = nearly flat side view (rock sides barely show)
const VIEW_TILES_HIGH := 15.0      # how much of the world fits on screen vertically
const CAM_DIST := VIEW_TILES_HIGH / 2.0 / 0.15838  # tan(9 deg): distance that frames VIEW_TILES_HIGH
const LOOK_AHEAD := 2.5            # tiles the camera leads the diver's movement
const ROCK_SHADER := preload("res://src/view3d/spatial_env_rock.gdshader")
const KELP_SHADER := preload("res://src/view3d/spatial_env_kelp.gdshader")
const FISH_PER_SCHOOL := 14
## School home spots (tile coords) in wide open water, and their colors (body, belly).
const SCHOOLS := [
	[Vector2(40, 12), Color(0.95, 0.75, 0.25), Color(1.0, 0.95, 0.8)],
	[Vector2(84, 10), Color(0.3, 0.7, 0.95), Color(0.9, 0.95, 1.0)],
	[Vector2(116, 14), Color(0.95, 0.45, 0.4), Color(1.0, 0.85, 0.8)],
	[Vector2(60, 30), Color(0.6, 0.85, 0.5), Color(0.95, 1.0, 0.85)],
	[Vector2(40, 54), Color(0.55, 0.5, 0.95), Color(0.9, 0.9, 1.0)],
	[Vector2(90, 53), Color(0.4, 0.95, 0.85), Color(0.9, 1.0, 1.0)],
]
const WATER_DEEP := Color(0.01, 0.05, 0.12)
const WATER_FOG := Color(0.07, 0.36, 0.56)  # shallow water: clear bright blue, so dark rock stands out
const AIR_FOG := Color(0.82, 0.91, 1.0)
const SKY := Color(0.18, 0.53, 0.92)
const SKY_WARM_HORIZON := Color(1.0, 0.83, 0.64)
const AIR_TRANSITION_SECONDS := 0.5

var world: World
var camera := Camera3D.new()
var sprites := {}                  # key -> Sprite3D, created on first use
var _env := Environment.new()
var _sky_material := ShaderMaterial.new()
var _air_blend := 0.0
var _backdrop := MeshInstance3D.new()
var _underwater_backdrop_mat := StandardMaterial3D.new()
var _backdrop_mat := ShaderMaterial.new()
var _sun := DirectionalLight3D.new()
var _lamp := OmniLight3D.new()
var _rock_mat := ShaderMaterial.new()
var _rock := MeshInstance3D.new()
var _chunks := MeshInstance3D.new()
var _edges := MeshInstance3D.new()
var _contour: Array = []           # [Vector2, Vector2] wall outline segments, from the last terrain build
var _water := MeshInstance3D.new()
var _revision := -1
var _lead := Vector3.ZERO          # smoothed look-ahead offset
var _shown := {}
var _tex := {}
var _noise := FastNoiseLite.new()
var _kelp_mat := ShaderMaterial.new()
var _shafts: Array[MeshInstance3D] = []
var _snow := GPUParticles3D.new()
var _schools: Array = []           # {mm: MultiMeshInstance3D, home: Vector3, fish: Array of {pos, vel, offset}}


func setup(w: World, textures: Dictionary) -> void:
	world = w
	_tex = textures
	_noise.seed = 7
	_noise.frequency = 0.35
	_rock_mat.shader = ROCK_SHADER
	# the sandy stone matches the concept art and stays a different hue from the blue water
	_rock_mat.set_shader_parameter("stone_wet", load("res://assets/tiles/stone_dry.png"))
	_rock_mat.set_shader_parameter("stone_dry", load("res://assets/tiles/stone_dry.png"))
	_rock.material_override = _rock_mat
	_chunks.material_override = _rock_mat
	add_child(_rock)
	add_child(_chunks)
	var edge_mat := StandardMaterial3D.new()
	edge_mat.vertex_color_use_as_albedo = true
	edge_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	edge_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	_edges.material_override = edge_mat
	add_child(_edges)
	_build_environment()
	_build_stage()
	_build_water()
	_build_snow()
	_build_kelp()
	_build_schools()
	_build_light_shafts()
	camera.fov = CAM_FOV
	camera.current = true
	add_child(camera)


## Called every frame by the game with the live state.
func sync(diver: Diver, dry: float, dt: float) -> void:
	if world.revision != _revision:
		_revision = world.revision
		_rock.mesh = _terrain_mesh()
		_edges.mesh = _edge_mesh()
		_chunks.mesh = _chunk_mesh()
	var sy := -world.surface_y * PX
	_rock_mat.set_shader_parameter("surface_y", sy)
	_rock_mat.set_shader_parameter("dry_amount", dry)
	_place_water(sy)
	_kelp_mat.set_shader_parameter("surface_y", sy)
	for piece in _life:
		piece.visible = float(piece.get_meta("stage_base_y")) < sy
	_move_schools(to3(diver.pos), sy, dt)
	_move_shafts(sy)
	# marine snow only fills the water: re-aim the emitter at what is still flooded
	var water_h := maxf(sy + world.height, 0.5)
	_snow.position = Vector3(world.width / 2.0, sy - water_h / 2.0, 0)
	(_snow.process_material as ParticleProcessMaterial).emission_box_extents = Vector3(world.width / 2.0 + 4, water_h / 2.0, 4)
	var d := to3(diver.pos)
	var wet := world.in_water(diver.pos)
	# Blend the complete environment over half a second; at zero the underwater
	# colors, fog and lighting retain their original values.
	_air_blend = move_toward(_air_blend, 0.0 if wet else 1.0, dt / AIR_TRANSITION_SECONDS)
	var depth := clampf((d.y - sy) / -20.0, 0.0, 1.0)
	var water_color := WATER_FOG.lerp(WATER_DEEP, depth)
	var horizon := AIR_FOG.lerp(SKY_WARM_HORIZON, dry)
	_sky_material.set_shader_parameter("top_color", water_color.lerp(SKY.lerp(Color(0.3, 0.59, 0.94), dry), _air_blend))
	_sky_material.set_shader_parameter("horizon_color", water_color.lerp(horizon, _air_blend))
	# Fog's sky affect is independent of density and otherwise flattens the sky
	# to the fog color even with almost no distance fog.
	_env.fog_sky_affect = 1.0 - _air_blend
	_env.background_mode = Environment.BG_SKY if _air_blend > 0.0 else Environment.BG_COLOR
	_env.background_color = water_color
	_env.fog_light_color = water_color.lerp(horizon, _air_blend)
	_env.fog_density = lerpf(lerpf(0.003, 0.012, depth), 0.0003, _air_blend)
	_env.volumetric_fog_density = lerpf(0.006, 0.0, _air_blend)
	_env.ambient_light_color = Color(0.45, 0.65, 0.75).lerp(Color(0.85, 0.9, 1.0).lerp(Color(1.0, 0.9, 0.75), dry), _air_blend)
	_env.ambient_light_energy = lerpf(lerpf(0.55, 0.2, depth), lerpf(0.5, 1.0, dry), _air_blend)
	_sun.light_energy = lerpf(lerpf(1.6, 0.3, depth), lerpf(1.2, 1.8, dry), _air_blend)
	_sun.light_color = Color(0.6, 0.85, 1.0).lerp(Color(1.0, 0.97, 0.9).lerp(Color(1.0, 0.86, 0.66), dry), _air_blend)
	_backdrop_mat.set_shader_parameter("surface_y", sy)
	_backdrop_mat.set_shader_parameter("air_blend", _air_blend)
	# Use the original opaque pass underwater so the wall still writes depth.
	_backdrop.material_override = _backdrop_mat if _air_blend > 0.0 else _underwater_backdrop_mat
	_lamp.position = d + Vector3(0, 0.4, 1.0)
	_lamp.light_energy = lerpf(lerpf(0.6, 2.2, depth), 0.3, _air_blend)
	_lamp.omni_range = 9.0 if world.village.has("tower") else 6.0  # the light tower widens the lantern
	# camera: follow, clamped to the map so the edge of the world never shows
	var half_h := CAM_DIST * tan(deg_to_rad(CAM_FOV / 2.0))
	var half_w := half_h * 16.0 / 9.0
	_lead = _lead.lerp(Vector3(diver.vel.x, -diver.vel.y, 0) / Tuning.SWIM_MAX * LOOK_AHEAD, minf(dt * 1.5, 1.0))
	var target := Vector3(
		clampf(d.x + _lead.x, half_w, world.width - half_w),
		clampf(d.y + 0.5 + _lead.y * 0.5, -world.height + half_h, -half_h),
		CAM_DIST)
	camera.position = target if camera.position == Vector3.ZERO else camera.position.lerp(target, minf(dt * 5.0, 1.0))


static func to3(p: Vector2) -> Vector3:
	return Vector3(p.x * PX, -p.y * PX, 0.0)


## Shows (creating once) the sprite under key with texture name, at a 2D game position.
## base: the position is the sprite's bottom edge instead of its center.
func show_sprite(key: String, tex_name: String, at: Vector2, flip := false, base := false, z := 0.0, tint := Color.WHITE) -> void:
	var s: Sprite3D = sprites.get(key)
	if s == null:
		s = Sprite3D.new()
		s.pixel_size = SPRITE_PX
		s.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
		s.alpha_cut = SpriteBase3D.ALPHA_CUT_DISCARD
		s.shaded = false
		add_child(s)
		sprites[key] = s
	_shown[key] = true
	s.texture = _tex[tex_name]
	s.flip_h = flip
	s.modulate = tint
	s.visible = true
	var p := to3(at)
	if base:
		p.y += s.texture.get_height() * SPRITE_PX / 2.0
	s.position = p + Vector3(0, 0, z)


## Each frame: begin_frame(), show_sprite() for everything visible, end_frame() hides the rest.
func begin_frame() -> void:
	_shown.clear()


func end_frame() -> void:
	for k in sprites:
		if not _shown.has(k):
			sprites[k].visible = false


# --- terrain ---------------------------------------------------------------

func _solid(x: int, y: int) -> bool:
	return world.cell(Vector2i(x, y)) == World.Cell.WALL


## Marching squares over tile centers: walls end exactly on tile edges (matching the
## collision grid) and corners get cut diagonally, which reads as rounded rock.
func _terrain_mesh() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	_contour.clear()
	for y in range(-1, world.height):
		for x in range(-1, world.width):
			var c := [_solid(x, y), _solid(x + 1, y), _solid(x + 1, y + 1), _solid(x, y + 1)]
			if not (c[0] or c[1] or c[2] or c[3]):
				continue
			var o := Vector2(x + 0.5, y + 0.5)
			var corners := [o, o + Vector2(1, 0), o + Vector2(1, 1), o + Vector2(0, 1)]
			var poly: Array[Vector2] = []
			var is_mid: Array[bool] = []
			for i in 4:
				if c[i]:
					poly.append(corners[i])
					is_mid.append(false)
				if c[i] != c[(i + 1) % 4]:
					poly.append((corners[i] + corners[(i + 1) % 4]) / 2.0)
					is_mid.append(true)
			for i in range(1, poly.size() - 1):
				_tri(st, _v(poly[0], FRONT_Z), _v(poly[i], FRONT_Z), _v(poly[i + 1], FRONT_Z))
			# contour: consecutive midpoints inside the square → side wall back into the scene
			for i in poly.size():
				var j := (i + 1) % poly.size()
				if is_mid[i] and is_mid[j] and poly.size() > 2:
					_quad(st, poly[i], poly[j])
					_contour.append([poly[i], poly[j]])
	st.generate_normals()
	return st.commit()


func _v(p: Vector2, z: float) -> Vector3:
	var bump := _noise.get_noise_2d(p.x, p.y)
	return Vector3(p.x + bump * 0.15, -p.y + _noise.get_noise_2d(p.y, p.x) * 0.15, z + bump * 0.35)


func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	# order chosen so generate_normals() points camera-facing faces at +z (checked on screen:
	# the reverse order lit the rock's front as if it were a side)
	st.add_vertex(a)
	st.add_vertex(b)
	st.add_vertex(c)


func _quad(st: SurfaceTool, a: Vector2, b: Vector2) -> void:
	var af := _v(a, FRONT_Z)
	var bf := _v(b, FRONT_Z)
	var ab := _v(a, BACK_Z)
	var bb := _v(b, BACK_Z)
	_tri(st, af, bb, bf)
	_tri(st, af, ab, bb)


## Breakable tiles (cracked, rubble, hard) as separate lumpy blocks, so breaking one
## visibly removes a chunk. Rebuilt when the World's revision changes.
func _chunk_mesh() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var any := false
	for y in world.height:
		for x in world.width:
			var kind := world.cell(Vector2i(x, y))
			if kind == World.Cell.OPEN or kind == World.Cell.WALL:
				continue
			any = true
			var shrink: float = {World.Cell.CRACKED: 0.04, World.Cell.RUBBLE: 0.12, World.Cell.HARD: 0.0}[kind]
			var col: Color = {World.Cell.CRACKED: Color(0.75, 0.85, 0.85), World.Cell.RUBBLE: Color(0.9, 0.75, 0.6), World.Cell.HARD: Color(0.45, 0.45, 0.5)}[kind]
			st.set_color(col)
			_box(st, Vector2(x + shrink, y + shrink), Vector2(x + 1 - shrink, y + 1 - shrink))
	if not any:
		return null
	st.generate_normals()
	return st.commit()


func _box(st: SurfaceTool, a: Vector2, b: Vector2) -> void:
	var tl := a
	var tr := Vector2(b.x, a.y)
	var br := b
	var bl := Vector2(a.x, b.y)
	_tri(st, _v(tl, FRONT_Z), _v(tr, FRONT_Z), _v(br, FRONT_Z))
	_tri(st, _v(tl, FRONT_Z), _v(br, FRONT_Z), _v(bl, FRONT_Z))
	for e in [[tl, tr], [tr, br], [br, bl], [bl, tl]]:
		_quad(st, e[0], e[1])


# --- scenery ---------------------------------------------------------------

func _build_environment() -> void:
	var sky := Sky.new()
	sky.sky_material = _sky_material
	_sky_material.shader = preload("res://src/view3d/clear_sky.gdshader")
	_env.sky = sky
	_env.background_mode = Environment.BG_COLOR
	_env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	_env.ambient_light_color = Color(0.45, 0.65, 0.75)
	_env.fog_enabled = true
	_env.fog_light_color = WATER_FOG
	_env.fog_density = 0.04
	_env.volumetric_fog_enabled = true
	_env.volumetric_fog_density = 0.006
	_env.volumetric_fog_albedo = Color(0.6, 0.85, 0.95)
	_env.glow_enabled = true
	_env.glow_intensity = 0.6
	_env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	var we := WorldEnvironment.new()
	we.environment = _env
	add_child(we)
	# sunlight from the surface, slanted so the ruins cast light shafts through the fog
	_sun.rotation_degrees = Vector3(-62, 18, 0)
	_sun.shadow_enabled = true
	_sun.light_volumetric_fog_energy = 2.5
	add_child(_sun)
	_lamp.omni_range = 6.0
	_lamp.light_color = Color(1.0, 0.85, 0.6)
	add_child(_lamp)


## Paper stage: the concept-art cutouts (assets/stage) stood up at three depths.
## Life sits on floors and rooftops just behind the diver; big rocks and ruins fill
## the middle distance; giant towers and arches make the drowned skyline far back.
## Fog does the depth. Widths are in tiles; heights follow each picture's aspect.
const STAGE_LIFE := {"coral_orange": 2.0, "coral_pink": 2.2, "anemone": 1.6, "tube_sponge": 1.5,
	"sea_fan": 2.0, "rock_small": 2.0, "ruin_column_broken": 1.8, "kelp_tall": 1.4}
const STAGE_MID := {"rock_big": 9.0, "rock_mid": 5.0, "rock_arch": 9.0, "cliff_wall": 6.0, "ruin_arch": 7.0,
	"ruin_wall": 7.0, "ruin_wall_broken": 8.0, "ruin_column": 3.0, "ruin_stairs": 7.0, "wreck_bow": 12.0,
	"seabed_slab": 11.0, "coral_orange": 4.0, "sea_fan": 4.0}
const STAGE_FAR := {"ruin_tower_top": 16.0, "ruin_column": 5.0, "ruin_arch": 14.0, "cliff_wall": 12.0,
	"rock_arch": 18.0, "ruin_wall": 14.0}
var _life: Array[Node3D] = []   # living decor: hidden once the water leaves it


const STAGE_MODEL_GLOW := 0.15  # share of a GLB texture added as emission, so it matches the cutouts
const STAGE_MODEL_LIFT := 1.6  # vertex-colored GLBs: brighten albedo instead


func _stage_piece(name: String, width: float, base: Vector3, tint := Color.WHITE, flip := false) -> Node3D:
	var model_path := "res://assets/models/%s.glb" % name
	if ResourceLoader.exists(model_path):
		var scene := load(model_path) as PackedScene
		if scene != null:
			var model := _stage_model(scene, width, base, tint, flip)
			if model != null:
				model.set_meta("stage_asset", name)
				return model
	return _stage_cutout(name, width, base, tint, flip)


func _stage_cutout(name: String, width: float, base: Vector3, tint: Color, flip: bool) -> Sprite3D:
	var tex: Texture2D = load("res://assets/stage/%s.png" % name)
	var sp := Sprite3D.new()
	sp.texture = tex
	sp.pixel_size = width / tex.get_width()
	sp.alpha_cut = SpriteBase3D.ALPHA_CUT_DISCARD
	sp.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	sp.shaded = true
	sp.double_sided = true
	sp.flip_h = flip
	sp.modulate = tint
	sp.position = base + Vector3(0, tex.get_height() * sp.pixel_size / 2.0, 0)
	sp.set_meta("stage_asset", name)
	sp.set_meta("stage_base_y", base.y)
	add_child(sp)
	return sp


## Imported meshes may have transformed parents and arbitrary origins. Measure
## every surface in the holder's coordinates after flipping, then fit its width.
func _stage_model(scene: PackedScene, width: float, base: Vector3, tint: Color, flip: bool) -> Node3D:
	var holder := Node3D.new()
	var instance := scene.instantiate()
	if not instance is Node3D:
		instance.free()
		holder.free()
		return null
	holder.add_child(instance)
	if flip:
		holder.rotation.y = PI
	var boxes: Array[AABB] = []
	_stage_meshes(holder, Transform3D.IDENTITY, tint, boxes)
	if boxes.is_empty():
		holder.free()
		return null
	var bounds := boxes[0]
	for box in boxes.slice(1):
		bounds = bounds.merge(box)
	if bounds.size.x <= 0.0001:
		holder.free()
		return null
	var factor := width / bounds.size.x
	holder.scale = Vector3.ONE * factor
	holder.position = base - Vector3(bounds.get_center().x, bounds.position.y, bounds.get_center().z) * factor
	holder.set_meta("stage_base_y", base.y)
	add_child(holder)
	return holder


func _stage_meshes(node: Node, parent_transform: Transform3D, tint: Color, boxes: Array[AABB]) -> void:
	var local := parent_transform
	if node is Node3D:
		local *= (node as Node3D).transform
	if node is MeshInstance3D:
		var mi := node as MeshInstance3D
		if mi.mesh != null:
			boxes.append(local * mi.get_aabb())
			for surface in mi.mesh.get_surface_count():
				var original := mi.get_active_material(surface)
				var material: BaseMaterial3D
				if original is BaseMaterial3D:
					material = original.duplicate() as BaseMaterial3D
				else:
					material = StandardMaterial3D.new()
				material.albedo_color *= tint
				# TRELLIS exports PBR that reads metallic and murky underwater: match the
				# painted cutouts instead — matte, plus a little of its own texture as light
				material.metallic = 0.0
				material.roughness = 1.0
				if material.albedo_texture != null:  # no texture (vertex colors): glowing would wash it white
					material.emission_enabled = true
					material.emission = Color.WHITE * tint
					material.emission_texture = material.albedo_texture
					material.emission_energy_multiplier = STAGE_MODEL_GLOW
				else:
					material.albedo_color *= STAGE_MODEL_LIFT
				mi.set_surface_override_material(surface, material)
			mi.material_override = null  # allow the isolated per-surface tints to show
	for child in node.get_children():
		_stage_meshes(child, local, tint, boxes)


## Top surface (world y) of the first wall below row from in column x, or NAN if none.
func _ground_below(x: int, from: int) -> float:
	for y in range(from, world.height):
		if world.cell(Vector2i(x, y)) == World.Cell.WALL and world.cell(Vector2i(x, y - 1)) != World.Cell.WALL:
			return -float(y)
	return NAN


func _build_stage() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 33
	# life on every floor and rooftop, behind the play plane (the diver swims in front)
	for y in range(Zone1.SURFACE_ROW + 2, world.height):
		for x in world.width:
			if world.cell(Vector2i(x, y)) != World.Cell.OPEN or world.cell(Vector2i(x, y + 1)) != World.Cell.WALL:
				continue
			if rng.randf() > 0.22:
				continue
			var name: String = STAGE_LIFE.keys()[rng.randi() % STAGE_LIFE.size()]
			var piece := _stage_piece(name, STAGE_LIFE[name] * rng.randf_range(0.8, 1.25),
				Vector3(x + rng.randf(), -(y + 1) - 0.15, rng.randf_range(-1.4, -0.3)), Color.WHITE, rng.randf() < 0.5)
			_life.append(piece)
	# middle distance: along the seabed and the deep cavern floor
	for floor_from in [Zone1.SURFACE_ROW + 2, Zone1.PRESSURE_ROW]:
		var x := -4.0
		while x < world.width + 4:
			var col := clampi(int(x), 0, world.width - 1)
			var ground := _ground_below(col, floor_from)
			if not is_nan(ground):
				var name: String = STAGE_MID.keys()[rng.randi() % STAGE_MID.size()]
				var depth := rng.randf_range(-9.0, -3.5)
				var dim := remap(depth, -9.0, -3.5, 0.55, 0.85)
				var mid_piece := _stage_piece(name, STAGE_MID[name] * rng.randf_range(0.9, 1.4), Vector3(x, ground - 0.6, depth),
					Color(dim, dim * 1.05, dim * 1.1), rng.randf() < 0.5)
				if STAGE_LIFE.has(name):  # big corals and fans die off with the water too
					_life.append(mid_piece)
			x += rng.randf_range(4.0, 9.0)
	# the drowned skyline
	var fx := -20.0
	while fx < world.width + 20:
		var name: String = STAGE_FAR.keys()[rng.randi() % STAGE_FAR.size()]
		var depth := rng.randf_range(-34.0, -14.0)
		_stage_piece(name, STAGE_FAR[name] * rng.randf_range(0.8, 1.4),
			Vector3(fx, -world.height * 0.75 + rng.randf_range(-4.0, 6.0), depth), Color(0.45, 0.55, 0.62), rng.randf() < 0.5)
		fx += rng.randf_range(7.0, 15.0)
	# far back wall so the fog has something to fade into
	_underwater_backdrop_mat.albedo_color = Color(0.08, 0.2, 0.26)
	var shader := Shader.new()
	shader.code = """shader_type spatial;
	uniform float surface_y;
	uniform float air_blend;
	varying float world_y;
	void vertex() { world_y = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y; }
	void fragment() {
		ALBEDO = vec3(0.08, 0.2, 0.26);
		ROUGHNESS = 1.0;
		ALPHA = world_y > surface_y ? 1.0 - air_blend : 1.0;
	}
	"""
	_backdrop_mat.shader = shader
	var plane := PlaneMesh.new()
	plane.size = Vector2(world.width + 120, world.height + 80)
	plane.orientation = PlaneMesh.FACE_Z
	_backdrop.mesh = plane
	_backdrop.material_override = _underwater_backdrop_mat
	_backdrop.position = Vector3(world.width / 2.0, -world.height / 2.0, -60)
	add_child(_backdrop)


func _build_water() -> void:
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(0.1, 0.45, 0.65, 0.1)
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	var quad := QuadMesh.new()
	quad.size = Vector2(1, 1)
	_water.mesh = quad
	_water.material_override = mat
	add_child(_water)
	var line := MeshInstance3D.new()
	var lmat := StandardMaterial3D.new()
	lmat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	lmat.albedo_color = Color(0.7, 0.95, 1.0)
	lmat.emission_enabled = true
	lmat.emission = Color(0.5, 0.9, 1.0)
	var bar := BoxMesh.new()
	bar.size = Vector3(world.width + 20, 0.06, 0.06)
	line.mesh = bar
	line.material_override = lmat
	line.name = "SurfaceLine"
	_water.add_child(line)


## A translucent sheet in front of the play plane from the surface down: tints
## everything underwater, so the waterline is visible even from dry ground.
func _place_water(sy: float) -> void:
	var bottom := -float(world.height)
	var h := maxf(sy - bottom, 0.01)
	_water.scale = Vector3(world.width + 20, h, 1)
	_water.position = Vector3(world.width / 2.0, bottom + h / 2.0, FRONT_Z + 0.8)
	var line: Node3D = _water.get_node("SurfaceLine")
	line.scale = Vector3(1.0 / _water.scale.x, 1.0 / h, 1)
	line.position = Vector3(0, 0.5, 0)


func _build_snow() -> void:
	var p := _snow
	p.amount = 400
	p.lifetime = 12.0
	p.preprocess = 12.0
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(world.width / 2.0 + 4, world.height / 2.0 + 2, 4)
	pm.gravity = Vector3(0, -0.08, 0)
	pm.initial_velocity_min = 0.02
	pm.initial_velocity_max = 0.1
	pm.direction = Vector3(1, 0, 0)
	pm.spread = 180.0
	p.process_material = pm
	var q := QuadMesh.new()
	q.size = Vector2(PX * 1.5, PX * 1.5)
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = Color(0.75, 0.9, 0.95, 0.7)
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	q.material = m
	p.draw_pass_1 = q
	p.visibility_aabb = AABB(Vector3(-world.width, -world.height, -10), Vector3(world.width * 2, world.height * 2, 20))
	p.position = Vector3(world.width / 2.0, -world.height / 2.0, 0)
	add_child(p)


# --- life ----------------------------------------------------------------------

## Kelp on seafloor and rooftops: every open tile sitting on solid ground gets a
## strand now and then, some in front of the play plane and some behind.
func _build_kelp() -> void:
	_kelp_mat.shader = KELP_SHADER
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var rng := RandomNumberGenerator.new()
	rng.seed = 21
	for y in range(Zone1.SURFACE_ROW + 2, world.height):
		for x in world.width:
			if world.cell(Vector2i(x, y)) != World.Cell.OPEN or world.cell(Vector2i(x, y + 1)) != World.Cell.WALL:
				continue
			if rng.randf() > 0.45:
				continue
			for k in rng.randi_range(1, 3):
				var root := Vector3(x + rng.randf(), -(y + 1), rng.randf_range(-2.5, 1.2))
				_strand(st, root, rng.randf_range(1.2, 3.6), rng.randf_range(0.12, 0.22))
	var mi := MeshInstance3D.new()
	mi.mesh = st.commit()
	mi.material_override = _kelp_mat
	add_child(mi)


func _strand(st: SurfaceTool, root: Vector3, height: float, width: float) -> void:
	const SEGMENTS := 5
	for i in SEGMENTS:
		var v0 := float(i) / SEGMENTS
		var v1 := float(i + 1) / SEGMENTS
		var w0 := width * (1.0 - v0 * 0.6)
		var w1 := width * (1.0 - v1 * 0.6)
		var a := [root + Vector3(-w0, v0 * height, 0), root + Vector3(w0, v0 * height, 0),
			root + Vector3(w1, v1 * height, 0), root + Vector3(-w1, v1 * height, 0)]
		var uv := [Vector2(0, v0), Vector2(1, v0), Vector2(1, v1), Vector2(0, v1)]
		for idx in [0, 1, 2, 0, 2, 3]:
			st.set_uv(uv[idx])
			st.set_uv2(Vector2(root.y + 0.3, 0))  # shader drops the strand once this is above water
			st.add_vertex(a[idx])


## One small low-poly fish (diamond body + forked tail), facing +x, colored by vertex.
func _fish_mesh(body: Color, belly: Color) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var nose := Vector3(0.32, 0, 0)
	var top := Vector3(0, 0.13, 0)
	var bot := Vector3(0, -0.11, 0)
	var back := Vector3(-0.2, 0, 0)
	var l := Vector3(0, 0, 0.07)
	var r := Vector3(0, 0, -0.07)
	var tail_t := Vector3(-0.36, 0.11, 0)
	var tail_b := Vector3(-0.36, -0.1, 0)
	for f in [[nose, top, l, body], [nose, l, bot, belly], [nose, r, top, body], [nose, bot, r, belly],
			[back, l, top, body], [back, bot, l, belly], [back, top, r, body], [back, r, bot, belly],
			[back, tail_t, tail_b, body.darkened(0.2)]]:
		st.set_color(f[3])
		st.add_vertex(f[0])
		st.set_color(f[3])
		st.add_vertex(f[1])
		st.set_color(f[3])
		st.add_vertex(f[2])
	st.generate_normals()
	return st.commit()


## Schools wander around their home spot and scatter from the diver. They swim in a
## layer just behind the play plane, so they never need collision.
func _build_schools() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 5
	for spec in SCHOOLS:
		var mat := StandardMaterial3D.new()
		mat.vertex_color_use_as_albedo = true
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		mat.roughness = 0.5
		mat.emission_enabled = true  # fish read as bright flecks even deep down, like the reference
		mat.emission = spec[1] * 0.35
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = _fish_mesh(spec[1], spec[2])
		mm.instance_count = FISH_PER_SCHOOL
		var mi := MultiMeshInstance3D.new()
		mi.multimesh = mm
		mi.material_override = mat
		add_child(mi)
		var home := Vector3(spec[0].x, -spec[0].y, -1.4)
		var fish := []
		for i in FISH_PER_SCHOOL:
			fish.append({
				"pos": home + Vector3(rng.randf_range(-2, 2), rng.randf_range(-1, 1), rng.randf_range(-0.8, 0.8)),
				"vel": Vector3(rng.randf_range(-1, 1), 0, 0),
				"offset": Vector3(rng.randf_range(-1.8, 1.8), rng.randf_range(-0.9, 0.9), rng.randf_range(-0.8, 0.8)),
			})
		_schools.append({"mi": mi, "home": home, "fish": fish, "phase": rng.randf() * TAU})


func _move_schools(diver: Vector3, surface: float, dt: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0
	for sc in _schools:
		var home: Vector3 = sc["home"]
		# fish only live in water: a school whose home dried out is gone
		sc["mi"].visible = home.y < surface - 1.0
		if not sc["mi"].visible:
			continue
		var center := home + Vector3(sin(t * 0.15 + sc["phase"]) * 6.0, sin(t * 0.3 + sc["phase"]) * 1.2, 0)
		var mm: MultiMesh = sc["mi"].multimesh
		for i in sc["fish"].size():
			var f: Dictionary = sc["fish"][i]
			var goal: Vector3 = center + f["offset"]
			var steer: Vector3 = (goal - f["pos"]) * 1.2
			var away: Vector3 = f["pos"] - diver
			away.z = 0
			if away.length() < 3.0:  # scatter from the diver
				steer += away.normalized() * (3.0 - away.length()) * 4.0
			f["vel"] = (f["vel"] + steer * dt).limit_length(3.0)
			f["pos"] += f["vel"] * dt
			var dir: Vector3 = f["vel"]
			var basis := Basis.IDENTITY
			if absf(dir.x) > 0.01:
				basis = Basis(Vector3.UP, 0.0 if dir.x > 0 else PI) * Basis(Vector3.BACK, clampf(dir.y / maxf(absf(dir.x), 0.3), -0.6, 0.6) * signf(dir.x))
			mm.set_instance_transform(i, Transform3D(basis, f["pos"]))


## Light shafts: long additive quads slanting down from the surface, fading with depth
## and slowly breathing. Cheaper and more readable than volumetric fog alone.
func _build_light_shafts() -> void:
	var g := Gradient.new()
	# additive, so keep it faint: a hint of light, strongest just under the surface
	g.set_color(0, Color(0.06, 0.09, 0.1, 1.0))
	g.add_point(0.35, Color(0.03, 0.05, 0.06, 1.0))
	g.set_color(1, Color(0, 0, 0, 1.0))
	var tex := GradientTexture2D.new()
	tex.gradient = g
	tex.fill_from = Vector2(0, 1)
	tex.fill_to = Vector2(0, 0)  # QuadMesh UV y runs bottom-up here: bright at the top (surface), gone below
	var rng := RandomNumberGenerator.new()
	rng.seed = 9
	for i in 14:
		var mat := StandardMaterial3D.new()
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD  # black adds nothing, so no alpha needed
		mat.albedo_texture = tex
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		mat.disable_fog = true
		var q := QuadMesh.new()
		q.size = Vector2(rng.randf_range(1.5, 4.0), 22.0)
		var mi := MeshInstance3D.new()
		mi.mesh = q
		mi.material_override = mat
		mi.rotation_degrees.z = rng.randf_range(14.0, 24.0)  # same slant as the sun
		mi.position = Vector3(rng.randf_range(0.0, world.width), 0, rng.randf_range(-6.0, -1.0))
		mi.set_meta("x", mi.position.x)
		add_child(mi)
		_shafts.append(mi)


func _move_shafts(surface: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0
	for i in _shafts.size():
		var mi := _shafts[i]
		# hang from the current surface; sway and pulse slowly
		mi.position = Vector3(mi.get_meta("x") + sin(t * 0.2 + i) * 1.5 - 4.0, surface - 11.0, mi.position.z)
		var mat: StandardMaterial3D = mi.material_override
		mat.albedo_color = Color.WHITE * (0.3 + 0.2 * sin(t * 0.5 + i * 1.7))


## Where rock meets water: a dark outline on every wall edge, so the shape of the
## ground reads at a glance.
func _edge_mesh() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	const LINE := 0.1
	var z := FRONT_Z + 0.4  # in front of the noise bumps on the rock face
	for seg in _contour:
		var a: Vector2 = seg[0]
		var b: Vector2 = seg[1]
		var n := (b - a).orthogonal().normalized()
		# the normal must point out of the rock: step a little and check which side is solid
		var mid := (a + b) / 2.0
		if _solid(floori(mid.x + n.x * 0.3), floori(mid.y + n.y * 0.3)):
			n = -n
		_strip(st, a, b, n * LINE, Color(0.03, 0.03, 0.04), z)
	return st.commit()


func _strip(st: SurfaceTool, a: Vector2, b: Vector2, off: Vector2, col: Color, z: float) -> void:
	var pa := _v(a, FRONT_Z)
	var pb := _v(b, FRONT_Z)
	var o := Vector3(off.x, -off.y, 0)
	for v in [pa, pb, pb + o, pa, pb + o, pa + o]:
		st.set_color(col)
		st.add_vertex(Vector3(v.x, v.y, z))
