extends SceneTree
## Run two copies with -- --server / --client to check a real ENet handshake.
## Without a role, checks timeout, cancel, stale timeout, and hosting after failure.

var _fails: int = 0

func _initialize() -> void:
	_run()

func _run() -> void:
	var main: Node = load("res://src/main.tscn").instantiate()
	root.add_child(main)
	await process_frame
	main.port = 17837
	var args: PackedStringArray = OS.get_cmdline_user_args()
	if "--ui-connect" in args or "--ui-failure" in args:
		main.connect_timeout_seconds = 0.15 if "--ui-failure" in args else 10.0
		main.join("127.0.0.1:17838")
		await create_timer(15.0).timeout
	elif "--server" in args:
		main.host(false)
		for frame in 300:
			if main.players.get_child_count() == 2:
				break
			await create_timer(0.02).timeout
		_expect(main.players.get_child_count() == 2, "host did not see both players")
		await create_timer(0.75).timeout
		main._end_day(250)
		await create_timer(0.2).timeout
		main.next_day()
		await create_timer(2.5).timeout
		for id in main.multiplayer.get_peers():
			main.kill_player(id)  # the ghost got the client: it should spectate us
		_expect(main.run.phase == "day", "one death out of two ended the day")
		await create_timer(2.5).timeout
	elif "--client" in args:
		main.join("127.0.0.1:17837")
		for frame in 150:
			if main.players.get_child_count() == 2:
				break
			await create_timer(0.02).timeout
		var local_player: Node = main.players.get_node_or_null(str(main.multiplayer.get_unique_id()))
		_expect(local_player != null, "local player not spawned")
		if local_player != null:
			local_player.stun(20.0)
		await create_timer(2.0).timeout
		_expect(main.run.day == 2 and main.run.phase == "day" and main.run.haul == 250,
			"next day did not replicate to the client")
		if local_player != null:
			_expect(not local_player.stunned, "next day did not recover the remote owner")
		var active_count: int = 0
		for piece in main.furniture.get_children():
			if piece.in_play:
				active_count += 1
		_expect(active_count == load("res://src/tuning.gd").data.days[1].size(), "day-2 furniture visibility did not replicate")
		_expect(not main._connecting, "client handshake still pending")
		_expect(main.multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED,
			"client did not connect")
		_expect(main.players.get_child_count() == 2, "client did not receive both players")
		_expect(main.furniture.get_node("chair").freeze, "client simulates host furniture")
		if local_player != null:
			for frame in 200:
				if local_player.dead:
					break
				await create_timer(0.02).timeout
			await process_frame
			var host_player: Node = main.players.get_node_or_null("1")
			_expect(local_player.dead and local_player.stunned, "client was not killed")
			_expect(host_player != null and host_player.cam.current and not local_player.cam.current,
				"dead client is not spectating the host")
	else:
		main.connect_timeout_seconds = 0.15
		main.join("127.0.0.1:17838")
		_expect(main._connecting, "join not pending")
		await create_timer(0.25).timeout
		_expect(not main._connecting and main._hint.text.begins_with("연결 실패"), "timeout did not report failure")
		_expect(main.multiplayer.multiplayer_peer is OfflineMultiplayerPeer, "failed peer was retained")
		_expect(not main.furniture.get_node("chair").freeze, "failed join left host physics frozen")
		main.join("127.0.0.1:17838")
		main._cancel_connection("cancelled")
		main.connect_timeout_seconds = 1.0
		main.join("127.0.0.1:17838")
		await create_timer(0.25).timeout
		_expect(main._connecting, "old timeout cancelled a newer attempt")
		main._cancel_connection("cancelled")
		main.host(false)
		_expect(main.players.get_child_count() == 1 and main.is_host_active(), "cannot host after cancelling a join")
	print("PASS connection" if _fails == 0 else "FAIL connection %d" % _fails)
	main.multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	quit(0 if _fails == 0 else 1)

func _expect(ok: bool, message: String) -> void:
	if not ok:
		_fails += 1
		print("FAIL: ", message)
