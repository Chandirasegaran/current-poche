#!/usr/bin/env python3
"""Generates the game's pixel art into Assets/Art. Run from the project root:

    python3 Tools/gen_art.py

Everything is drawn in daytime colours; the night look comes from Unity's 2D lights.
"""
import math
import os
import random

from PIL import Image, ImageDraw

OUT = "Assets/Art"
CLEAR = (0, 0, 0, 0)


def rgb(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def shade(c, f):
    return (max(0, min(255, int(c[0] * f))), max(0, min(255, int(c[1] * f))), max(0, min(255, int(c[2] * f))), c[3])


def save(img, *path):
    full = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    img.save(full)


# ---------------------------------------------------------------- tiles

def noise_tile(seed, base, specks, density):
    rnd = random.Random(seed)
    img = Image.new("RGBA", (16, 16), rgb(base))
    px = img.load()
    for _ in range(density):
        px[rnd.randrange(16), rnd.randrange(16)] = rgb(rnd.choice(specks))
    return img


def grass_tile(seed):
    rnd = random.Random(seed)
    img = noise_tile(seed, "#3f7a3a", ["#3a7236", "#458240"], 40)
    px = img.load()
    for _ in range(5):  # little tufts: a light blade over a dark root
        x, y = rnd.randrange(1, 15), rnd.randrange(2, 15)
        px[x, y] = rgb("#2f6330")
        px[x, y - 1] = rgb("#5a9c47")
        if rnd.random() < 0.5:
            px[x + 1, y - 1] = rgb("#4f8f41")
    if seed % 3 == 0:  # the odd tiny flower
        x, y = rnd.randrange(2, 14), rnd.randrange(2, 14)
        px[x, y] = rgb(rnd.choice(["#f2e6a0", "#f0b7c8", "#ffffff"]))
    return img


def cement_tile(seed):
    rnd = random.Random(seed)
    img = noise_tile(seed, "#9b968c", ["#948f85", "#a39e94"], 30)
    px = img.load()
    if seed % 2 == 0:  # a crack
        x, y = rnd.randrange(3, 12), 0
        while y < 16 and 0 < x < 15:
            px[x, y] = rgb("#7d786f")
            x += rnd.choice([-1, 0, 0, 1])
            y += 1
    return img


def make_tiles():
    for i in range(3):
        save(noise_tile(100 + i, "#3d3f4c", ["#373945", "#454857", "#4b4e5d"], 34), "Tiles", f"road_{i}.png")
        save(noise_tile(200 + i, "#8b6c4c", ["#836446", "#947553", "#7a5d41"], 38), "Tiles", f"dirt_{i}.png")
        save(grass_tile(300 + i), "Tiles", f"grass_{i}.png")
        save(cement_tile(400 + i), "Tiles", f"cement_{i}.png")


# ---------------------------------------------------------------- characters

BODY = [
    "................",
    ".....oooooo.....",
    "....oHHHHHHo....",
    "...oHHHHHHHHo...",
    "...oHhhhhhhho...",
    "...ohhsssshho...",
    "...ohsssssso....",
    "...osesssseso...",
    "...odssssssdo...",
    "....ossmmsso....",
    ".....oooooo.....",
    "....oTttttTo....",
    "...ostttttTso...",
    "...ostttttTso...",
    "....oPppppPo....",
]
# The outline at row 6 is patched below so the head stays symmetrical.
BODY[6] = "...ohsssssssho.."[:16]
BODY[6] = "...ohssssssho..."

LEGS = {
    "idle": [
        "....oppoopPo....",
        "....osdoosdo....",
        "....obbooboo....",
        ".....oo..oo.....",
    ],
    "walk1": [
        "....oppoopPo....",
        "....osdo.oo.....",
        "....obbo........",
        ".....oo.........",
    ],
    "walk2": [
        "....oppoopPo....",
        ".....oo.osdo....",
        "........obbo....",
        ".........oo.....",
    ],
}

KIDS = [
    # Four boys, then four girls. The picker on the title screen shows them in this order.
    # name,     hair,      skin,      shirt,     shorts,    braids, extra
    ("kavin", "#2a2233", "#c98b5e", "#e9b63a", "#3b5d8f", False, None),
    ("abdul", "#33261f", "#d39c6c", "#3fae8f", "#4a4458", False, None),
    ("arul", "#1f1a2b", "#b9774d", "#e2574c", "#2f3a5a", False, "glasses"),
    ("muthu", "#2a2233", "#8f5c3a", "#f3ead7", "#5a7a3a", False, None),
    ("yazhini", "#1f1a2b", "#b9774d", "#d9486a", "#3a3f6b", True, None),
    ("mercy", "#241c26", "#a86a45", "#7d6be0", "#2f4a57", True, None),
    ("nila", "#2a2233", "#d39c6c", "#3f8fd9", "#6a3f6b", False, "bun"),
    ("kayal", "#33261f", "#c98b5e", "#f08a3c", "#3a5a4a", True, None),
]


# The head as seen from behind (all hair) and from the side (one eye, facing right).
BACK_HEAD = {
    5: "...ohhhhhhhho...",
    6: "...ohhhhhhhho...",
    7: "...ohhhhhhhho...",
    8: "...ohhhhhhhho...",
    9: "....ohhhhhho....",
}
SIDE_HEAD = {
    5: "...ohhhhsssho...",
    6: "...ohhhssssso...",
    7: "...ohhhsssseo...",
    8: "...ohhhssssdo...",
    9: "....ohhssmso....",
}


def kid_frame(hair, skin, shirt, shorts, braids, legs, view="down", extra=None):
    hair, skin, shirt, shorts = rgb(hair), rgb(skin), rgb(shirt), rgb(shorts)
    pal = {
        ".": CLEAR,
        "o": rgb("#17131f"),
        "H": shade(hair, 1.35),
        "h": hair,
        "s": skin,
        "d": shade(skin, 0.82),
        "e": rgb("#17131f"),
        "m": shade(skin, 0.7),
        "t": shirt,
        "T": shade(shirt, 0.78),
        "p": shorts,
        "P": shade(shorts, 0.78),
        "b": rgb("#d9d2c4"),
    }
    rows = list(BODY) + LEGS[legs]
    for row, pixels in {"up": BACK_HEAD, "side": SIDE_HEAD}.get(view, {}).items():
        rows[row] = pixels
    img = Image.new("RGBA", (16, len(rows)), CLEAR)
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            px[x, y] = pal[ch]
    if braids:  # two plaits hanging beside the face, with a ribbon (one shows from the side)
        for y in range(7, 12):
            for x in ((2,) if view == "side" else (2, 13)):
                px[x, y] = pal["h"] if y < 11 else rgb("#f25c5c")
            px[1, y] = pal["o"] if px[1, y] == CLEAR else px[1, y]
            if view != "side":
                px[14, y] = pal["o"] if px[14, y] == CLEAR else px[14, y]
        for x in ((2,) if view == "side" else (2, 13)):
            px[x, 12] = pal["o"]
    if extra == "bun":  # hair tied up on top of the head
        for x in (7, 8):
            px[x, 0] = pal["H"]
        px[6, 0] = px[9, 0] = pal["o"]
    lens = rgb("#cfe3ea")
    if extra == "glasses" and view == "down":  # pale lenses either side of each eye, joined by a bridge
        for x in (4, 6, 9, 11):
            px[x, 7] = lens
        px[7, 7] = px[8, 7] = pal["o"]
    if extra == "glasses" and view == "side":
        px[10, 7] = lens
        px[9, 7] = pal["o"]
    return img


def make_characters():
    for number, (name, hair, skin, shirt, shorts, braids, extra) in enumerate(KIDS):
        # a portrait for the character picker on the title screen
        save(kid_frame(hair, skin, shirt, shorts, braids, "idle", "down", extra), "..", "Resources", "UI", f"kid_{number}.png")
        for legs in LEGS:
            save(kid_frame(hair, skin, shirt, shorts, braids, legs, "down", extra), "Characters", f"{name}_{legs}.png")
            for view in ("up", "side"):
                save(kid_frame(hair, skin, shirt, shorts, braids, legs, view, extra), "Characters", f"{name}_{view}_{legs}.png")


# ---------------------------------------------------------------- props

def house(seed, wall_hex, door_hex):
    rnd = random.Random(seed)
    w, h = 64, 60
    img = Image.new("RGBA", (w, h), CLEAR)
    d = ImageDraw.Draw(img)
    outline = rgb("#1b1622")
    wall = rgb(wall_hex)

    # wall
    d.rectangle([2, 16, 61, 55], fill=wall, outline=outline)
    d.rectangle([3, 17, 60, 21], fill=shade(wall, 0.72))  # shadow under the eave
    d.rectangle([3, 48, 60, 54], fill=shade(wall, 0.6))  # painted plinth band
    for _ in range(26):  # weathering
        img.putpixel((rnd.randrange(4, 60), rnd.randrange(22, 48)), shade(wall, rnd.choice([0.92, 1.07])))

    # tiled roof, seen from slightly above
    roof = rgb("#b4553b")
    d.rectangle([0, 0, 63, 17], fill=roof, outline=outline)
    for y in range(1, 17):
        for x in range(1, 63):
            row = y // 4
            if y % 4 == 0:
                img.putpixel((x, y), shade(roof, 0.7))
            elif (x + row * 3) % 6 == 0:
                img.putpixel((x, y), shade(roof, 0.84))
            elif y % 4 == 1:
                img.putpixel((x, y), shade(roof, 1.14))
    d.line([1, 1, 62, 1], fill=shade(roof, 1.3))

    # door with a thoranam (mango-leaf garland) above it
    door = rgb(door_hex)
    d.rectangle([25, 27, 38, 55], fill=shade(door, 0.55), outline=outline)
    d.rectangle([27, 29, 31, 54], fill=door)
    d.rectangle([32, 29, 36, 54], fill=shade(door, 0.9))
    d.rectangle([28, 31, 30, 39], fill=shade(door, 0.8))
    d.rectangle([33, 31, 35, 39], fill=shade(door, 0.72))
    d.rectangle([28, 43, 30, 51], fill=shade(door, 0.8))
    d.rectangle([33, 43, 35, 51], fill=shade(door, 0.72))
    img.putpixel((31, 42), rgb("#e8c66a"))
    for i, x in enumerate(range(24, 40)):
        img.putpixel((x, 25), rgb("#2e6b34"))
        if i % 2 == 0:
            img.putpixel((x, 26), rgb("#4f9a45"))

    # two barred windows
    for x0 in (8, 45):
        d.rectangle([x0, 27, x0 + 11, 41], fill=rgb("#141726"), outline=outline)
        d.rectangle([x0 - 1, 42, x0 + 12, 43], fill=shade(wall, 1.2), outline=None)  # sill
        for bx in range(x0 + 2, x0 + 11, 3):
            d.line([bx, 28, bx, 40], fill=rgb("#7f8491"))
        d.line([x0 + 1, 34, x0 + 10, 34], fill=rgb("#7f8491"))

    # thinnai: the raised stone veranda along the front
    stone = rgb("#8f867a")
    d.rectangle([0, 56, 63, 59], fill=stone, outline=outline)
    d.line([1, 57, 62, 57], fill=shade(stone, 1.25))
    return img


def streetlight(lit):
    img = Image.new("RGBA", (16, 56), CLEAR)
    d = ImageDraw.Draw(img)
    outline, pole = rgb("#1b1622"), rgb("#6f7683")
    d.rectangle([5, 50, 10, 55], fill=shade(pole, 0.8), outline=outline)  # base
    d.rectangle([6, 6, 9, 50], fill=outline)
    d.line([7, 7, 7, 50], fill=shade(pole, 1.2))
    d.line([8, 7, 8, 50], fill=shade(pole, 0.8))
    d.rectangle([6, 3, 14, 6], fill=outline)  # arm
    d.line([7, 4, 13, 4], fill=pole)
    d.rectangle([9, 6, 15, 9], fill=outline)  # lamp head
    d.line([10, 7, 14, 7], fill=shade(pole, 0.7))
    bulb = rgb("#fff2b3") if lit else rgb("#4f535f")
    d.line([10, 8, 14, 8], fill=bulb)
    for y in range(14, 50, 9):  # paint bands
        d.line([7, y, 8, y], fill=rgb("#c9cdd6"))
    return img


def coconut_tree(seed):
    rnd = random.Random(seed)
    w, h = 48, 72
    img = Image.new("RGBA", (w, h), CLEAR)
    px = img.load()
    outline = rgb("#1b1622")
    trunk = rgb("#7b5b3d")
    lean = rnd.choice([-5, 4, 6])
    crown = (24 + lean, 24)

    # trunk: a gentle curve, ringed
    for y in range(crown[1], h):
        t = (y - crown[1]) / (h - crown[1])
        cx = crown[0] - lean * (t ** 1.6)
        half = 1.5 + 1.2 * t
        for x in range(int(cx - half) - 1, int(cx + half) + 2):
            if 0 <= x < w:
                inside = cx - half <= x <= cx + half
                if inside:
                    c = trunk if (y // 3) % 2 else shade(trunk, 0.82)
                    px[x, y] = shade(c, 1.15) if x < cx - half + 1 else c
                elif px[x, y] == CLEAR:
                    px[x, y] = outline

    # fronds: arcs of drooping leaflets
    greens = [rgb("#2c6a37"), rgb("#3c8a44"), rgb("#57a84f")]
    for i, ang in enumerate([-160, -125, -95, -60, -25, 10, 190]):
        a = math.radians(ang + rnd.uniform(-6, 6))
        length = rnd.uniform(17, 22)
        for s in range(int(length * 2)):
            t = s / (length * 2)
            x = crown[0] + math.cos(a) * length * t
            y = crown[1] + math.sin(a) * length * t + 9 * t * t  # droop
            xi, yi = int(round(x)), int(round(y))
            if 0 <= xi < w and 0 <= yi < h:
                px[xi, yi] = greens[1]
                leaf = int(1 + 4 * math.sin(math.pi * min(1, t * 1.15)))
                for k in range(1, leaf + 1):
                    if 0 <= yi + k < h:
                        px[xi, yi + k] = greens[0] if k > leaf / 2 else greens[1]
                if 0 <= yi - 1 < h:
                    px[xi, yi - 1] = greens[2]
    for dx, dy in [(-2, 2), (1, 3), (3, 1)]:  # coconuts
        for ox in range(3):
            for oy in range(3):
                if (ox, oy) not in [(0, 0), (2, 0), (0, 2), (2, 2)]:
                    px[crown[0] + dx + ox - 1, crown[1] + dy + oy] = rgb("#5d7a2c")
        px[crown[0] + dx, crown[1] + dy + 1] = rgb("#86a43a")
    return img


def kolam():
    """A sikku-style kolam: dots with a looping line, four-way symmetric."""
    size = 32
    img = Image.new("RGBA", (size, size), CLEAR)
    px = img.load()
    white = rgb("#efeadc", 235)
    soft = rgb("#efeadc", 150)
    c = size // 2

    def plot4(x, y, col):
        for sx, sy in ((x, y), (size - 1 - x, y), (x, size - 1 - y), (size - 1 - x, size - 1 - y)):
            if 0 <= sx < size and 0 <= sy < size:
                px[sx, sy] = col
        for sx, sy in ((y, x), (size - 1 - y, x), (y, size - 1 - x), (size - 1 - y, size - 1 - x)):
            if 0 <= sx < size and 0 <= sy < size:
                px[sx, sy] = col

    for i in range(0, 13):  # outer diamond
        plot4(c - 1 - i, 3 + i, white)
    for i in range(0, 7):  # inner diamond
        plot4(c - 1 - i, 9 + i, soft)
    for gx in range(4, c, 4):  # the dot grid
        for gy in range(4, c, 4):
            if gx + gy >= 14:
                plot4(gx, gy, white)
    for i in range(3):  # petals at the four points
        plot4(c - 1 - i, 1 + i // 2, white)
    px[c, c] = px[c - 1, c] = px[c, c - 1] = px[c - 1, c - 1] = white
    return img


def tulsi_pot():
    img = Image.new("RGBA", (16, 26), CLEAR)
    d = ImageDraw.Draw(img)
    outline = rgb("#1b1622")
    d.rectangle([3, 12, 12, 25], fill=rgb("#d8cdb8"), outline=outline)
    for x in range(4, 12, 2):  # red and white temple stripes
        d.line([x, 13, x, 24], fill=rgb("#b4433a"))
    d.rectangle([2, 10, 13, 12], fill=rgb("#e8dfcc"), outline=outline)
    for x, y, c in [(7, 2, "#57a84f"), (5, 4, "#3c8a44"), (9, 4, "#3c8a44"), (4, 6, "#2c6a37"), (7, 5, "#57a84f"),
                    (10, 6, "#2c6a37"), (6, 7, "#3c8a44"), (8, 7, "#3c8a44"), (7, 8, "#2c6a37"), (5, 8, "#57a84f"),
                    (9, 8, "#57a84f"), (7, 3, "#3c8a44"), (6, 5, "#2c6a37"), (8, 5, "#2c6a37"), (7, 9, "#2c6a37")]:
        img.putpixel((x, y), rgb(c))
    return img


def glow():
    """Soft round dot used for fireflies and light flecks."""
    img = Image.new("RGBA", (8, 8), CLEAR)
    px = img.load()
    for y in range(8):
        for x in range(8):
            dist = math.hypot(x - 3.5, y - 3.5)
            a = max(0.0, 1 - dist / 4)
            px[x, y] = (255, 255, 255, int(255 * a * a))
    return img


def make_props():
    for i, (wall, door) in enumerate([("#e58fa6", "#7a4a2a"), ("#6fc2b0", "#5b3a24"), ("#f0c75e", "#6a4226"),
                                      ("#86a9e3", "#74482c")]):
        save(house(500 + i, wall, door), "Props", f"house_{i}.png")
    save(streetlight(True), "Props", "streetlight_on.png")
    save(streetlight(False), "Props", "streetlight_off.png")
    for i in range(3):
        save(coconut_tree(600 + i), "Props", f"coconut_{i}.png")
    save(tulsi_pot(), "Props", "tulsi.png")
    save(kolam(), "Decals", "kolam.png")
    save(glow(), "Decals", "glow.png")


if __name__ == "__main__":
    make_tiles()
    make_characters()
    make_props()
    import gen_art_town
    gen_art_town.main()
    print("art written to", OUT)
