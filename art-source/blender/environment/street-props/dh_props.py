"""Textured street props (round 3 quality): light geometry carrying tileable
colour + normal maps (dh_prop_tex) and one shared label atlas for every sign
face, panel and road decal.

Conventions: metres, Z up, origin on the ground (road decals: on the road
surface) at the prop's footprint centre / post base; the prop's front faces
-Y (signs face oncoming traffic when the prop's -Y points at it). Every
human-use dimension is multiplied by PROP_SCALE (1.15), matching the houses'
door scale, as in round 2.

Materials: a handful of tileable surfaces, each multiplied by a paint colour
(exported as glTF baseColorFactor), so one texture serves many props.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# The committed round-3 tree modules (../trees) and house kit (art-source/blender).
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_trees_tex as TT  # noqa: E402
import dh_prop_tex as PT  # noqa: E402

S = 1.15                       # PROP_SCALE (round 2)
TILE = 0.5                     # metres per tileable texture repeat (paint, galv, plastic)
WOOD_TILE = 0.9
CONC_TILE = 0.8
DECAL_Z = 0.005                # road decals sit this far above the road surface

# --- sign dimensions (MUTCD sizes, scaled) ---------------------------------
POST_W = 0.055 * S             # square galvanised sign post
SIGN_T = 0.018 * S             # blade thickness
SIGN_BEVEL = 0.012 * S
SIGN_BOTTOM = 2.0 * S          # bottom edge of the lowest blade
SIGN_GAP = 0.05 * S            # between a sign and its plaque
STOP_W = 0.76 * S
YIELD_W = 0.76 * S
SPEED_WH = (0.61 * S, 0.76 * S)
DIAMOND_SIDE = 0.76 * S
PLAQUE_WH = (0.61 * S, 0.46 * S)
ARROW_WH = (0.61 * S, 0.30 * S)
NOPARK_WH = (0.30 * S, 0.46 * S)
BUS_WH = (0.46 * S, 0.61 * S)
CORNER_R = 0.04 * S            # rounded corners on rectangular blades

# paint colours (multiplied over the light tileable textures)
PAINT = {
    "lamp_black": ("paint", "#2A2D33", 0.45, 0.0),
    "cobra_grey": ("paint", "#A3A8AE", 0.4, 0.0),
    "galv": ("galv", None, 0.38, 0.55),
    "pole_galv": ("galv", "#D8DBDE", 0.45, 0.5),
    "hydrant_red": ("paint", "#C63A2C", 0.4, 0.0),
    "hydrant_cap": ("paint", "#C9CDD1", 0.35, 0.0),
    "post_white": ("paint", "#F4F2EC", 0.55, 0.0),
    "mailbox": ("paint", "#30343A", 0.4, 0.0),
    "flag": ("paint", "#CC3029", 0.45, 0.0),
    "bench_iron": ("paint", "#2B2F35", 0.5, 0.0),
    "bench_wood": ("wood", None, 0.75, 0.0),
    "bus_green": ("paint", "#2F5E48", 0.45, 0.0),
    "trash": ("plastic", "#45524A", 0.6, 0.0),
    "recycle": ("plastic", "#2E6FC0", 0.55, 0.0),
    "rubber": ("plastic", "#2A2A2C", 0.85, 0.0),
    "waste_green": ("paint", "#2C6E47", 0.5, 0.0),
    "news_navy": ("paint", "#2D4E8C", 0.4, 0.0),
    "concrete": ("concrete", None, 0.9, 0.0),
    "pole_wood": ("wood", "#7C6450", 0.9, 0.0),
    "insulator": ("plastic", "#8FA79A", 0.25, 0.0),
    "wire": ("plastic", "#1C1D1F", 0.6, 0.0),
    "transformer": ("paint", "#8E9499", 0.5, 0.0),
}
GLASS_HEX, GLASS_EMIT = "#FFE9B0", 1.2
LENS_HEX, LENS_EMIT = "#F4F6FF", 1.0


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def _tiled(name, paths, tint, rough, metal):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = TT.bark_material(name, paths)
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    for n in nt.nodes:
        if n.type == "NORMAL_MAP":
            n.inputs["Strength"].default_value = 1.0
    if tint:
        tex = [n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image.colorspace_settings.name != "Non-Color"][0]
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = K.hex_rgba(tint)
        nt.links.new(tex.outputs["Color"], mix.inputs[6])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return mat


def _emissive(name, hex_, strength):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = K.hex_rgba(hex_)
    b.inputs["Roughness"].default_value = 0.2
    b.inputs["Emission Color"].default_value = K.hex_rgba(hex_)
    b.inputs["Emission Strength"].default_value = strength
    mat.use_backface_culling = True
    return mat


_MATS = None


def materials():
    global _MATS
    if _MATS is not None and all(m.name in bpy.data.materials for m in _MATS.values()):
        return _MATS
    tx = PT.paths()
    m = {k: _tiled("DH_Prop_" + k, tx[t], tint, r, mt) for k, (t, tint, r, mt) in PAINT.items()}
    lab = _tiled("DH_Prop_labels", tx["labels"], None, 0.45, 0.0)
    m["labels"] = lab
    m["glass"] = _emissive("DH_Prop_lamp_glass", GLASS_HEX, GLASS_EMIT)
    m["lens"] = _emissive("DH_Prop_led_lens", LENS_HEX, LENS_EMIT)
    _MATS = m
    return m


# ---------------------------------------------------------------------------
# builder
# ---------------------------------------------------------------------------
class PropBuilder(TT.TreeBuilder):
    """Faces with explicit UVs; flat-shaded unless a smooth primitive gives
    custom normals. Faces are oriented by an outward hint, so callers never
    have to get the winding right by hand."""

    def __init__(self, name):
        super().__init__(name)
        self.tiles = {}

    # --- core -------------------------------------------------------------
    def oface(self, vs, uvs, mat, out, nrms=None):
        vs = [Vector(v) for v in vs]
        n = Vector((0, 0, 0))
        for i in range(len(vs)):                      # Newell normal
            a, b = vs[i], vs[(i + 1) % len(vs)]
            n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
        if n.dot(Vector(out)) < 0:
            vs, uvs = vs[::-1], uvs[::-1]
            nrms = nrms[::-1] if nrms else None
        self.face(vs, uvs, mat, nrms)

    def pface(self, vs, mat, out, tile=TILE, grain=None):
        """Planar-mapped face: V follows `grain` (or world Z on walls / world
        Y on floors), at `tile` metres per texture repeat."""
        vs = [Vector(v) for v in vs]
        o = Vector(out).normalized()
        n = (vs[1] - vs[0]).cross(vs[2] - vs[0])
        n = n.normalized() if n.length > 1e-12 else o
        if n.dot(o) < 0:
            n = -n
        g = Vector(grain).normalized() if grain is not None else None
        if g is not None and abs(g.dot(n)) < 0.8:
            v = g
        elif abs(n.z) < 0.7:
            v = Vector((0, 0, 1))
        else:
            v = Vector((0, 1, 0))
        v = (v - n * v.dot(n)).normalized()
        u = v.cross(n)
        self.oface(vs, [(p.dot(u) / tile, p.dot(v) / tile) for p in vs], mat, o)

    # --- primitives -------------------------------------------------------
    def box(self, lo, hi, mat, tile=TILE, grain=None, skip=()):
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
        P = [Vector((x, y, z)) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
        quads = {"-x": (0, 2, 6, 4), "+x": (1, 3, 7, 5), "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6),
                 "-z": (0, 1, 3, 2), "+z": (4, 5, 7, 6)}
        for k, q in quads.items():
            if k in skip:
                continue
            vs = [P[i] for i in q]
            fc = sum(vs, Vector()) / 4
            self.pface(vs, mat, fc - c, tile, grain)

    def frustum(self, bot, top, z0, z1, mat, tile=TILE, grain=None, caps=(False, True)):
        """Box whose bottom rect bot=(x0,y0,x1,y1) tapers to top rect."""
        B = [Vector((bot[0], bot[1], z0)), Vector((bot[2], bot[1], z0)), Vector((bot[2], bot[3], z0)),
             Vector((bot[0], bot[3], z0))]
        T = [Vector((top[0], top[1], z1)), Vector((top[2], top[1], z1)), Vector((top[2], top[3], z1)),
             Vector((top[0], top[3], z1))]
        c = (sum(B, Vector()) + sum(T, Vector())) / 8
        for i in range(4):
            j = (i + 1) % 4
            vs = [B[i], B[j], T[j], T[i]]
            self.pface(vs, mat, sum(vs, Vector()) / 4 - c, tile, grain)
        if caps[0]:
            self.pface(B, mat, (0, 0, -1), tile, grain)
        if caps[1]:
            self.pface(T, mat, (0, 0, 1), tile, grain)

    def beam(self, p0, p1, w, h, mat, up=(0, 0, 1), tile=TILE, ends=True):
        """Oriented box from p0 to p1, cross-section w (side) x h (up)."""
        p0, p1 = Vector(p0), Vector(p1)
        t = (p1 - p0).normalized()
        u = Vector(up)
        u = (u - t * u.dot(t)).normalized()
        sd = t.cross(u).normalized()
        ring = lambda p: [p + sd * (sx * w / 2) + u * (sz * h / 2) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        a, b = ring(p0), ring(p1)
        c = (p0 + p1) / 2
        for i in range(4):
            j = (i + 1) % 4
            vs = [a[i], a[j], b[j], b[i]]
            self.pface(vs, mat, sum(vs, Vector()) / 4 - c, tile, grain=t)
        if ends:
            self.pface(a, mat, -t, tile)
            self.pface(b, mat, t, tile)

    def tube(self, pts, radii, mat, n=8, cap=True, tile=TILE, cap0=False):
        """Smooth tube along a path (custom radial normals), U around, V along."""
        pts = [Vector(p) for p in pts]
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(pts)
        elif len(radii) != len(pts):               # (start, end): interpolate along the path
            radii = [radii[0] + (radii[-1] - radii[0]) * k / (len(pts) - 1) for k in range(len(pts))]
        t0 = (pts[1] - pts[0]).normalized()
        ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
        u = t0.cross(ref).normalized()
        rings, dirs, vc, acc = [], [], [], 0.0
        for k, p in enumerate(pts):
            t = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
            u = (u - t * u.dot(t)).normalized()
            v = t.cross(u)
            if k:
                acc += (pts[k] - pts[k - 1]).length
            vc.append(acc / tile)
            d = [(u * math.cos(2 * math.pi * i / n) + v * math.sin(2 * math.pi * i / n)) for i in range(n)]
            dirs.append(d)
            rings.append([p + d[i] * radii[k] for i in range(n)])
        reps = max(1, round(2 * math.pi * max(radii) / tile))
        for k in range(len(rings) - 1):
            for i in range(n):
                j = (i + 1) % n
                vs = [rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]]
                uv = [(reps * i / n, vc[k]), (reps * (i + 1) / n, vc[k]), (reps * (i + 1) / n, vc[k + 1]),
                      (reps * i / n, vc[k + 1])]
                nr = [dirs[k][i], dirs[k][j], dirs[k + 1][j], dirs[k + 1][i]]
                self.oface(vs, uv, mat, (dirs[k][i] + dirs[k][j]), nr)
        for end, ok in ((0, cap0), (-1, cap)):
            if not ok:
                continue
            r = rings[end]
            t = (pts[-1] - pts[-2]) if end == -1 else (pts[0] - pts[1])
            self.pface(r, mat, t, tile)

    def lathe(self, prof, mat, n=8, c=(0, 0), tile=TILE, smooth=35.0, rot=0.0):
        """Surface of revolution about the vertical axis through c; prof is a
        list of (r, z) from bottom to top. Normals are smooth around the
        axis; along the profile they are smoothed where the turn is below
        `smooth` degrees, hard otherwise."""
        cx, cy = c
        segn = []
        for a, b in zip(prof, prof[1:]):
            dr, dz = b[0] - a[0], b[1] - a[1]
            L = math.hypot(dr, dz) or 1.0
            segn.append((dz / L, -dr / L))
        ang = [math.radians(rot) + 2 * math.pi * i / n for i in range(n)]
        acc = [0.0]
        for a, b in zip(prof, prof[1:]):
            acc.append(acc[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        reps = max(1, round(2 * math.pi * max(r for r, _ in prof) / tile))

        def vn(k, side):
            """normal at profile vertex k for segment `side` (0: below, 1: above)."""
            s = k - 1 if side == 0 else k
            own = segn[s]
            other = segn[k] if side == 0 and k < len(segn) else (segn[k - 1] if side == 1 and k > 0 else None)
            if other is None:
                return own
            cosang = own[0] * other[0] + own[1] * other[1]
            if cosang > math.cos(math.radians(smooth)):
                x, z = own[0] + other[0], own[1] + other[1]
                L = math.hypot(x, z) or 1.0
                return (x / L, z / L)
            return own
        for s in range(len(prof) - 1):
            (r0, z0), (r1, z1) = prof[s], prof[s + 1]
            n0, n1 = vn(s, 1), vn(s + 1, 0)
            for i in range(n):
                j = (i + 1) % n
                ci, si, cj, sj = math.cos(ang[i]), math.sin(ang[i]), math.cos(ang[j]), math.sin(ang[j])
                P = lambda r, z, co, so: (cx + r * co, cy + r * so, z)
                N = lambda nn, co, so: Vector((nn[0] * co, nn[0] * so, nn[1])).normalized()
                vs = [P(r0, z0, ci, si), P(r0, z0, cj, sj), P(r1, z1, cj, sj), P(r1, z1, ci, si)]
                uv = [(reps * i / n, acc[s] / tile), (reps * (i + 1) / n, acc[s] / tile),
                      (reps * (i + 1) / n, acc[s + 1] / tile), (reps * i / n, acc[s + 1] / tile)]
                nr = [N(n0, ci, si), N(n0, cj, sj), N(n1, cj, sj), N(n1, ci, si)]
                out = Vector((segn[s][0] * (ci + cj) / 2, segn[s][0] * (si + sj) / 2, segn[s][1]))
                if r0 < 1e-6:
                    vs, uv, nr = vs[1:], uv[1:], nr[1:]
                elif r1 < 1e-6:
                    vs, uv, nr = vs[:3], uv[:3], nr[:3]
                self.oface(vs, uv, mat, out, nr)

    def ellipsoid(self, c, r, mat, nu=8, nv=4, tile=TILE, zcut=None):
        """Low-poly smooth ellipsoid (custom normals); zcut drops the rings
        below that height (flat-bottomed shells)."""
        c = Vector(c)
        pts = []
        for j in range(nv + 1):
            th = math.pi * j / nv
            row = []
            for i in range(nu):
                ph = 2 * math.pi * i / nu
                d = Vector((math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)))
                p = c + Vector((d.x * r[0], d.y * r[1], d.z * r[2]))
                nn = Vector((d.x / r[0], d.y / r[1], d.z / r[2])).normalized()
                row.append((p, nn, (i / nu, 1 - j / nv)))
            pts.append(row)
        for j in range(nv):
            for i in range(nu):
                k = (i + 1) % nu
                q = [pts[j][i], pts[j][k], pts[j + 1][k], pts[j + 1][i]]
                if j == 0:
                    q = [q[0], q[2], q[3]]
                elif j == nv - 1:
                    q = [q[0], q[1], q[2]]
                if zcut is not None and min(p.z for p, _, _ in q) < zcut - 1e-6:
                    continue
                out = sum((p for p, _, _ in q), Vector()) / len(q) - c
                self.oface([p for p, _, _ in q], [(u * 2, v) for _, _, (u, v) in q], mat, out,
                           [nn for _, nn, _ in q])

    def plate(self, outline, y, key, z_off=0.0, x_off=0.0, T=SIGN_T, bevel=SIGN_BEVEL, back="galv"):
        """Sign blade in the XZ plane facing -Y: outline (CCW, as seen from
        the front) in metres. Front + chamfer carry the label atlas cell
        `key` (outline bbox -> label rect); edge + back are galvanised."""
        ol = [Vector((x + x_off, 0, z + z_off)) for x, z in outline]
        xs, zs = [p.x for p in ol], [p.z for p in ol]
        x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
        lab = lambda p: PT.label_uv(key, (p.x - x0) / (x1 - x0), (p.z - z0) / (z1 - z0))
        inset = _offset_poly([(p.x, p.z) for p in ol], bevel)
        yf, yb = y - T / 2, y + T / 2
        F = [Vector((x, yf, z)) for x, z in inset]
        M = [Vector((p.x, yf + bevel * 0.6, p.z)) for p in ol]
        Bk = [Vector((p.x, yb, p.z)) for p in ol]
        self.oface(F, [lab(p) for p in F], "labels", (0, -1, 0))
        ctr = Vector(((x0 + x1) / 2, y, (z0 + z1) / 2))
        n = len(ol)
        for i in range(n):
            j = (i + 1) % n
            out = (M[i] + M[j]) / 2 - ctr
            out.y = 0
            q = [F[i], F[j], M[j], M[i]]
            self.oface(q, [lab(p) for p in q], "labels", out - Vector((0, 0.5 * out.length, 0)))
            self.pface([M[i], M[j], Bk[j], Bk[i]], back, out)
        self.pface(Bk, back, (0, 1, 0))

    def label_quad(self, corners, key, out):
        """Flat label (atlas cell) on four corners given bottom-left,
        bottom-right, top-right, top-left as seen from outside."""
        uv = [PT.label_uv(key, 0, 0), PT.label_uv(key, 1, 0), PT.label_uv(key, 1, 1), PT.label_uv(key, 0, 1)]
        self.oface(corners, uv, "labels", out)

    def build(self, mats=None):
        return super().build(mats or materials())


def _offset_poly(poly, d):
    """Inset a convex CCW polygon by d (edge offset, mitred corners)."""
    n = len(poly)
    lines = []
    for i in range(n):
        a, b = Vector(poly[i]), Vector(poly[(i + 1) % n])
        t = (b - a).normalized()
        nin = Vector((-t.y, t.x))                     # left of a CCW edge = inside
        lines.append((a + nin * d, t))
    out = []
    for i in range(n):
        (p1, t1), (p2, t2) = lines[i - 1], lines[i]
        den = t1.x * t2.y - t1.y * t2.x
        if abs(den) < 1e-9:
            out.append(tuple(p2))
            continue
        s = ((p2.x - p1.x) * t2.y - (p2.y - p1.y) * t2.x) / den
        q = p1 + t1 * s
        out.append((q.x, q.y))
    return out


def _rrect(w, h, r, cz, seg=2):
    """Rounded rectangle outline (CCW from the front) centred at (0, cz)."""
    pts = []
    for cx, cy, a0 in ((w / 2 - r, -h / 2 + r, -90), (w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90),
                       (-w / 2 + r, -h / 2 + r, 180)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cz + cy + r * math.sin(a)))
    return pts


def _ngon(n, R, cz, rot):
    return [(R * math.cos(math.radians(rot + 360 * i / n)), cz + R * math.sin(math.radians(rot + 360 * i / n)))
            for i in range(n)]


# ---------------------------------------------------------------------------
# signs
# ---------------------------------------------------------------------------
def _sign_post(b, top, y=0.0):
    w = POST_W
    b.box((-w / 2, y - w / 2, 0), (w / 2, y + w / 2, top), "galv", skip=("-z",))
    b.box((-w * 0.6, y - w * 0.6, top), (w * 0.6, y + w * 0.6, top + 0.012 * S), "galv", skip=("-z",))


def _blade(b, kind, z_bottom):
    """Adds one blade with its bottom edge at z_bottom; returns its top z."""
    y = -POST_W / 2 - SIGN_T / 2 - 0.004
    if kind == "stop":
        R = STOP_W / 2 / math.cos(math.radians(22.5))
        cz = z_bottom + STOP_W / 2
        b.plate(_ngon(8, R, cz, 22.5), y, "stop")
        return z_bottom + STOP_W
    if kind == "yield":
        h = YIELD_W * math.sqrt(3) / 2
        pts = [(0, z_bottom), (YIELD_W / 2, z_bottom + h), (-YIELD_W / 2, z_bottom + h)]
        b.plate(pts, y, "yield")
        return z_bottom + h
    if kind in ("dogs", "ped", "dead_end"):
        d = DIAMOND_SIDE * math.sqrt(2)
        cz = z_bottom + d / 2
        pts = [(0, z_bottom), (d / 2, cz), (0, z_bottom + d), (-d / 2, cz)]
        b.plate(_round_corners(pts, 0.035 * S), y, kind)
        return z_bottom + d
    wh = {"speed25": SPEED_WH, "slow_plaque": PLAQUE_WH, "arrow_plaque": ARROW_WH, "no_parking": NOPARK_WH,
          "bus_stop": BUS_WH}[kind]
    b.plate(_rrect(wh[0], wh[1], CORNER_R, z_bottom + wh[1] / 2), y, kind)
    return z_bottom + wh[1]


def _round_corners(pts, r):
    """Chamfer each corner of a convex polygon (2 points per corner)."""
    out = []
    n = len(pts)
    for i in range(n):
        p, a, c = Vector(pts[i]), Vector(pts[i - 1]), Vector(pts[(i + 1) % n])
        out.append(tuple(p + (a - p).normalized() * r))
        out.append(tuple(p + (c - p).normalized() * r))
    return out


def sign(kinds, name):
    """Post with blades stacked top-down: kinds[0] is the top blade."""
    b = PropBuilder(name)
    z = SIGN_BOTTOM
    tops = []
    for k in reversed(kinds):
        z = _blade(b, k, z) + SIGN_GAP
        tops.append(z)
    _sign_post(b, z - SIGN_GAP - 0.03 * S)
    return b.build()


def stop_sign(name="stop_sign"):
    return sign(["stop"], name)


def yield_sign(name="yield_sign"):
    return sign(["yield"], name)


def speed_limit_sign(name="speed_limit_25"):
    return sign(["speed25"], name)


def dogs_at_play_sign(name="slow_dogs_at_play"):
    return sign(["dogs", "slow_plaque"], name)


def ped_crossing_sign(name="pedestrian_crossing_ahead"):
    return sign(["ped"], name)


def crosswalk_sign(name="crosswalk_sign"):
    return sign(["ped", "arrow_plaque"], name)


def dead_end_sign(name="dead_end"):
    return sign(["dead_end"], name)


def no_parking_sign(name="no_parking"):
    return sign(["no_parking"], name)


def bus_stop_sign(name="bus_stop_sign"):
    return sign(["bus_stop"], name)


# ---------------------------------------------------------------------------
# lights
# ---------------------------------------------------------------------------
def lamp_lantern(name="lamp_post_lantern"):
    """Classic residential post-top lantern: turned base, slim pole, collar,
    six-sided glass lantern (emissive) with corner posts, flared cap, finial."""
    b = PropBuilder(name)
    k = lambda pr: [(r * S, z * S) for r, z in pr]
    b.lathe(k([(0.17, 0.0), (0.17, 0.06), (0.135, 0.09), (0.12, 0.30), (0.085, 0.46), (0.095, 0.50),
               (0.065, 0.55)]), "lamp_black", n=8)
    b.tube([(0, 0, 0.55 * S), (0, 0, 3.3 * S)], [0.06 * S, 0.05 * S], "lamp_black", n=8, cap=False)
    b.lathe(k([(0.05, 3.3), (0.085, 3.33), (0.085, 3.38), (0.055, 3.42), (0.12, 3.49), (0.145, 3.53),
               (0.145, 3.56), (0.0, 3.56)]), "lamp_black", n=8)
    g0, g1, r0, r1 = 3.56 * S, 3.98 * S, 0.12 * S, 0.19 * S
    ang = [math.radians(30 + 60 * i) for i in range(6)]
    for i in range(6):
        j = (i + 1) % 6
        vs = [(r0 * math.cos(ang[i]), r0 * math.sin(ang[i]), g0), (r0 * math.cos(ang[j]), r0 * math.sin(ang[j]), g0),
              (r1 * math.cos(ang[j]), r1 * math.sin(ang[j]), g1), (r1 * math.cos(ang[i]), r1 * math.sin(ang[i]), g1)]
        mid = math.radians(60 + 60 * i)
        b.pface(vs, "glass", (math.cos(mid), math.sin(mid), 0))
        b.beam((r0 * math.cos(ang[i]), r0 * math.sin(ang[i]), g0), (r1 * math.cos(ang[i]), r1 * math.sin(ang[i]), g1),
               0.022 * S, 0.022 * S, "lamp_black", up=(math.cos(ang[i]), math.sin(ang[i]), 0), ends=False)
    b.lathe(k([(0.17, 3.98), (0.215, 4.0), (0.215, 4.03), (0.11, 4.16), (0.04, 4.21), (0.03, 4.27), (0.045, 4.3),
               (0.0, 4.36)]), "lamp_black", n=6, rot=30)
    return b.build()


def lamp_cobra(name="lamp_post_cobra"):
    """Contemporary street light: galvanised tapered pole on a concrete pier,
    curved davit arm reaching over the road (-Y), LED cobra-head with an
    emissive lens underneath."""
    b = PropBuilder(name)
    H = 6.4
    b.frustum((-0.2, -0.2, 0.2, 0.2), (-0.17, -0.17, 0.17, 0.17), 0.0, 0.35, "concrete", tile=CONC_TILE)
    b.box((-0.16, -0.16, 0.35), (0.16, 0.16, 0.38), "pole_galv", skip=("-z",))
    b.lathe([(0.11, 0.38), (0.10, 0.5), (0.085, 0.58)], "pole_galv", n=8)
    b.tube([(0, 0, 0.58), (0, 0, H)], [0.085, 0.06], "pole_galv", n=8, cap=True)
    arm = []
    for i in range(7):
        t = i / 6
        a = math.radians(90 * t)
        arm.append((0, -(1 - math.cos(a)) * 0.9 - t * 0.9, H - 0.6 + math.sin(a) * 0.55 + t * 0.12))
    b.tube(arm, [0.045, 0.035], "pole_galv", n=6, cap=False)
    tip = Vector(arm[-1])
    hc = tip + Vector((0, -0.32, -0.02))
    b.ellipsoid(hc, (0.17, 0.40, 0.11), "cobra_grey", nu=8, nv=4, zcut=hc.z - 0.02)
    lens = [hc + Vector((0.14 * math.cos(a), 0.34 * math.sin(a), -0.02)) for a in
            [2 * math.pi * i / 8 for i in range(8)]]
    b.oface(lens, [(0.5, 0.5)] * 8, "lens", (0, 0, -1))
    return b.build()


# ---------------------------------------------------------------------------
# benches
# ---------------------------------------------------------------------------
def bench_park(name="bench_park"):
    """Park bench: cast-iron ends (legs, arm, back support) and stained
    wooden slats; faces -Y."""
    b = PropBuilder(name)
    L = 1.6 * S
    zs, d = 0.44 * S, 0.5 * S
    for x in (-L / 2 + 0.12 * S, L / 2 - 0.12 * S):
        w = 0.05 * S
        b.beam((x, -d / 2 + 0.04, 0), (x, -d / 2 + 0.06, zs), w, 0.05 * S, "bench_iron", up=(0, 1, 0))
        b.beam((x, d / 2, 0), (x, d / 2 - 0.08, zs), w, 0.05 * S, "bench_iron", up=(0, 1, 0))
        b.beam((x, -d / 2 + 0.02, zs - 0.02), (x, d / 2 + 0.02, zs - 0.02), w, 0.05 * S, "bench_iron")
        b.beam((x, d / 2 - 0.08, zs), (x, d / 2 + 0.06, zs + 0.42 * S), w, 0.05 * S, "bench_iron", up=(0, 1, 0))
        b.beam((x, -d / 2 + 0.05, zs + 0.2 * S), (x, d / 2 - 0.02, zs + 0.24 * S), w * 1.1, 0.045 * S, "bench_iron")
        b.beam((x, -d / 2 + 0.05, zs), (x, -d / 2 + 0.05, zs + 0.2 * S), w, 0.045 * S, "bench_iron", up=(0, 1, 0))
    sw, gap = 0.085 * S, 0.02 * S
    for i in range(4):
        y0 = -d / 2 + i * (sw + gap)
        b.box((-L / 2, y0, zs), (L / 2, y0 + sw, zs + 0.035 * S), "bench_wood", tile=WOOD_TILE, grain=(1, 0, 0))
    for i in range(3):
        f = 0.12 + i * 0.12
        y, z = d / 2 - 0.08 + f * 0.33, zs + f * S * 1.0 + 0.04
        b.beam((-L / 2, y + 0.03, z), (L / 2, y + 0.03, z), 0.03 * S, 0.09 * S, "bench_wood", up=(0, 0.3, 1),
               tile=WOOD_TILE)
    return b.build()


def bench_bus(name="bench_bus"):
    """Bus-stop bench: powder-coated steel frame on three legs with a centre
    armrest (no lying down), steel slats, backrest; faces -Y."""
    b = PropBuilder(name)
    L = 1.9 * S
    zs, d = 0.45 * S, 0.48 * S
    for x in (-L / 2 + 0.05, 0.0, L / 2 - 0.05):
        b.beam((x, -d / 2 + 0.06, 0), (x, -d / 2 + 0.08, zs), 0.06, 0.06, "bus_green", up=(0, 1, 0))
        b.beam((x, d / 2 - 0.06, 0), (x, d / 2 - 0.02, zs), 0.06, 0.06, "bus_green", up=(0, 1, 0))
        b.box((x - 0.035, -d / 2 + 0.02, 0), (x + 0.035, d / 2, 0.03), "bus_green", skip=("-z",))
        b.beam((x, -d / 2 + 0.02, zs - 0.03), (x, d / 2, zs - 0.03), 0.05, 0.05, "bus_green")
        b.beam((x, d / 2 - 0.04, zs), (x, d / 2 + 0.1, zs + 0.45 * S), 0.05, 0.05, "bus_green", up=(0, 1, 0))
        b.beam((x, -d / 2 + 0.05, zs + 0.22 * S), (x, d / 2 - 0.04, zs + 0.24 * S), 0.06, 0.04, "bus_green")
        b.beam((x, -d / 2 + 0.06, zs), (x, -d / 2 + 0.06, zs + 0.22 * S), 0.05, 0.04, "bus_green", up=(0, 1, 0))
    sw, gap = 0.07 * S, 0.018 * S
    for i in range(5):
        y0 = -d / 2 + i * (sw + gap)
        b.box((-L / 2, y0, zs), (L / 2, y0 + sw, zs + 0.025 * S), "bus_green", grain=(1, 0, 0))
    for i in range(4):
        z = zs + 0.08 + i * 0.09 * S
        y = d / 2 - 0.02 + (z - zs) * 0.31
        b.beam((-L / 2, y + 0.03, z), (L / 2, y + 0.03, z), 0.022 * S, 0.075 * S, "bus_green", up=(0, 0.3, 1))
    return b.build()


# ---------------------------------------------------------------------------
# hydrant, mailbox, bins, waste station, bike rack, newspaper box
# ---------------------------------------------------------------------------
def hydrant(name="fire_hydrant"):
    """Fire hydrant: flanged base with bolt heads, barrel, bonnet dome with a
    pentagon nut, two hose nozzles and a pumper nozzle facing -Y."""
    b = PropBuilder(name)
    k = lambda pr: [(r * S, z * S) for r, z in pr]
    b.lathe(k([(0.17, 0.0), (0.17, 0.045), (0.14, 0.065), (0.12, 0.085), (0.115, 0.50), (0.145, 0.52),
               (0.145, 0.56), (0.12, 0.58), (0.11, 0.66), (0.07, 0.73), (0.0, 0.75)]), "hydrant_red", n=10, smooth=40)
    b.lathe(k([(0.035, 0.735), (0.035, 0.80), (0.0, 0.80)]), "hydrant_cap", n=5)
    for i in range(6):
        a = 2 * math.pi * (i + 0.5) / 6
        x, y = 0.155 * S * math.cos(a), 0.155 * S * math.sin(a)
        b.box((x - 0.012 * S, y - 0.012 * S, 0.045 * S), (x + 0.012 * S, y + 0.012 * S, 0.07 * S), "hydrant_red",
              skip=("-z",))
    for sx in (-1, 1):
        b.tube([(sx * 0.10 * S, 0, 0.40 * S), (sx * 0.17 * S, 0, 0.40 * S)], 0.04 * S, "hydrant_red", n=8, cap=False)
        b.tube([(sx * 0.17 * S, 0, 0.40 * S), (sx * 0.21 * S, 0, 0.40 * S)], 0.046 * S, "hydrant_cap", n=6)
    b.tube([(0, -0.10 * S, 0.33 * S), (0, -0.17 * S, 0.33 * S)], 0.06 * S, "hydrant_red", n=8, cap=False)
    b.tube([(0, -0.17 * S, 0.33 * S), (0, -0.22 * S, 0.33 * S)], 0.066 * S, "hydrant_cap", n=6)
    return b.build()


def mailbox(name="mailbox"):
    """Curbside mailbox on a white post: arched box (door faces -Y), red flag."""
    b = PropBuilder(name)
    pw = 0.1 * S
    b.box((-pw / 2, -pw / 2, 0), (pw / 2, pw / 2, 1.0 * S), "post_white", grain=(0, 0, 1))
    b.box((-pw * 0.7, -pw * 0.7, 1.0 * S), (pw * 0.7, pw * 0.7, 1.03 * S), "post_white")
    b.beam((0, -0.08 * S, 0.80 * S), (0, 0.0, 0.98 * S), 0.05 * S, 0.05 * S, "post_white", up=(0, 1, 0))
    W, L, h0 = 0.18 * S, 0.48 * S, 0.11 * S
    y0, y1, zb = -0.30 * S, -0.30 * S + L, 1.03 * S
    b.box((-W / 2, y0, zb), (W / 2, y1, zb + h0), "mailbox", skip=("+z", "-y"))
    arc = [(W / 2 * math.cos(math.pi * i / 6), zb + h0 + W / 2 * math.sin(math.pi * i / 6)) for i in range(7)]
    for i in range(6):
        (xa, za), (xb, zb_) = arc[i], arc[i + 1]
        vs = [(xa, y0, za), (xb, y0, zb_), (xb, y1, zb_), (xa, y1, za)]
        na = math.pi * i / 6
        nb = math.pi * (i + 1) / 6
        nr = [Vector((math.cos(na), 0, math.sin(na))), Vector((math.cos(nb), 0, math.sin(nb))),
              Vector((math.cos(nb), 0, math.sin(nb))), Vector((math.cos(na), 0, math.sin(na)))]
        uv = [(i / 6, 0), ((i + 1) / 6, 0), ((i + 1) / 6, L / TILE), (i / 6, L / TILE)]
        b.oface(vs, uv, "mailbox", ((xa + xb) / 2, 0, (za + zb_) / 2 - zb - h0), nr)
    door = [(W / 2 + 0.006, zb - 0.006)] + [(x * 1.03, z + 0.004) for x, z in arc] + [(-W / 2 - 0.006, zb - 0.006)]
    dy = y0 - 0.012 * S
    b.pface([(x, dy, z) for x, z in door], "mailbox", (0, -1, 0))
    for (xa, za), (xb, zb_) in zip(door, door[1:] + door[:1]):
        b.pface([(xa, dy, za), (xb, dy, zb_), (xb, y0, zb_), (xa, y0, za)], "mailbox", ((xa + xb) / 2, 0, (za + zb_) / 2 - zb - h0 / 2))
    b.box((-0.02 * S, dy - 0.03 * S, zb + h0 + 0.03 * S), (0.02 * S, dy, zb + h0 + 0.05 * S), "hydrant_cap")
    fx = W / 2 + 0.006
    b.box((fx, y0 + 0.30 * S, zb + 0.03 * S), (fx + 0.008, y0 + 0.33 * S, zb + h0 + 0.14 * S), "flag")
    b.box((fx, y0 + 0.30 * S, zb + h0 + 0.06 * S), (fx + 0.008, y0 + 0.42 * S, zb + h0 + 0.14 * S), "flag")
    return b.build()


def _cart(b, mat, label=False):
    """Wheeled curbside cart (96 gal), lid hinged at the back (+Y), handle
    and wheels at the back; front faces -Y."""
    W, D, H = 0.60 * S, 0.70 * S, 0.95 * S
    b.frustum((-W / 2 + 0.04, -D / 2 + 0.06, W / 2 - 0.04, D / 2 - 0.02), (-W / 2, -D / 2, W / 2, D / 2),
              0.05, H, mat, caps=(True, False))
    b.box((-W / 2 - 0.02, -D / 2 - 0.035, H), (W / 2 + 0.02, D / 2 + 0.02, H + 0.05), mat)
    b.tube([(-W / 2 + 0.03, D / 2 + 0.06, H - 0.02), (W / 2 - 0.03, D / 2 + 0.06, H - 0.02)], 0.022, mat, n=6,
           cap0=True)
    b.box((-W / 2 + 0.04, D / 2 - 0.02, H - 0.08), (W / 2 - 0.04, D / 2 + 0.05, H - 0.01), mat)
    for sx in (-1, 1):
        b.tube([(sx * (W / 2 - 0.02), D / 2 - 0.03, 0.11), (sx * (W / 2 + 0.05), D / 2 - 0.03, 0.11)], 0.11,
               "rubber", n=8, cap0=True)
    b.box((-W / 2 + 0.06, -D / 2 + 0.06, 0.0), (-W / 2 + 0.12, -D / 2 + 0.12, 0.06), mat, skip=("-z",))
    b.box((W / 2 - 0.12, -D / 2 + 0.06, 0.0), (W / 2 - 0.06, -D / 2 + 0.12, 0.06), mat, skip=("-z",))
    if label:
        zl0, zl1, ww = 0.45, 0.82, 0.34
        f = lambda z: -D / 2 + 0.06 * (1 - (z - 0.05) / (H - 0.05)) - 0.003
        b.label_quad([(-ww / 2, f(zl0), zl0), (ww / 2, f(zl0), zl0), (ww / 2, f(zl1), zl1), (-ww / 2, f(zl1), zl1)],
                     "recycle", (0, -1, 0.05))


def trash_bin(name="trash_bin"):
    b = PropBuilder(name)
    _cart(b, "trash")
    return b.build()


def recycling_bin(name="recycling_bin"):
    b = PropBuilder(name)
    _cart(b, "recycle", label=True)
    return b.build()


def dog_waste_station(name="dog_waste_station"):
    """Green post with a bag-dispenser box (label panel faces -Y) and a
    lidded waste can beside it."""
    b = PropBuilder(name)
    pw = 0.07 * S
    b.box((-pw / 2, -pw / 2, 0), (pw / 2, pw / 2, 1.55 * S), "waste_green", skip=("-z",))
    b.box((-0.17 * S, -0.10 * S, 1.05 * S), (0.17 * S, pw / 2, 1.50 * S), "waste_green")
    b.frustum((-0.19 * S, -0.12 * S, 0.19 * S, pw / 2 + 0.01), (-0.08 * S, -0.06 * S, 0.08 * S, pw / 2),
              1.50 * S, 1.56 * S, "waste_green")
    b.label_quad([(-0.15 * S, -0.10 * S - 0.003, 1.07 * S), (0.15 * S, -0.10 * S - 0.003, 1.07 * S),
                  (0.15 * S, -0.10 * S - 0.003, 1.48 * S), (-0.15 * S, -0.10 * S - 0.003, 1.48 * S)], "waste",
                 (0, -1, 0))
    b.box((-0.11 * S, -0.115 * S, 1.06 * S), (0.11 * S, -0.10 * S, 1.09 * S), "rubber")
    cx = 0.36 * S
    b.lathe([(0.16 * S, 0.0), (0.18 * S, 0.70 * S), (0.19 * S, 0.70 * S), (0.19 * S, 0.74 * S), (0.12 * S, 0.80 * S),
             (0.0, 0.81 * S)], "waste_green", n=10, c=(cx, 0.0), smooth=30)
    b.tube([(cx - 0.06 * S, 0, 0.82 * S), (cx + 0.06 * S, 0, 0.82 * S)], 0.015 * S, "rubber", n=5)
    return b.build()


def bike_rack(name="bike_rack"):
    """Three inverted-U galvanised hoops, 0.75 m apart, set into the ground."""
    b = PropBuilder(name)
    H, R = 0.85 * S, 0.30 * S
    for x in (-0.75 * S, 0.0, 0.75 * S):
        pts = [(x, -R, -0.02), (x, -R, H - R)]
        for i in range(1, 6):
            a = math.pi - math.pi * i / 6
            pts.append((x, R * math.cos(a), H - R + R * math.sin(a)))
        pts += [(x, R, H - R), (x, R, -0.02)]
        b.tube(pts, 0.026 * S, "galv", n=6, cap=False)
    return b.build()


def newspaper_box(name="newspaper_box"):
    """Coin-op newspaper box on a pedestal; printed front (window, NEWS
    header, coin plate) faces -Y; sloped lid."""
    b = PropBuilder(name)
    W, D = 0.50 * S, 0.42 * S
    z0, z1, z2 = 0.42 * S, 1.02 * S, 1.07 * S
    b.box((-W / 2, -D / 2, z0), (W / 2, D / 2, z1), "news_navy", skip=("-y",))
    b.label_quad([(-W / 2, -D / 2, z0), (W / 2, -D / 2, z0), (W / 2, -D / 2, z1), (-W / 2, -D / 2, z1)], "news",
                 (0, -1, 0))
    b.frustum((-W / 2 - 0.015, -D / 2 - 0.03, W / 2 + 0.015, D / 2 + 0.01),
              (-W / 2 - 0.015, -D / 2 - 0.03, W / 2 + 0.015, D / 2 + 0.01), z1, z1 + 0.025, "news_navy",
              caps=(True, False))
    top = [(-W / 2 - 0.015, -D / 2 - 0.03, z1 + 0.025), (W / 2 + 0.015, -D / 2 - 0.03, z1 + 0.025),
           (W / 2 + 0.015, D / 2 + 0.01, z2 + 0.025), (-W / 2 - 0.015, D / 2 + 0.01, z2 + 0.025)]
    b.pface(top, "news_navy", (0, -0.08, 1))
    b.pface([top[1], (W / 2 + 0.015, D / 2 + 0.01, z1 + 0.025), top[2]], "news_navy", (1, 0, 0))
    b.pface([top[0], top[3], (-W / 2 - 0.015, D / 2 + 0.01, z1 + 0.025)], "news_navy", (-1, 0, 0))
    b.pface([(W / 2 + 0.015, D / 2 + 0.01, z1 + 0.025), (-W / 2 - 0.015, D / 2 + 0.01, z1 + 0.025), top[3], top[2]],
            "news_navy", (0, 1, 0))
    b.box((-0.06 * S, -0.06 * S, 0.03), (0.06 * S, 0.06 * S, z0), "news_navy", skip=("-z",))
    b.box((-0.22 * S, -0.18 * S, 0.0), (0.22 * S, 0.18 * S, 0.03), "news_navy", skip=("-z",))
    return b.build()


# ---------------------------------------------------------------------------
# utility pole + wires
# ---------------------------------------------------------------------------
POLE_H = 10.0
ARM_Z = 9.3
ARM_L = 2.4
WIRE_Y = (-1.0, 1.0)          # conductors on the crossarm (arm runs across the street, along Y)
COMM_Z = 6.2
SPAN = 30.0
SAG = 0.45


def utility_pole(name="utility_pole"):
    """Creosoted wood pole: crossarm (along Y, across the wire run) with
    braces, two arm insulators + one pole-top insulator, a transformer can,
    a lower communications clamp. Wires run along X."""
    b = PropBuilder(name)
    b.tube([(0, 0, -0.1), (0, 0, POLE_H)], [0.16, 0.12], "pole_wood", n=8, tile=1.4)
    b.box((-0.05, -ARM_L / 2, ARM_Z - 0.05), (0.05, ARM_L / 2, ARM_Z + 0.05), "pole_wood", tile=1.4, grain=(0, 1, 0))
    for sy in (-1, 1):
        b.beam((0.08, 0, ARM_Z - 0.75), (0.08, sy * 0.6, ARM_Z - 0.04), 0.03, 0.02, "pole_galv", up=(1, 0, 0))
    ins = [(0.0, y, ARM_Z + 0.05) for y in WIRE_Y] + [(0.0, 0.0, POLE_H)]
    for x, y, zb in ins:
        b.lathe([(0.03, zb), (0.03, zb + 0.04), (0.06, zb + 0.06), (0.045, zb + 0.10), (0.06, zb + 0.13),
                 (0.03, zb + 0.18), (0.0, zb + 0.19)], "insulator", n=6, c=(x, y))
    tc = (-0.36, 0.0)
    b.lathe([(0.2, 7.6), (0.21, 7.62), (0.21, 8.45), (0.22, 8.47), (0.15, 8.56), (0.0, 8.58)], "transformer", n=8,
            c=tc)
    b.box((-0.18, -0.05, 7.9), (-0.12, 0.05, 8.3), "pole_galv")
    b.box((-0.08, -0.12, COMM_Z - 0.06), (0.08, 0.12, COMM_Z + 0.06), "pole_galv")
    return b.build()


def wire_ends():
    """(y, z) of each wire where it leaves a pole: arm conductors, pole-top
    conductor, comms cable."""
    return [(y, ARM_Z + 0.24) for y in WIRE_Y] + [(0.0, POLE_H + 0.19), (0.12, COMM_Z)]


def utility_wires(name="utility_wires", span=SPAN, seg=12):
    """Wires for one span between two poles at x = 0 and x = span, sagging."""
    b = PropBuilder(name)
    for (y, z), r in zip(wire_ends(), (0.018, 0.018, 0.018, 0.028)):
        pts = [(span * i / seg, y, z - SAG * 4 * (i / seg) * (1 - i / seg)) for i in range(seg + 1)]
        b.tube(pts, r, "wire", n=4, cap=False, tile=2.0)
    return b.build()


# ---------------------------------------------------------------------------
# road decals
# ---------------------------------------------------------------------------
def manhole(name="manhole_cover"):
    b = PropBuilder(name)
    R = 0.33 * S
    n = 12
    pts = [(R * math.cos(2 * math.pi * i / n), R * math.sin(2 * math.pi * i / n), DECAL_Z) for i in range(n)]
    uv = [PT.label_uv("manhole", 0.5 + 0.5 * math.cos(2 * math.pi * i / n), 0.5 + 0.5 * math.sin(2 * math.pi * i / n))
          for i in range(n)]
    b.oface(pts, uv, "labels", (0, 0, 1))
    return b.build()


def storm_drain(name="storm_drain"):
    """Curb-inlet grate, long side along X (parallel to the curb at +Y)."""
    b = PropBuilder(name)
    Lx, Wy = 0.92 * S, 0.56 * S
    b.oface([(-Lx / 2, -Wy, DECAL_Z), (Lx / 2, -Wy, DECAL_Z), (Lx / 2, 0, DECAL_Z), (-Lx / 2, 0, DECAL_Z)],
            [PT.label_uv("storm", 0, 0), PT.label_uv("storm", 0, 1), PT.label_uv("storm", 1, 1),
             PT.label_uv("storm", 1, 0)], "labels", (0, 0, 1))
    return b.build()


CROSSWALK_LEN = 3.0     # stripe length along traffic (= walk width)
STRIPE_W, STRIPE_PITCH = 0.5, 1.0
ROAD_W = 6.0


def _stripe(b, x0, x1, y0, y1):
    b.oface([(x0, y0, DECAL_Z), (x1, y0, DECAL_Z), (x1, y1, DECAL_Z), (x0, y1, DECAL_Z)],
            [PT.label_uv("paint", 0, 0), PT.label_uv("paint", 1, 0), PT.label_uv("paint", 1, 1),
             PT.label_uv("paint", 0, 1)], "labels", (0, 0, 1))


def crosswalk(name="crosswalk", road_w=ROAD_W):
    """Continental crosswalk across a road whose centreline runs along X:
    bars parallel to traffic, spaced across the road (origin = road centre)."""
    b = PropBuilder(name)
    n = int(road_w / STRIPE_PITCH)
    for i in range(n):
        yc = -road_w / 2 + STRIPE_PITCH * (i + 0.5)
        _stripe(b, -CROSSWALK_LEN / 2, CROSSWALK_LEN / 2, yc - STRIPE_W / 2, yc + STRIPE_W / 2)
    return b.build()


def stop_line(name="stop_line", lane_w=ROAD_W / 2):
    """Stop bar across one lane (lane from y = 0 to y = lane_w)."""
    b = PropBuilder(name)
    _stripe(b, -0.2, 0.2, 0.15, lane_w - 0.15)
    return b.build()


PIECES = {
    "lamp_post_lantern": (lamp_lantern, "Lamp post, lantern"),
    "lamp_post_cobra": (lamp_cobra, "Street light, cobra-head"),
    "stop_sign": (stop_sign, "Stop"),
    "yield_sign": (yield_sign, "Yield"),
    "speed_limit_25": (speed_limit_sign, "Speed limit 25"),
    "slow_dogs_at_play": (dogs_at_play_sign, "Slow - Dogs at Play"),
    "pedestrian_crossing_ahead": (ped_crossing_sign, "Pedestrian crossing"),
    "crosswalk_sign": (crosswalk_sign, "Crosswalk (with arrow)"),
    "dead_end": (dead_end_sign, "Dead end"),
    "no_parking": (no_parking_sign, "No parking"),
    "bus_stop_sign": (bus_stop_sign, "Bus stop sign"),
    "bench_park": (bench_park, "Park bench"),
    "bench_bus": (bench_bus, "Bus-stop bench"),
    "fire_hydrant": (hydrant, "Fire hydrant"),
    "mailbox": (mailbox, "Mailbox"),
    "trash_bin": (trash_bin, "Trash cart"),
    "recycling_bin": (recycling_bin, "Recycling cart"),
    "dog_waste_station": (dog_waste_station, "Dog-waste station"),
    "bike_rack": (bike_rack, "Bike rack"),
    "newspaper_box": (newspaper_box, "Newspaper box"),
    "utility_pole": (utility_pole, "Utility pole"),
    "utility_wires": (utility_wires, "Wires, one 30 m span"),
    "manhole_cover": (manhole, "Manhole cover (decal)"),
    "storm_drain": (storm_drain, "Storm drain (decal)"),
    "crosswalk": (crosswalk, "Crosswalk (decal)"),
    "stop_line": (stop_line, "Stop line (decal)"),
}
