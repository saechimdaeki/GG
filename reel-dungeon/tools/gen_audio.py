"""Synthesizes Reel Dungeon's sound effects and two music loops into Assets/Resources/Audio/*.wav.

Deterministic (fixed seed), no downloads. Run after editing a recipe:
    python3 tools/gen_audio.py
Helpers follow cursed-pawnshop/tools/gen_audio.py (copied, not imported: each game stays standalone).
"""
import os
import wave

import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio")
rng = np.random.default_rng(7)


def t(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.005, curve=3.0):
    x = np.linspace(0, 1, n)
    e = (1 - x) ** curve
    a = max(1, int(SR * attack))
    e[:a] *= np.linspace(0, 1, a)
    return e


def sine(freq, dur, vol=1.0, curve=3.0, attack=0.005):
    x = np.sin(2 * np.pi * freq * t(dur))
    return x * env(len(x), attack, curve) * vol


def square(freq, dur, vol=1.0, curve=3.0, attack=0.003):
    x = np.sign(np.sin(2 * np.pi * freq * t(dur)))
    return x * env(len(x), attack, curve) * vol


def sweep(f0, f1, dur, vol=1.0, curve=2.0, wave_fn=np.sin):
    f = np.linspace(f0, f1, int(SR * dur))
    x = wave_fn(2 * np.pi * np.cumsum(f) / SR)
    return x * env(len(x), 0.003, curve) * vol


def bellish(freq, dur, vol=1.0):
    parts = [(1.0, 1.0), (2.76, 0.5), (5.4, 0.25), (8.9, 0.12)]
    return sum(sine(freq * r, dur, vol * a, curve=2.5 + r * 0.3) for r, a in parts)


def noise(dur, vol=1.0, curve=3.0):
    x = rng.uniform(-1, 1, int(SR * dur))
    return x * env(len(x), 0.002, curve) * vol


def lowpass(x, k):
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def highpass(x, k):
    return x - lowpass(x, k)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def pad(x, start):
    return np.concatenate([np.zeros(int(SR * start)), x])


def save(name, x, peak=0.8):
    x = x / max(1e-9, np.max(np.abs(x))) * peak
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def note(n):
    """Semitones from A4."""
    return 440.0 * 2 ** (n / 12)


# ---------- effects ----------

save("reel_tick", mix(square(1800, 0.025, 0.4, curve=6), noise(0.02, 0.3, 6)), 0.35)
save("reel_stop", mix(sine(140, 0.12, 1.0, curve=4), lowpass(noise(0.08, 0.8, 5), 0.2), square(900, 0.03, 0.3, 6)), 0.6)
save("ui_click", mix(sine(1200, 0.04, 0.6, 6), sine(1800, 0.03, 0.3, 6)), 0.4)
save("map_step", mix(lowpass(noise(0.12, 1, 4), 0.08), pad(lowpass(noise(0.1, 0.8, 4), 0.08), 0.16)), 0.5)
save("slash", mix(highpass(noise(0.18, 1, 2.5), 0.3), sweep(2400, 600, 0.15, 0.4)), 0.7)
save("hit", mix(sine(90, 0.18, 1.0, 4), lowpass(noise(0.1, 1, 4), 0.3), square(220, 0.05, 0.3, 6)), 0.8)
save("heavy", mix(sweep(160, 40, 0.4, 1.0, 2), lowpass(noise(0.3, 1, 2.5), 0.12), sine(60, 0.4, 0.8, 2)), 0.9)
save("arrow", mix(highpass(noise(0.16, 0.7, 1.5), 0.5) * np.linspace(0.2, 1, int(SR * 0.16)), pad(square(1400, 0.03, 0.4, 6), 0.15)), 0.5)
save("magic", mix(sweep(300, 1400, 0.3, 0.6, 1.5), sweep(450, 2100, 0.3, 0.3, 1.5), pad(bellish(1320, 0.4, 0.4), 0.2)), 0.6)
save("ice", mix(*[pad(bellish(note(n), 0.6, 0.5), i * 0.05) for i, n in enumerate([15, 19, 22, 27])]), 0.6)
save("beam", mix(sweep(200, 900, 0.5, 0.5, 1.2), *[sine(note(n), 0.7, 0.3, 1.5) for n in [12, 16, 19, 24]]), 0.6)
save("block", mix(bellish(520, 0.35, 0.8), noise(0.04, 0.6, 6)), 0.6)
save("hurt", mix(sweep(300, 90, 0.25, 0.8, 2, wave_fn=lambda p: np.sign(np.sin(p))), lowpass(noise(0.15, 0.8, 3), 0.25)), 0.7)
save("coin", mix(bellish(note(19), 0.25, 0.7), pad(bellish(note(24), 0.35, 0.7), 0.07)), 0.5)
save("skill", mix(*[pad(square(note(n), 0.12, 0.35, 3), i * 0.06) for i, n in enumerate([3, 7, 10, 15, 19])], sweep(200, 1200, 0.4, 0.3)), 0.6)
save("combo", mix(*[pad(bellish(note(n), 0.35, 0.6), i * 0.07) for i, n in enumerate([12, 16, 19, 24, 28])]), 0.6)
save("kill", mix(sweep(600, 80, 0.5, 0.7, 2, wave_fn=lambda p: np.sign(np.sin(p))), lowpass(noise(0.45, 1, 2), 0.2)), 0.7)
save("win", mix(*[pad(square(note(n), 0.25, 0.4, 2), i * 0.13) for i, n in enumerate([3, 7, 10, 15, 22, 27])]), 0.6)
save("lose", mix(*[pad(square(note(n), 0.4, 0.4, 2), i * 0.2) for i, n in enumerate([3, 2, 0, -2, -5])]), 0.6)


# ---------- music: short chiptune loops ----------

def loop(bpm, bass, arp, lead, bars=8):
    beat = 60 / bpm
    total = int(SR * beat * 4 * bars)
    out = np.zeros(total)

    def put(x, at):
        i = int(SR * at)
        end = min(total, i + len(x))
        out[i:end] += x[: end - i]

    for b in range(bars):
        root = bass[b % len(bass)]
        for k in range(8):  # eighth-note bass
            put(square(note(root - 24) if k % 2 == 0 else note(root - 12), beat / 2, 0.22, 4), (b * 4 + k / 2) * beat)
        for k in range(16):  # sixteenth-note arpeggio
            put(square(note(root + arp[k % len(arp)]), beat / 4, 0.08, 5), (b * 4 + k / 4) * beat)
        for k, n in enumerate(lead[b % len(lead)]):
            if n is not None:
                put(sine(note(n), beat * 0.9, 0.18, 1.5, 0.02), (b * 4 + k) * beat)
        put(lowpass(noise(0.05, 0.25, 6), 0.4), (b * 4 + 1) * beat)  # soft snare
        put(lowpass(noise(0.05, 0.25, 6), 0.4), (b * 4 + 3) * beat)
    return out


A_MINOR = [0, 0, -4, -2, 0, 0, 3, -2]
save("bgm_dungeon", loop(108, A_MINOR, [0, 3, 7, 12, 7, 3], [[12, None, 15, 14], [12, None, 10, None], [8, 7, 8, 10], [7, None, None, None]]), 0.5)
save("bgm_boss", loop(150, [0, 1, 0, -2], [0, 3, 6, 12, 6, 3], [[12, 13, 12, 10], [9, None, 6, None], [12, 15, 13, 12], [10, 9, 6, None]]), 0.55)
print("wrote", sorted(os.listdir(OUT)))
