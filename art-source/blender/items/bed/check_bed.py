"""The dog bed's checks (#411), as plain functions of measured numbers and the exported glTF.

export_bed.py applies them to the real export and exits non-zero if any fails;
tests/test_bed_checks.py pins them. No Blender needed. Each function returns a list of error
strings, empty when the check passes.
"""
import os

# The bed's footprint (long side along X). Derek on #411: about 15% bigger than today's slab.
FOOTPRINT_W = 1.60
FOOTPRINT_D = 1.15
# The bed must also fill at least this share of each side of its footprint.
FOOTPRINT_FILL = 0.96
# Today's graybox slab (DecorationView: localScale 1.4 x 0.35 x 1.0), kept for comparison renders.
SLAB_W = 1.4
SLAB_D = 1.0
# The provisional per-mesh triangle backstop (#799), and the bed's own, lower target (Derek on #411).
BUDGET_TRIS = 6000
TARGET_TRIS = 2000
# The flat area round ATTACH_REST that a dog lies on must be at least this big (w, d), in metres.
REST_AREA_MIN = (0.95, 0.50)
# Pivot: on the ground at the footprint centre, within this many metres.
PIVOT_TOL = 0.002
# The rest area: every sample within this many metres of the rest height.
FLAT_TOL = 0.012
MATERIAL_PREFIX = "DH_Item_Bed_"
REST_NODE = "ATTACH_REST"


def footprint_errors(size_xyz):
    w, d = size_xyz[0], size_xyz[1]
    errs = []
    if w > FOOTPRINT_W + 1e-6 or d > FOOTPRINT_D + 1e-6:
        errs.append("size %.3f x %.3f m is outside the %.2f x %.2f m footprint" % (w, d, FOOTPRINT_W, FOOTPRINT_D))
    if w < FOOTPRINT_W * FOOTPRINT_FILL or d < FOOTPRINT_D * FOOTPRINT_FILL:
        errs.append("size %.3f x %.3f m fills less than %d%% of the footprint"
                    % (w, d, round(FOOTPRINT_FILL * 100)))
    return errs


def pivot_errors(lo, hi):
    errs = []
    cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    if abs(cx) > PIVOT_TOL or abs(cy) > PIVOT_TOL:
        errs.append("footprint centre is at (%.3f, %.3f), not the pivot" % (cx, cy))
    if abs(lo[2]) > PIVOT_TOL:
        errs.append("lowest point is at z = %.3f, not on the ground" % lo[2])
    return errs


def budget_errors(tris, limit=BUDGET_TRIS):
    if tris > limit:
        return ["%d triangles is over the limit of %d" % (tris, limit)]
    return []


def flat_top_errors(zs, rest_z):
    if not zs:
        return ["no samples of the rest area"]
    errs = []
    missed = sum(1 for z in zs if z is None)
    if missed:
        errs.append("%d of %d rest-area samples found no cushion" % (missed, len(zs)))
    off = [z for z in zs if z is not None and abs(z - rest_z) > FLAT_TOL]
    if off:
        errs.append("%d rest-area samples are more than %.3f m from the rest height %.3f (worst %.3f)"
                    % (len(off), FLAT_TOL, rest_z, max(off, key=lambda z: abs(z - rest_z))))
    return errs


def gltf_errors(j, gltf_path, tex_dir):
    errs = []
    tex = os.path.realpath(tex_dir)
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None:
            errs.append("an image is embedded, not referenced from textures/")
            continue
        if os.path.isabs(uri) or ":" in uri:
            errs.append("image URI %s is not relative" % uri)
            continue
        f = os.path.realpath(os.path.join(os.path.dirname(gltf_path), uri))
        if os.path.dirname(f) != tex or not os.path.isfile(f):
            errs.append("image URI %s does not resolve inside textures/" % uri)
    for m in j.get("materials", []):
        if not m.get("name", "").startswith(MATERIAL_PREFIX):
            errs.append("material %r lacks the %s prefix" % (m.get("name"), MATERIAL_PREFIX))
    if not any(n.get("name") == REST_NODE for n in j.get("nodes", [])):
        errs.append("no %s node" % REST_NODE)
    return errs


# ---------------------------------------------------------------------------
# The colour palette (bed_colours.json): the cover and base maps are neutral, so a bed's colour
# is its materials' base colour. Each entry is {name, cover, base}; base = darken(cover).
PALETTE_SIZE = 10
BASE_DARKEN = 0.70           # the base is the cover colour x this (cf. the houses' DERIVED_DARKEN)
LAWN_HEX = "#8E9E68"         # the review renders' lawn
MIN_DELTA_E = 15.0           # every two covers differ by at least this (CIE76, Lab)
MIN_LAWN_DELTA_E = 20.0      # every cover stands out from the lawn by at least this


def _rgb(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def is_hex(h):
    return isinstance(h, str) and len(h) == 7 and h[0] == "#" and all(c in "0123456789ABCDEFabcdef" for c in h[1:])


def hex_linear(h):
    """The linear RGB a hex colour exports as (glTF baseColorFactor)."""
    return [_lin(c) for c in _rgb(h)]


def darken(h, f=BASE_DARKEN):
    return "#" + "".join("%02X" % int(round(c * 255 * f)) for c in _rgb(h))


def luminance(h):
    r, g, b = hex_linear(h)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def lab(h):
    r, g, b = hex_linear(h)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def delta_e(a, b):
    return sum((p - q) ** 2 for p, q in zip(lab(a), lab(b))) ** 0.5


def palette_errors(doc):
    errs = []
    cols = doc.get("colours", [])
    if len(cols) != PALETTE_SIZE:
        errs.append("%d colours, not %d" % (len(cols), PALETTE_SIZE))
    names = [c.get("name") for c in cols]
    if len(set(names)) != len(names):
        errs.append("duplicate colour names")
    if doc.get("default") not in names:
        errs.append("default %r is not in the palette" % doc.get("default"))
    good = []
    for c in cols:
        if not (is_hex(c.get("cover")) and is_hex(c.get("base"))):
            errs.append("%s: cover/base is not a #RRGGBB hex" % c.get("name"))
            continue
        if luminance(c["base"]) >= luminance(c["cover"]):
            errs.append("%s: base %s is not darker than cover %s" % (c["name"], c["base"], c["cover"]))
        if delta_e(c["cover"], LAWN_HEX) < MIN_LAWN_DELTA_E:
            errs.append("%s: cover %s is too close to the lawn" % (c["name"], c["cover"]))
        good.append(c)
    for i, a in enumerate(good):
        for b in good[i + 1:]:
            de = delta_e(a["cover"], b["cover"])
            if de < MIN_DELTA_E:
                errs.append("%s and %s are too alike (delta E %.1f)" % (a["name"], b["name"], de))
    return errs
