"""
Things on the terraces of the Quartieri, top-down at 128 px per cell, lit by the north-west sun:
water tanks, dishes, solar heaters, aerials, plastic tables, potted lemons, geraniums, AC units,
stair huts and drying racks. Run: python3 roofprops.py <out folder>   (Resources/Roof)
"""
import os
import sys
import numpy as np
from paint import Canvas, fbm, vnoise, smooth, grime

C = lambda *v: np.array(v, np.float32) / 255


def tank(dark=True, seed=1):
    c = Canvas(76, 76)
    cx = cy = 38
    body = C(38, 40, 44) if dark else C(222, 222, 214)
    r = np.hypot(c.x - cx, c.y - cy)
    ribs = 0.93 + 0.07 * np.sin(r * 1.9)
    c.fill(c.circle(cx, cy, 34), body, z0=0, height=26, profile="dome", bevel=12, gloss=0.5, tex=ribs * grime(c, 0.18, 9, seed))
    c.fill(c.circle(cx, cy, 13), body * 0.85, z0=24, height=4, profile="bevel", bevel=2, gloss=0.6)
    c.fill(c.circle(cx, cy, 9), body * 0.7, z0=27, height=1.5, profile="dome", bevel=4, gloss=0.6,
           tex=0.9 + 0.1 * np.sin(np.arctan2(c.y - cy, c.x - cx) * 16))
    # an inlet pipe running off to the side
    c.fill(c.box(cx + 30, cy + 22, 26, 4, 2, 35), C(200, 200, 196) if dark else C(60, 60, 64), z0=2, height=3, profile="dome", bevel=2, gloss=0.4)
    return c.render()


def dish(seed=2):
    c = Canvas(60, 60)
    cx, cy = 30, 26
    dx, dy = c.local(cx, cy)
    d = np.hypot(dx / 24, dy / 19)
    c.fill((d - 1) * 19, C(214, 214, 208), z0=6, height=10, profile="dome", bevel=14, gloss=0.6, tex=grime(c, 0.15, 6, seed))
    c.fill(c.ellipse(cx, cy, 20, 15), C(196, 196, 190), z0=10, height=-4, profile="dome", bevel=10, gloss=0.4, add_z=False)
    c.fill(c.box(cx, cy + 15, 3, 26, 1), C(70, 70, 72), z0=8, height=2, gloss=0.5)
    c.fill(c.box(cx, cy + 27, 8, 6, 2), C(40, 40, 44), z0=8, height=4, gloss=0.6)
    return c.render()


def solar(seed=3):
    c = Canvas(104, 76)
    panel = c.box(52, 30, 96, 46, 2)
    grid = ((c.x % 12) < 1.0) | ((c.y % 15) < 1.0)
    pc = np.where(grid[..., None], C(150, 156, 166), C(32, 46, 72))
    c.fill(panel, pc, z0=10, height=3, bevel=1.5, gloss=1.0, tex=0.9 + 0.2 * smooth(0.3, 0.9, (c.x + c.y * 0.7) / 140))
    c.fill(c.box(52, 62, 94, 14, 7), C(224, 224, 218), z0=6, height=12, profile="dome", bevel=7, gloss=0.5, tex=grime(c, 0.12, 8, seed))
    for ex in (10, 94):
        c.fill(c.circle(ex, 62, 6.5), C(196, 196, 190), z0=14, height=2, profile="dome", bevel=6, gloss=0.4)
    return c.render()


def antenna(seed=4):
    c = Canvas(90, 90)
    # boom across, elements along it, a mast in the middle
    c.fill(c.box(45, 45, 76, 2.4, 1, 20), C(120, 120, 124), z0=30, height=1.5, profile="dome", bevel=1.2, gloss=0.7, line=0.5)
    for k in range(-3, 4):
        a = np.radians(20)
        px, py = 45 + k * 10 * np.cos(a), 45 + k * 10 * np.sin(a)
        ln = 34 - abs(k) * 3
        c.fill(c.box(px, py, 1.8, ln, 0.9, 20), C(150, 150, 154), z0=30, height=1, profile="dome", bevel=0.9, gloss=0.7, line=0.5)
    c.fill(c.circle(45, 45, 3.2), C(80, 80, 84), z0=31, height=2, profile="dome", bevel=3, gloss=0.6)
    return c.render()


def table(seed=5):
    """A white plastic table with two chairs pulled up, backs to the table's far side."""
    c = Canvas(116, 116)
    white = C(232, 230, 222)
    for ang in (200, 20):
        a = np.radians(ang)
        px, py = 58 + 36 * np.cos(a), 58 + 36 * np.sin(a)
        # seat, then the curved back on the outer side
        c.fill(c.box(px, py, 30, 28, 7, ang), white * 0.96, z0=0, height=8, bevel=3, gloss=0.4, tex=grime(c, 0.12, 6, seed + ang))
        bx, by = 58 + 50 * np.cos(a), 58 + 50 * np.sin(a)
        c.fill(c.box(bx, by, 8, 32, 4, ang), white * 0.9, z0=8, height=12, profile="dome", bevel=4, gloss=0.4)
    c.fill(c.circle(58, 58, 30), white, z0=10, height=6, bevel=4, gloss=0.5, tex=grime(c, 0.1, 8, seed))
    c.fill(c.circle(58, 58, 4), C(150, 150, 150), z0=16, height=1, profile="flat", line=0.2)
    c.fill(c.circle(66, 52, 6), C(170, 120, 80), z0=16, height=2, profile="dome", bevel=3, gloss=0.6)
    c.fill(c.circle(48, 64, 4), C(210, 225, 230), z0=16, height=5, profile="bevel", bevel=1, gloss=1.0)
    return c.render()


def lemon_pot(seed=6):
    c = Canvas(84, 84)
    cx = cy = 42
    c.fill(c.circle(cx, cy, 30), C(176, 96, 62), z0=0, height=16, bevel=4, gloss=0.2, tex=grime(c, 0.2, 7, seed))
    c.fill(c.circle(cx, cy, 25), C(70, 50, 36), z0=6, height=0, profile="flat")
    rng = np.random.default_rng(seed)
    for k in range(26):
        a, rr = rng.random() * 6.28, rng.random() * 30
        lx, ly = cx + np.cos(a) * rr, cy + np.sin(a) * rr
        ang = rng.random() * 180
        leaf = np.maximum(c.ellipse(lx, ly, 9, 4, ang), -60)
        c.fill(leaf, C(52, 104 + rng.integers(0, 30), 46), z0=16 + rng.random() * 10, height=3, profile="dome", bevel=3, gloss=0.5, line=0.35, add_z=True)
    for k in range(6):
        a, rr = rng.random() * 6.28, rng.random() * 22
        c.fill(c.ellipse(cx + np.cos(a) * rr, cy + np.sin(a) * rr, 5, 4, rng.random() * 90), C(236, 200, 50), z0=26, height=4, profile="dome", bevel=4, gloss=0.6, line=0.3)
    return c.render()


def geraniums(seed=7):
    c = Canvas(84, 44)
    c.fill(c.box(42, 22, 76, 30, 4), C(186, 100, 66), z0=0, height=12, bevel=3, tex=grime(c, 0.2, 6, seed))
    c.fill(c.box(42, 22, 68, 22, 3), C(66, 48, 34), z0=6, height=0, profile="flat")
    rng = np.random.default_rng(seed)
    for k in range(30):
        x, y = 10 + rng.random() * 64, 12 + rng.random() * 20
        c.fill(c.circle(x, y, 4 + rng.random() * 3), C(56, 110 + rng.integers(0, 30), 50), z0=12 + rng.random() * 4, height=3, profile="dome", bevel=4, gloss=0.4, line=0.3, add_z=True)
    for k in range(9):
        x, y = 12 + rng.random() * 60, 13 + rng.random() * 18
        c.fill(c.circle(x, y, 3.2), C(214, 40 + rng.integers(0, 30), 56), z0=19, height=2, profile="dome", bevel=3, gloss=0.4, line=0.3)
    return c.render()


def ac_unit(seed=8):
    c = Canvas(70, 50)
    c.fill(c.box(35, 25, 62, 40, 3), C(206, 206, 200), z0=0, height=16, bevel=2.5, gloss=0.3, tex=grime(c, 0.2, 6, seed))
    r = np.hypot(c.x - 44, c.y - 25)
    grille = 0.75 + 0.25 * (np.sin(r * 2.2) > 0)
    c.fill(c.circle(44, 25, 15), C(60, 60, 62), z0=16, height=-2, profile="flat", tex=grille, gloss=0.2)
    c.fill(c.circle(44, 25, 4), C(120, 120, 120), z0=15, height=1, profile="dome", bevel=4)
    return c.render()


def torrino(seed=9):
    """The stair hut: a concrete box with its own little flat roof and a vent pipe."""
    c = Canvas(184, 152)
    c.fill(c.box(92, 76, 176, 144, 2), C(196, 190, 178), z0=0, height=30, bevel=4, gloss=0.05, tex=grime(c, 0.25, 14, seed))
    slab = c.box(92, 76, 164, 132, 1)
    c.fill(slab, C(150, 146, 138), z0=30, height=0, profile="flat", tex=(0.9 + 0.12 * fbm(c.x, c.y, 10, 4, seed)) * grime(c, 0.3, 18, seed + 1), line=0.3)
    # tar-patched cracks and a puddle mark
    patch = smooth(0.6, 0.62, fbm(c.x, c.y, 30, 3, seed + 2))
    c.col = c.col * (1 - 0.2 * patch * (c.z > 29))[..., None]
    c.fill(c.circle(150, 36, 7), C(70, 70, 74), z0=30, height=14, profile="dome", bevel=6, gloss=0.4)
    c.fill(c.circle(150, 36, 4), C(30, 30, 32), z0=44, height=0, profile="flat")
    return c.render()


def rack(seed=10):
    c = Canvas(132, 70)
    for yy in (18, 36, 54):
        c.fill(c.box(66, yy, 120, 2.2, 1), C(190, 190, 186), z0=14, height=1, profile="dome", bevel=1, gloss=0.6, line=0.4)
    rng = np.random.default_rng(seed)
    cols = [C(240, 240, 234), C(70, 110, 170), C(196, 70, 70), C(232, 204, 92), C(150, 196, 220), C(40, 40, 44), C(220, 150, 180)]
    x = 8
    while x < 118:
        w = 10 + rng.random() * 14
        yy = (18, 36, 54)[rng.integers(0, 3)]
        col = cols[rng.integers(0, len(cols))]
        folds = 0.85 + 0.15 * np.sin((c.x - x) * 0.9)
        c.fill(c.box(x + w / 2, yy, w, 12, 1.5), col, z0=15, height=2, bevel=2, tex=folds, gloss=0.05, line=0.3)
        x += w + 3
    for ex in (5, 127):
        c.fill(c.box(ex, 36, 4, 50, 2), C(170, 170, 166), z0=10, height=6, bevel=2, gloss=0.5)
    return c.render()


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    items = {
        "tank_black": lambda: tank(True), "tank_white": lambda: tank(False, 11), "dish": dish, "solar": solar,
        "antenna": antenna, "table": table, "lemon_pot": lemon_pot, "geraniums": geraniums, "ac_unit": ac_unit,
        "torrino": torrino, "rack": rack,
    }
    for k, fn in items.items():
        fn().save(os.path.join(out, f"roof_{k}.png"))
        print(k)
