"""Synthesizes the shop's sound effects and the rain ambience into assets/audio/*.wav.

Deterministic (fixed seed), no downloads. Run after editing a recipe:
    python3 tools/gen_audio.py && godot --headless --path . --import
"""
import os
import wave

import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "audio")
rng = np.random.default_rng(13)


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


def bellish(freq, dur, vol=1.0):
    """Inharmonic partials: small brass bell / chime."""
    parts = [(1.0, 1.0), (2.76, 0.5), (5.4, 0.25), (8.9, 0.12)]
    return sum(sine(freq * r, dur, vol * a, curve=2.5 + r * 0.3) for r, a in parts)


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def lowpass(x, k):
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def pad(x, start):
    return np.concatenate([np.zeros(int(SR * start)), x])


def save(name, x, vol=0.8):
    x = x / max(1e-6, np.max(np.abs(x))) * vol
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def main():
    os.makedirs(OUT, exist_ok=True)
    # door bell: two quick strikes of a small shop bell
    save("bell", mix(bellish(1320, 1.2), pad(bellish(1180, 1.0, 0.7), 0.12)), 0.6)
    # coin: bright clinks
    save("coin", mix(bellish(2600, 0.35, 0.8), pad(bellish(3100, 0.3, 0.6), 0.06), pad(bellish(2800, 0.25, 0.4), 0.13)), 0.55)
    # click: soft wooden tick
    save("click", lowpass(noise(0.03), 0.35) * env(int(SR * 0.03), curve=4), 0.5)
    # page: paper rustle for the morning letter / story
    rustle = lowpass(noise(0.35), 0.25) * env(int(SR * 0.35), attack=0.05, curve=1.5)
    save("page", rustle, 0.45)
    # weigh: metal pan settling
    save("weigh", mix(bellish(700, 0.5, 0.6), pad(bellish(760, 0.4, 0.4), 0.08)), 0.5)
    # scratch: touchstone scrape
    s = noise(0.45) * env(int(SR * 0.45), attack=0.02, curve=1.2)
    save("scratch", s - lowpass(s, 0.08), 0.5)
    # candle: whoosh + low eerie tone
    whoosh = lowpass(noise(0.6), 0.06) * env(int(SR * 0.6), attack=0.15, curve=1.5)
    save("candle", mix(whoosh * 2, sine(220, 0.9, 0.3, curve=1.5, attack=0.2)), 0.6)
    # curse: detuned low drone swell for the violet flame and night curses
    drone = mix(*[sine(f, 1.6, 0.4, curve=1.2, attack=0.4) for f in (55, 58.3, 82.4, 110 * 1.06)])
    save("curse", drone, 0.7)
    # angry: low growl
    g = np.sign(np.sin(2 * np.pi * 70 * t(0.35))) * env(int(SR * 0.35), curve=2)
    save("angry", lowpass(g + noise(0.35) * 0.3, 0.2), 0.55)
    # deal: stamp thump + coin
    thump = lowpass(noise(0.12), 0.05) * env(int(SR * 0.12), curve=3) * 3
    save("deal", mix(thump, pad(bellish(2600, 0.3, 0.5), 0.05)), 0.6)
    # baal: ominous minor chord with a slow attack
    save("baal", mix(*[sine(f, 2.2, 0.5, curve=1.0, attack=0.3) for f in (73.4, 87.3, 110, 146.8)]), 0.7)
    # ending: slow falling chime
    save("ending", mix(bellish(880, 2.0, 0.6), pad(bellish(660, 2.0, 0.6), 0.5), pad(bellish(587, 2.4, 0.7), 1.0)), 0.6)

    # ambience loop: rain hiss + distant drips + a very low drone, 16 s, seamless
    dur = 16.0
    n = int(SR * dur)
    rain = lowpass(noise(dur), 0.18) - lowpass(noise(dur), 0.02) * 0.6
    drips = np.zeros(n)
    for _ in range(40):
        at = int(rng.uniform(0, dur - 0.3) * SR)
        d = sine(rng.uniform(900, 1600), 0.12, rng.uniform(0.05, 0.15), curve=4)
        drips[at:at + len(d)] += d
    low = sine(49, dur, 0.12, curve=0.0) + sine(73.4, dur, 0.06, curve=0.0)
    amb = rain * 0.35 + drips + low
    fade = int(SR * 0.5)  # crossfade the ends so the loop has no click
    amb[:fade] = amb[:fade] * np.linspace(0, 1, fade) + amb[-fade:] * np.linspace(1, 0, fade)
    save("ambience", amb[: n - fade], 0.5)
    print("audio ->", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
