"""Synthesises the intro sound track. Every footstep and impact uses the times written by animate_thief.py.

Run:  blender --background --factory-startup --python Blender/Intro/build_soundtrack.py
(Blender is used only because its Python includes numpy.)
"""
import json
import os
import sys
import wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
from intro_layout import LENGTH  # noqa: E402

RATE = 44100
random = np.random.default_rng(1907)
mix = np.zeros(int((LENGTH + 0.2) * RATE))


def clock(seconds): return np.arange(int(seconds * RATE)) / RATE


def band(signal, low, high):
    """Soft band-pass in the frequency domain."""
    spectrum = np.fft.rfft(signal)
    f = np.fft.rfftfreq(len(signal), 1 / RATE)
    spectrum *= 1 / (1 + (low / np.maximum(f, 1)) ** 4) * 1 / (1 + (f / high) ** 4)
    return np.fft.irfft(spectrum, len(signal))


def noise(seconds, low, high):
    raw = band(random.standard_normal(int(seconds * RATE)), low, high)
    return raw / (np.abs(raw).max() + 1e-9)


def add(time, sound, gain):
    start = int(time * RATE)
    end = min(len(mix), start + len(sound))
    if end > start: mix[start:end] += gain * sound[:end - start]


def hit(seconds, decay, low, high):
    t = clock(seconds)
    return noise(seconds, low, high) * np.exp(-t * decay) * np.minimum(1, t / 0.002)


def ping(seconds, decay, partials):
    t = clock(seconds)
    return sum(a * np.sin(2 * np.pi * f * t + i) * np.exp(-t * decay * (1 + 0.5 * i)) for i, (f, a) in enumerate(partials)) * np.minimum(1, t / 0.001)


def thump(seconds, decay, start, end):
    t = clock(seconds)
    return np.sin(2 * np.pi * np.cumsum(end + (start - end) * np.exp(-t * 18)) / RATE) * np.exp(-t * decay)


def van():
    t = clock(4.6)
    speed = np.clip((3.7 - t) / 1.9, 0, 1)                      # 1 while cruising, 0 when stopped
    rpm = 27 + 26 * speed
    phase = 2 * np.pi * np.cumsum(rpm) / RATE
    engine = sum(np.sin(phase * k + 0.7 * k) * a for k, a in ((1, 0.5), (2, 1.0), (3, 0.5), (4, 0.6), (6, 0.35), (8, 0.22), (11, 0.1)))
    engine *= 1 + 0.18 * noise(4.6, 8, 40)
    near = 1 / (1 + (np.clip(3.2 - t, 0, 4) / 0.8) ** 2)          # it grows louder as it arrives
    engine *= (0.25 + 0.75 * near) * np.where(t < 4.15, 1, np.exp(-(t - 4.15) * 16)) * np.minimum(1, t / 0.5)
    add(0, band(engine, 30, 900), 0.085)
    add(0, noise(4.6, 150, 1400) * speed * (0.2 + 0.8 * near) * np.minimum(1, t / 0.5), 0.10)
    squeal = np.sin(2 * np.pi * 1850 * t + 2 * np.sin(2 * np.pi * 37 * t)) * np.exp(-((t - 3.45) / 0.16) ** 2)
    add(0, squeal, 0.012)
    add(4.12, hit(0.12, 40, 900, 5000), 0.10)                     # handbrake
    t = clock(0.85)
    slide = noise(0.85, 250, 2600) * np.sin(np.pi * np.clip(t / 0.8, 0, 1)) * (0.8 + 0.2 * np.sin(2 * np.pi * 34 * t))
    add(4.68, hit(0.15, 30, 600, 4200), 0.22)
    add(4.74, slide, 0.16)
    add(5.50, thump(0.35, 16, 150, 70) + 0.7 * hit(0.35, 22, 300, 2500), 0.28)
    add(6.25, noise(0.4, 500, 1500) * np.sin(np.pi * np.clip(clock(0.4) / 0.4, 0, 1)) ** 2, 0.035)


def score():
    t = clock(LENGTH)
    fade = np.minimum(1, t / 2.5) * np.clip((LENGTH - t) / 1.2, 0, 1)
    drone = sum(a * np.sin(2 * np.pi * f * t + 0.6 * np.sin(2 * np.pi * 0.11 * t * (i + 1))) for i, (f, a) in
                enumerate(((55.0, 1.0), (82.4, 0.55), (110.0, 0.35), (130.8, 0.22))))
    rise = np.interp(t, (0, 9, 18, 23.7, 24.6, 25.2, LENGTH), (0.5, 0.6, 0.85, 1.25, 0.2, 0.45, 0.5))
    add(0, drone * fade * rise, 0.050)
    add(0, noise(LENGTH, 120, 700) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.17 * t)) * fade, 0.05)    # wind
    # A muted pulse that quickens towards the jump.
    beat, gap = 4.6, 1.0
    while beat < 23.4:
        strength = np.interp(beat, (4.6, 12, 23), (0.07, 0.12, 0.20))
        add(beat, thump(0.5, 11, 95, 48), strength)
        add(beat + gap * 0.28, thump(0.4, 14, 85, 46), strength * 0.55)
        gap = np.interp(beat, (4.6, 12, 18, 23), (1.0, 0.8, 0.68, 0.55))
        beat += gap
    # A plucked minor figure over the climb and the sneak.
    notes = (220.0, 261.6, 329.6, 311.1, 220.0, 261.6, 349.2, 329.6)
    for i in range(34):
        when = 9.6 + i * 0.4
        if when > 23.0: break
        add(when, ping(0.7, 7, ((notes[i % 8], 1), (notes[i % 8] * 2, 0.35), (notes[i % 8] * 3, 0.12))), 0.034)


def events():
    with open(os.path.join(HERE, "intro_events.json")) as file: timeline = json.load(file)
    for time, kind in timeline:
        vary = random.uniform(0.8, 1.15)
        if kind == "step": add(time - 0.02, hit(0.16, 34, 180, 1900) + 0.5 * thump(0.16, 30, 120, 70), 0.13 * vary)
        elif kind == "roof": add(time - 0.02, hit(0.2, 30, 400, 3200) + 0.8 * thump(0.2, 22, 110, 60), 0.10 * vary)
        elif kind == "grass": add(time - 0.03, hit(0.2, 22, 900, 5200), 0.075 * vary)
        elif kind == "rung":
            add(time - 0.02, ping(0.35, 15, ((612 * vary, 1), (1480 * vary, 0.6), (2730, 0.35))), 0.055)
            add(time - 0.02, hit(0.12, 40, 250, 1800), 0.10 * vary)
        elif kind == "ladder":
            for k, (late, loud) in enumerate(((0, 1), (0.14, 0.4), (0.26, 0.18))):
                add(time + late, ping(0.9, 6 + 3 * k, ((371, 1), (893, 0.8), (1640, 0.6), (2510, 0.4), (3870, 0.25))), 0.10 * loud)
                add(time + late, hit(0.2, 24, 200, 2400), 0.22 * loud)
        elif kind == "land":
            t = clock(0.88)
            add(time - 0.88, noise(0.88, 300, 2200) * (t / 0.88) ** 2, 0.12)
            add(time, thump(0.6, 9, 110, 42) + 0.6 * hit(0.6, 16, 200, 2400), 0.50)


van()
score()
events()
mix = np.tanh(mix * 1.5) / 1.1
mix *= 0.8 / np.abs(mix).max()
target = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Audio", "Intro", "IntroSoundtrack.wav"))
os.makedirs(os.path.dirname(target), exist_ok=True)
with wave.open(target, "wb") as out:
    out.setnchannels(1); out.setsampwidth(2); out.setframerate(RATE)
    out.writeframes((mix * 32767).astype("<i2").tobytes())
levels = [float(np.sqrt(np.mean(mix[int(s * RATE):int((s + 1) * RATE)] ** 2))) for s in range(int(LENGTH))]
print("SOUND", round(len(mix) / RATE, 2), "s rms per second", [round(v, 3) for v in levels])
