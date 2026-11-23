"""Backyard fence pieces (#806, CONCEPT round): three style options for
Derek to choose from, each as the same set of modular pieces.

Styles (STYLES):
  privacy    A. cedar privacy fence: vertical dog-eared boards, 1.83 m (6 ft)
  picket     B. white picket fence: pointed pickets, 1.07 m (3.5 ft)
  splitrail  C. three-rail split-cedar fence with black welded-wire mesh on the
             yard side, 1.35 m posts (today's fence height)

Pieces per style (PIECE_KINDS):
  panel        one bay between two post centres, NOMINAL_LEN long. It is the
               piece FenceTiling tiles and compresses (its local X is scaled by
               length / NOMINAL_LEN). It has NO posts in it, so compression only
               narrows boards and gaps; posts never get squashed.
  line_post    the post at every joint inside a run.
  corner_post  a heavier post where two runs meet (side/rear corners, and the
               side-run front end where a connector turns in).
  end_post     the post where a connector meets the house; its pivot is on the
               face that touches the wall and its body runs along local +X
               (down the connector, away from the wall). Black wall brackets.
  gate         a walk gate, GATE_LEN wide (half a bay), hinges at -X, latch at
               +X. Where a gate would go is not specified anywhere (Open).

Conventions (docs/engineering/blender-pipeline.md): metres, Z up, the run
runs along local +X, the finished ("good neighbour") face is -Y and faces out
of the yard, pivot on the ground at the panel's centre / the post base.
Materials are DH_Fence_<slot>; every surface carries a generated colour +
normal map from dh_fence_tex (the wire mesh is an alpha-masked, double-sided
colour + normal map). Board colour variation comes from UVs picking one of
the eight boards in the cedar texture, seeded per piece name, so every run
gives the same model.
"""
import math
import os
import random
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# The committed street-prop builder/materials (../street-props), tree modules
# (../trees) and house kit (art-source/blender), by relative path.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "street-props")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_trees_tex as TT  # noqa: E402
import dh_props as P  # noqa: E402
import dh_fence_tex as FT  # noqa: E402

# --- shared layout ---------------------------------------------------------
# One bay, post centre to post centre. FenceTiling's nominal piece today is
# the Kenney piece x5 = 0.475 * 5 = 2.375 m; keeping exactly that keeps
# FenceTiling's whole-piece count per run unchanged (an 8 ft bay is 2.44 m).
NOMINAL_LEN = 0.475 * 5
GATE_LEN = NOMINAL_LEN / 2          # 1.19 m: a standard ~4 ft walk gate
WOOD_V_TILE = 1.0                   # metres of board length per cedar-texture repeat
CEDAR_COL_MARGIN = 0.004            # keep a board's UVs inside its texture column
PAINT_TILE = 0.6
RAIL_TILE = (0.5, 1.0)              # (around, along) metres per rail-texture repeat
METAL_TILE = 0.3
WIRE_TILE = FT.WIRE_TILE

STYLES = {
    "privacy": {
        "label": "A. Cedar privacy",
        "height": 1.83,           # top of the boards (6 ft)
        "board_w": 0.14, "board_t": 0.016, "board_gap": 0.004, "dog_ear": 0.025,
        "board_bottom": 0.05,
        "rail_w": 0.038, "rail_h": 0.089, "rails_z": (0.30, 0.93, 1.56),
        "post": 0.089, "post_top": 1.80, "corner_post": 0.14, "corner_top": 1.86,
        "cap_over": 0.012, "cap_t": 0.025,
    },
    "picket": {
        "label": "B. White picket",
        "height": 1.07,           # picket points (3.5 ft)
        "board_w": 0.07, "board_t": 0.019, "board_gap": 0.062, "point": 0.06,
        "board_bottom": 0.06,
        "rail_w": 0.038, "rail_h": 0.089, "rails_z": (0.24, 0.82),
        "post": 0.10, "post_top": 1.12, "corner_post": 0.125, "corner_top": 1.18,
        "cap_over": 0.02, "cap_t": 0.03, "cap_rise": 0.06,
        "paint": "#F3F1EA",
    },
    "splitrail": {
        "label": "C. Split rail + wire",
        "height": 1.35,           # post tops (= today's 1.35 m fence)
        "post": (0.15, 0.12), "post_top": 1.35, "corner_post": (0.18, 0.15), "corner_top": 1.40,
        "rails_z": (0.36, 0.78, 1.20), "rail_r": (0.065, 0.045),
        "wire_z": (0.03, 1.12), "wire_off": 0.006,
    },
}
PIECE_KINDS = ("panel", "line_post", "corner_post", "end_post", "gate")
HARDWARE_HEX = "#24262A"

# material key -> (texture, tint or None, roughness, metallic); names DH_Fence_<key>
MATERIALS = {
    "cedar": ("cedar", None, 0.82, 0.0),
    "white_paint": ("paintwood", STYLES["picket"]["paint"], 0.55, 0.0),
    "split_rail": ("rail", None, 0.92, 0.0),
    "hardware": ("metal", HARDWARE_HEX, 0.45, 0.3),
}


def materials():
    tx = FT.paths()
    m = {k: P._tiled("DH_Fence_" + k, tx[t], tint, r, mt) for k, (t, tint, r, mt) in MATERIALS.items()}
    w = TT.leaf_material("DH_Fence_wire", tx["wire"][0])
    nt = w.node_tree
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(tx["wire"][1], check_existing=True)
    tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], nt.nodes["Principled BSDF"].inputs["Normal"])
    nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.5
    m["wire"] = w
    return m


class FenceBuilder(P.PropBuilder):
    """PropBuilder plus fence primitives with explicit UVs."""

    def __init__(self, name):
        super().__init__(name)
        self.rng = random.Random(zlib.crc32(name.encode()) & 0xFFFFFF)

    def prism_xz(self, outline, y0, y1, mat, uvfn):
        """Board: polygon `outline` [(x, z)...] (CCW seen from -Y) extruded
        from y0 (front) to y1 (back). Every face takes uvfn(x, z), so the
        grain runs up the board on the faces and edges alike."""
        F = [(x, y0, z) for x, z in outline]
        B = [(x, y1, z) for x, z in outline]
        self.oface(F, [uvfn(x, z) for x, z in outline], mat, (0, -1, 0))
        self.oface(B, [uvfn(x, z) for x, z in outline], mat, (0, 1, 0))
        cx = sum(x for x, _ in outline) / len(outline)
        cz = sum(z for _, z in outline) / len(outline)
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            (xa, za), (xb, zb) = outline[i], outline[j]
            out = ((xa + xb) / 2 - cx, 0, (za + zb) / 2 - cz)
            # edge faces: spread U a little across the thickness so they aren't one texel
            du = 0.01
            uva, uvb = uvfn(xa, za), uvfn(xb, zb)
            self.oface([F[i], F[j], B[j], B[i]],
                       [uva, uvb, (uvb[0] + du, uvb[1]), (uva[0] + du, uva[1])], mat, out)

    def pyramid(self, x0, y0, x1, y1, z0, z1, mat, tile=PAINT_TILE):
        apex = Vector(((x0 + x1) / 2, (y0 + y1) / 2, z1))
        base = [Vector((x0, y0, z0)), Vector((x1, y0, z0)), Vector((x1, y1, z0)), Vector((x0, y1, z0))]
        c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
        for i in range(4):
            vs = [base[i], base[(i + 1) % 4], apex]
            self.pface(vs, mat, sum(vs, Vector()) / 3 - c, tile)

    def sweep(self, p0, p1, section, mat, tile=RAIL_TILE, caps=True):
        """Prism along p0 -> p1 (horizontal) with a polygon cross-section
        [(side, up)...]; U around the perimeter, V along the length (grain)."""
        p0, p1 = Vector(p0), Vector(p1)
        t = (p1 - p0).normalized()
        up = Vector((0, 0, 1))
        sd = t.cross(up).normalized()
        L = (p1 - p0).length
        ring0 = [p0 + sd * s + up * u for s, u in section]
        ring1 = [p1 + sd * s + up * u for s, u in section]
        n = len(section)
        acc = [0.0]
        for i in range(n):
            a, b = Vector(section[i]), Vector(section[(i + 1) % n])
            acc.append(acc[-1] + (b - a).length)
        mid = (p0 + p1) / 2
        for i in range(n):
            j = (i + 1) % n
            vs = [ring0[i], ring0[j], ring1[j], ring1[i]]
            u0, u1 = acc[i] / tile[0], acc[i + 1] / tile[0]
            uv = [(u0, 0), (u1, 0), (u1, L / tile[1]), (u0, L / tile[1])]
            fc = sum(vs, Vector()) / 4
            self.oface(vs, uv, mat, fc - (mid + (fc - mid).dot(t) * t))
        if caps:
            self.pface(ring0, mat, -t, tile[0])
            self.pface(ring1, mat, t, tile[0])

    def build(self, mats=None):
        return super().build(mats or materials())


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------
def _cedar_uv(rng, x0, x1):
    """UV function for one cedar board spanning x0..x1: one random texture
    column across the board, random V offset."""
    c = rng.randrange(FT.CEDAR_COLS)
    u0 = c / FT.CEDAR_COLS + CEDAR_COL_MARGIN
    u1 = (c + 1) / FT.CEDAR_COLS - CEDAR_COL_MARGIN
    voff = rng.random()
    return lambda x, z: (u0 + (u1 - u0) * (x - x0) / (x1 - x0), z / WOOD_V_TILE + voff)


def _paint_uv(x0, x1):
    return lambda x, z: ((x - x0) / PAINT_TILE, z / PAINT_TILE)


def _boards(b, st, x_lo, x_hi, y_front, mat, uv_kind):
    """Fill x_lo..x_hi with boards/pickets on the face at y_front (their back
    at y_front + board_t). Board count is the whole number that fits at the
    style's board width + gap; the leftover is spread over the gaps."""
    bw, gap, bt = st["board_w"], st["board_gap"], st["board_t"]
    span = x_hi - x_lo
    n = max(1, int((span + gap) / (bw + gap)))
    g = (span - n * bw) / max(1, n - 1) if n > 1 else 0.0
    if st.get("dog_ear"):
        g, n = gap, max(1, int(round((span + gap) / (bw + gap))))
        bw = (span - (n - 1) * g) / n                  # privacy: boards close the bay
    for i in range(n):
        x0 = x_lo + i * (bw + g)
        x1 = x0 + bw
        zb, zt = st["board_bottom"], st["height"]
        if "dog_ear" in st:
            e = st["dog_ear"]
            ol = [(x0, zb), (x1, zb), (x1, zt - e), (x1 - e, zt), (x0 + e, zt), (x0, zt - e)]
        else:
            ol = [(x0, zb), (x1, zb), (x1, zt - st["point"]), ((x0 + x1) / 2, zt), (x0, zt - st["point"])]
        uvf = _cedar_uv(b.rng, x0, x1) if uv_kind == "cedar" else _paint_uv(x0, x1)
        b.prism_xz(ol, y_front, y_front + bt, mat, uvf)


def _bracket(b, x_face, z, half_y):
    """Black L wall bracket: a plate on the wall face (x = x_face, in YZ) and
    a strap along the post."""
    th = 0.004
    b.box((x_face, -half_y - 0.03, z - 0.05), (x_face + th, half_y + 0.03, z + 0.05), "hardware", tile=METAL_TILE)
    b.box((x_face, half_y, z - 0.03), (x_face + 0.12, half_y + th, z + 0.03), "hardware", tile=METAL_TILE)


# ---------------------------------------------------------------------------
# style A + B: board fences (posts behind rails behind boards)
# ---------------------------------------------------------------------------
def _board_style_mat(style):
    return ("cedar", "cedar") if style == "privacy" else ("white_paint", "paint")


def _board_panel(style, b, x_lo, x_hi):
    st = STYLES[style]
    mat, uvk = _board_style_mat(style)
    hp = st["post"] / 2
    y_rail0 = -hp - st["rail_w"]
    for z in st["rails_z"]:
        b.box((x_lo, y_rail0, z - st["rail_h"] / 2), (x_hi, -hp, z + st["rail_h"] / 2), mat,
              tile=PAINT_TILE if mat != "cedar" else WOOD_V_TILE, grain=(1, 0, 0))
    _boards(b, st, x_lo, x_hi, y_rail0 - st["board_t"], mat, uvk)


def _board_post(style, b, kind):
    st = STYLES[style]
    mat, _ = _board_style_mat(style)
    corner = kind == "corner_post"
    s = st["corner_post"] if corner else st["post"]
    top = st["corner_top"] if corner else st["post_top"]
    x0 = 0.0 if kind == "end_post" else -s / 2
    hp = st["post"] / 2                 # posts share the line posts' back face line
    yc = 0.0 if not corner else -(s / 2 - hp)  # corner post keeps its front flush with the line posts
    if mat == "cedar":
        uvf = _cedar_uv(b.rng, x0, x0 + s)
        b.prism_xz([(x0, 0), (x0 + s, 0), (x0 + s, top), (x0, top)], yc - s / 2, yc + s / 2, mat, uvf)
    else:
        b.box((x0, yc - s / 2, 0), (x0 + s, yc + s / 2, top), mat, tile=PAINT_TILE)
    # cap centred on the post
    o, t = st["cap_over"], st["cap_t"]
    cx = x0 + s / 2
    b.box((cx - s / 2 - o, yc - s / 2 - o, top), (cx + s / 2 + o, yc + s / 2 + o, top + t), mat,
          tile=PAINT_TILE)
    if st.get("cap_rise"):
        b.pyramid(cx - s / 2 - o, yc - s / 2 - o, cx + s / 2 + o, yc + s / 2 + o, top + t,
                  top + t + st["cap_rise"] * (1.3 if corner else 1.0), mat)
    if kind == "end_post":
        for z in (0.35, top - 0.35):
            _bracket(b, 0.0, z, s / 2)


def _board_gate(style, b):
    st = STYLES[style]
    mat, uvk = _board_style_mat(style)
    hp = st["post"] / 2
    L = GATE_LEN
    x_lo, x_hi = -L / 2 + 0.012, L / 2 - 0.012           # swing clearance at each side
    y_rail0 = -hp - st["rail_w"]
    z_lo, z_hi = st["rails_z"][0], st["rails_z"][-1]
    tile = PAINT_TILE if mat != "cedar" else WOOD_V_TILE
    for z in (z_lo, z_hi):
        b.box((x_lo, y_rail0, z - st["rail_h"] / 2), (x_hi, -hp, z + st["rail_h"] / 2), mat, tile=tile,
              grain=(1, 0, 0))
    # Z brace: bottom at the hinge side, rising to the latch side
    b.beam((x_lo + 0.05, (y_rail0 - hp) / 2, z_lo + st["rail_h"] / 2),
           (x_hi - 0.05, (y_rail0 - hp) / 2, z_hi - st["rail_h"] / 2), st["rail_h"], st["rail_w"], mat,
           up=(0, 1, 0), tile=tile)
    _boards(b, st, x_lo, x_hi, y_rail0 - st["board_t"], mat, uvk)
    for z in (z_lo, z_hi):                                # strap hinges on the yard side
        b.box((x_lo - 0.02, -hp, z - 0.02), (x_lo + 0.32, -hp + 0.005, z + 0.02), "hardware", tile=METAL_TILE)
    zl = (z_lo + z_hi) / 2                                # thumb latch
    b.box((x_hi - 0.16, -hp, zl - 0.025), (x_hi - 0.02, -hp + 0.02, zl + 0.025), "hardware", tile=METAL_TILE)
    b.box((x_hi - 0.08, y_rail0 - st["board_t"] - 0.03, zl - 0.06),
          (x_hi - 0.06, y_rail0 - st["board_t"], zl + 0.06), "hardware", tile=METAL_TILE)


# ---------------------------------------------------------------------------
# style C: split rail + welded wire
# ---------------------------------------------------------------------------
def _rail_section(rng, r):
    """Irregular split-rail cross-section (a quartered log: roughly a
    rounded wedge), 6 points, seeded."""
    a, b = r
    pts = []
    for k in range(6):
        ang = 2 * math.pi * k / 6 + rng.uniform(-0.15, 0.15)
        f = rng.uniform(0.85, 1.1)
        pts.append((math.cos(ang) * b * f, math.sin(ang) * a * f))
    return pts


def _split_panel(b, x_lo, x_hi, with_wire=True):
    st = STYLES["splitrail"]
    for z in st["rails_z"]:
        sec = _rail_section(b.rng, st["rail_r"])
        b.sweep((x_lo, 0, z), (x_hi, 0, z), sec, "split_rail")
    if with_wire:
        yw = st["post"][1] / 2 + st["wire_off"]
        z0, z1 = st["wire_z"]
        vs = [(x_lo, yw, z0), (x_hi, yw, z0), (x_hi, yw, z1), (x_lo, yw, z1)]
        uv = [(x_lo / WIRE_TILE, z0 / WIRE_TILE), (x_hi / WIRE_TILE, z0 / WIRE_TILE),
              (x_hi / WIRE_TILE, z1 / WIRE_TILE), (x_lo / WIRE_TILE, z1 / WIRE_TILE)]
        b.oface(vs, uv, "wire", (0, 1, 0))


def _split_post(b, kind):
    st = STYLES["splitrail"]
    corner = kind == "corner_post"
    sx, sy = st["corner_post"] if corner else st["post"]
    top = st["corner_top"] if corner else st["post_top"]
    x0 = 0.0 if kind == "end_post" else -sx / 2
    # rough split post: slightly irregular rectangle, top cut on a slant to shed water
    j = [b.rng.uniform(-0.012, 0.012) for _ in range(4)]
    sec = [(x0 + j[0], -sy / 2), (x0 + sx + j[1], -sy / 2), (x0 + sx + j[2], sy / 2), (x0 + j[3], sy / 2)]
    if kind == "end_post":
        sec = [(x0, -sy / 2), (x0 + sx + j[1], -sy / 2), (x0 + sx + j[2], sy / 2), (x0, sy / 2)]
    lo = [Vector((x, y, 0)) for x, y in sec]
    slant = 0.05
    hi = [Vector((x, y, top - slant * (y + sy / 2) / sy)) for x, y in sec]
    c = Vector((x0 + sx / 2, 0, top / 2))
    acc = 0.0
    for i in range(4):
        k = (i + 1) % 4
        vs = [lo[i], lo[k], hi[k], hi[i]]
        el = (lo[k] - lo[i]).length
        uv = [(acc / RAIL_TILE[0], 0), ((acc + el) / RAIL_TILE[0], 0), ((acc + el) / RAIL_TILE[0], hi[k].z / RAIL_TILE[1]),
              (acc / RAIL_TILE[0], hi[i].z / RAIL_TILE[1])]
        acc += el
        fc = sum(vs, Vector()) / 4
        b.oface(vs, uv, "split_rail", Vector((fc.x - c.x, fc.y - c.y, 0)))
    b.pface(hi, "split_rail", (0, 0, 1), RAIL_TILE[0])
    if kind == "end_post":
        for z in (0.35, top - 0.3):
            _bracket(b, 0.0, z, sy / 2)


def _split_gate(b):
    st = STYLES["splitrail"]
    L = GATE_LEN
    x_lo, x_hi = -L / 2 + 0.012, L / 2 - 0.012
    fr = (0.05, 0.05)                         # gate frame: sawn cedar, wire infill
    z0, z1 = st["wire_z"][0] + 0.05, st["rails_z"][-1] + 0.02
    sq = [(-fr[0] / 2, -fr[1] / 2), (fr[0] / 2, -fr[1] / 2), (fr[0] / 2, fr[1] / 2), (-fr[0] / 2, fr[1] / 2)]
    for z in (z0, z1):
        b.sweep((x_lo, 0, z), (x_hi, 0, z), sq, "split_rail")
    for x in (x_lo + fr[0] / 2, x_hi - fr[0] / 2):
        b.box((x - fr[0] / 2, -fr[1] / 2, z0 - fr[1] / 2), (x + fr[0] / 2, fr[1] / 2, z1 + fr[1] / 2), "split_rail",
              tile=RAIL_TILE[0])
    b.beam((x_lo + fr[0], 0, z0 + fr[1] / 2), (x_hi - fr[0], 0, z1 - fr[1] / 2), fr[0] * 0.8, fr[1] * 0.8,
           "split_rail", up=(0, 1, 0), tile=RAIL_TILE[0])
    yw = fr[1] / 2 + 0.003
    vs = [(x_lo, yw, z0), (x_hi, yw, z0), (x_hi, yw, z1), (x_lo, yw, z1)]
    b.oface(vs, [(v[0] / WIRE_TILE, v[2] / WIRE_TILE) for v in vs], "wire", (0, 1, 0))
    for z in (z0 + 0.1, z1 - 0.1):
        b.box((x_lo - 0.02, -fr[1] / 2 - 0.005, z - 0.02), (x_lo + 0.25, -fr[1] / 2, z + 0.02), "hardware",
              tile=METAL_TILE)
    zl = (z0 + z1) / 2
    b.box((x_hi - 0.03, -fr[1] / 2 - 0.02, zl - 0.03), (x_hi + 0.02, fr[1] / 2 + 0.02, zl + 0.03), "hardware",
          tile=METAL_TILE)


# ---------------------------------------------------------------------------
# public builders
# ---------------------------------------------------------------------------
def piece_name(style, kind):
    return "fence_%s_%s" % (style, kind)


def build_piece(style, kind, name=None):
    """One piece as a single mesh object at the origin."""
    name = name or piece_name(style, kind)
    b = FenceBuilder(name)
    L = NOMINAL_LEN
    if style in ("privacy", "picket"):
        if kind == "panel":
            _board_panel(style, b, -L / 2, L / 2)
        elif kind == "gate":
            _board_gate(style, b)
        else:
            _board_post(style, b, kind)
    else:
        if kind == "panel":
            _split_panel(b, -L / 2, L / 2)
        elif kind == "gate":
            _split_gate(b)
        else:
            _split_post(b, kind)
    return b.build()


def build_style_pieces(style):
    return {k: build_piece(style, k) for k in PIECE_KINDS}


# ---------------------------------------------------------------------------
# laying pieces along LotFence runs (mirrors Core FenceTiling: whole pieces,
# each compressed to exactly run_length / count, never stretched)
# ---------------------------------------------------------------------------
FIT_EPS = 0.0001


def piece_count(length):
    return max(1, math.ceil(length / NOMINAL_LEN - FIT_EPS))


def lay_runs(runs, style, yard_centre, house_ends, protos=None, name="fence"):
    """Instance a style's pieces along runs [(ax, ay, bx, by)...] (Blender
    XY). yard_centre orients each panel's finished -Y face OUT of the yard.
    house_ends: the run end points that touch the house (get an end post,
    body pointing down the run). Returns (objects, per-run report)."""
    protos = protos or build_style_pieces(style)
    for o in protos.values():
        o.hide_render = True
        o.hide_set(True)
    objs, report = [], []
    corners = set()

    def inst(kind, loc, yaw, sx=1.0):
        src = protos[kind]
        o = bpy.data.objects.new("%s_%s_%d" % (name, kind, len(objs)), src.data)
        bpy.context.scene.collection.objects.link(o)
        o.location = (loc[0], loc[1], 0)
        o.rotation_euler = (0, 0, yaw)
        o.scale = (sx, 1, 1)
        objs.append(o)
        return o

    for (ax, ay, bx, by) in runs:
        L = math.hypot(bx - ax, by - ay)
        n = piece_count(L)
        pl = L / n
        dx, dy = (bx - ax) / L, (by - ay) / L
        yaw = math.atan2(dy, dx)
        # local -Y = (dy, -dx) after rotation; flip to face out of the yard
        mx, my = (ax + bx) / 2, (ay + by) / 2
        out = Vector((mx - yard_centre[0], my - yard_centre[1]))
        if Vector((dy, -dx)).dot(out) < 0:
            yaw += math.pi
        for i in range(n):
            t = (i + 0.5) * pl
            inst("panel", (ax + dx * t, ay + dy * t), yaw, pl / NOMINAL_LEN)
        for i in range(1, n):
            t = i * pl
            inst("line_post", (ax + dx * t, ay + dy * t), yaw)
        for (px, py), sgn in (((ax, ay), 1), ((bx, by), -1)):
            key = (round(px, 4), round(py, 4))
            if key in house_ends:
                # end post body runs along +X from the wall: point +X down the run
                inst("end_post", (px, py), math.atan2(dy * sgn, dx * sgn))
            elif key not in corners:
                corners.add(key)
                inst("corner_post", (px, py), yaw)
        report.append({"length_m": round(L, 3), "pieces": n, "piece_len_m": round(pl, 3),
                       "compression": round(pl / NOMINAL_LEN, 3)})
    return objs, report
