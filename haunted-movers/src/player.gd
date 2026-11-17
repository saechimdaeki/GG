extends CharacterBody3D
## First-person mover. The owning peer moves it, carries up to `slots` pieces of scrap
## (one in hand), and asks the host to pick up / drop; everyone else sees the synced
## transform, slots and the item in hand.

const Tuning = preload("res://src/tuning.gd")
const Loot = preload("res://src/loot.gd")
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
const SWING_TIME := 0.28
var _swing_t := 0.0
var _weapon: Node3D
## This player's voice, heard where they stand (remote copies only).
var _speaker := AudioStreamPlayer3D.new()
var _gen := AudioStreamGenerator.new()
## Voice chunks received (for the smoke test) and a short "talking" timer for the HUD.
## Something in hand: arms go forward. Owner sets it, synced so everyone sees the pose.
var carrying := false
## Scrap carried, by slot: loot node names, "" = empty. Owner sets; synced (hand model, ghost).
var slots: Array = []
## Active slot (the one in hand).
var slot := 0
## Sprint stamina 0..1 (owner only).
var stamina := 1.0
var _hand: Node3D
var _hand_item := ""
var _swing := 0.0
var heard_chunks := 0
var talk_t := 0.0
var _talk_icon := Label3D.new()
var _walked := 0.0
var _last_pos := Vector3.ZERO
## Flashlight on/off, owned by this player, synced so others see the beam.
var flash_on := true
## Flashlight charge 0..1, owner only (others just see flash_on). Full again each morning.
var battery := 1.0
const FLASH_ENERGY := 4.5
## Knocked out by the ghost: can't move or pick up. Synced so others see us lying down.
var stunned := false
var _stun_left := 0.0
## Dead until the next day (ghost, or left behind by the truck). The owner spectates a
## living teammate. Synced so everyone sees who is gone.
var dead := false
## Radio mode (Tab at the truck): the map is up, we stand still, and our voice reaches
## everyone at full volume — and theirs reaches us. Synced so others hear us over the radio.
var on_radio := false
## A dust ghost sits on our head: the screen is black until a teammate clicks us. Synced so
## teammates can tell (and the statue knows we can't watch it).
var blinded := false


func _init() -> void:
	var t := Tuning.data
	slots.resize(int(t.slots))
	slots.fill("")
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
	flashlight.spot_range = 18.0
	flashlight.spot_angle = 28.0
	flashlight.spot_attenuation = 0.8
	flashlight.light_energy = FLASH_ENERGY
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
	_steps.stream = Sfx.footsteps(false)
	_steps.unit_size = 4.0
	_steps.max_polyphony = 2
	add_child(_steps)
	ray.target_position = Vector3(0, 0, -t.reach)
	cam.add_child(ray)

	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", "Head:rotation", ".:stunned", ".:dead", ".:on_radio", ".:blinded", ".:flash_on", ".:carrying", ".:slots", ".:slot"]:
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


## Walk cycle from ground speed; arms forward while carrying. Pure visuals, runs on every peer.
func _animate(speed: float, delta: float) -> void:
	if is_multiplayer_authority():
		carrying = active_item() != ""
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


## Called on the owning peer when knocked out; `sec` = INF means dead for the day.
func stun(sec: float) -> void:
	_stun_left = sec
	stunned = true
	if is_inf(sec) and not dead:
		dead = true
		clear_slots()  # the host drops whatever we carried where we fell
		if is_inside_tree():
			_main()._result.text = "죽었습니다 — 동료 화면을 관전합니다. 다음 날 다시 출근해요."


## Called on the owning peer when a new day starts; no old stun or death carries over.
func recover() -> void:
	stunned = false
	_stun_left = 0.0
	dead = false
	on_radio = false
	blinded = false
	battery = 1.0
	flash_on = true
	stamina = 1.0
	clear_slots()
	carrying = false
	cam.current = is_multiplayer_authority()


## Dead owner: look through the first living teammate's camera (own body if nobody is left).
func _spectate() -> void:
	for p in get_tree().get_nodes_in_group("players"):
		if p != self and not p.dead:
			if not p.cam.current:
				p.cam.current = true
			return
	cam.current = true


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
			_steps.stream = Sfx.footsteps(global_position.y < -20.0)  # the mansion inside sits far below the yard
			_steps.play()
	if is_multiplayer_authority():
		cam.position.y = sin(_walked / STEP_LENGTH * PI) * 0.035  # head bob, one dip per step
	if is_multiplayer_authority() and dead and is_inside_tree():
		_spectate()
	if is_multiplayer_authority() and on_radio and (dead or _main().run.phase != "day"):
		on_radio = false
	if not is_multiplayer_authority() and is_inside_tree():
		_apply_radio_voice()
	if is_multiplayer_authority() and is_inside_tree():
		ray.force_raycast_update()
		_main().set_aim(null if stunned or dead else ray.get_collider())
	if is_inside_tree():
		_update_hand()
		if is_multiplayer_authority():
			_update_weapon(delta)
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
	elif event is InputEventMouseButton and event.pressed:
		if not captured:
			if event.button_index == MOUSE_BUTTON_LEFT and _main().run.phase == "day":
				Input.mouse_mode = Input.MOUSE_MODE_CAPTURED  # between days the mouse is for the shop
		elif event.button_index == MOUSE_BUTTON_LEFT:
			interact()
		elif event.button_index == MOUSE_BUTTON_RIGHT:
			_main().scan()
		elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
			switch_slot(slot - 1)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			switch_slot(slot + 1)
	elif event.is_action_pressed("interact"):
		interact()
	elif event.is_action_pressed("drop"):
		drop_active()
	elif event.is_action_pressed("flashlight"):
		flash_on = not flash_on and battery > 0.0
		_main().flash_note("손전등 켬" if flash_on else ("배터리 없음 — 트럭에서 충전" if battery <= 0.0 else "손전등 끔"))
	elif event.is_action_pressed("use_tape"):
		_main().request_tape()
	elif event.is_action_pressed("swing"):
		_main().request_swing()
		_swing_t = SWING_TIME
	elif event.is_action_pressed("flashbang"):
		_main().request_flashbang()
	elif event.is_action_pressed("radio"):
		set_radio(not on_radio)
	elif event.is_action_pressed("lure") and on_radio:
		_main().request_lure()
	elif event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	elif event is InputEventKey and event.pressed and not event.echo \
			and event.physical_keycode >= KEY_1 and event.physical_keycode < KEY_1 + slots.size():
		switch_slot(event.physical_keycode - KEY_1)


func _physics_process(delta: float) -> void:
	if not is_multiplayer_authority():
		return
	var t := Tuning.data
	if is_inside_tree() and _main().run.phase == "day":
		update_battery(delta, _main().at_truck(global_position))

	if stunned or on_radio:
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
	var sprinting := Input.is_action_pressed("sprint") and input != Vector2.ZERO and stamina > 0.0
	update_stamina(delta, sprinting)
	var speed: float = t.walk_speed * carry_factor() * (t.sprint_mult if sprinting else 1.0)
	velocity.x = dir.x * speed
	velocity.z = dir.z * speed
	move_and_slide()


## Owner: radio mode only at the truck, alive, during a day. Turning it on lets go of things.
func set_radio(on: bool) -> void:
	var m := _main()
	on_radio = on and not dead and m.run.phase == "day" and m.at_truck(global_position)


## Remote copy: play this player's voice flat and unlimited when either side is on the radio.
func _apply_radio_voice() -> void:
	var me: Node = _main().players.get_node_or_null(str(multiplayer.get_unique_id()))
	var walkie: bool = "walkie" in _main().run.get("owned", [])  # the team bought walkie-talkies
	var radio: bool = on_radio or (me != null and me.on_radio) or walkie
	_speaker.attenuation_model = AudioStreamPlayer3D.ATTENUATION_DISABLED if radio else AudioStreamPlayer3D.ATTENUATION_INVERSE_DISTANCE
	_speaker.max_distance = 0.0 if radio else 25.0  # 0 = no limit
	_speaker.panning_strength = 0.0 if radio else 1.0


## Owner: a lit flashlight drains the battery; standing at the truck recharges it. Empty =
## the light goes out and won't turn on. The beam dims over the last `dim_below` of charge.
func update_battery(delta: float, at_truck: bool) -> void:
	var c: Dictionary = Tuning.data.flashlight
	if at_truck:
		battery = minf(1.0, battery + delta / c.recharge_seconds)
	elif flash_on:
		var bonus: float = _main().run.get("battery_bonus", 0.0) if is_inside_tree() else 0.0
		var ghost: Node = _main().ghost if is_inside_tree() else null
		var drain: float = c.ghost_drain if ghost and ghost.lit_by(self) else 1.0  # holding her off costs light
		battery = maxf(0.0, battery - drain * delta / (c.battery_seconds * (1.0 + bonus)))
	if battery <= 0.0:
		flash_on = false
	flashlight.light_energy = FLASH_ENERGY * clampf(battery / c.dim_below, 0.15, 1.0)


## Owner: sprinting drains stamina (empty = no sprint until it refills a bit); resting refills.
func update_stamina(delta: float, sprinting: bool) -> void:
	var t := Tuning.data
	if sprinting:
		stamina = maxf(0.0, stamina - delta / t.stamina_seconds)
	else:
		stamina = minf(1.0, stamina + delta / t.stamina_regen_seconds)


## Walk speed multiplier: everything carried weighs you down; shoes from the shop help.
func carry_factor() -> float:
	var t := Tuning.data
	var f: float = clampf(1.0 - carried_weight() / t.slow_weight, t.min_weight_speed, 1.0)
	return f * (1.0 + float(_main().run.get("speed_bonus", 0.0)) if is_inside_tree() else f)


## kg of scrap in every slot.
func carried_weight() -> float:
	var kg := 0.0
	for n in slots:
		var it := _item(n)
		if it:
			kg += it.weight
	return kg


## Loot node name in hand, or "".
func active_item() -> String:
	return slots[slot] if slot < slots.size() else ""


func _item(item_name: String) -> Loot:
	if item_name == "" or not is_inside_tree():
		return null
	return _main().loot.get_node_or_null(item_name) as Loot


## Owner: hand to slot `i` (wraps). A two-handed item can't be switched away from.
func switch_slot(i: int) -> void:
	var it := _item(active_item())
	if it and it.two_handed:
		_main().flash_note("양손 짐이에요 — G로 내려놔야 바꿀 수 있어요")
		return
	slot = posmod(i, slots.size())


## Owner: E / left click on what we're looking at.
func interact() -> void:
	ray.force_raycast_update()
	var hit := ray.get_collider()
	var m := _main()
	if hit == null:
		return
	if hit.name == "DepartButton":
		m.request_depart()
	elif hit.has_meta("door"):
		use_door(hit.get_meta("door"))
	elif hit.has_meta("container"):
		m.request_search(hit.get_meta("container"))
	elif hit.has_meta("vault"):
		m.request_unlock()
	elif hit is CharacterBody3D and hit.get("blinded"):
		m.request_free(hit.name.to_int())  # pull the dust ghost off a teammate's head
	elif hit is Loot:
		var held := _item(active_item())
		if held and held.two_handed:
			m.flash_note("양손이 다 찼어요 — G로 내려놓고 주워요")
		elif not slots.has(""):
			m.flash_note("짐 칸이 꽉 찼어요 (최대 %d개) — 트럭에 내려놔요" % slots.size())
		elif hit.two_handed and active_item() != "":
			m.flash_note("양손 짐이에요 — 빈 칸(1~4, 휠)으로 바꾸고 주워요")
		else:
			m.request_pickup(hit.name)


## Owner: go through door `i` of today's mansion (front door / fire exit, either side).
func use_door(i: int) -> void:
	var dd: Dictionary = _main().interior.doors[i]
	global_position = dd.to
	rotation.y = dd.yaw
	velocity = Vector3.ZERO
	_main().broadcast_door()


## Owner: the host gave us `item_name` — into the hand slot if it's free, else the first free one.
func take(item_name: String) -> void:
	var s := slots.duplicate()
	var i := slot if s[slot] == "" else s.find("")
	if i < 0:
		return
	s[i] = item_name
	slots = s
	slot = i


## Owner: G — put the item in hand down in front of us.
func drop_active() -> void:
	var n := active_item()
	if n == "":
		return
	var s := slots.duplicate()
	s[slot] = ""
	slots = s
	var fwd := -global_basis.z * Vector3(1, 0, 1)
	_main().request_drop(n, global_position + fwd.normalized() * 0.7 + Vector3.UP * 0.3)


func clear_slots() -> void:
	var s := slots.duplicate()
	s.fill("")
	slots = s


## Everyone: show the active item's model in hand (first person for the owner, at the
## chest for everyone else). Rebuilt only when the item in hand changes.
## Owner view: the weapon (crowbar, or the rusty spade once the team owns one) sweeps across
## the screen for a moment when you press R.
func _update_weapon(delta: float) -> void:
	_swing_t = maxf(0.0, _swing_t - delta)
	var spade: bool = "shovel" in _main().run.get("owned", [])
	var want := "rusted_spade_01" if spade else "crowbar_01"
	if _weapon == null or _weapon.name != want:
		if _weapon:
			_weapon.queue_free()
		_weapon = (load("res://assets/models/polyhaven/%s/%s.gltf" % [want, want]) as PackedScene).instantiate()
		_weapon.name = want
		_weapon.scale = Vector3.ONE * (0.7 if spade else 1.1)
		cam.add_child(_weapon)
	_weapon.visible = _swing_t > 0.0
	var k := 1.0 - _swing_t / SWING_TIME  # 0 → 1 over the swing
	_weapon.position = Vector3(lerpf(0.55, -0.15, k), lerpf(-0.05, -0.45, k), -0.6)
	_weapon.rotation = Vector3(lerpf(0.3, -1.4, k), 0.4, lerpf(0.9, -0.2, k))


func _update_hand() -> void:
	var n := active_item() if not dead else ""
	if n == _hand_item:
		return
	_hand_item = n
	if _hand:
		_hand.queue_free()
		_hand = null
	var it := _item(n)
	if it == null:
		return
	_hand = Models.scrap(it.kind, it.color)
	var big := maxf(it.size.x, maxf(it.size.y, it.size.z))
	if is_multiplayer_authority():
		_hand.scale = Vector3.ONE * minf(1.0, 0.24 / big)
		# low and to the right, outside the flashlight cone (or it glares white)
		_hand.position = Vector3(0.42, -0.4, -0.55) if not it.two_handed else Vector3(0, -0.5, -0.6)
		cam.add_child(_hand)
	else:
		_hand.position = Vector3(0, 1.1, -0.45)
		add_child(_hand)


## Main is our grandparent (Main/Players/<id>); works in tests where current_scene is null.
func _main() -> Node:
	return get_parent().get_parent()
