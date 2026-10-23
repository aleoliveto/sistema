"""
Cuts each painted body (Resources/Characters/*body_NN.png) into a torso and two arms, so the arms can
swing when the figure walks. The bodies are painted from above with the arms hanging at the sides:
below the shoulder line, whatever lies outside the torso's own width (measured at the trousers) is arm.
Writes <name>_torso.png, <name>_arml.png, <name>_armr.png next to the originals (same 256 canvas and
centre, so they line up when drawn on top of each other), plus the shoulder pivot rows in arms.txt.
Run: python3 split_bodies.py <Resources/Characters>
"""
import os
import sys
import glob
import numpy as np
from PIL import Image

SHOULDER = 0.56   # rows below this fraction of the canvas can be arm
ARM = 0.17        # arm width, as a fraction of the canvas


def runs(row):
    on = row > 96
    out, start = [], None
    for x, v in enumerate(on):
        if v and start is None:
            start = x
        if not v and start is not None:
            out.append((start, x - 1))
            start = None
    if start is not None:
        out.append((start, len(on) - 1))
    return out


def split(path, out):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    h, w = a.shape[:2]
    alpha = a[..., 3]
    cx = w // 2
    # Arms are a strip about a sixth of the canvas wide along the outer edge of each row, below the shoulder.
    arm_w = int(w * ARM)
    top = int(h * SHOULDER)
    yy, xx = np.mgrid[0:h, 0:w]
    on = alpha > 96
    has = on.any(axis=1)
    x0 = np.where(has, on.argmax(axis=1), cx)
    x1 = np.where(has, w - 1 - on[:, ::-1].argmax(axis=1), cx)
    # Smooth the edges down the rows so a wobble in the painting doesn't make a ragged cut.
    k = np.ones(9) / 9
    x0 = np.convolve(np.pad(x0, 4, mode="edge"), k, "valid")
    x1 = np.convolve(np.pad(x1, 4, mode="edge"), k, "valid")
    fade = np.clip((yy - top) / 10.0, 0, 1)
    edge = 3.0
    arm_l = np.clip((x0[:, None] + arm_w - xx) / edge, 0, 1) * fade
    arm_r = np.clip((xx - (x1[:, None] - arm_w)) / edge, 0, 1) * fade
    left, right = int(np.median(x0[top:]) + arm_w), int(np.median(x1[top:]) - arm_w)
    base = os.path.splitext(os.path.basename(path))[0]

    def save(mask, suffix):
        b = a.copy()
        b[..., 3] = (alpha * mask).astype(np.uint8)
        Image.fromarray(b).save(os.path.join(out, f"{base}_{suffix}.png"))

    save(1 - np.maximum(arm_l, arm_r), "torso")
    save(arm_l, "arml")
    save(arm_r, "armr")
    return base, top, left, right


if __name__ == "__main__":
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else src
    lines = []
    for p in sorted(glob.glob(os.path.join(src, "*body_[0-9][0-9].png"))):
        base, top, left, right = split(p, out)
        lines.append(f"{base} {top} {left} {right}")
    with open(os.path.join(out, "arms.txt"), "w") as f:
        f.write("\n".join(lines) + "\n")
    print(len(lines), "bodies split")
