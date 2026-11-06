## Procedural audio: no asset files. Builds a looping lounge-jazz bed and a few
## effects as AudioStreamWAV at startup.
## ponytail: synthesized placeholders; swap for recorded audio once the feel is set.

const RATE := 22050


## 8-second loop: Cmaj7 → Am7 → Dm7 → G7, soft pad + walking bass + a vibes ding.
static func music() -> AudioStreamWAV:
	var chords := [[48, 60, 64, 67, 71], [45, 57, 60, 64, 67], [50, 62, 65, 69, 72], [43, 59, 62, 65, 67]]
	var bar := 2.0
	var n := int(RATE * bar * chords.size())
	var s := PackedFloat32Array()
	s.resize(n)
	for ci in chords.size():
		var ch: Array = chords[ci]
		var start := int(ci * bar * RATE)
		for i in int(bar * RATE):
			var t := float(i) / RATE
			var env := minf(t / 0.08, 1.0) * minf((bar - t) / 0.08, 1.0)
			var v := 0.0
			for k in range(1, ch.size()):
				v += sin(TAU * _hz(ch[k]) * t) * 0.06
			# walking bass: root, fifth, octave, fifth on each beat
			var beat := int(t / 0.5)
			var bass_note: int = ch[0] + [0, 7, 12, 7][beat]
			var bt := fmod(t, 0.5)
			v += sin(TAU * _hz(bass_note) * bt) * 0.22 * exp(-bt * 5.0)
			# vibes ding on the downbeat, an octave over the top note
			v += sin(TAU * _hz(ch[-1] + 12) * t) * 0.12 * exp(-t * 3.0)
			s[start + i] = v * env
	return _wav(s, true)


## Short low thump (impacts, buck).
static func thud() -> AudioStreamWAV:
	return _make(0.18, func(t: float) -> float:
		return (sin(TAU * 70.0 * t) * 0.8 + (randf() * 2.0 - 1.0) * 0.2) * exp(-t * 22.0))


## Two-tone truck horn (depart).
static func horn() -> AudioStreamWAV:
	return _make(0.6, func(t: float) -> float:
		var f := 330.0 if t < 0.3 else 262.0
		return signf(sin(TAU * f * t)) * 0.25 * minf(t / 0.02, 1.0))


## Rising breathy whoosh (ghost hit).
static func whoosh() -> AudioStreamWAV:
	return _make(0.8, func(t: float) -> float:
		return (randf() * 2.0 - 1.0) * 0.5 * sin(PI * t / 0.8) + sin(TAU * (200.0 + 600.0 * t) * t) * 0.15)


## Coin arpeggio (payout / purchase).
static func coin() -> AudioStreamWAV:
	return _make(0.45, func(t: float) -> float:
		var note: int = [76, 79, 84][mini(int(t / 0.1), 2)]
		return sin(TAU * _hz(note) * t) * 0.35 * exp(-fmod(t, 0.1) * 12.0))


## Music pitch near the ghost: 1.0 when calm; wobbles and sags as `noise` (0..1) rises.
static func music_pitch(noise: float, t: float) -> float:
	return 1.0 - noise * (0.12 + 0.1 * sin(t * 7.0))


static func _hz(midi: int) -> float:
	return 440.0 * pow(2.0, (midi - 69) / 12.0)


static func _make(seconds: float, f: Callable) -> AudioStreamWAV:
	var s := PackedFloat32Array()
	s.resize(int(seconds * RATE))
	for i in s.size():
		s[i] = f.call(float(i) / RATE)
	return _wav(s, false)


static func _wav(samples: PackedFloat32Array, loop: bool) -> AudioStreamWAV:
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in samples.size():
		data.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32767.0))
	var w := AudioStreamWAV.new()
	w.format = AudioStreamWAV.FORMAT_16_BITS
	w.mix_rate = RATE
	w.data = data
	if loop:
		w.loop_mode = AudioStreamWAV.LOOP_FORWARD
		w.loop_end = samples.size()
	return w
