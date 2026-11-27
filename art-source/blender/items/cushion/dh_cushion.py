"""Doggiehood cushion (#412): a puffy, tufted floor cushion for a dog to lie on.

Everything is built from the named numbers below; there is no randomness, so the same script gives
the same model every run. Conventions follow docs/engineering/blender-pipeline.md: 1 unit = 1 m
(in-game size), front -Y, up +Z, pivot at the centre of the footprint on the ground. The long side
runs along X, like today's 1.4 x 1.0 m DecorationView slab.

The shape is a pillow, not a bed and not a blanket: two soft fabric panels sewn together at a
seam a little above the ground, with no raised rim. The top panel domes up to a broad, near-level
plateau a dog lies on; it is pulled down into shallow dimples by six tufting buttons. The bottom
panel curls under to a flat patch on the ground. Contrast-colour piping runs round the seam, and
the corners pinch out into little "ears", as on a stuffed cushion.

Objects (one glTF node):
  item_cushion   fabric (top + bottom panels), piping round the seam, six buttons (one mesh,
                 three materials)
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))   # the house kit (read-only)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402

NAME = "item_cushion"

# --- Palette (sRGB hex, roughness). Coral, the colour today's graybox cushion slab already uses
# (#FF6F61 in DecorationView), toned down a little; cream piping and buttons for contrast. ---
COLOURS = {
    "fabric": ("#E9765F", 0.92),
    "piping": ("#F3E4CB", 0.8),
    "button": ("#F3E4CB", 0.55),
}

# --- Outline (metres). The seam's outline: half-extents at the corners, and how far the middle of
# each edge bows in (a stuffed cushion pulls its edges in between the corners). ---
SEAM_HALF_X = 0.668           # with the piping, the cushion is 1.376 m long (slab: 1.4)
SEAM_HALF_Y = 0.468           # with the piping, 0.976 m deep (slab: 1.0)
EDGE_BOW = 0.035              # fraction of the half-extent the edge middles pull in

# --- Panels. Height at a point = seam height +/- rise * g(s) * g(t), where s, t run -1..1 across
# the outline and g(a) = (1 - |a|^P)^Q: 0 at the seam, a broad plateau in the middle. ---
SEAM_Z = 0.10                 # the seam (and the piping) sits this high off the ground
TOP_RISE = 0.195              # the top's peak above the seam: the plateau is about 0.30 m high
TOP_P, TOP_Q = 4.0, 0.30      # plateau sharpness and how steeply the top rises off the seam
BOTTOM_DROP = 0.14            # the bottom panel bulges this far below the seam ...
BOTTOM_P, BOTTOM_Q = 3.0, 0.35
GROUND_Z = 0.0                # ... and is squashed flat on the ground where it would go under it

# --- Grid. Columns and rows across each panel; spacing tightens toward the seam, where the panel
# curves most. The bottom panel is mostly hidden, so it uses every second column and row. ---
TOP_COLS, TOP_ROWS = 40, 28
BOTTOM_STEP = 2

# --- Tufting: buttons on a 3 x 2 grid, each pulling a soft dimple into the top. ---
BUTTON_XS = (-0.34, 0.0, 0.34)
BUTTON_YS = (-0.16, 0.16)
DIMPLE_DEPTH = 0.03
DIMPLE_RADIUS = 0.07         # Gaussian width of each dimple
BUTTON_R = 0.026              # button radius ...
BUTTON_H = 0.011              # ... and height above the dimple floor
BUTTON_SEGMENTS, BUTTON_RINGS = 10, 3

# --- Piping: a round cord on the seam. ---
PIPING_R = 0.02
PIPING_SIDES = 6


# ---------------------------------------------------------------------------
# Shape functions
# ---------------------------------------------------------------------------
def profile(a, p, q):
    """Panel height profile across the cushion: 1 in the middle, 0 at the seam."""
    return max(0.0, 1.0 - abs(a) ** p) ** q


def spacing(i, n):
    """Grid coordinate -1..1 for column i of n, denser toward the seam than in the middle."""
    return math.sin(math.pi / 2 * (2.0 * i / n - 1.0))


def outline(s, t):
    """Seam-plane position of grid coordinate (s, t): a rectangle whose edges bow in mid-span."""
    return (s * SEAM_HALF_X * (1 - EDGE_BOW * (1 - t * t)),
            t * SEAM_HALF_Y * (1 - EDGE_BOW * (1 - s * s)))


def dimple(x, y):
    """How far the tufting pulls the top down at (x, y)."""
    d = 0.0
    for bx in BUTTON_XS:
        for by in BUTTON_YS:
            r2 = (x - bx) ** 2 + (y - by) ** 2
            d += DIMPLE_DEPTH * math.exp(-r2 / (DIMPLE_RADIUS ** 2))
    return d


def top_z(s, t):
    x, y = outline(s, t)
    g = profile(s, TOP_P, TOP_Q) * profile(t, TOP_P, TOP_Q)
    return SEAM_Z + TOP_RISE * g - dimple(x, y) * g


def bottom_z(s, t):
    g = profile(s, BOTTOM_P, BOTTOM_Q) * profile(t, BOTTOM_P, BOTTOM_Q)
    return max(GROUND_Z, SEAM_Z - BOTTOM_DROP * g)


# ---------------------------------------------------------------------------
# Mesh pieces: plain vertex / face lists, faces carry a material key
# ---------------------------------------------------------------------------
class Mesh:
    def __init__(self):
        self.v, self.f, self.m = [], [], []

    def add(self, verts, faces, mat):
        o = len(self.v)
        self.v += [tuple(p) for p in verts]
        self.f += [tuple(i + o for i in fc) for fc in faces]
        self.m += [mat] * len(faces)


def panel(m, cols, rows, zfn, up):
    """One fabric panel on a (cols x rows) grid; faces point up (top) or down (bottom)."""
    verts = []
    for j in range(rows + 1):
        t = spacing(j, rows)
        for i in range(cols + 1):
            s = spacing(i, cols)
            x, y = outline(s, t)
            verts.append((x, y, zfn(s, t)))
    faces = []
    w = cols + 1
    for j in range(rows):
        for i in range(cols):
            a, b, c, d = j * w + i, j * w + i + 1, (j + 1) * w + i + 1, (j + 1) * w + i
            faces.append((a, b, c, d) if up else (a, d, c, b))
    m.add(verts, faces, "fabric")


def seam_ring():
    """The seam outline, counter-clockwise seen from above, on the top panel's grid."""
    ring = []
    for i in range(TOP_COLS):
        ring.append(outline(spacing(i, TOP_COLS), -1.0))
    for j in range(TOP_ROWS):
        ring.append(outline(1.0, spacing(j, TOP_ROWS)))
    for i in range(TOP_COLS, 0, -1):
        ring.append(outline(spacing(i, TOP_COLS), 1.0))
    for j in range(TOP_ROWS, 0, -1):
        ring.append(outline(-1.0, spacing(j, TOP_ROWS)))
    return ring


def piping(m):
    """A closed round cord along the seam; mitred at the corners so it keeps its thickness."""
    ring = seam_ring()
    n = len(ring)
    verts = []
    for k in range(n):
        px, py = ring[k]
        ax, ay = ring[k - 1]
        bx, by = ring[(k + 1) % n]
        t0 = (px - ax, py - ay)
        t1 = (bx - px, by - py)
        l0, l1 = math.hypot(*t0), math.hypot(*t1)
        tx, ty = t0[0] / l0 + t1[0] / l1, t0[1] / l0 + t1[1] / l1
        lt = math.hypot(tx, ty)
        tx, ty = tx / lt, ty / lt
        ox, oy = ty, -tx                                  # outward (the ring runs counter-clockwise)
        miter = 1.0 / max(0.5, (t0[0] / l0) * tx + (t0[1] / l0) * ty)
        for sd in range(PIPING_SIDES):
            a = 2 * math.pi * sd / PIPING_SIDES
            r_out = PIPING_R * math.cos(a) * miter
            verts.append((px + ox * r_out, py + oy * r_out, SEAM_Z + PIPING_R * math.sin(a)))
    faces = []
    for k in range(n):
        k1 = (k + 1) % n
        for sd in range(PIPING_SIDES):
            s1 = (sd + 1) % PIPING_SIDES
            faces.append((k * PIPING_SIDES + sd, k * PIPING_SIDES + s1, k1 * PIPING_SIDES + s1,
                          k1 * PIPING_SIDES + sd))
    m.add(verts, faces, "piping")


def button(m, bx, by):
    """A flattened dome sitting in its dimple."""
    z0 = top_z(*_grid_of(bx, by)) - BUTTON_H * 0.3
    verts = [(bx, by, z0 + BUTTON_H)]
    for r in range(1, BUTTON_RINGS + 1):
        a = (math.pi / 2) * r / BUTTON_RINGS
        rad, h = BUTTON_R * math.sin(a), BUTTON_H * math.cos(a)
        for sg in range(BUTTON_SEGMENTS):
            b = 2 * math.pi * sg / BUTTON_SEGMENTS
            verts.append((bx + rad * math.cos(b), by + rad * math.sin(b), z0 + h))
    faces = []
    for sg in range(BUTTON_SEGMENTS):
        faces.append((0, 1 + sg, 1 + (sg + 1) % BUTTON_SEGMENTS))
    for r in range(BUTTON_RINGS - 1):
        o0, o1 = 1 + r * BUTTON_SEGMENTS, 1 + (r + 1) * BUTTON_SEGMENTS
        for sg in range(BUTTON_SEGMENTS):
            s1 = (sg + 1) % BUTTON_SEGMENTS
            faces.append((o0 + sg, o1 + sg, o1 + s1, o0 + s1))
    m.add(verts, faces, "button")


def _grid_of(x, y):
    """Grid coordinate (s, t) of a seam-plane point well inside the outline (fixed-point solve)."""
    s, t = x / SEAM_HALF_X, y / SEAM_HALF_Y
    for _ in range(20):
        ox, oy = outline(s, t)
        s, t = s * x / ox if ox else s, t * y / oy if oy else t
    return s, t


def rest_top():
    """Height of the top at the centre of the cushion, where a lying dog's body goes."""
    return top_z(0.0, 0.0)


# ---------------------------------------------------------------------------
SPECULAR_DEFAULT = 0.5        # Blender's default specular level: exports with no glTF extension


def material(slot):
    """The kit's flat-colour material, single-sided and with no specular extension."""
    hexc, rough = COLOURS[slot]
    mat = K.material("DH_Item_cushion_" + slot, hexc, roughness=rough)
    mat.node_tree.nodes["Principled BSDF"].inputs["Specular IOR Level"].default_value = SPECULAR_DEFAULT
    mat.use_backface_culling = True
    return mat


def to_object(name, mesh):
    me = bpy.data.meshes.new(name)
    me.from_pydata(mesh.v, [], mesh.f)
    keys = list(COLOURS)
    for k in keys:
        me.materials.append(material(k))
    for p, k in zip(me.polygons, mesh.m):
        p.material_index = keys.index(k)
        p.use_smooth = True
    me.validate()
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build(name=NAME):
    m = Mesh()
    panel(m, TOP_COLS, TOP_ROWS, top_z, up=True)
    panel(m, TOP_COLS // BOTTOM_STEP, TOP_ROWS // BOTTOM_STEP, bottom_z, up=False)
    piping(m)
    for bx in BUTTON_XS:
        for by in BUTTON_YS:
            button(m, bx, by)
    return to_object(name, m)
