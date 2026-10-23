"""
Cuts the ChatGPT world sheets (Assets/_Project/Art/World, prompts in ART_PROMPTS_WORLD.md) into game art:
furniture and shop fittings -> Resources/Props/w_*.png, cars -> Resources/Vehicles/wcar_*.png,
doors -> Resources/Doors/door_*.png, minigame pieces -> Resources/UI/Minigame/mg_*.png,
ground textures -> Resources/Ground/ground_*.png (made seamless, tiled to the game's scale).
Run: python3 cut_world.py
"""
import os
import numpy as np
from PIL import Image
from PIL import ImageFilter


# ---------- Small stand-ins for scipy.ndimage (not installed here) ----------

def label(mask, step=4):
    """Connected regions of a boolean mask: (labels, count). Flood fill on a mask shrunk by `step`
    (fast enough in plain Python), then each full-size pixel takes its block's label."""
    from collections import deque
    h, w = mask.shape
    sh, sw = (h + step - 1) // step, (w + step - 1) // step
    pad = np.zeros((sh * step, sw * step), bool)
    pad[:h, :w] = mask
    small = pad.reshape(sh, step, sw, step).any(axis=(1, 3))
    lab = np.zeros((sh, sw), np.int32)
    n = 0
    for y0, x0 in zip(*np.nonzero(small)):
        if lab[y0, x0]:
            continue
        n += 1
        lab[y0, x0] = n
        q = deque([(y0, x0)])
        while q:
            y, x = q.popleft()
            for yy, xx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= yy < sh and 0 <= xx < sw and small[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = n
                    q.append((yy, xx))
    full = np.repeat(np.repeat(lab, step, axis=0), step, axis=1)[:h, :w]
    return np.where(mask, full, 0), n


def dilate(mask, it):
    im = Image.fromarray((mask * 255).astype(np.uint8))
    for _ in range(it):
        im = im.filter(ImageFilter.MaxFilter(3))
    return np.array(im) > 0


def erode(mask, it):
    im = Image.fromarray((mask * 255).astype(np.uint8))
    for _ in range(it):
        im = im.filter(ImageFilter.MinFilter(3))
    return np.array(im) > 0


def boxes(lab, n):
    out = [None] * (n + 1)
    ys, xs = np.nonzero(lab)
    ls = lab[ys, xs]
    for i in range(1, n + 1):
        sel = ls == i
        if not sel.any():
            continue
        y, x = ys[sel], xs[sel]
        out[i] = (slice(y.min(), y.max() + 1), slice(x.min(), x.max() + 1))
    return out

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "../../Assets/_Project"))
ART = os.path.join(ROOT, "Art/World")
RES = os.path.join(ROOT, "Resources")

# sheet: (columns, rows, output folder, prefix, longest side in pixels)
SHEETS = {
    "furn_1x1_home": (4, 4, "Props", "w_home1", 256),
    "furn_1x1_shops": (4, 4, "Props", "w_shop1", 256),
    "furn_2x1_home": (2, 4, "Props", "w_home2", 400),
    "furn_2x1_shops": (2, 4, "Props", "w_shop2", 400),
    "furn_2x2_home": (3, 3, "Props", "w_home4", 400),
    "furn_2x2_shops": (3, 3, "Props", "w_shop4", 400),
    "doors": (4, 4, "Doors", "door", 256),
    "minigame": (3, 3, "UI/Minigame", "mg", 512),
    "cars_city": (4, 2, "Vehicles", "wcar", 256),
    "cars_emergency": (4, 2, "Vehicles", "wcar_em", 256),
}


# Pieces that touch something below them on the sheet: keep only this top share of the height.
TRIM_BOTTOM = {"w_shop4_04": 0.85}


def load_rgba(path):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    if a[..., 3].min() < 250:
        return a
    # Saved without transparency (a painted checkerboard): flood away the light, colourless
    # background from the borders, stopping at anything with colour or shade.
    rgb = a[..., :3].astype(int)
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    bg_like = (spread < 10) & (rgb.min(axis=2) > 185)
    lab, _ = label(bg_like)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(edge))
    # Checker squares enclosed by the object's outline (gaps between legs, under an umbrella) too:
    # a big enough pocket of pure background is background.
    sizes = np.bincount(lab.ravel())
    big = np.where(sizes > 1500)[0]
    bg |= np.isin(lab, [b for b in big if b != 0])
    bg = dilate(erode(bg, 1), 1)
    out = a.copy()
    out[..., 3] = np.where(bg, 0, 255).astype(np.uint8)
    return out


def pieces(a, cols, rows):
    """Everything on the sheet, grouped by the grid cell each piece's middle falls in."""
    h, w = a.shape[:2]
    solid = a[..., 3] > 40
    lab, n = label(dilate(solid, 1), step=2)
    cell_area = (w * h) / (cols * rows)
    # Which grid cell every pixel is in.
    cell_of = (np.minimum(rows - 1, (np.arange(h) * rows) // h)[:, None] * cols +
               np.minimum(cols - 1, (np.arange(w) * cols) // w)[None, :])
    owner = np.full((h, w), -1, np.int32)
    for i, sl in enumerate(boxes(lab, n)):
        if sl is None:
            continue
        mask = (lab[sl] == i) & solid[sl]
        area = mask.sum()
        if area < cell_area * 0.004:
            continue   # specks
        cells = cell_of[sl][mask]
        counts = np.bincount(cells, minlength=cols * rows)
        main = int(counts.argmax())
        # Two things touching across a cell line (a palm's leaves on the fridge, two cars' mirrors):
        # each cell with a real share of it keeps its own part.
        own = [c for c in np.nonzero(counts)[0] if counts[c] > cell_area * 0.06]
        region = owner[sl]
        for c in np.nonzero(counts)[0]:
            region[mask & (cell_of[sl] == c)] = c if c in own else main
    out = {}
    for k in np.unique(owner):
        if k < 0:
            continue
        keep = (owner == k) & solid
        # Only this cell's own pieces: drop slivers of a neighbour cut off at the cell line.
        plab, pn = label(keep, step=2)
        if pn > 1:
            sizes = np.bincount(plab.ravel())[1:]
            big = [j + 1 for j, v in enumerate(sizes) if v >= sizes.max() * 0.3]
            keep &= np.isin(plab, big)
        ys, xs = np.nonzero(keep)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        crop = a[y0:y1, x0:x1].copy()
        crop[..., 3] = np.where(keep[y0:y1, x0:x1], crop[..., 3], 0)
        out[k] = Image.fromarray(crop)
    return out


def fit(im, longest):
    s = longest / max(im.size)
    if s < 1:
        im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    return im


def cut_sheets():
    made = []
    for name, (cols, rows, folder, prefix, longest) in SHEETS.items():
        path = os.path.join(ART, name + ".png")
        if not os.path.exists(path):
            continue
        a = load_rgba(path)
        found = pieces(a, cols, rows)
        os.makedirs(os.path.join(RES, folder), exist_ok=True)
        if name.startswith("cars"):
            # Cars keep their sizes against each other: all on one canvas height, the longest car's.
            tallest = max(p.height for p in found.values())
            scale = longest / tallest
            for k, p in sorted(found.items()):
                im = p.resize((max(1, round(p.width * scale)), max(1, round(p.height * scale))), Image.LANCZOS)
                canvas = Image.new("RGBA", (im.width, longest), (0, 0, 0, 0))
                canvas.alpha_composite(im, (0, (longest - im.height) // 2))
                out = os.path.join(RES, folder, f"{prefix}_{k:02d}.png")
                canvas.save(out)
                made.append(out)
            continue
        for k, p in sorted(found.items()):
            out = os.path.join(RES, folder, f"{prefix}_{k:02d}.png")
            keep = TRIM_BOTTOM.get(f"{prefix}_{k:02d}")
            if keep:
                p = p.crop((0, 0, p.width, round(p.height * keep)))
                p = p.crop(p.getbbox())
            fit(p, longest).save(out)
            made.append(out)
        # The church pews come in pairs: one bench is a pew.
        if name == "furn_2x2_shops" and 8 in found:
            pair = found[8]
            al = np.array(pair)[..., 3] > 40
            window = range(int(pair.height * 0.35), int(pair.height * 0.65))
            gap = min(window, key=lambda y: al[y].sum())
            one = pair.crop((0, 0, pair.width, gap))
            one = one.crop(one.getbbox())
            out = os.path.join(RES, folder, f"{prefix}_08_one.png")
            fit(one, longest).save(out)
            made.append(out)
        # The two single beds side by side: each half is a single bed.
        if name == "furn_2x2_home" and 2 in found:
            twin = found[2]
            al = np.array(twin)[..., 3] > 40
            cols_on = al.any(axis=0)
            mid = twin.width // 2
            # The gap between the beds: the emptiest column near the middle.
            window = range(int(twin.width * 0.4), int(twin.width * 0.6))
            gap = min(window, key=lambda x: al[:, x].sum())
            for side, box in (("l", (0, 0, gap, twin.height)), ("r", (gap, 0, twin.width, twin.height))):
                half = twin.crop(box)
                bb = half.getbbox()
                if bb:
                    half = half.crop(bb)
                out = os.path.join(RES, folder, f"{prefix}_single_{side}.png")
                fit(half, longest).save(out)
                made.append(out)
    return made


# ---------- Ground ----------

# game slot <- new texture, how many times it repeats across the game's 8-cell period
GROUND = {
    "asphalt": ("ground_asphalt_patched", 1),
    "basoli": ("ground_basoli", 3),
    "slabs": ("ground_sanpietrini", 2),
    "cotto": ("ground_cotto", 3),
    "marble": ("ground_marble", 3),
    "tar": ("ground_lastrico", 2),
    "grass": ("ground_garden", 2),
}
OUT_SIZE = 2048


def seamless(a, band=0.14):
    """Blend each edge with the picture shifted by half, so it wraps without a seam."""
    h, w = a.shape[:2]
    rolled = np.roll(np.roll(a, h // 2, axis=0), w // 2, axis=1)

    def ramp(n):
        t = np.ones(n)
        b = int(n * band)
        r = np.linspace(0, 1, b)
        t[:b] = r
        t[-b:] = r[::-1]
        return t
    m = np.minimum.outer(ramp(h), ramp(w))[..., None]
    return a * m + rolled * (1 - m)


def mean_lum(a):
    return float((a[..., :3] * [0.3, 0.59, 0.11]).sum(axis=2).mean())


def make_ground():
    made = []
    for slot, (src, reps) in GROUND.items():
        path = os.path.join(ART, src + ".png")
        if not os.path.exists(path):
            continue
        a = np.asarray(Image.open(path).convert("RGB")).astype(float)
        a = seamless(a)
        # Same overall brightness as the texture it replaces, so the lighting still reads.
        old = os.path.join(RES, "Ground", f"ground_{slot}.png")
        orig = os.path.join(HERE, "ground_originals", f"ground_{slot}.png")
        os.makedirs(os.path.dirname(orig), exist_ok=True)
        if os.path.exists(old) and not os.path.exists(orig):
            Image.open(old).save(orig)
        if os.path.exists(orig):
            ref = mean_lum(np.asarray(Image.open(orig).convert("RGB")).astype(float))
            k = np.clip(ref / max(1.0, mean_lum(a)), 0.8, 1.45)
            a = np.clip(a * k, 0, 255)
        tile = np.tile(a, (reps, reps, 1)).astype(np.uint8)
        im = Image.fromarray(tile).resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)
        out = os.path.join(RES, "Ground", f"ground_{slot}.png")
        im.save(out)
        made.append(out)
    # Kerbstones: the middle band of the kerb strip (the stones only), stacked into the period.
    kp = os.path.join(ART, "ground_kerb.png")
    if os.path.exists(kp):
        k = Image.open(kp).convert("RGB")
        w, h = k.size
        band = k.crop((0, int(h * 0.32), w, int(h * 0.66)))
        b = seamless(np.asarray(band).astype(float), band=0.06)
        band = Image.fromarray(b.astype(np.uint8)).resize((OUT_SIZE, OUT_SIZE // 8), Image.LANCZOS)
        sheet = Image.new("RGB", (OUT_SIZE, OUT_SIZE))
        for i in range(8):
            sheet.paste(band, (0, i * OUT_SIZE // 8))
        orig = os.path.join(HERE, "ground_originals")
        for slot in ("kerb_h", "kerb_v"):
            old = os.path.join(RES, "Ground", f"ground_{slot}.png")
            if os.path.exists(old) and not os.path.exists(os.path.join(orig, f"ground_{slot}.png")):
                Image.open(old).save(os.path.join(orig, f"ground_{slot}.png"))
        # Horizontal kerb: stones run left to right (joints vertical) — as the strip is drawn.
        sheet.save(os.path.join(RES, "Ground", "ground_kerb_h.png"))
        sheet.rotate(90, expand=True).save(os.path.join(RES, "Ground", "ground_kerb_v.png"))
        made += ["ground_kerb_h", "ground_kerb_v"]
    return made


if __name__ == "__main__":
    a = cut_sheets()
    b = make_ground()
    print(f"world cut: {len(a)} pieces, {len(b)} ground textures")
