## Player settings, saved to user://settings.cfg between runs.
## Values are plain floats so the UI can bind sliders straight to them.

const PATH := "user://settings.cfg"
## name -> [default, min, max, step, label]
const SPEC := {
	"sensitivity": [1.0, 0.25, 3.0, 0.05, "마우스 감도"],
	"volume": [0.8, 0.0, 1.0, 0.05, "음량"],
	"effects": [1.0, 0.0, 1.0, 0.05, "화면 효과 (픽셀·노이즈)"],
}

static var values := {}


static func load_or_default() -> void:
	var cfg := ConfigFile.new()
	var ok := cfg.load(PATH) == OK
	for k in SPEC:
		var s: Array = SPEC[k]
		values[k] = clampf(float(cfg.get_value("settings", k, s[0])) if ok else s[0], s[1], s[2])


static func save() -> void:
	var cfg := ConfigFile.new()
	for k in values:
		cfg.set_value("settings", k, values[k])
	cfg.save(PATH)


static func get_value(k: String) -> float:
	if values.is_empty():
		load_or_default()
	return values[k]
