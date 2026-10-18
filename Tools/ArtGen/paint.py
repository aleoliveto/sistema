"""
A tiny top-down prop painter: shapes are signed-distance fields with a colour, a surface texture
and a height profile. The height map is lit from the top-left (diffuse + specular + ambient
occlusion + self-shadow), edges get a thin dark line, and the whole thing is drawn 4x oversized
and scaled down, so it comes out smooth and painted rather than flat vector.
"""
import numpy as np
from PIL import Image, ImageFilter

# ---------- noise ----------

_rng = np.random.default_rng(7)
_perm = _rng.random((512, 512))


def vnoise(x, y, scale, seed=0):
    """Smooth value noise in 0..1."""
    x = x / scale + seed * 17.3
    y = y / scale + seed * 31.7
    xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
    xf, yf = x - xi, y - yi
    u, v = xf * xf * (3 - 2 * xf), yf * yf * (3 - 2 * yf)
    g = lambda i, j: _perm[(yi + j) & 511, (xi + i) & 511]
    return (g(0, 0) * (1 - u) + g(1, 0) * u) * (1 - v) + (g(0, 1) * (1 - u) + g(1, 1) * u) * v


def fbm(x, y, scale, octaves=4, seed=0):
    s, a, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        s = s + vnoise(x, y, scale / (2 ** o), seed + o) * a
        tot += a
        a *= 0.5
    return s / tot


def blur(a, sigma):
    """Gaussian blur of a float array (three box passes, edge-clamped)."""
    r = max(1, int(round(sigma * 0.9)))
    for _ in range(3):
        for ax in (0, 1):
            p = np.pad(a, [(r + 1, r) if i == ax else (0, 0) for i in range(2)], mode="edge")
            cs = np.cumsum(p, axis=ax, dtype=np.float64)
            hi = np.take(cs, range(2 * r + 1, cs.shape[ax]), axis=ax)
            lo = np.take(cs, range(0, cs.shape[ax] - 2 * r - 1), axis=ax)
            a = ((hi - lo) / (2 * r + 1)).astype(np.float32)
    return a


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ---------- shapes (distance in output pixels, negative inside) ----------

def _local(c, x, y, cx, cy, ang):
    dx, dy = x - cx, y - cy
    if ang:
        a = np.radians(ang)
        dx, dy = dx * np.cos(a) + dy * np.sin(a), -dx * np.sin(a) + dy * np.cos(a)
    return dx, dy


class Canvas:
    def __init__(self, w, h, ss=4):
        self.w, self.h, self.ss = w, h, ss
        H, W = h * ss, w * ss
        self.y, self.x = np.mgrid[0:H, 0:W].astype(np.float32) / ss
        self.col = np.zeros((H, W, 3), np.float32)
        self.a = np.zeros((H, W), np.float32)
        self.z = np.zeros((H, W), np.float32)
        self.gloss = np.zeros((H, W), np.float32)
        self.line = np.zeros((H, W), np.float32)

    # distance fields
    def box(self, cx, cy, w, h, r=0, ang=0):
        dx, dy = _local(self, self.x, self.y, cx, cy, ang)
        qx, qy = np.abs(dx) - w / 2 + r, np.abs(dy) - h / 2 + r
        return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r

    def ellipse(self, cx, cy, rx, ry, ang=0):
        dx, dy = _local(self, self.x, self.y, cx, cy, ang)
        k = np.hypot(dx / rx, dy / ry)
        return (k - 1) * min(rx, ry)

    def circle(self, cx, cy, r):
        return np.hypot(self.x - cx, self.y - cy) - r

    def ring(self, cx, cy, r, t):
        return np.abs(np.hypot(self.x - cx, self.y - cy) - r) - t / 2

    def local(self, cx, cy, ang=0):
        return _local(self, self.x, self.y, cx, cy, ang)

    # painting
    def fill(self, d, color, z0=0.0, height=4.0, profile="bevel", bevel=2.0, tex=None, gloss=0.0,
             line=0.55, clip=None, add_z=False):
        """
        Paint the shape d with color (rgb 0..1, or an HxWx3 array), sitting at z0 with its own
        height profile. tex multiplies the colour (HxW, around 1). clip: only where clip < 0.
        """
        cov = np.clip(0.5 - d * self.ss, 0, 1)
        if clip is not None:
            cov = cov * np.clip(0.5 - clip * self.ss, 0, 1)
        t = np.maximum(-d, 0)
        if profile == "bevel":
            p = smooth(0, bevel, t)
        elif profile == "dome":
            r = bevel
            q = np.minimum(t / r, 1)
            p = np.sqrt(1 - (1 - q) ** 2)
        elif profile == "pillow":
            p = 1 - (1 - smooth(0, bevel, t)) ** 2
        else:
            p = np.ones_like(t)
        zz = z0 + height * p
        c = np.broadcast_to(np.asarray(color, np.float32), self.col.shape).copy() if np.ndim(color) == 1 else color
        if tex is not None:
            c = c * tex[..., None]
        m = cov[..., None]
        self.col = self.col * (1 - m) + c * m
        if add_z:
            self.z = np.maximum(self.z, zz * (cov > 0.5))
        else:
            self.z = np.where(cov > 0.5, zz, self.z)
        self.gloss = self.gloss * (1 - cov) + gloss * cov
        self.a = np.maximum(self.a, cov)
        # outline: a band just inside the edge
        band = np.clip(1 - np.abs(d + 0.35) / 0.55, 0, 1) * cov
        self.line = np.maximum(self.line * (1 - cov), band * line)
        return cov

    # lighting and output
    def render(self, out_w=None, light=(-0.55, -0.7, 0.9), shadow=True):
        z = self.z
        gy, gx = np.gradient(z * self.ss)
        n = np.dstack([-gx, -gy, np.ones_like(z)])
        n /= np.linalg.norm(n, axis=2, keepdims=True)
        L = np.array(light, np.float32)
        L /= np.linalg.norm(L)
        ndl = np.clip((n * L).sum(2), 0, 1)
        diffuse = 0.32 + 0.68 * ndl / L[2]   # a flat top comes out at 1
        diffuse = np.minimum(diffuse, 1.35)
        H = np.array([L[0], L[1], L[2] + 1])
        H /= np.linalg.norm(H)
        spec = np.clip((n * H).sum(2), 0, 1) ** 40 * self.gloss
        # ambient occlusion: lower than the blurred neighbourhood = in a crease
        bz = blur(z, 3 * self.ss)
        ao = np.clip(1 - np.maximum(bz - z, 0) * 0.12, 0.4, 1)
        # worn edges: the rims that stick up catch a little extra light
        edge = np.clip((z - bz) * 0.08, 0, 0.25)
        # self-shadow: march towards the light
        sh = np.ones_like(z)
        if shadow:
            step = self.ss
            for k in range(1, 14):
                ox, oy = int(round(L[0] / np.hypot(L[0], L[1]) * step * k * 0.5)), int(round(L[1] / np.hypot(L[0], L[1]) * step * k * 0.5))
                zs = np.roll(np.roll(z, -oy, 0), -ox, 1)
                rise = zs - z - k * 0.5 * 0.9
                sh = np.minimum(sh, 1 - np.clip(rise * 0.3, 0, 0.55))
            sh = blur(sh, self.ss * 0.8)
        col = self.col * (diffuse * ao * sh + edge)[..., None] + spec[..., None] * 0.85
        col = col * (1 - self.line[..., None] * 0.75)
        # a touch of painterly grain
        grain = (fbm(self.x, self.y, 1.6, 2, seed=99) - 0.5) * 0.06
        col = np.clip(col + grain[..., None], 0, 1)
        rgba = np.dstack([col, self.a])
        img = Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")
        return img.resize((self.w, self.h), Image.LANCZOS)


# ---------- surface textures ----------

def wood(c, along="x", plank=None, seed=0, scale=1.0):
    x, y = (c.x, c.y) if along == "x" else (c.y, c.x)
    warp = fbm(x, y, 20 * scale, 3, seed) * 9
    grain = 0.5 + 0.5 * np.sin((y * 1.6 / scale + warp) * 2.1)
    t = 0.82 + 0.16 * grain + (fbm(x, y * 6, 30 * scale, 2, seed + 3) - 0.5) * 0.25
    if plank:
        gap = np.abs(((y / plank) % 1) - 0.5) * plank
        t = t * (0.55 + 0.45 * smooth(plank * 0.5 - 0.9, plank * 0.5 - 0.3, plank * 0.5 - gap + 0.6 * 0))
        g2 = np.abs((y % plank)) < 0.7
        t = np.where(g2, t * 0.55, t)
    return t


def grime(c, amount=0.25, scale=14, seed=0):
    """Soft dirt: broad and faint, with streaks, not blotches."""
    broad = fbm(c.x, c.y, scale * 2.5, 3, seed)
    streak = fbm(c.x * 0.35, c.y * 2.2, scale, 3, seed + 11)
    return 1 - amount * (0.6 * smooth(0.35, 0.85, broad) + 0.4 * smooth(0.5, 0.9, streak))


def corrugated(c, period=3.0, along="x"):
    v = c.x if along == "x" else c.y
    return np.sin(v / period * 2 * np.pi)
