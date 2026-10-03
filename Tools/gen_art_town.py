#!/usr/bin/env python3
"""Town art: shops, landmarks, grown-ups, the dog, and the UI. Run via gen_art.py."""
import math
import random

from PIL import Image, ImageDraw, ImageFont

from gen_art import CLEAR, BODY, rgb, save, shade

OUTLINE = rgb("#1b1622")
TEXT_FONT = "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf"
LOGO_FONT = "/usr/share/fonts/aajohan-comfortaa-fonts/Comfortaa-Bold.otf"


def outlined(img, colour=OUTLINE):
    """Returns a copy with a 1px outline around everything opaque."""
    w, h = img.size
    out = Image.new("RGBA", (w + 2, h + 2), CLEAR)
    out.paste(img, (1, 1))
    src = out.copy().load()
    px = out.load()
    for y in range(h + 2):
        for x in range(w + 2):
            if src[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w + 2 and 0 <= ny < h + 2 and src[nx, ny][3] > 0:
                        px[x, y] = colour
                        break
    return out


def pixel_text(text, size=9, colour=(255, 255, 255, 255)):
    """Renders text with hard pixel edges (no smoothing)."""
    font = ImageFont.truetype(TEXT_FONT, size)
    box = font.getbbox(text)
    mask = Image.new("1", (box[2] + 2, box[3] + 2), 0)
    ImageDraw.Draw(mask).text((0, 0), text, font=font, fill=1)
    img = Image.new("RGBA", mask.size, CLEAR)
    img.paste(Image.new("RGBA", mask.size, colour), (0, 0), mask)
    return img.crop(img.getbbox())


def sign(img, text, box, bg, fg="#fff6dc"):
    d = ImageDraw.Draw(img)
    d.rectangle(box, fill=rgb(bg), outline=OUTLINE)
    label = pixel_text(text, 9, rgb(fg))
    x = box[0] + (box[2] - box[0] + 1 - label.width) // 2
    y = box[1] + (box[3] - box[1] + 1 - label.height) // 2
    img.alpha_composite(label, (x, y))


# ---------------------------------------------------------------- tiles

def water_tile(seed):
    rnd = random.Random(seed)
    img = Image.new("RGBA", (16, 16), rgb("#2b5a8c"))
    px = img.load()
    for _ in range(30):
        px[rnd.randrange(16), rnd.randrange(16)] = rgb(rnd.choice(["#27527f", "#316698"]))
    for _ in range(3):  # ripples
        x, y, n = rnd.randrange(0, 11), rnd.randrange(1, 15), rnd.randrange(3, 6)
        for i in range(n):
            px[x + i, y] = rgb("#5f97c4")
    return img


def paddy_tile(seed):
    rnd = random.Random(seed)
    img = Image.new("RGBA", (16, 16), rgb("#3f6f6a"))  # standing water between the rows
    px = img.load()
    for x in range(1, 16, 4):
        for y in range(16):
            px[x, y] = rgb("#4f9a45")
            px[x + 1, y] = rgb("#3f8540") if (y + x) % 3 else rgb("#6bb552")
            if rnd.random() < 0.3:
                px[(x + 2) % 16, y] = rgb("#58a54a")
    return img


def steps_tile(seed):
    rnd = random.Random(seed)
    img = Image.new("RGBA", (16, 16), rgb("#a59c8e"))
    px = img.load()
    for y in (0, 5, 10, 15):
        for x in range(16):
            px[x, y] = rgb("#7f776c")
            if y + 1 < 16:
                px[x, y + 1] = rgb("#bdb4a5")
    for _ in range(10):
        px[rnd.randrange(16), rnd.randrange(16)] = rgb("#978e81")
    return img


def pitch_tile(seed):
    rnd = random.Random(seed)
    img = Image.new("RGBA", (16, 16), rgb("#b89b6e"))
    px = img.load()
    for _ in range(26):
        px[rnd.randrange(16), rnd.randrange(16)] = rgb(rnd.choice(["#ad9166", "#c2a678"]))
    return img


def make_tiles():
    for i in range(3):
        save(water_tile(700 + i), "Tiles", f"water_{i}.png")
        save(paddy_tile(710 + i), "Tiles", f"paddy_{i}.png")
        save(steps_tile(720 + i), "Tiles", f"steps_{i}.png")
        save(pitch_tile(730 + i), "Tiles", f"pitch_{i}.png")


# ---------------------------------------------------------------- buildings

def shop(seed, wall_hex, sign_hex, text, shutter_hex="#8b93a1"):
    """A flat-roofed shop with its shutter pulled down (no power, no business)."""
    rnd = random.Random(seed)
    img = Image.new("RGBA", (64, 60), CLEAR)
    d = ImageDraw.Draw(img)
    wall = rgb(wall_hex)
    d.rectangle([2, 12, 61, 55], fill=wall, outline=OUTLINE)
    d.rectangle([3, 13, 60, 17], fill=shade(wall, 0.72))
    d.rectangle([3, 49, 60, 54], fill=shade(wall, 0.6))
    for _ in range(20):
        img.putpixel((rnd.randrange(4, 60), rnd.randrange(18, 48)), shade(wall, rnd.choice([0.92, 1.07])))
    d.rectangle([0, 9, 63, 12], fill=shade(wall, 1.15), outline=OUTLINE)  # parapet
    sign(img, text, [4, 0, 59, 10], sign_hex)

    shutter = rgb(shutter_hex)
    d.rectangle([9, 21, 54, 55], fill=shutter, outline=OUTLINE)
    for y in range(23, 55, 3):
        d.line([10, y, 53, y], fill=shade(shutter, 0.75))
        d.line([10, y + 1, 53, y + 1], fill=shade(shutter, 1.12))
    d.rectangle([29, 51, 34, 53], fill=rgb("#3a3d46"))  # lock

    stone = rgb("#8f867a")
    d.rectangle([0, 56, 63, 59], fill=stone, outline=OUTLINE)
    d.line([1, 57, 62, 57], fill=shade(stone, 1.25))
    return img


def tea_stall():
    img = Image.new("RGBA", (64, 60), CLEAR)
    d = ImageDraw.Draw(img)
    wood = rgb("#6a4a2f")
    d.rectangle([2, 12, 61, 55], fill=shade(wood, 0.5), outline=OUTLINE)  # dark interior
    d.rectangle([2, 12, 6, 55], fill=wood, outline=OUTLINE)
    d.rectangle([57, 12, 61, 55], fill=wood, outline=OUTLINE)
    sign(img, "TEA", [14, 0, 49, 10], "#b4433a")
    for i, x in enumerate(range(0, 64, 8)):  # striped awning
        d.rectangle([x, 11, x + 7, 20], fill=rgb("#d9534a") if i % 2 else rgb("#f3ead7"))
    d.rectangle([0, 11, 63, 20], outline=OUTLINE)
    for x in range(0, 64, 8):
        d.polygon([(x, 21), (x + 7, 21), (x + 4, 24)], fill=rgb("#d9534a") if (x // 8) % 2 else rgb("#f3ead7"))

    # shelves with glass jars
    d.line([8, 30, 55, 30], fill=wood)
    for x in range(10, 54, 8):
        d.rectangle([x, 25, x + 4, 29], fill=rgb("#cfe3ea"), outline=rgb("#5f7f8c"))
        d.rectangle([x + 1, 27, x + 3, 29], fill=rgb("#d9a441"))
    # counter with kettle and stove
    d.rectangle([4, 40, 59, 55], fill=wood, outline=OUTLINE)
    d.line([5, 41, 58, 41], fill=shade(wood, 1.35))
    for x in range(8, 58, 6):
        d.line([x, 43, x, 54], fill=shade(wood, 0.8))
    d.rectangle([40, 35, 50, 40], fill=rgb("#3a3d46"), outline=OUTLINE)  # stove
    d.line([42, 39, 48, 39], fill=rgb("#ff9a3c"))  # embers
    d.rectangle([42, 29, 48, 35], fill=rgb("#b9bec9"), outline=OUTLINE)  # kettle
    d.line([49, 31, 51, 30], fill=OUTLINE)
    for x in (12, 18, 24):  # glasses
        d.rectangle([x, 36, x + 3, 40], fill=rgb("#e8f1f4"), outline=rgb("#5f7f8c"))
        d.line([x + 1, 38, x + 2, 38], fill=rgb("#b5713a"))
    stone = rgb("#8f867a")
    d.rectangle([0, 56, 63, 59], fill=stone, outline=OUTLINE)
    d.line([1, 57, 62, 57], fill=shade(stone, 1.25))
    return img


def wedding_hall():
    w, h = 128, 76
    img = Image.new("RGBA", (w, h), CLEAR)
    d = ImageDraw.Draw(img)
    wall = rgb("#f1e3c4")
    d.rectangle([2, 16, w - 3, h - 5], fill=wall, outline=OUTLINE)
    d.rectangle([3, 17, w - 4, 22], fill=shade(wall, 0.75))
    d.rectangle([3, h - 12, w - 4, h - 6], fill=rgb("#a8433a"))
    d.rectangle([0, 12, w - 1, 16], fill=shade(wall, 1.08), outline=OUTLINE)
    for x in (10, w - 19):  # little domes on the roof
        d.pieslice([x, 2, x + 9, 20], 180, 360, fill=rgb("#d9a441"), outline=OUTLINE)
    sign(img, "KALYANA MAHAL", [16, 0, w - 17, 12], "#a8433a", "#ffe9a8")

    # arched doorway flanked by banana trees, the way weddings are decorated
    d.rectangle([52, 34, 75, h - 5], fill=rgb("#2a1f2b"), outline=OUTLINE)
    d.pieslice([52, 24, 75, 46], 180, 360, fill=rgb("#2a1f2b"), outline=OUTLINE)
    d.arc([50, 22, 77, 48], 180, 360, fill=rgb("#d9a441"))
    for x0, flip in ((44, 1), (83, -1)):
        d.line([x0, 38, x0, h - 6], fill=rgb("#7fae4a"), width=2)
        for i, dy in enumerate((0, 5, 10)):
            d.line([x0, 38 + dy, x0 - flip * 6, 32 + dy], fill=rgb("#4f9a45"), width=2)
            d.line([x0, 38 + dy, x0 + flip * 5, 34 + dy], fill=rgb("#3c8a44"), width=2)
    for x0 in (12, 28, 90, 106):  # windows
        d.rectangle([x0, 30, x0 + 9, 46], fill=rgb("#141726"), outline=OUTLINE)
        d.pieslice([x0, 25, x0 + 9, 35], 180, 360, fill=rgb("#141726"), outline=OUTLINE)
    for x in range(5, w - 5, 5):  # a string of dead serial lights
        img.putpixel((x, 23 + (x // 5) % 2), rgb("#6b6f7a"))
    stone = rgb("#8f867a")
    d.rectangle([0, h - 4, w - 1, h - 1], fill=stone, outline=OUTLINE)
    d.line([1, h - 3, w - 2, h - 3], fill=shade(stone, 1.25))
    return img


def shrine():
    w, h = 48, 80
    img = Image.new("RGBA", (w, h), CLEAR)
    d = ImageDraw.Draw(img)
    # hall with the red and white stripes of a temple wall
    d.rectangle([2, 50, w - 3, h - 5], fill=rgb("#efe6d2"), outline=OUTLINE)
    for x in range(4, w - 4, 6):
        d.rectangle([x, 51, x + 2, h - 6], fill=rgb("#a8433a"))
    d.rectangle([18, 58, 29, h - 5], fill=rgb("#2a1f2b"), outline=OUTLINE)
    img.putpixel((23, 66), rgb("#ffcf6b"))
    img.putpixel((24, 66), rgb("#ffcf6b"))
    # the gopuram: stacked tiers, each a different colour
    tiers = ["#5b86c9", "#d9739a", "#e9b63a", "#4fa07f", "#d9534a"]
    y = 50
    for i, colour in enumerate(tiers):
        inset = 2 + i * 4
        top = y - 9
        d.rectangle([inset, top, w - 1 - inset, y], fill=rgb(colour), outline=OUTLINE)
        d.line([inset + 1, top + 1, w - 2 - inset, top + 1], fill=shade(rgb(colour), 1.3))
        for x in range(inset + 3, w - 2 - inset, 4):
            img.putpixel((x, top + 5), rgb("#fff6dc"))
        y = top
    d.rectangle([21, y - 6, 26, y], fill=rgb("#e8c66a"), outline=OUTLINE)  # kalasam
    d.rectangle([23, y - 9, 24, y - 6], fill=rgb("#e8c66a"))
    stone = rgb("#8f867a")
    d.rectangle([0, h - 4, w - 1, h - 1], fill=stone, outline=OUTLINE)
    d.line([1, h - 3, w - 2, h - 3], fill=shade(stone, 1.25))
    return img


# ---------------------------------------------------------------- props

def banyan():
    rnd = random.Random(42)
    w, h = 96, 92
    img = Image.new("RGBA", (w, h), CLEAR)
    d = ImageDraw.Draw(img)
    stone = rgb("#9a9184")
    d.ellipse([18, 72, 77, 90], fill=stone)  # the platform people sit on
    d.ellipse([18, 70, 77, 86], fill=shade(stone, 1.18))
    trunk = rgb("#6b4c33")
    d.rectangle([40, 44, 55, 80], fill=trunk)
    for x in (42, 46, 50, 53):
        d.line([x, 44, x + rnd.choice([-1, 0, 1]), 80], fill=shade(trunk, 0.75))
    for x in (30, 35, 60, 66, 71):  # aerial roots
        d.line([x, 40, x, 74 + rnd.randrange(4)], fill=shade(trunk, 0.9))
    canopy = Image.new("RGBA", (w, h), CLEAR)
    cd = ImageDraw.Draw(canopy)
    blobs = [(48, 26, 24), (26, 32, 18), (70, 32, 18), (38, 16, 16), (60, 16, 16), (16, 40, 12), (80, 40, 12),
             (48, 40, 20)]
    for x, y, r in blobs:
        cd.ellipse([x - r, y - r, x + r, y + r], fill=rgb("#2f6b38"))
    cpx = canopy.load()
    for _ in range(520):  # leaf texture
        x, y = rnd.randrange(w), rnd.randrange(h)
        if cpx[x, y][3]:
            cpx[x, y] = rgb(rnd.choice(["#27592f", "#3c8a44", "#3c8a44", "#57a84f"]))
    img.alpha_composite(canopy)
    return outlined(img)


def transformer():
    w, h = 40, 60
    img = Image.new("RGBA", (w, h), CLEAR)
    d = ImageDraw.Draw(img)
    pole = rgb("#6f7683")
    for x in (7, 31):
        d.rectangle([x, 4, x + 2, h - 1], fill=pole)
        d.line([x, 4, x, h - 1], fill=shade(pole, 1.25))
    d.rectangle([4, 8, 36, 10], fill=pole)
    d.rectangle([4, 40, 36, 42], fill=pole)
    for x in (10, 20, 29):  # insulators
        d.rectangle([x, 4, x + 1, 8], fill=rgb("#d9d2c4"))
    box = rgb("#7d8795")
    d.rectangle([11, 18, 29, 40], fill=box)
    for x in range(13, 29, 3):
        d.line([x, 21, x, 37], fill=shade(box, 0.7))
    d.rectangle([15, 25, 25, 33], fill=rgb("#e9c53a"))
    d.polygon([(21, 26), (18, 30), (20, 30), (19, 33), (23, 28), (21, 28)], fill=rgb("#17131f"))
    return outlined(img)


def barricade():
    img = Image.new("RGBA", (34, 20), CLEAR)
    d = ImageDraw.Draw(img)
    for x in (3, 28):
        d.rectangle([x, 2, x + 2, 19], fill=rgb("#8b6c4c"))
    d.rectangle([0, 4, 33, 10], fill=rgb("#f3ead7"))
    for x in range(-4, 34, 8):
        d.polygon([(x, 10), (x + 4, 4), (x + 8, 4), (x + 4, 10)], fill=rgb("#d9534a"))
    d.rectangle([0, 13, 33, 15], fill=rgb("#f3ead7"))
    return outlined(img.crop((0, 0, 34, 20)))


def well():
    img = Image.new("RGBA", (26, 22), CLEAR)
    d = ImageDraw.Draw(img)
    stone = rgb("#9a9184")
    d.rectangle([1, 8, 24, 20], fill=shade(stone, 0.85))
    d.ellipse([1, 14, 24, 21], fill=shade(stone, 0.85))
    for y in (12, 16):
        d.line([2, y, 23, y], fill=shade(stone, 0.65))
    d.ellipse([1, 1, 24, 14], fill=shade(stone, 1.15))
    d.ellipse([4, 3, 21, 12], fill=rgb("#16233b"))
    d.arc([6, 6, 18, 11], 200, 320, fill=rgb("#3f6f9a"))
    return outlined(img)


def stumps():
    img = Image.new("RGBA", (9, 15), CLEAR)
    d = ImageDraw.Draw(img)
    for x in (1, 4, 7):
        d.line([x, 2, x, 14], fill=rgb("#e8d9a8"))
    d.line([0, 1, 8, 1], fill=rgb("#c9a45a"))
    return outlined(img)


def cycle():
    img = Image.new("RGBA", (28, 18), CLEAR)
    d = ImageDraw.Draw(img)
    for cx in (6, 21):
        d.ellipse([cx - 5, 7, cx + 5, 17], outline=rgb("#2a2630"))
        d.ellipse([cx - 4, 8, cx + 4, 16], outline=rgb("#8b93a1"))
    frame = rgb("#3fae8f")
    d.line([6, 12, 12, 5], fill=frame)
    d.line([12, 5, 20, 5], fill=frame)
    d.line([12, 5, 14, 12], fill=frame)
    d.line([14, 12, 6, 12], fill=frame)
    d.line([14, 12, 20, 5], fill=frame)
    d.line([20, 5, 21, 12], fill=frame)
    d.line([19, 2, 22, 2], fill=rgb("#2a2630"))
    d.line([20, 2, 20, 5], fill=rgb("#2a2630"))
    d.rectangle([10, 3, 13, 4], fill=rgb("#5b3a24"))
    d.rectangle([1, 4, 6, 8], fill=rgb("#8b6c4c"))  # carrier box
    return img


def make_props():
    save(shop(800, "#f0c75e", "#3b5d8f", "STORES"), "Props", "shop_stores.png")
    save(shop(801, "#6fc2b0", "#7d4bb0", "TAILOR", "#9a8f86"), "Props", "shop_tailor.png")
    save(shop(802, "#e58fa6", "#2f7a55", "MEDICALS"), "Props", "shop_medical.png")
    save(shop(803, "#c9cdd6", "#c79a1e", "EB", "#6f7683"), "Props", "eb_office.png")
    save(tea_stall(), "Props", "tea_stall.png")
    save(wedding_hall(), "Props", "wedding_hall.png")
    save(shrine(), "Props", "shrine.png")
    save(banyan(), "Props", "banyan.png")
    save(transformer(), "Props", "transformer.png")
    save(barricade(), "Props", "barricade.png")
    save(well(), "Props", "well.png")
    save(stumps(), "Props", "stumps.png")
    save(cycle(), "Props", "cycle.png")


# ---------------------------------------------------------------- people and the dog

ADULT_LOWER = [
    "...oTttttttTo...",
    "..osTttttttTso..",
    "..osTttttttTso..",
    "..osTttttttTso..",
    "...oTttttttTo...",
    "...oPppppppPo...",
    "...oPppppppPo...",
    "...oPppppppPo...",
    "...oPppppppPo...",
    "...oPppppppPo...",
    "...oPppppppPo...",
    "...oPppppppPo...",
    "...oPqqqqqqPo...",
    "...oPppppppPo...",
    "....obboobbo....",
    ".....oo..oo.....",
]


def adult(hair, skin, top, bottom, hem=None, hat=None, moustache=False, bun=False):
    hair, skin, top, bottom = rgb(hair), rgb(skin), rgb(top), rgb(bottom)
    crown = rgb(hat) if hat else hair
    pal = {
        ".": CLEAR, "o": rgb("#17131f"), "H": shade(crown, 1.25 if hat else 1.35), "h": hair,
        "s": skin, "d": shade(skin, 0.82), "e": rgb("#17131f"), "m": hair if moustache else shade(skin, 0.7),
        "t": top, "T": shade(top, 0.78), "p": bottom, "P": shade(bottom, 0.8),
        "q": rgb(hem) if hem else bottom, "b": rgb("#6b4c33"),
    }
    rows = BODY[:11] + ADULT_LOWER
    if hat:  # a hard hat covers the top of the head
        rows = rows[:4] + [rows[4].replace("h", "H")] + rows[5:]
    img = Image.new("RGBA", (16, len(rows)), CLEAR)
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            px[x, y] = pal[ch]
    if moustache:
        for x in (5, 6, 9, 10):
            px[x, 9] = hair
    if bun:
        for x, y in ((7, 0), (8, 0)):
            px[x, y] = hair
        px[6, 0] = px[9, 0] = pal["o"]
    return img


def dog(pose):
    img = Image.new("RGBA", (18, 12), CLEAR)
    d = ImageDraw.Draw(img)
    fur, dark = rgb("#c98b4f"), rgb("#8f5c30")
    d.rectangle([3, 4, 12, 8], fill=fur)  # body
    d.rectangle([4, 7, 11, 8], fill=rgb("#ecd9b8"))  # belly
    d.rectangle([11, 1, 15, 6], fill=fur)  # head
    d.rectangle([15, 4, 16, 5], fill=rgb("#ecd9b8"))  # snout
    img.putpixel((16, 4), rgb("#17131f"))
    img.putpixel((14, 3), rgb("#17131f"))
    d.rectangle([11, 0, 12, 2], fill=dark)  # ear
    d.line([2, 2, 3, 4], fill=fur)  # tail
    img.putpixel((1, 1), fur)
    legs = {"idle": [(4, 3), (6, 3), (10, 3), (12, 3)], "walk1": [(3, 3), (7, 2), (9, 3), (13, 2)],
            "walk2": [(5, 2), (6, 3), (11, 2), (12, 3)]}[pose]
    for i, (x, length) in enumerate(legs):
        d.line([x, 9, x, 8 + length], fill=dark if i % 2 else fur)
    d.rectangle([9, 3, 10, 4], fill=rgb("#d9534a"))  # collar
    return outlined(img)


def make_characters():
    save(adult("#e9e9ee", "#b9774d", "#8a3f7a", "#8a3f7a", hem="#e8c66a", bun=True), "Characters", "paati.png")
    save(adult("#2a2233", "#a86a45", "#b89a5e", "#8f7a4a", hat="#e9c53a", moustache=True), "Characters", "lineman.png")
    save(adult("#2a2233", "#c98b5e", "#f3ead7", "#4a6fa5", hem="#f3ead7", moustache=True), "Characters", "teamaster.png")
    save(adult("#55505f", "#d39c6c", "#ffffff", "#f6f1e4", hem="#e8c66a", moustache=True), "Characters", "chairman.png")
    for pose in ("idle", "walk1", "walk2"):
        save(dog(pose), "Characters", f"dog_{pose}.png")


# ---------------------------------------------------------------- UI

def font_atlas():
    """ASCII 32..127 in a 16x6 grid. The game measures each glyph's width itself."""
    cw, ch = 12, 16
    font = ImageFont.truetype(TEXT_FONT, 10)
    mask = Image.new("1", (cw * 16, ch * 6), 0)
    d = ImageDraw.Draw(mask)
    for code in range(32, 127):
        col, row = (code - 32) % 16, (code - 32) // 16
        d.text((col * cw, row * ch + 1), chr(code), font=font, fill=1)
    img = Image.new("RGBA", mask.size, CLEAR)
    img.paste(Image.new("RGBA", mask.size, (255, 255, 255, 255)), (0, 0), mask)
    return img


def logo():
    font = ImageFont.truetype(LOGO_FONT, 30)
    lines = ["CURRENT", "POCHU!"]
    w, h = 190, 78
    mask = Image.new("1", (w, h), 0)
    d = ImageDraw.Draw(mask)
    y = 0
    for line in lines:
        box = font.getbbox(line)
        d.text(((w - box[2]) // 2, y), line, font=font, fill=1)
        y += 34
    m = mask.load()
    face = Image.new("RGBA", (w, h), CLEAR)
    px = face.load()
    for yy in range(h):
        band = (yy % 34) / 34.0  # each line fades from pale yellow to orange
        colour = (255, int(236 - 96 * band), int(120 - 80 * band), 255)
        for xx in range(w):
            if m[xx, yy]:
                px[xx, yy] = colour
    out = Image.new("RGBA", (w + 6, h + 8), CLEAR)
    edge = outlined(outlined(face, rgb("#3a1d12")), rgb("#17131f"))
    shadow = Image.new("RGBA", edge.size, CLEAR)
    shadow.paste(Image.new("RGBA", edge.size, rgb("#0a0c18", 200)), (0, 0), edge)
    out.alpha_composite(shadow, (1, 4))
    out.alpha_composite(edge, (1, 1))
    return out.crop(out.getbbox())


def nine_slice(fill, border, light=None, dark=None):
    img = Image.new("RGBA", (16, 16), CLEAR)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, 15, 15], radius=3, fill=rgb(border))
    d.rounded_rectangle([1, 1, 14, 14], radius=2, fill=fill if isinstance(fill, tuple) else rgb(fill))
    if light:
        d.line([3, 2, 12, 2], fill=rgb(light))
    if dark:
        d.rectangle([2, 12, 13, 13], fill=rgb(dark))
    return img


def bulb(lit):
    img = Image.new("RGBA", (9, 11), CLEAR)
    d = ImageDraw.Draw(img)
    d.ellipse([0, 0, 8, 8], fill=rgb("#ffe27a") if lit else rgb("#5a6078"))
    if lit:
        d.ellipse([2, 1, 4, 3], fill=rgb("#fff8d6"))
    d.rectangle([3, 8, 5, 10], fill=rgb("#aab0c0"))
    return outlined(img)


def make_ui():
    ui = ("..", "Resources", "UI")
    save(font_atlas(), *ui, "font.png")
    save(logo(), *ui, "logo.png")
    save(nine_slice("#f2b33d", "#17131f", light="#ffe08a", dark="#c9862a"), *ui, "button_9s.png")
    save(nine_slice(rgb("#11162e", 235), "#4a5aa8"), *ui, "panel_9s.png")
    save(nine_slice(rgb("#070a18", 255), "#2c376e"), *ui, "field_9s.png")
    save(bulb(True), *ui, "bulb_on.png")
    save(bulb(False), *ui, "bulb_off.png")


# ---------------------------------------------------------------- quest art

def goat(pose):
    img = Image.new("RGBA", (20, 14), CLEAR)
    d = ImageDraw.Draw(img)
    coat, patch = rgb("#efe9dc"), rgb("#4a4038")
    d.rectangle([3, 5, 13, 9], fill=coat)
    d.rectangle([5, 5, 8, 7], fill=patch)
    d.rectangle([12, 2, 16, 6], fill=coat)  # head
    d.rectangle([16, 4, 17, 6], fill=rgb("#d9cdb8"))
    img.putpixel((15, 3), rgb("#17131f"))
    d.line([12, 0, 13, 2], fill=rgb("#8f867a"))  # horns
    d.line([14, 0, 14, 2], fill=rgb("#8f867a"))
    d.line([16, 7, 16, 8], fill=coat)  # beard
    d.line([2, 4, 3, 5], fill=coat)  # tail
    legs = {"idle": [(4, 3), (6, 3), (11, 3), (13, 3)], "walk1": [(3, 3), (7, 2), (10, 3), (14, 2)],
            "walk2": [(5, 2), (6, 3), (12, 2), (13, 3)]}[pose]
    for i, (x, length) in enumerate(legs):
        d.line([x, 10, x, 9 + length], fill=patch if i % 2 else coat)
    return outlined(img)


def bandicoot(frame):
    img = Image.new("RGBA", (16, 8), CLEAR)
    d = ImageDraw.Draw(img)
    fur = rgb("#4b4652")
    d.ellipse([3, 1, 12, 6], fill=fur)
    d.rectangle([11, 3, 14, 5], fill=fur)
    img.putpixel((15, 4), rgb("#d98c8c"))  # nose
    img.putpixel((12, 3), rgb("#ff5a4a"))  # eye
    d.rectangle([10, 0, 11, 1], fill=shade(fur, 1.4))  # ear
    d.line([0, 4 if frame else 2, 3, 4], fill=rgb("#b98d8d"))  # tail
    for x in ((4, 9) if frame else (5, 10)):
        d.line([x, 6, x, 7], fill=shade(fur, 0.7))
    return outlined(img)


def small(pixels, palette):
    img = Image.new("RGBA", (len(pixels[0]), len(pixels)), CLEAR)
    for y, row in enumerate(pixels):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), rgb(palette[ch]))
    return outlined(img)


def ice_cart():
    img = Image.new("RGBA", (34, 26), CLEAR)
    d = ImageDraw.Draw(img)
    box = rgb("#5b9bd5")
    d.rectangle([2, 2, 31, 17], fill=box)
    d.rectangle([2, 2, 31, 4], fill=shade(box, 1.3))
    d.rectangle([2, 14, 31, 17], fill=shade(box, 0.75))
    label = pixel_text("ICE", 9, rgb("#ffffff"))
    img.alpha_composite(label, (17 - label.width // 2, 6))
    for cx in (8, 25):
        d.ellipse([cx - 4, 16, cx + 4, 24], fill=rgb("#2a2630"))
        d.ellipse([cx - 2, 18, cx + 2, 22], fill=rgb("#8b93a1"))
    return outlined(img)


def pen():
    img = Image.new("RGBA", (40, 18), CLEAR)
    d = ImageDraw.Draw(img)
    wood = rgb("#8b6c4c")
    for x in range(1, 40, 6):
        d.rectangle([x, 2, x + 1, 17], fill=wood)
        img.putpixel((x, 2), shade(wood, 1.3))
    d.rectangle([0, 6, 39, 7], fill=shade(wood, 1.15))
    d.rectangle([0, 12, 39, 13], fill=shade(wood, 0.85))
    return outlined(img)


def make_quest_art():
    for pose in ("idle", "walk1", "walk2"):
        save(goat(pose), "Characters", f"goat_{pose}.png")
    save(bandicoot(0), "Characters", "bandicoot_1.png")
    save(bandicoot(1), "Characters", "bandicoot_2.png")
    save(ice_cart(), "Props", "ice_cart.png")
    save(pen(), "Props", "pen.png")
    save(small(["bbb.bbb", "bgb.bgb", "bbbbbbb"], {"b": "#3a2a1c", "g": "#cfe3ea"}), "Items", "glasses.png")
    save(small(["..gG.", ".gGGg", "gGGg.", "GGg..", "s...."], {"g": "#57a84f", "G": "#3c8a44", "s": "#2c6a37"}), "Items", "leaf.png")
    save(small(["wiiiw", "iiwii", "iwiii", "iiiiw", "wiiii"], {"i": "#a8d8f0", "w": "#f2fbff"}), "Items", "ice.png")
    save(small([".rr.", "rwrr", "rrwr", ".rr."], {"r": "#c0392b", "w": "#f3ead7"}), "Items", "ball.png")
    save(small(["mmmm", "cccc", "cyyc", "cccc", "cyyc", "cccc", "mmmm"], {"m": "#aab0c0", "c": "#f3ead7", "y": "#e9b63a"}), "Items", "fuse.png")


def main():
    make_tiles()
    make_props()
    make_characters()
    make_ui()
    make_quest_art()
