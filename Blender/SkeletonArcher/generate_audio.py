"""Synthesise the skeleton archer's sound effects with the Python standard library.

Run from the repository root: python3 Blender/SkeletonArcher/generate_audio.py
Writes 16-bit mono WAV files to Assets/_Project/Audio/SkeletonArcher/. A fixed seed
keeps the output reproducible; no recorded or downloaded audio is used.
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/Audio/SkeletonArcher"
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(1907)

def silence(seconds): return [0.0] * int(seconds * RATE)

def add(buffer, start, samples, gain=1.0):
    offset = int(start * RATE)
    for i, value in enumerate(samples):
        if 0 <= offset + i < len(buffer): buffer[offset + i] += value * gain

def click(frequency, decay, length=0.05, noise=0.4):
    """A short resonant knock: a decaying tone plus a burst of noise, like bone on bone."""
    out, phase = [], rng.random() * math.tau
    for i in range(int(length * RATE)):
        t = i / RATE
        envelope = math.exp(-t * decay)
        out.append(envelope * (math.sin(phase + math.tau * frequency * t) + noise * (rng.random() * 2 - 1) * math.exp(-t * decay * 3)))
    return out

def lowpass(samples, amount):
    out, state = [], 0.0
    for value in samples:
        state += amount * (value - state)
        out.append(state)
    return out

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

# Bow creak: stick-slip friction pulses that speed up and rise in pitch as the limbs bend.
creak = silence(0.75)
t = 0.0
while t < 0.7:
    progress = t / 0.7
    add(creak, t, click(180 + 260 * progress, 70, 0.03, 0.9), 0.35 + 0.5 * progress)
    t += 0.045 - 0.03 * progress + rng.random() * 0.006
write("BowCreak.wav", fade(normalise(lowpass(creak, 0.35), 0.7)))

# Release: a plucked string (Karplus-Strong) with a low wooden thump from the limbs.
period = int(RATE / 98)
line = [rng.random() * 2 - 1 for _ in range(period)]
twang = []
for i in range(int(0.45 * RATE)):
    value = line[i % period]
    line[i % period] = 0.996 * 0.5 * (value + line[(i + 1) % period])
    twang.append(value)
thump = [math.exp(-i / RATE * 30) * math.sin(math.tau * 70 * i / RATE) for i in range(int(0.2 * RATE))]
release = silence(0.45)
add(release, 0, twang, 0.8)
add(release, 0, thump, 0.9)
add(release, 0, click(1400, 160, 0.02, 1.0), 0.5)
write("BowRelease.wav", fade(normalise(release), 0.004))

# Flight whoosh: filtered noise whose brightness and loudness peak mid-flight.
whoosh, state = [], 0.0
length = int(0.5 * RATE)
for i in range(length):
    p = i / length
    envelope = math.sin(math.pi * p) ** 2
    state += (0.05 + 0.25 * envelope) * ((rng.random() * 2 - 1) - state)
    whoosh.append(state * envelope)
write("ArrowWhoosh.wav", fade(normalise(whoosh, 0.6)))

# Impact: a short low knock and a splintering click.
impact = silence(0.3)
add(impact, 0, [math.exp(-i / RATE * 28) * math.sin(math.tau * (150 - 60 * i / RATE) * i / RATE) for i in range(int(0.3 * RATE))])
add(impact, 0, click(2100, 90, 0.06, 1.2), 0.6)
add(impact, 0.012, click(900, 120, 0.04, 0.8), 0.4)
write("ArrowImpact.wav", fade(normalise(impact), 0.003))

# Rattle loop: random hollow bone clicks, wrapped so the loop point is seamless.
loop = silence(1.2)
for _ in range(34):
    start = rng.random() * 1.2
    sample = click(rng.choice([650, 900, 1300, 1800, 2400]) * (0.9 + rng.random() * 0.2), 110, 0.05, 0.6)
    gain = 0.4 + rng.random() * 0.6
    add(loop, start, sample, gain)
    overflow = int(start * RATE) + len(sample) - len(loop)
    if overflow > 0: add(loop, 0, sample[-overflow:], gain)
write("BoneRattleLoop.wav", normalise(lowpass(loop, 0.6), 0.7))

# Collapse: a dense clatter that thins out, with heavier knocks as large bones land.
collapse = silence(1.8)
for k in range(150):
    start = 1.6 * (rng.random() ** 1.8)
    add(collapse, start, click(rng.uniform(500, 2600), rng.uniform(60, 140), 0.06, 0.7), rng.uniform(0.2, 0.7) * (1 - start / 2))
for k in range(9):
    add(collapse, 0.05 + k * 0.13 + rng.random() * 0.05, click(rng.uniform(140, 260), 35, 0.15, 0.3), 0.8)
collapse = fade(normalise(lowpass(collapse, 0.7)), 0.02)
write("BoneCollapse.wav", collapse)
# Assembly is the collapse played backwards: the bones clatter together and settle.
write("BoneAssemble.wav", list(reversed(collapse)))
print("Wrote", ", ".join(sorted(p.name for p in OUT.glob("*.wav"))))
