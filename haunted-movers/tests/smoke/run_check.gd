extends SceneTree
## Smoke check.
## Story 001: the weight rule. One player can't lift the sofa, two can, a light
## chair lifts alone, and letting go drops the holder count.
## Story 002: payout math, drop damage, and departing with furniture on the truck.
## Story 003: the runaway wardrobe hops away from a nearby player; the piano bucks
## its holders off.
## Story 004: ghost state transitions; the ghost reaches a lone player carrying
## something, kills them (dead for the day), makes them let go, and fails the day.
## Story 005: run rules (endless growing quota over 3-day cycles, haul lost on a wipe,
## fired on a miss, shop), next day resets the house, a tape exorcises a nearby ghost.
## Battery: lit flashlight drains, empty stays off, truck recharges; bulbs die at the blackout.
## Truck: departing kills whoever is not at the truck, fines each death; nobody aboard wipes.
## Audio: the music loop is 8 s and loops; it only warps near the ghost; walking plays footsteps.
## Layouts: each day has its own piece count; nothing spawns overlapping; a run's seed replays
## the same house and a new day reshuffles it.
## Mimic: the moving box bites whoever holds it past the limit — knocked out, let go, teeth
## shown — but a bite never ends the day by itself.
## Animation: walking swings the legs; carrying puts both hands out in front.
## Level: two bots lift the sofa, turn it, carry it out the front door onto the
## truck, and depart — the core loop is physically possible in this house.
## Net: join addresses parse with or without a port.
## Settings: defaults are in range; effects 0 turns pixelation off.
## Voice: PCM round-trips, resampling keeps duration, a remote player plays its owner's
## chunks and ignores chunks sent in someone else's name.
## Tips: the first-day guide walks click → grab → (too heavy) → load → depart, and is quiet after day 1.
## Prints PASS or FAIL n.
##   godot --headless --path . -s res://tests/smoke/run_check.gd

const Payout = preload("res://src/payout.gd")
const Ghost = preload("res://src/ghost.gd")
const Player = preload("res://src/player.gd")
const RunState = preload("res://src/run_state.gd")
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
	main.layout_day(3)  # day 3 has every piece in play (piano, mimic box, bed…)
	await _frames(main, 40)  # let furniture settle on the floor

	var sofa: RigidBody3D = main.furniture.get_node("sofa")
	var chair: RigidBody3D = main.furniture.get_node("chair")
	var y0 := sofa.global_position.y

	await _hold(main, sofa, [100], 90)
	_expect(sofa.global_position.y - y0 < 0.3, "one player lifted the sofa by %.2f m" % (sofa.global_position.y - y0))
	_expect(sofa.holders == 1, "sofa holders %d, expected 1" % sofa.holders)

	await _hold(main, sofa, [100, 101], 120)
	_expect(sofa.global_position.y - y0 > 0.5, "two players lifted the sofa only %.2f m" % (sofa.global_position.y - y0))
	_expect(sofa.holders == 2, "sofa holders %d, expected 2" % sofa.holders)

	var c0 := chair.global_position.y
	await _hold(main, chair, [100], 90)
	_expect(chair.global_position.y - c0 > 0.5, "one player lifted the chair only %.2f m" % (chair.global_position.y - c0))

	await _frames(main, 30)  # no more updates: grabs time out
	_expect(main.grabs.is_empty(), "grabs not cleared after release: %s" % [main.grabs.keys()])
	_expect(sofa.holders == 0 and chair.holders == 0, "holders not reset after release")

	_check_payout()
	await _check_drop_damage(main)
	await _check_depart(main)
	await _check_runaway(main)
	await _check_buck(main)
	_check_ghost_states()
	_expect(main.run.phase == "shop" and main.run.day == 2, "depart did not end day 1: %s" % main.run)
	main.next_day()
	await _check_ghost_attack(main)
	_check_stun_reset(main)
	_check_run_rules()
	_check_left_behind(main)
	await _check_battery_and_blackout(main)
	await _check_next_day_and_tape(main)
	_check_audio(main)
	await _check_footsteps(main)
	await _check_animation(main)
	await _check_mimic(main)
	_check_layouts(main)
	_check_address_parse()
	_check_settings(main)
	_check_tips()
	await _check_voice(main)
	await _check_carry_to_truck(main)

	print("PASS" if _fails == 0 else "FAIL %d" % _fails)
	quit(0 if _fails == 0 else 1)


## Two 1 m cubes side by side fill their box: efficiency 1. A gap lowers it.
## A broken item pays nothing but still counts as broken.
func _check_payout() -> void:
	var a := AABB(Vector3(0, 0, 0), Vector3.ONE)
	var b := AABB(Vector3(1, 0, 0), Vector3.ONE)
	var r := Payout.compute([{value = 100.0, durability = 1.0, aabb = a}, {value = 100.0, durability = 0.5, aabb = b}], 0.5, 0.5)
	_expect(r.base == 150 and is_equal_approx(r.efficiency, 1.0) and r.bonus == 75 and r.total == 225,
		"tight packing payout %s" % r)
	var far := AABB(Vector3(3, 0, 0), Vector3.ONE)
	var loose := Payout.compute([{value = 100.0, durability = 1.0, aabb = a}, {value = 100.0, durability = 1.0, aabb = far}], 0.5, 0.5)
	_expect(loose.efficiency < 0.6 and loose.bonus < 100, "gappy packing should earn less bonus %s" % loose)
	var broke := Payout.compute([{value = 100.0, durability = 0.0, aabb = a}], 0.5, 0.5)
	_expect(broke.total == 0 and broke.broken == 1, "broken item %s" % broke)
	_expect(Payout.compute([], 0.5, 0.5).total == 0, "empty truck pays 0")


## A chair dropped from 1.5 m (under the 2F slab) loses durability.
func _check_drop_damage(main: Node) -> void:
	var chair: RigidBody3D = main.furniture.get_node("chair")
	chair.durability = 1.0
	chair.global_position += Vector3(0, 1.5, 0)
	chair.linear_velocity = Vector3.ZERO
	await _frames(main, 90)
	_expect(chair.durability < 1.0, "dropped chair kept durability %.2f" % chair.durability)


## Put the lamp on the truck and depart: one item loaded, paid.
func _check_depart(main: Node) -> void:
	var box: RigidBody3D = main.furniture.get_node("lamp")
	box.global_position = main.truck.bed.get_center() * Vector3(1, 0, 1) + Vector3(0, 0.3, 0)
	box.linear_velocity = Vector3.ZERO
	await _frames(main, 60)
	var r: Dictionary = main._depart()
	_expect(r.loaded == 1 and r.total > 0, "depart with box on truck %s" % r)


## A stand-in player 1.5 m from the wardrobe: it hops away.
func _check_runaway(main: Node) -> void:
	var w: RigidBody3D = main.furniture.get_node("wardrobe")
	var fake := Node3D.new()
	fake.add_to_group("players")
	main.add_child(fake)
	fake.global_position = w.global_position + Vector3(1.5, 0, 0)
	var d0 := (w.global_position - fake.global_position).length()
	await _frames(main, 180)
	var d1 := (w.global_position - fake.global_position).length()
	_expect(d1 > d0 + 0.5, "runaway wardrobe only moved from %.2f to %.2f m" % [d0, d1])
	fake.free()


## Holding the piano for 4 s makes it buck at least once (interval 3 s).
func _check_buck(main: Node) -> void:
	var piano: RigidBody3D = main.furniture.get_node("piano")
	var count := [0]
	piano.bucked.connect(func(_b: RigidBody3D) -> void: count[0] += 1)
	await _hold(main, piano, [100], 240)
	_expect(count[0] >= 1, "piano never bucked")


func _check_ghost_states() -> void:
	var c := {sight = 7.0, hit_range = 0.9}
	_expect(Ghost.next_state("patrol", INF, 0.0, c) == "patrol", "patrol with nobody near")
	_expect(Ghost.next_state("patrol", 3.0, 0.0, c) == "chase", "patrol -> chase when someone in sight")
	_expect(Ghost.next_state("chase", 0.5, 0.0, c) == "attack", "chase -> attack in range")
	_expect(Ghost.next_state("chase", INF, 0.0, c) == "patrol", "chase -> patrol when target lost")
	_expect(Ghost.next_state("cooldown", 0.5, 1.0, c) == "cooldown", "cooldown holds while time left")
	_expect(Ghost.next_state("cooldown", 0.5, 0.0, c) == "patrol", "cooldown -> patrol when over")


## The only player stands near the ghost holding the chair: they get knocked out,
## let go, and the day fails because everyone (one person) is out.
func _check_ghost_attack(main: Node) -> void:
	var failed := [false]
	main.day_failed.connect(func() -> void: failed[0] = true)
	main.run.time_left -= Tuning.data.ghost.start_delay  # past the morning grace period
	var p: CharacterBody3D = Player.new()
	p.name = "1"
	main.players.add_child(p)
	p.global_position = main.ghost.global_position * Vector3(1, 0, 1) + Vector3(1.5, 0, -1.0)
	var chair: RigidBody3D = main.furniture.get_node("chair")
	p.held = chair
	for i in 240:
		if p.stunned:
			break
		main.hold(1, "chair", Vector3.ZERO, chair.global_position)
		await physics_frame
	_expect(p.stunned and p.dead and main.dead.has(1), "ghost never killed the player")
	_expect(p.held == null and not main.grabs.has(1), "knocked-out player still holds the chair")
	_expect(failed[0], "day did not fail with everyone knocked out")
	_expect(main.ghost.state == "cooldown", "ghost state after attack: %s" % main.ghost.state)
	p.free()


## Late packets cannot re-grab after a stun; a new day clears the local stun too.
func _check_stun_reset(main: Node) -> void:
	var p: CharacterBody3D = Player.new()
	p.name = "1"
	main.players.add_child(p)
	var chair: RigidBody3D = main.furniture.get_node("chair")
	main.stun_player(1, 4.0, false)
	main.hold(1, "chair", Vector3.ZERO, chair.global_position)
	_expect(not main.grabs.has(1), "late grab packet reattached a stunned player")
	main.run.phase = "shop"
	main.next_day()
	_expect(not p.stunned and is_zero_approx(p._stun_left) and not main.is_stunned(1),
		"new day kept the player's stun")
	main.hold(1, "chair", Vector3.ZERO, chair.global_position)
	_expect(main.grabs.has(1), "recovered player cannot grab on the new day")
	main.grabs.clear()
	p.free()


func _check_run_rules() -> void:
	var cfg := {quota_base = 100, quota_growth = 2.0, cycle_days = 2, start_money = 100, day_seconds = 60,
		shop = {cart = {price = 50, strength = 0.35}, tape = {price = 45, tapes = 1, stack = true}}}
	var r := RunState.new_run(cfg)
	r = RunState.end_day(r, 60, cfg)
	_expect(r.phase == "shop" and r.day == 2 and r.haul == 60 and r.money == 100, "day 1 goes to haul %s" % r)
	_expect(RunState.buy(r, "cart", cfg) and r.money == 50 and is_equal_approx(r.strength_mult, 1.35), "buy cart %s" % r)
	_expect(not RunState.buy(r, "cart", cfg), "cart bought twice")
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


## The truck leaves: a mover at the truck lives, one in the house dies and costs the fine;
## all dead is a wipe.
func _check_left_behind(main: Node) -> void:
	main.run = RunState.new_run(Tuning.data.run)
	main._reset_house()
	var aboard: CharacterBody3D = Player.new()
	aboard.name = "1"
	var inside: CharacterBody3D = Player.new()
	inside.name = "400"
	inside.set_multiplayer_authority(400)
	main.players.add_child(aboard)
	main.players.add_child(inside)
	aboard.global_position = main.truck.bed.get_center() * Vector3(1, 0, 1)
	inside.global_position = Vector3(0, 0, 8)
	main._depart()
	_expect(aboard.dead == false and main.dead.has(400) and not main.dead.has(1), "left-behind rule %s" % main.dead)
	_expect(main.run.haul == 0 and main.run.phase == "shop", "fine did not eat the empty-truck pay %s" % main.run)
	main.next_day()
	_expect(main.dead.is_empty() and not aboard.dead, "next day kept the dead")
	aboard.global_position = Vector3(0, 0, 8)
	main.run.haul = 77
	main._depart()
	_expect(main.run.haul == 0 and main.dead.size() == 2, "everyone left behind should wipe the haul %s" % main.run)
	main.run = RunState.new_run(Tuning.data.run)
	main._reset_house()
	aboard.free()
	inside.free()


## The flashlight drains while lit, dies empty and won't relight; the truck recharges it.
## Halfway into the day the house bulbs go out.
func _check_battery_and_blackout(main: Node) -> void:
	var c: Dictionary = Tuning.data.flashlight
	var p: CharacterBody3D = Player.new()
	p.name = "500"
	p.set_multiplayer_authority(500)
	main.players.add_child(p)
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
	var house: Node = main.get_node("House")
	var cfg: Dictionary = Tuning.data.run
	main.run.phase = "day"
	main.run.time_left = cfg.day_seconds - cfg.blackout_after + 5.0
	await process_frame
	_expect(house._bulbs[0].visible, "bulbs off before the blackout")
	main.run.time_left = cfg.day_seconds - cfg.blackout_after - 1.0
	await process_frame
	_expect(not house._bulbs[0].visible, "no blackout after %.0f s" % cfg.blackout_after)
	main.run.time_left = cfg.day_seconds


## next_day from the shop puts the furniture back; a tape near the ghost hides it.
func _check_next_day_and_tape(main: Node) -> void:
	var sofa: RigidBody3D = main.furniture.get_node("sofa")
	sofa.global_position += Vector3(0, 0, -2)
	sofa.durability = 0.3
	main.run.phase = "shop"
	main.next_day()
	_expect(main.run.phase == "day" and sofa.global_position.distance_to(Vector3(0, 0.43, 3)) < 0.05
		and sofa.durability == 1.0, "next day did not reset the sofa")
	_expect(main.ghost.visible, "ghost invisible after reset")
	var g0: Vector3 = main.ghost.global_position
	await _frames(main, 60)
	_expect(main.ghost.global_position.distance_to(g0) < 0.1, "ghost moved during the morning grace period")
	main.run.time_left -= Tuning.data.ghost.start_delay
	var p: CharacterBody3D = Player.new()
	p.name = "1"
	main.players.add_child(p)
	p.global_position = main.ghost.global_position + Vector3(1, -1.2, 0)
	main.run.tapes = 0
	_expect(not main.use_tape(1), "tape used with none left")
	main.run.tapes = 1
	_expect(main.use_tape(1) and not main.ghost.visible and main.run.tapes == 0, "tape near ghost did nothing")
	p.free()


func _check_layouts(main: Node) -> void:
	var counts := []
	for day in [1, 2, 3]:
		main.layout_day(day)
		var live: Array = main.furniture.get_children().filter(func(f: Node) -> bool: return f.in_play)
		counts.append(live.size())
		for i in live.size():
			for j in range(i + 1, live.size()):
				var a: AABB = live[i].world_aabb().grow(-0.01)
				var b: AABB = live[j].world_aabb().grow(-0.01)
				_expect(not a.intersects(b), "day %d: %s spawns inside %s" % [day, live[i].name, live[j].name])
	_expect(counts == [6, 9, 10], "pieces per day %s, expected [6, 9, 10]" % [counts])
	var snap := func() -> Array:
		return main.furniture.get_children().map(func(f: Node) -> Vector3: return f.global_position.snapped(Vector3.ONE * 0.01))
	main.layout_day(2)
	var a1: Array = snap.call()
	main.layout_day(2)
	_expect(snap.call() == a1, "same seed + day gave a different house")
	main.layout_day(3)
	var moved := 0
	var a3: Array = snap.call()
	for i in a1.size():
		if a1[i].y > -10 and a3[i].y > -10 and a1[i] != a3[i]:
			moved += 1
	_expect(moved >= 3, "day 3 barely reshuffled (%d pieces moved)" % moved)


func _check_mimic(main: Node) -> void:
	main.layout_day(3)
	await _frames(main, 30)
	var box: RigidBody3D = main.furniture.get_node("box")
	var bites := [0]
	box.bit.connect(func(_b: RigidBody3D) -> void: bites[0] += 1)
	main.stun_until.erase(300)
	main.run.phase = "day"
	var lone: CharacterBody3D = Player.new()  # the only player → a day-failing stun would end the day
	lone.name = "300"
	lone.set_multiplayer_authority(300)
	main.players.add_child(lone)
	main.grabs.clear()
	for i in 120:  # up to 2 s held (limit 1.5 s); a real client lets go once it's bitten
		if bites[0] > 0:
			break
		main.hold(300, "box", Vector3.ZERO, box.global_position + Vector3.UP * 0.3)
		await physics_frame
	_expect(bites[0] >= 1, "mimic never bit")
	_expect(main.is_stunned(300) and not main.grabs.has(300), "bitten holder not knocked out / still holding")
	_expect(box.bite_show > 0.0, "teeth not shown")
	_expect(main.run.phase == "day", "a mimic bite alone ended the day")
	main.stun_until.erase(300)
	lone.free()


func _check_animation(main: Node) -> void:
	var p: CharacterBody3D = Player.new()
	p.name = "1"
	main.players.add_child(p)
	await physics_frame
	var leg: Node3D = p.body.get_node("LegL")
	var swung := 0.0
	for i in 20:
		p._animate(3.0, 0.05)
		swung = maxf(swung, absf(leg.rotation.x))
	_expect(swung > 0.2, "legs don't swing while walking (max %.2f rad)" % swung)
	p.held = main.furniture.get_node("chair")
	for i in 30:
		p._animate(0.0, 0.05)
	var hand: Node3D = p.body.get_node("ArmR").get_child(1)
	_expect(p.carrying and hand.global_position.z < p.global_position.z - 0.3,
		"carrying: hand should be out in front (-z), hand z %.2f vs body %.2f" % [hand.global_position.z, p.global_position.z])
	p.held = null
	p.free()


## A player who walks a step's length plays a footstep; standing still doesn't.
func _check_footsteps(main: Node) -> void:
	var p: CharacterBody3D = Player.new()
	p.name = "77"
	main.players.add_child(p)
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
	_expect(main._sfx.size() == 4, "sfx players %d" % main._sfx.size())
	var calm := true
	var warped := true
	for i in 50:
		calm = calm and is_equal_approx(Sfx.music_pitch(0.0, i * 0.13), 1.0)
		warped = warped and Sfx.music_pitch(1.0, i * 0.13) < 0.99
	_expect(calm, "music pitch moves with no ghost near")
	_expect(warped, "music pitch not warped with the ghost on top of you")


## Two grab points at the sofa ends follow a carry frame: lift, turn 90°, walk
## along z through the 1.6 m front door to the truck bed, set down, depart.
func _check_carry_to_truck(main: Node) -> void:
	main.run.phase = "shop"
	main.next_day()
	await _frames(main, 20)
	var sofa: RigidBody3D = main.furniture.get_node("sofa")
	var center := sofa.global_position
	var yaw := 0.0
	var bed_mid: Vector3 = main.truck.bed.get_center()
	# (goal center, goal yaw) legs; the carry frame moves at most 1.2 m/s and 1.5 rad/s.
	var legs := [[center + Vector3(0, 0.5, 0), 0.0], [center + Vector3(0, 0.5, 0), PI / 2],
		[Vector3(0, center.y + 0.5, bed_mid.z), PI / 2], [Vector3(0, center.y + 0.1, bed_mid.z), PI / 2]]
	var dt := 1.0 / 60.0
	for leg in legs:
		for i in 900:
			center = center.move_toward(leg[0], 1.2 * dt)
			yaw = move_toward(yaw, leg[1], 1.5 * dt)
			var basis := Basis(Vector3.UP, yaw)
			main.hold(200, "sofa", Vector3(-0.9, 0, 0), center + basis * Vector3(-0.9, 0, 0))
			main.hold(201, "sofa", Vector3(0.9, 0, 0), center + basis * Vector3(0.9, 0, 0))
			await physics_frame
			if center.is_equal_approx(leg[0]) and is_equal_approx(yaw, leg[1]):
				break
	main.grabs.clear()
	await _frames(main, 60)
	_expect(main.truck.bed.has_point(sofa.global_position), "sofa did not reach the truck: %s" % sofa.global_position)
	_expect(sofa.durability > 0.5, "sofa wrecked on the way: %.2f" % sofa.durability)
	var r: Dictionary = main._depart()
	_expect(r.get("loaded", 0) >= 1 and r.total > 0, "depart after carry %s" % r)


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
	_expect(is_equal_approx(main._vhs.get_shader_parameter("pixel_size"), 3.0), "effects 1 not pixelated")
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
	var friend: CharacterBody3D = Player.new()
	friend.name = "55"
	friend.set_multiplayer_authority(55)
	main.players.add_child(friend)
	await physics_frame
	friend.receive_voice(Voice.encode(tone))
	_expect(friend.heard_chunks == 1 and friend.talk_t > 0.0, "remote player did not take a voice chunk")
	friend._rpc_voice(Voice.encode(tone))  # local call: sender 0, not peer 55 → ignored
	_expect(friend.heard_chunks == 1, "voice sent in someone else's name was played")
	friend.free()


func _check_tips() -> void:
	var Tips = load("res://src/tips.gd")
	var s := {captured = false, grabbed_once = false, heavy_alone = false, loaded_any = false}
	var seen := []
	seen.append(Tips.tip_for(1, "day", s))
	s.captured = true
	seen.append(Tips.tip_for(1, "day", s))
	s.grabbed_once = true
	s.heavy_alone = true
	seen.append(Tips.tip_for(1, "day", s))
	s.heavy_alone = false
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


## Each peer in `ids` pulls the body's centre toward 1 m above its start, for `n` frames.
func _hold(main: Node, body: RigidBody3D, ids: Array, n: int) -> void:
	var target := body.global_position + Vector3.UP
	main.grabs.clear()
	for i in n:
		for id in ids:
			main.hold(id, body.name, Vector3.ZERO, target)
		await physics_frame


func _frames(_main: Node, n: int) -> void:
	for i in n:
		await physics_frame


func _expect(ok: bool, msg: String) -> void:
	if not ok:
		_fails += 1
		print("FAIL: ", msg)
