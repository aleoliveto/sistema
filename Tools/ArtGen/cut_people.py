"""
Cuts the people sheets made from ART_PROMPTS_PEOPLE.md (4x4 grids, transparent) into the game's parts.

  torsos + arms -> Resources/Characters/<prefix>body_NN_torso.png   the armless torso, on a 256 canvas
                   <prefix>body_NN_arml.png / _armr.png                 the arm hanging from its shoulder, same canvas
                   <prefix>body_NN.png                                  torso and both arms together (the still figure)
                   arms.txt                                             "<key> <shoulder row> <left pivot x> <right pivot x>"

Every torso is scaled to the old painted bodies' size (about 196 px across on 256) and centred on its
neck opening, so the heads and hair sit where they always have. The arm is scaled to a set length and hung
with its rounded top at the torso's shoulder, under the sleeve; the right arm is the left one mirrored.
All the sizes and nudges live in people_fit.json (edit it by hand, or with fit_people.py and its sliders).
Run: python3 cut_people.py          (re-cuts every sheet into Assets/_Project/Resources/Characters)
"""
import json
import os
import sys
import numpy as np
from PIL import Image

CANVAS = 256
BODY_W = 196            # the old bodies' width on the canvas
SHOULDER_TOP = 86      # the old bodies' top of the shoulders on the canvas: the heads were made to sit over it
ARM_THICK = 0.23        # arm thickness, as a fraction of the torso's width (the old painted arms)
ARM_HIDDEN = 0.30       # how much of the arm's top is tucked under the sleeve


def cells(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    cw, ch = w / 4, h / 4
    for r in range(4):
        for c in range(4):
            box = (round(c * cw), round(r * ch), round((c + 1) * cw), round((r + 1) * ch))
            cell = np.array(im.crop(box))
            # Neighbours sometimes poke over the border: clear a thin margin all round.
            m = max(2, int(cell.shape[0] * 0.035))
            cell[:m, :, 3] = 0
            cell[-m:, :, 3] = 0
            cell[:, :m, 3] = 0
            cell[:, -m:, 3] = 0
            yield Image.fromarray(main_piece(cell))


def main_piece(a, thr=30):
    """Keep the cell's own item: drop the pieces that poke in from a neighbouring cell (they touch the
    cell's border and are smaller than the main one). A flood fill on the alpha mask."""
    from collections import deque
    h, w = a.shape[:2]
    on = a[..., 3] > thr
    label = np.zeros((h, w), np.int32)
    sizes, touches = [0], [False]
    n = 0
    for y0 in range(h):
        row = on[y0]
        for x0 in np.where(row & (label[y0] == 0))[0]:
            if label[y0, x0]:
                continue
            n += 1
            q = deque([(y0, x0)])
            label[y0, x0] = n
            size, touch = 0, False
            while q:
                y, x = q.popleft()
                size += 1
                if y < 3 or x < 3 or y >= h - 3 or x >= w - 3:
                    touch = True
                for yy, xx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                    if 0 <= yy < h and 0 <= xx < w and on[yy, xx] and not label[yy, xx]:
                        label[yy, xx] = n
                        q.append((yy, xx))
            sizes.append(size)
            touches.append(touch)
    if n == 0:
        return a
    biggest = int(np.argmax(sizes))
    keep = np.zeros(n + 1, bool)
    for k in range(1, n + 1):
        # The main piece always; other pieces only if they're inside the cell and not specks.
        keep[k] = k == biggest or (not touches[k] and sizes[k] > sizes[biggest] * 0.05)
    kept = keep[label] & on
    # The soft edge round the kept piece stays; everything further than a few pixels from it goes.
    near = kept.copy()
    for _ in range(4):
        near = near | np.roll(near, 1, 0) | np.roll(near, -1, 0) | np.roll(near, 1, 1) | np.roll(near, -1, 1)
    out = a.copy()
    out[..., 3] = np.where(near, a[..., 3], 0)
    return out


def bbox(a, thr=40):
    ys, xs = np.where(a[..., 3] > thr)
    if len(xs) == 0:
        return None
    return xs.min(), ys.min(), xs.max(), ys.max()


def neck_centre(a, box):
    """The flat grey neck opening: low saturation, mid value, in the upper part of the torso."""
    rgb = a[..., :3].astype(np.float32)
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    grey = (a[..., 3] > 200) & (mx - mn < 14) & (mx > 90) & (mx < 200)
    x0, y0, x1, y1 = box
    grey[: y0] = False
    grey[y0 + int((y1 - y0) * 0.6):] = False
    ys, xs = np.where(grey)
    if len(xs) < 50:
        return (x0 + x1) / 2, y0 + (y1 - y0) * 0.3
    return xs.mean(), ys.mean()


NECK_AT = (128, 121)    # where the neck opening's centre goes: under the head


def neck_on_canvas(a):
    """The neck opening on a placed torso: flat mid-grey pixels in the middle of the upper body. None if unsure."""
    r, g, b, al = (a[..., k].astype(int) for k in range(4))
    grey = (al > 200) & (abs(r - g) < 7) & (abs(g - b) < 7) & (r > 95) & (r < 175)
    grey[:, :70] = False
    grey[:, 186:] = False
    grey[170:] = False
    ys, xs = np.where(grey)
    if len(xs) < 800:
        return None
    return xs.mean(), ys.mean()


def place(img, scale, src_pt, dst_pt):
    """img scaled by scale and moved so src_pt lands on dst_pt, on a fresh canvas."""
    w, h = img.size
    s = img.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    out = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    out.alpha_composite(s, (round(dst_pt[0] - src_pt[0] * scale), round(dst_pt[1] - src_pt[1] * scale)))
    return out



HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
ART = os.path.join(PROJECT, "Assets/_Project/Art/Characters")
OUT = os.path.join(PROJECT, "Assets/_Project/Resources/Characters")
FIT_PATH = os.path.join(HERE, "people_fit.json")

# What each group of sheets is, and how it was drawn.
GROUPS = {
    "men": dict(torsos="sheet_torsos_men.png", arms="sheet_arms_men.png", hair="sheet_hair_men.png", prefix="",
                body_w=196, flip_arms=True, arm_thick=0.23, hair_turn=(), hair_scalp=(6, 7, 8, 15)),
    "women": dict(torsos="sheet_torsos_women.png", arms="sheet_arms_women.png", hair="sheet_hair_women_fit.png", prefix="f",
                  body_w=178, flip_arms=False, arm_thick=0.26, hair_turn=(9,), hair_scalp=()),
}

DEFAULT_FIT = {
    "heads": {"size": 1.0},
    "hair": {"size": 0.97, "up": 3,
             "men": {"7": {"x": 0, "y": 16, "size": 0.94}, "8": "none"},
             "women": {}},
    "arms": {"men": {"size": 1.0, "down": 0}, "women": {"size": 0.82, "down": 0}},
}


def load_fit():
    fit = json.loads(json.dumps(DEFAULT_FIT))
    if os.path.exists(FIT_PATH):
        user = json.load(open(FIT_PATH))
        for k, v in user.items():
            if isinstance(v, dict) and isinstance(fit.get(k), dict):
                fit[k].update(v)
            else:
                fit[k] = v
    return fit


_cells = {}


def sheet_cells(name):
    """The 16 cells of a sheet, cut once and kept."""
    if name not in _cells:
        _cells[name] = list(cells(os.path.join(ART, name)))
    return _cells[name]


# ---------- torso and arms ----------

def torso_and_arms(group, i, fit):
    g = GROUPS[group]
    tc, ac = sheet_cells(g["torsos"])[i], sheet_cells(g["arms"])[i]
    tb, ab = bbox(np.array(tc)), bbox(np.array(ac))
    if tb is None or ab is None:
        return None
    body_w = g["body_w"]
    scale = body_w / (tb[2] - tb[0] + 1)
    # Shoulders' top on the old bodies' line, centred; then nudged so the neck opening is right under the head.
    torso = place(tc, scale, ((tb[0] + tb[2]) / 2, tb[1]), (CANVAS / 2, SHOULDER_TOP))
    nk = neck_on_canvas(np.array(torso))
    if nk is not None:
        dx = float(np.clip(NECK_AT[0] - nk[0], -16, 16))
        dy = float(np.clip(NECK_AT[1] - nk[1], -16, 16))
        torso = place(tc, scale, ((tb[0] + tb[2]) / 2, tb[1]), (CANVAS / 2 + dx, SHOULDER_TOP + dy))
    t = np.array(torso)
    # The shoulder: a little in from the torso's outer edge, a little below the top of the shoulders.
    tb2 = bbox(t)
    row = int(tb2[1] + (tb2[3] - tb2[1]) * 0.30)
    on = np.where(t[row, :, 3] > 60)[0]
    left_edge = on.min() if len(on) else tb2[0]
    right_edge = on.max() if len(on) else tb2[2]
    # The arm: as thick as the old painted arms, times the group's size; its top tucked under the sleeve.
    af = fit["arms"].get(group, {})
    aw, ah = ab[2] - ab[0] + 1, ab[3] - ab[1] + 1
    ascale = (body_w * g["arm_thick"]) / aw * float(af.get("size", 1.0))
    arm_w = aw * ascale
    pivot_src = ((ab[0] + ab[2]) / 2, ab[1] + ah * ARM_HIDDEN)
    px = left_edge + arm_w * 0.42
    rx = right_edge - arm_w * 0.42

    def col_top(x):
        col = np.where(t[:, int(round(x)), 3] > 60)[0]
        return col.min() if len(col) else row
    drop = (pivot_src[1] - ab[1]) * ascale
    py = max(col_top(px), col_top(rx)) + 14 + drop + float(af.get("down", 0))
    # Some sheets come with the thumb on the outside (a right arm): those are mirrored for the left.
    flip = ac.transpose(Image.FLIP_LEFT_RIGHT)
    if g["flip_arms"]:
        arm_l = place(flip, ascale, (ac.width - pivot_src[0], pivot_src[1]), (px, py))
        arm_r = place(ac, ascale, pivot_src, (rx, py))
    else:
        arm_l = place(ac, ascale, pivot_src, (px, py))
        arm_r = place(flip, ascale, (ac.width - pivot_src[0], pivot_src[1]), (rx, py))
    return torso, arm_l, arm_r, py, px, rx


# ---------- walking legs: 4 rows (trousers) x 4 frames ----------

HIP_W = 116            # the trousers' width under the torso, on the 256 canvas
LEGS_TOP = 16          # where the hips sit on the legs canvas (the pivot)
FORESHORTEN = 0.28     # seen from above, legs are mostly hidden: squash them


def legs_image(cell):
    a = np.array(cell)
    b = bbox(a)
    if b is None:
        return None
    x0, y0, x1, y1 = b
    top_rows = a[y0:y0 + max(2, (y1 - y0) // 10), :, 3] > 60      # the hips' width, across the top tenth
    cols = np.where(top_rows.any(axis=0))[0]
    hip_w = cols.max() - cols.min() + 1 if len(cols) else x1 - x0
    sx = HIP_W / hip_w
    sy = sx * FORESHORTEN
    w, h = cell.size
    s = cell.resize((max(1, round(w * sx)), max(1, round(h * sy))), Image.LANCZOS)
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    hx = (cols.min() + cols.max()) / 2 if len(cols) else (x0 + x1) / 2
    canvas.alpha_composite(s, (round(CANVAS / 2 - hx * sx), round(LEGS_TOP - y0 * sy)))
    return canvas


# ---------- heads and hair, fitted to the old heads' place so the bodies' neck still fits ----------

HEAD_BOX = (73, 77, 182, 192)   # the old heads' bounding box on the canvas: ears to ears, crown to nose tip


def head_scale_ref():
    """How big ChatGPT drew a head: the median height on the heads sheet."""
    hs = [bbox(np.array(c))[3] - bbox(np.array(c))[1] + 1 for c in sheet_cells("sheet_heads.png") if bbox(np.array(c)) is not None]
    return float(np.median(hs))


def head_image(i, fit):
    cell = sheet_cells("sheet_heads.png")[i]
    b = bbox(np.array(cell))
    if b is None:
        return None
    x0, y0, x1, y1 = b
    # By height, crown to nose tip (the new heads' ears are small, so their width would make them too big).
    s = (HEAD_BOX[3] - HEAD_BOX[1]) / (y1 - y0 + 1) * float(fit["heads"].get("size", 1.0))
    return place(cell, s, ((x0 + x1) / 2, y0), ((HEAD_BOX[0] + HEAD_BOX[2]) / 2, HEAD_BOX[1]))


def no_skin(img, strong=False):
    """Hair sheets paint some scalp (balding, shaved, partings): take the skin out so any head shows through."""
    a = np.array(img).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    sat = (mx - mn) / np.maximum(mx, 1)
    if strong:   # styles painted on a bare scalp: take all the skin, leave the hair
        k = np.clip((mx - 95) / 30, 0, 1) * np.clip((sat - 0.12) / 0.1, 0, 1) * ((r > g) & (g >= b) & (r - b > 18))
    else:
        k = np.clip((mx - 140) / 30, 0, 1) * np.clip((sat - 0.25) / 0.1, 0, 1) * ((r > g) & (g > b) & (r - b > 30))
    a[..., 3] *= 1 - k
    return Image.fromarray(a.astype(np.uint8))


def hair_image(group, i, fit, ref=None):
    g = GROUPS[group]
    hf = fit["hair"]
    own = hf.get(group, {}).get(str(i), {})
    if own == "none":
        return Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    cell = sheet_cells(g["hair"])[i]
    if i in g["hair_turn"]:
        cell = cell.rotate(180)   # drawn hanging over the face: turn it so the length falls down the back
    cell = no_skin(cell, strong=i in g["hair_scalp"])
    a = np.array(cell)
    b = bbox(a)
    if b is None:
        return None
    x0, y0, x1, y1 = b
    # The skull's crown is the first row where the hair is wide (a bun or a ponytail on top is narrow).
    on = a[..., 3] > 60
    widths = on.sum(axis=1)
    wide = np.where(widths > widths.max() * 0.55)[0]
    crown = wide.min() if len(wide) else y0
    cols = np.where(on[int(np.argmax(widths))])[0]
    cx = (cols.min() + cols.max()) / 2 if len(cols) else (x0 + x1) / 2
    s = (HEAD_BOX[3] - HEAD_BOX[1]) / (ref or head_scale_ref()) * float(fit["heads"].get("size", 1.0))
    s *= float(hf.get("size", 1.0)) * float(own.get("size", 1.0))
    tx = (HEAD_BOX[0] + HEAD_BOX[2]) / 2 + float(own.get("x", 0))
    ty = HEAD_BOX[1] - float(hf.get("up", 0)) + float(own.get("y", 0))
    return place(cell, s, (cx, crown), (tx, ty))


# ---------- everything ----------

def build_all(fit=None, out=OUT):
    fit = fit or load_fit()
    os.makedirs(out, exist_ok=True)
    lines = []
    for group, g in GROUPS.items():
        for i in range(16):
            r = torso_and_arms(group, i, fit)
            if r is None:
                continue
            torso, arm_l, arm_r, py, px, rx = r
            key = f"{g['prefix']}body_{i:02d}"
            torso.save(os.path.join(out, key + "_torso.png"))
            arm_l.save(os.path.join(out, key + "_arml.png"))
            arm_r.save(os.path.join(out, key + "_armr.png"))
            whole = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
            for part in (arm_l, arm_r, torso):
                whole.alpha_composite(part)
            whole.save(os.path.join(out, key + ".png"))
            lines.append(f"{key} {int(round(py))} {int(round(px))} {int(round(rx))}")
        ref = head_scale_ref()
        for i in range(16):
            h = hair_image(group, i, fit, ref)
            if h is not None:
                h.save(os.path.join(out, f"{g['prefix']}hair_{i:02d}.png"))
    for i in range(16):
        h = head_image(i, fit)
        if h is not None:
            h.save(os.path.join(out, f"head_{i:02d}.png"))
    for i, cell in enumerate(sheet_cells("sheet_legs_walk.png")):
        img = legs_image(cell)
        if img is not None:
            r, f = divmod(i, 4)
            img.save(os.path.join(out, f"legs_{r}{f}.png"))
    # ChatGPT puts the same foot forward in both stepping frames: the second step is the first one mirrored.
    for r in range(4):
        for a_, b_ in ((0, 2), (1, 3)):
            src = os.path.join(out, f"legs_{r}{a_}.png")
            if os.path.exists(src):
                Image.open(src).transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(out, f"legs_{r}{b_}.png"))
    # arms.txt: replace these bodies' shoulder pivots, keep the others' (police, street people).
    path = os.path.join(out, "arms.txt")
    old = {}
    if os.path.exists(path):
        for l in open(path):
            f = l.split()
            if f:
                old[f[0]] = l.strip()
    for l in lines:
        old[l.split()[0]] = l
    with open(path, "w") as f:
        f.write("\n".join(old[k] for k in sorted(old)) + "\n")
    print("people cut:", len(lines), "bodies, hair, heads, legs")


if __name__ == "__main__":
    build_all()
