"""
More furniture for interiors, in the painted props' framing: seen from above and a little from the
front (front facing down), 192 px per cell, north-west light. Run: python3 furniture.py <out>
(Resources/Props, as furn_<name>.png)
"""
import os
import sys
import numpy as np
from paint import Canvas, fbm, vnoise, smooth, grime

C = lambda *v: np.array(v, np.float32) / 255
CELL = 192
WOOD, DARKWOOD, WHITE = (150, 102, 62), (104, 68, 40), (232, 230, 222)


def wood(c, seed, along="x", scale=1.0):
    u, v = (c.x, c.y) if along == "x" else (c.y, c.x)
    warp = fbm(u, v, 30 * scale, 3, seed) * 7
    return (0.84 + 0.12 * np.sin((v * 1.1 / scale + warp) * 1.4)) * grime(c, 0.15, 20, seed)


def dining_chair(seed=1):
    c = Canvas(96, 110)
    col = C(*WOOD)
    c.fill(c.box(48, 62, 62, 52, 5), col, z0=20, height=4, bevel=3, tex=wood(c, seed), front=10, gloss=0.2)
    weave = 0.82 + 0.18 * ((np.sin(c.x * 1.6) * np.sin(c.y * 1.6)) > 0)
    c.fill(c.box(48, 62, 48, 40, 4), C(200, 164, 104), z0=24, height=2, bevel=2, tex=weave, line=0.3)
    c.fill(c.box(48, 24, 66, 12, 4), col * 0.92, z0=40, height=4, profile="dome", bevel=4, tex=wood(c, seed + 1), front=6)
    for px in (20, 76):
        c.fill(c.box(px, 32, 9, 22, 3), col * 0.85, z0=30, height=4, profile="dome", bevel=3)
    return c.render()


def nightstand(seed=2):
    c = Canvas(120, 130)
    c.fill(c.box(60, 52, 100, 80, 4), C(*DARKWOOD), z0=20, height=4, bevel=3, tex=wood(c, seed), gloss=0.35,
           front=30, front_tex=wood(c, seed + 3) * (0.85 + 0.15 * ((c.y % 15) > 1.5)))
    c.fill(c.box(60, 112, 14, 4, 2), C(200, 170, 80), z0=22, height=1, gloss=0.8, line=0.3)       # drawer knob
    c.fill(c.circle(44, 46, 18), C(240, 226, 196), z0=34, height=10, profile="dome", bevel=12, gloss=0.2)  # lamp shade
    c.fill(c.circle(44, 46, 5), C(200, 170, 80), z0=44, height=2, profile="dome", bevel=4, gloss=0.8)
    c.fill(c.box(82, 64, 18, 24, 2, 8), C(60, 40, 30), z0=26, height=2, line=0.4)                   # a little book
    return c.render()


def dresser(seed=3):
    c = Canvas(300, 140)
    tex_front = wood(c, seed + 3) * (0.86 + 0.14 * ((c.y % 14) > 1.6)) * (0.9 + 0.1 * ((c.x % 100) > 2))
    c.fill(c.box(150, 50, 284, 80, 5), C(*DARKWOOD), z0=24, height=4, bevel=3, tex=wood(c, seed), gloss=0.4,
           front=40, front_tex=tex_front)
    for kx in (60, 150, 240):
        for ky in (104, 118):
            c.fill(c.circle(kx, ky, 3), C(206, 176, 90), z0=26, height=1, profile="dome", bevel=3, gloss=0.8, line=0.3)
    lace = 0.8 + 0.2 * ((np.sin(c.x * 0.9) * np.sin(c.y * 0.9)) > 0.2)
    c.fill(c.ellipse(150, 48, 60, 26), C(240, 236, 226), z0=28, height=1, profile="flat", tex=lace, line=0.25)
    c.fill(c.circle(150, 46, 12), C(70, 110, 160), z0=29, height=14, profile="dome", bevel=12, gloss=0.8)  # vase
    for k, (px, col) in enumerate(((60, C(220, 200, 150)), (86, C(150, 110, 70)))):                      # photo frames
        c.fill(c.box(px, 40, 20, 26, 2, -6 + k * 10), col, z0=29, height=3, bevel=1.5, gloss=0.4)
    c.fill(c.circle(238, 44, 16), C(196, 160, 90), z0=28, height=3, profile="dome", bevel=6, gloss=0.6)    # a bowl
    return c.render()


def washing_machine(seed=5):
    c = Canvas(150, 160)
    c.fill(c.box(75, 58, 132, 98, 8), C(*WHITE), z0=30, height=3, bevel=4, gloss=0.5, tex=grime(c, 0.12, 12, seed), front=44)
    c.fill(c.box(44, 24, 50, 14, 3), C(190, 200, 210), z0=33, height=1, gloss=0.6, line=0.3)          # detergent drawer
    for k, kx in enumerate((92, 112)):
        c.fill(c.circle(kx, 26, 6), C(160, 160, 166), z0=33, height=2, profile="dome", bevel=6, gloss=0.6)
    c.fill(c.circle(75, 132, 18), C(150, 156, 166), z0=12, height=2, profile="dome", bevel=4, gloss=0.6)  # the porthole, on the front
    c.fill(c.circle(75, 132, 12), C(60, 80, 110), z0=13, height=0, profile="flat", gloss=1.0)
    c.fill(c.box(60, 60, 70, 30, 6, 6), C(200, 70, 80), z0=34, height=3, profile="pillow", bevel=3)       # a folded towel on top
    return c.render()


def floor_lamp(seed=6):
    c = Canvas(110, 110)
    c.fill(c.circle(55, 55, 16), C(60, 50, 40), z0=0, height=4, profile="dome", bevel=6, gloss=0.6)
    c.fill(c.circle(55, 55, 40), C(232, 214, 176), z0=60, height=16, profile="dome", bevel=24, gloss=0.1,
           tex=0.92 + 0.08 * np.sin(np.arctan2(c.y - 55, c.x - 55) * 18))
    c.fill(c.circle(55, 55, 8), C(200, 170, 90), z0=76, height=3, profile="dome", bevel=6, gloss=0.8)
    return c.render()


def bar_stool(seed=8):
    c = Canvas(90, 96)
    c.fill(c.circle(45, 44, 30), C(170, 172, 178), z0=0, height=3, profile="dome", bevel=3, gloss=0.8, front=8)
    c.fill(c.circle(45, 44, 25), C(130, 40, 40), z0=40, height=8, profile="pillow", bevel=10, gloss=0.6,
           tex=0.92 + 0.08 * (np.hypot(c.x - 45, c.y - 44) % 6 > 1), front=6)
    return c.render()


def flour_sacks(seed=10):
    c = Canvas(170, 150)
    rng = np.random.default_rng(seed)
    for k, (px, py, ang) in enumerate(((52, 52, -10), (118, 56, 8), (82, 100, 2))):
        weave = 0.88 + 0.12 * ((np.sin(c.x * 2.2) > 0) ^ (np.sin(c.y * 2.2) > 0))
        c.fill(c.box(px, py, 66, 82, 18, ang), C(226, 214, 186), z0=k * 10, height=16, profile="pillow", bevel=16, tex=weave * grime(c, 0.18, 10, k), front=12)
        c.fill(c.box(px, py + 4, 34, 20, 4, ang), C(60, 90, 150), z0=k * 10 + 16, height=0.5, profile="flat", line=0.2)
    return c.render()


def glass_vitrine(seed=11):
    """The deli counter: a glass case of cheeses, salami and olives, seen from the customer's side."""
    c = Canvas(380, 170)
    c.fill(c.box(190, 70, 364, 100, 6), C(206, 206, 202), z0=30, height=2, bevel=3, gloss=0.6, front=44,
           front_tex=0.85 + 0.15 * ((c.x % 90) > 3))
    c.fill(c.box(190, 70, 344, 80, 4), C(150, 170, 176), z0=31, height=0, profile="flat", gloss=1.0)
    rng = np.random.default_rng(seed)
    goods = [C(240, 210, 120), C(236, 226, 200), C(170, 60, 60), C(200, 110, 90), C(90, 110, 50), C(220, 180, 90)]
    for k in range(9):
        px = 40 + k * 37
        g = goods[k % len(goods)]
        if k % 3 == 2:   # a salame, sliced
            c.fill(c.ellipse(px, 70, 14, 30, 8), g * 0.9, z0=33, height=8, profile="dome", bevel=10, gloss=0.4, line=0.4)
        else:
            c.fill(c.box(px, 70, 28, 52, 4, rng.random() * 10 - 5), g, z0=33, height=6, profile="pillow", bevel=6,
                   tex=0.9 + 0.1 * vnoise(c.x, c.y, 2, k), gloss=0.3, line=0.4)
    # the glass catches the light: a soft sheen across the top
    sheen = smooth(0.0, 1.0, 1 - np.abs((c.x - c.y * 1.4 - 80) / 70))
    c.col = c.col + (sheen * 0.12 * (c.y < 112))[..., None]
    return c.render()


def jewel_case(seed=13):
    c = Canvas(200, 150)
    c.fill(c.box(100, 60, 184, 90, 5), C(*DARKWOOD), z0=30, height=2, bevel=3, tex=wood(c, seed), gloss=0.5, front=40)
    c.fill(c.box(100, 60, 164, 72, 3), C(110, 24, 36), z0=31, height=0, profile="flat", tex=0.9 + 0.1 * vnoise(c.x, c.y, 3, 4))
    rng = np.random.default_rng(seed)
    for k in range(10):
        px, py = 34 + (k % 5) * 32, 42 + (k // 5) * 34
        if k % 3 == 0:
            c.fill(c.ring(px, py, 8, 3), C(230, 196, 90), z0=33, height=2, profile="dome", bevel=2, gloss=1.0, line=0.3)
        else:
            c.fill(c.box(px, py, 20, 12, 3), C(40, 40, 46), z0=32, height=2, bevel=2, gloss=0.4)
            c.fill(c.circle(px, py, 4), C(220, 220, 230) if k % 2 else C(230, 196, 90), z0=34, height=2, profile="dome", bevel=4, gloss=1.0)
    sheen = smooth(0.0, 1.0, 1 - np.abs((c.x - c.y * 1.4 - 40) / 60))
    c.col = c.col + (sheen * 0.12 * (c.y < 98))[..., None]
    return c.render()


def display_table(seed=14):
    c = Canvas(300, 150)
    c.fill(c.box(150, 60, 280, 96, 4), C(*WOOD), z0=26, height=3, bevel=3, tex=wood(c, seed), front=30)
    rng = np.random.default_rng(seed)
    for k in range(8):
        px, py = 44 + (k % 4) * 70, 40 + (k // 4) * 42
        col = C(*[(60, 80, 140), (200, 200, 196), (150, 50, 50), (40, 40, 44), (190, 160, 110)][rng.integers(0, 5)])
        for layer in range(3):
            c.fill(c.box(px, py - layer * 2, 52, 30, 3), col * (0.9 + 0.05 * layer), z0=30 + layer * 3, height=3,
                   profile="pillow", bevel=3, tex=0.9 + 0.1 * np.sin(c.y * 1.4), line=0.35)
    return c.render()


def tyre_stack(seed=16):
    c = Canvas(150, 150)
    for k, (px, py) in enumerate(((52, 54), (100, 70), (64, 104))):
        r = np.hypot(c.x - px, c.y - py)
        tread = 0.8 + 0.2 * (np.sin(np.arctan2(c.y - py, c.x - px) * 30) > 0)
        c.fill(c.ring(px, py, 26, 20), C(34, 34, 36), z0=k * 8, height=10, profile="dome", bevel=10, gloss=0.4, tex=tread)
        c.fill(c.circle(px, py, 15), C(150, 150, 156), z0=k * 8, height=3, profile="dome", bevel=6, gloss=0.8)
    return c.render()


def charred(seed=17):
    c = Canvas(190, 170)
    rng = np.random.default_rng(seed)
    c.fill(c.ellipse(95, 85, 86, 70) + (fbm(c.x, c.y, 12, 3, seed) - 0.5) * 30, C(36, 32, 30), z0=0, height=1, profile="flat",
           tex=0.8 + 0.4 * vnoise(c.x, c.y, 3, seed), line=0)
    for k in range(9):
        px, py, ang = 30 + rng.random() * 130, 30 + rng.random() * 110, rng.random() * 180
        burnt = 0.6 + 0.4 * (vnoise(c.x, c.y, 2, k) > 0.5)
        c.fill(c.box(px, py, 70 + rng.random() * 40, 11, 2, ang), C(46, 34, 26), z0=2 + k, height=5, profile="dome", bevel=4,
               tex=burnt, gloss=0.3, line=0.4)
        glow = (vnoise(c.x, c.y, 1.5, k + 50) > 0.9) * 0.0
    ash = (vnoise(c.x, c.y, 1.2, 99) > 0.8) * 0.25
    c.col = c.col + (ash * (c.a > 0.5))[..., None]
    return c.render()


def potted_palm(seed=18):
    c = Canvas(160, 160)
    c.fill(c.circle(80, 80, 30), C(184, 104, 66), z0=0, height=20, bevel=4, gloss=0.2, tex=grime(c, 0.2, 8, seed))
    for k in range(9):
        a = k / 9 * 6.28 + 0.3
        px, py = 80 + np.cos(a) * 38, 80 + np.sin(a) * 38
        leaf = c.ellipse(px, py, 46, 11, np.degrees(a))
        frond = 0.75 + 0.25 * (np.sin((c.x * np.cos(a + 1.57) + c.y * np.sin(a + 1.57)) * 1.8) > 0)
        c.fill(leaf, C(60, 120 + k * 4, 54), z0=30 + k * 2, height=4, profile="dome", bevel=6, gloss=0.5, tex=frond, line=0.35, add_z=True)
    return c.render()


def sideboard(seed=19):
    c = Canvas(300, 140)
    tex_front = wood(c, seed + 3) * (0.86 + 0.14 * ((c.x % 72) > 2.5))
    c.fill(c.box(150, 50, 284, 76, 4), C(120, 76, 44), z0=26, height=4, bevel=3, tex=wood(c, seed), gloss=0.5, front=40, front_tex=tex_front)
    c.fill(c.box(110, 46, 80, 40, 3), C(230, 228, 220), z0=30, height=1, profile="flat", line=0.2)
    for k in range(3):
        c.fill(c.circle(96 + k * 14, 46, 8), C(240, 240, 236), z0=31, height=6, profile="dome", bevel=6, gloss=0.8)   # good china
    c.fill(c.box(214, 42, 46, 36, 2), C(220, 196, 120), z0=30, height=6, bevel=2, gloss=0.6)                      # a framed wedding photo
    c.fill(c.box(214, 42, 36, 26, 1), C(120, 110, 100), z0=36, height=0, profile="flat")
    return c.render()


def shelf_unit(seed, frame, fill_bay, back=(58, 40, 28)):
    """
    A tall unit against the wall, seen like the painted shelves: a thin top, and mostly its front:
    a frame of three bays, each stocked by fill_bay(c, x0, x1, ytop, ybottom, rng).
    """
    c = Canvas(180, 190)
    rng = np.random.default_rng(seed)
    fr = C(*frame)
    c.fill(c.box(90, 12, 172, 16, 2), fr * 1.05, z0=60, height=2, bevel=1.5, tex=wood(c, seed), gloss=0.3)     # the top
    c.fill(c.box(90, 102, 172, 168, 2), fr, z0=40, height=0, profile="flat", tex=wood(c, seed + 1, "y"))        # the frame
    bays = [(26, 70), (76, 120), (126, 170)]
    for (y0, y1) in bays:
        c.fill(c.box(90, (y0 + y1) / 2, 156, y1 - y0, 1), C(*back), z0=30, height=0, profile="flat",
               tex=0.75 + 0.25 * smooth(y0, y1, c.y))                                                          # the dark inside
        fill_bay(c, 14, 166, y0, y1, rng)
        c.fill(c.box(90, y1 + 2, 164, 5, 1), fr * 0.9, z0=42, height=1, profile="flat", line=0.4)              # the shelf's lip
    return c.render()


def bookcase(seed=4):
    def bay(c, x0, x1, y0, y1, rng):
        x = x0 + 2
        while x < x1 - 4:
            w = 6 + rng.random() * 8
            h = (y1 - y0) * (0.65 + rng.random() * 0.3)
            col = C(*[(140, 40, 40), (40, 70, 120), (60, 100, 60), (200, 170, 90), (90, 60, 40), (220, 210, 190), (120, 90, 140)][rng.integers(0, 7)])
            c.fill(c.box(x + w / 2, y1 - h / 2, w, h, 1), col * (0.8 + 0.3 * rng.random()), z0=34, height=2, bevel=1, line=0.5,
                   tex=1 - 0.25 * (np.abs(c.y - (y1 - h * 0.7)) < 1.2))
            x += w + 0.5
    return shelf_unit(seed, (104, 68, 40), bay)


def wine_rack(seed=9):
    def bay(c, x0, x1, y0, y1, rng):
        for row in range(2):
            for k in range(8):
                px, py = x0 + 10 + k * 19.5, y0 + 12 + row * 20
                glass = C(*[(40, 70, 40), (80, 30, 40), (40, 50, 30), (190, 160, 90)][rng.integers(0, 4)])
                c.fill(c.circle(px, py, 8), glass, z0=36, height=4, profile="dome", bevel=8, gloss=1.0, line=0.5)
                c.fill(c.circle(px, py, 3), C(210, 180, 130), z0=40, height=1, profile="flat")
    return shelf_unit(seed, (150, 102, 62), bay)


def bread_rack(seed=15):
    def bay(c, x0, x1, y0, y1, rng):
        x = x0 + 4
        while x < x1 - 20:
            w = 20 + rng.random() * 14
            c.fill(c.ellipse(x + w / 2, y1 - 13, w / 2, 13), C(206, 150, 80) * (0.85 + 0.25 * rng.random()), z0=36, height=8,
                   profile="dome", bevel=10, gloss=0.4, line=0.5, tex=1 - 0.18 * (np.abs(((c.x - x) % 9) - 4.5) < 1))
            x += w + 2
    return shelf_unit(seed, (150, 102, 62), bay, back=(120, 90, 60))


def pharmacy_cabinet(seed=12):
    def bay(c, x0, x1, y0, y1, rng):
        x = x0 + 2
        while x < x1 - 6:
            w = 9 + rng.random() * 10
            h = (y1 - y0) * (0.45 + rng.random() * 0.35)
            col = C(*[(240, 240, 236), (90, 160, 200), (230, 120, 110), (120, 190, 130), (240, 210, 110)][rng.integers(0, 5)])
            c.fill(c.box(x + w / 2, y1 - h / 2, w, h, 1), col, z0=34, height=2, bevel=1, line=0.5)
            c.fill(c.box(x + w / 2, y1 - h * 0.6, w * 0.7, 3, 0.5), col * 0.7, z0=36, height=0, profile="flat")
            x += w + 1.5
    return shelf_unit(seed, (236, 234, 228), bay, back=(200, 206, 210))


def burnt_shelf(seed=20):
    def bay(c, x0, x1, y0, y1, rng):
        for k in range(4):
            c.fill(c.circle(x0 + 10 + rng.random() * (x1 - x0 - 20), y1 - 8, 6 + rng.random() * 6), C(30, 28, 28), z0=36, height=3, profile="dome", bevel=6)
    return shelf_unit(seed, (54, 40, 32), bay, back=(24, 22, 22))


PIECES = {
    "dining_chair": dining_chair, "nightstand": nightstand, "dresser": dresser, "bookcase": bookcase,
    "washing_machine": washing_machine, "floor_lamp": floor_lamp, "bar_stool": bar_stool,
    "wine_rack": wine_rack, "flour_sacks": flour_sacks, "glass_vitrine": glass_vitrine, "pharmacy_cabinet": pharmacy_cabinet,
    "jewel_case": jewel_case, "display_table": display_table, "bread_rack": bread_rack, "tyre_stack": tyre_stack,
    "charred": charred, "potted_palm": potted_palm, "sideboard": sideboard, "burnt_shelf": burnt_shelf,
}

if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    only = sys.argv[2:] or list(PIECES)
    for name in only:
        PIECES[name]().save(os.path.join(out, f"furn_{name}.png"))
        print(name)
