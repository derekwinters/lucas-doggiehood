"""The backyard dog pools (#741): two approved above-ground pools, sized by their water surface
(pool_core): a 5 m round pool ("circle") and a 6 x 3 m oval with rounded corners ("oval").

Conventions (docs/engineering/blender-pipeline.md): metres, Z up, pivot on the ground at the
centre of the water. One mesh per shape, flat colours, no textures.

Both shapes are built the same way. Every ring (liner, wall, rim) is the water outline offset
outward (pool_core.outline), so the wall and rim are the same thickness on both:
  - wall: one wall from the ground to the rim, with uprights standing proud of it
  - rim: a coping cap whose top is exactly PoolHeight, lipping over both faces of the wall
  - inside: a liner wall down to a floor, and the water at PoolWaterSurfaceHeight, with a few
    pale ripple arcs just above it so it reads as water from the 45 deg game camera
  - steps: pool_core.STEP_COUNT low treads against the outside wall, on the camera's side
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))   # the house kit
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import pool_core as PC  # noqa: E402

CORE = PC.core_consts()
HEIGHT = CORE["PoolHeight"]                  # rim top: one adult dog tall
WATER_Z = CORE["PoolWaterSurfaceHeight"]     # below the rim

RIM_H = 0.06                     # coping cap thickness
FLOOR_Z = 0.10                   # inside floor, above the ground
WALL_TOP = HEIGHT - RIM_H        # where the wall meets the cap
STEP_RISE = HEIGHT / (PC.STEP_COUNT + 1)     # even hops: ground, treads, rim
UPRIGHT_W = 0.07                 # post width (along the wall)
UPRIGHT_D = 0.02                 # how far a post stands proud of the wall (inside the rim's lip)
UPRIGHT_SPACING = 1.25           # metres of wall between uprights
UPRIGHT_CLEAR = 0.15             # an upright keeps this clear of the steps' sides
RIPPLE_Z = 0.004                 # ripple arcs float this far above the water
RIPPLE_W = 0.028                 # ripple arc width
RIPPLE_SEGMENTS = 6              # sides per ripple arc
# (centre x, centre y, radius) as fractions of the water's half extents (radius: of the half
# width), then start deg, sweep deg; the groups never cross and all stay inside the water
RIPPLES = ((0.35, 0.30, 0.18, 30.0, 120.0), (0.35, 0.30, 0.32, 200.0, 90.0),
           (-0.40, -0.25, 0.16, 160.0, 120.0), (-0.40, -0.25, 0.28, 320.0, 80.0),
           (0.40, -0.50, 0.12, 60.0, 130.0))

PALETTE = {                      # material slot -> colour, exported as DH_Item_pool_<slot>
    "shell": "#7E97A6",          # painted steel wall, slate blue-grey
    "coping": "#E6E1D5",         # off-white cap
    "upright": "#5F7685",
    "step": "#E2DCCD",           # off-white treads
    "riser": "#A99F8A",          # darker risers and sides, so the treads stand out
    "liner": "#86CFDC",          # pale aqua liner
    "floor": "#5DB5C9",
    "water": "#2C9CC4",
    "ripple": "#CDEFF5",
}
ROUGHNESS = {"water": 0.12, "ripple": 0.2, "liner": 0.55}
DEFAULT_ROUGHNESS = 0.8


def name_for(shape):
    return "pool_" + shape


class _Mesh:
    """Quads and n-gons with explicit winding (counter-clockwise seen from the face's front);
    every part keeps its own vertices so the flat-shaded edges stay crisp."""

    def __init__(self, shape):
        self.shape = shape
        self.bm = bmesh.new()
        self.slots = list(PALETTE)

    def face(self, pts, slot):
        f = self.bm.faces.new([self.bm.verts.new(Vector(p)) for p in pts])
        f.material_index = self.slots.index(slot)

    def wall(self, d, z0, z1, slot, inward=False):
        pts = PC.outline(self.shape, d)
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            q = [(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)]
            self.face(q[::-1] if inward else q, slot)

    def annulus(self, d0, d1, z, slot, up=True):
        p0, p1 = PC.outline(self.shape, d0), PC.outline(self.shape, d1)
        for i in range(len(p0)):
            j = (i + 1) % len(p0)
            q = [(p0[i][0], p0[i][1], z), (p1[i][0], p1[i][1], z), (p1[j][0], p1[j][1], z),
                 (p0[j][0], p0[j][1], z)]
            self.face(q if up else q[::-1], slot)

    def disc(self, d, z, slot):
        self.face([(x, y, z) for x, y in PC.outline(self.shape, d)], slot)

    def arc(self, cx, cy, r, start, sweep, z, slot):
        for k in range(RIPPLE_SEGMENTS):
            t0 = math.radians(start + sweep * k / RIPPLE_SEGMENTS)
            t1 = math.radians(start + sweep * (k + 1) / RIPPLE_SEGMENTS)
            p = [(cx + rr * math.cos(t), cy + rr * math.sin(t), z)
                 for rr, t in ((r - RIPPLE_W / 2, t0), (r + RIPPLE_W / 2, t0),
                               (r + RIPPLE_W / 2, t1), (r - RIPPLE_W / 2, t1))]
            self.face(p, slot)

    def post(self, p, n):
        """An upright on the wall's outer face at p, outward normal n, ground to rim cap."""
        t = (-n[1], n[0])
        hw = UPRIGHT_W / 2

        def v(k, w, z):
            return (p[0] + n[0] * k + t[0] * w, p[1] + n[1] * k + t[1] * w, z)

        o, i, z0, z1 = UPRIGHT_D, -UPRIGHT_D, 0.0, WALL_TOP
        self.face([v(o, -hw, z0), v(o, hw, z0), v(o, hw, z1), v(o, -hw, z1)], "upright")
        self.face([v(i, -hw, z0), v(o, -hw, z0), v(o, -hw, z1), v(i, -hw, z1)], "upright")
        self.face([v(o, hw, z0), v(i, hw, z0), v(i, hw, z1), v(o, hw, z1)], "upright")

    def uprights(self):
        """Evenly spaced by wall length, starting half a space from the steps, none beside them."""
        pts = PC.outline(self.shape, PC.D_WALL)
        segs = [(pts[i], pts[(i + 1) % len(pts)]) for i in range(len(pts))]
        lens = [math.dist(a, b) for a, b in segs]
        total = sum(lens)
        anchor, _n, _s = PC.steps_anchor(self.shape)
        best, s0, acc = None, 0.0, 0.0           # wall-length position of the steps
        for (a, b), ln in zip(segs, lens):
            t = ((anchor[0] - a[0]) * (b[0] - a[0]) + (anchor[1] - a[1]) * (b[1] - a[1])) / ln ** 2
            t = max(0.0, min(1.0, t))
            q = (a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]))
            if best is None or math.dist(q, anchor) < best:
                best, s0 = math.dist(q, anchor), acc + t * ln
            acc += ln
        count = max(1, round(total / UPRIGHT_SPACING))
        for k in range(count):
            s = (s0 + total * (k + 0.5) / count) % total
            for (a, b), ln in zip(segs, lens):
                if s <= ln:
                    t = s / ln
                    p = (a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]))
                    n = ((b[1] - a[1]) / ln, -(b[0] - a[0]) / ln)
                    break
                s -= ln
            if math.dist(p, anchor) < PC.STEP_WIDTH / 2 + UPRIGHT_CLEAR + UPRIGHT_W:
                continue
            self.post(p, n)

    def steps(self):
        """STEP_COUNT columns side by side, highest at the wall. Each has a tread on top, a riser
        facing out (from the next tread down) and two sides; the wall hides the end against it."""
        _p, _n, sag = PC.steps_anchor(self.shape)
        hw = PC.STEP_WIDTH / 2

        def S(along, across, z):
            x, y = PC.steps_point(self.shape, along, across)
            return (x, y, z)

        for j in range(PC.STEP_COUNT):
            a0 = -sag - PC.STEP_EMBED if j == 0 else j * PC.STEP_TREAD
            a1 = (j + 1) * PC.STEP_TREAD
            top = (PC.STEP_COUNT - j) * STEP_RISE
            below = top - STEP_RISE
            self.face([S(a0, -hw, top), S(a1, -hw, top), S(a1, hw, top), S(a0, hw, top)], "step")
            self.face([S(a1, -hw, below), S(a1, hw, below), S(a1, hw, top), S(a1, -hw, top)], "riser")
            self.face([S(a1, hw, 0.0), S(a0, hw, 0.0), S(a0, hw, top), S(a1, hw, top)], "riser")
            self.face([S(a0, -hw, 0.0), S(a1, -hw, 0.0), S(a1, -hw, top), S(a0, -hw, top)], "riser")

    def build(self, name):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for slot in self.slots:
            me.materials.append(K.material("DH_Item_pool_" + slot, PALETTE[slot],
                                           roughness=ROUGHNESS.get(slot, DEFAULT_ROUGHNESS)))
        for poly in me.polygons:
            poly.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def build(shape):
    if shape not in PC.SHAPES:
        raise ValueError("unknown pool shape %r; one of %s" % (shape, ", ".join(PC.SHAPES)))
    m = _Mesh(shape)
    m.wall(PC.D_WALL, 0.0, WALL_TOP, "shell")
    m.uprights()
    # rim cap: outer edge, top, inner edge, and the lip undersides
    m.wall(PC.D_RIM_OUT, WALL_TOP, HEIGHT, "coping")
    m.annulus(PC.D_RIM_IN, PC.D_RIM_OUT, HEIGHT, "coping")
    m.wall(PC.D_RIM_IN, WALL_TOP, HEIGHT, "coping", inward=True)
    m.annulus(PC.D_WALL, PC.D_RIM_OUT, WALL_TOP, "coping", up=False)
    m.annulus(PC.D_RIM_IN, PC.D_LINER, WALL_TOP, "coping", up=False)
    # inside: liner, floor, water, ripples
    m.wall(PC.D_LINER, FLOOR_Z, WALL_TOP, "liner", inward=True)
    m.disc(PC.D_LINER, FLOOR_Z, "floor")
    m.disc(PC.D_LINER, WATER_Z, "water")
    hx, hy = PC.water_half_extents(shape)
    for cx, cy, r, start, sweep in RIPPLES:
        m.arc(cx * hx, cy * hy, r * hy, start, sweep, WATER_Z + RIPPLE_Z, "ripple")
    m.steps()
    return m.build(name_for(shape))


def measures(shape, ob):
    """Size figures from the built mesh itself (the footprint is what the model covers)."""
    vs = [v.co for v in ob.data.vertices]
    xs, ys = [v.x for v in vs], [v.y for v in vs]
    hx, hy = PC.water_half_extents(shape)
    return {
        "shape": shape,
        "water_m": [round(2 * hx, 4), round(2 * hy, 4)],
        "oval_corner_radius_m": PC.OVAL_CORNER_R if shape == "oval" else None,
        "rim_outer_m": [round(2 * (hx + PC.D_RIM_OUT), 4), round(2 * (hy + PC.D_RIM_OUT), 4)],
        "footprint_x_m": [round(min(xs), 4), round(max(xs), 4)],
        "footprint_y_m": [round(min(ys), 4), round(max(ys), 4)],
        "footprint_size_m": [round(max(xs) - min(xs), 4), round(max(ys) - min(ys), 4)],
        "bounding_radius_m": round(max(math.hypot(v.x, v.y) for v in vs), 4),
        "height_m": round(max(v.z for v in vs), 4),
        "water_z_m": round(WATER_Z, 4),
        "water_below_rim_m": round(HEIGHT - WATER_Z, 4),
        "water_depth_m": round(WATER_Z - FLOOR_Z, 4),
        "steps": PC.STEP_COUNT,
        "step_rise_m": round(STEP_RISE, 4),
        "step_tread_m": PC.STEP_TREAD,
        "step_width_m": PC.STEP_WIDTH,
    }
