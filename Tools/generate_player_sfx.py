import math
import struct
import wave
import os

out = os.path.join("Assets", "Resources", "PlayerSFX")
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


def env(i, n, a=0.01, r=0.08):
    t = i / max(n, 1)
    attack = min(1.0, t / a) if a > 0 else 1.0
    release = min(1.0, (1.0 - t) / r) if r > 0 else 1.0
    return attack * release


def noise(i, seed):
    x = (i * 1103515245 + seed) % 2147483647
    return 2.0 * x / 2147483647.0 - 1.0


def footstep():
    n = int(sr * 0.09)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.005, 0.05)
        thump = 0.55 * math.sin(2 * math.pi * 90 * t) * math.exp(-35 * t)
        click = 0.18 * math.sin(2 * math.pi * 420 * t) * math.exp(-60 * t)
        nse = 0.08 * noise(i, 12345) * math.exp(-50 * t)
        samples.append((thump + click + nse) * e * 0.7)
    return samples


def shoot():
    n = int(sr * 0.14)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.002, 0.08)
        freq = 880 * math.exp(-6 * t) + 220
        tone = 0.45 * math.sin(2 * math.pi * freq * t)
        buzz = 0.2 * math.sin(2 * math.pi * freq * 2.0 * t)
        nse = 0.12 * noise(i, 99991) * math.exp(-25 * t)
        samples.append((tone + buzz + nse) * e * 0.75)
    return samples


def jump():
    n = int(sr * 0.18)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.01, 0.1)
        freq = 180 + 420 * t
        tone = 0.4 * math.sin(2 * math.pi * freq * t)
        air = 0.15 * noise(i, 4242) * math.exp(-12 * t)
        samples.append((tone + air) * e * 0.65)
    return samples


def dash():
    n = int(sr * 0.16)
    samples = []
    for i in range(n):
        t = i / sr
        e = env(i, n, 0.005, 0.09)
        freq = 260 * math.exp(-3 * t) + 120
        tone = 0.35 * math.sin(2 * math.pi * freq * t)
        nse = 0.25 * noise(i, 777) * math.exp(-18 * t)
        samples.append((tone + nse) * e * 0.7)
    return samples


write_wav(os.path.join(out, "SoftWalk.wav"), footstep())
write_wav(os.path.join(out, "SoftShoot.wav"), shoot())
write_wav(os.path.join(out, "SoftJump.wav"), jump())
write_wav(os.path.join(out, "SoftDash.wav"), dash())
print("Wrote WAV files to", out)
