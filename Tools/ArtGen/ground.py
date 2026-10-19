"""
Seamless ground materials for the map, 8x8 cells at 128 px per cell (1024 px, repeating).
Each is pre-lit by the same north-west sun as the painted props (relief only: the game adds
the shade, shadows and wetness on top). Run: python3 ground.py <out folder>

  ground_asphalt.png   the wider roads: worn asphalt, patches, cracks, oil
  ground_basoli.png    the vicoli: rows of dark Vesuvian lava blocks, polished by feet
  ground_kerb_h.png    piperno kerbstones, joints across (for kerbs along an east-west road)
  ground_kerb_v.png    the same turned (north-south roads)
  ground_slabs.png     piazze: big pale stone slabs in running bond
"""
import os
import sys
import numpy as np
from PIL import Image

P = 1024          # output period in pixels
SS = 2            # drawn twice the size, then scaled down
N = P * SS
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / SS   # output pixel units


# ---------- tileable noise ----------

def _lattice(seed, n):
    return np.random.default_rng(seed).random((n, n)).astype(np.float32)


def tnoise(x, y, cells, seed):
    """Value noise with `cells` lattice cells across the period: wraps exactly."""
    g = _lattice(seed, cells)
    fx, fy = x / P * cells, y / P * cells
    xi, yi = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - xi, fy - yi
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    x0, y0 = xi % cells, yi % cells
    x1, y1 = (xi + 1) % cells, (yi + 1) % cells
    a, b = g[y0, x0], g[y0, x1]
    c, d = g[y1, x0], g[y1, x1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def fbm(x, y, cells, octaves, seed):
    s, a, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        s = s + tnoise(x, y, cells * 2 ** o, seed * 7 + o) * a
        tot += a
        a *= 0.5
    return s / tot


def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def hsh(a, b, s=0):
    a = np.asarray(a).astype(np.int64)
    b = np.asarray(b).astype(np.int64)
    h = (a * 73856093) ^ (b * 19349663) ^ (s * 83492791 + 7)
    h = (h ^ (h >> 13)) * 1274126177
    return (((h ^ (h >> 16)) & 0xFFFF) / 65535.0).astype(np.float32)


def C(*v):
    return np.array(v, np.float32) / 255


def wrap_blur(a, r):
    """Box blur x3 that wraps round the edges (keeps the tile seamless)."""
    r = max(1, int(r))
    for _ in range(3):
        for ax in (0, 1):
            acc = np.zeros_like(a)
            for k in range(-r, r + 1):
                acc += np.roll(a, k, axis=ax)
            a = acc / (2 * r + 1)
    return a


# ---------- light (relief only, wraps) ----------

def light(alb, z, gloss, ink=None):
    gx = (np.roll(z, -1, 1) - np.roll(z, 1, 1)) * 0.5 * SS
    gy = (np.roll(z, -1, 0) - np.roll(z, 1, 0)) * 0.5 * SS
    n = np.dstack([-gx, -gy, np.ones_like(z)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    L = np.array([-0.5, -0.62, 0.6], np.float32)
    L /= np.linalg.norm(L)
    ndl = np.clip((n * L).sum(2), 0, 1) / L[2]
    warm, cool = np.array([1.04, 1.0, 0.94]), np.array([0.66, 0.7, 0.84])
    t = np.clip(ndl, 0, 1.3)
    lit = cool * (1 - np.minimum(t, 1))[..., None] + warm * t[..., None]
    H = L + np.array([0, 0, 1.0], np.float32)
    H /= np.linalg.norm(H)
    spec = np.clip((n * H).sum(2), 0, 1) ** 28 * gloss
    cav = np.clip(1 - np.maximum(wrap_blur(z, 1.5 * SS) - z, 0) * 0.22, 0.55, 1.05)
    ao = np.clip(1 - np.maximum(wrap_blur(z, 5 * SS) - z, 0) * 0.05, 0.6, 1)
    col = alb * lit * (cav * ao)[..., None] + spec[..., None] * 0.5
    if ink is not None:
        col = col * (1 - ink[..., None] * 0.5)
    return np.clip(col, 0, 1)


def save(col, path):
    img = Image.fromarray((col * 255).astype(np.uint8), "RGB").resize((P, P), Image.LANCZOS)
    img.save(path)


# ---------- materials ----------

def basoli():
    rows = 24
    rh = P / rows
    row = np.floor(yy / rh)
    ly = yy - row * rh
    per_row = 16
    ln = P / per_row
    off = hsh(row, 3) * ln
    wob = (fbm(xx, yy, 32, 2, 5) - 0.5) * 7
    u = xx + off + wob
    st = np.floor(u / ln)
    lx = u - st * ln
    st = st % per_row
    d = np.minimum(np.minimum(lx, ln - lx), np.minimum(ly, rh - ly)) - hsh(st, row, 9) * 1.6
    tone = 0.78 + 0.32 * hsh(st, row, 5)
    base = C(108, 105, 110) * tone[..., None]
    pits = smooth(0.8, 0.88, tnoise(xx, yy, 512, 21)) * 0.24
    grain = (fbm(xx, yy, 256, 2, 22) - 0.5) * 0.2
    polish = smooth(0.42, 0.78, fbm(xx, yy, 8, 3, 23))
    col = base * (1 + grain - pits)[..., None]
    col = col * (1 - 0.1 * polish)[..., None]
    stain = smooth(0.6, 0.85, fbm(xx, yy, 16, 4, 24)) * 0.18
    col = col * (1 - stain)[..., None]
    joint = 1 - smooth(0.5, 2.6, d)
    col = col * (1 - 0.5 * joint)[..., None] + C(76, 66, 54) * (joint * 0.3)[..., None]
    z = 4 * np.sqrt(np.clip(smooth(0, 9, d), 0, 1)) - pits * 1.3
    return light(col, z, 0.12 + 0.45 * polish, joint * 0.2)


def asphalt():
    agg = tnoise(xx, yy, 512, 31)
    base = C(64, 64, 68) * (0.88 + 0.16 * fbm(xx, yy, 16, 3, 32))[..., None]
    col = base * (0.9 + 0.2 * agg)[..., None]
    col = np.where((tnoise(xx, yy, 512, 33) > 0.86)[..., None], col * 1.35, col)     # pale stones
    patch = smooth(0.62, 0.64, fbm(xx, yy, 6, 3, 34))
    col = col * (1 - 0.14 * patch)[..., None]                                          # newer tar
    crack = (1 - smooth(0.0, 0.006, np.abs(fbm(xx, yy, 12, 4, 35) - 0.5))) * smooth(0.6, 0.7, fbm(xx, yy, 4, 2, 37))
    col = col * (1 - 0.45 * crack)[..., None]
    oil = smooth(0.66, 0.8, fbm(xx, yy, 24, 3, 36))
    col = col * (1 - 0.22 * oil)[..., None]
    z = agg * 0.9 + patch * 0.6 - crack * 1.2
    return light(col, z, 0.06 + 0.3 * oil)


def kerb(horizontal=True):
    u, v = (xx, yy) if horizontal else (yy, xx)
    ln = P / 8
    off = (fbm(u, v, 16, 2, 41) - 0.5) * 6
    k = np.floor((u + off) / ln)
    lu = (u + off) - k * ln
    d = np.minimum(lu, ln - lu)
    tone = 0.85 + 0.25 * hsh(k % 8, 1, 42)
    base = C(132, 130, 124) * tone[..., None]
    speck = (tnoise(xx, yy, 512, 43) > 0.8) * 0.3
    col = base * (1 - speck + (fbm(xx, yy, 128, 3, 44) - 0.5) * 0.2)[..., None]
    joint = 1 - smooth(0.5, 2.0, d)
    col = col * (1 - 0.55 * joint)[..., None]
    z = 3 * smooth(0, 5, d) + (fbm(xx, yy, 256, 2, 45) - 0.5) * 0.6
    return light(col, z, 0.08, joint * 0.2)


def slabs():
    n = 12
    s = P / n
    col_i = np.floor(xx / s)
    v = yy + (col_i % 2) * s / 2
    row_i = np.floor(v / s)
    lx, ly = xx - col_i * s, v - row_i * s
    row_i = row_i % n
    d = np.minimum(np.minimum(lx, s - lx), np.minimum(ly, s - ly))
    tone = 0.82 + 0.24 * hsh(col_i, row_i, 51)
    base = C(170, 164, 152) * tone[..., None]
    veins = 0
    col = base * (1 + (fbm(xx, yy, 128, 3, 53) - 0.5) * 0.14 - veins * 0.12)[..., None]
    worn = smooth(0.5, 0.8, fbm(xx, yy, 4, 3, 54))
    col = col * (1 + 0.08 * worn)[..., None]
    dirt = smooth(0.62, 0.85, fbm(xx, yy, 16, 4, 55)) * 0.16
    col = col * (1 - dirt)[..., None]
    joint = 1 - smooth(0.5, 2.0, d)
    col = col * (1 - 0.45 * joint)[..., None]
    z = 2 * smooth(0, 4, d)
    return light(col, z, 0.1 + 0.3 * worn, joint * 0.2)


def coppi():
    """Curved terracotta tiles in columns down the slope: caps and channels, lichen, soot."""
    cols = 46
    pw = P / cols
    k = np.floor(xx / pw)
    fu = xx / pw - k
    cap = (k % 2 == 0)
    prof = np.where(cap, np.sin(fu * np.pi), -0.5 * np.sin(fu * np.pi))
    rows = 26
    off = hsh(k % cols, 0, 31) * 0.5
    v = yy / (P / rows) + off
    fv = v - np.floor(v)
    row = np.floor(v) % rows
    z = 6 + prof * 5 + (1 - fv) * 2.4
    tone = 0.82 + 0.28 * hsh(k % cols, row, 32)
    col = C(182, 96, 62) * tone[..., None]
    col = np.where(cap[..., None], col, col * 0.8)
    col = np.where((hsh(k % cols, row, 35) < 0.04)[..., None], col * 1.18, col)            # a newer tile
    lichen = smooth(0.66, 0.78, fbm(xx, yy, 32, 4, 33)) * 0.55
    col = col * (1 - lichen)[..., None] + C(178, 172, 128) * lichen[..., None]
    soot = smooth(0.55, 0.85, fbm(xx, yy, 8, 3, 34)) * 0.25
    col = col * (1 - soot)[..., None]
    lip = (fv > 0.93) * 0.35
    return light(col, z, 0.1, lip)


def tar():
    """Bitumen sheeting on a flat terrace: strips with lapped seams, patches of silver paint."""
    strip = P / 8
    k = np.floor(yy / strip)
    fy = yy - k * strip
    seam = smooth(strip - 8, strip - 2, fy)
    base = C(98, 96, 98) * (0.85 + 0.2 * fbm(xx, yy, 16, 4, 61))[..., None] * (0.93 + 0.12 * hsh(k, 0, 62))[..., None]
    grit = (tnoise(xx, yy, 512, 63) > 0.75) * 0.12
    col = base * (1 + grit)[..., None]
    silver = smooth(0.7, 0.73, fbm(xx, yy, 16, 4, 64)) * 0.8
    col = col * (1 - silver)[..., None] + C(150, 150, 146) * (0.85 + 0.2 * fbm(xx, yy, 64, 2, 65))[..., None] * silver[..., None]
    bubble = smooth(0.78, 0.84, fbm(xx, yy, 32, 3, 66))
    z = seam * 1.2 + bubble * 1.5 + grit
    return light(col, z, 0.15 + 0.2 * silver, seam * 0.15)


def cotto():
    n = 42
    s = P / n
    tx, ty = np.floor(xx / s), np.floor(yy / s)
    lx, ly = xx - tx * s, yy - ty * s
    tx, ty = tx % n, ty % n
    d = np.minimum(np.minimum(lx, s - lx), np.minimum(ly, s - ly))
    base = C(190, 118, 84) * (0.84 + 0.26 * hsh(tx, ty, 71))[..., None]
    base = np.where((hsh(tx, ty, 72) < 0.05)[..., None], C(150, 86, 62), base)
    bleach = smooth(0.4, 0.85, fbm(xx, yy, 8, 3, 73)) * 0.22
    base = base * (1 - bleach)[..., None] + C(222, 200, 176) * bleach[..., None]
    col = base * (1 + (fbm(xx, yy, 256, 2, 74) - 0.5) * 0.16)[..., None]
    grout = 1 - smooth(0.4, 1.4, d)
    col = col * (1 - grout)[..., None] + C(150, 138, 122) * grout[..., None]
    z = 1.4 * smooth(0, 2.5, d)
    return light(col, z, 0.06, grout * 0.2)


def cement():
    """Grey cement-and-grit tiles (marmette), stained and chipped."""
    n = 32
    s = P / n
    tx, ty = np.floor(xx / s), np.floor(yy / s)
    lx, ly = xx - tx * s, yy - ty * s
    tx, ty = tx % n, ty % n
    d = np.minimum(np.minimum(lx, s - lx), np.minimum(ly, s - ly))
    base = C(176, 172, 164) * (0.86 + 0.2 * hsh(tx, ty, 81))[..., None]
    chips = (tnoise(xx, yy, 512, 82) > 0.72) * 0.18 - (tnoise(xx, yy, 512, 83) > 0.8) * 0.15
    stain = smooth(0.55, 0.85, fbm(xx, yy, 8, 4, 84)) * 0.25
    col = base * (1 + chips - stain)[..., None]
    grout = 1 - smooth(0.4, 1.4, d)
    col = col * (1 - 0.4 * grout)[..., None]
    z = 1.2 * smooth(0, 2.5, d)
    return light(col, z, 0.05, grout * 0.2)


def plaster():
    """
    Facade plaster, nearly white so the game can tint it with each building's colour: mottled,
    streaked by rain, cracked, and fallen away in places to the yellow tuff blocks underneath.
    """
    mott = (fbm(xx, yy, 8, 5, 91) - 0.5) * 0.16
    streak = (fbm(xx, yy * 0.125, 64, 3, 92) - 0.5) * 0.12
    col = np.ones((N, N, 3), np.float32) * (0.93 + mott + streak)[..., None]
    soot = smooth(0.62, 0.9, fbm(xx, yy, 16, 4, 93)) * 0.18
    col = col * (1 - soot)[..., None]
    crack = (1 - smooth(0.0, 0.005, np.abs(fbm(xx, yy, 8, 5, 94) - 0.5))) * smooth(0.55, 0.65, fbm(xx, yy, 4, 2, 95))
    col = col * (1 - 0.4 * crack)[..., None]
    # bare patches: courses of tuff blocks (bricks 32 x 16 px)
    peel = smooth(0.7, 0.72, fbm(xx, yy, 8, 5, 96))
    bh, bw = 16.0, 32.0
    row = np.floor(yy / bh)
    bx = np.floor((xx + (row % 2) * bw / 2) / bw)
    lx = (xx + (row % 2) * bw / 2) - bx * bw
    ly = yy - row * bh
    mortar = 1 - smooth(0.5, 1.6, np.minimum(np.minimum(lx, bw - lx), np.minimum(ly, bh - ly)))
    tuff = C(214, 186, 128) * (0.85 + 0.22 * hsh(bx % 32, row % 64, 97))[..., None] * (1 - 0.35 * mortar)[..., None]
    col = col * (1 - peel)[..., None] + tuff * peel[..., None] / C(214, 160, 120)    # undo most of the tint
    edge = peel * (1 - peel) * 4
    col = col * (1 - 0.25 * edge)[..., None]
    z = (1 - peel) * 2 + (fbm(xx, yy, 128, 3, 98) - 0.5) * 0.6 - crack
    return light(np.clip(col, 0, 1.2) * 0.98, z, 0.03)


def grass():
    """Unkempt grass in a courtyard garden: clumps, blades catching the sun, bare worn patches."""
    clump = fbm(xx, yy, 32, 4, 101)
    blades = tnoise(xx, yy, 512, 102)
    col = C(74, 112, 52) * (0.7 + 0.45 * clump)[..., None]
    col = col * (0.8 + 0.35 * blades)[..., None]
    dry = smooth(0.55, 0.75, fbm(xx, yy, 8, 3, 103)) * 0.45
    col = col * (1 - dry)[..., None] + C(150, 136, 82) * dry[..., None]
    worn = smooth(0.74, 0.8, fbm(xx, yy, 4, 3, 104))
    col = col * (1 - worn)[..., None] + C(124, 106, 80) * (0.85 + 0.2 * blades)[..., None] * worn[..., None]
    flowers = (tnoise(xx, yy, 512, 105) > 0.965) * (1 - worn)
    col = np.where(flowers[..., None] > 0, C(236, 226, 120), col)
    z = blades * 1.6 + clump * 2 - worn * 1.5
    return light(col, z, 0.04)


def earth():
    """Packed earth with grit and pebbles, rutted by feet and wheels."""
    base = C(124, 106, 82) * (0.82 + 0.3 * fbm(xx, yy, 16, 4, 111))[..., None]
    grit = tnoise(xx, yy, 512, 112)
    col = base * (0.88 + 0.22 * grit)[..., None]
    peb = smooth(0.82, 0.86, tnoise(xx, yy, 256, 113))
    col = col * (1 - peb)[..., None] + C(168, 158, 140) * (0.8 + 0.3 * grit)[..., None] * peb[..., None]
    damp = smooth(0.6, 0.8, fbm(xx, yy, 8, 3, 114)) * 0.2
    col = col * (1 - damp)[..., None]
    z = grit * 0.8 + peb * 2.5
    return light(col, z, 0.03)


def tiles():
    """Glazed floor tiles of an old basso, nearly white: the game tints them per home (half-cell grid)."""
    n = 16
    s = P / n
    tx, ty = np.floor(xx / s), np.floor(yy / s)
    lx, ly = xx - tx * s, yy - ty * s
    tx, ty = tx % n, ty % n
    d = np.minimum(np.minimum(lx, s - lx), np.minimum(ly, s - ly))
    col = np.ones((N, N, 3), np.float32) * (0.93 + 0.08 * hsh(tx, ty, 121))[..., None]
    col = col * (1 + (fbm(xx, yy, 128, 3, 122) - 0.5) * 0.08)[..., None]
    wear = smooth(0.55, 0.85, fbm(xx, yy, 8, 3, 123)) * 0.12
    col = col * (1 - wear)[..., None]
    chip = (tnoise(xx, yy, 512, 124) > 0.97) * 0.25
    col = col * (1 - chip)[..., None]
    grout = 1 - smooth(0.6, 2.2, d)
    col = col * (1 - 0.45 * grout)[..., None]
    z = 1.2 * smooth(0, 3, d)
    return light(col, z, 0.35 * (1 - wear * 4), grout * 0.15)


def marble():
    """Polished white marble in big slabs, grey veins."""
    n = 8
    s = P / n
    tx, ty = np.floor(xx / s), np.floor(yy / s)
    lx, ly = xx - tx * s, yy - ty * s
    d = np.minimum(np.minimum(lx, s - lx), np.minimum(ly, s - ly))
    v = fbm(xx, yy, 8, 5, 131)
    vein = np.exp(-np.abs(v - 0.5) * 60) * 0.35 + np.exp(-np.abs(fbm(xx, yy, 16, 4, 132) - 0.5) * 90) * 0.2
    col = C(232, 228, 220) * (0.95 + 0.06 * hsh(tx % n, ty % n, 133))[..., None]
    col = col * (1 - vein)[..., None] + C(120, 120, 126) * vein[..., None]
    joint = 1 - smooth(0.5, 1.5, d)
    col = col * (1 - 0.3 * joint)[..., None]
    return light(col, 0.6 * smooth(0, 2, d), 0.7)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in (("basoli", basoli), ("asphalt", asphalt), ("kerb_h", lambda: kerb(True)),
                     ("kerb_v", lambda: kerb(False)), ("slabs", slabs), ("coppi", coppi), ("tar", tar),
                     ("cotto", cotto), ("cement", cement), ("plaster", plaster),
                     ("grass", grass), ("earth", earth), ("tiles", tiles), ("marble", marble)):
        save(fn(), os.path.join(out, f"ground_{name}.png"))
        print(name)
