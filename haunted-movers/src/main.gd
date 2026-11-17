extends Node3D
## The game: networking, player spawning, the scrap loop and the quota run.
## Each day the truck parks at a haunted mansion whose inside (interior.gd) is different
## every day. Movers go in, pick up scrap (up to 4 each, E), bring it back to the truck
## (G to put it down) and leave before the clock runs out. The host owns every piece of
## scrap: clients ask to pick up / drop and the host decides.
##
## User args (after `--`): --host [--no-upnp] [--port=N] | --join [ip[:port]] | --join-lan | --screenshot=<path> [--shot-delay=s] | --demo-load | --cam=x,y,z,yaw,pitch | --settings | --day=N | --seed=N | --elapsed=s | --radio | --monster-lineup | --inside | --scan

const Tuning = preload("res://src/tuning.gd")
const Player = preload("res://src/player.gd")
const Loot = preload("res://src/loot.gd")
const Models = preload("res://src/models.gd")
const Truck = preload("res://src/truck.gd")
const House = preload("res://src/house.gd")
const RadioMap = preload("res://src/radio_map.gd")
const Interior = preload("res://src/interior.gd")
const Monster = preload("res://src/monster.gd")
const Ghost = preload("res://src/ghost.gd")
const RunState = preload("res://src/run_state.gd")
const Sfx = preload("res://src/sfx.gd")
const Net = preload("res://src/net.gd")
const UiTheme = preload("res://src/ui_theme.gd")
const Settings = preload("res://src/settings.gd")
const Tips = preload("res://src/tips.gd")
const Voice = preload("res://src/voice.gd")

## Host: everyone is knocked out at once — the day is lost (story 005 ends the day).
signal day_failed
const DEFAULT_PORT := 7777
## Game port; `--port=N` overrides it (e.g. test copies next to a running game).
@export var connect_timeout_seconds: float = 10.0
var _connecting := false
var _connect_attempt := 0
var _cancel_join: Button
var port := DEFAULT_PORT

## Every piece of scrap (a fixed pool; today's pieces are `in_play`).
var loot := Node3D.new()
var players := Node3D.new()
var spawner := MultiplayerSpawner.new()
var truck := Truck.new()
## The mansion's inside, rebuilt each day (see interior.gd). `_interior_key` = [seed, day] built.
var interior := Interior.new()
var _interior_key := []
## Monster pool (parked until the day's schedule wakes them). Host runs the brains.
var monsters: Array = []
## Host: index of the next entry in today's monster schedule.
var _next_spawn := 0
## Blindfold shown while a dust ghost sits on our head.
var _blind := ColorRect.new()
var ghost := Ghost.new()
## Host: peer id -> msec when their stun ends.
var stun_until := {}
## Host: peers dead for the rest of today (caught by the ghost, or left behind by the truck).
var dead := {}
## Host: peer id → true once the ghost has caught them today (a second catch kills).
var ghost_marked := {}
## Host: container index → name of the pool piece hidden in it today.
var container_loot := {}
## Containers searched today (every peer).
var opened := {}
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
var _settings := PanelContainer.new()
## Shown on the menu after a reload (e.g. why we were sent back). Survives the scene reload.
static var _menu_note := ""
## Command-line args (--host, --join…) apply to the first launch only, not to a reload after leaving.
static var _args_used := false
var _shooting := false  # --screenshot: keep the Esc menu out of the picture
var _sfx := {}
var _last_thud := 0
var _menu: Control
var _hint := Label.new()
var _result := Label.new()
var _aim := Label.new()
var _tip := Label.new()
var _picked_once := false
var _entered_once := false
## Scan (right click): seconds left on screen, and what it found [{node, text, color}].
var _scan_t := 0.0
var _scan_ready_at := 0
var _scan_hits: Array = []
var _scan_layer := Control.new()
var _scan_total := Label.new()
## Inventory slots along the bottom, the stamina bar over them, the carried weight.
var _slot_boxes: Array[PanelContainer] = []
var _stamina := ProgressBar.new()
var _weight := Label.new()
var _clock := Label.new()
## Seconds left for the big centre message and the small note under the crosshair.
var _result_t := 0.0
var _note := Label.new()
var _note_t := 0.0
var voice := Voice.new()
var _mic_label := Label.new()
## Truck radio map, shown while our player is on the radio.
var radio_map := RadioMap.new()
## Host: msec time the horn can lure the ghost again.
var _lure_ready_at := 0


func _ready() -> void:
	# Controls under CanvasLayers don't inherit a Window theme, so merge into the fallback theme.
	ThemeDB.get_default_theme().merge_with(UiTheme.build())
	_setup_input()
	_build_room()
	truck.name = "Truck"
	add_child(truck)
	ghost.name = "Ghost"
	ghost.main = self
	add_child(ghost)
	interior.name = "Interior"
	add_child(interior)
	for k in Tuning.data.monsters.pool:
		for i in int(Tuning.data.monsters.pool[k]):
			var m := Monster.new()
			m.name = "%s%d" % [k.capitalize(), i]
			m.main = self
			m.setup(k)
			add_child(m)
			monsters.append(m)
	loot.name = "Loot"
	add_child(loot)
	for d in Tuning.data.scrap:
		for i in int(Tuning.data.scrap_pool):
			var it := Loot.new()
			it.setup(d, i)
			loot.add_child(it)
	layout_day(run.day)
	ghost.position = interior.cell_center(interior.maze.exit) + Vector3.UP * 1.2  # offline/host view; a client gets positions from the host
	players.name = "Players"
	add_child(players)
	spawner.name = "Spawner"
	add_child(spawner)
	spawner.spawn_path = spawner.get_path_to(players)
	spawner.spawn_function = _spawn_player
	Settings.load_or_default()
	if AudioServer.get_bus_index(&"Voice") == -1:  # friends' voices get their own volume
		AudioServer.add_bus()
		AudioServer.set_bus_name(AudioServer.bus_count - 1, "Voice")
	voice.name = "Voice"
	add_child(voice)
	_build_hud()
	_build_audio()
	_apply_settings()
	day_failed.connect(_on_day_failed)
	_apply_run(run)

	net.name = "Net"
	add_child(net)
	multiplayer.connected_to_server.connect(_on_connected)
	multiplayer.connection_failed.connect(_on_connection_failed)
	net.wan_ready.connect(_on_wan_ready)
	var args := OS.get_cmdline_user_args() if not _args_used else PackedStringArray()
	_args_used = true
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
	for a in args:
		if a.begins_with("--seed=") and multiplayer.is_server():  # test tooling: a reproducible house
			run.seed = a.trim_prefix("--seed=").to_int()
			layout_day(run.day)
	for a in args:
		if a.begins_with("--day=") and multiplayer.is_server():  # test tooling: start on day N
			run.day = maxi(1, a.trim_prefix("--day=").to_int())
			layout_day(run.day)
			_push_run()
	for a in args:
		if a.begins_with("--elapsed=") and multiplayer.is_server():  # test tooling: skip into the day
			run.time_left -= a.trim_prefix("--elapsed=").to_float()
			_push_run()
	if "--settings" in args:
		open_settings()
	if "--demo-load" in args:
		_demo_load()
	if "--radio" in args:
		_demo_radio()
	if "--monster-lineup" in args:
		_demo_lineup()
	if "--inside" in args:
		_demo_inside()
	if "--scan" in args:
		_demo_scan()
	# Screenshot args survive a reload (they're test tooling), and the delay counts from app start.
	var all_args := OS.get_cmdline_user_args()
	var delay := 4.0
	for a in all_args:
		if a.begins_with("--shot-delay="):
			delay = a.trim_prefix("--shot-delay=").to_float()
	delay = maxf(0.1, delay - Time.get_ticks_msec() / 1000.0)
	for a in all_args:
		if a.begins_with("--screenshot="):
			_shooting = true
			_screenshot_later(a.trim_prefix("--screenshot="), delay)


## Hosts on `port`, announces itself on the LAN, and (unless `upnp` is false) asks the
## router to open the port for friends over the internet.
func host(upnp := true) -> void:
	if _connecting:
		return
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
	layout_day(run.day)
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
	if _connecting:
		return
	var a := Net.parse_address(address, port)
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(a[0], a[1])
	if err != OK:
		_hint.text = "참가 실패: %s" % error_string(err)
		return
	net.stop_listening()
	multiplayer.multiplayer_peer = peer
	for it in loot.get_children():
		it.freeze = true  # host owns the physics; we only display it
	if not multiplayer.server_disconnected.is_connected(_on_host_lost):
		multiplayer.server_disconnected.connect(_on_host_lost)
	_connecting = true
	_connect_attempt += 1
	if is_instance_valid(_cancel_join):
		_cancel_join.visible = true
	_hint.text = "연결 중: %s — 잠시 기다려 주세요" % address
	_wait_for_connection(_connect_attempt)


## A successful handshake is the point at which the menu gives way to play.
func _on_connected() -> void:
	if not _connecting:
		return
	_connecting = false
	if is_instance_valid(_cancel_join):
		_cancel_join.visible = false
	_close_menu()


func _wait_for_connection(attempt: int) -> void:
	await get_tree().create_timer(connect_timeout_seconds).timeout
	if _connecting and attempt == _connect_attempt:
		_on_connection_failed()


## Failed or cancelled joins keep the menu usable, including hosting afterwards.
func _on_connection_failed() -> void:
	if not _connecting:
		return
	_cancel_connection("연결 실패 — 호스트 주소와 게임 실행 상태를 확인하고 다시 참가해 주세요")


func _cancel_connection(message: String) -> void:
	_connecting = false
	if is_instance_valid(_cancel_join):
		_cancel_join.visible = false
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	for it in loot.get_children():
		it.freeze = not it.lying()
	_hint.text = message
	if _menu != null:
		net.start_listening()



func _spawn_player(id: int) -> Node:
	var p := Player.new()
	p.name = str(id)
	p.set_multiplayer_authority(id)
	if id == multiplayer.get_unique_id():
		voice.me = p  # our mic talks through our own player node
	p.position = _spawn_spot(players.get_child_count())
	p.rotation.y = PI  # out of the truck, facing the mansion
	return p


## Where mover number `i` starts a day: in the truck bed, side by side.
func _spawn_spot(i: int) -> Vector3:
	var c := truck.bed.get_center()
	return Vector3(c.x - 1.2 + (i % 4) * 0.8, 0, c.z + 0.6)


func _on_peer_joined(id: int) -> void:
	spawner.spawn(id)
	_rpc_run.rpc_id(id, run)


func _on_day_failed() -> void:
	if run.phase == "day":
		_end_day(0, "전원 사망", "", true)


func _on_peer_left(id: int) -> void:
	_drop_all(id)
	var p := players.get_node_or_null(str(id))
	if p:
		p.queue_free()


# --- scrap: pick up / drop ----------------------------------------------------

## Local player wants the piece `item_name` it is looking at.
func request_pickup(item_name: String) -> void:
	if multiplayer.is_server():
		pickup(multiplayer.get_unique_id(), item_name)
	else:
		_rpc_pickup.rpc_id(1, item_name)


@rpc("any_peer", "reliable")
func _rpc_pickup(item_name: String) -> void:
	if multiplayer.is_server():
		pickup(multiplayer.get_remote_sender_id(), item_name)


## Host: give `item_name` to peer `id` if it is lying within reach and they have a free
## slot. True if given (the owner puts it in a slot).
func pickup(id: int, item_name: String) -> bool:
	var it: Loot = loot.get_node_or_null(item_name)
	var p := players.get_node_or_null(str(id))
	if it == null or p == null or not it.lying() or is_stunned(id) or not p.slots.has(""):
		return false
	if p.global_position.distance_to(it.global_position) > Tuning.data.reach + 2.0:
		return false
	it.holder = id
	if id == multiplayer.get_unique_id():
		_take_local(item_name)
	elif id in multiplayer.get_peers():
		_rpc_take.rpc_id(id, item_name)
	return true


@rpc("authority", "reliable")
func _rpc_take(item_name: String) -> void:
	_take_local(item_name)


func _take_local(item_name: String) -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.take(item_name)
		_picked_once = true


## Local player put `item_name` down at `at` (already out of its slots).
func request_drop(item_name: String, at: Vector3) -> void:
	if multiplayer.is_server():
		drop(multiplayer.get_unique_id(), item_name, at)
	else:
		_rpc_drop.rpc_id(1, item_name, at)


@rpc("any_peer", "reliable")
func _rpc_drop(item_name: String, at: Vector3) -> void:
	if multiplayer.is_server():
		drop(multiplayer.get_remote_sender_id(), item_name, at)


## Host: peer `id` puts down the piece they carry.
func drop(id: int, item_name: String, at: Vector3) -> void:
	var it: Loot = loot.get_node_or_null(item_name)
	if it and it.holder == id:
		it.place(at)


## Host: everything peer `id` carries falls where they stand (death, leaving).
func _drop_all(id: int) -> void:
	var p := players.get_node_or_null(str(id))
	var at: Vector3 = p.global_position if p else Vector3.ZERO
	var k := 0
	for it in loot.get_children():
		if it.holder == id:
			it.place(at + Vector3(0.35 * (k % 3) - 0.35, 0, 0.35 * (k / 3)))
			k += 1


## True on the host (or offline) once its peer is up: the side that runs physics and AI.
func is_host_active() -> bool:
	return multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED \
		and multiplayer.is_server()


func is_stunned(id: int) -> bool:
	return dead.has(id) or stun_until.get(id, 0) > Time.get_ticks_msec()


## Host: knock out peer `id` for `sec` seconds (INF = dead until the next day). Everyone
## out at once fails the day, unless `can_fail_day` is false (the mimic's bite is a scare,
## not a run-ender when you play alone).
func stun_player(id: int, sec: float, can_fail_day := true) -> void:
	_knock(id, sec)
	var all_out := players.get_child_count() > 0
	for p in players.get_children():
		all_out = all_out and is_stunned(p.name.to_int())
	if all_out and can_fail_day:
		announce("전원 사망 — 오늘 모은 할당량이 전부 날아갑니다!")
		day_failed.emit()


## Host: the ghost caught `id`. First catch of the day = knocked down (`ghost.stun` s) and
## marked; caught again while marked = dead for the rest of the day (they spectate).
func ghost_hit(id: int) -> void:
	if ghost_marked.has(id):
		kill_player(id)
		return
	ghost_marked[id] = true
	stun_player(id, Tuning.data.ghost.stun, false)
	var who := "호스트" if id == 1 else "동료"
	announce("유령에게 홀렸어요 (%s) — 오늘 한 번 더 잡히면 죽어요" % who)


## Host: dead for the rest of the day; they spectate.
func kill_player(id: int) -> void:
	stun_player(id, INF)


## Host: stun or kill without the everyone-out check (the truck leaving does its own).
func _knock(id: int, sec: float) -> void:
	if is_inf(sec):
		dead[id] = true
	else:
		stun_until[id] = Time.get_ticks_msec() + int(sec * 1000)
	broadcast_sfx("whoosh")
	if is_inf(sec):
		_drop_all(id)
	if id == multiplayer.get_unique_id():
		_stun_local(sec)
	elif id in multiplayer.get_peers():
		_rpc_stun.rpc_id(id, sec)


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
	_result_t = 7.0
	if not multiplayer.get_peers().is_empty():
		_rpc_announce.rpc(text)


@rpc("authority", "reliable")
func _rpc_announce(text: String) -> void:
	_result.text = text
	_result_t = 7.0


## Screen static grows as the ghost gets close to our camera.
func _process(delta: float) -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	var amount := 0.0
	if me and ghost.visible and run.phase == "day":
		var d: float = me.cam.global_position.distance_to(ghost.global_position)
		amount = clampf(1.0 - d / Tuning.data.ghost.noise_range, 0.0, 1.0)
	# ghost static stays a little even with effects off: it's a gameplay warning
	_vhs.set_shader_parameter("noise", amount * lerpf(0.4, 1.0, Settings.get_value("effects")))
	# Esc menu: whenever the mouse is free during play (no menu, no shop). The game keeps running.
	_pause.visible = _menu == null and run.phase == "day" and players.get_child_count() > 0 \
		and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE and DisplayServer.get_name() != "headless" \
		and not _shooting
	_refresh_lan()
	radio_map.visible = me != null and me.on_radio
	_blind.visible = me != null and me.blinded and not me.dead
	if me:
		_mic_label.text = ("마이크 꺼짐 (V)" if voice.muted else ("● 말하는 중" if me.talk_t > 0.0 else "마이크 켜짐 (V)")) \
			+ "\n손전등 %d%%%s" % [roundi(me.battery * 100), " — 트럭에서 충전" if me.battery < 0.25 else ""]
		_mic_label.modulate = Color(1, 0.45, 0.4) if voice.muted else (Color(0.6, 1, 0.6) if me.talk_t > 0.0 else Color(1, 1, 1, 0.7))
		_entered_once = _entered_once or interior.contains(me.global_position)
	else:
		_mic_label.text = ""
	_update_inventory_hud(me)
	_update_scan(delta, me)
	_result_t = maxf(0.0, _result_t - delta)
	_result.modulate.a = clampf(_result_t, 0.0, 1.0) if run.phase == "day" else 1.0
	_note_t = maxf(0.0, _note_t - delta)
	_note.modulate.a = clampf(_note_t * 2.0, 0.0, 1.0)
	_interior_lights()
	_tip.text = Tips.tip_for(run.day, run.phase, _tip_state(me)) if me and _menu == null else ""
	_music.pitch_scale = Sfx.music_pitch(amount, Time.get_ticks_msec() / 1000.0)


## What the first-day guide needs to know about the local player.
func _tip_state(me: Node) -> Dictionary:
	var loaded := false
	for it in loot.get_children():
		loaded = loaded or (it.lying() and truck.bed.has_point(it.global_position))
	return {captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED or _shooting,
		entered = _entered_once, picked_once = _picked_once,
		carrying = me.slots.any(func(n: String) -> bool: return n != ""),
		loaded_any = loaded}


## What the local player is looking at (set by player.gd each frame): an E prompt.
func set_aim(target: Object) -> void:
	if target == null or not target is Node:
		_aim.text = ""
	elif target.name == "DepartButton":
		_aim.text = "[E] 출발 레버 — 오늘 일 끝내고 떠나기"
	elif target.has_meta("door"):
		_aim.text = "[E] " + interior.doors[target.get_meta("door")].label
	elif target.has_meta("container"):
		var i: int = target.get_meta("container")
		_aim.text = "(이미 뒤졌어요)" if opened.has(i) else "[E] 뒤지기 — " + str(interior.containers[i].label)
	elif target is Loot:
		var it: Loot = target
		_aim.text = "[E] 줍기 — %s%s" % [it.label, " (양손)" if it.two_handed else ""]
	elif target is CharacterBody3D and target.get("blinded"):
		_aim.text = "[E] 동료 머리에서 먼지귀신 떼기"
	else:
		_aim.text = ""


## A short note under the crosshair for the local player (full slots, two hands...).
func flash_note(text: String) -> void:
	_note.text = text
	_note_t = 2.5


func _physics_process(delta: float) -> void:
	if not is_host_active():
		return
	var t := Tuning.data
	if run.phase == "day":
		var warn: float = t.run.depart_warning
		var before: float = run.time_left
		run.time_left -= delta
		var dark_at: float = t.run.day_seconds - t.run.blackout_after
		if before > dark_at and run.time_left <= dark_at:
			announce("정전! 이제 손전등뿐입니다 — 배터리는 트럭에서 충전")
			broadcast_sfx("thud")
		if before > warn and run.time_left <= warn:
			announce("트럭이 %d초 뒤에 떠납니다! 못 탄 사람은 죽어요 — 트럭으로!" % int(warn))
			broadcast_sfx("horn")
		if run.time_left <= 0.0:
			_depart()
		_sync_t += delta
		if _sync_t >= 1.0:  # clients only need the clock once a second
			_sync_t = 0.0
			_push_run()
	if run.phase == "day":
		_spawn_monsters()


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


## True if a mover standing at `feet` counts as aboard the truck (rides along, recharges).
func at_truck(feet: Vector3) -> bool:
	return truck.bed.grow(Tuning.data.run.aboard_margin).has_point(feet + Vector3.UP * 0.5)


## Scrap that counts at departure: lying on the truck bed, or carried by someone aboard.
func truck_value() -> int:
	var total := 0
	for it in loot.get_children():
		if not it.in_play:
			continue
		var p := players.get_node_or_null(str(it.holder)) if it.holder != 0 else null
		if (it.holder == 0 and truck.bed.has_point(it.global_position)) \
				or (p != null and not dead.has(it.holder) and at_truck(p.global_position)):
			total += it.value
	return total


## Host: the truck leaves (lever or clock). Anyone not at the truck is left behind and
## dies (what they carried stays behind); each dead mover costs a fine. Nobody aboard = the
## haul is lost. Only during a day. Returns {total, count}.
func _depart() -> Dictionary:
	if run.phase != "day":
		return {}
	var cfg: Dictionary = Tuning.data.run
	var left_behind := 0
	for p in players.get_children():
		var id: int = p.name.to_int()
		if not dead.has(id) and not at_truck(p.global_position):
			_knock(id, INF)
			left_behind += 1
	var r := {total = truck_value()}
	broadcast_sfx("horn")
	var text := "출발! 실어 온 고물 %d원\n" % r.total
	if left_behind > 0:
		text += "못 탄 사람 %d명 사망\n" % left_behind
	var fine: int = dead.size() * int(cfg.death_fine)
	if fine > 0:
		text += "사망자 %d명 벌금 -%d원\n" % [dead.size(), fine]
	var wiped := players.get_child_count() > 0 and dead.size() >= players.get_child_count()
	_end_day(maxi(0, r.total - fine), "", text, wiped)
	return r


# --- run: days, shop, reset --------------------------------------------------

## Host: close the day with `earned`, tell everyone, open the shop (or end the run).
func _end_day(earned: int, why := "", prefix := "", wiped := false) -> void:
	run = RunState.end_day(run, earned, Tuning.data.run, why, wiped)
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
	_build_interior(r.get("seed", 0), r.day)
	var cfg: Dictionary = Tuning.data.run
	var secs := maxi(0, ceili(r.time_left))
	_clock.text = "%d일차   %d:%02d" % [r.day, secs / 60, secs % 60] if r.phase == "day" else "%d일차 · 상점" % r.day
	var left := RunState.days_left(r, cfg)
	_status.text = "할당량 %d / %d원 · %s\n팀 돈 %d원%s" % [
		r.haul, RunState.quota(r, cfg), "오늘 마감!" if left == 0 else "마감까지 %d일" % (left + 1),
		r.money, " · 퇴마 테이프 %d (Q)" % r.tapes if r.tapes > 0 else ""]
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


## Host: scatter today's scrap through the mansion (seeded by run + day, so a run replays
## the same day): `loot_per_day` pieces, kinds drawn by rarity, values rolled in each kind's
## range and worth a little more the deeper the room. Everything else is parked.
func layout_day(day: int) -> void:
	var cfg := Tuning.data
	var seed: int = run.get("seed", 0)
	_build_interior(seed, day)
	var counts: Array = cfg.loot_per_day
	var n: int = counts[clampi(day, 1, counts.size()) - 1]
	var spots: Array = interior.loot_spots(hash([seed, day, "spots"]), n)
	var rng := RandomNumberGenerator.new()
	rng.seed = hash([seed, day, "loot"])
	var free: Array = loot.get_children()
	for it in free:
		it.holder = 0
		it.in_play = false
		it.global_position = Vector3(0, -60, 0)
	for spot in spots:
		var it := _roll_piece(rng, free, interior.maze.dist.get(spot[1], 0))
		if it == null:
			break
		it.in_play = true
		it.place(spot[0])
	# Some wardrobes, drawers, fridges and boxes hide a piece: it stays parked until searched.
	container_loot.clear()
	opened.clear()
	var sc: Dictionary = cfg.interior.search
	for i in interior.containers.size():
		if rng.randf() >= float(sc.loot_chance):
			continue
		var c: Dictionary = interior.containers[i]
		var it := _roll_piece(rng, free, interior.maze.dist.get(interior.cell_of(c.at), 0))
		if it == null:
			break
		container_loot[i] = str(it.name)


## Host: takes a free pool piece of a kind drawn by rarity (any free piece if that kind ran
## out), rolls its value (worth more `depth` rooms in), and returns it. Null when none left.
func _roll_piece(rng: RandomNumberGenerator, free: Array, depth: int) -> Loot:
	var cfg := Tuning.data
	var total_rarity := 0.0
	for d in cfg.scrap:
		total_rarity += d.rarity
	var roll := rng.randf() * total_rarity
	var kind := ""
	for d in cfg.scrap:
		roll -= d.rarity
		kind = d.name
		if roll <= 0.0:
			break
	var pick: Array = free.filter(func(it: Node) -> bool: return it.kind == kind)
	if pick.is_empty():
		pick = free  # that kind's pool is used up: take any free piece
	if pick.is_empty():
		return null
	var it: Loot = pick[0]
	free.erase(it)
	var d: Dictionary = cfg.scrap.filter(func(e: Dictionary) -> bool: return e.name == it.kind)[0]
	it.value = roundi(rng.randi_range(int(d.value[0]), int(d.value[1])) * (1.0 + cfg.loot_depth_bonus * depth))
	it.rotation = Vector3(0, rng.randf() * TAU, 0)
	return it


## Local player pressed E on container `i` (a wardrobe, drawer, fridge or box).
func request_search(i: int) -> void:
	if multiplayer.is_server():
		search(multiplayer.get_unique_id(), i)
	else:
		_rpc_search.rpc_id(1, i)


@rpc("any_peer", "reliable")
func _rpc_search(i: int) -> void:
	if multiplayer.is_server():
		search(multiplayer.get_remote_sender_id(), i)


## Host: peer `id` searches container `i` (once per day for everybody). If it hid a piece,
## the piece drops out in front of it. Everyone learns it's been searched. True if searched now.
func search(id: int, i: int) -> bool:
	var p := players.get_node_or_null(str(id))
	if opened.has(i) or p == null or is_stunned(id) or i < 0 or i >= interior.containers.size():
		return false
	var c: Dictionary = interior.containers[i]
	if p.global_position.distance_to(c.at) > Tuning.data.reach + 2.0:
		return false
	var found := ""
	if container_loot.has(i):
		var it: Loot = loot.get_node(container_loot[i])
		it.in_play = true
		it.place(c.at + c.front + c.front.normalized() * 0.3)
		found = it.label
	_set_opened(i, id, found)
	if not multiplayer.get_peers().is_empty():
		_rpc_opened.rpc(i, id, found)
	return true


@rpc("authority", "reliable")
func _rpc_opened(i: int, id: int, found: String) -> void:
	_set_opened(i, id, found)


func _set_opened(i: int, id: int, found: String) -> void:
	opened[i] = true
	if id == multiplayer.get_unique_id():
		play_sfx("thud")
		flash_note("%s 발견!" % found if found != "" else "아무것도 없어요")


## Host: wake the monsters whose time has come today (schedule in tuning, by day), each in
## a random mansion room at least `min_spawn_dist` rooms from the entrance.
func _spawn_monsters() -> void:
	var c: Dictionary = Tuning.data.monsters
	var plan: Array = c.schedule[clampi(run.day, 1, c.schedule.size()) - 1]
	var elapsed: float = Tuning.data.run.day_seconds - run.time_left
	while _next_spawn < plan.size() and elapsed >= float(plan[_next_spawn][0]):
		var kind: String = plan[_next_spawn][1]
		_next_spawn += 1
		var free: Array = monsters.filter(func(m: Node) -> bool: return m.kind == kind and not m.active())
		if free.is_empty():
			continue
		var rooms: Array = interior.maze.dist.keys().filter(func(r: Vector2i) -> bool:
			return interior.maze.dist[r] >= int(c.min_spawn_dist))
		if rooms.is_empty():
			continue
		var rng := RandomNumberGenerator.new()
		rng.seed = hash([run.seed, run.day, _next_spawn])
		free[0].wake(interior.cell_center(rooms[rng.randi_range(0, rooms.size() - 1)]))


## Host: put a blindfold on (or take it off) peer `id`'s screen.
func blind_player(id: int, on: bool) -> void:
	if id == multiplayer.get_unique_id():
		_blind_local(on)
	elif id in multiplayer.get_peers():
		_rpc_blind.rpc_id(id, on)


@rpc("authority", "reliable")
func _rpc_blind(on: bool) -> void:
	_blind_local(on)


func _blind_local(on: bool) -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.blinded = on


## A mover clicked a teammate: pull a dust ghost off their head.
func request_free(target: int) -> void:
	if multiplayer.is_server():
		free_player(target)
	else:
		_rpc_free.rpc_id(1, target)


@rpc("any_peer", "reliable")
func _rpc_free(target: int) -> void:
	if multiplayer.is_server():
		free_player(target)


## Host: true if something was on `target`'s head and is gone now.
func free_player(target: int) -> bool:
	for m in monsters:
		if m.free_from(target):
			broadcast_sfx("thud")
			return true
	return false


## Builds today's mansion inside if it isn't already (host in layout_day, clients from the run).
func _build_interior(seed: int, day: int) -> void:
	if _interior_key != [seed, day]:
		_interior_key = [seed, day]
		interior.build(seed, day)


## Interior bulbs flicker on old wiring and die at the blackout (the red fire-exit lamp stays).
## Every peer derives it from the synced clock. Cosmetic, so local.
func _interior_lights() -> void:
	var cfg: Dictionary = Tuning.data.run
	var dark: bool = run.phase == "day" and cfg.day_seconds - run.time_left >= cfg.blackout_after
	var t := Time.get_ticks_msec() / 1000.0
	for i in interior.lights.size():
		var l: OmniLight3D = interior.lights[i]
		l.visible = not dark or l.get_meta("emergency", false)
		var out := 0.1 if sin(t * 0.9 + i * 2.3) > 0.985 else 1.0
		l.light_energy = (0.9 if l.get_meta("emergency", false) else 1.0) * out * (0.92 + 0.08 * sin(t * 11.0 + i))


func _reset_house() -> void:
	stun_until.clear()
	dead.clear()
	ghost_marked.clear()
	_lure_ready_at = 0
	_next_spawn = 0
	for m in monsters:
		m.park()
	layout_day(run.day)
	ghost.global_position = interior.cell_center(interior.maze.exit) + Vector3.UP * 1.2
	ghost.exorcise(0.0)
	_respawn_local()
	if not multiplayer.get_peers().is_empty():
		_rpc_respawn.rpc()


@rpc("authority", "reliable")
func _rpc_respawn() -> void:
	_respawn_local()


## Each peer moves its own player back to its spawn spot.
func _respawn_local() -> void:
	opened.clear()  # a new day: every container is shut again
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.global_position = _spawn_spot(me.get_index())
		me.rotation.y = PI
		me.velocity = Vector3.ZERO
		me.recover()


## Truck radio: the local operator pressed H — sound the horn to pull the ghost to the truck.
func request_lure() -> void:
	if multiplayer.is_server():
		lure(multiplayer.get_unique_id())
	else:
		_rpc_lure.rpc_id(1)


@rpc("any_peer", "reliable")
func _rpc_lure() -> void:
	if multiplayer.is_server():
		lure(multiplayer.get_remote_sender_id())


## Host: peer `id` (on the radio at the truck) sounds the horn: the ghost drifts to the
## mansion room farthest from every living mover, if the horn has cooled down. True if it worked.
func lure(id: int) -> bool:
	var c: Dictionary = Tuning.data.radio
	var p := players.get_node_or_null(str(id))
	if run.phase != "day" or p == null or not p.on_radio or Time.get_ticks_msec() < _lure_ready_at:
		return false
	_lure_ready_at = Time.get_ticks_msec() + int(c.lure_cooldown * 1000)
	ghost.lure(interior.cell_center(empty_room()) + Vector3.UP * 1.2, c.lure_seconds)
	broadcast_sfx("horn")
	announce("빵빵! 유령을 아무도 없는 방으로 쫓았어요 (%d초)" % int(c.lure_seconds))
	_set_lure_cooldown(c.lure_cooldown)
	if not multiplayer.get_peers().is_empty():
		_rpc_lure_cooldown.rpc(c.lure_cooldown)
	return true


## The mansion room farthest (in rooms walked) from every living mover inside.
func empty_room() -> Vector2i:
	var inside: Array = []
	for p in players.get_children():
		if not is_stunned(p.name.to_int()) and interior.contains(p.global_position):
			inside.append(interior.cell_of(p.global_position))
	var best: Vector2i = interior.maze.exit
	var best_d := -1
	for r in interior.maze.dist:
		var near := 1 << 20
		for q in inside:
			near = mini(near, absi(r.x - q.x) + absi(r.y - q.y))
		if near > best_d:
			best_d = near
			best = r
	return best


@rpc("authority", "reliable")
func _rpc_lure_cooldown(sec: float) -> void:
	_set_lure_cooldown(sec)


func _set_lure_cooldown(sec: float) -> void:
	radio_map.lure_ready_in = sec


## Exorcism tape: the local player pressed Q.
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
	_music.volume_db = -16.0  # a radio somewhere in the house, not a soundtrack
	add_child(_music)
	_music.play()
	var hum := AudioStreamPlayer.new()
	hum.stream = Sfx.cached("drone")
	hum.volume_db = -10.0
	hum.autoplay = true
	add_child(hum)
	var streams := {thud = Sfx.thud(), horn = Sfx.horn(), whoosh = Sfx.whoosh(), coin = Sfx.coin(), scan = Sfx.scan()}
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


## Local: a door creaks when we go through it.
func broadcast_door() -> void:
	play_sfx("thud")


# --- room, HUD, menu ---------------------------------------------------------

func _build_room() -> void:
	var moon := DirectionalLight3D.new()  # low cold moon
	moon.rotation = Vector3(-0.5, 2.6, 0)
	moon.light_color = Color("#9fb4d8")
	moon.light_energy = 0.35
	moon.shadow_enabled = true
	moon.light_volumetric_fog_energy = 0.4
	add_child(moon)
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()  # dusk gone to night: ink top, a last bruise on the horizon
	sky_mat.sky_top_color = Color("#0a0c16")
	sky_mat.sky_horizon_color = Color("#3a2a3a")
	sky_mat.ground_horizon_color = Color("#1a141c")
	sky_mat.ground_bottom_color = Color("#08080a")
	sky_mat.sun_angle_max = 10.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("#3a4050")
	env.ambient_light_energy = 0.2  # dark: flashlights and the odd bulb do the work
	env.fog_enabled = true
	env.fog_light_color = Color("#1a1c24")
	env.fog_density = 0.02
	env.fog_sky_affect = 0.4
	# Volumetric fog: dust in the air, so the flashlight cuts a visible beam and bulbs get halos.
	env.volumetric_fog_enabled = true
	env.volumetric_fog_density = 0.04
	env.volumetric_fog_albedo = Color("#b8b8c0")
	env.volumetric_fog_anisotropy = 0.6   # forward scattering: beams glow when you look along them
	env.volumetric_fog_length = 40.0
	env.ssao_enabled = true       # contact shadows in corners and under furniture
	env.ssao_intensity = 1.8
	env.glow_enabled = true       # bulbs, lamps and headlights bloom a little
	env.glow_intensity = 0.7
	env.glow_hdr_threshold = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.adjustment_enabled = true
	env.adjustment_saturation = 0.8
	env.adjustment_contrast = 1.15
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	var house := House.new()
	house.name = "House"
	add_child(house)
	# Menu camera: the mansion at night from the road; a player's camera takes over on spawn.
	var menu_cam := Camera3D.new()
	menu_cam.name = "MenuCam"
	add_child(menu_cam)
	menu_cam.look_at_from_position(Vector3(-11, 2.0, -16), Vector3(0, 4.0, 5), Vector3.UP)
	menu_cam.current = true

	# Low-res camcorder overlay: above the 3D view, below the HUD (layer 1).
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
	_scan_layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_scan_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_scan_layer.draw.connect(_draw_scan)
	layer.add_child(_scan_layer)
	var cross := ColorRect.new()  # a 4 px dot
	cross.color = Color(UiTheme.CREAM, 0.8)
	cross.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	cross.offset_left = -2
	cross.offset_right = 2
	cross.offset_top = -2
	cross.offset_bottom = 2
	cross.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(cross)
	for l in [_aim, _note]:
		l.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
		l.grow_horizontal = Control.GROW_DIRECTION_BOTH
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		layer.add_child(l)
	_aim.offset_top = 18
	_aim.add_theme_font_size_override("font_size", 17)
	_note.offset_top = 44
	_note.add_theme_font_size_override("font_size", 15)
	_note.add_theme_color_override("font_color", Color("#ff9a70"))
	_clock.position = Vector2(20, 14)
	_clock.add_theme_font_size_override("font_size", 28)
	layer.add_child(_clock)
	_status.position = Vector2(22, 52)
	_status.add_theme_font_size_override("font_size", 15)
	_status.modulate = Color(1, 1, 1, 0.85)
	layer.add_child(_status)
	_mic_label.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_mic_label.offset_left = -260
	_mic_label.offset_right = -20
	_mic_label.offset_top = 14
	_mic_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_mic_label.add_theme_font_size_override("font_size", 14)
	layer.add_child(_mic_label)
	_scan_total.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_scan_total.offset_left = -320
	_scan_total.offset_right = -20
	_scan_total.offset_top = 62
	_scan_total.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_scan_total.add_theme_font_size_override("font_size", 20)
	_scan_total.add_theme_color_override("font_color", Color("#9fe8a0"))
	layer.add_child(_scan_total)
	# Inventory: slots along the bottom, stamina over them, weight to the right.
	var inv := HBoxContainer.new()
	inv.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	inv.grow_horizontal = Control.GROW_DIRECTION_BOTH
	inv.offset_top = -92
	inv.offset_bottom = -20
	inv.add_theme_constant_override("separation", 8)
	inv.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for i in int(Tuning.data.slots):
		var box := PanelContainer.new()
		box.custom_minimum_size = Vector2(96, 64)
		var l := Label.new()
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		l.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		l.add_theme_font_size_override("font_size", 13)
		box.add_child(l)
		inv.add_child(box)
		_slot_boxes.append(box)
	_weight.add_theme_font_size_override("font_size", 14)
	_weight.custom_minimum_size = Vector2(70, 0)
	_weight.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	inv.add_child(_weight)
	layer.add_child(inv)
	_stamina.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	_stamina.offset_left = -205
	_stamina.offset_right = 205
	_stamina.offset_top = -102
	_stamina.offset_bottom = -97
	_stamina.show_percentage = false
	_stamina.max_value = 1.0
	_stamina.add_theme_stylebox_override("background", UiTheme._box(Color(0, 0, 0, 0.5), Color(0, 0, 0, 0), 0, 0))
	_stamina.add_theme_stylebox_override("fill", UiTheme._box(Color("#e8d8a8"), Color(0, 0, 0, 0), 0, 0))
	layer.add_child(_stamina)
	_hint.text = "E 줍기·문·레버 · G 내려놓기 · 우클릭 스캔 · 1~4/휠 손 바꾸기 · Shift 달리기 · F 손전등 · Q 퇴마 테이프 · Tab 무전 · V 마이크 · Esc 메뉴"
	_tip.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_tip.offset_top = -150
	_tip.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_tip.add_theme_font_size_override("font_size", 20)
	_tip.add_theme_color_override("font_color", UiTheme.BRASS.lightened(0.2))
	layer.add_child(_tip)
	_hint.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_hint.offset_top = -16
	_hint.offset_bottom = -2
	_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hint.add_theme_font_size_override("font_size", 12)
	_hint.modulate = Color(1, 1, 1, 0.55)
	layer.add_child(_hint)
	_result.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	_result.offset_top = 110
	_result.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_result.add_theme_font_size_override("font_size", 24)
	_result.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_result.offset_left = 120
	_result.offset_right = -120
	layer.add_child(_result)
	_blind.color = Color(0.02, 0.015, 0.01, 0.97)
	_blind.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_blind.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_blind.visible = false
	var bl := Label.new()
	bl.text = "먼지귀신이 머리에 붙었어요! 앞이 안 보여요 — 동료가 나를 보고 E를 눌러야 떼져요"
	bl.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	bl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	bl.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_blind.add_child(bl)
	layer.add_child(_blind)
	radio_map.main = self
	radio_map.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	radio_map.mouse_filter = Control.MOUSE_FILTER_IGNORE
	radio_map.visible = false
	layer.add_child(radio_map)
	_addr.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	_addr.offset_left = 16
	_addr.offset_right = 420
	_addr.offset_top = -80
	_addr.offset_bottom = -24
	_addr.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_addr.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	_addr.add_theme_font_size_override("font_size", 12)
	_addr.modulate = Color(1, 1, 1, 0.6)
	layer.add_child(_addr)

	_shop.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	_shop.offset_top = -250
	_shop.offset_bottom = -120
	_shop.grow_vertical = Control.GROW_DIRECTION_BEGIN  # extra height goes up, clear of the inventory
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
	_build_settings()


## Bottom bar: each slot shows its piece (and value) — the one in hand outlined in brass.
func _update_inventory_hud(me: Node) -> void:
	var show: bool = me != null and not me.dead and _menu == null
	_stamina.visible = show
	_weight.visible = show
	_hint.visible = show and run.day == 1 and run.phase == "day"
	for i in _slot_boxes.size():
		var box := _slot_boxes[i]
		box.visible = show
		if not show:
			continue
		var n: String = me.slots[i]
		var it: Loot = loot.get_node_or_null(n) if n != "" else null
		var active: bool = i == me.slot
		(box.get_child(0) as Label).text = "%d\n%s" % [i + 1, "%s\n%d원" % [it.label, it.value] if it else ""]
		box.add_theme_stylebox_override("panel", UiTheme._box(Color(UiTheme.INK, 0.75 if active else 0.45),
			UiTheme.BRASS if active else Color(UiTheme.BRASS, 0.25), 2 if active else 1, 4))
	if show:
		_stamina.value = me.stamina
		_stamina.modulate.a = 0.35 if me.stamina >= 1.0 else 1.0
		_weight.text = "%dkg" % roundi(me.carried_weight())


## Right click: show every piece of scrap (and monster) in sight within `radius` for a few
## seconds, with values and a total. Local only.
func scan() -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	var c: Dictionary = Tuning.data.scan
	if me == null or me.dead or Time.get_ticks_msec() < _scan_ready_at:
		return
	_scan_ready_at = Time.get_ticks_msec() + int(c.cooldown * 1000)
	_scan_t = c.show_seconds
	_scan_hits.clear()
	play_sfx("scan")
	var eye: Vector3 = me.cam.global_position
	var space := get_world_3d().direct_space_state
	var total := 0
	var targets: Array = loot.get_children().filter(func(n: Node) -> bool: return n.lying())
	targets += monsters.filter(func(m: Node) -> bool: return m.active())
	if ghost.visible:
		targets.append(ghost)
	for n in targets:
		var at: Vector3 = n.global_position + (Vector3.UP * 1.0 if not n is Loot else Vector3.ZERO)
		if eye.distance_to(at) > c.radius or me.cam.is_position_behind(at):
			continue
		var q := PhysicsRayQueryParameters3D.create(eye, at)
		q.exclude = [me.get_rid()]
		var hit := space.intersect_ray(q)
		if not hit.is_empty() and hit.collider != n and hit.collider is StaticBody3D:
			continue  # behind a wall
		if n is Loot:
			total += n.value
			_scan_hits.append({node = n, text = "%s\n%d원" % [n.label, n.value], color = Color("#9fe8a0")})
		else:
			_scan_hits.append({node = n, text = n.get_meta("label", "유령"), color = Color("#ff7060")})
	_scan_total.text = "스캔: 고물 %d개 · %d원" % [_scan_hits.filter(func(h: Dictionary) -> bool: return h.node is Loot).size(), total]


func _update_scan(delta: float, me: Node) -> void:
	_scan_t = maxf(0.0, _scan_t - delta)
	_scan_total.modulate.a = clampf(_scan_t, 0.0, 1.0)
	if me and run.phase == "day":  # the truck total stays up while you stand in it
		if at_truck(me.global_position) and _scan_t <= 0.0:
			_scan_total.text = "트럭에 실린 고물: %d원" % truck_value()
			_scan_total.modulate.a = 1.0
	_scan_layer.queue_redraw()


func _draw_scan() -> void:
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me == null or _scan_t <= 0.0:
		return
	var cam: Camera3D = me.cam
	var font := get_window().get_theme_default_font() if get_window() else ThemeDB.fallback_font
	var shown: float = Tuning.data.scan.show_seconds - _scan_t
	if shown < 0.5:  # the sweep: a ring growing out of the crosshair
		var c := _scan_layer.size / 2.0
		_scan_layer.draw_arc(c, shown * 1400.0, 0, TAU, 64, Color(0.6, 1, 0.6, 0.5 - shown), 3.0)
	var a := clampf(_scan_t, 0.0, 1.0)
	for h in _scan_hits:
		var n: Node3D = h.node
		if not is_instance_valid(n) or (n is Loot and not n.lying()):
			continue
		var at: Vector3 = n.global_position + (Vector3.UP * 1.0 if not n is Loot else Vector3.ZERO)
		if cam.is_position_behind(at):
			continue
		var p := cam.unproject_position(at)
		var col: Color = h.color
		_scan_layer.draw_rect(Rect2(p - Vector2(6, 6), Vector2(12, 12)), Color(col, a), false, 2.0)
		var lines: PackedStringArray = String(h.text).split("\n")
		for i in lines.size():
			_scan_layer.draw_string_outline(font, p + Vector2(12, -4 + i * 18), lines[i], HORIZONTAL_ALIGNMENT_LEFT, -1, 16, 4, Color(0, 0, 0, a))
			_scan_layer.draw_string(font, p + Vector2(12, -4 + i * 18), lines[i], HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(col, a))


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
	sub.text = "귀신 들린 저택에서 고물을 건져 와라   v%s" % ProjectSettings.get_setting("application/config/version", "")
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sub.modulate = Color(1, 1, 1, 0.75)
	box.add_child(sub)
	if _menu_note != "":
		var note := Label.new()
		note.text = _menu_note
		note.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		note.modulate = Color(1, 0.6, 0.5)
		box.add_child(note)
		_menu_note = ""
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
	_cancel_join = Button.new()
	_cancel_join.text = "연결 취소"
	if is_instance_valid(_cancel_join):
		_cancel_join.visible = false
	_cancel_join.pressed.connect(func() -> void: _cancel_connection("연결을 취소했어요 — 다시 참가하거나 호스트를 시작할 수 있어요"))
	box.add_child(_cancel_join)
	var settings_btn := Button.new()
	settings_btn.text = "설정"
	settings_btn.pressed.connect(open_settings)
	box.add_child(settings_btn)
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
			["설정", open_settings],
			["전체 화면 (F11)", _toggle_fullscreen],
			["메뉴로 나가기", _leave_to_menu],
			["게임 종료", get_tree().quit]]:
		var b := Button.new()
		b.text = item[0]
		b.pressed.connect(item[1])
		box.add_child(b)
	_pause.visible = false
	layer.add_child(_pause)


## Settings panel (sliders from Settings.SPEC), on its own layer above every menu.
func _build_settings() -> void:
	var layer := CanvasLayer.new()
	layer.layer = 5
	add_child(layer)
	_settings.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	_settings.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_settings.grow_vertical = Control.GROW_DIRECTION_BOTH
	_settings.add_theme_stylebox_override("panel", UiTheme._box(UiTheme.INK, UiTheme.BRASS, 2, 18))  # opaque: sits over menus
	var box := VBoxContainer.new()
	box.custom_minimum_size = Vector2(380, 0)
	_settings.add_child(box)
	var title := Label.new()
	title.text = "설정"
	title.add_theme_font_size_override("font_size", 26)
	box.add_child(title)
	for k in Settings.SPEC:
		var spec: Array = Settings.SPEC[k]
		var label := Label.new()
		box.add_child(label)
		var slider := HSlider.new()
		slider.min_value = spec[1]
		slider.max_value = spec[2]
		slider.step = spec[3]
		slider.value = Settings.get_value(k)
		slider.custom_minimum_size = Vector2(0, 24)
		var show := func(v: float) -> void: label.text = "%s  %d%%" % [spec[4], roundi(v * 100)]
		show.call(slider.value)
		slider.value_changed.connect(func(v: float) -> void:
			Settings.values[k] = v
			show.call(v)
			_apply_settings())
		box.add_child(slider)
	var close := Button.new()
	close.text = "닫기"
	close.pressed.connect(func() -> void:
		Settings.save()
		_settings.visible = false)
	box.add_child(close)
	_settings.visible = false
	layer.add_child(_settings)


func open_settings() -> void:
	_settings.visible = true


## Pushes the current settings into the audio bus and the screen effect.
func _apply_settings() -> void:
	AudioServer.set_bus_volume_db(0, linear_to_db(maxf(Settings.get_value("volume"), 0.0001)))
	var e := Settings.get_value("effects")
	_vhs.set_shader_parameter("pixel_size", lerpf(1.0, 2.5, e))   # 1 = full resolution
	_vhs.set_shader_parameter("color_levels", lerpf(64.0, 24.0, e))
	_vhs.set_shader_parameter("split", 0.0)
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index(&"Voice"), linear_to_db(maxf(Settings.get_value("voice"), 0.0001)))
	_vhs.set_shader_parameter("scanline", 0.0)


func _toggle_fullscreen() -> void:
	var full := DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_FULLSCREEN
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED if full else DisplayServer.WINDOW_MODE_FULLSCREEN)


func _unhandled_key_input(event: InputEvent) -> void:
	if event.is_action_pressed("fullscreen"):
		_toggle_fullscreen()
	elif event.is_action_pressed("voice_mute"):
		voice.muted = not voice.muted


## Client: the host is gone. Drop the dead peer at once (it errors on every use), say so,
## and go back to the menu shortly.
func _on_host_lost() -> void:
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	for p in players.get_children():
		p.queue_free()
	voice.me = null
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	announce("호스트와 연결이 끊겼어요 — 곧 메뉴로 돌아가요")
	_menu_note = "호스트와 연결이 끊겼어요"
	await get_tree().create_timer(2.0).timeout
	_leave_to_menu()


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


## --monster-lineup: stand in the mansion entrance facing the three monster kinds (screenshots).
func _demo_lineup() -> void:
	await get_tree().create_timer(1.0).timeout
	_next_spawn = 99
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	var c: Vector3 = interior.cell_center(Vector2i.ZERO)
	if me:
		me.global_position = c + Vector3(0, 0, 2.6)
		me.rotation.y = 0.0
		me.head.rotation.x = 0.05
	var x := -1.9
	for k in ["statue", "dust", "boar"]:
		var m: Node = monsters.filter(func(n: Node) -> bool: return n.kind == k)[0]
		m.wake(c + Vector3(x, 0, -0.8))
		m.set_physics_process(false)  # pose only
		m.rotation.y = PI + x * 0.2   # face the camera
		x += 1.3
	ghost.set_physics_process(false)
	ghost.global_position = c + Vector3(x, 1.2, -0.8)
	ghost.rotation.y = PI - 0.3
	ghost.state = "chase"  # arms up for the photo


## --inside: stand just inside the front door, holding two pieces (screenshots).
func _demo_inside() -> void:
	await get_tree().create_timer(1.0).timeout
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.use_door(0)
		for it in loot.get_children().filter(func(n: Node) -> bool: return n.lying()).slice(0, 2):
			it.place(me.global_position)
			pickup(me.name.to_int(), it.name)


## --scan: stand in a room with scrap and scan (screenshots).
func _demo_scan() -> void:
	await get_tree().create_timer(1.0).timeout
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	var it: Node = loot.get_children().filter(func(n: Node) -> bool: return n.lying())[0]
	if me:
		var c: Vector3 = interior.cell_center(interior.cell_of(it.global_position))
		me.global_position = c + (c - it.global_position).normalized() * 2.0 * Vector3(1, 0, 1)
		me.look_at(it.global_position * Vector3(1, 0, 1) + Vector3(0, me.global_position.y, 0) + Vector3(0.001, 0, 0))
		me.head.rotation.x = -0.35
		await get_tree().create_timer(0.3).timeout
		scan()


## --radio: stand at the truck and open the radio map (for screenshots).
func _demo_radio() -> void:
	await get_tree().create_timer(1.0).timeout
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.global_position = truck.bed.get_center() * Vector3(1, 0, 1)
		me.set_radio(true)


## --demo-load: put three pieces on the truck, face it, and depart (for screenshots).
func _demo_load() -> void:
	await get_tree().create_timer(1.0).timeout
	var c := truck.bed.get_center()
	var pieces: Array = loot.get_children().filter(func(n: Node) -> bool: return n.in_play).slice(0, 3)
	for i in pieces.size():
		pieces[i].place(Vector3(c.x - 1.0 + i, 0, c.z + 1.0))
	var me := players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.global_position = Vector3(0, 0, -0.8)
		me.rotation.y = 0.0
		me.head.rotation.x = -0.35
	await get_tree().create_timer(1.5).timeout
	_depart()


func _setup_input() -> void:
	var keys := {move_forward = KEY_W, move_back = KEY_S, move_left = KEY_A, move_right = KEY_D, jump = KEY_SPACE,
		sprint = KEY_SHIFT, interact = KEY_E, drop = KEY_G, use_tape = KEY_Q, fullscreen = KEY_F11,
		flashlight = KEY_F, voice_mute = KEY_V, radio = KEY_TAB, lure = KEY_H}
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
	for p in players.get_children():  # proof for automated voice runs: chunks heard per remote player
		if not p.is_multiplayer_authority():
			print("voice heard from %s: %d chunks" % [p.name, p.heard_chunks])
	get_tree().quit()
