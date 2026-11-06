extends Node3D
## The game: networking, player spawning, co-op grab physics, and the 3-day run.
## The host simulates every piece of furniture; each player sends where it wants
## its grab point to be, and the host pulls it there with a capped spring force.
## A capped force is the whole weight rule: one player can't out-pull gravity on
## heavy furniture, two can.
##
## User args (after `--`): --host [--no-upnp] [--port=N] | --join [ip[:port]] | --join-lan | --demo-grab | --screenshot=<path> [--shot-delay=s] | --demo-load | --cam=x,y,z,yaw,pitch

const Tuning = preload("res://src/tuning.gd")
const Player = preload("res://src/player.gd")
const Furniture = preload("res://src/furniture.gd")
const Truck = preload("res://src/truck.gd")
const House = preload("res://src/house.gd")
const Ghost = preload("res://src/ghost.gd")
const RunState = preload("res://src/run_state.gd")
const Sfx = preload("res://src/sfx.gd")
const Net = preload("res://src/net.gd")
const UiTheme = preload("res://src/ui_theme.gd")

## Host: everyone is knocked out at once — the day is lost (story 005 ends the day).
signal day_failed
const DEFAULT_PORT := 7777
## Game port; `--port=N` overrides it (e.g. test copies next to a running game).
var port := DEFAULT_PORT

## peer id -> {body, local, target, t}. Host only.
var grabs := {}
var furniture := Node3D.new()
var players := Node3D.new()
var spawner := MultiplayerSpawner.new()
var truck := Truck.new()
var ghost := Ghost.new()
## Host: peer id -> msec when their stun ends.
var stun_until := {}
## The run (see run_state.gd). Host owns it; clients get copies via _rpc_run.
var run: Dictionary = RunState.new_run(Tuning.data.run)
var _vhs: ShaderMaterial
var _status := Label.new()
var _shop := PanelContainer.new()
var _shop_buttons := {}
var _next_btn := Button.new()
var _sync_t := 0.0
var _music := AudioStreamPlayer.new()
var net := Net.new()
var _addr := Label.new()
var _lan_box := VBoxContainer.new()
var _lan_seen := ""
var _auto_join_lan := false
var _pause := PanelContainer.new()
var _shooting := false  # --screenshot: keep the Esc menu out of the picture
var _sfx := {}
var _last_thud := 0
var _menu: Control
var _hint := Label.new()
var _result := Label.new()
var _aim := Label.new()


func _ready() -> void:
	# Controls under CanvasLayers don't inherit a Window theme, so merge into the fallback theme.
	ThemeDB.get_default_theme().merge_with(UiTheme.build())
	_setup_input()
	_build_room()
	truck.name = "Truck"
	add_child(truck)
	ghost.name = "Ghost"
	ghost.main = self
	var gs: Array = Tuning.data.ghost.start
	ghost.position = Vector3(gs[0], gs[1], gs[2])
	add_child(ghost)
	furniture.name = "Furniture"
	add_child(furniture)
	for d in Tuning.data.furniture:
		var f := Furniture.new()
		f.setup(d)
		f.bucked.connect(_on_bucked)
		f.hit.connect(_on_furniture_hit)
		furniture.add_child(f)
	players.name = "Players"
	add_child(players)
	spawner.name = "Spawner"
	add_child(spawner)
	spawner.spawn_path = spawner.get_path_to(players)
	spawner.spawn_function = _spawn_player
	_build_hud()
	_build_audio()
	day_failed.connect(_on_day_failed)
	_apply_run(run)

	net.name = "Net"
	add_child(net)
	net.wan_ready.connect(_on_wan_ready)
	var args := OS.get_cmdline_user_args()
	for arg in args:
		if arg.begins_with("--port="):
			port = arg.trim_prefix("--port=").to_int()
	if "--host" in args:
		host(not "--no-upnp" in args)
	elif "--join" in args:
		var i := args.find("--join")
		join(args[i + 1] if i + 1 < args.size() and not args[i + 1].begins_with("--") else "127.0.0.1")
	elif "--join-lan" in args:
		_auto_join_lan = true
		net.start_listening()
	elif DisplayServer.get_name() != "headless":
		_build_menu()
	for a in args:
		if a.begins_with("--cam="):
			_place_cam(a.trim_prefix("--cam=").split_floats(","))
	if "--demo-load" in args:
		_demo_load()
	var delay := 4.0
	for a in args:
		if a.begins_with("--shot-delay="):
			delay = a.trim_prefix("--shot-delay=").to_float()
	for a in args:
		if a.begins_with("--screenshot="):
			_shooting = true
			_screenshot_later(a.trim_prefix("--screenshot="), delay)


## Hosts on `port`, announces itself on the LAN, and (unless `upnp` is false) asks the
## router to open the port for friends over the internet.
func host(upnp := true) -> void:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_server(port, 4)
	if err != OK:
		_hint.text = "호스트 실패: 포트 %d를 이미 쓰는 중 — 이 게임이 이미 켜져 있나요? (%s)" % [port, error_string(err)]
		return
	multiplayer.multiplayer_peer = peer
	multiplayer.peer_connected.connect(_on_peer_joined)
	multiplayer.peer_disconnected.connect(_on_peer_left)
	spawner.spawn(1)
	_close_menu()
	run = RunState.new_run(Tuning.data.run)  # the clock ran behind the menu; start fresh
	_push_run()
	net.start_beacon(port)
	var lan := Net.local_ips()
	_addr.text = "같은 와이파이 친구: %s\n인터넷 친구: %s" % [
		", ".join(lan.map(func(a: String) -> String: return "%s:%d" % [a, port])) if not lan.is_empty() else "(없음)",
		"공유기 포트 여는 중…" if upnp else "꺼짐 (--no-upnp)"]
	if upnp:
		net.open_wan(port)


func _on_wan_ready(address: String, error: String) -> void:
	var lines := _addr.text.split("\n")
	lines[1] = "인터넷 친구: " + (address if address != "" else error)
	_addr.text = "\n".join(lines)


## Joins "ip" or "ip:port" (LAN address, or the host's internet address).
func join(address: String) -> void:
	var a := Net.parse_address(address, port)
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(a[0], a[1])
	if err != OK:
		_hint.text = "참가 실패: %s" % error_string(err)
		return
	net.stop_listening()
	multiplayer.multiplayer_peer = peer
	for f in furniture.get_children():
		f.freeze = true  # host owns the physics; we only display it
	multiplayer.server_disconnected.connect(func() -> void:
		_hint.text = "호스트와 연결이 끊김 — Esc 메뉴에서 \"메뉴로 나가기\""
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE)
	_close_menu()


func _spawn_player(id: int) -> Node:
	var p := Player.new()
	p.name = str(id)
	p.set_multiplayer_authority(id)
	# Spawns face each other across the sofa.
	var left := players.get_child_count() % 2 == 0
	p.position = Vector3(-2.5 if left else 2.5, 0, 3.0)
	p.rotation.y = -PI / 2 if left else PI / 2
	return p


func _on_peer_joined(id: int) -> void:
	spawner.spawn(id)
	_rpc_run.rpc_id(id, run)


func _on_day_failed() -> void:
	if run.phase == "day":
		_end_day(0, "전원 기절")


func _on_peer_left(id: int) -> void:
	grabs.erase(id)
	var p := players.get_node_or_null(str(id))
	if p:
		p.queue_free()


# --- grab protocol -----------------------------------------------------------

## Called by the local player every physics frame while it holds something.
func send_grab(body_name: String, local_point: Vector3, target: Vector3) -> void:
	if multiplayer.is_server():
		hold(multiplayer.get_unique_id(), body_name, local_point, target)
	else:
		_rpc_grab.rpc_id(1, body_name, local_point, target)


func send_release() -> void:
	if multiplayer.is_server():
		grabs.erase(multiplayer.get_unique_id())
	else:
		_rpc_release.rpc_id(1)


@rpc("any_peer", "unreliable_ordered")
func _rpc_grab(body_name: String, local_point: Vector3, target: Vector3) -> void:
	if multiplayer.is_server():
		hold(multiplayer.get_remote_sender_id(), body_name, local_point, target)


@rpc("any_peer", "reliable")
func _rpc_release() -> void:
	if multiplayer.is_server():
		grabs.erase(multiplayer.get_remote_sender_id())


## True on the host (or offline) once its peer is up: the side that runs physics and AI.
func is_host_active() -> bool:
	return multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED \
		and multiplayer.is_server()


func is_stunned(id: int) -> bool:
	return stun_until.get(id, 0) > Time.get_ticks_msec()


## Host: knock out peer `id` for `sec` seconds. Everyone out at once fails the day.
func stun_player(id: int, sec: float) -> void:
	stun_until[id] = Time.get_ticks_msec() + int(sec * 1000)
	broadcast_sfx("whoosh")
	force_release(id)
	if id == multiplayer.get_unique_id():
		_stun_local(sec)
	elif id in multiplayer.get_peers():
		_rpc_stun.rpc_id(id, sec)
	var all_out := players.get_child_count() > 0
	for p in players.get_children():
		all_out = all_out and is_stunned(p.name.to_int())
	if all_out:
		announce("전원 기절 — 오늘은 실패!")
		day_failed.emit()


@rpc("authority", "reliable")
func _rpc_stun(sec: float) -> void:
	_stun_local(sec)


func _stun_local(sec: float) -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.stun(sec)


## Host: show a message on every screen.
func announce(text: String) -> void:
	_result.text = text
	if not multiplayer.get_peers().is_empty():
		_rpc_announce.rpc(text)


@rpc("authority", "reliable")
func _rpc_announce(text: String) -> void:
	_result.text = text


## Screen static grows as the ghost gets close to our camera.
func _process(_delta: float) -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	var amount := 0.0
	if me and ghost.visible and run.phase == "day":
		var d: float = me.cam.global_position.distance_to(ghost.global_position)
		amount = clampf(1.0 - d / Tuning.data.ghost.noise_range, 0.0, 1.0)
	_vhs.set_shader_parameter("noise", amount)
	# Esc menu: whenever the mouse is free during play (no menu, no shop). The game keeps running.
	_pause.visible = _menu == null and run.phase == "day" and players.get_child_count() > 0 \
		and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE and DisplayServer.get_name() != "headless" \
		and not _shooting
	_refresh_lan()
	_music.pitch_scale = Sfx.music_pitch(amount, Time.get_ticks_msec() / 1000.0)


## What the local player is looking at (set by player.gd each frame).
func set_aim(target: Object) -> void:
	if target == null:
		_aim.text = ""
	elif target is Node and target.name == "DepartButton":
		_aim.text = "출발 버튼 — 클릭하면 오늘 일 끝"
	elif target is Furniture:
		var f: Furniture = target
		var lift_kg: float = Tuning.data.player_strength * run.strength_mult / 9.8
		var parts := ["%s · %dkg · %d원" % [f.label, roundi(f.mass), roundi(f.value * f.durability)]]
		if f.mass > lift_kg:
			parts.append("둘이 들어야 함")
		if f.curse == "runaway":
			parts.append("가까이 가면 도망감")
		elif f.curse == "buck":
			parts.append("들면 들썩임")
		if f.durability < 1.0:
			parts.append("파손 %d%%" % roundi((1.0 - f.durability) * 100))
		_aim.text = " · ".join(parts)
	else:
		_aim.text = ""


## Host: a cursed piece shook everyone off.
func _on_bucked(body: RigidBody3D) -> void:
	broadcast_sfx("thud")
	for id in grabs.keys():
		if grabs[id].body == body:
			force_release(id)


## Host: make peer `id` let go (their client drops it too).
func force_release(id: int) -> void:
	grabs.erase(id)
	if id == multiplayer.get_unique_id():
		_drop_local()
	elif id in multiplayer.get_peers():
		_rpc_dropped.rpc_id(id)


@rpc("authority", "reliable")
func _rpc_dropped() -> void:
	_drop_local()


func _drop_local() -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.held = null


## Host: peer `id` wants `local_point` of `body_name` at `target` (world).
func hold(id: int, body_name: String, local_point: Vector3, target: Vector3) -> void:
	var body := furniture.get_node_or_null(body_name)
	if body:
		grabs[id] = {body = body, local = local_point, target = target, t = Time.get_ticks_msec()}


func _physics_process(delta: float) -> void:
	if not is_host_active():
		return
	var t := Tuning.data
	if run.phase == "day":
		run.time_left -= delta
		if run.time_left <= 0.0:
			_end_day(0, "시간 초과")
		_sync_t += delta
		if _sync_t >= 1.0:  # clients only need the clock once a second
			_sync_t = 0.0
			_push_run()
	var now := Time.get_ticks_msec()
	var counts := {}
	for id in grabs.keys():
		var g: Dictionary = grabs[id]
		# A stale unreliable packet can land after a release; the timeout drops it.
		if now - g.t > t.grab_timeout_ms or not is_instance_valid(g.body):
			grabs.erase(id)
			continue
		var b: RigidBody3D = g.body
		var p := b.to_global(g.local)
		var r := p - b.global_position
		var v := b.linear_velocity + b.angular_velocity.cross(r)
		var f: Vector3 = (g.target - p) * t.grab_spring - v * t.grab_damping
		b.apply_force(f.limit_length(t.player_strength * run.strength_mult), r)
		b.apply_torque(-b.angular_velocity * t.grab_angular_damping)
		b.sleeping = false
		counts[b] = counts.get(b, 0) + 1
	for b in furniture.get_children():
		b.holders = counts.get(b, 0)


# --- depart ------------------------------------------------------------------

## Anyone can press the button; the host scores the truck and tells everyone.
func request_depart() -> void:
	if multiplayer.is_server():
		_depart()
	else:
		_rpc_depart.rpc_id(1)


@rpc("any_peer", "reliable")
func _rpc_depart() -> void:
	if multiplayer.is_server():
		_depart()


## Host: score the truck and end the day with that pay. Only during a day.
func _depart() -> Dictionary:
	if run.phase != "day":
		return {}
	var r := truck.evaluate(furniture.get_children())
	broadcast_sfx("horn")
	var text := "출발! 실은 가구 %d개 · 파손 %d개 · 기본 %d원 + 적재 보너스 %d원 (빈틈 없이 %d%%) = %d원\n" % [
		r.loaded, r.broken, r.base, r.bonus, roundi(r.efficiency * 100), r.total]
	_end_day(r.total, "", text)
	return r


# --- run: days, shop, reset --------------------------------------------------

## Host: close the day with `earned`, tell everyone, open the shop (or end the run).
func _end_day(earned: int, why := "", prefix := "") -> void:
	run = RunState.end_day(run, earned, Tuning.data.run, why)
	for id in grabs.keys():
		force_release(id)
	announce(prefix + run.text)
	_push_run()


func _push_run() -> void:
	_apply_run(run)
	if not multiplayer.get_peers().is_empty():
		_rpc_run.rpc(run)


@rpc("authority", "reliable")
func _rpc_run(r: Dictionary) -> void:
	_apply_run(r)


## Everyone: show the run on the HUD and the shop between days.
func _apply_run(r: Dictionary) -> void:
	run = r
	var cfg: Dictionary = Tuning.data.run
	var secs := maxi(0, ceili(r.time_left))
	var clock := "남은 시간 %d:%02d" % [secs / 60, secs % 60] if r.phase == "day" else "상점"
	_status.text = "%d일차 / %d · %s · 할당량 %d원 · 팀 돈 %d원 · 미달 %d/%d · 퇴마 테이프 %d (E)" % [
		r.day, cfg.quotas.size(), clock, RunState.quota(r, cfg), r.money,
		r.misses, cfg.max_misses, r.tapes]
	var open: bool = r.phase != "day"
	_shop.visible = open
	for item in _shop_buttons:
		var b: Button = _shop_buttons[item]
		var it: Dictionary = cfg.shop[item]
		var once_owned: bool = item in r.owned and not it.get("stack", false)
		b.text = "%s  (%d원)%s" % [it.label, it.price, "  ✓" if once_owned else ""]
		b.disabled = r.phase != "shop" or r.money < it.price or once_owned
	_next_btn.text = "새 런 시작 →" if r.phase == "over" else "%d일차 시작 →" % r.day
	if open and DisplayServer.get_name() != "headless":
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func request_buy(item: String) -> void:
	if multiplayer.is_server():
		_buy(item)
	else:
		_rpc_buy.rpc_id(1, item)


@rpc("any_peer", "reliable")
func _rpc_buy(item: String) -> void:
	if multiplayer.is_server():
		_buy(item)


func _buy(item: String) -> void:
	if RunState.buy(run, item, Tuning.data.run):
		broadcast_sfx("coin")
		_push_run()


func request_next_day() -> void:
	if multiplayer.is_server():
		next_day()
	else:
		_rpc_next_day.rpc_id(1)


@rpc("any_peer", "reliable")
func _rpc_next_day() -> void:
	if multiplayer.is_server():
		next_day()


## Host: from the shop, start the next day; from "over", start a new run. Resets the house.
func next_day() -> void:
	if run.phase == "day":
		return
	if run.phase == "over":
		run = RunState.new_run(Tuning.data.run)
	else:
		RunState.start_day(run, Tuning.data.run)
	_reset_house()
	announce("")
	_push_run()


func _reset_house() -> void:
	grabs.clear()
	stun_until.clear()
	for d in Tuning.data.furniture:
		var f: RigidBody3D = furniture.get_node(NodePath(d.name))
		f.global_position = Vector3(d.pos[0], d.pos[1], d.pos[2])
		f.rotation = Vector3.ZERO
		f.linear_velocity = Vector3.ZERO
		f.angular_velocity = Vector3.ZERO
		f.durability = 1.0
	var gs: Array = Tuning.data.ghost.start
	ghost.global_position = Vector3(gs[0], gs[1], gs[2])
	ghost.exorcise(0.0)
	_respawn_local()
	if not multiplayer.get_peers().is_empty():
		_rpc_respawn.rpc()


@rpc("authority", "reliable")
func _rpc_respawn() -> void:
	_respawn_local()


## Each peer moves its own player back to its spawn spot.
func _respawn_local() -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		var left := me.get_index() % 2 == 0
		me.global_position = Vector3(-2.5 if left else 2.5, 0, 3.0)
		me.velocity = Vector3.ZERO
		me.held = null


## Exorcism tape: the local player pressed E.
func request_tape() -> void:
	if multiplayer.is_server():
		use_tape(multiplayer.get_unique_id())
	else:
		_rpc_tape.rpc_id(1)


@rpc("any_peer", "reliable")
func _rpc_tape() -> void:
	if multiplayer.is_server():
		use_tape(multiplayer.get_remote_sender_id())


## Host: spend a tape if peer `id` is close enough to the visible ghost. True if used.
func use_tape(id: int) -> bool:
	var p := players.get_node_or_null(str(id))
	var cfg: Dictionary = Tuning.data.run
	if p == null or run.tapes <= 0 or not ghost.visible or run.phase != "day":
		return false
	if p.global_position.distance_to(ghost.global_position) > cfg.tape_range:
		return false
	run.tapes -= 1
	ghost.exorcise(cfg.tape_seconds)
	_push_run()
	return true


# --- audio --------------------------------------------------------------------

func _build_audio() -> void:
	_music.stream = Sfx.music()
	_music.volume_db = -8.0
	add_child(_music)
	_music.play()
	var streams := {thud = Sfx.thud(), horn = Sfx.horn(), whoosh = Sfx.whoosh(), coin = Sfx.coin()}
	for n in streams:
		var p := AudioStreamPlayer.new()
		p.stream = streams[n]
		add_child(p)
		_sfx[n] = p


## Host: play an effect on every screen.
func broadcast_sfx(sfx_name: String) -> void:
	play_sfx(sfx_name)
	if not multiplayer.get_peers().is_empty():
		_rpc_sfx.rpc(sfx_name)


@rpc("authority", "unreliable")
func _rpc_sfx(sfx_name: String) -> void:
	play_sfx(sfx_name)


func play_sfx(sfx_name: String) -> void:
	_sfx[sfx_name].play()


## Host: furniture took damage. Throttled so a tumbling sofa doesn't machine-gun.
func _on_furniture_hit() -> void:
	var now := Time.get_ticks_msec()
	if now - _last_thud > 150:
		_last_thud = now
		broadcast_sfx("thud")


# --- room, HUD, menu ---------------------------------------------------------

func _build_room() -> void:
	var sun := DirectionalLight3D.new()  # low dusk sun
	sun.rotation = Vector3(-0.35, 2.4, 0)
	sun.light_color = Color("#ff9a6a")
	sun.light_energy = 0.8
	sun.shadow_enabled = true
	add_child(sun)
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()  # dusk: violet top, burnt-orange horizon
	sky_mat.sky_top_color = Color("#2a2347")
	sky_mat.sky_horizon_color = Color("#d9785a")
	sky_mat.ground_horizon_color = Color("#4a3040")
	sky_mat.ground_bottom_color = Color("#1a1420")
	sky_mat.sun_angle_max = 20.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.16  # dark: the flashlight and bulbs do the work
	env.fog_enabled = true
	env.fog_light_color = Color("#6a4a5a")
	env.fog_density = 0.012
	env.fog_sky_affect = 0.3
	env.ssao_enabled = true       # contact shadows in corners and under furniture
	env.ssao_intensity = 1.6
	env.glow_enabled = true       # bulbs, windows and headlights bloom a little
	env.glow_intensity = 0.6
	env.glow_hdr_threshold = 1.2
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.1
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	var house := House.new()
	house.name = "House"
	add_child(house)
	# Menu camera: the house at dusk from the road; a player's camera takes over on spawn.
	var menu_cam := Camera3D.new()
	menu_cam.name = "MenuCam"
	add_child(menu_cam)
	menu_cam.look_at_from_position(Vector3(-9, 2.2, -13), Vector3(0, 2.0, 4), Vector3.UP)
	menu_cam.current = true

	# VHS overlay: above the 3D view, below the HUD (layer 1).
	var vhs_layer := CanvasLayer.new()
	vhs_layer.layer = -1
	var rect := ColorRect.new()
	rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var sm := ShaderMaterial.new()
	sm.shader = preload("res://src/canvas_post_vhs.gdshader")
	rect.material = sm
	_vhs = sm
	vhs_layer.add_child(rect)
	add_child(vhs_layer)



func _build_hud() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	var cross := Label.new()
	cross.text = "•"
	cross.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	cross.grow_horizontal = Control.GROW_DIRECTION_BOTH
	cross.grow_vertical = Control.GROW_DIRECTION_BOTH
	layer.add_child(cross)
	_aim.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_aim.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_aim.offset_top = 22
	_aim.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_aim.add_theme_font_size_override("font_size", 15)
	layer.add_child(_aim)
	_hint.text = "클릭: 잡기(누르고 있기) / 빨간 버튼: 출발 · E: 퇴마 테이프 · F: 손전등 · WASD 이동 · Space 점프 · Esc 마우스 풀기"
	_hint.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_hint.offset_top = -40
	_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	layer.add_child(_hint)
	_result.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	_result.offset_top = 40
	_result.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_result.add_theme_font_size_override("font_size", 26)
	_result.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_result.offset_left = 40
	_result.offset_right = -40
	layer.add_child(_result)
	_status.position = Vector2(16, 10)
	layer.add_child(_status)
	_addr.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	_addr.offset_left = 16
	_addr.offset_right = 520
	_addr.offset_top = -120
	_addr.offset_bottom = -48
	_addr.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_addr.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	_addr.add_theme_font_size_override("font_size", 14)
	layer.add_child(_addr)

	_shop.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	_shop.offset_top = -250
	_shop.offset_bottom = -56
	_shop.grow_vertical = Control.GROW_DIRECTION_BEGIN  # extra height goes up, clear of the hint
	_shop.offset_left = -260
	_shop.offset_right = 260
	var box := VBoxContainer.new()
	_shop.add_child(box)
	var title := Label.new()
	title.text = "상점 — 팀 돈으로 사기"
	box.add_child(title)
	for item in Tuning.data.run.shop:
		var b := Button.new()
		b.pressed.connect(request_buy.bind(item))
		box.add_child(b)
		_shop_buttons[item] = b
	_next_btn.pressed.connect(request_next_day)
	box.add_child(_next_btn)
	_shop.visible = false
	layer.add_child(_shop)
	_build_pause(layer)


func _build_menu() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	var panel := PanelContainer.new()
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	layer.add_child(panel)
	var box := VBoxContainer.new()
	box.custom_minimum_size = Vector2(380, 0)
	panel.add_child(box)
	_menu = panel
	_status.visible = false
	var title := Label.new()
	title.text = "유령집 이삿짐센터"
	title.add_theme_font_size_override("font_size", 40)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var sub := Label.new()
	sub.text = "가구는 무겁고, 집은 귀신 들렸다   v%s" % ProjectSettings.get_setting("application/config/version", "")
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sub.modulate = Color(1, 1, 1, 0.75)
	box.add_child(sub)
	var host_btn := Button.new()
	host_btn.text = "호스트"
	host_btn.pressed.connect(host)
	box.add_child(host_btn)
	var lan_title := Label.new()
	lan_title.text = "같은 와이파이에서 찾은 게임:"
	box.add_child(lan_title)
	_lan_box.add_child(_lan_placeholder())
	box.add_child(_lan_box)
	var ip := LineEdit.new()
	ip.placeholder_text = "호스트 주소 (예: 123.45.67.89:7777)"
	box.add_child(ip)
	var join_btn := Button.new()
	join_btn.text = "주소로 참가"
	join_btn.pressed.connect(func() -> void: join(ip.text))
	box.add_child(join_btn)
	var quit_btn := Button.new()
	quit_btn.text = "게임 종료"
	quit_btn.pressed.connect(get_tree().quit)
	box.add_child(quit_btn)
	if net.start_listening() != OK:
		_lan_box.get_child(0).text = "(이 컴퓨터에서 이미 찾는 중 — 주소로 참가)"


func _lan_placeholder() -> Label:
	var l := Label.new()
	l.text = "(찾는 중…)"
	return l


## Menu: keep the LAN host list current; --join-lan joins the first host heard.
func _refresh_lan() -> void:
	var keys: Array = net.found.keys()
	keys.sort()
	if _auto_join_lan and not keys.is_empty():
		_auto_join_lan = false
		join(keys[0])
		return
	if _menu == null or ",".join(keys) == _lan_seen:
		return
	_lan_seen = ",".join(keys)
	for c in _lan_box.get_children():
		c.queue_free()
	if keys.is_empty():
		_lan_box.add_child(_lan_placeholder())
	for k in keys:
		var b := Button.new()
		b.text = "참가: " + k
		b.pressed.connect(join.bind(k))
		_lan_box.add_child(b)


func _build_pause(layer: CanvasLayer) -> void:
	_pause.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_pause.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_pause.grow_vertical = Control.GROW_DIRECTION_BOTH
	_pause.custom_minimum_size = Vector2(300, 0)
	var box := VBoxContainer.new()
	_pause.add_child(box)
	var note := Label.new()
	note.text = "온라인이라 게임은 계속 돌아가요"
	box.add_child(note)
	for item in [["계속하기", func() -> void: Input.mouse_mode = Input.MOUSE_MODE_CAPTURED],
			["전체 화면 (F11)", _toggle_fullscreen],
			["메뉴로 나가기", _leave_to_menu],
			["게임 종료", get_tree().quit]]:
		var b := Button.new()
		b.text = item[0]
		b.pressed.connect(item[1])
		box.add_child(b)
	_pause.visible = false
	layer.add_child(_pause)


func _toggle_fullscreen() -> void:
	var full := DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_FULLSCREEN
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED if full else DisplayServer.WINDOW_MODE_FULLSCREEN)


func _unhandled_key_input(event: InputEvent) -> void:
	if event.is_action_pressed("fullscreen"):
		_toggle_fullscreen()


## Drops the connection (the router port mapping is removed on exit) and reloads to the menu.
func _leave_to_menu() -> void:
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	get_tree().reload_current_scene()


func _close_menu() -> void:
	_status.visible = true
	if _menu:
		_menu.get_parent().queue_free()
		_menu = null


## --cam=x,y,z,yaw,pitch (degrees): move our player there after spawning (for screenshots).
func _place_cam(v: PackedFloat64Array) -> void:
	await get_tree().create_timer(0.5).timeout
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.global_position = Vector3(v[0], v[1], v[2])
		me.rotation.y = deg_to_rad(v[3])
		me.head.rotation.x = deg_to_rad(v[4])


## --demo-load: put three pieces on the truck, face it, and depart (for screenshots).
func _demo_load() -> void:
	await get_tree().create_timer(1.0).timeout
	var c := truck.bed.get_center()
	var i := 0
	for n in ["chair", "lamp", "fridge"]:
		var f: RigidBody3D = furniture.get_node(n)
		f.global_position = Vector3(c.x - 1.0 + i, f.size.y / 2 + 0.05, c.z + 1.0)
		f.rotation = Vector3.ZERO
		f.linear_velocity = Vector3.ZERO
		i += 1
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.global_position = Vector3(0, 0, -0.8)
		me.rotation.y = 0.0
		me.head.rotation.x = -0.35
	await get_tree().create_timer(1.5).timeout
	_depart()


func _setup_input() -> void:
	var keys := {move_forward = KEY_W, move_back = KEY_S, move_left = KEY_A, move_right = KEY_D, jump = KEY_SPACE, use_tape = KEY_E, fullscreen = KEY_F11, flashlight = KEY_F}
	for action in keys:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		var ev := InputEventKey.new()
		ev.physical_keycode = keys[action]
		InputMap.action_add_event(action, ev)


func _screenshot_later(path: String, delay: float) -> void:
	await get_tree().create_timer(delay).timeout
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(path)
	print("screenshot: ", path)
	get_tree().quit()
