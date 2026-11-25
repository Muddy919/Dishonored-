import math
import os
import struct
import wave

out = os.path.join("Assets", "Resources", "EnemySFX")
os.makedirs(out, exist_ok=True)
sr = 44100


def write_wav(path, samples):
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        frames = b"".join(
            struct.pack("<h", max(-32767, min(32767, int(s * 32767)))) for s in samples
        )
        w.writeframes(frames)


def noise(i, seed):
    x = (i * 1103515245 + seed) % 2147483647
    return 2.0 * x / 2147483647.0 - 1.0


def env(i, n, a=0.004, r=0.08):
    t = i / max(n, 1)
    attack = min(1.0, t / a) if a > 0 else 1.0
    release = min(1.0, (1.0 - t) / r) if r > 0 else 1.0
    return attack * release


def laser_zap():
    n = int(sr * 0.16)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.002, 0.09)
        freq = 1400 * math.exp(-9 * t) + 260
        tone = 0.42 * math.sin(2 * math.pi * freq * t)
        harm = 0.18 * math.sin(2 * math.pi * freq * 2.2 * t)
        spark = 0.12 * noise(i, 4242) * math.exp(-28 * t)
        samples.append((tone + harm + spark) * e * 0.8)
    return samples


def laser_burst():
    n = int(sr * 0.11)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.0015, 0.06)
        freq = 980 * math.exp(-11 * t) + 180
        tone = 0.38 * math.sin(2 * math.pi * freq * t)
        chirp = 0.2 * math.sin(2 * math.pi * (freq + 700 * t) * t)
        nse = 0.1 * noise(i, 777) * math.exp(-32 * t)
        samples.append((tone + chirp + nse) * e * 0.78)
    return samples


def laser_sniper():
    n = int(sr * 0.22)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.003, 0.12)
        freq = 620 * math.exp(-4.5 * t) + 140
        tone = 0.4 * math.sin(2 * math.pi * freq * t)
        beam = 0.22 * math.sin(2 * math.pi * freq * 3.1 * t) * math.exp(-8 * t)
        whoosh = 0.14 * noise(i, 9991) * math.exp(-14 * t)
        samples.append((tone + beam + whoosh) * e * 0.82)
    return samples


write_wav(os.path.join(out, "LaserZap.wav"), laser_zap())
write_wav(os.path.join(out, "LaserBurst.wav"), laser_burst())
write_wav(os.path.join(out, "LaserSniper.wav"), laser_sniper())
print("Wrote enemy laser SFX to", out)
