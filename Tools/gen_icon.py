#!/usr/bin/env python3
"""Draws the game's icon: Minnal glowing over the dark rooftops of Minnalpatti.

    python3 Tools/gen_icon.py

Drawn at 64x64 and scaled up without smoothing, so it stays pixel art.
"""
import math
import random

from PIL import Image, ImageDraw

N = 64
img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
px = img.load()
rnd = random.Random(5)
centre = (32, 27)

# Night sky, lit from the middle by Minnal's glow (dithered so it stays crisp).
for y in range(N):
    for x in range(N):
        dist = math.hypot(x - centre[0], y - centre[1])
        glow = max(0.0, 1 - dist / 30)
        glow = glow * glow
        if rnd.random() < 0.35:
            glow = max(0.0, glow + rnd.uniform(-0.08, 0.08))
        sky = (16 + y // 6, 20 + y // 5, 48 + y // 3)
        warm = (255, 214, 110)
        px[x, y] = tuple(int(sky[i] + (warm[i] - sky[i]) * glow * 0.75) for i in range(3)) + (255,)

d = ImageDraw.Draw(img)
for _ in range(9):  # minminis
    x, y = rnd.randrange(5, 59), rnd.randrange(5, 40)
    if math.hypot(x - centre[0], y - centre[1]) > 15:
        px[x, y] = (190, 255, 110, 255)

# Rooftops and a coconut tree in silhouette along the bottom.
dark = (9, 11, 26, 255)
d.rectangle([0, 52, 63, 63], fill=dark)
d.polygon([(2, 52), (10, 44), (20, 44), (26, 52)], fill=dark)
d.rectangle([30, 46, 46, 52], fill=dark)
d.polygon([(46, 52), (52, 42), (62, 42), (63, 52)], fill=dark)
for x, y in ((8, 54), (14, 54), (36, 49), (41, 49), (55, 48)):  # a few lit windows
    d.rectangle([x, y, x + 1, y + 2], fill=(255, 200, 110, 255))
d.line([53, 44, 55, 31], fill=dark, width=2)  # trunk
for dx, dy in ((-7, 2), (-5, -3), (0, -5), (5, -3), (7, 2)):
    d.line([55, 31, 55 + dx, 31 + dy], fill=dark, width=1)

# Minnal itself.
minnal = Image.open("Assets/Art/Characters/minnal.png").convert("RGBA").resize((24, 26), Image.NEAREST)
img.alpha_composite(minnal, (centre[0] - 12, centre[1] - 13))

# Rounded corners.
mask = Image.new("L", (N, N), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, N - 1, N - 1], radius=11, fill=255)
img.putalpha(mask)

big = img.resize((256, 256), Image.NEAREST)
big.save("Assets/Art/UI/icon.png")
big.save("Packaging/currentpochu.png")
big.save("Packaging/currentpochu.ico", sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("icon written")
