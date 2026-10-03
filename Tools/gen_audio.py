#!/usr/bin/env python3
"""Generates every sound in the game into Assets/Resources/Audio. Run from the project root:

    python3 Tools/gen_audio.py

Everything is synthesised from sine, square and triangle waves and noise; there are no samples.
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = "Assets/Resources/Audio"


def save(name, samples, volume=0.8):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(b"".join(struct.pack("<h", int(s / max(peak, 1.0) * 32767 * volume)) for s in samples))


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def sine(p):
    return math.sin(2 * math.pi * p)


def square(p):
    return 1.0 if p % 1 < 0.5 else -1.0


def triangle(p):
    return 4 * abs(p % 1 - 0.5) - 1


def tone(freq, seconds, shape=sine, attack=0.005, release=0.05, end_freq=None, volume=1.0):
    """One note. The pitch can slide from freq to end_freq."""
    n = int(RATE * seconds)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        f = freq + (end_freq - freq) * t if end_freq else freq
        phase += f / RATE
        env = min(1.0, i / (attack * RATE + 1)) * min(1.0, (n - i) / (release * RATE + 1))
        out.append(shape(phase) * env * volume)
    return out


def noise(seconds, volume=1.0, smooth=0.0, seed=1):
    rnd = random.Random(seed)
    n = int(RATE * seconds)
    out, last = [], 0.0
    for i in range(n):
        last = last * smooth + rnd.uniform(-1, 1) * (1 - smooth)
        env = min(1.0, i / 40) * (1 - i / n)
        out.append(last * env * volume)
    return out


def mix(*tracks):
    out = [0.0] * max(len(t) for t in tracks)
    for track in tracks:
        for i, s in enumerate(track):
            out[i] += s
    return out


def add(dest, src, at_seconds):
    start = int(at_seconds * RATE)
    for i, s in enumerate(src):
        if start + i < len(dest):
            dest[start + i] += s


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(name):
    """'A4' -> 440.0"""
    return 440.0 * 2 ** ((NOTES[name[0]] + (int(name[-1]) - 4) * 12 - 9 + (1 if "#" in name else 0)) / 12)


def bell(note, seconds=0.5, volume=1.0):
    f = hz(note)
    n = int(RATE * seconds)
    return [(sine(f * i / RATE) + 0.4 * sine(2.01 * f * i / RATE)) * math.exp(-5 * i / n) * volume for i in range(n)]


def effects():
    save("step", noise(0.045, smooth=0.75, seed=3), 0.5)
    save("blip", tone(760, 0.03, square, release=0.02), 0.25)
    save("click", tone(520, 0.07, triangle, end_freq=900), 0.6)
    save("minmini", tone(1300, 0.12, sine, end_freq=2000, release=0.09), 0.5)
    save("pickup", tone(660, 0.07, triangle) + tone(990, 0.12, triangle, release=0.1), 0.6)
    save("fail", tone(440, 0.16, triangle) + tone(330, 0.16, triangle) + tone(233, 0.35, triangle, release=0.3), 0.6)
    save("squeak", tone(2400, 0.07, sine, end_freq=3300) + tone(3000, 0.09, sine, end_freq=2200), 0.5)
    save("bark", mix(noise(0.11, 0.5, 0.5, 5), tone(420, 0.11, square, end_freq=260, volume=0.6)) + silence(0.05)
         + mix(noise(0.13, 0.5, 0.5, 6), tone(390, 0.13, square, end_freq=230, volume=0.6)), 0.6)
    save("bleat", tone(520, 0.45, lambda p: triangle(p) * (0.6 + 0.4 * sine(p / 22)), end_freq=430, release=0.2), 0.5)

    gust = noise(1.6, 1.0, 0.93, 21)
    save("gust", [g * math.sin(math.pi * i / len(gust)) for i, g in enumerate(gust)], 0.7)

    lamp = silence(1.3)
    add(lamp, tone(110, 0.35, lambda p: 2 * (p % 1) - 1, attack=0.3, volume=0.35), 0)
    for i, note in enumerate(["C6", "E6", "G6", "C7"]):
        add(lamp, bell(note, 0.6, 0.6), 0.25 + i * 0.09)
    save("lamp", lamp, 0.7)

    quest = silence(1.5)
    for i, note in enumerate(["G5", "C6", "E6", "G6", "E6", "G6"]):
        add(quest, bell(note, 0.5, 0.7), i * 0.11)
    add(quest, bell("C7", 0.8, 0.8), 0.7)
    save("quest", quest, 0.7)

    power = silence(3.2)
    add(power, tone(55, 1.4, lambda p: 2 * (p % 1) - 1, attack=1.2, end_freq=220, volume=0.4), 0)
    add(power, noise(1.4, 0.25, 0.9, 9)[::-1], 0)
    for note in ["C4", "G4", "C5", "E5", "G5", "C6"]:
        add(power, bell(note, 1.8, 0.45), 1.35)
    save("power", power, 0.8)


def ambience():
    """Eight seconds of night: crickets over a very soft hush. Loops cleanly."""
    seconds = 8
    out = [s * 0.05 for s in noise(seconds, 1.0, 0.97, 11)]
    n = len(out)
    out = [out[i] * 0 + 0.04 * math.sin(2 * math.pi * 0.25 * i / RATE) * out[i] + out[i] * 0.6 for i in range(n)]
    rnd = random.Random(4)
    for voice in range(3):
        freq = 4200 + voice * 450
        t = rnd.uniform(0, 1)
        while t < seconds - 0.6:
            for k in range(rnd.choice([3, 4, 5])):  # a train of quick chirps
                chirp = tone(freq, 0.035, sine, attack=0.004, release=0.02, volume=0.12 - voice * 0.03)
                add(out, chirp, t + k * 0.06)
            t += rnd.uniform(0.7, 1.6)
    save("ambience", out, 0.5)


def music():
    """A gentle looping night theme in Mohanam (the major pentatonic: C D E G A)."""
    beat = 60 / 92
    bars = 16
    total = bars * 4 * beat
    out = silence(total)
    scale = ["C4", "D4", "E4", "G4", "A4", "C5", "D5", "E5", "G5", "A5"]
    roots = ["C3", "C3", "A2", "A2", "F2", "F2", "G2", "G2"] * 2

    def soft(p):
        return 0.7 * triangle(p) + 0.3 * sine(p)

    rnd = random.Random(7)
    position = 5
    for bar in range(bars):
        start = bar * 4 * beat
        root = roots[bar]
        # bass: root and fifth, slow
        add(out, tone(hz(root), beat * 1.9, sine, attack=0.02, release=0.4, volume=0.5), start)
        add(out, tone(hz(root) * 1.5, beat * 1.9, sine, attack=0.02, release=0.4, volume=0.35), start + 2 * beat)
        # a soft tick on the off-beats, like a distant mridangam
        for b in (1, 2.5, 3):
            add(out, noise(0.03, 0.12, 0.6, bar * 10 + int(b * 2)), start + b * beat)
        # melody: a wandering line that mostly moves by step and rests now and then
        t = 0.0
        phrase_rest = bar % 4 == 3
        while t < 4:
            length = rnd.choice([0.5, 0.5, 1, 1, 1.5, 2])
            length = min(length, 4 - t)
            if not (phrase_rest and t >= 2) and rnd.random() > 0.12:
                position = max(0, min(len(scale) - 1, position + rnd.choice([-2, -1, -1, 0, 1, 1, 2])))
                add(out, tone(hz(scale[position]), length * beat * 0.95, soft, attack=0.01, release=0.18, volume=0.42),
                    start + t * beat)
                if rnd.random() < 0.25:  # an occasional sparkle an octave up
                    add(out, bell(scale[position][0] + "6", 0.5, 0.12), start + t * beat)
            t += length
    save("music", out, 0.7)


if __name__ == "__main__":
    effects()
    ambience()
    music()
    print("audio written to", OUT)
