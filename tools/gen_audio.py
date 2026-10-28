"""Synthesizes every sound effect and the two music loops into assets/audio/*.wav.

Chiptune-style, no downloads, deterministic (fixed seed). Run after editing a recipe:
    python3 tools/gen_audio.py && godot --headless --path . --import
"""
import os
import wave

import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "audio")
rng = np.random.default_rng(7)


def t(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.005, release=None, curve=3.0):
    """Fast attack, exponential-ish decay over the whole sound."""
    x = np.linspace(0, 1, n)
    e = (1 - x) ** curve
    a = max(1, int(SR * attack))
    e[:a] *= np.linspace(0, 1, a)
    return e


def sweep(f0, f1, dur, shape="square"):
    """Oscillator whose pitch glides from f0 to f1 (Hz)."""
    freq = np.geomspace(max(f0, 1), max(f1, 1), int(SR * dur))
    phase = np.cumsum(freq) / SR
    if shape == "square":
        return np.sign(np.sin(2 * np.pi * phase))
    if shape == "tri":
        return 2 * np.abs(2 * (phase % 1) - 1) - 1
    if shape == "saw":
        return 2 * (phase % 1) - 1
    return np.sin(2 * np.pi * phase)


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def lowpass(x, k):
    """Cheap one-pole low-pass; k in (0,1], smaller = darker."""
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def tone(freq, dur, shape="square", vol=1.0, curve=2.0):
    s = sweep(freq, freq, dur, shape)
    return s * env(len(s), curve=curve) * vol


def seq(*parts):
    return np.concatenate(parts)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def save(name, x, vol=0.8):
    x = x / max(1e-6, np.max(np.abs(x))) * vol
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def note(n):
    """MIDI note number -> Hz."""
    return 440.0 * 2 ** ((n - 69) / 12)


SFX = {
    "swing": lambda: lowpass(noise(0.12), 0.35) * env(int(SR * 0.12), curve=1.5),
    "hit": lambda: mix(lowpass(noise(0.1), 0.6) * env(int(SR * 0.1), curve=4),
                       sweep(160, 50, 0.12, "sine") * env(int(SR * 0.12), curve=2)),
    "hurt": lambda: sweep(420, 120, 0.2, "square") * env(int(SR * 0.2), curve=1.5) * 0.6,
    "dash": lambda: lowpass(noise(0.16), 0.2) * np.sin(np.linspace(0, np.pi, int(SR * 0.16))),
    "bolt": lambda: sweep(1400, 300, 0.14, "square") * env(int(SR * 0.14), curve=2) * 0.5,
    "skill": lambda: mix(lowpass(noise(0.6), 0.15) * env(int(SR * 0.6), curve=2),
                         sweep(120, 35, 0.6, "sine") * env(int(SR * 0.6), curve=1.5)),
    "hero_die": lambda: mix(lowpass(noise(0.25), 0.4) * env(int(SR * 0.25), curve=3),
                            sweep(300, 60, 0.25, "square") * env(int(SR * 0.25), curve=2) * 0.4),
    "monster_die": lambda: sweep(500, 80, 0.3, "tri") * env(int(SR * 0.3), curve=1.2),
    "trap": lambda: mix(sweep(2200, 1800, 0.06, "square") * env(int(SR * 0.06)) * 0.4,
                        lowpass(noise(0.05), 0.8) * env(int(SR * 0.05))),
    "build": lambda: mix(sweep(140, 70, 0.12, "sine") * env(int(SR * 0.12), curve=2),
                         lowpass(noise(0.06), 0.5) * env(int(SR * 0.06)) * 0.5),
    "error": lambda: sweep(110, 100, 0.18, "square") * env(int(SR * 0.18), curve=0.5) * 0.4,
    "coin": lambda: seq(tone(note(83), 0.05, vol=0.5), tone(note(88), 0.14, vol=0.5)),
    "wave_start": lambda: mix(sweep(note(45), note(50), 0.7, "saw") * env(int(SR * 0.7), curve=0.8) * 0.6,
                              sweep(note(57), note(62), 0.7, "square") * env(int(SR * 0.7), curve=0.8) * 0.25),
    "wave_clear": lambda: seq(*[tone(note(n), 0.09, vol=0.5) for n in (72, 76, 79)], tone(note(84), 0.35, vol=0.5)),
    "level_up": lambda: seq(*[tone(note(n), 0.06, "tri") for n in (76, 79, 83, 88)], tone(note(91), 0.3, "tri")),
    "core_hit": lambda: seq(tone(note(52), 0.12, vol=0.6), tone(note(47), 0.25, vol=0.6)),
    "click": lambda: tone(note(84), 0.03, vol=0.4, curve=4),
    "game_over": lambda: seq(*[tone(note(n), 0.22, "tri") for n in (64, 60, 57)], tone(note(52), 0.7, "tri")),
    "victory": lambda: seq(*[tone(note(n), 0.12) for n in (72, 72, 72)], tone(note(76), 0.2), tone(note(79), 0.5)),
    "heal": lambda: mix(tone(note(88), 0.25, "sine", 0.5), tone(note(95), 0.25, "sine", 0.3)),
    "gate": lambda: mix(sweep(300, 260, 0.25, "square") * env(int(SR * 0.25), curve=3) * 0.3,
                        lowpass(noise(0.2), 0.3) * env(int(SR * 0.2), curve=2)),
}


def music(bpm, bars, bass, arp, drums, arp_vol=0.18):
    """Loop of `bars` 4/4 bars: bass notes per beat, 8th-note arpeggio, kick/hat pattern."""
    beat = 60.0 / bpm
    n = int(SR * beat * 4 * bars)
    out = np.zeros(n)
    step = beat / 2
    for i in range(bars * 8):
        start = int(i * step * SR)
        b = bass[(i // 2) % len(bass)]
        if i % 2 == 0 and b:
            s = tone(note(b), beat * 0.9, "tri", 0.45, curve=1.2)
            out[start:start + len(s)] += s[: n - start]
        a = arp[i % len(arp)]
        if a:
            s = tone(note(a), step * 0.9, "square", arp_vol, curve=2.5)
            out[start:start + len(s)] += s[: n - start]
        d = drums[i % len(drums)]
        if d == "k":
            s = sweep(110, 40, 0.12, "sine") * env(int(SR * 0.12), curve=2) * 0.7
            out[start:start + len(s)] += s[: n - start]
        elif d == "h":
            s = lowpass(noise(0.04), 0.9) * env(int(SR * 0.04), curve=4) * 0.15
            out[start:start + len(s)] += s[: n - start]
        elif d == "s":
            s = lowpass(noise(0.12), 0.6) * env(int(SR * 0.12), curve=3) * 0.35
            out[start:start + len(s)] += s[: n - start]
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, make in SFX.items():
        save(name, make())
    # Build phase: slow, dark A-minor arpeggio, no drums.
    save("music_build", music(84, 8,
                              bass=[45, 45, 41, 41, 43, 43, 40, 40] * 4,
                              arp=[69, 72, 76, 72, 69, 72, 76, 79, 65, 69, 72, 69, 67, 71, 74, 71],
                              drums=[""], arp_vol=0.12), vol=0.55)
    # Raid: faster, driving bass and drums.
    save("music_raid", music(140, 8,
                             bass=[45, 45, 45, 48, 41, 41, 43, 44] * 4,
                             arp=[81, 0, 76, 0, 79, 0, 76, 74, 77, 0, 72, 0, 76, 0, 71, 0],
                             drums=["k", "h", "s", "h", "k", "k", "s", "h"]), vol=0.6)
    print("wrote", len(SFX) + 2, "files to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
