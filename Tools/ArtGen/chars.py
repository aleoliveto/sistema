"""
People, painted part by part with the props' painter, in the look of the painted character art: seen from
above and a little from the front (the figure faces south, down the image; the game turns it), soft light
from the north-west, no hard outlines, fabric with folds, real-looking skin and hair.

Every part is on a 256 px canvas at one scale (the body ~200 px across), so they stack exactly:
  torso  - shoulders, chest, belly, the trousers below; centred on the canvas
  arm    - one arm hanging from the shoulder (sleeve, forearm, fist); its own 96x192 canvas, pivot at the
           shoulder (ARM_PX, ARM_PY); the left arm, flipped for the right; sits at SHOULDER_X / SHOULDER_Y
  head   - the top of the head, ears, the tip of the nose at the front
  hair   - over the head, same canvas
  shoe   - one shoe; its own canvas, toe at the bottom
Run: python3 chars.py <out> [--preview]
"""
import os
import sys
import numpy as np
from PIL import Image
from paint import Canvas, fbm, smooth, grime

C = lambda *v: np.array(v, np.float32) / 255

SKINS = [C(226, 184, 150), C(212, 164, 126), C(186, 136, 100), C(150, 104, 74), C(236, 200, 170)]
HAIRS = [C(34, 26, 22), C(62, 42, 28), C(98, 70, 46), C(140, 136, 130)]

# Where the arms hang from, on the torso canvas (left arm; the right is mirrored).
SHOULDER_X, SHOULDER_Y = 128 - 84, 134
ARM_W, ARM_H, ARM_PX, ARM_PY = 96, 192, 48, 24
LINE = 0.08   # the painted art has no ink lines: only the faintest edge


def folds(c, seed, along_y=True, strength=1.0):
    """Fabric: long soft folds, a fine weave, a little wear."""
    x, y = (c.x, c.y) if along_y else (c.y, c.x)
    warp = fbm(x, y, 40, 3, seed) * 6
    long_folds = np.sin((x * 0.09 + warp)) * 0.5 + 0.5
    weave = fbm(x * 3, y * 3, 2.5, 2, seed + 7)
    t = 0.86 + 0.12 * long_folds * strength + 0.06 * (weave - 0.5) + 0.08 * (fbm(x * 0.7, y * 1.6, 14, 3, seed + 3) - 0.5) * strength
    return t * grime(c, 0.08, 20, seed + 5)


def skin_tex(c, seed):
    return 0.95 + 0.07 * fbm(c.x, c.y, 9, 3, seed) + 0.03 * (fbm(c.x * 4, c.y * 4, 2, 2, seed + 1) - 0.5)


def union(*ds):
    out = ds[0]
    for d in ds[1:]:
        out = np.minimum(out, d)
    return out


def poly(c, pts):
    from ui import poly as p
    return p(c, pts)


# ---------- torso ----------

OUTFITS = [
    # name, colour, sleeve colour (None: bare arms), style, trousers
    ("tee_white", C(226, 224, 218), C(226, 224, 218), "tee", C(52, 54, 62)),
    ("tee_black", C(38, 38, 42), C(38, 38, 42), "tee", C(60, 66, 84)),
    ("napoli", C(36, 92, 176), C(36, 92, 176), "football", C(230, 230, 230)),
    ("track_black", C(30, 30, 34), C(30, 30, 34), "track", C(30, 30, 34)),
    ("leather", C(56, 42, 34), C(56, 42, 34), "jacket", C(44, 48, 60)),
    ("vest", C(232, 230, 222), None, "vest", C(56, 64, 86)),
    ("shirt", C(232, 232, 230), C(232, 232, 230), "shirt", C(40, 40, 46)),
    ("hoodie", C(110, 110, 116), C(110, 110, 116), "hoodie", C(46, 50, 62)),
    ("suit", C(40, 40, 48), C(40, 40, 48), "suit", C(40, 40, 48)),
    ("police", C(30, 40, 70), C(30, 40, 70), "police", C(26, 32, 52)),
    ("blouse", C(176, 64, 74), C(176, 64, 74), "blouse", C(40, 40, 46)),
]

LONG_SLEEVES = {"track", "jacket", "shirt", "hoodie", "suit", "police"}


def torso(colour, style, trousers, seed, narrow=False):
    c = Canvas(256, 256)
    cx = 128
    k = 0.88 if narrow else 1.0
    # The trousers below the shirt: the figure is seen a little from the front.
    legs = c.box(cx, 222, 116 * k, 52, 22)
    crease = 1 - 0.18 * smooth(6, 0, np.abs(c.x - cx)) * smooth(204, 236, c.y)   # between the legs
    c.fill(legs, trousers, z0=0, height=5, profile="pillow", bevel=18, tex=folds(c, seed + 11, strength=0.7) * crease, gloss=0.0, line=LINE)
    # Shoulders across, chest and belly coming forward.
    shoulders = c.ellipse(cx, 126, 98 * k, 40)
    chest = c.ellipse(cx, 162, 86 * k, 58)
    body = union(shoulders, chest, c.box(cx, 142, 160 * k, 52, 26))
    if style not in ("vest",):
        # Sleeves (short or long) carry the shoulder line down the sides.
        body = union(body, c.ellipse(cx - 82 * k, 150, 24, 38, ang=8), c.ellipse(cx + 82 * k, 150, 24, 38, ang=-8))
    c.fill(body, colour, z0=4, height=13, profile="pillow", bevel=52, tex=folds(c, seed), gloss=0.08 if style in ("jacket", "police") else 0.0, line=LINE)
    # The hem of the shirt over the trousers.
    c.fill(c.box(cx, 206, 140 * k, 6, 3), colour * 0.82, z0=6, height=2, profile="bevel", bevel=3, line=0.0, clip=body)
    # The sleeve's hem: a fold where it ends on the arm.
    if style not in ("vest",) and style not in LONG_SLEEVES:
        for s in (-1, 1):
            c.fill(c.ellipse(cx + s * 84 * k, 182, 20, 5, ang=-s * 10), colour * 0.8, z0=16, height=1, profile="flat", line=0.0, clip=body)
    neckband = c.ellipse(cx, 116, 42, 30)
    if style == "football":
        c.fill(neckband, C(236, 236, 236), z0=17, height=1, profile="flat", line=0.0, clip=body)
        for s in (-1, 1):
            band = c.box(cx + s * 60, 124, 70, 7, 3, ang=-s * 22)
            c.fill(band, C(236, 236, 236), z0=17, height=0.5, profile="flat", line=0.0, clip=body)
    elif style == "track":
        for s in (-1, 1):
            c.fill(c.box(cx + s * 78 * k, 150, 6, 90, 2, ang=s * 6), C(232, 232, 232), z0=17, height=0.5, profile="flat", line=0.0, clip=body)
        c.fill(c.box(cx, 168, 3, 80), C(170, 170, 170), z0=17, height=0.5, profile="flat", line=0.0, clip=body)
    elif style == "jacket":
        c.fill(poly(c, [(cx - 24, 112), (cx + 24, 112), (cx + 14, 200), (cx - 14, 200)]), C(34, 32, 34), z0=17, height=1, profile="flat", line=0.0, clip=body)
        for s in (-1, 1):
            c.fill(c.box(cx + s * 24, 140, 16, 56, 5, ang=s * 14), colour * 0.86, z0=18, height=3, profile="bevel", bevel=3, tex=folds(c, seed + 9), gloss=0.25, line=LINE)
    elif style == "shirt":
        c.fill(c.box(cx, 170, 3, 70), C(196, 196, 196), z0=17, height=0.5, profile="flat", line=0.0, clip=body)
        for s in (-1, 1):
            c.fill(c.box(cx + s * 16, 108, 26, 14, 4, ang=s * 28), colour, z0=18, height=3, profile="bevel", bevel=3, tex=folds(c, seed + 9), line=LINE)
    elif style == "hoodie":
        c.fill(c.ellipse(cx, 98, 56, 26), colour * 0.88, z0=16, height=6, profile="pillow", bevel=14, tex=folds(c, seed + 9), line=LINE)
        c.fill(c.box(cx, 184, 74, 30, 10), colour * 0.93, z0=17, height=2, profile="bevel", bevel=4, tex=folds(c, seed + 10), line=LINE, clip=body)
    elif style == "suit":
        c.fill(poly(c, [(cx - 26, 112), (cx + 26, 112), (cx, 196)]), C(232, 232, 232), z0=17, height=1, profile="flat", line=0.0, clip=body)
        c.fill(c.box(cx, 152, 9, 62, 3), C(120, 26, 32), z0=18, height=2, profile="bevel", bevel=2, line=LINE)
    elif style == "police":
        for s in (-1, 1):
            c.fill(c.box(cx + s * 62, 112, 40, 11, 3, ang=-s * 8), colour * 1.35, z0=18, height=3, profile="bevel", bevel=2, line=LINE)
            c.fill(c.box(cx + s * 34, 150, 28, 22, 3), colour * 0.88, z0=17, height=2, profile="bevel", bevel=2, line=LINE, clip=body)   # pockets
        c.fill(c.box(cx, 200, 150, 12, 3), C(24, 22, 22), z0=18, height=3, profile="bevel", bevel=2, gloss=0.4, line=LINE)   # the belt
        c.fill(c.box(cx - 40, 130, 12, 16, 2), C(204, 172, 84), z0=19, height=2, profile="bevel", bevel=1, gloss=0.9, line=LINE)
    elif style == "vest":
        skin = SKINS[1]
        c.fill(body, skin, z0=4, height=13, profile="pillow", bevel=52, tex=skin_tex(c, seed), gloss=0.05, line=LINE)
        c.fill(c.ellipse(cx, 168, 62 * k, 52), colour, z0=10, height=10, profile="pillow", bevel=30, tex=folds(c, seed), clip=body, line=LINE)
        for s in (-1, 1):
            c.fill(c.box(cx + s * 30, 124, 12, 40, 4), colour, z0=26, height=4, profile="bevel", bevel=4, tex=folds(c, seed + 3), line=LINE, clip=body)
    elif style == "blouse":
        c.fill(c.ellipse(cx, 114, 36, 18), C(244, 240, 236), z0=17, height=2, profile="bevel", bevel=3, line=LINE)
    else:
        c.fill(cut(neckband, c.ellipse(cx, 116, 36, 25)), colour * 0.86, z0=17, height=1.5, profile="bevel", bevel=2, line=0.0)
    return c.render()


def cut(a, b):
    return np.maximum(a, -b)


# ---------- arm: hangs from the shoulder, a little forward ----------

def arm(sleeve, skin, long_sleeve, seed, bare=False):
    c = Canvas(ARM_W, ARM_H)
    x, y = ARM_PX, ARM_PY
    st = skin_tex(c, seed)
    # Shoulder cap, upper arm, the forearm a little thicker below the elbow, the wrist, a loose fist.
    upper = union(c.circle(x, y + 6, 18), c.box(x, y + 40, 32, 60, 14))
    fore = union(c.ellipse(x - 1, y + 88, 17, 22), c.box(x - 2, y + 112, 22, 32, 9))
    fist = union(c.ellipse(x - 3, y + 136, 14, 13), c.ellipse(x + 8, y + 128, 5, 8, ang=-25))   # the thumb, towards the body
    shade = 0.92 + 0.12 * smooth(-14, 14, x - c.x)   # the inside of the arm in a little shade
    if long_sleeve:
        c.fill(union(upper, fore), sleeve, z0=0, height=8, profile="pillow", bevel=16, tex=folds(c, seed, strength=1.3) * shade, gloss=0.0, line=LINE)
        c.fill(c.box(x - 2, y + 124, 24, 8, 4), sleeve * 0.8, z0=6, height=2, profile="bevel", bevel=3, line=LINE)   # cuff
    else:
        c.fill(union(upper, fore), skin, z0=0, height=8, profile="pillow", bevel=16, tex=st * shade, gloss=0.05, line=LINE)
    c.fill(fist, skin * 0.97, z0=4, height=7, profile="pillow", bevel=10, tex=st, gloss=0.06, line=LINE)
    # The fingers folded over: three soft creases across the front of the fist.
    for i in range(3):
        c.fill(c.box(x - 3, y + 140 + i * 0.1 + (i - 1) * 4.5, 22, 0.8), skin * 0.8, z0=11, height=0, profile="flat", line=0.0, clip=fist)
    return c.render()


# ---------- head and hair ----------

def head(skin, seed):
    c = Canvas(256, 256)
    cx, cy = 128, 124
    st = skin_tex(c, seed)
    for s in (-1, 1):
        c.fill(c.ellipse(cx + s * 50, cy + 10, 9, 16), skin * 0.93, z0=0, height=8, profile="pillow", bevel=8, tex=st, gloss=0.1, line=LINE)
    c.fill(c.ellipse(cx, cy, 46, 52), skin, z0=0, height=16, profile="dome", bevel=50, tex=st, gloss=0.06, line=LINE)
    c.fill(c.ellipse(cx, cy + 50, 7, 6), skin * 0.97, z0=12, height=2.5, profile="dome", bevel=6, tex=st, gloss=0.05, line=0.0)
    return c.render()


HAIRSTYLES = ["buzz", "crop", "slick", "curly", "balding", "long", "ponytail", "bald"]


def strands(c, cx, cy, seed, fine=1.0):
    """Hair growing out from the crown: fine streaks along the direction away from it."""
    dx, dy = c.x - cx, c.y - cy
    ang = np.arctan2(dy, dx)
    r = np.hypot(dx, dy)
    s1 = fbm(ang * 60 * fine, r * 0.25, 1.0, 3, seed)
    s2 = fbm(ang * 140 * fine, r * 0.5, 1.0, 2, seed + 3)
    return 0.45 + 0.75 * s1 + 0.3 * (s2 - 0.5)


def hair(style, colour, seed):
    c = Canvas(256, 256)
    cx, cy = 128, 124
    if style == "bald":
        return c.render()
    crown = (cx, cy - 18)
    if style == "buzz":
        # Stubble: very fine speckle, the scalp showing through at the edge.
        speck = fbm(c.x * 6, c.y * 6, 1.0, 2, seed)
        shape = c.ellipse(cx, cy - 8, 45, 46)
        edge = smooth(0, 14, -shape)          # thinner at the hairline
        cov = np.clip((0.35 + 0.65 * edge) * (0.5 + speck), 0, 1)
        col = C(150, 112, 90)[None, None, :] * (1 - cov[..., None]) + colour[None, None, :] * cov[..., None]
        c.fill(shape, col, z0=16, height=1.5, profile="dome", bevel=40, tex=0.85 + 0.3 * speck, gloss=0.0, line=0.0)
    elif style == "crop":
        c.fill(c.ellipse(cx, cy - 10, 47, 46), colour, z0=16, height=4, profile="dome", bevel=40, tex=strands(c, *crown, seed), gloss=0.1, line=LINE)
    elif style == "slick":
        comb = 0.6 + 0.5 * fbm(c.x * 1.6, c.y * 0.12, 1.0, 3, seed)     # combed straight back
        c.fill(c.ellipse(cx, cy - 10, 47, 46), colour, z0=16, height=4, profile="dome", bevel=40, tex=comb, gloss=0.35, line=LINE)
    elif style == "curly":
        curls = 0.6 + 0.45 * np.abs(np.sin(fbm(c.x, c.y, 3.5, 3, seed) * 26))
        lumps = c.ellipse(cx, cy - 10, 50, 48) - 5 * fbm(c.x, c.y, 5, 2, seed + 2)
        c.fill(lumps, colour, z0=16, height=6, profile="dome", bevel=24, tex=curls, gloss=0.08, line=LINE)
    elif style == "balding":
        ring = cut(c.ellipse(cx, cy - 2, 50, 54), c.ellipse(cx, cy - 18, 30, 36))
        speck = 0.55 + 0.7 * fbm(c.x * 5, c.y * 5, 1.2, 2, seed)
        c.fill(ring, colour, z0=16, height=3, profile="dome", bevel=14, tex=speck, gloss=0.05, line=0.0)
    elif style in ("long", "ponytail"):
        back = cy - 50
        hair_mass = c.ellipse(cx, cy - 8, 50, 50)
        c.fill(hair_mass, colour, z0=16, height=5, profile="dome", bevel=40, tex=strands(c, cx, cy + 30, seed, 0.8), gloss=0.2, line=LINE)
        if style == "ponytail":
            tail = union(c.ellipse(cx, back - 18, 22, 30), c.ellipse(cx + 6, back - 44, 16, 22))
            flow = 0.6 + 0.45 * fbm(c.x * 2.2, c.y * 0.25, 1.0, 3, seed + 4)
            c.fill(tail, colour, z0=20, height=10, profile="pillow", bevel=14, tex=flow, gloss=0.35, line=LINE)
            c.fill(c.box(cx, back + 4, 22, 7, 3), C(20, 20, 20), z0=24, height=3, profile="bevel", bevel=2, line=LINE)
        else:
            flow = 0.6 + 0.45 * fbm(c.x * 2.2, c.y * 0.25, 1.0, 3, seed + 4)
            c.fill(c.box(cx, cy - 46, 120, 70, 30), colour, z0=16, height=6, profile="pillow", bevel=20, tex=flow, gloss=0.3, line=LINE)
    return c.render()


# ---------- shoe ----------

def shoe(colour, seed, sole=C(210, 206, 196)):
    c = Canvas(64, 96)
    c.fill(c.ellipse(32, 50, 18, 34), sole, z0=0, height=3, profile="bevel", bevel=3, line=LINE)
    c.fill(c.ellipse(32, 48, 16, 31), colour, z0=2, height=12, profile="pillow", bevel=14, gloss=0.35, tex=0.95 + 0.06 * fbm(c.x, c.y, 4, 2, seed), line=LINE)
    c.fill(c.ellipse(32, 34, 9, 11), colour * 0.55, z0=13, height=0, profile="flat", line=0.0)   # the opening, under the trousers
    return c.render()


# ---------- assembly, for the preview ----------

def assemble(parts, phase):
    """A whole figure at a point in the walk: shoes stepping, arms swinging, on one 256 canvas."""
    t, a, hd, hr, sh = parts
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    step = int(22 * phase)
    img.alpha_composite(sh, (128 - 26 - 32, 222 - step - 48))
    img.alpha_composite(sh.transpose(Image.FLIP_LEFT_RIGHT), (128 + 26 - 32, 222 + step - 48))
    for side, k in ((-1, phase), (1, -phase)):
        h = int(ARM_H * (1 + 0.22 * k))
        a2 = a.resize((ARM_W, h), Image.LANCZOS)
        px = ARM_PX
        if side > 0:
            a2 = a2.transpose(Image.FLIP_LEFT_RIGHT)
            px = ARM_W - ARM_PX
        img.alpha_composite(a2, (128 + side * (128 - SHOULDER_X) - px, SHOULDER_Y - int(ARM_PY * (1 + 0.22 * k))))
    img.alpha_composite(t)
    img.alpha_composite(hd)
    img.alpha_composite(hr)
    return img


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    picks = [(0, 1, "buzz", 0), (2, 2, "balding", 1), (4, 1, "slick", 0), (9, 0, "crop", 1), (7, 3, "curly", 0), (10, 4, "ponytail", 2)]
    rows = []
    for oi, si, hs, hc in picks:
        name, col, sleeve, style, trousers = OUTFITS[oi]
        skin = SKINS[si]
        t = torso(col, style, trousers, oi, narrow=style == "blouse")
        a = arm(sleeve, skin, style in LONG_SLEEVES, oi)
        parts = (t, a, head(skin, si), hair(hs, HAIRS[hc], oi), shoe(C(28, 24, 22), 1))
        rows.append([assemble(parts, p) for p in (-1, 0, 1)])
    sheet = Image.new("RGBA", (3 * 200 + 20, len(rows) * 200 + 20), (96, 92, 86, 255))
    for r, frames in enumerate(rows):
        for k, f in enumerate(frames):
            sheet.alpha_composite(f.resize((190, 190), Image.LANCZOS), (10 + k * 200, 10 + r * 200))
    sheet.save(os.path.join(out, "chars_preview.png"))
    print("preview written")
