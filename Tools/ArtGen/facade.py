"""
The faces of the palazzi: windows with persiane (open or shut, green or brown) for the strip of
south face the game shows under each roof edge, and iron balconies that stick out over the lane.
Top-down-ish at 128 px per cell, north-west sun. Run: python3 facade.py <out>  (Resources/Facade)
"""
import os
import sys
import numpy as np
from paint import Canvas, fbm, smooth, grime

C = lambda *v: np.array(v, np.float32) / 255
GREEN, BROWN = (62, 104, 74), (116, 78, 48)


def window(shutter, open_):
    """A window seen on the shaded south face: stone frame, glass, slatted persiane."""
    c = Canvas(96, 44)
    cx, cy = 48, 22
    c.fill(c.box(cx, cy, 52, 38, 1.5), C(206, 198, 182), z0=0, height=3, bevel=1.5, tex=grime(c, 0.25, 6, 3))       # stone frame
    glass = c.box(cx, cy + 1, 42, 30, 1)
    refl = 0.75 + 0.35 * smooth(0, 1, (c.x - c.y * 0.6 - 20) / 50)
    c.fill(glass, C(40, 50, 64), z0=-1, height=0, profile="flat", gloss=1.0, tex=refl, line=0.5)
    c.fill(c.box(cx, cy + 1, 2.4, 30), C(220, 214, 200), z0=0, height=1, profile="flat", line=0.3)                   # the mullion
    slats = 0.78 + 0.22 * (np.sin(c.y * 1.7) > 0)
    col = C(*shutter)
    if open_:
        for sx in (cx - 34, cx + 34):
            c.fill(c.box(sx, cy, 15, 36, 1), col, z0=1, height=2, bevel=1, tex=slats * grime(c, 0.2, 5, sx), gloss=0.15)
    else:
        for sx in (cx - 10.5, cx + 10.5):
            c.fill(c.box(sx, cy + 1, 20, 31, 1), col, z0=1, height=2, bevel=1, tex=slats * grime(c, 0.2, 5, sx), gloss=0.15)
    c.fill(c.box(cx, cy + 19, 58, 5, 1), C(220, 214, 200), z0=2, height=2, bevel=1, tex=grime(c, 0.2, 4, 9))         # the sill
    return c.render()


def balcony(pots, seed):
    """An iron balcony from above: a stone slab with a railing round its open sides, pots on it."""
    c = Canvas(136, 48)
    c.fill(c.box(68, 22, 128, 38, 2), C(168, 162, 152), z0=0, height=4, bevel=2, tex=grime(c, 0.3, 8, seed), gloss=0.05)
    iron = C(34, 32, 32)
    c.fill(c.box(68, 40, 128, 2.6, 1), iron, z0=14, height=1.5, profile="dome", bevel=1.3, gloss=0.6, line=0.4)
    for ex in (5, 131):
        c.fill(c.box(ex, 22, 2.6, 38, 1), iron, z0=14, height=1.5, profile="dome", bevel=1.3, gloss=0.6, line=0.4)
    for k in range(13):
        c.fill(c.circle(6 + k * 10, 40, 1.5), iron, z0=12, height=2, profile="dome", bevel=1.5, gloss=0.5, line=0.3)
    rng = np.random.default_rng(seed)
    if pots:
        for px in (20, 112) if rng.random() < 0.5 else (24, 68, 112):
            c.fill(c.circle(px, 22, 10), C(176, 96, 62), z0=4, height=8, bevel=2, gloss=0.2)
            for k in range(9):
                a, r = rng.random() * 6.28, rng.random() * 8
                c.fill(c.circle(px + np.cos(a) * r, 22 + np.sin(a) * r, 4 + rng.random() * 2), C(56, 108 + rng.integers(0, 30), 50),
                       z0=12 + rng.random() * 3, height=3, profile="dome", bevel=3, gloss=0.4, line=0.3, add_z=True)
            for k in range(4):
                a, r = rng.random() * 6.28, rng.random() * 7
                c.fill(c.circle(px + np.cos(a) * r, 22 + np.sin(a) * r, 2.6), C(212, 44, 60), z0=17, height=2, profile="dome", bevel=2.5, line=0.3)
    else:
        # a plastic chair and a bucket
        c.fill(c.box(40, 20, 22, 22, 4), C(230, 228, 220), z0=4, height=6, bevel=2.5, gloss=0.4)
        c.fill(c.circle(100, 20, 8), C(60, 120, 190), z0=4, height=9, bevel=2, gloss=0.5)
    return c.render()


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, img in (("window_green_open", window(GREEN, True)), ("window_green_shut", window(GREEN, False)),
                      ("window_brown_open", window(BROWN, True)), ("window_brown_shut", window(BROWN, False)),
                      ("balcony_pots", balcony(True, 1)), ("balcony_pots2", balcony(True, 5)), ("balcony_chair", balcony(False, 2))):
        img.save(os.path.join(out, f"facade_{name}.png"))
        print(name)
