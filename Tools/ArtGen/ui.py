"""
The HUD's painted pieces, in the same light as the props: gilded icons, the panel frame and the
buttons. Everything is drawn at twice its size on screen (and 4x inside the painter), so it stays
crisp on big monitors; frames are 9-slice (the USS gives the slice margins and halves them back).
Run: python3 ui.py <out>  (Resources/UI/Art)
"""
import os
import sys
import numpy as np
from paint import Canvas, fbm, smooth, grime

C = lambda *v: np.array(v, np.float32) / 255
GOLD = C(214, 166, 82)
GOLD_DARK = C(150, 106, 46)
INK = C(26, 20, 14)


# ---------- helpers ----------

def poly(c, pts):
    """Signed distance to a closed polygon (canvas px), negative inside."""
    x, y = c.x, c.y
    d = np.full(x.shape, 1e9, np.float32)
    inside = np.zeros(x.shape, bool)
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        wx, wy = x - ax, y - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-9), 0, 1)
        d = np.minimum(d, np.hypot(wx - ex * t, wy - ey * t))
        cond = ((ay > y) != (by > y)) & (x < (bx - ax) * (y - ay) / (by - ay + 1e-9) + ax)
        inside ^= cond
    return np.where(inside, -d, d)


def union(*ds):
    out = ds[0]
    for d in ds[1:]:
        out = np.minimum(out, d)
    return out


def cut(a, b):
    return np.maximum(a, -b)


def gilt(c, seed=0, tone=1.0):
    """Gold leaf: a soft sheen across the piece, a little rubbed and uneven."""
    sheen = 0.9 + 0.22 * np.sin((c.x * 0.6 - c.y) / max(c.w, c.h) * 5.0 + 1.2)
    rub = 0.92 + 0.12 * fbm(c.x, c.y, 10, 3, seed)
    return sheen * rub * tone


def save(img, out, name):
    img.save(os.path.join(out, name + ".png"))
    print("  ", name, img.size)


# ---------- icons: 128 px (64 on screen) ----------

def icon_money():
    c = Canvas(128, 128)
    cx, cy = 64, 66
    c.fill(c.circle(cx, cy, 52), GOLD, z0=0, height=8, profile="dome", bevel=14, tex=gilt(c, 1), gloss=0.7)
    c.fill(c.ring(cx, cy, 42, 3), GOLD_DARK, z0=5, height=1, profile="bevel", bevel=1, tex=gilt(c, 2, 0.9), gloss=0.4)
    # the euro sign, raised
    arc = cut(c.ring(cx + 4, cy, 22, 9), c.box(cx + 26, cy, 22, 26))
    bars = union(c.box(cx - 6, cy - 6, 34, 6, 2), c.box(cx - 6, cy + 6, 34, 6, 2))
    c.fill(union(arc, bars), GOLD, z0=6, height=4, profile="bevel", bevel=2.5, tex=gilt(c, 3, 1.05), gloss=0.8)
    return c.render()


def icon_respect():
    c = Canvas(128, 128)
    cx = 64
    pts = [(14, 96), (14, 50), (38, 72), (64, 30), (90, 72), (114, 50), (114, 96)]
    c.fill(poly(c, pts), GOLD, z0=0, height=6, profile="bevel", bevel=6, tex=gilt(c, 4), gloss=0.7)
    c.fill(c.box(cx, 100, 104, 16, 3), GOLD_DARK, z0=2, height=6, profile="bevel", bevel=4, tex=gilt(c, 5, 0.95), gloss=0.6)
    for x, y in ((14, 46), (64, 26), (114, 46)):
        c.fill(c.circle(x, y, 8), GOLD, z0=4, height=6, profile="dome", bevel=8, tex=gilt(c, 6), gloss=0.9)
    for x, col in ((40, C(170, 40, 36)), (64, C(40, 110, 70)), (88, C(170, 40, 36))):
        c.fill(c.circle(x, 100, 5), col, z0=7, height=4, profile="dome", bevel=5, gloss=1.0, line=0.4)
    return c.render()


def icon_heat():
    c = Canvas(128, 128)
    t = np.linspace(0, 1, 40)

    def flame(cx, base, w, h, lean):
        left = [(cx - w * np.sin(np.pi * s) * (1 - 0.35 * s) + lean * s * s, base - h * s) for s in t]
        right = [(cx + w * np.sin(np.pi * s) * (1 - 0.6 * s) + lean * s * s, base - h * s) for s in t[::-1]]
        return poly(c, left + right)

    outer = union(flame(64, 118, 46, 108, 8), c.circle(64, 88, 30))
    grad = smooth(20, 118, c.y)
    col = C(232, 92, 38)[None, None, :] * (1 - grad[..., None]) + C(196, 40, 28)[None, None, :] * grad[..., None]
    c.fill(outer, col, z0=0, height=6, profile="dome", bevel=16, gloss=0.5)
    inner = union(flame(64, 114, 24, 62, 4), c.circle(64, 96, 16))
    c.fill(inner, C(250, 196, 84), z0=5, height=4, profile="dome", bevel=10, gloss=0.6, line=0.25)
    return c.render()


def icon_crew():
    c = Canvas(128, 128)
    for cx, cy, s, tone in ((34, 60, 0.78, 0.82), (94, 60, 0.78, 0.82), (64, 52, 1.0, 1.0)):
        z = 0 if s < 1 else 6
        body = cut(c.ellipse(cx, cy + 50 * s, 30 * s, 28 * s), c.box(cx, cy + 90 * s, 80 * s, 40 * s))
        c.fill(body, GOLD, z0=z, height=6, profile="dome", bevel=12, tex=gilt(c, 7, tone), gloss=0.6)
        c.fill(c.circle(cx, cy + 4 * s, 17 * s), GOLD, z0=z + 2, height=7, profile="dome", bevel=14, tex=gilt(c, 8, tone), gloss=0.7)
    return c.render()


def icon_territory():
    c = Canvas(128, 128)
    roof = poly(c, [(8, 62), (64, 14), (120, 62), (106, 62), (64, 28), (22, 62)])
    walls = c.box(64, 84, 76, 52, 2)
    door = c.box(64, 96, 20, 28, 2)
    c.fill(cut(walls, door), GOLD, z0=0, height=5, profile="bevel", bevel=5, tex=gilt(c, 9, 0.95), gloss=0.6)
    c.fill(door, INK, z0=-1, height=0, profile="flat", line=0.6)
    c.fill(poly(c, [(4, 64), (64, 12), (124, 64), (110, 66), (64, 28), (18, 66)]), GOLD, z0=3, height=6, profile="bevel", bevel=5, tex=gilt(c, 10), gloss=0.7)
    c.fill(c.box(88, 40, 10, 22, 1), GOLD_DARK, z0=2, height=6, profile="bevel", bevel=3, tex=gilt(c, 11, 0.9), gloss=0.5)   # chimney
    return c.render()


def icon_supplies():
    c = Canvas(128, 128)
    top = [(64, 14), (116, 38), (64, 62), (12, 38)]
    left = [(12, 38), (64, 62), (64, 118), (12, 94)]
    right = [(64, 62), (116, 38), (116, 94), (64, 118)]
    c.fill(poly(c, left), GOLD, z0=0, height=4, profile="bevel", bevel=4, tex=gilt(c, 12, 0.78), gloss=0.4)
    c.fill(poly(c, right), GOLD, z0=0, height=4, profile="bevel", bevel=4, tex=gilt(c, 13, 0.62), gloss=0.4)
    c.fill(poly(c, top), GOLD, z0=1, height=4, profile="bevel", bevel=4, tex=gilt(c, 14, 1.08), gloss=0.7)
    # slats on the sides
    for k in (0.33, 0.66):
        y0 = 38 + 56 * k
        c.fill(poly(c, [(12, y0), (64, y0 + 24), (64, y0 + 27), (12, y0 + 3)]), GOLD_DARK, z0=3, height=1, profile="flat", line=0.2)
        c.fill(poly(c, [(64, y0 + 24), (116, y0), (116, y0 + 3), (64, y0 + 27)]), GOLD_DARK, z0=3, height=1, profile="flat", line=0.2)
    return c.render()


def icon_health():
    c = Canvas(128, 128)
    heart = union(c.circle(44, 48, 28), c.circle(84, 48, 28), poly(c, [(18, 58), (110, 58), (64, 114)]))
    grad = smooth(20, 114, c.y)
    col = C(226, 70, 60)[None, None, :] * (1 - grad[..., None]) + C(150, 26, 26)[None, None, :] * grad[..., None]
    c.fill(heart, col, z0=0, height=8, profile="dome", bevel=18, gloss=0.8)
    return c.render()


def icon_food():
    c = Canvas(128, 128)
    bowl = cut(c.ellipse(64, 70, 54, 44), c.box(64, 30, 140, 60))
    c.fill(bowl, GOLD, z0=0, height=6, profile="dome", bevel=12, tex=gilt(c, 51), gloss=0.6)
    c.fill(c.ellipse(64, 62, 52, 12), C(196, 64, 40), z0=4, height=3, profile="dome", bevel=6, gloss=0.5, tex=0.9 + 0.2 * fbm(c.x, c.y, 4, 2, 52))
    c.fill(c.box(64, 112, 44, 8, 3), GOLD_DARK, z0=0, height=4, profile="bevel", bevel=3, tex=gilt(c, 53, 0.9), gloss=0.5)
    for x in (48, 64, 80):
        steam = c.box(x + 4 * np.sin((c.y - 20) / 8), 32, 4, 30, 2)
        c.fill(steam, C(230, 222, 206), z0=2, height=1, profile="dome", bevel=2, line=0.15)
    return c.render()


def icon_sleep():
    c = Canvas(128, 128)
    moon = cut(c.circle(60, 66, 48), c.circle(84, 50, 42))
    c.fill(moon, GOLD, z0=0, height=7, profile="dome", bevel=16, tex=gilt(c, 54, 1.05), gloss=0.7)
    for x, y, r in ((96, 86, 6), (104, 30, 4), (80, 104, 3)):
        c.fill(c.circle(x, y, r), C(240, 220, 160), z0=2, height=3, profile="dome", bevel=r, gloss=0.9, line=0.3)
    return c.render()


def icon_ammo():
    c = Canvas(128, 128)
    for x in (40, 64, 88):
        tip = union(c.ellipse(x, 34, 11, 22), c.box(x, 50, 22, 20))
        c.fill(c.box(x, 84, 24, 56, 3), GOLD, z0=0, height=5, profile="dome", bevel=10, tex=gilt(c, 55), gloss=0.8)
        c.fill(cut(tip, c.box(x, 74, 40, 40)), C(176, 112, 70), z0=1, height=6, profile="dome", bevel=10, gloss=0.6)
    return c.render()


def icon_clans():
    c = Canvas(128, 128)
    skull = union(c.circle(64, 54, 40), c.box(64, 92, 46, 28, 6))
    holes = union(c.ellipse(48, 56, 11, 13), c.ellipse(80, 56, 11, 13), poly(c, [(64, 70), (58, 82), (70, 82)]))
    teeth = union(*[c.box(x, 104, 2.5, 16) for x in (54, 64, 74)])
    c.fill(cut(cut(skull, holes), teeth), C(222, 210, 186), z0=0, height=7, profile="dome", bevel=16, gloss=0.4,
           tex=0.95 + 0.08 * fbm(c.x, c.y, 8, 3, 56))
    c.fill(holes, C(150, 28, 26), z0=-1, height=0, profile="flat", line=0.5)
    return c.render()


def icon_rosse():
    """A pack of ROSSE: red and white, a couple of cigarettes poking out."""
    c = Canvas(128, 128)
    for x in (54, 70):
        c.fill(c.box(x, 26, 12, 30, 4), C(236, 230, 214), z0=0, height=4, profile="dome", bevel=5, line=0.4)
        c.fill(c.box(x, 18, 12, 12, 4), C(204, 150, 90), z0=1, height=4, profile="dome", bevel=5, line=0.4)
    c.fill(c.box(64, 76, 72, 88, 5), C(240, 236, 226), z0=2, height=5, profile="bevel", bevel=6, gloss=0.3)
    c.fill(c.box(64, 66, 72, 34, 2), C(196, 36, 34), z0=7, height=1, profile="flat", gloss=0.4, line=0.3)
    c.fill(c.box(64, 40, 72, 8, 2), GOLD, z0=7, height=1, profile="flat", tex=gilt(c, 57), gloss=0.6, line=0.3)
    return c.render()


def icon_dose():
    """A knotted baggie of white powder."""
    c = Canvas(128, 128)
    bag = union(c.ellipse(64, 82, 38, 34), poly(c, [(40, 66), (88, 66), (70, 34), (58, 34)]))
    c.fill(bag, C(226, 230, 234), z0=0, height=7, profile="dome", bevel=18, gloss=0.9, line=0.45)
    c.fill(c.ellipse(64, 90, 30, 22), C(246, 244, 240), z0=3, height=4, profile="dome", bevel=12, gloss=0.2, line=0.1)
    c.fill(c.box(64, 30, 26, 10, 4), GOLD, z0=4, height=4, profile="dome", bevel=5, tex=gilt(c, 58), gloss=0.7)
    return c.render()


def icon_casa():
    """The basso's door: an arched doorway in a stone surround, the lamp lit."""
    c = Canvas(128, 128)
    surround = union(c.box(64, 80, 84, 80, 4), c.circle(64, 44, 42))
    c.fill(cut(surround, c.box(64, 124, 140, 12)), GOLD, z0=0, height=6, profile="bevel", bevel=6, tex=gilt(c, 61, 0.92), gloss=0.6)
    door = union(c.box(64, 86, 52, 64, 2), c.circle(64, 54, 26))
    c.fill(cut(door, c.box(64, 124, 140, 12)), C(70, 46, 26), z0=4, height=3, profile="bevel", bevel=3,
           tex=0.85 + 0.2 * np.abs(np.sin(c.x * 0.55)), gloss=0.2, line=0.5)
    c.fill(c.box(64, 84, 3, 60), C(40, 26, 14), z0=7, height=0, profile="flat", line=0.0)
    c.fill(c.circle(76, 86, 4), GOLD, z0=8, height=3, profile="dome", bevel=4, tex=gilt(c, 62), gloss=0.9)
    c.fill(c.ellipse(64, 50, 9, 11), C(255, 214, 120), z0=8, height=4, profile="dome", bevel=8, gloss=0.8, line=0.3)
    return c.render()


def icon_piazza():
    """A fedora: what the street knows you by."""
    c = Canvas(128, 128)
    brim = c.ellipse(64, 88, 58, 18)
    c.fill(brim, C(52, 40, 30), z0=0, height=4, profile="dome", bevel=8, gloss=0.25, tex=0.9 + 0.15 * fbm(c.x, c.y, 6, 2, 63))
    crown = union(c.ellipse(64, 60, 36, 30), c.box(64, 74, 70, 22, 6))
    crown = cut(crown, c.ellipse(64, 32, 20, 8))   # the pinch on top
    c.fill(crown, C(64, 50, 38), z0=3, height=8, profile="dome", bevel=16, gloss=0.25, tex=0.9 + 0.15 * fbm(c.x, c.y, 6, 2, 64))
    c.fill(c.box(64, 78, 72, 9, 3), GOLD, z0=9, height=2, profile="dome", bevel=3, tex=gilt(c, 65), gloss=0.8, line=0.4)
    return c.render()


def icon_phone():
    """An old mobile with an aerial: the family phone."""
    c = Canvas(128, 128)
    c.fill(c.box(84, 22, 8, 28, 4), C(40, 38, 40), z0=0, height=4, profile="dome", bevel=4, gloss=0.5)
    c.fill(c.box(64, 70, 54, 100, 12), C(36, 34, 36), z0=0, height=7, profile="dome", bevel=12, gloss=0.6)
    rim = cut(c.box(64, 70, 54, 100, 12), c.box(64, 70, 48, 94, 10))
    c.fill(rim, GOLD, z0=6, height=2, profile="dome", bevel=2, tex=gilt(c, 66), gloss=0.7, line=0.3)
    c.fill(c.box(64, 46, 38, 30, 3), C(150, 178, 110), z0=7, height=0, profile="flat", gloss=0.9, line=0.5)
    for r in range(3):
        for k in range(3):
            c.fill(c.box(50 + k * 14, 78 + r * 11, 10, 7, 2), GOLD, z0=7, height=2, profile="dome", bevel=3, tex=gilt(c, 67 + r, 0.9), gloss=0.6, line=0.3)
    return c.render()


def icon_map():
    """A folded street map with a pin."""
    c = Canvas(128, 128)
    panels = [[(14, 30), (48, 20), (48, 104), (14, 114)], [(48, 20), (80, 30), (80, 114), (48, 104)], [(80, 30), (114, 20), (114, 104), (80, 114)]]
    for i, pts in enumerate(panels):
        c.fill(poly(c, pts), C(226, 206, 160), z0=0, height=2, profile="bevel", bevel=2, tex=(0.92 if i == 1 else 1.0) * grime(c, 0.3, 8, 70 + i), gloss=0.1)
    roads = union(c.box(64, 64, 100, 4), c.box(40, 64, 4, 80, 0, ang=12), c.box(94, 60, 4, 70, 0, ang=-8))
    c.fill(roads, C(176, 140, 90), z0=2, height=0, profile="flat", clip=poly(c, [(14, 30), (48, 20), (80, 30), (114, 20), (114, 104), (80, 114), (48, 104), (14, 114)]), line=0.0)
    pin = union(c.circle(78, 44, 13), poly(c, [(68, 50), (88, 50), (78, 72)]))
    c.fill(pin, C(200, 46, 38), z0=4, height=6, profile="dome", bevel=10, gloss=0.8)
    c.fill(c.circle(78, 43, 5), C(250, 240, 220), z0=10, height=1, profile="flat", line=0.2)
    return c.render()


def icon_build():
    """A mason's hammer across a trowel."""
    c = Canvas(128, 128)
    c.fill(c.box(64, 70, 12, 96, 5, ang=-35), C(122, 80, 44), z0=0, height=5, profile="dome", bevel=6,
           tex=0.88 + 0.18 * np.abs(np.sin((c.x + c.y) * 0.7)), gloss=0.3)
    head = c.box(40, 38, 52, 22, 4, ang=-35)
    c.fill(head, GOLD, z0=4, height=7, profile="bevel", bevel=6, tex=gilt(c, 71), gloss=0.8)
    return c.render()


def icon_help():
    """A gold medallion with a question mark."""
    c = Canvas(128, 128)
    c.fill(c.circle(64, 64, 52), GOLD_DARK, z0=0, height=6, profile="dome", bevel=12, tex=gilt(c, 72, 0.85), gloss=0.6)
    q = union(cut(c.ring(64, 48, 18, 10), c.box(46, 58, 30, 22)), c.box(66, 72, 10, 16, 2), c.circle(66, 94, 6))
    c.fill(q, GOLD, z0=6, height=4, profile="bevel", bevel=3, tex=gilt(c, 73, 1.1), gloss=0.9)
    return c.render()


# ---------- frames: 9-slice ----------

def panel_frame():
    """160 px square; slice 28 px each side. Dark worn leather inside, a gilded double rule and corner bosses."""
    S = 160
    c = Canvas(S, S)
    leather = (0.94 + 0.08 * fbm(c.x, c.y, 26, 3, 21)) * grime(c, 0.1, 30, 22)
    c.fill(c.box(S / 2, S / 2, S - 4, S - 4, 6), C(13, 11, 9), z0=0, height=1, profile="bevel", bevel=6, tex=leather, line=0.0)
    rule = cut(c.box(S / 2, S / 2, S - 10, S - 10, 5), c.box(S / 2, S / 2, S - 20, S - 20, 3))
    c.fill(rule, GOLD, z0=1, height=3, profile="dome", bevel=3, tex=gilt(c, 23, 0.95), gloss=0.7, line=0.5)
    hair = cut(c.box(S / 2, S / 2, S - 30, S - 30, 3), c.box(S / 2, S / 2, S - 34, S - 34, 2))
    c.fill(hair, GOLD_DARK, z0=1, height=1, profile="flat", tex=gilt(c, 24, 0.85), gloss=0.3, line=0.2)
    for x in (9, S - 9):
        for y in (9, S - 9):
            c.fill(c.box(x, y, 14, 14, 1, ang=45), GOLD, z0=2, height=4, profile="bevel", bevel=4, tex=gilt(c, 25), gloss=0.8)
    return c.render(shadow=False)


def button(state):
    """96x64; slice 16 px. Near-black inside always; normal: a dim gold edge; hover: brighter; active: a bright double rim."""
    W, H = 96, 64
    c = Canvas(W, H)
    base = C(16, 13, 10) if state == "normal" else C(24, 19, 14) if state == "hover" else C(28, 21, 13)
    tex = (0.95 + 0.06 * fbm(c.x, c.y, 18, 2, 33)) * (1.12 - 0.3 * smooth(0, H, c.y))
    c.fill(c.box(W / 2, H / 2, W - 4, H - 4, 5), base, z0=0, height=2, profile="bevel", bevel=5, tex=tex, gloss=0.1, line=0.0)
    rim = 4 if state == "active" else 3
    edge = cut(c.box(W / 2, H / 2, W - 4, H - 4, 5), c.box(W / 2, H / 2, W - 4 - rim * 2, H - 4 - rim * 2, 4))
    tone = {"normal": 0.62, "hover": 0.9, "active": 1.15}[state]
    c.fill(edge, GOLD, z0=2, height=2, profile="dome", bevel=2, tex=gilt(c, 34, tone), gloss=0.7, line=0.3)
    if state == "active":
        hair = cut(c.box(W / 2, H / 2, W - 16, H - 16, 3), c.box(W / 2, H / 2, W - 19, H - 19, 2))
        c.fill(hair, C(240, 200, 120), z0=1, height=1, profile="flat", line=0.0)
    return c.render(shadow=False)


def bar_strip():
    """The top and bottom bars: 64x64, dark with a gilded rule along the bottom edge; slice 8 left/right, 0 top, 10 bottom."""
    S = 64
    c = Canvas(S, S)
    tex = (0.95 + 0.08 * fbm(c.x, c.y, 20, 3, 41)) * (1.15 - 0.4 * smooth(0, S, c.y))
    c.fill(c.box(S / 2, S / 2, S + 8, S + 8), C(14, 12, 9), z0=0, height=0, profile="flat", tex=tex, line=0.0)
    c.fill(c.box(S / 2, S - 4, S + 8, 3), GOLD, z0=1, height=2, profile="dome", bevel=1.5, tex=gilt(c, 42, 0.9), gloss=0.6, line=0.2)
    return c.render(shadow=False)


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, fn in (("icon_money", icon_money), ("icon_respect", icon_respect), ("icon_heat", icon_heat),
                     ("icon_crew", icon_crew), ("icon_territory", icon_territory), ("icon_supplies", icon_supplies),
                     ("icon_health", icon_health), ("icon_food", icon_food), ("icon_sleep", icon_sleep), ("icon_ammo", icon_ammo),
                     ("icon_clans", icon_clans), ("icon_rosse", icon_rosse), ("icon_dose", icon_dose),
                     ("icon_casa", icon_casa), ("icon_piazza", icon_piazza), ("icon_phone", icon_phone), ("icon_map", icon_map),
                     ("icon_build", icon_build), ("icon_help", icon_help)):
        save(fn(), out, name)
    save(panel_frame(), out, "frame_panel")
    for st in ("normal", "hover", "active"):
        save(button(st), out, f"button_{st}")
    save(bar_strip(), out, "bar_strip")
