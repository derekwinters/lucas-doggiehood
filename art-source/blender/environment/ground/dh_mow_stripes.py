"""Mowed-stripe tint for ONE lot's lawn (concept and logic only; the mowing
feature itself is not built). Plain math, no Blender, so the same formula
can be written in a shader (and mirrored in Core) unchanged.

The stripe pattern is a colour MULTIPLIER on the lawn, per world point p:

    mult(p) = 1 + strength * AMP_RGB * wave(s) * mask(u, v)

  u, v      p in the lot's frame: u across the lot (along the house front),
            v front to back (parallel to the house's side walls), both
            measured from the CENTRE of the lot's lawn rectangle.
  pattern   one of PATTERNS, per lot: the stripes vary with
            s = u cos(t) + v sin(t), t = 0 (front-to-back), 90 (side-to-side),
            +45 (diagonal-left) or -45 degrees (diagonal-right). s is a
            distance measured perpendicular to the stripes, so a stripe is
            STRIPE_W wide in every pattern.
  wave(s)   a soft square wave, +1 on one stripe and -1 on the next:
            clamp(EDGE_SHARPNESS * sin(pi * s / STRIPE_W), -1, 1). It is an odd
            function of s, and the lawn rectangle is symmetric through its
            centre (u, v -> -u, -v flips s), so over the lot it averages to
            exactly 0 for every pattern, both diagonals included: the light
            stripes are as much lighter as the dark ones are darker, and the
            lot's mean colour stays that of an unmowed lawn.
  mask      1 inside the lot's lawn rectangle, 0 outside, with an EDGE_FADE
            soft edge so it never draws an aliased line. Outside, mult == 1
            exactly: paving, verge and the neighbouring lots never change.
  strength  0..1. 0 gives mult == 1 everywhere, exactly an unmowed lawn; the
            later grow-back just lowers it toward 0.

The lawn rectangle is the lot's quadrant with every road-side edge pulled in
to the yard/pavement line, the same rule as Core's
LotBounds.ClearRoadCorridors (StreetCorridorInset = 5.75 m), so stripes stop
at the sidewalk's outer edge and at the lot line to a neighbour. Paving,
house and fence are drawn on top of the lawn, so the stripes under them are
hidden anyway.

Why light and dark: real mow stripes are the light catching blades the
mower bent away from or toward the viewer. The game camera's yaw is fixed,
so a fixed tint is a fair stand-in; the light stripe is also a little paler
(blue lifted more than green) and the dark one a little richer.
"""
import math

STRIPE_W = 0.75                  # metres per mower pass (a 21-inch walk-behind with overlap; riding mowers ~1 m)
AMP_RGB_BASE = (0.075, 0.065, 0.11)   # first-round amounts (about +-7 % value)
CONTRAST = 1.5                   # Derek 2026-11-24: "make the lines like 50% darker", both sides scaled
AMP_RGB = tuple(round(a * CONTRAST, 6) for a in AMP_RGB_BASE)   # (0.1125, 0.0975, 0.165): about +-11 %
EDGE_SHARPNESS = 6.0             # wave steepness: the light/dark transition is ~0.11 x STRIPE_W (8 cm)
EDGE_FADE = 0.05                 # metres of soft edge on the lot mask
PATTERNS = {                     # Derek 2026-11-24: exactly these four. The value is the angle (degrees)
    "front-to-back": 0.0,        # of the stripe NORMAL from the lot's across axis u. Seen from the
    "side-to-side": 90.0,        # street (v pointing to the back of the lot): 0 = stripes run front to
    "diagonal-left": 45.0,       # back, 90 = along the house front, +45 = they run back and to the
    "diagonal-right": -45.0,     # left, -45 = back and to the right.
}
CHECK_STEP = 0.001               # metres between samples on the width walk
CHECK_GRID = 0.05                # metres between samples on the balance grid


class LotFrame:
    """A lot's lawn rectangle in world XY (Blender ground plane): centre,
    unit `across` direction (along the house front), half sizes."""

    def __init__(self, centre, across, half_across, half_along):
        ax, ay = across
        n = math.hypot(ax, ay)
        self.c = centre
        self.a = (ax / n, ay / n)
        self.b = (-self.a[1], self.a[0])            # front-to-back direction
        self.hu, self.hv = half_across, half_along

    @classmethod
    def from_rect(cls, x0, x1, y0, y1, across=(1.0, 0.0)):
        """Axis-aligned rectangle; `across` is +-X or +-Y."""
        c = ((x0 + x1) / 2, (y0 + y1) / 2)
        if abs(across[0]) > abs(across[1]):
            return cls(c, across, (x1 - x0) / 2, (y1 - y0) / 2)
        return cls(c, across, (y1 - y0) / 2, (x1 - x0) / 2)

    def xy(self, u, v):
        return (self.c[0] + self.a[0] * u + self.b[0] * v, self.c[1] + self.a[1] * u + self.b[1] * v)

    def uv(self, x, y):
        dx, dy = x - self.c[0], y - self.c[1]
        return dx * self.a[0] + dy * self.a[1], dx * self.b[0] + dy * self.b[1]


def lawn_rect(quad, road_x=None, road_y=None, inset=5.75):
    """The lot's lawn rectangle: quadrant (x0, x1, y0, y1) with each edge that
    lies on a road centreline (road_x / road_y) pulled in by `inset`."""
    x0, x1, y0, y1 = quad
    if road_x is not None:
        if abs(x0 - road_x) < 1e-3:
            x0 += inset
        if abs(x1 - road_x) < 1e-3:
            x1 -= inset
    if road_y is not None:
        if abs(y0 - road_y) < 1e-3:
            y0 += inset
        if abs(y1 - road_y) < 1e-3:
            y1 -= inset
    return x0, x1, y0, y1


def wave(u):
    return max(-1.0, min(1.0, EDGE_SHARPNESS * math.sin(math.pi * u / STRIPE_W)))


def _smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def mask(frame, u, v):
    du = frame.hu - abs(u)
    dv = frame.hv - abs(v)
    return _smooth01(du / EDGE_FADE) * _smooth01(dv / EDGE_FADE)


def pattern_angle(pattern):
    if pattern not in PATTERNS:
        raise ValueError("unknown stripe pattern %r; one of %s" % (pattern, list(PATTERNS)))
    return math.radians(PATTERNS[pattern])


def stripe_normal(frame, pattern):
    """World unit vector across the stripes (the direction the width is measured in)."""
    t = pattern_angle(pattern)
    c, s = math.cos(t), math.sin(t)
    return (frame.a[0] * c + frame.b[0] * s, frame.a[1] * c + frame.b[1] * s)


def multiplier(frame, x, y, strength, pattern="front-to-back"):
    """(r, g, b) multiplier at world (x, y)."""
    t = pattern_angle(pattern)
    if strength <= 0.0:
        return (1.0, 1.0, 1.0)
    u, v = frame.uv(x, y)
    m = mask(frame, u, v)
    if m <= 0.0:
        return (1.0, 1.0, 1.0)
    w = wave(u * math.cos(t) + v * math.sin(t)) * m * strength
    return tuple(1.0 + a * w for a in AMP_RGB)


def self_check(frame, strength=1.0, pattern="front-to-back"):
    """The rules above, checked numerically for one pattern. Returns a list
    of failures (empty when all hold)."""
    bad = []
    tag = "%s s=%.1f: " % (pattern, strength)
    # 1. balance: mean multiplier over the whole lawn rectangle (cell-centred grid) is 1
    nu = int(round(2 * frame.hu / CHECK_GRID))
    nv = int(round(2 * frame.hv / CHECK_GRID))
    sums, lo, hi, cnt = [0.0, 0.0, 0.0], [9.0] * 3, [0.0] * 3, 0
    for i in range(nu):
        u = -frame.hu + (i + 0.5) * 2 * frame.hu / nu
        for j in range(nv):
            v = -frame.hv + (j + 0.5) * 2 * frame.hv / nv
            m = multiplier(frame, *frame.xy(u, v), strength, pattern=pattern)
            cnt += 1
            for ch in range(3):
                sums[ch] += m[ch]
                lo[ch], hi[ch] = min(lo[ch], m[ch]), max(hi[ch], m[ch])
    for ch in range(3):
        if abs(sums[ch] / cnt - 1.0) > 1e-3:
            bad.append(tag + "mean multiplier channel %d is %.5f, not 1" % (ch, sums[ch] / cnt))
        if strength > 0 and not (lo[ch] < 1.0 < hi[ch]):
            bad.append(tag + "channel %d is not both lighter and darker" % ch)
    # 2. width measured perpendicular to the stripes; constant along them
    if strength > 0:
        nx, ny = stripe_normal(frame, pattern)
        tx, ty = -ny, nx
        reach = min(frame.hu, frame.hv) * 0.5
        prev, cross = None, []
        k = int(reach / CHECK_STEP)
        for i in range(-k, k + 1):
            t = i * CHECK_STEP
            x, y = frame.c[0] + nx * t, frame.c[1] + ny * t
            s = 1 if multiplier(frame, x, y, strength, pattern=pattern)[1] > 1.0 else -1
            if prev is not None and s != prev:
                cross.append(t)
            prev = s
        gaps = [b - a for a, b in zip(cross, cross[1:])]
        if not gaps or any(abs(g - STRIPE_W) > 2 * CHECK_STEP for g in gaps):
            bad.append(tag + "stripe width across the stripes is not %.2f m: %s" % (STRIPE_W, gaps[:4]))
        for t in (0.13, 0.41, -0.29):
            x, y = frame.c[0] + nx * t, frame.c[1] + ny * t
            m0 = multiplier(frame, x, y, strength, pattern=pattern)
            m1 = multiplier(frame, x + tx * reach * 0.7, y + ty * reach * 0.7, strength, pattern=pattern)
            if max(abs(a - b) for a, b in zip(m0, m1)) > 1e-9:
                bad.append(tag + "colour changes along a stripe")
    # 3. nothing outside the lot changes
    for k in range(64):
        a = 2 * math.pi * k / 64
        for r in (1.0001, 1.2, 3.0):
            u, v = frame.hu * r * math.cos(a), frame.hv * r * math.sin(a)
            if abs(u) <= frame.hu and abs(v) <= frame.hv:
                continue
            if multiplier(frame, *frame.xy(u, v), strength, pattern=pattern) != (1.0, 1.0, 1.0):
                bad.append(tag + "tint outside the lot at u=%.2f v=%.2f" % (u, v))
    # 4. strength 0 is exactly unmowed
    for i in range(-20, 21):
        if multiplier(frame, *frame.xy(i * frame.hu / 21, i * frame.hv / 23), 0.0, pattern=pattern) != (1.0, 1.0, 1.0):
            bad.append(tag + "strength 0 is not exactly unmowed")
            break
    return bad


if __name__ == "__main__":
    # An example lot lawn rectangle (24.25 x 24.25 m, both road edges inset), across = +X.
    f = LotFrame.from_rect(-24.25, 0.0, 0.0, 24.25, across=(1.0, 0.0))
    fails = []
    want = {"front-to-back": 0.0, "side-to-side": 90.0, "diagonal-left": 45.0, "diagonal-right": -45.0}
    if PATTERNS != want:
        fails.append("patterns are not exactly the four: %s" % PATTERNS)
    # the two diagonals are mirror images: seen from the street (v pointing to the back), the
    # stripes of diagonal-left run back and to the left (-u), diagonal-right back and to the right
    for name, side in (("diagonal-left", -1), ("diagonal-right", 1)):
        if name in PATTERNS:
            nx, ny = stripe_normal(f, name)
            ru, rv = f.uv(f.c[0] - ny, f.c[1] + nx)          # run direction = normal turned +90
            ru, rv = (ru, rv) if rv > 0 else (-ru, -rv)        # the end that heads to the back
            if not (ru * side > 0.5 and rv > 0.5):
                fails.append("%s does not lean %s going back" % (name, "left" if side < 0 else "right"))
    try:
        multiplier(f, f.c[0], f.c[1], 1.0, pattern="zigzag")
        fails.append("an unknown pattern was accepted")
    except ValueError:
        pass
    g = LotFrame.from_rect(-20.0, 0.0, 0.0, 14.5, across=(1.0, 0.0))     # a non-square lot too
    for p in PATTERNS:
        for s in (1.0, 0.5, 0.0):
            r = self_check(f, s, p) + self_check(g, s, p)
            print("%-14s strength %.1f: %s" % (p, s, "OK" if not r else r[:3]))
            fails += r
    lo = tuple(1 - a for a in AMP_RGB)
    hi = tuple(1 + a for a in AMP_RGB)
    if any(abs((h - 1) + (l - 1)) > 1e-9 for h, l in zip(hi, lo)):
        fails.append("light and dark are not balanced around 1")
    if any(abs(x - y) > 1e-9 for x, y in zip(AMP_RGB, (0.1125, 0.0975, 0.165))):
        fails.append("AMP_RGB is not 1.5 x the first-round amounts")
    print("stripe width %.2f m; at strength 1 light x%s, dark x%s" % (STRIPE_W, hi, lo))
    print("ALL OK" if not fails else "FAILED: %d" % len(fails))
    raise SystemExit(1 if fails else 0)
