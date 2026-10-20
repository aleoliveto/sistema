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


# ---------- frames: 9-slice ----------

def panel_frame():
    """160 px square; slice 28 px each side. Dark worn leather inside, a gilded double rule and corner bosses."""
    S = 160
    c = Canvas(S, S)
    leather = (0.92 + 0.12 * fbm(c.x, c.y, 26, 3, 21)) * grime(c, 0.18, 30, 22)
    c.fill(c.box(S / 2, S / 2, S - 4, S - 4, 6), C(24, 19, 14), z0=0, height=1, profile="bevel", bevel=6, tex=leather, line=0.0)
    rule = cut(c.box(S / 2, S / 2, S - 10, S - 10, 5), c.box(S / 2, S / 2, S - 20, S - 20, 3))
    c.fill(rule, GOLD, z0=1, height=3, profile="dome", bevel=3, tex=gilt(c, 23, 0.95), gloss=0.7, line=0.5)
    hair = cut(c.box(S / 2, S / 2, S - 30, S - 30, 3), c.box(S / 2, S / 2, S - 34, S - 34, 2))
    c.fill(hair, GOLD_DARK, z0=1, height=1, profile="flat", tex=gilt(c, 24, 0.85), gloss=0.3, line=0.2)
    for x in (9, S - 9):
        for y in (9, S - 9):
            c.fill(c.box(x, y, 14, 14, 1, ang=45), GOLD, z0=2, height=4, profile="bevel", bevel=4, tex=gilt(c, 25), gloss=0.8)
    return c.render(shadow=False)


def button(state):
    """96x64; slice 16 px. normal: dark with a gold edge; hover: warmer; active: gold."""
    W, H = 96, 64
    c = Canvas(W, H)
    if state == "active":
        fillc = C(206, 156, 74)
        tex = gilt(c, 31) * (1.08 - 0.25 * smooth(0, H, c.y))
        c.fill(c.box(W / 2, H / 2, W - 4, H - 4, 5), fillc, z0=0, height=3, profile="bevel", bevel=5, tex=tex, gloss=0.6, line=0.6)
        c.fill(cut(c.box(W / 2, H / 2, W - 10, H - 10, 3), c.box(W / 2, H / 2, W - 13, H - 13, 2)), C(250, 222, 150), z0=3, height=1, profile="flat", line=0.0)
    else:
        base = C(30, 24, 17) if state == "normal" else C(48, 37, 23)
        tex = (0.95 + 0.08 * fbm(c.x, c.y, 18, 2, 33)) * (1.12 - 0.3 * smooth(0, H, c.y))
        c.fill(c.box(W / 2, H / 2, W - 4, H - 4, 5), base, z0=0, height=2, profile="bevel", bevel=5, tex=tex, gloss=0.1, line=0.0)
        edge = cut(c.box(W / 2, H / 2, W - 4, H - 4, 5), c.box(W / 2, H / 2, W - 8, H - 8, 4))
        tone = 0.7 if state == "normal" else 1.0
        c.fill(edge, GOLD, z0=2, height=2, profile="dome", bevel=2, tex=gilt(c, 34, tone), gloss=0.6, line=0.3)
    return c.render(shadow=False)


def bar_strip():
    """The top and bottom bars: 64x64, dark with a gilded rule along the bottom edge; slice 8 left/right, 0 top, 10 bottom."""
    S = 64
    c = Canvas(S, S)
    tex = (0.95 + 0.08 * fbm(c.x, c.y, 20, 3, 41)) * (1.15 - 0.4 * smooth(0, S, c.y))
    c.fill(c.box(S / 2, S / 2, S + 8, S + 8), C(26, 20, 14), z0=0, height=0, profile="flat", tex=tex, line=0.0)
    c.fill(c.box(S / 2, S - 4, S + 8, 3), GOLD, z0=1, height=2, profile="dome", bevel=1.5, tex=gilt(c, 42, 0.9), gloss=0.6, line=0.2)
    return c.render(shadow=False)


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, fn in (("icon_money", icon_money), ("icon_respect", icon_respect), ("icon_heat", icon_heat),
                     ("icon_crew", icon_crew), ("icon_territory", icon_territory), ("icon_supplies", icon_supplies)):
        save(fn(), out, name)
    save(panel_frame(), out, "frame_panel")
    for st in ("normal", "hover", "active"):
        save(button(st), out, f"button_{st}")
    save(bar_strip(), out, "bar_strip")
