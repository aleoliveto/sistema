"""
Cuts the people sheets made from ART_PROMPTS_PEOPLE.md (4x4 grids, transparent) into the game's parts.

  torsos + arms -> Resources/Characters/<prefix>body_NN_torso.png   the armless torso, on a 256 canvas
                   <prefix>body_NN_arml.png / _armr.png                 the arm hanging from its shoulder, same canvas
                   <prefix>body_NN.png                                  torso and both arms together (the still figure)
                   arms.txt                                             "<key> <shoulder row> <left pivot x> <right pivot x>"

Every torso is scaled to the old painted bodies' size (about 196 px across on 256) and centred on its
neck opening, so the heads and hair sit where they always have. The arm is scaled to a set length and hung
with its rounded top at the torso's shoulder, under the sleeve; the right arm is the left one mirrored.
Run: python3 cut_people.py <sheet_torsos> <sheet_arms> <out dir> <prefix> [first index]
"""
import os
import sys
import numpy as np
from PIL import Image

CANVAS = 256
BODY_W = 196            # the old bodies' width on the canvas
NECK = (128, 112)       # where the head sits on the canvas
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


def main(torsos, arms, out, prefix, first=0):
    os.makedirs(out, exist_ok=True)
    lines = []
    for i, (tc, ac) in enumerate(zip(cells(torsos), cells(arms))):
        ta = np.array(tc)
        tb = bbox(ta)
        ab = bbox(np.array(ac))
        if tb is None or ab is None:
            print("skip cell", i)
            continue
        scale = BODY_W / (tb[2] - tb[0] + 1)
        nx, ny = neck_centre(ta, tb)
        torso = place(tc, scale, (nx, ny), NECK)
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
        ascale = (BODY_W * ARM_THICK) / aw
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
        # ChatGPT draws the arm with the thumb on the outside: as it comes, it's a right arm. Mirror it for the left.
        flip = ac.transpose(Image.FLIP_LEFT_RIGHT)
        arm_l = place(flip, ascale, (ac.width - pivot_src[0], pivot_src[1]), (px, py))
        arm_r = place(ac, ascale, pivot_src, (rx, py))
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


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4] if len(sys.argv) > 4 else "", int(sys.argv[5]) if len(sys.argv) > 5 else 0)
