extends CharacterBody3D
## First-person mover. The owning peer moves it and sends grab targets to the host;
## everyone else sees the synced transform.

const Tuning = preload("res://src/tuning.gd")
const Furniture = preload("res://src/furniture.gd")

var head := Node3D.new()
var cam := Camera3D.new()
var ray := RayCast3D.new()
var face := MeshInstance3D.new()
var held: RigidBody3D
var held_local := Vector3.ZERO
var hold_dist := 2.0
var _demo := "--demo-grab" in OS.get_cmdline_user_args()
var _age := 0.0
## Knocked out by the ghost: can't move or grab. Synced so others see us lying down.
var stunned := false
var _stun_left := 0.0


func _init() -> void:
	var t := Tuning.data
	var cap := CapsuleShape3D.new()
	cap.radius = 0.35
	cap.height = 1.7
	var col := CollisionShape3D.new()
	col.shape = cap
	col.position.y = 0.85
	add_child(col)

	var body_mesh := CapsuleMesh.new()
	body_mesh.radius = 0.35
	body_mesh.height = 1.7
	var body := MeshInstance3D.new()
	body.name = "Body"
	body.mesh = body_mesh
	body.position.y = 0.85
	add_child(body)

	head.name = "Head"
	head.position.y = 1.55
	add_child(head)
	var face_mesh := BoxMesh.new()
	face_mesh.size = Vector3(0.3, 0.15, 0.1)
	face.mesh = face_mesh
	face.position = Vector3(0, 0, -0.33)
	head.add_child(face)
	head.add_child(cam)
	ray.target_position = Vector3(0, 0, -t.reach)
	cam.add_child(ray)

	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", "Head:rotation", ".:stunned"]:
		cfg.add_property(NodePath(p))
	var sync := MultiplayerSynchronizer.new()
	sync.replication_config = cfg
	add_child(sync)


func _ready() -> void:
	add_to_group("players")
	ray.add_exception(self)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color.from_hsv(fmod(name.to_int() * 0.37, 1.0), 0.6, 0.9)
	($Body as MeshInstance3D).material_override = mat
	var mine := is_multiplayer_authority()
	cam.current = mine
	face.visible = not mine  # ponytail: the face box would sit in front of our own camera
	if mine and _demo:
		head.rotation.x = -0.5


## Called on the owning peer when the ghost hits us.
func stun(sec: float) -> void:
	_stun_left = sec
	stunned = true
	held = null


func _process(_delta: float) -> void:
	($Body as Node3D).rotation.x = PI / 2 if stunned else 0.0


func _unhandled_input(event: InputEvent) -> void:
	if not is_multiplayer_authority() or stunned:
		return
	var captured := Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	if event is InputEventMouseMotion and captured:
		var s: float = Tuning.data.mouse_sensitivity
		rotate_y(-event.relative.x * s)
		head.rotation.x = clampf(head.rotation.x - event.relative.y * s, -1.4, 1.4)
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed and not captured:
			if _main().run.phase == "day":  # between days the mouse is for the shop
				Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		elif event.pressed:
			_try_grab()
		else:
			_release()
	elif event.is_action_pressed("use_tape"):
		_main().request_tape()
	elif event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		_release()


func _physics_process(delta: float) -> void:
	if not is_multiplayer_authority():
		return
	var t := Tuning.data
	_age += delta
	if _demo:
		_demo_step()

	if stunned:
		_stun_left -= delta
		stunned = _stun_left > 0.0
		velocity.x = 0.0
		velocity.z = 0.0
		velocity += get_gravity() * delta
		move_and_slide()
		return
	if not is_on_floor():
		velocity += get_gravity() * delta
	elif Input.is_action_just_pressed("jump"):
		velocity.y = t.jump_velocity
	var input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var dir := (transform.basis * Vector3(input.x, 0, input.y)).normalized()
	var speed: float = t.walk_speed * carry_factor()
	velocity.x = dir.x * speed
	velocity.z = dir.z * speed
	move_and_slide()

	if held and not is_instance_valid(held):
		held = null
	if held:
		var target := cam.global_position - cam.global_basis.z * hold_dist
		_main().send_grab(held.name, held_local, target)


## Walk speed multiplier: heavy things slow you down, sharing the load helps.
func carry_factor() -> float:
	if held == null or not is_instance_valid(held):
		return 1.0
	var t := Tuning.data
	var share: float = held.mass / maxi(held.holders, 1)
	return clampf(1.0 - share / t.slow_mass, t.min_carry_speed, 1.0)


func _try_grab() -> void:
	ray.target_position = Vector3(0, 0, -(Tuning.data.reach + _main().run.reach_bonus))
	ray.force_raycast_update()
	var hit := ray.get_collider()
	if hit and hit.name == "DepartButton":
		_main().request_depart()
	elif hit is Furniture:
		held = hit
		var p := ray.get_collision_point()
		held_local = held.to_local(p)
		hold_dist = cam.global_position.distance_to(p)


func _release() -> void:
	if held:
		held = null
		_main().send_release()


## --demo-grab: grab whatever is in front after 1.5 s, then look up to lift it.
func _demo_step() -> void:
	if _age > 1.5 and held == null:
		_try_grab()
	if held:
		head.rotation.x = move_toward(head.rotation.x, -0.1, 0.004)


## Main is our grandparent (Main/Players/<id>); works in tests where current_scene is null.
func _main() -> Node:
	return get_parent().get_parent()
