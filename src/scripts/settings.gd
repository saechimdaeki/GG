class_name Settings
## Player options (volume, fullscreen) saved in save_path and applied at startup.

## Overridable so tests do not touch the real file.
static var save_path := "user://settings.cfg"

const DEFAULTS := {"master": 0.8, "music": 0.6, "sfx": 0.8, "fullscreen": false}

static var values: Dictionary = {}


static func load_and_apply() -> void:
	var cfg := ConfigFile.new()
	cfg.load(save_path)  # missing file = defaults
	for k: String in DEFAULTS:
		values[k] = cfg.get_value("options", k, DEFAULTS[k])
	apply()


## Sets one option, applies it and saves.
static func set_value(key: String, value: Variant) -> void:
	if values.is_empty():
		load_and_apply()
	values[key] = value
	apply()
	var cfg := ConfigFile.new()
	for k: String in values:
		cfg.set_value("options", k, values[k])
	cfg.save(save_path)


static func apply() -> void:
	Audio.set_volume("Master", float(values.master))
	Audio.set_volume("Music", float(values.music))
	Audio.set_volume("SFX", float(values.sfx))
	if DisplayServer.get_name() != "headless":
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN if values.fullscreen else DisplayServer.WINDOW_MODE_WINDOWED)
