"""Synthesise the mummies' and canopic jars' sound effects with the Python standard library.

Run from the repository root: python3 Blender/Mummy/generate_audio.py
Writes 16-bit mono WAV files to Assets/_Project/Audio/Mummy/. A fixed seed keeps
the output reproducible; no recorded or downloaded audio is used.
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/Audio/Mummy"
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(1922)

def silence(seconds): return [0.0] * int(seconds * RATE)

def add(buffer, start, samples, gain=1.0):
    offset = int(start * RATE)
    for i, value in enumerate(samples):
        if 0 <= offset + i < len(buffer): buffer[offset + i] += value * gain

def click(frequency, decay, length=0.05, noise=0.4):
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

def resonator(samples, frequency, q=12.0):
    """Two-pole band-pass filter, used for vowel-like formants."""
    w = math.tau * frequency / RATE
    r = math.exp(-w / (2 * q))
    a1, a2 = -2 * r * math.cos(w), r * r
    y1 = y2 = 0.0
    out = []
    for x in samples:
        y = (1 - r) * x - a1 * y1 - a2 * y2
        y2, y1 = y1, y
        out.append(y)
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

def moan(seconds, pitch, rise=0.0):
    """A hollow throat moan: a buzzing source through two formants, with a slow swell."""
    source, phase = [], 0.0
    n = int(seconds * RATE)
    for i in range(n):
        t = i / RATE
        p = i / n
        f = pitch * (1 + rise * p) * (1 + 0.03 * math.sin(math.tau * 5.5 * t)) * (1 + 0.04 * math.sin(math.tau * 0.7 * t))
        phase += f / RATE
        buzz = 2 * (phase % 1.0) - 1
        source.append(buzz + 0.35 * (rng.random() * 2 - 1))
    voice = [a + 0.7 * b for a, b in zip(resonator(source, 420, 8), resonator(source, 780, 10))]
    return [v * math.sin(math.pi * min(1.0, (i / n) * 1.2)) ** 1.5 for i, v in enumerate(voice)]

write("MummyGroan.wav", fade(normalise(moan(1.5, 82, -0.12), 0.8), 0.05))

# Bite: a dry snap of teeth over a short crunch.
bite = silence(0.35)
add(bite, 0.0, click(1800, 120, 0.05, 1.4), 0.7)
add(bite, 0.01, lowpass([rng.random() * 2 - 1 for _ in range(int(0.12 * RATE))], 0.5), 0.5)
add(bite, 0.0, [math.exp(-i / RATE * 30) * math.sin(math.tau * 110 * i / RATE) for i in range(int(0.2 * RATE))], 0.6)
write("MummyBite.wav", fade(normalise(bite), 0.003))

# Shuffle loop: bandaged feet dragging, two steps per loop, wrapped for a seamless loop.
shuffle, state = [], 0.0
length = int(1.6 * RATE)
for i in range(length):
    p = (i / length) * 2 % 1.0
    drag = math.exp(-((p - 0.35) / 0.18) ** 2)
    state += 0.08 * ((rng.random() * 2 - 1) - state)
    shuffle.append(state * (0.15 + drag))
for step in (0.05, 0.85):
    add(shuffle, step, [math.exp(-i / RATE * 25) * math.sin(math.tau * 70 * i / RATE) for i in range(int(0.2 * RATE))], 0.5)
write("MummyShuffleLoop.wav", normalise(shuffle, 0.6))

# Rise: cloth rustling and creaking joints building into a groan.
rise = silence(1.7)
add(rise, 0.0, [v * min(1.0, i / (0.4 * RATE)) for i, v in enumerate(lowpass([rng.random() * 2 - 1 for _ in range(int(1.2 * RATE))], 0.12))], 0.5)
for k in range(10): add(rise, rng.uniform(0.1, 1.0), click(rng.uniform(220, 480), 50, 0.06, 0.8), 0.35)
add(rise, 0.55, moan(1.1, 95, 0.1), 0.8)
write("MummyRise.wav", fade(normalise(rise), 0.03))

# Crumble: dry dust pouring away, thinning out.
crumble, state = silence(1.9), 0.0
grains = []
for i in range(int(1.8 * RATE)):
    p = i / (1.8 * RATE)
    state += 0.25 * ((rng.random() * 2 - 1) - state)
    grains.append(state * (1 - p) ** 1.5)
add(crumble, 0, grains, 0.7)
for k in range(60):
    start = 1.5 * rng.random() ** 2
    add(crumble, start, click(rng.uniform(1500, 4000), 200, 0.02, 1.0), 0.25 * (1 - start / 1.6))
add(crumble, 0.0, moan(0.7, 70, -0.3), 0.5)
write("MummyCrumble.wav", fade(normalise(crumble), 0.02))

# Jar shatter: a ceramic crack, bright ringing shards, and pieces skittering away.
shatter = silence(0.9)
add(shatter, 0.0, click(900, 60, 0.12, 2.0), 0.9)
add(shatter, 0.0, [math.exp(-i / RATE * 40) * math.sin(math.tau * 160 * i / RATE) for i in range(int(0.15 * RATE))], 0.6)
for k in range(40):
    start = 0.6 * rng.random() ** 1.6
    add(shatter, start, click(rng.uniform(2200, 5200), rng.uniform(80, 160), 0.06, 0.5), rng.uniform(0.2, 0.6) * (1 - start))
write("JarShatter.wav", fade(normalise(shatter), 0.003))

# Curse breaking: a ghostly falling wail with a wind rush, played when a bound spirit escapes its jar.
curse, state = silence(1.4), 0.0
n = int(1.3 * RATE)
phase = 0.0
for i in range(n):
    p = i / n
    phase += (900 - 600 * p) / RATE
    state += 0.03 * ((rng.random() * 2 - 1) - state)
    envelope = math.sin(math.pi * p) ** 2
    curse[i] += envelope * (0.5 * math.sin(math.tau * phase) * (1 + 0.5 * math.sin(math.tau * 9 * p)) + 2.5 * state)
write("CurseBreak.wav", fade(normalise(curse, 0.7), 0.02))
# Sarcophagus lid: heavy stone grinding on stone, slowing to a thud as the lid swings clear.
grind, state = silence(1.5), 0.0
n = int(1.3 * RATE)
for i in range(n):
    p = i / n
    state += 0.02 * ((rng.random() * 2 - 1) - state)
    roughness = 1 + 0.6 * math.sin(math.tau * (14 - 8 * p) * p * 1.3)
    grind[i] += state * roughness * (1 - p) ** 0.6 * min(1.0, p * 12)
for k in range(14): add(grind, rng.uniform(0.0, 1.0), click(rng.uniform(120, 300), 40, 0.08, 1.2), 0.25)
add(grind, 1.25, [math.exp(-i / RATE * 20) * math.sin(math.tau * 75 * i / RATE) for i in range(int(0.25 * RATE))], 0.9)
write("SarcophagusOpen.wav", fade(normalise(grind), 0.01))

# Sealed jar: a dull knock that does not crack, under a faint low hum.
sealed = silence(0.6)
add(sealed, 0.0, [math.exp(-i / RATE * 35) * math.sin(math.tau * 210 * i / RATE) for i in range(int(0.3 * RATE))], 0.8)
add(sealed, 0.0, [0.25 * math.sin(math.pi * i / (0.6 * RATE)) * math.sin(math.tau * 110 * i / RATE) * (1 + 0.3 * math.sin(math.tau * 6 * i / RATE))
                  for i in range(int(0.6 * RATE))])
write("JarSealed.wav", fade(normalise(sealed, 0.7), 0.005))
print("Wrote", ", ".join(sorted(p.name for p in OUT.glob("*.wav"))))
