extends CharacterBody3D
## First-person mover. The owning peer moves it and sends grab targets to the host;
## everyone else sees the synced transform.

const Tuning = preload("res://src/tuning.gd")
const Furniture = preload("res://src/furniture.gd")
const Models = preload("res://src/models.gd")
const Settings = preload("res://src/settings.gd")
const Sfx = preload("res://src/sfx.gd")
const Voice = preload("res://src/voice.gd")
const STEP_LENGTH := 0.7  # m walked per footstep

var head := Node3D.new()
var cam := Camera3D.new()
var ray := RayCast3D.new()
var body: Node3D
var flashlight := SpotLight3D.new()
var _steps := AudioStreamPlayer3D.new()
## This player's voice, heard where they stand (remote copies only).
var _speaker := AudioStreamPlayer3D.new()
var _gen := AudioStreamGenerator.new()
## Voice chunks received (for the smoke test) and a short "talking" timer for the HUD.
## Holding something: arms go forward. Owner sets it, synced so everyone sees the pose.
var carrying := false
var _swing := 0.0
var heard_chunks := 0
var talk_t := 0.0
var _talk_icon := Label3D.new()
var _walked := 0.0
var _last_pos := Vector3.ZERO
## Flashlight on/off, owned by this player, synced so others see the beam.
var flash_on := true
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

	head.name = "Head"
	head.position.y = 1.55
	add_child(head)
	head.add_child(cam)
	flashlight.position = Vector3(0.18, -0.15, 0)  # held at the right hand, under the eyes
	flashlight.spot_range = 14.0
	flashlight.spot_angle = 24.0
	flashlight.spot_attenuation = 0.8
	flashlight.light_energy = 3.0
	flashlight.light_color = Color("#fff2d6")
	flashlight.shadow_enabled = true
	flashlight.light_volumetric_fog_energy = 4.0  # visible beam in the dusty air
	cam.add_child(flashlight)
	_gen.mix_rate = Voice.RATE
	_gen.buffer_length = 0.4  # jitter buffer: absorbs bursty packets
	_speaker.stream = _gen
	_speaker.position.y = 1.6
	_speaker.unit_size = 6.0
	_speaker.max_distance = 25.0
	_speaker.bus = &"Voice"
	add_child(_speaker)
	_talk_icon.text = "♪"  # over a remote player's head while they talk
	_talk_icon.font_size = 110
	_talk_icon.outline_size = 12
	_talk_icon.modulate = Color(0.75, 1.0, 0.75)
	_talk_icon.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_talk_icon.no_depth_test = true
	_talk_icon.position.y = 2.15
	_talk_icon.visible = false
	add_child(_talk_icon)
	_steps.stream = Sfx.cached("step")
	_steps.unit_size = 4.0
	_steps.max_polyphony = 2
	add_child(_steps)
	ray.target_position = Vector3(0, 0, -t.reach)
	cam.add_child(ray)

	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", "Head:rotation", ".:stunned", ".:flash_on", ".:carrying"]:
		cfg.add_property(NodePath(p))
	var sync := MultiplayerSynchronizer.new()
	sync.name = "Sync"  # fixed name: auto names (@...@N) differ between peers
	sync.replication_config = cfg
	add_child(sync)


func _ready() -> void:
	add_to_group("players")
	ray.add_exception(self)
	body = Models.mover(Color.from_hsv(fmod(name.to_int() * 0.37, 1.0), 0.6, 0.85))
	add_child(body)
	var mine := is_multiplayer_authority()
	cam.current = mine
	if not mine:
		_speaker.play()
	# our own body would poke into the camera, and our flashlight (held at the body) would
	# throw its shadow across the room as a long diagonal line, so it's fully hidden for us
	if mine:
		body.visible = false
	if mine and _demo:
		head.rotation.x = -0.5


## Walk cycle from ground speed; arms forward while carrying. Pure visuals, runs on every peer.
func _animate(speed: float, delta: float) -> void:
	if is_multiplayer_authority():
		carrying = held != null
	if body == null or stunned:
		return
	var walking := speed > 0.3 and speed < 20.0
	_swing += delta * clampf(speed, 0.0, 6.0) * 2.4 if walking else 0.0
	var amp := 0.55 if walking else 0.0
	var leg := sin(_swing) * amp
	for side in ["L", "R"]:
		var sgn := 1.0 if side == "L" else -1.0
		var hip := body.get_node("Leg" + side) as Node3D
		hip.rotation.x = lerpf(hip.rotation.x, leg * sgn, 0.3)
		var arm := body.get_node("Arm" + side) as Node3D
		var target := 1.35 if carrying else -leg * sgn * 0.7  # carrying: both arms reach forward
		arm.rotation.x = lerpf(arm.rotation.x, target, 0.25)


## Local player: send one encoded voice chunk to everyone (the server relays for clients).
func send_voice(pcm: PackedByteArray) -> void:
	talk_t = 0.25
	var peer := multiplayer.multiplayer_peer
	if peer == null or peer.get_connection_status() != MultiplayerPeer.CONNECTION_CONNECTED:
		return  # host left: nobody to talk to (and get_peers() would error)
	if not multiplayer.get_peers().is_empty():
		_rpc_voice.rpc(pcm)


@rpc("any_peer", "unreliable_ordered")
func _rpc_voice(pcm: PackedByteArray) -> void:
	if multiplayer.get_remote_sender_id() == get_multiplayer_authority():  # only the owner speaks for itself
		receive_voice(pcm)


## Remote copy: queue the chunk on our 3D speaker.
func receive_voice(pcm: PackedByteArray) -> void:
	heard_chunks += 1
	talk_t = 0.25
	var pb := _speaker.get_stream_playback() as AudioStreamGeneratorPlayback
	if pb == null:
		return
	for v in Voice.decode(pcm):
		if not pb.can_push_buffer(1):
			break  # buffer full: drop the rest rather than lag further behind
		pb.push_frame(Vector2(v, v))


## Called on the owning peer when the ghost hits us.
func stun(sec: float) -> void:
	_stun_left = sec
	stunned = true
	held = null


func _process(delta: float) -> void:
	talk_t = maxf(0.0, talk_t - delta)
	_talk_icon.visible = talk_t > 0.0 and not is_multiplayer_authority()
	flashlight.visible = flash_on and not stunned
	# Footsteps from how far we moved on the ground (works for remote players too: synced position).
	var moved := (global_position - _last_pos) * Vector3(1, 0, 1)
	_last_pos = global_position
	_animate(moved.length() / maxf(delta, 0.0001), delta)
	if moved.length() < 1.0 and not stunned:  # ignore teleports (respawn, --cam)
		_walked += moved.length()
		if _walked >= STEP_LENGTH:
			_walked = 0.0
			_steps.pitch_scale = randf_range(0.85, 1.15)
			_steps.play()
	if is_multiplayer_authority():
		cam.position.y = sin(_walked / STEP_LENGTH * PI) * 0.035  # head bob, one dip per step
	if is_multiplayer_authority() and is_inside_tree():
		ray.target_position = Vector3(0, 0, -(Tuning.data.reach + _main().run.reach_bonus))
		ray.force_raycast_update()
		_main().set_aim(null if held or stunned else ray.get_collider())
	if body:
		body.rotation.x = PI / 2 if stunned else 0.0  # knocked flat on the back
		(body.get_node("HeadVis") as Node3D).rotation.x = head.rotation.x * 0.6


func _unhandled_input(event: InputEvent) -> void:
	if not is_multiplayer_authority() or stunned:
		return
	var captured := Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	if event is InputEventMouseMotion and captured:
		var s: float = Tuning.data.mouse_sensitivity * Settings.get_value("sensitivity")
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
	elif event.is_action_pressed("flashlight"):
		flash_on = not flash_on
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
