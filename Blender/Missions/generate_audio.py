"""Synthesise the mission sound effects with the Python standard library.

Run from the repository root: python3 Blender/Missions/generate_audio.py
Writes 16-bit mono WAV files to Assets/_Project/Audio/Missions/. A fixed seed keeps
the output reproducible; no recorded or downloaded audio is used.
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/Audio/Missions"
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(1983)

def normalise(samples, peak=0.85):
    top = max(1e-9, max(abs(v) for v in samples))
    return [v * peak / top for v in samples]

def fade(samples, seconds=0.01):
    n = min(len(samples) // 2, int(seconds * RATE))
    for i in range(n):
        samples[i] *= i / n
        samples[-1 - i] *= i / n
    return samples

def write(name, samples):
    with wave.open(str(OUT / name), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in samples))

# Alarm siren: a museum bell-and-wail, rising and falling once per 2 s so it loops seamlessly.
siren, phase = [], 0.0
length = int(2.0 * RATE)
for i in range(length):
    p = i / length
    frequency = 620 + 380 * (0.5 - 0.5 * math.cos(math.tau * p))
    phase += frequency / RATE
    tone = math.sin(math.tau * phase) + 0.35 * math.sin(math.tau * phase * 2) + 0.15 * math.sin(math.tau * phase * 3)
    siren.append(tone)
write("AlarmSiren.wav", normalise(siren, 0.7))

# Guard whistle: a pea whistle, two short trills with a breathy edge.
whistle = []
for i in range(int(0.75 * RATE)):
    t = i / RATE
    blast = 1.0 if t < 0.28 or 0.36 < t < 0.7 else 0.0
    trill = 1 + 0.06 * math.sin(math.tau * 38 * t)
    envelope = blast * min(1.0, t * 60) * (1 - max(0.0, t - 0.62) * 8)
    whistle.append(envelope * (math.sin(math.tau * 2900 * trill * t) + 0.25 * (rng.random() * 2 - 1)))
write("GuardWhistle.wav", fade(normalise(whistle, 0.8), 0.01))
print("Wrote", ", ".join(sorted(p.name for p in OUT.glob("*.wav"))))
