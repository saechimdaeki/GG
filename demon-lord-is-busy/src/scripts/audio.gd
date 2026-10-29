class_name Audio
## Sound effects and music from res://assets/audio (made by tools/gen_audio.py).
## Static helper: lazily adds its players under the tree root, so any script can call
## Audio.play("hit") without wiring. Buses "Music" and "SFX" carry the volume settings.

const POOL := 12

static var _pool: Array[AudioStreamPlayer] = []
static var _next: int = 0
static var _music: AudioStreamPlayer
static var _music_name: String = ""
static var _cache: Dictionary = {}


static func _stream(name: String) -> AudioStream:
	if not _cache.has(name):
		var path := "res://assets/audio/%s.wav" % name
		_cache[name] = load(path) if ResourceLoader.exists(path) else null
	return _cache[name]


static func _ready_players() -> bool:
	if not _pool.is_empty() and is_instance_valid(_pool[0]):
		return true
	var tree := Engine.get_main_loop() as SceneTree
	# ponytail: headless runs (tests) are silent anyway; skipping avoids exit-time leak noise
	if tree == null or tree.root == null or DisplayServer.get_name() == "headless":
		return false
	for bus: String in ["Music", "SFX"]:
		if AudioServer.get_bus_index(bus) == -1:
			AudioServer.add_bus()
			AudioServer.set_bus_name(AudioServer.bus_count - 1, bus)
	_pool.clear()
	# One holder node: when the tree shuts down it drops the cached streams too (no leaks).
	var holder := Node.new()
	holder.name = "Audio"
	holder.process_mode = Node.PROCESS_MODE_ALWAYS
	holder.tree_exiting.connect(func() -> void:
		_cache.clear()
		_pool.clear()
		_music = null
		_music_name = "")
	for i in POOL:
		var p := AudioStreamPlayer.new()
		p.bus = "SFX"
		holder.add_child(p)
		_pool.append(p)
	_music = AudioStreamPlayer.new()
	_music.bus = "Music"
	holder.add_child(_music)
	tree.root.add_child.call_deferred(holder)
	return true


## Plays a one-shot effect. pitch_jitter randomizes pitch a little so repeats don't drone.
static func play(name: String, volume_db: float = 0.0, pitch_jitter: float = 0.08) -> void:
	var s := _stream(name)
	if s == null or not _ready_players():
		return
	var p := _pool[_next]
	_next = (_next + 1) % POOL
	if not p.is_inside_tree():
		return
	p.stream = s
	p.volume_db = volume_db
	p.pitch_scale = 1.0 + randf_range(-pitch_jitter, pitch_jitter)
	p.play()


## Switches the looping music track ("" stops it). Same track = no restart.
static func music(name: String) -> void:
	if not _ready_players() or name == _music_name:
		return
	_music_name = name
	var s := _stream(name) as AudioStreamWAV
	if s == null:
		_music.stop.call_deferred()
		return
	s.loop_mode = AudioStreamWAV.LOOP_FORWARD
	s.loop_end = s.data.size() / 2  # 16-bit mono frames
	_music.stream = s
	_music.play.call_deferred()


## Sets a bus volume from a 0..1 slider value ("Master", "Music" or "SFX").
static func set_volume(bus: String, value: float) -> void:
	_ready_players()
	var i := AudioServer.get_bus_index(bus)
	if i >= 0:
		AudioServer.set_bus_volume_db(i, linear_to_db(maxf(value, 0.0001)))
