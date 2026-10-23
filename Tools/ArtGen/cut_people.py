"""
Cuts the people sheets made from ART_PROMPTS_PEOPLE.md (4x4 grids, transparent) into the game's parts.

  torsos + arms -> Resources/Characters/<prefix>body_NN_torso.png   the armless torso, on a 256 canvas
                   <prefix>body_NN_arml.png / _armr.png                 the arm hanging from its shoulder, same canvas
                   <prefix>body_NN.png                                  torso and both arms together (the still figure)
                   arms.txt                                             "<key> <shoulder row> <left pivot x> <right pivot x>"

Every torso is scaled to the old painted bodies' size (about 196 px across on 256) and centred on its
neck opening, so the heads and hair sit where they always have. The arm is scaled to a set length and hung
with its rounded top at the torso's shoulder, under the sleeve; the right arm is the left one mirrored.
Run: python3 cut_people.py <sheet_torsos> <sheet_arms> <out dir> <prefix> [first index] [torso width]
     [flip arms 1/0] [arm thickness]
     men: "" 0 196 1 0.23 (thumbs drawn outside); women: f 0 178 0 0.26 (thumbs drawn inside)
"""
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
            yield Image.fromarray(cell)


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


def place(img, scale, src_pt, dst_pt):
    """img scaled by scale and moved so src_pt lands on dst_pt, on a fresh canvas."""
    w, h = img.size
    s = img.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    out = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    out.alpha_composite(s, (round(dst_pt[0] - src_pt[0] * scale), round(dst_pt[1] - src_pt[1] * scale)))
    return out


def main(torsos, arms, out, prefix, first=0, body_w=BODY_W, flip_arms=True, arm_thick=ARM_THICK):
    os.makedirs(out, exist_ok=True)
    lines = []
    for i, (tc, ac) in enumerate(zip(cells(torsos), cells(arms))):
        ta = np.array(tc)
        tb = bbox(ta)
        ab = bbox(np.array(ac))
        if tb is None or ab is None:
            print("skip cell", i)
            continue
        scale = body_w / (tb[2] - tb[0] + 1)
        # Shoulders' top on the old bodies' line, centred: the heads and hair sit over the neck as they always did.
        torso = place(tc, scale, ((tb[0] + tb[2]) / 2, tb[1]), (CANVAS / 2, SHOULDER_TOP))
        t = np.array(torso)
        # The shoulder: a little in from the torso's outer edge, a little below the top of the shoulders.
        tb2 = bbox(t)
        top = tb2[1]
        row = int(top + (tb2[3] - top) * 0.30)
        on = np.where(t[row, :, 3] > 60)[0]
        left_edge = on.min() if len(on) else tb2[0]
        right_edge = on.max() if len(on) else tb2[2]
        # The arm: its own scale so it's ARM_LEN of the torso's width long; pivot near its rounded top.
        a = np.array(ac)
        aw, ah = ab[2] - ab[0] + 1, ab[3] - ab[1] + 1
        ascale = (body_w * arm_thick) / aw
        arm_w = aw * ascale
        pivot_src = ((ab[0] + ab[2]) / 2, ab[1] + ah * ARM_HIDDEN)
        px = left_edge + arm_w * 0.42
        rx = right_edge - arm_w * 0.42
        # The arm's top starts just inside the shoulder's outline at that point, so it never shows above it.
        def col_top(x):
            col = np.where(t[:, int(round(x)), 3] > 60)[0]
            return col.min() if len(col) else row
        drop = (pivot_src[1] - ab[1]) * ascale
        py = max(col_top(px), col_top(rx)) + 14 + drop
        # Some sheets come with the thumb on the outside (a right arm): those are mirrored for the left.
        flip = ac.transpose(Image.FLIP_LEFT_RIGHT)
        if flip_arms:
            arm_l = place(flip, ascale, (ac.width - pivot_src[0], pivot_src[1]), (px, py))
            arm_r = place(ac, ascale, pivot_src, (rx, py))
        else:
            arm_l = place(ac, ascale, pivot_src, (px, py))
            arm_r = place(flip, ascale, (ac.width - pivot_src[0], pivot_src[1]), (rx, py))
        key = f"{prefix}body_{first + i:02d}"
        torso.save(os.path.join(out, key + "_torso.png"))
        arm_l.save(os.path.join(out, key + "_arml.png"))
        arm_r.save(os.path.join(out, key + "_armr.png"))
        whole = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        whole.alpha_composite(arm_l)
        whole.alpha_composite(arm_r)
        whole.alpha_composite(torso)
        whole.save(os.path.join(out, key + ".png"))
        lines.append(f"{key} {int(round(py))} {int(round(px))} {int(round(rx))}")
    # Merge into arms.txt: replace these keys, keep the others.
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
    print(len(lines), "figures cut")


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] in ("legs", "heads", "hair"):
    pass
elif __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4] if len(sys.argv) > 4 else "", int(sys.argv[5]) if len(sys.argv) > 5 else 0,
         int(sys.argv[6]) if len(sys.argv) > 6 else BODY_W, (sys.argv[7] != "0") if len(sys.argv) > 7 else True,
         float(sys.argv[8]) if len(sys.argv) > 8 else ARM_THICK)


# ---------- walking legs: 4 rows (trousers) x 4 frames ----------

HIP_W = 116            # the trousers' width under the torso, on the 256 canvas
LEGS_TOP = 16          # where the hips sit on the legs canvas (the pivot)
FORESHORTEN = 0.28     # seen from above, legs are mostly hidden: squash them


def legs(sheet, out):
    for i, cell in enumerate(cells(sheet)):
        a = np.array(cell)
        b = bbox(a)
        if b is None:
            continue
        x0, y0, x1, y1 = b
        # The hips' width: across the top tenth of the figure.
        top_rows = a[y0:y0 + max(2, (y1 - y0) // 10), :, 3] > 60
        cols = np.where(top_rows.any(axis=0))[0]
        hip_w = cols.max() - cols.min() + 1 if len(cols) else x1 - x0
        sx = HIP_W / hip_w
        sy = sx * FORESHORTEN
        w, h = cell.size
        s = cell.resize((max(1, round(w * sx)), max(1, round(h * sy))), Image.LANCZOS)
        canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        hx = (cols.min() + cols.max()) / 2 if len(cols) else (x0 + x1) / 2
        canvas.alpha_composite(s, (round(CANVAS / 2 - hx * sx), round(LEGS_TOP - y0 * sy)))
        r, f = divmod(i, 4)
        canvas.save(os.path.join(out, f"legs_{r}{f}.png"))
    # ChatGPT puts the same foot forward in both stepping frames: make the second step the first one mirrored.
    for r in range(4):
        for a_, b_ in ((0, 2), (1, 3)):
            src = os.path.join(out, f"legs_{r}{a_}.png")
            if os.path.exists(src):
                Image.open(src).transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(out, f"legs_{r}{b_}.png"))
    print("legs cut")


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "legs":
    # python3 cut_people.py legs <sheet_legs_walk> <out dir>
    legs(sys.argv[2], sys.argv[3])


# ---------- heads and hair, fitted to the old heads' place so the bodies' neck still fits ----------

HEAD_BOX = (73, 77, 182, 192)   # the old heads' bounding box on the canvas: ears to ears, crown to nose tip
HAIRLINE_Y = 178                # where the old hair stops at the forehead


def fit(cell_img, box, scale=None):
    """Scale an image so its own bounding box fills `box` (by width), centred on it; or by a given scale."""
    a = np.array(cell_img)
    b = bbox(a)
    if b is None:
        return None, None
    x0, y0, x1, y1 = b
    s = scale or (box[2] - box[0]) / (x1 - x0 + 1)
    return s, b


def heads(sheet, out, prefix=""):
    for i, cell in enumerate(cells(sheet)):
        b = bbox(np.array(cell))
        if b is None:
            continue
        x0, y0, x1, y1 = b
        # By height, crown to nose tip: the new heads have smaller ears, so their width would make them too big.
        s = (HEAD_BOX[3] - HEAD_BOX[1]) / (y1 - y0 + 1)
        place(cell, s, ((x0 + x1) / 2, y0), ((HEAD_BOX[0] + HEAD_BOX[2]) / 2, HEAD_BOX[1])).save(os.path.join(out, f"{prefix}head_{i:02d}.png"))
    print("heads cut")


def no_skin(img):
    """Hair sheets paint some scalp (balding, shaved, partings): take the skin out so any head shows through."""
    a = np.array(img).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    sat = (mx - mn) / np.maximum(mx, 1)
    skin = (r > g) & (g > b) & (r - b > 30)
    k = np.clip((mx - 140) / 30, 0, 1) * np.clip((sat - 0.25) / 0.1, 0, 1) * skin
    a[..., 3] *= 1 - k
    return Image.fromarray(a.astype(np.uint8))


def hair(sheet, heads_sheet, out, prefix=""):
    # One scale for every style: the heads sheet tells how big ChatGPT drew a head.
    heights = [bbox(np.array(c))[3] - bbox(np.array(c))[1] + 1 for c in cells(heads_sheet) if bbox(np.array(c)) is not None]
    s = (HEAD_BOX[3] - HEAD_BOX[1]) / float(np.median(heights))
    for i, cell in enumerate(cells(sheet)):
        cell = no_skin(cell)
        a = np.array(cell)
        b = bbox(a)
        if b is None:
            continue
        x0, y0, x1, y1 = b
        # The skull's crown is the first row where the hair is wide (a bun or a ponytail on top is narrow).
        on = a[..., 3] > 60
        widths = on.sum(axis=1)
        wide = np.where(widths > widths.max() * 0.55)[0]
        crown = wide.min() if len(wide) else y0
        widest = int(np.argmax(widths))
        cols = np.where(on[widest])[0]
        cx = (cols.min() + cols.max()) / 2 if len(cols) else (x0 + x1) / 2
        # The crown of a head is a little rounder than the hair's widest band: start it a touch higher.
        place(cell, s, (cx, crown), ((HEAD_BOX[0] + HEAD_BOX[2]) / 2, HEAD_BOX[1] + 14)).save(os.path.join(out, f"{prefix}hair_{i:02d}.png"))
    print("hair cut")


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "heads":
    # python3 cut_people.py heads <sheet_heads> <out dir>
    heads(sys.argv[2], sys.argv[3])

if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "hair":
    # python3 cut_people.py hair <sheet_hair> <sheet_heads> <out dir> [prefix]
    hair(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5] if len(sys.argv) > 5 else "")
