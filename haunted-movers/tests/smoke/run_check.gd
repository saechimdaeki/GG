extends SceneTree
## Smoke check.
## Mansion: rooms all reachable, fire exit on an outer wall, a new layout each day; the front
## door and the fire exit take a mover in and out (E on the door).
## Scrap: each day scatters its count of pieces inside the mansion, apart, values in range;
## the seed replays a day and the next day reshuffles. Pick up (E) fills a slot, up to 4;
## a two-handed piece locks the hand; G puts it down; weight slows you; sprint drains stamina.
## Truck: what lies on the bed or is carried by someone aboard counts at departure; whoever
## is not at the truck dies and their scrap stays behind; nobody aboard wipes the haul.
## Ghost: state transitions; it kills a lone carrier (dropping their scrap) and the day fails.
## Run rules: endless growing quota over 3-day cycles, haul lost on a wipe, fired on a miss, shop.
## Battery / blackout: lit flashlight drains, truck recharges; mansion bulbs die (fire exit stays).
## Radio: at the truck only, holds still, map shows, horn chases the ghost to an empty room.
## Monsters: statue freezes when watched, dust ghost blinds (teammate frees), boar charges,
## schedule wakes them in far rooms; walking monsters make footsteps.
## Scan: right click lists the scrap in sight with values, not what is behind a wall.
## Audio, animation, settings, voice, tips, address parsing.
## Prints PASS or FAIL n.
##   godot --headless --path . -s res://tests/smoke/run_check.gd

const Ghost = preload("res://src/ghost.gd")
const Player = preload("res://src/player.gd")
const RunState = preload("res://src/run_state.gd")
const Interior = preload("res://src/interior.gd")
const Monster = preload("res://src/monster.gd")
const Sfx = preload("res://src/sfx.gd")
const Net = preload("res://src/net.gd")
const Tuning = preload("res://src/tuning.gd")

var _fails := 0


func _initialize() -> void:
	_run()


func _run() -> void:
	var main: Node = load("res://src/main.tscn").instantiate()
	root.add_child(main)
	await process_frame  # main._ready (and its day-1 layout) runs on the first frame
	await _frames(main, 20)

	_check_maze()
	_check_layouts(main)
	await _check_doors(main)
	await _check_pickup_and_drop(main)
	_check_search(main)
	_check_weight_and_stamina(main)
	await _check_depart(main)
	_check_ghost_states()
	await _check_ghost_attack(main)
	await _check_ghost_flashlight(main)
	_check_stun_reset(main)
	_check_run_rules()
	_check_left_behind(main)
	await _check_battery_and_blackout(main)
	await _check_radio(main)
	await _check_monsters(main)
	await _check_scan(main)
	await _check_next_day_and_tape(main)
	_check_audio(main)
	await _check_footsteps(main)
	await _check_animation(main)
	_check_address_parse()
	_check_settings(main)
	_check_tips()
	await _check_voice(main)

	print("PASS" if _fails == 0 else "FAIL %d" % _fails)
	quit(0 if _fails == 0 else 1)


func _fresh(main: Node, day := 1) -> void:
	main.run = RunState.new_run(Tuning.data.run)
	main.run.seed = 4242
	main.run.day = day
	main._reset_house()
	main._next_spawn = 99  # no scheduled monsters in the way


func _mover(main: Node, id: int) -> CharacterBody3D:
	var p: CharacterBody3D = Player.new()
	p.name = str(id)
	if id != 1:
		p.set_multiplayer_authority(id)
	main.players.add_child(p)
	return p


func _lying(main: Node) -> Array:
	return main.loot.get_children().filter(func(n: Node) -> bool: return n.lying())


func _check_maze() -> void:
	for seed in [1, 2, 3, 99]:
		var m: Dictionary = Interior.generate(seed, 6, 5, 6.0, 0.2)
		_expect(m.dist.size() == 30, "seed %d: only %d of 30 rooms reachable" % [seed, m.dist.size()])
		var e: Vector2i = m.exit
		_expect(e != Vector2i.ZERO and (e.x == 0 or e.y == 0 or e.x == 5 or e.y == 4), "seed %d: bad fire exit %s" % [seed, e])
	var a: Dictionary = Interior.generate(hash([5, 1, "interior"]), 6, 5, 6.0, 0.2)
	var b: Dictionary = Interior.generate(hash([5, 2, "interior"]), 6, 5, 6.0, 0.2)
	_expect(a.open != b.open, "next day's mansion has the same layout")


func _check_layouts(main: Node) -> void:
	main.run.seed = 4242
	var want: Array = Tuning.data.loot_per_day
	for day in [1, 2, 3]:
		main.layout_day(day)
		var live := _lying(main)
		_expect(live.size() == int(want[day - 1]), "day %d: %d pieces, expected %d" % [day, live.size(), want[day - 1]])
		for i in live.size():
			_expect(main.interior.contains(live[i].global_position), "day %d: %s is not inside the mansion" % [day, live[i].name])
			var d: Dictionary = Tuning.data.scrap.filter(func(e: Dictionary) -> bool: return e.name == live[i].kind)[0]
			_expect(live[i].value >= int(d.value[0]) and live[i].value <= roundi(d.value[1] * 2.0), "%s value %d out of range" % [live[i].name, live[i].value])
			for j in range(i + 1, live.size()):
				_expect(live[i].global_position.distance_to(live[j].global_position) > 1.0, "day %d: %s on top of %s" % [day, live[i].name, live[j].name])
			var at := Vector2(live[i].global_position.x, live[i].global_position.z)
			_expect(main.interior.blocked.all(func(b: Rect2) -> bool: return not b.has_point(at)), "day %d: %s spawned inside furniture" % [day, live[i].name])
		var kinds: Array = main.interior.room_kinds.values()
		_expect(main.interior.room_kinds[Vector2i.ZERO] == "hall", "entrance is not the hall")
		_expect(kinds.filter(func(k: String) -> bool: return k != "hall").size() == kinds.size() - 1 and Array(kinds).duplicate().reduce(func(acc: Dictionary, k: String) -> Dictionary: acc[k] = true; return acc, {}).size() >= 4,
			"day %d: rooms barely vary: %s" % [day, kinds])
		_expect(main.interior.blocked.size() >= main.interior.room_kinds.size(), "day %d: only %d pieces of furniture for %d rooms" % [day, main.interior.blocked.size(), main.interior.room_kinds.size()])
	var snap := func() -> Array:
		return main.loot.get_children().map(func(n: Node) -> Array: return [n.in_play, n.global_position.snapped(Vector3.ONE * 0.01), n.value])
	main.layout_day(2)
	var a1: Array = snap.call()
	main.layout_day(2)
	_expect(snap.call() == a1, "same seed + day gave a different scrap layout")
	main.layout_day(3)
	_expect(snap.call() != a1, "day 3 did not reshuffle")


## Each door with E: the front door takes you to the entrance room and back out, the fire
## exit to the far room and back.
func _check_doors(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	var it: Node = main.interior
	for i in 4:
		p.global_position = it.doors[i].at + it.doors[i].inward * 1.0
		p.use_door(i)
		var other: Dictionary = it.doors[i ^ 1]
		_expect(p.global_position.distance_to(other.at) < 2.0, "door '%s' did not lead to '%s'" % [it.doors[i].label, other.label])
		_expect(it.contains(p.global_position) == (i % 2 == 0), "door '%s' landed on the wrong side" % it.doors[i].label)
	await _frames(main, 3)
	p.free()


## E picks up into a slot (up to 4), the host refuses a 5th; a two-handed piece in hand
## blocks switching; G puts the piece down on the floor where you stand.
func _check_pickup_and_drop(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	var pieces := _lying(main).filter(func(n: Node) -> bool: return not n.two_handed)
	var heavy: Node = main.loot.get_children().filter(func(n: Node) -> bool: return n.two_handed)[0]
	for i in 5:
		p.global_position = pieces[i].global_position + Vector3(0.5, 0, 0)
		var ok: bool = main.pickup(1, pieces[i].name)
		_expect(ok == (i < 4), "pickup %d returned %s" % [i, ok])
	_expect(not p.slots.has("") and pieces[0].holder == 1 and not pieces[0].lying(), "slots after 4 pickups %s" % [p.slots])
	_expect(pieces[4].lying(), "a 5th piece was taken with full slots")
	p.slot = 2
	var name2: String = p.slots[2]
	p.global_position = Vector3(0, 0, -5)
	p.drop_active()
	await _frames(main, 30)
	var dropped: Node = main.loot.get_node(name2)
	_expect(p.slots[2] == "" and dropped.lying() and dropped.global_position.distance_to(p.global_position) < 1.5,
		"G did not put the piece down in front (%s)" % dropped.global_position)
	heavy.in_play = true
	heavy.place(p.global_position + Vector3(0.5, 0, 0))
	_expect(main.pickup(1, heavy.name) and p.slots[2] == heavy.name, "two-handed piece did not go into the free hand")
	p.switch_slot(0)
	_expect(p.slot == 2, "switched away from a two-handed piece")
	p.free()


func _check_weight_and_stamina(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	_expect(is_equal_approx(p.carry_factor(), 1.0), "empty hands slowed down")
	var heavy: Node = main.loot.get_children().filter(func(n: Node) -> bool: return n.kind == "safe")[0]
	heavy.in_play = true
	heavy.place(p.global_position)
	main.pickup(1, heavy.name)
	_expect(p.carry_factor() < 0.7, "a 25 kg safe barely slowed: %.2f" % p.carry_factor())
	p.update_stamina(Tuning.data.stamina_seconds + 1.0, true)
	_expect(p.stamina == 0.0, "sprinting did not drain stamina")
	p.update_stamina(Tuning.data.stamina_regen_seconds, false)
	_expect(p.stamina == 1.0, "resting did not refill stamina")
	p.free()


## Scrap on the bed and scrap carried by someone aboard count; scrap carried by the one left
## behind does not, and it stays where they died.
func _check_depart(main: Node) -> void:
	_fresh(main)
	var aboard := _mover(main, 1)
	var inside := _mover(main, 400)
	var bed: Vector3 = main.truck.bed.get_center() * Vector3(1, 0, 1)
	var pieces := _lying(main)
	# keep the piece clear of the mover: a body dropped on top of it can shove it off the bed
	# (physics depenetration differs between machines — this failed only on the Linux CI)
	pieces[0].place(bed + Vector3(1.0, 0, 1.0))
	aboard.global_position = bed - Vector3(0.8, 0, 0.8)
	pieces[1].place(aboard.global_position + Vector3(0.6, 0, 0))
	main.pickup(1, pieces[1].name)
	inside.global_position = main.interior.cell_center(Vector2i(1, 1))
	pieces[2].place(inside.global_position)
	main.pickup(400, pieces[2].name)
	await _frames(main, 20)
	_expect(main.truck.bed.has_point(pieces[0].global_position) and main.at_truck(aboard.global_position),
		"setup: piece %s / mover %s not on the bed %s" % [pieces[0].global_position, aboard.global_position, main.truck.bed])
	var want: int = pieces[0].value + pieces[1].value
	_expect(main.truck_value() == want, "truck value %d, expected %d" % [main.truck_value(), want])
	var r: Dictionary = main._depart()
	_expect(r.total == want, "depart paid %s, expected %d" % [r, want])
	_expect(main.dead.has(400) and pieces[2].lying() and main.interior.contains(pieces[2].global_position),
		"left-behind mover's scrap did not stay in the mansion")
	aboard.free()
	inside.free()


func _check_ghost_states() -> void:
	var c := {sight = 7.0, hit_range = 0.9}
	_expect(Ghost.next_state("patrol", INF, 0.0, c) == "patrol", "patrol with nobody near")
	_expect(Ghost.next_state("patrol", 3.0, 0.0, c) == "chase", "patrol -> chase when someone in sight")
	_expect(Ghost.next_state("chase", 0.5, 0.0, c) == "attack", "chase -> attack in range")
	_expect(Ghost.next_state("chase", INF, 0.0, c) == "patrol", "chase -> patrol when target lost")
	_expect(Ghost.next_state("cooldown", 0.5, 1.0, c) == "cooldown", "cooldown holds while time left")
	_expect(Ghost.next_state("cooldown", 0.5, 0.0, c) == "patrol", "cooldown -> patrol when over")


## The only mover stands near the ghost carrying scrap: first catch knocks them down (keeps the
## scrap, day goes on); a second catch kills — the scrap drops and the day fails.
func _check_ghost_attack(main: Node) -> void:
	_fresh(main)
	var failed := [false]
	var on_fail := func() -> void: failed[0] = true
	main.day_failed.connect(on_fail)
	main.run.time_left -= Tuning.data.ghost.start_delay  # past the morning grace period
	var p := _mover(main, 1)
	p.global_position = main.ghost.global_position * Vector3(1, 0, 1) + Vector3(1.5, main.interior.ORIGIN.y, -1.0)
	var piece: Node = _lying(main)[0]
	piece.place(p.global_position)
	main.pickup(1, piece.name)
	for i in 240:
		if p.stunned:
			break
		await physics_frame
	_expect(p.stunned and not p.dead and not main.dead.has(1), "first catch: stunned %s dead %s" % [p.stunned, p.dead])
	_expect(not failed[0] and p.slots.has(piece.name), "first catch failed the day or took the scrap")
	_expect(main.ghost.state == "cooldown", "ghost state after attack: %s" % main.ghost.state)
	var at: Vector3 = main.ghost.global_position
	await _frames(main, 30)
	_expect(main.ghost.global_position.distance_to(at) > 0.3, "ghost hovers over the downed mover")
	main.ghost_hit(1)
	_expect(p.dead and main.dead.has(1), "second catch did not kill")
	_expect(piece.lying() and not p.slots.has(piece.name), "dead mover still carries the scrap")
	_expect(failed[0], "day did not fail with everyone dead")
	main.day_failed.disconnect(on_fail)
	p.free()


## A flashlight on the ghost: she flinches and backs off, and that light drains much faster.
## Lights off (or pointed away): she comes on as normal.
func _check_ghost_flashlight(main: Node) -> void:
	_fresh(main)
	main.run.time_left -= Tuning.data.ghost.start_delay
	var p := _mover(main, 1)
	var g: Node3D = main.ghost
	p.global_position = g.global_position * Vector3(1, 0, 1) + Vector3(0, main.interior.ORIGIN.y, 4.0)
	p.rotation.y = 0.0  # facing -z, straight at her
	p.head.rotation.x = 0.0
	await _frames(main, 2)
	g.global_position = p.cam.global_position + Vector3(0, 0, -4.0)
	var before := g.global_position.distance_to(p.global_position)
	await _frames(main, 20)
	_expect(g.state == "flinch", "lit ghost state: %s" % g.state)
	_expect(g.global_position.distance_to(p.global_position) > before + 0.3, "lit ghost did not back off")
	var b0: float = p.battery
	p.update_battery(1.0, false)
	var lit_drain: float = b0 - p.battery
	p.flash_on = false
	b0 = p.battery
	await _frames(main, 2)
	_expect(not g.lit_by(p), "ghost counts as lit with the light off")
	p.flash_on = true
	p.rotation.y = PI  # pointed away
	await _frames(main, 2)
	_expect(not g.lit_by(p), "ghost counts as lit behind the mover")
	p.update_battery(1.0, false)
	_expect(lit_drain > (b0 - p.battery) * 3.0, "holding her off did not drain faster (%.4f vs %.4f)" % [lit_drain, b0 - p.battery])
	p.free()


## Searchable furniture: some hide a piece (parked until searched); E drops it out in front,
## once — a second search does nothing. Empty ones say so. A new day shuts them all again.
func _check_search(main: Node) -> void:
	_fresh(main)
	var I: Node = main.interior
	_expect(I.containers.size() >= 5, "only %d searchable pieces of furniture" % I.containers.size())
	_expect(not main.container_loot.is_empty(), "no furniture hides scrap today")
	var full: int = main.container_loot.keys()[0]
	var empty := -1
	for i in I.containers.size():
		if not main.container_loot.has(i):
			empty = i
			break
	var hidden: Node = main.loot.get_node(main.container_loot[full])
	_expect(not hidden.lying(), "hidden piece is already lying out")
	var p := _mover(main, 1)
	p.global_position = I.containers[full].at + I.containers[full].front
	_expect(main.search(1, full), "search failed next to the wardrobe")
	_expect(hidden.lying() and hidden.global_position.distance_to(p.global_position) < 1.5, "searched piece did not drop out in front")
	_expect(not main.search(1, full), "searched the same thing twice")
	p.global_position = Vector3(0, 0, -30)
	if empty >= 0:
		_expect(not main.search(1, empty), "searched from across the map")
	main.run.phase = "shop"
	main.next_day()
	_expect(main.opened.is_empty(), "containers still open on the new day")
	p.free()


## A stunned mover can't pick up; a new day clears the stun.
func _check_stun_reset(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	var piece: Node = _lying(main)[0]
	p.global_position = piece.global_position
	main.stun_player(1, 4.0, false)
	_expect(not main.pickup(1, piece.name), "stunned mover picked something up")
	main.run.phase = "shop"
	main.next_day()
	_expect(not p.stunned and not main.is_stunned(1), "new day kept the player's stun")
	piece = _lying(main)[0]
	p.global_position = piece.global_position
	_expect(main.pickup(1, piece.name), "recovered mover cannot pick up on the new day")
	p.free()


func _check_run_rules() -> void:
	var cfg := {quota_base = 100, quota_growth = 2.0, cycle_days = 2, start_money = 100, day_seconds = 60,
		shop = {shoes = {price = 50, speed = 0.15}, tape = {price = 45, tapes = 1, stack = true}}}
	var r := RunState.new_run(cfg)
	r = RunState.end_day(r, 60, cfg)
	_expect(r.phase == "shop" and r.day == 2 and r.haul == 60 and r.money == 100, "day 1 goes to haul %s" % r)
	_expect(RunState.buy(r, "shoes", cfg) and r.money == 50 and is_equal_approx(r.speed_bonus, 0.15), "buy shoes %s" % r)
	_expect(not RunState.buy(r, "shoes", cfg), "shoes bought twice")
	_expect(RunState.buy(r, "tape", cfg) and r.tapes == 1 and not RunState.buy(r, "tape", cfg), "tape / no money %s" % r)
	RunState.start_day(r, cfg)
	_expect(not RunState.buy(r, "tape", cfg), "bought during a day")
	r = RunState.end_day(r, 50, cfg)
	_expect(r.phase == "shop" and r.cycle == 2 and r.haul == 0 and r.money == 115
		and RunState.quota(r, cfg) == 200, "quota met pays haul, quota grows %s" % r)
	RunState.start_day(r, cfg)
	var w := RunState.end_day(r, 150, cfg)
	w = RunState.end_day(w, 100, cfg, "", true)
	_expect(w.phase == "over" and w.haul == 0, "a wipe loses the haul and misses the quota %s" % w)
	var m := RunState.end_day(r, 150, cfg)
	m = RunState.end_day(m, 49, cfg)
	_expect(m.phase == "over" and m.money == 115, "199 of 200 gets fired %s" % m)


## The truck leaves: a mover at the truck lives, one inside dies and costs the fine;
## all dead is a wipe.
func _check_left_behind(main: Node) -> void:
	_fresh(main)
	var aboard := _mover(main, 1)
	var inside := _mover(main, 400)
	aboard.global_position = main.truck.bed.get_center() * Vector3(1, 0, 1)
	inside.global_position = main.interior.cell_center(Vector2i(1, 0))
	main._depart()
	_expect(not aboard.dead and main.dead.has(400) and not main.dead.has(1), "left-behind rule %s" % main.dead)
	_expect(main.run.haul == 0 and main.run.phase == "shop", "fine did not eat the empty-truck pay %s" % main.run)
	main.next_day()
	_expect(main.dead.is_empty() and not aboard.dead, "next day kept the dead")
	aboard.global_position = main.interior.cell_center(Vector2i(1, 0))
	main.run.haul = 77
	main._depart()
	_expect(main.run.haul == 0 and main.dead.size() == 2, "everyone left behind should wipe the haul %s" % main.run)
	aboard.free()
	inside.free()


func _check_battery_and_blackout(main: Node) -> void:
	_fresh(main)
	var c: Dictionary = Tuning.data.flashlight
	var p := _mover(main, 500)
	p.update_battery(c.battery_seconds / 2.0, false)
	_expect(is_equal_approx(p.battery, 0.5) and p.flash_on, "half drain %.2f" % p.battery)
	p.flash_on = false
	p.update_battery(100.0, false)
	_expect(is_equal_approx(p.battery, 0.5), "light off still drained %.2f" % p.battery)
	p.flash_on = true
	p.update_battery(c.battery_seconds, false)
	_expect(p.battery == 0.0 and not p.flash_on, "empty battery kept the light on")
	p.update_battery(c.recharge_seconds, true)
	_expect(p.battery == 1.0, "truck did not recharge %.2f" % p.battery)
	p.free()
	# F really turns it off and it stays off (the real key path, a few frames later).
	var me := _mover(main, 1)
	await _frames(main, 2)
	var f := InputEventKey.new()
	f.physical_keycode = KEY_F
	f.pressed = true
	me._unhandled_input(f)
	await _frames(main, 10)
	_expect(not me.flash_on and not me.flashlight.visible, "F: flash_on %s, light %s" % [me.flash_on, me.flashlight.visible])
	me._unhandled_input(f)
	await _frames(main, 2)
	_expect(me.flash_on and me.flashlight.visible, "F again did not turn it back on")
	me.free()
	var lights: Array = main.interior.lights
	var bulb: OmniLight3D = lights.filter(func(l: OmniLight3D) -> bool: return not l.get_meta("emergency"))[0]
	var exit_lamp: OmniLight3D = lights.filter(func(l: OmniLight3D) -> bool: return l.get_meta("emergency"))[0]
	var cfg: Dictionary = Tuning.data.run
	main.run.time_left = cfg.day_seconds - cfg.blackout_after + 5.0
	await process_frame
	_expect(bulb.visible, "bulbs off before the blackout")
	main.run.time_left = cfg.day_seconds - cfg.blackout_after - 1.0
	await process_frame
	_expect(not bulb.visible and exit_lamp.visible, "blackout: bulb %s, fire-exit lamp %s" % [bulb.visible, exit_lamp.visible])
	main.run.time_left = cfg.day_seconds


## Truck radio: only at the truck; holds you still, shows the map; the horn sends the ghost
## toward the room farthest from the movers inside; a remote mover on the radio is heard flat.
func _check_radio(main: Node) -> void:
	_fresh(main)
	main.run.time_left -= Tuning.data.ghost.start_delay + 1.0  # ghost awake
	var p := _mover(main, 1)
	p.global_position = Vector3(0, 0, -2)
	p.set_radio(true)
	_expect(not p.on_radio, "radio opened away from the truck")
	p.global_position = main.truck.bed.get_center() * Vector3(1, 0, 1)
	p.set_radio(true)
	await _frames(main, 3)
	_expect(p.on_radio and main.radio_map.visible, "radio did not open at the truck")
	var before: Vector3 = p.global_position
	await _frames(main, 10)
	_expect(p.global_position.distance_to(before) < 0.05, "radio operator drifted")
	var friend := _mover(main, 600)
	friend.global_position = main.interior.cell_center(Vector2i.ZERO)
	var target: Vector3 = main.interior.cell_center(main.empty_room())
	_expect(main.interior.cell_of(target) != Vector2i.ZERO, "empty room is where the mover stands")
	var g0: float = main.ghost.global_position.distance_to(target)
	_expect(main.lure(1), "horn did not lure")
	_expect(not main.lure(1), "horn ignored its cooldown")
	await _frames(main, 60)
	var g1: float = main.ghost.global_position.distance_to(target)
	_expect(g1 < g0 - 1.0 or g1 < 1.5, "lured ghost did not head for the empty room (%.1f -> %.1f)" % [g0, g1])
	await _frames(main, 3)
	_expect(friend._speaker.attenuation_model == AudioStreamPlayer3D.ATTENUATION_DISABLED,
		"teammate not heard over the radio")
	p.set_radio(false)
	await _frames(main, 3)
	_expect(friend._speaker.attenuation_model != AudioStreamPlayer3D.ATTENUATION_DISABLED
		and not main.radio_map.visible, "radio stayed on after closing")
	main.run.phase = "shop"
	main.next_day()
	_expect(main.lure(1) == false, "lure worked between days")
	friend.free()
	p.free()


func _check_monsters(main: Node) -> void:
	var line := {open = {Interior.edge_key(Vector2i(0, 0), Vector2i(1, 0)): true,
		Interior.edge_key(Vector2i(1, 0), Vector2i(1, 1)): true}}
	_expect(Monster.next_cell(line, Vector2i(0, 0), Vector2i(1, 1)) == Vector2i(1, 0), "maze path step")
	_expect(Monster.next_cell(line, Vector2i(0, 0), Vector2i(0, 1)) == Vector2i(0, 0), "walked through a wall")
	var fresh := func() -> void:
		_fresh(main)
		main.run.time_left = 200.0
		main.ghost.exorcise(999.0)  # keep the ghost out of these
	var p := _mover(main, 1)
	var it: Node = main.interior
	var by_kind := func(k: String) -> Node: return main.monsters.filter(func(m: Node) -> bool: return m.kind == k)[0]

	fresh.call()
	# a room whose decor can't sit between the two: use the entrance's straight middle line
	var room: Vector3 = it.cell_center(Vector2i(1, 1))
	var statue: Node = by_kind.call("statue")
	p.global_position = room - Vector3(1.5, 0, 0)
	p.rotation.y = -PI / 2  # facing +x, at the statue
	statue.wake(room + Vector3(1.5, 0, 0))
	await _frames(main, 40)
	_expect(statue.global_position.distance_to(room + Vector3(1.5, 0, 0)) < 0.05, "statue moved while watched")
	p.rotation.y = PI / 2   # turn away
	for i in 120:
		if main.dead.has(1):
			break
		await physics_frame
	_expect(main.dead.has(1), "statue never got the mover who looked away")

	fresh.call()
	var dust: Node = by_kind.call("dust")
	dust.wake(room)
	p.global_position = room + Vector3(0.3, 0, 0)
	await _frames(main, 5)
	_expect(p.blinded and dust.state == "latched", "walking under the dust ghost did not blind")
	_expect(main.free_player(1) and not p.blinded and not dust.active(), "teammate could not pull it off")
	dust.wake(room)
	await _frames(main, 5)
	dust._t = Tuning.data.monsters.dust.latch_kill
	await _frames(main, 3)
	_expect(main.dead.has(1) and not p.blinded, "nobody freed the mover and they lived")

	fresh.call()
	var boar: Node = by_kind.call("boar")
	boar.wake(room + Vector3(1.5, 0, 0))
	p.global_position = room - Vector3(1.5, 0, 0)
	var charged := false
	for i in 180:
		charged = charged or boar.state == "charge"
		if main.dead.has(1):
			break
		await physics_frame
	_expect(charged and main.dead.has(1), "boar did not charge down the mover (state %s)" % boar.state)

	fresh.call()
	boar.wake(room)
	boar.state = "wander"
	boar._steps.stop()
	p.global_position = Vector3(0, 0, -15)  # outside: nobody to charge
	for i in 90:
		await physics_frame
		boar._process(0.016)
		if boar._steps.playing:
			break
	_expect(boar._steps.playing, "a wandering boar made no footsteps")

	fresh.call()
	main.run.day = 3
	main._reset_house()
	main.run.time_left = Tuning.data.run.day_seconds - 200.0
	p.global_position = Vector3(0, 0, -15)  # outside, out of their way
	await _frames(main, 3)
	var plan: Array = Tuning.data.monsters.schedule[2]
	var due: int = plan.filter(func(e: Array) -> bool: return float(e[0]) <= 200.0).size()
	var awake: int = main.monsters.filter(func(m: Node) -> bool: return m.active()).size()
	_expect(awake == due, "day 3 at 200 s should have %d monsters out, has %d" % [due, awake])
	for m in main.monsters:
		if m.active():
			_expect(it.contains(Vector3(m.global_position.x, Interior.ORIGIN.y, m.global_position.z)), "%s woke outside the mansion" % m.name)
			_expect(it.maze.dist[it.cell_of(m.global_position)] >= 2, "%s woke next to the entrance" % m.name)
	p.free()
	_fresh(main)


## Scan from inside a room: the piece in front shows with its value; one moved into a room
## behind a wall doesn't.
func _check_scan(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	await _frames(main, 2)
	var pieces := _lying(main)
	var room: Vector3 = main.interior.cell_center(Vector2i(0, 0))
	pieces[0].place(room + Vector3(1.0, 0, 0))
	var walled: Vector2i = Vector2i(0, 1) if not main.interior.maze.open.has(Interior.edge_key(Vector2i.ZERO, Vector2i(0, 1))) else Vector2i(1, 0)
	var behind: Node = pieces[1]
	var room2: Vector3 = main.interior.cell_center(walled)
	behind.place(room2)
	p.global_position = room - Vector3(1.0, 0, 0)
	p.look_at(room2 * Vector3(1, 0, 1) + Vector3(0, p.global_position.y, 0) + Vector3(0.001, 0, 0))
	p.head.rotation.x = -0.3
	await _frames(main, 3)
	main._scan_ready_at = 0
	main.scan()
	var hit: Array = main._scan_hits.map(func(h: Dictionary) -> Node: return h.node)
	_expect(hit.has(pieces[0]) or main.interior.cell_of(pieces[0].global_position) != Vector2i.ZERO, "scan missed the piece in the room")
	if not main.interior.maze.open.has(Interior.edge_key(Vector2i.ZERO, walled)):
		_expect(not hit.has(behind), "scan saw through a wall")
	p.look_at(pieces[0].global_position * Vector3(1, 0, 1) + Vector3(0, p.global_position.y, 0) + Vector3(0.001, 0, 0))
	await _frames(main, 2)
	main._scan_ready_at = 0
	main.scan()
	hit = main._scan_hits.map(func(h: Dictionary) -> Node: return h.node)
	_expect(hit.has(pieces[0]) and main._scan_total.text.contains(str(pieces[0].value)), "scan of the piece ahead: %s" % main._scan_total.text)
	p.free()


## next_day from the shop lays out fresh scrap; a tape near the ghost hides it.
func _check_next_day_and_tape(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	var piece: Node = _lying(main)[0]
	p.global_position = piece.global_position
	main.pickup(1, piece.name)
	main.run.phase = "shop"
	main.next_day()
	_expect(main.run.phase == "day" and main.loot.get_children().all(func(n: Node) -> bool: return n.holder == 0)
		and not p.slots.any(func(n: String) -> bool: return n != ""), "next day kept carried scrap")
	_expect(_lying(main).size() == int(Tuning.data.loot_per_day[main.run.day - 1]), "new day scrap count")
	_expect(main.ghost.visible, "ghost invisible after reset")
	var g0: Vector3 = main.ghost.global_position
	await _frames(main, 60)
	_expect(main.ghost.global_position.distance_to(g0) < 0.1, "ghost moved during the morning grace period")
	main.run.time_left -= Tuning.data.ghost.start_delay
	p.global_position = main.ghost.global_position + Vector3(1, -1.2, 0)
	main.run.tapes = 0
	_expect(not main.use_tape(1), "tape used with none left")
	main.run.tapes = 1
	_expect(main.use_tape(1) and not main.ghost.visible and main.run.tapes == 0, "tape near ghost did nothing")
	p.free()


func _check_animation(main: Node) -> void:
	_fresh(main)
	var p := _mover(main, 1)
	await physics_frame
	var leg: Node3D = p.body.get_node("LegL")
	var swung := 0.0
	for i in 20:
		p._animate(3.0, 0.05)
		swung = maxf(swung, absf(leg.rotation.x))
	_expect(swung > 0.2, "legs don't swing while walking (max %.2f rad)" % swung)
	var piece: Node = _lying(main)[0]
	p.global_position = piece.global_position
	main.pickup(1, piece.name)
	for i in 30:
		p._animate(0.0, 0.05)
	var hand: Node3D = p.body.get_node("ArmR").get_child(1)
	_expect(p.carrying and hand.global_position.z < p.global_position.z - 0.3,
		"carrying: hand should be out in front (-z), hand z %.2f vs body %.2f" % [hand.global_position.z, p.global_position.z])
	await _frames(main, 2)
	_expect(p._hand != null and p._hand.get_parent() == p.cam, "the piece in hand is not shown in first person")
	p.free()


func _check_footsteps(main: Node) -> void:
	var p := _mover(main, 77)
	await physics_frame
	p._process(0.016)
	p._steps.stop()
	p._process(0.016)
	_expect(not p._steps.playing, "footstep while standing still")
	for i in 8:
		p.global_position += Vector3(0.1, 0, 0)
		p._process(0.016)
	_expect(p._steps.playing, "no footstep after walking 0.8 m")
	p.free()


func _check_audio(main: Node) -> void:
	var m: AudioStreamWAV = main._music.stream
	_expect(is_equal_approx(m.get_length(), 8.0) and m.loop_mode == AudioStreamWAV.LOOP_FORWARD, "music loop %.2f s" % m.get_length())
	_expect(main._sfx.size() == 5, "sfx players %d" % main._sfx.size())
	var calm := true
	var warped := true
	for i in 50:
		calm = calm and is_equal_approx(Sfx.music_pitch(0.0, i * 0.13), 1.0)
		warped = warped and Sfx.music_pitch(1.0, i * 0.13) < 0.99
	_expect(calm, "music pitch moves with no ghost near")
	_expect(warped, "music pitch not warped with the ghost on top of you")


func _check_settings(main: Node) -> void:
	var Settings = load("res://src/settings.gd")
	for k in Settings.SPEC:
		var spec: Array = Settings.SPEC[k]
		var v: float = Settings.get_value(k)
		_expect(v >= spec[1] and v <= spec[2], "setting %s out of range: %f" % [k, v])
	var keep: float = Settings.values.effects
	Settings.values.effects = 0.0
	main._apply_settings()
	_expect(is_equal_approx(main._vhs.get_shader_parameter("pixel_size"), 1.0), "effects 0 still pixelates")
	Settings.values.effects = 1.0
	main._apply_settings()
	_expect(main._vhs.get_shader_parameter("pixel_size") > 2.0, "effects 1 not pixelated")
	Settings.values.effects = keep
	main._apply_settings()


func _check_voice(main: Node) -> void:
	var Voice = load("res://src/voice.gd")
	var tone := PackedFloat32Array()
	for i in 640:
		tone.append(sin(i * 0.2) * 0.5)
	var back: PackedFloat32Array = Voice.decode(Voice.encode(tone))
	var worst := 0.0
	for i in tone.size():
		worst = maxf(worst, absf(back[i] - tone[i]))
	_expect(back.size() == tone.size() and worst < 0.001, "voice PCM round-trip error %f" % worst)
	var stereo := PackedVector2Array()
	stereo.resize(4410)  # 0.1 s at 44.1 kHz
	_expect(absi(Voice.downmix(stereo, 44100.0, 16000.0).size() - 1600) <= 1, "0.1 s should resample to 1600 samples")
	_expect(Voice.rms(tone) > Voice.GATE and Voice.rms(PackedFloat32Array([0.0, 0.001])) < Voice.GATE, "voice gate")
	var friend := _mover(main, 55)
	await physics_frame
	friend.receive_voice(Voice.encode(tone))
	_expect(friend.heard_chunks == 1 and friend.talk_t > 0.0, "remote player did not take a voice chunk")
	friend._rpc_voice(Voice.encode(tone))  # local call: sender 0, not peer 55 → ignored
	_expect(friend.heard_chunks == 1, "voice sent in someone else's name was played")
	friend.free()


func _check_tips() -> void:
	var Tips = load("res://src/tips.gd")
	var s := {captured = false, entered = false, picked_once = false, carrying = false, loaded_any = false}
	var seen := []
	seen.append(Tips.tip_for(1, "day", s))
	s.captured = true
	seen.append(Tips.tip_for(1, "day", s))
	s.entered = true
	seen.append(Tips.tip_for(1, "day", s))
	s.picked_once = true
	s.carrying = true
	seen.append(Tips.tip_for(1, "day", s))
	s.loaded_any = true
	seen.append(Tips.tip_for(1, "day", s))
	var distinct := {}
	for t in seen:
		distinct[t] = true
	_expect(distinct.size() == 5 and not distinct.has(""), "day-1 tips should be 5 distinct steps: %s" % [seen])
	_expect(Tips.tip_for(2, "day", s) == "" and Tips.tip_for(1, "shop", s) == "", "tips after day 1 / in shop")


func _check_address_parse() -> void:
	_expect(Net.parse_address("192.168.0.5", 7777) == ["192.168.0.5", 7777], "plain ip")
	_expect(Net.parse_address(" 1.2.3.4:9000 ", 7777) == ["1.2.3.4", 9000], "ip:port")
	_expect(Net.parse_address("", 7777) == ["127.0.0.1", 7777], "empty -> localhost")
	_expect(Net.parse_address("host.example:abc", 7777) == ["host.example:abc", 7777], "bad port kept as host")


func _frames(_main: Node, n: int) -> void:
	for i in n:
		await physics_frame


func _expect(ok: bool, msg: String) -> void:
	if not ok:
		_fails += 1
		print("FAIL: ", msg)
