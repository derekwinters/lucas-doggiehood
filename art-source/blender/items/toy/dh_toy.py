"""The dog toys (#332): five designs, each built from named dimensions.

Each design is one mesh, `toy_<design>`, made from three shared primitives built
in a fixed order (ellipsoids, lathed solids and tubes along a path), see
DESIGNS at the end:

  disc            1. solid flying disc (frisbee)
  chew_cone       2. rubber chew cone, three stacked lobes
  hotdog          3. squeaky plush hot dog
  rubber_chicken  4. long rubber gag chicken
  rope_knot       5. two-tone rope with one chunky knot and frayed tassels

Plush and rope parts share one tileable fleece texture pair (dh_toy_tex.py),
tinted per material; rubber, plastic and the eyes are plain colour.

Conventions (docs/engineering/blender-pipeline.md, same as the street props):
1 unit = 1 m at in-game size, front faces -Y, up is +Z, and the pivot is on
the ground at the centre of the footprint.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

TRI_BUDGET = 6000          # provisional per-mesh backstop of the art budget (#799)
FLEECE_M = 0.12            # metres of fabric per repeat of the fleece texture

ROUGHNESS = {"plush": 0.95, "eye": 0.25, "rubber": 0.45, "plastic": 0.35}
SPECULAR_LEVEL = 0.2


def _rot(deg_x=0.0, deg_z=0.0):
    return Matrix.Rotation(math.radians(deg_z), 3, "Z") @ Matrix.Rotation(math.radians(deg_x), 3, "X")


def _mirror(spec, side):
    c = spec["c"]
    return dict(spec, c=(side * c[0], c[1], c[2]))


def _uv_repeats(r):
    """Whole repeats of the fleece around and along a part, so the texture meets itself at the seam."""
    mean = sum(r) / 3.0
    return (max(1, round(2 * math.pi * mean / FLEECE_M)), max(1, round(math.pi * mean / FLEECE_M)))


def _hex_lin(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb) + (1.0,)


def _plush_material(name, hex_, tex):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    col = nt.nodes.new("ShaderNodeTexImage")
    col.image = bpy.data.images.load(tex["color"], check_existing=True)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs[7].default_value = _hex_lin(hex_)
    nt.links.new(col.outputs["Color"], mix.inputs[6])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    nrm = nt.nodes.new("ShaderNodeTexImage")
    nrm.image = bpy.data.images.load(tex["normal"], check_existing=True)
    nrm.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nrm.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = ROUGHNESS["plush"]
    bsdf.inputs["Specular IOR Level"].default_value = SPECULAR_LEVEL
    mat.diffuse_color = _hex_lin(hex_)
    mat.use_backface_culling = True   # closed parts: exported single-sided
    return mat


def _plain_material(name, hex_, roughness=ROUGHNESS["eye"]):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = _hex_lin(hex_)
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = _hex_lin(hex_)
    mat.use_backface_culling = True   # closed parts: exported single-sided
    return mat




def material_name(key):
    return "DH_Toy_" + key


# ---------------------------------------------------------------------------
# Builder: the shared primitives. Every vertex and face is created in a fixed
# order, so the same design gives the same mesh bytes every run.
# ---------------------------------------------------------------------------
class Builder:
    def __init__(self, slots):
        """slots: ordered {material key: (hex, kind)}, kind = plush | rubber | plastic | eye."""
        self.slots = slots
        self.keys = list(slots)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _tri(self, vs, uvs, mi, rep):
        f = self.bm.faces.new(vs)
        f.material_index = mi
        for loop, (u, w) in zip(f.loops, uvs):
            loop[self.uv].uv = (u * rep[0], w * rep[1])

    def ellipsoid(self, spec, mat, rot=None, deform=None):
        """A deformed UV sphere: spec = dict(c=centre, r=radii, seg=(u, v)). Poles on local Z."""
        bm = self.bm
        mi = self.keys.index(mat)
        r, c = Vector(spec["r"]), Vector(spec["c"])
        rot = rot or Matrix.Identity(3)
        nu, nv = spec["seg"]
        rep = _uv_repeats(spec["r"])

        def place(local):
            co = rot @ Vector((local[0] * r.x, local[1] * r.y, local[2] * r.z)) + c
            if deform:
                deform(co)
            return bm.verts.new(co)

        # rings from the top pole (i = 0) to the bottom pole (i = nv), built in a fixed order
        # (bmesh.ops.create_uvsphere does not give the same face winding order every run)
        top, bottom = place((0.0, 0.0, 1.0)), place((0.0, 0.0, -1.0))
        rings = []
        for i in range(1, nv):
            th = math.pi * i / nv
            rings.append([place((math.sin(th) * math.cos(2 * math.pi * j / nu),
                                 math.sin(th) * math.sin(2 * math.pi * j / nu), math.cos(th)))
                          for j in range(nu)])
        for j in range(nu):
            k = (j + 1) % nu
            u0, u1, um = j / nu, (j + 1) / nu, (j + 0.5) / nu
            self._tri((top, rings[0][j], rings[0][k]), ((um, 1.0), (u0, 1 - 1 / nv), (u1, 1 - 1 / nv)), mi, rep)
            for i in range(nv - 2):
                a, b = rings[i], rings[i + 1]
                w0, w1 = 1 - (i + 1) / nv, 1 - (i + 2) / nv
                self._tri((a[j], b[j], b[k]), ((u0, w0), (u0, w1), (u1, w1)), mi, rep)
                self._tri((a[j], b[k], a[k]), ((u0, w0), (u1, w1), (u1, w0)), mi, rep)
            self._tri((rings[-1][j], bottom, rings[-1][k]), ((u0, 1 / nv), (um, 0.0), (u1, 1 / nv)), mi, rep)

    def lathe(self, profile, mat, n, centre=(0.0, 0.0)):
        """A solid of revolution about the vertical axis through `centre`.

        profile: [(radius, z), ...] traced from the top of the axis, round the outside, back to
        the axis at the bottom (the solid on the right of the direction of travel); the first
        and last points have radius 0 and become poles."""
        bm = self.bm
        mi = self.keys.index(mat)
        lengths = [0.0]
        for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
            lengths.append(lengths[-1] + math.hypot(r1 - r0, z1 - z0))
        mean_r = max(r for r, _ in profile)
        rep = (max(1, round(2 * math.pi * mean_r / FLEECE_M)), 1.0 / FLEECE_M)
        cx, cy = centre
        top = bm.verts.new((cx, cy, profile[0][1]))
        bottom = bm.verts.new((cx, cy, profile[-1][1]))
        rings = [[bm.verts.new((cx + r * math.cos(2 * math.pi * j / n), cy + r * math.sin(2 * math.pi * j / n), z))
                  for j in range(n)] for r, z in profile[1:-1]]
        L = lengths[-1]
        for j in range(n):
            k = (j + 1) % n
            u0, u1, um = j / n, (j + 1) / n, (j + 0.5) / n
            self._tri((top, rings[0][j], rings[0][k]),
                      ((um, L), (u0, L - lengths[1]), (u1, L - lengths[1])), mi, rep)
            for i in range(len(rings) - 1):
                a, b = rings[i], rings[i + 1]
                w0, w1 = L - lengths[i + 1], L - lengths[i + 2]
                self._tri((a[j], b[j], b[k]), ((u0, w0), (u0, w1), (u1, w1)), mi, rep)
                self._tri((a[j], b[k], a[k]), ((u0, w0), (u1, w1), (u1, w0)), mi, rep)
            self._tri((rings[-1][j], bottom, rings[-1][k]),
                      ((u0, L - lengths[-2]), (um, 0.0), (u1, L - lengths[-2])), mi, rep)

    def tube(self, path, radii, mat, n, caps=True):
        """A round tube along a path of points; radii is one radius or one per point.
        Frames are parallel-transported along the path, so the tube never twists."""
        bm = self.bm
        mi = self.keys.index(mat)
        pts = [Vector(p) for p in path]
        rs = list(radii) if isinstance(radii, (list, tuple)) else [radii] * len(pts)
        tans = []
        for i in range(len(pts)):
            a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
            tans.append((b - a).normalized())
        ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
        n1 = (ref - tans[0] * ref.dot(tans[0])).normalized()
        frames = []
        for i, t in enumerate(tans):
            if i:
                n1 = (n1 - t * n1.dot(t)).normalized()
            frames.append((n1.copy(), t.cross(n1)))
        lengths = [0.0]
        for a, b in zip(pts, pts[1:]):
            lengths.append(lengths[-1] + (b - a).length)
        rep = (max(1, round(2 * math.pi * max(rs) / FLEECE_M)), 1.0 / FLEECE_M)
        rings = [[bm.verts.new(p + r * (math.cos(2 * math.pi * j / n) * f1 + math.sin(2 * math.pi * j / n) * f2))
                  for j in range(n)] for p, r, (f1, f2) in zip(pts, rs, frames)]
        for j in range(n):
            k = (j + 1) % n
            u0, u1 = j / n, (j + 1) / n
            for i in range(len(rings) - 1):
                a, b = rings[i], rings[i + 1]
                w0, w1 = lengths[i], lengths[i + 1]
                self._tri((a[j], a[k], b[k]), ((u0, w0), (u1, w0), (u1, w1)), mi, rep)
                self._tri((a[j], b[k], b[j]), ((u0, w0), (u1, w1), (u0, w1)), mi, rep)
        if caps:
            c0, c1 = bm.verts.new(pts[0]), bm.verts.new(pts[-1])
            for j in range(n):
                k = (j + 1) % n
                u0, u1, um = j / n, (j + 1) / n, (j + 0.5) / n
                self._tri((c0, rings[0][k], rings[0][j]), ((um, 0.0), (u1, 0.0), (u0, 0.0)), mi, rep)
                self._tri((c1, rings[-1][j], rings[-1][k]),
                          ((um, lengths[-1]), (u0, lengths[-1]), (u1, lengths[-1])), mi, rep)

    def finish(self, name, tex, scale=1.0, collection=None):
        """Scale, put the pivot on the ground at the footprint centre, and make the object."""
        bm = self.bm
        for v in bm.verts:
            v.co *= scale
        xs, ys, zs = [v.co.x for v in bm.verts], [v.co.y for v in bm.verts], [v.co.z for v in bm.verts]
        shift = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
        for v in bm.verts:
            v.co -= shift
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for k in self.keys:
            hex_, kind = self.slots[k]
            me.materials.append(_plush_material(material_name(k), hex_, tex) if kind == "plush"
                                else _plain_material(material_name(k), hex_, ROUGHNESS[kind]))
        for p in me.polygons:
            p.use_smooth = True
        ob = bpy.data.objects.new(name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


# ---------------------------------------------------------------------------
# 1. Flying disc: one solid lathed shell, domed top, rolled rim, no hole
# ---------------------------------------------------------------------------
DISC_SLOTS = {"disc_plastic": ("#1F78E8", "plastic")}   # bright solid blue (yellow is the ball's colour)
DISC_SEGMENTS = 48
# (radius, z) from the top centre, out over the dome, a flight ridge and the rolled rim, under the
# lip and back in along the underside. A real 25 cm disc at the world's 1.8x dog scale: 0.45 m across.
DISC_PROFILE = [(0.0, 0.050), (0.080, 0.049), (0.150, 0.046), (0.160, 0.0455), (0.168, 0.0490),
                (0.176, 0.0445), (0.195, 0.042), (0.215, 0.037), (0.225, 0.027), (0.226, 0.015),
                (0.222, 0.004), (0.214, 0.0), (0.205, 0.004), (0.203, 0.020), (0.190, 0.032),
                (0.120, 0.037), (0.0, 0.039)]


def _disc(b):
    b.lathe(DISC_PROFILE, "disc_plastic", DISC_SEGMENTS)


# ---------------------------------------------------------------------------
# 2. Rubber chew cone: three stacked lobes, a hole in the top, flat base
# ---------------------------------------------------------------------------
CONE_SLOTS = {"cone_rubber": ("#9A4CF0", "rubber")}     # bright purple: not red, not the lawn
CONE_SEGMENTS = 32
CONE_LOBES = [(0.075, 0.115, 0.090), (0.175, 0.088, 0.060), (0.248, 0.062, 0.042)]  # (centre z, radius, half-height)
CONE_HOLE_R = 0.024          # the stuffing hole in the top
CONE_HOLE_DEPTH = 0.045
CONE_LIP = 0.008             # rubber between the hole and the outside at the top
CONE_SAMPLES = 40            # profile samples down the outside


def _cone_r(z):
    return max(r * math.sqrt(max(0.0, 1 - ((z - c) / hh) ** 2)) for c, r, hh in CONE_LOBES)


def _cone_profile():
    top = max(c + hh for c, _, hh in CONE_LOBES)
    z_lip = top
    while _cone_r(z_lip) < CONE_HOLE_R + CONE_LIP:
        z_lip -= 0.0005
    prof = [(0.0, z_lip - CONE_HOLE_DEPTH), (CONE_HOLE_R, z_lip - CONE_HOLE_DEPTH + 0.005),
            (CONE_HOLE_R, z_lip - 0.003)]
    for i in range(CONE_SAMPLES + 1):
        z = z_lip * (1 - i / CONE_SAMPLES)
        prof.append((_cone_r(z), z))
    prof.append((0.0, 0.0))
    # from the axis at the hole floor (the lathe's first pole), out along the floor, up the hole
    # wall, over the lip, down the outside and in along the base: the rubber is always on the right
    return prof


def _chew_cone(b):
    b.lathe(_cone_profile(), "cone_rubber", CONE_SEGMENTS)


# ---------------------------------------------------------------------------
# 3. Plush hot dog: two bun halves, a sausage poking out of both ends, mustard zigzag
# ---------------------------------------------------------------------------
HOTDOG_SLOTS = {"hotdog_bun": ("#E9A957", "plush"), "hotdog_sausage": ("#B4492F", "plush"),
                "hotdog_mustard": ("#FFD21F", "plush")}
BUN = dict(c=(0.050, 0.0, 0.065), r=(0.068, 0.210, 0.065), seg=(20, 10))
SAUSAGE = dict(c=(0.0, 0.0, 0.098), r=(0.046, 0.046, 0.046), seg=(16, 12))
SAUSAGE_HALF = 0.205         # half the straight length of the sausage (its round ends add the radius)
SAUSAGE_BEND = 0.018         # the sausage's ends curl up this far
MUSTARD_R = 0.012
MUSTARD_SPAN = 0.17          # half the length of the zigzag along the sausage
MUSTARD_ZIGS = 4.5           # zigzag waves
MUSTARD_SWING = 0.026        # side-to-side
MUSTARD_PTS = 48
MUSTARD_SIDES = 6


def _flat_bottom(co):
    co.z = max(co.z, 0.0)


def _sausage(co):
    c = SAUSAGE["c"]
    d = co.y - c[1]
    if abs(d) > 1e-9:
        co.y += math.copysign(SAUSAGE_HALF, d)
    co.z += SAUSAGE_BEND * (co.y / (SAUSAGE_HALF + SAUSAGE["r"][1])) ** 2


def _hotdog(b):
    for side in (1, -1):
        b.ellipsoid(_mirror(BUN, side), "hotdog_bun", deform=_flat_bottom)
    b.ellipsoid(SAUSAGE, "hotdog_sausage", rot=Matrix.Rotation(math.radians(90), 3, "X"), deform=_sausage)
    top = SAUSAGE["c"][2] + SAUSAGE["r"][2] + MUSTARD_R * 0.4
    path = []
    for i in range(MUSTARD_PTS + 1):
        t = i / MUSTARD_PTS
        y = -MUSTARD_SPAN + 2 * MUSTARD_SPAN * t
        x = MUSTARD_SWING * math.sin(2 * math.pi * MUSTARD_ZIGS * t)
        z = top - (x / SAUSAGE["r"][0]) ** 2 * SAUSAGE["r"][2] * 0.35 + SAUSAGE_BEND * (y / (SAUSAGE_HALF + SAUSAGE["r"][1])) ** 2
        path.append((x, y, z))
    b.tube(path, MUSTARD_R, "hotdog_mustard", MUSTARD_SIDES)


# ---------------------------------------------------------------------------
# 4. Rubber chicken: long plucked yellow body lying flat, neck stretched forward, legs back
# ---------------------------------------------------------------------------
CHICKEN_SLOTS = {"chicken_yellow": ("#FFD02B", "rubber"), "chicken_orange": ("#FF8A1C", "rubber"),
                 "chicken_comb": ("#E5432A", "rubber"), "chicken_eye": ("#141414", "eye")}
CHICKEN_SCALE = 0.85         # the whole chicken, so it is about 0.8 m long
CH_BODY = dict(c=(0.0, 0.05, 0.085), r=(0.105, 0.190, 0.085), seg=(20, 12))
CH_WING = dict(c=(0.098, 0.02, 0.105), r=(0.026, 0.090, 0.040), seg=(10, 6))
CH_WING_TILT_DEG = 12.0
CH_NECK = [(0.0, -0.10, 0.105), (0.0, -0.17, 0.100), (0.0, -0.23, 0.085), (0.0, -0.28, 0.068),
           (0.0, -0.33, 0.058)]
CH_NECK_R = (0.042, 0.036, 0.032, 0.030, 0.032)
CH_HEAD = dict(c=(0.0, -0.37, 0.058), r=(0.046, 0.062, 0.052), seg=(14, 8))
CH_BEAK = [(0.0, -0.418, 0.055), (0.0, -0.448, 0.050), (0.0, -0.475, 0.044)]
CH_BEAK_R = (0.020, 0.011, 0.002)
CH_COMB = [dict(c=(0.0, y, z), r=(0.012, 0.020, 0.022), seg=(8, 5))
           for y, z in ((-0.395, 0.102), (-0.367, 0.112), (-0.340, 0.104))]
CH_WATTLE = dict(c=(0.0, -0.412, 0.020), r=(0.012, 0.016, 0.020), seg=(8, 5))
CH_EYE = dict(c=(0.040, -0.388, 0.072), r=(0.010, 0.010, 0.010), seg=(6, 4))
CH_LEG = [(0.050, 0.200, 0.055), (0.060, 0.300, 0.040), (0.075, 0.420, 0.022)]
CH_LEG_R = (0.020, 0.016, 0.014)
CH_TOE_LEN = 0.060
CH_TOE_R = 0.009
CH_TOE_SPREAD_DEG = (-35.0, 0.0, 35.0)
CH_SIDES = 8


def _chicken(b):
    b.ellipsoid(CH_BODY, "chicken_yellow", deform=_flat_bottom)
    for side in (1, -1):
        b.ellipsoid(_mirror(CH_WING, side), "chicken_yellow", rot=_rot(CH_WING_TILT_DEG, 0.0))
    b.tube(CH_NECK, CH_NECK_R, "chicken_yellow", CH_SIDES)
    b.ellipsoid(CH_HEAD, "chicken_yellow")
    b.tube(CH_BEAK, CH_BEAK_R, "chicken_orange", CH_SIDES)
    for comb in CH_COMB:
        b.ellipsoid(comb, "chicken_comb")
    b.ellipsoid(CH_WATTLE, "chicken_comb")
    for side in (1, -1):
        b.ellipsoid(_mirror(CH_EYE, side), "chicken_eye")
        leg = [(side * x, y, z) for x, y, z in CH_LEG]
        b.tube(leg, CH_LEG_R, "chicken_orange", CH_SIDES)
        ax, ay, az = leg[-1]
        for a in CH_TOE_SPREAD_DEG:
            ang = math.radians(a * side)
            tip = (ax + CH_TOE_LEN * math.sin(ang), ay + CH_TOE_LEN * math.cos(ang), CH_TOE_R)
            b.tube([(ax, ay, az), tip], (CH_TOE_R, CH_TOE_R * 0.6), "chicken_orange", CH_SIDES // 2 + 2)


# ---------------------------------------------------------------------------
# 5. Knotted rope: two twisted strands (two colours), one chunky knot, frayed tassels
# ---------------------------------------------------------------------------
ROPE_SLOTS = {"rope_orange": ("#FF8A1C", "plush"), "rope_blue": ("#2C6BDB", "plush")}
ROPE_R = 0.045               # the rope's overall radius
STRAND_R = 0.028             # each of the two strands
STRAND_OFF = 0.019           # strand centre from the rope centre
TWIST_M = 0.09               # metres of rope per full twist
ROPE_LEG = (0.075, 0.275)    # each straight leg runs along Y between these distances from the knot
ROPE_STEP = 0.010            # metres between rings on the legs
KNOT_S = 0.034               # trefoil knot scale (its loops reach about 3x this from the centre)
KNOT_ZAMP = 1.0              # how far the knot's loops rise and fall, in units of KNOT_S
KNOT_PTS = 96
STRAND_SIDES = 6
TASSELS = 14                 # frayed strands at each end
TASSEL_R = 0.0085
TASSEL_LEN = (0.08, 0.11)
TASSEL_SPREAD = 0.055        # sideways spread of the tassel tips
TASSEL_PTS = 4
TASSEL_SIDES = 4
TASSEL_SEED = 332


def _twisted(b, path):
    """Two strands, one per colour, twisted round a centre path."""
    pts = [Vector(p) for p in path]
    lengths = [0.0]
    for a, c in zip(pts, pts[1:]):
        lengths.append(lengths[-1] + (c - a).length)
    tans = [(pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(len(pts))]
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    n1 = (ref - tans[0] * ref.dot(tans[0])).normalized()
    frames = []
    for i, t in enumerate(tans):
        if i:
            n1 = (n1 - t * n1.dot(t)).normalized()
        frames.append((n1.copy(), t.cross(n1)))
    for k, mat in enumerate(("rope_orange", "rope_blue")):
        strand = []
        for p, s, (f1, f2) in zip(pts, lengths, frames):
            a = 2 * math.pi * s / TWIST_M + k * math.pi
            strand.append(p + STRAND_OFF * (math.cos(a) * f1 + math.sin(a) * f2))
        b.tube(strand, STRAND_R, mat, STRAND_SIDES)


def _rope(b):
    import random
    rng = random.Random(TASSEL_SEED)
    kz = KNOT_S * KNOT_ZAMP + ROPE_R
    knot = []
    for i in range(KNOT_PTS + 1):
        t = 2 * math.pi * i / KNOT_PTS
        knot.append((KNOT_S * (math.sin(t) + 2 * math.sin(2 * t)), KNOT_S * (math.cos(t) - 2 * math.cos(2 * t)),
                     kz - KNOT_S * KNOT_ZAMP * math.sin(3 * t)))
    _twisted(b, knot)
    n = int(round((ROPE_LEG[1] - ROPE_LEG[0]) / ROPE_STEP))
    for side in (-1, 1):
        leg = [(0.0, side * (ROPE_LEG[0] + (ROPE_LEG[1] - ROPE_LEG[0]) * i / n), ROPE_R) for i in range(n + 1)]
        _twisted(b, leg)
        end_y = side * ROPE_LEG[1]
        for t in range(TASSELS):
            a = 2 * math.pi * t / TASSELS
            x0, z0 = 0.7 * ROPE_R * math.cos(a), ROPE_R + 0.7 * ROPE_R * math.sin(a)
            ln = rng.uniform(*TASSEL_LEN)
            dx = x0 * 0.6 + rng.uniform(-TASSEL_SPREAD, TASSEL_SPREAD)
            path = []
            for i in range(TASSEL_PTS + 1):
                f = i / TASSEL_PTS
                path.append((x0 + dx * f, end_y + side * ln * f,
                             max(TASSEL_R, z0 + (TASSEL_R - z0) * f * f)))
            b.tube(path, TASSEL_R, ("rope_orange", "rope_blue")[t % 2], TASSEL_SIDES)


# ---------------------------------------------------------------------------
DESIGNS = {             # in Derek's numbering (1-5); the order of the line-up
    "disc": {"label": "Flying disc", "slots": DISC_SLOTS, "build": _disc},
    "chew_cone": {"label": "Rubber chew cone", "slots": CONE_SLOTS, "build": _chew_cone},
    "hotdog": {"label": "Plush hot dog", "slots": HOTDOG_SLOTS, "build": _hotdog},
    "rubber_chicken": {"label": "Rubber chicken", "slots": CHICKEN_SLOTS, "build": _chicken,
                       "scale": CHICKEN_SCALE},
    "rope_knot": {"label": "Knotted rope", "slots": ROPE_SLOTS, "build": _rope},
}


def object_name(design):
    return "toy_" + design


def build(tex, design, collection=None):
    """Build one design; tex = {"color": path, "normal": path} from dh_toy_tex.write(). Returns the object."""
    d = DESIGNS[design]
    b = Builder(d["slots"])
    d["build"](b)
    return b.finish(object_name(design), tex, d.get("scale", 1.0), collection)


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)
