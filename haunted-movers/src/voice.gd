extends Node
## Proximity voice. One instance lives under main (the mic side); each Player owns a 3D
## speaker that plays what its peer says, so voices come from where people stand and fade
## with distance.
##
## Mic → muted "Mic" bus with an AudioEffectCapture → downmix to mono, resample to RATE,
## 16-bit PCM → Player._rpc_voice every CHUNK_S. Quiet chunks (below `gate`) aren't sent.
## ponytail: raw 16-bit PCM is ~32 KB/s per talker; add a codec if 4 talkers on bad links stutter.

const RATE := 16000
const CHUNK_S := 0.04
const GATE := 0.012  # RMS below this = silence, not sent

## Local player node to send through (set by main once we've spawned).
var me: Node
var muted := false
## --voice-test: send a 440 Hz tone instead of the mic (proves the network path without a mic).
var test_tone := "--voice-test" in OS.get_cmdline_user_args()

var _capture: AudioEffectCapture
var _mic := AudioStreamPlayer.new()
var _pending := PackedFloat32Array()
var _phase := 0.0
var _src_rate := 44100.0


func _ready() -> void:
	_src_rate = AudioServer.get_mix_rate()
	if test_tone or DisplayServer.get_name() == "headless":
		return
	var bus := AudioServer.bus_count
	AudioServer.add_bus(bus)
	AudioServer.set_bus_name(bus, "Mic")
	AudioServer.set_bus_mute(bus, true)  # never hear yourself
	_capture = AudioEffectCapture.new()
	AudioServer.add_bus_effect(bus, _capture)
	_mic.stream = AudioStreamMicrophone.new()
	_mic.bus = "Mic"
	add_child(_mic)
	_mic.play()


func _process(delta: float) -> void:
	if me == null or not is_instance_valid(me) or muted:
		_pending.clear()
		return
	if test_tone:
		for i in int(RATE * delta):
			_phase += TAU * 440.0 / RATE
			_pending.append(sin(_phase) * 0.3)
	elif _capture:
		var frames := _capture.get_buffer(_capture.get_frames_available())
		_pending.append_array(downmix(frames, _src_rate, RATE))
	var chunk := int(RATE * CHUNK_S)
	while _pending.size() >= chunk:
		var part := _pending.slice(0, chunk)
		_pending = _pending.slice(chunk)
		if rms(part) >= GATE:
			me.send_voice(encode(part))


## Stereo frames at `from_rate` → mono samples at `to_rate` (nearest-sample resampling).
static func downmix(frames: PackedVector2Array, from_rate: float, to_rate: float) -> PackedFloat32Array:
	var out := PackedFloat32Array()
	var step := from_rate / to_rate
	var t := 0.0
	while int(t) < frames.size():
		var f := frames[int(t)]
		out.append((f.x + f.y) * 0.5)
		t += step
	return out


static func rms(s: PackedFloat32Array) -> float:
	var sum := 0.0
	for v in s:
		sum += v * v
	return sqrt(sum / maxi(s.size(), 1))


static func encode(s: PackedFloat32Array) -> PackedByteArray:
	var b := PackedByteArray()
	b.resize(s.size() * 2)
	for i in s.size():
		b.encode_s16(i * 2, int(clampf(s[i], -1.0, 1.0) * 32767.0))
	return b


static func decode(b: PackedByteArray) -> PackedFloat32Array:
	var s := PackedFloat32Array()
	s.resize(b.size() / 2)
	for i in s.size():
		s[i] = b.decode_s16(i * 2) / 32767.0
	return s
