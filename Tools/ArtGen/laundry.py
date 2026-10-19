"""
Washing hung out across the vicoli: shirts, vests, trousers, jeans, towels, sheets, a dress, socks,
a child's top, an apron. Seen hanging from the line (pegs at the top), folds catching the
north-west sun, at 128 px per cell. Run: python3 laundry.py <out>   (Resources/Laundry)
"""
import os
import sys
import numpy as np
from paint import Canvas, fbm, smooth

C = lambda *v: np.array(v, np.float32) / 255


def folds(c, k=0.7, seed=0):
    """Vertical folds where the cloth hangs, a little crumple."""
    return 0.82 + 0.18 * np.sin(c.x * k + fbm(c.x, c.y, 12, 2, seed) * 1.4) ** 2


def pegs(c, xs, top):
    for px in xs:
        c.fill(c.box(px, top + 1, 3.2, 7, 1), C(214, 196, 150), z0=8, height=2, bevel=1, gloss=0.3, line=0.4)


def garment(kind, colour, seed):
    col = C(*colour)
    if kind == "shirt":
        c = Canvas(56, 58)
        body = c.box(28, 33, 32, 46, 3)
        c.fill(body, col, z0=2, height=4, profile="pillow", bevel=4, tex=folds(c, 0.6, seed))
        for sx, ang in ((10, -24), (46, 24)):
            c.fill(c.box(sx, 22, 11, 26, 3, ang), col * 0.94, z0=2, height=3, profile="pillow", bevel=3, tex=folds(c, 0.8, seed + 1))
        c.fill(c.box(28, 12, 14, 5, 2), col * 0.85, z0=6, height=1, line=0.3)                       # collar
        for by in (20, 30, 40, 50):
            c.fill(c.circle(28, by, 1.2), C(230, 230, 224), z0=6, height=0.6, profile="dome", bevel=1.2)
        pegs(c, (14, 42), 9)
    elif kind == "vest":
        c = Canvas(44, 54)
        c.fill(c.box(22, 30, 30, 44, 4), col, z0=2, height=4, profile="pillow", bevel=4, tex=folds(c, 0.9, seed))
        c.fill(c.ellipse(22, 9, 9, 5), C(0, 0, 0), z0=0, height=0, profile="flat")                   # neck cut
        c.a = np.where(np.hypot((c.x - 22) / 9, (c.y - 9) / 5) < 1, 0, c.a)
        pegs(c, (10, 34), 8)
    elif kind == "trousers":
        c = Canvas(44, 72)
        c.fill(c.box(22, 14, 34, 14, 2), col, z0=2, height=4, profile="pillow", bevel=3, tex=folds(c, 0.5, seed))
        for lx in (13, 31):
            c.fill(c.box(lx, 44, 15, 54, 2), col * (0.97 if lx < 20 else 0.92), z0=2, height=4, profile="pillow", bevel=4, tex=folds(c, 0.9, seed + lx))
        pegs(c, (8, 36), 7)
    elif kind == "towel":
        c = Canvas(52, 60)
        stripe = np.where(((c.y // 6) % 5 == 0)[..., None], C(240, 240, 236), col)
        c.fill(c.box(26, 31, 44, 52, 2), stripe, z0=2, height=3, profile="pillow", bevel=3, tex=folds(c, 0.5, seed) * (0.92 + 0.08 * (np.sin(c.x * 3) > 0)))
        pegs(c, (10, 42), 5)
    elif kind == "sheet":
        c = Canvas(110, 70)
        c.fill(c.box(55, 36, 104, 62, 2), col, z0=2, height=4, profile="pillow", bevel=5, tex=folds(c, 0.32, seed))
        c.fill(c.box(55, 64, 104, 4, 1), col * 0.9, z0=5, height=0.5, profile="flat", line=0.2)       # hem
        pegs(c, (10, 55, 100), 5)
    elif kind == "dress":
        c = Canvas(56, 74)
        lx, ly = c.local(28, 40)
        half = 9 + np.clip(ly + 20, 0, 60) * 0.32
        shape = np.maximum(np.abs(lx) - half, np.abs(ly) - 32)
        dots = 1 - 0.25 * ((np.hypot(c.x % 8 - 4, c.y % 8 - 4) < 1.6) & (c.y > 12))
        c.fill(shape, col, z0=2, height=4, profile="pillow", bevel=4, tex=folds(c, 0.55, seed) * dots)
        for sx in (20, 36):
            c.fill(c.box(sx, 8, 3, 8, 1), col * 0.85, z0=4, height=1)
        pegs(c, (20, 36), 4)
    elif kind == "socks":
        c = Canvas(44, 40)
        for sx in (12, 32):
            c.fill(c.box(sx, 16, 10, 22, 3), col, z0=2, height=3, profile="pillow", bevel=3, tex=folds(c, 1.2, seed))
            c.fill(c.box(sx + 4, 28, 14, 9, 4), col * 0.95, z0=2, height=3, profile="pillow", bevel=3)
            c.fill(c.box(sx, 6, 10, 4, 1), C(236, 236, 230), z0=4, height=1)
        pegs(c, (12, 32), 3)
    else:  # apron
        c = Canvas(46, 60)
        c.fill(c.box(23, 34, 34, 46, 4), col, z0=2, height=3, profile="pillow", bevel=4, tex=folds(c, 0.7, seed))
        check = 0.88 + 0.12 * (((c.x // 5) + (c.y // 5)) % 2)
        c.fill(c.box(23, 36, 18, 12, 2), col * check[..., None] * 0.95, z0=5, height=1, line=0.3)        # pocket
        c.fill(c.box(23, 9, 40, 3, 1), col * 0.85, z0=4, height=1)                                      # ties
        pegs(c, (8, 38), 7)
    return c.render()


ITEMS = [
    ("shirt", (238, 238, 232)), ("shirt", (120, 160, 210)), ("shirt", (196, 70, 70)),
    ("vest", (240, 240, 236)), ("vest", (40, 40, 44)),
    ("trousers", (52, 70, 110)), ("trousers", (90, 84, 76)),
    ("towel", (210, 70, 90)), ("towel", (70, 130, 190)), ("towel", (232, 196, 80)),
    ("sheet", (236, 236, 228)), ("sheet", (170, 200, 220)),
    ("dress", (40, 60, 120)), ("dress", (200, 110, 150)),
    ("socks", (40, 40, 44)), ("apron", (150, 190, 120)),
]

if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for i, (kind, colour) in enumerate(ITEMS):
        garment(kind, colour, i).save(os.path.join(out, f"laundry_{i:02d}.png"))
    print(len(ITEMS))
