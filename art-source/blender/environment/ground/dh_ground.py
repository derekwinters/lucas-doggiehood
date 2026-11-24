"""Lawn ground for the starter neighbourhood (#801, CONCEPT round).

The ground is ONE LAWN MESH PER 60 m MAP TILE (WorldDimensions.TileSize),
lying under everything. Roads, curbs, sidewalks, walkways and driveways are
drawn ON TOP of it, so the lawn never shows through them, and the verge and
the open-space fill are simply the same lawn showing where nothing covers it.

Every UV is WORLD SPACE (world position / period), never a per-tile 0..1
UV, so the lawn is continuous across tile and lot borders by construction.

Repeat-hiding approaches (APPROACHES), all with a STANDARD material (URP Lit:
Base Map + Normal Map on UV0, Detail Albedo on UV1; no custom shader):

  plain       2-triangle plane per tile, UV0 = world / period.
  macro   (a) plain + a 256^2 macro tint map on UV1 = world / MACRO_M.
  wang    (b) grid of one-period cells; each cell's UV0 points at one of 4
              shared-edge variants in a 2048^2 atlas, chosen by a seeded hash
              of the cell's WORLD cell coordinates.
  wang_tile   (b) as Derek first described it: the variant is chosen once
              per 60 m map tile instead of per cell.
  combo   (c) (b) + (a).
  cellsN  (d) Derek's idea: grid of 3 m cells, each picking one of the first
              N lightly textured shared-edge variants of a 2048^2 atlas;
              "_macro" adds (a), "1k" uses the 1024^2 atlas.

The cell's variant is baked into the mesh's UVs when the mesh is built
(Core would do this when it builds the tile), not chosen at render time.
Because a cell is identified by its world coordinates, a cell cut by a tile
border gets the same variant and the same UVs on both sides.

Conventions as the rest of art-source/blender: metres, Z up, built in world
coordinates (object at the origin), materials DH_Ground_<slot>.
"""
import math
import os
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "fences")))
import dh_fences as F  # noqa: E402  (puts ../street-props, ../trees and the house kit on the path)
import dh_fence_fit as FF  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402
import dh_props as P  # noqa: E402
import dh_trees_tex as TT  # noqa: E402
import dh_ground_tex as GT  # noqa: E402
import dh_mow_stripes as MS  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

TILE_SIZE = 60.0                 # WorldDimensions.TileSize
ROAD_HALF = S.CURB_D             # 3.0: road centreline -> curb line
VERGE_W = 0.75                   # WorldDimensions.GrassVergeWidth
SIDEWALK_W = 2.0                 # WorldDimensions.SidewalkWidth
SIDEWALK_IN = ROAD_HALF + VERGE_W            # 3.75
SIDEWALK_OUT = SIDEWALK_IN + SIDEWALK_W      # 5.75
WALK_HALF = 0.6                  # front walkway half width (dh_scene.street)
CURB_W = 0.15
PAVE_BOTTOM = -0.03              # paving and roads reach below the lawn plane
ROAD_Z = 0.010                   # road top (on the lawn, which is z = 0)
FLUSH_Z = 0.012                  # paving top when it is "flush" with the lawn
SLAB_Z = 0.04                    # paving top for the "raised slab" variant
RAISED_Z = 0.15                  # curb / verge / sidewalk top for the "raised curb" variant
FRINGE_IN = 0.02                 # fringe card root, this far into the lawn
FRINGE_OUT = 0.05                # fringe tips lean this far over the paving
MULCH_W = 0.5                    # mulch strip under a fence, centred on it
MULCH_Z = 0.02
MOW_W = 0.3                      # concrete mow strip under a fence
HASH_SEED = 801                  # seeds the per-cell variant hash
MACRO_UV = "UVMacro"             # name of UV1

APPROACHES = {
    "rec": {"label": "Chosen: (d)+(a) 8 variants, 1024x512 atlas + macro", "atlas": "cells8", "n": 8,
            "per": "cell", "macro": True},
    "plain": {"label": "Plain tiling", "atlas": None, "macro": False},
    "macro": {"label": "(a) Macro variation", "atlas": None, "macro": True},
    "wang": {"label": "(b) 4 shared-edge variants per period", "atlas": "wang", "n": 4, "per": "cell",
             "macro": False},
    "wang_tile": {"label": "(b) 4 variants, one per 60 m tile", "atlas": "wang", "n": 4, "per": "tile",
                  "macro": False},
    "combo": {"label": "(c) (b) + macro", "atlas": "wang", "n": 4, "per": "cell", "macro": True},
    "cells8": {"label": "(d) 8 light variants / 3 m cell", "atlas": "cells", "n": 8, "per": "cell",
               "macro": False},
    "cells12": {"label": "(d) 12 light variants / 3 m cell", "atlas": "cells", "n": 12, "per": "cell",
                "macro": False},
    "cells16": {"label": "(d) 16 light variants / 3 m cell", "atlas": "cells", "n": 16, "per": "cell",
                "macro": False},
    "cells8_macro": {"label": "(d)+(a) 8 variants + macro", "atlas": "cells", "n": 8, "per": "cell",
                     "macro": True},
    "cells12_macro": {"label": "(d)+(a) 12 variants + macro", "atlas": "cells", "n": 12, "per": "cell",
                      "macro": True},
    "cells1k8": {"label": "(d) 8 variants @1024^2", "atlas": "cells1k", "n": 8, "per": "cell", "macro": False},
    "cells1k8_macro": {"label": "(d)+(a) 8 variants @1024^2 + macro", "atlas": "cells1k", "n": 8,
                       "per": "cell", "macro": True},
    "cells16_macro": {"label": "(d)+(a) 16 variants + macro", "atlas": "cells", "n": 16, "per": "cell",
                      "macro": True},
    "cells1k16_macro": {"label": "(d)+(a) 16 variants @1024^2 + macro", "atlas": "cells1k", "n": 16,
                        "per": "cell", "macro": True},
}

# Edge treatments for the close-ups (mix and match; nothing here picks one).
EDGE_VARIANTS = {
    "v1": {"label": "1. Flat curb band, flush paving, lawn to the posts",
           "curb": "flat", "paving": "flush", "fence": "none"},
    "v2": {"label": "2. Raised curb + verge, raised slabs, mulch strip",
           "curb": "raised", "paving": "slab", "fence": "mulch"},
    "v3": {"label": "3. Flat curb band, grass fringe, mow strip",
           "curb": "flat", "paving": "fringe", "fence": "mowstrip"},
}


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def _uvmap_node(nt, name):
    n = nt.nodes.new("ShaderNodeUVMap")
    n.uv_map = name
    return n


def _stripe_nodes(nt, frame, strength, pattern="front-to-back"):
    """dh_mow_stripes.multiplier() as shader nodes on world position: the
    per-lot shader option (1). Returns the RGB multiplier socket."""
    def math(op, a, b=None, clamp=False):
        n = nt.nodes.new("ShaderNodeMath")
        n.operation = op
        n.use_clamp = clamp
        for i, x in enumerate((a, b)):
            if x is None:
                continue
            if isinstance(x, (int, float)):
                n.inputs[i].default_value = x
            else:
                nt.links.new(x, n.inputs[i])
        return n.outputs[0]

    def vec(op, a, b):
        n = nt.nodes.new("ShaderNodeVectorMath")
        n.operation = op
        for i, x in enumerate((a, b)):
            if isinstance(x, tuple):
                n.inputs[i].default_value = x
            else:
                nt.links.new(x, n.inputs[i])
        return n.outputs["Value"] if op == "DOT_PRODUCT" else n.outputs["Vector"]

    def smooth01(x):
        n = nt.nodes.new("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHSTEP"
        n.clamp = True
        n.inputs["From Min"].default_value = 0.0
        n.inputs["From Max"].default_value = MS.EDGE_FADE
        nt.links.new(x, n.inputs["Value"])
        return n.outputs["Result"]

    geo = nt.nodes.new("ShaderNodeNewGeometry")
    d = vec("SUBTRACT", geo.outputs["Position"], (frame.c[0], frame.c[1], 0.0))
    u = vec("DOT_PRODUCT", d, (frame.a[0], frame.a[1], 0.0))
    v = vec("DOT_PRODUCT", d, (frame.b[0], frame.b[1], 0.0))
    n = MS.stripe_normal(frame, pattern)
    s = vec("DOT_PRODUCT", d, (n[0], n[1], 0.0))          # distance across the stripes
    w = math("SINE", math("MULTIPLY", s, 3.141592653589793 / MS.STRIPE_W))
    w = math("MAXIMUM", math("MINIMUM", math("MULTIPLY", w, MS.EDGE_SHARPNESS), 1.0), -1.0)
    mu = smooth01(math("SUBTRACT", frame.hu, math("ABSOLUTE", u)))
    mv = smooth01(math("SUBTRACT", frame.hv, math("ABSOLUTE", v)))
    k = math("MULTIPLY", math("MULTIPLY", w, math("MULTIPLY", mu, mv)), float(strength))
    sc = nt.nodes.new("ShaderNodeVectorMath")
    sc.operation = "SCALE"
    sc.inputs[0].default_value = MS.AMP_RGB
    nt.links.new(k, sc.inputs["Scale"])
    return vec("ADD", sc.outputs["Vector"], (1.0, 1.0, 1.0))


def lawn_material(look, approach, tint=None, stripes=None):
    """Standard material: base colour + normal on UV0 (the plain texture or
    the approach's atlas), and for macro approaches a detail tint on UV1,
    applied as URP Lit applies Detail Albedo (albedo x 2 x detail).
    stripes = (dh_mow_stripes.LotFrame, strength[, pattern]) adds one lot's
    mow stripes, computed from world position (the shader option)."""
    A = APPROACHES[approach]
    col, nrm = GT.need(look if A["atlas"] is None else "%s_%s" % (look, A["atlas"]))
    name = "DH_Ground_lawn_%s_%s%s" % (look, A["atlas"] or "plain", "_macro" if A["macro"] else "")
    if tint:
        name += "_tint"
    if stripes:
        name += "_mow%03d_%s" % (round(stripes[1] * 100), stripes[2] if len(stripes) > 2 else "front-to-back")
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    uv0 = _uvmap_node(nt, "UVMap")
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(col, check_existing=True)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(nrm, check_existing=True)
    tn.image.colorspace_settings.name = "Non-Color"
    for t in (tc, tn):
        nt.links.new(uv0.outputs["UV"], t.inputs["Vector"])
    out = tc.outputs["Color"]
    if A["macro"]:
        uv1 = _uvmap_node(nt, MACRO_UV)
        tm = nt.nodes.new("ShaderNodeTexImage")
        tm.image = bpy.data.images.load(GT.need("macro"), check_existing=True)
        tm.image.colorspace_settings.name = "Non-Color"     # a multiplier, not a colour
        nt.links.new(uv1.outputs["UV"], tm.inputs["Vector"])
        x2 = nt.nodes.new("ShaderNodeVectorMath")
        x2.operation = "SCALE"
        x2.inputs["Scale"].default_value = 2.0
        nt.links.new(tm.outputs["Color"], x2.inputs[0])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(out, mix.inputs[6])
        nt.links.new(x2.outputs["Vector"], mix.inputs[7])
        out = mix.outputs[2]
    if tint:
        mt = nt.nodes.new("ShaderNodeMix")
        mt.data_type = "RGBA"
        mt.blend_type = "MULTIPLY"
        mt.inputs["Factor"].default_value = 1.0
        mt.inputs[7].default_value = (tint[0], tint[1], tint[2], 1.0)
        nt.links.new(out, mt.inputs[6])
        out = mt.outputs[2]
    if stripes and stripes[1] > 0:
        ms = nt.nodes.new("ShaderNodeMix")
        ms.data_type = "RGBA"
        ms.blend_type = "MULTIPLY"
        ms.inputs["Factor"].default_value = 1.0
        nt.links.new(out, ms.inputs[6])
        nt.links.new(_stripe_nodes(nt, *stripes), ms.inputs[7])
        out = ms.outputs[2]
    nt.links.new(out, bsdf.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 1.0
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.95
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.12
    mat.use_backface_culling = True
    return mat


def fringe_material(look):
    name = "DH_Ground_fringe_%s" % look
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = TT.leaf_material(name, GT.need("fringe")[0])
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = [n for n in nt.nodes if n.type == "TEX_IMAGE"][0]
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs[7].default_value = K.hex_rgba(GT.LAWNS[look]["base"])
    nt.links.new(tex.outputs["Color"], mix.inputs[6])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


_PAVE_SPECS = {   # key: (texture key, tint, roughness); built only when a surface uses it
    "sidewalk": ("concrete", K.COMMON["sidewalk"], 0.85),
    "curb": ("concrete", K.COMMON["concrete"], 0.85),
    "drive": ("concrete", K.COMMON["concrete"], 0.85),
    "mowstrip": ("concrete", "#BDB6AA", 0.9),
    "mulch": ("mulch", None, 0.95),
}


class _PavingMaterials(dict):
    """Concrete surfaces tinted to today's palette (K.COMMON), the road in
    today's flat street colour. Built lazily, so a scene that uses no mulch
    never needs the mulch texture."""

    def __bool__(self):              # "mats or default" must keep an empty lazy map
        return True

    def __missing__(self, key):
        if key == "road":
            m = K.material("DH_Ground_road", K.COMMON["road"], roughness=0.9)
        else:
            tex, tint, rough = _PAVE_SPECS[key]
            m = P._tiled("DH_Ground_" + key, GT.need(tex), tint, rough, 0.0)
        self[key] = m
        return m


def paving_materials():
    """A fresh lazy material map (call again after a scene reset)."""
    return _PavingMaterials()


# ---------------------------------------------------------------------------
# lawn meshes
# ---------------------------------------------------------------------------
def cell_variant(ix, iy, n, seed=HASH_SEED):
    """Seeded hash of a WORLD cell's integer coordinates -> variant 0..n-1.
    Pure integer arithmetic, so Core can reproduce it exactly."""
    h = zlib.crc32(("%d,%d,%d" % (seed, ix, iy)).encode()) & 0xFFFFFFFF
    return h % n


def _slot_uv(kind, k, fx, fy):
    """Atlas UV for slot k at cell-relative (fx, fy) in 0..1 (V up)."""
    G, _, C, pad, _, _ = GT.ATLASES[kind]
    slot = GT.atlas_slot_px(kind)
    W, H = GT.atlas_size_px(kind)
    r, c = divmod(k, G)
    u = (c * slot + pad + fx * C) / W
    v = 1.0 - (r * slot + pad + (1.0 - fy) * C) / H
    return (u, v)


def _mesh(name, verts, faces, uv0, uv1, mat, vcol=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    l0 = me.uv_layers.new(name="UVMap")
    l1 = me.uv_layers.new(name=MACRO_UV)
    for poly in me.polygons:
        for k in range(poly.loop_total):
            li = poly.loop_start + k
            vi = me.loops[li].vertex_index
            l0.data[li].uv = uv0[poly.index][k]
            l1.data[li].uv = uv1[vi]
    if vcol is not None:
        ca = me.color_attributes.new("Col", "BYTE_COLOR", "POINT")
        for i, c in enumerate(vcol):
            ca.data[i].color = c
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = False
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


_MACRO_IMG = None


def macro_multiplier(x, y):
    """The macro map's tint multiplier at a world point (bilinear, wrapped),
    for baking it into vertex colours instead of a second texture."""
    global _MACRO_IMG
    if _MACRO_IMG is None:
        _MACRO_IMG = np.asarray(Image.open(GT.need("macro")), np.float32) / 255.0 * 2.0
    a = _MACRO_IMG
    N = a.shape[0]
    u, v = (x / GT.MACRO_M) % 1.0 * N, (1.0 - (y / GT.MACRO_M) % 1.0) * N
    x0, y0 = int(math.floor(u - 0.5)), int(math.floor(v - 0.5))
    fx, fy = u - 0.5 - x0, v - 0.5 - y0
    p = [a[(y0 + j) % N, (x0 + i) % N] for j in (0, 1) for i in (0, 1)]
    top = p[0] * (1 - fx) + p[1] * fx
    bot = p[2] * (1 - fx) + p[3] * fx
    return top * (1 - fy) + bot * fy


def lawn_tile(look, approach, x0, y0, size=TILE_SIZE, name=None, z=0.0, mat=None, bake_macro_vcol=False):
    """One map tile's lawn, [x0, x0+size] x [y0, y0+size], at height z.
    plain / macro: one quad (2 tris). Atlas approaches: a grid of world
    cells clipped to the tile, each cell's UV0 inside its variant's atlas
    slot. UV1 = world / MACRO_M everywhere."""
    A = APPROACHES[approach]
    name = name or "lawn_%s_%s_%d_%d" % (look, approach, round(x0), round(y0))
    mat = mat or lawn_material(look, approach)
    x1, y1 = x0 + size, y0 + size
    verts, faces, uv0 = [], [], []
    vidx = {}

    def V(x, y):
        key = (round(x, 5), round(y, 5))
        if key not in vidx:
            vidx[key] = len(verts)
            verts.append((x, y, z))
        return vidx[key]

    if A["atlas"] is None:
        P_ = GT.LAWNS[look]["tile_m"]
        q = [V(x0, y0), V(x1, y0), V(x1, y1), V(x0, y1)]
        faces.append(q)
        uv0.append([(verts[i][0] / P_, verts[i][1] / P_) for i in q])
    else:
        kind = A["atlas"]
        cm = GT.ATLASES[kind][4] or GT.LAWNS[look]["tile_m"]
        tile_k = cell_variant(int(math.floor(x0 / TILE_SIZE)), int(math.floor(y0 / TILE_SIZE)), A["n"],
                              HASH_SEED + 1)
        ix0, ix1 = int(math.floor(x0 / cm + 1e-9)), int(math.ceil(x1 / cm - 1e-9))
        iy0, iy1 = int(math.floor(y0 / cm + 1e-9)), int(math.ceil(y1 / cm - 1e-9))
        for iy in range(iy0, iy1):
            for ix in range(ix0, ix1):
                cx0, cy0 = ix * cm, iy * cm
                ax0, ax1 = max(x0, cx0), min(x1, cx0 + cm)
                ay0, ay1 = max(y0, cy0), min(y1, cy0 + cm)
                if ax1 - ax0 < 1e-6 or ay1 - ay0 < 1e-6:
                    continue
                k = tile_k if A["per"] == "tile" else cell_variant(ix, iy, A["n"])
                q = [V(ax0, ay0), V(ax1, ay0), V(ax1, ay1), V(ax0, ay1)]
                faces.append(q)
                uv0.append([_slot_uv(kind, k, (verts[i][0] - cx0) / cm, (verts[i][1] - cy0) / cm) for i in q])
    uv1 = [(v[0] / GT.MACRO_M, v[1] / GT.MACRO_M) for v in verts]
    vcol = None
    if bake_macro_vcol:
        # glTF COLOR_0 multiplies base colour but must stay <= 1, so the
        # multiplier is divided by its brightest value (a slight overall darkening)
        top = max(GT.MACRO_LIGHT)
        vcol = []
        for v in verts:
            m = macro_multiplier(v[0], v[1]) / top
            vcol.append((float(m[0]), float(m[1]), float(m[2]), 1.0))
    return _mesh(name, verts, faces, uv0, uv1, mat, vcol)


def lawn_tiles(look, approach, tiles, **kw):
    """tiles: iterable of (x0, y0) tile corners."""
    return [lawn_tile(look, approach, x0, y0, **kw) for x0, y0 in tiles]


def tri_count(objs):
    return sum(K.tri_count(o) for o in objs)


# ---------------------------------------------------------------------------
# paving drawn on top of the lawn
# ---------------------------------------------------------------------------
class Paving(P.PropBuilder):
    def __init__(self, name):
        super().__init__(name)

    def slab(self, x0, y0, x1, y1, top, mat, tile=GT.CONCRETE_M):
        self.box((min(x0, x1), min(y0, y1), PAVE_BOTTOM), (max(x0, x1), max(y0, y1), top), mat, tile=tile)

    def ramp_y(self, x0, x1, ya, yb, za, zb, mat, tile=GT.CONCRETE_M):
        """Slab from y = ya (top za) to y = yb (top zb): a curb-cut apron."""
        z = PAVE_BOTTOM
        c = [Vector(p) for p in ((x0, ya, z), (x1, ya, z), (x1, yb, z), (x0, yb, z),
                                 (x0, ya, za), (x1, ya, za), (x1, yb, zb), (x0, yb, zb))]
        cen = sum(c, Vector()) / 8
        for q in ((4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            vs = [c[i] for i in q]
            self.pface(vs, mat, sum(vs, Vector()) / 4 - cen, tile)

    def build(self, mats=None):
        return super().build(mats or paving_materials())


def street_tile(cx, cy, half=TILE_SIZE / 2, name="street"):
    """A FourWay tile's road, curb band and sidewalks around centre (cx, cy),
    flush-flat (today's look), on top of the lawn. Returns one object."""
    b = Paving(name)
    x0, x1, y0, y1 = cx - half, cx + half, cy - half, cy + half
    b.slab(x0, cy - ROAD_HALF, x1, cy + ROAD_HALF, ROAD_Z, "road", tile=4.0)
    for ya, yb in ((y0, cy - ROAD_HALF), (cy + ROAD_HALF, y1)):
        b.slab(cx - ROAD_HALF, ya, cx + ROAD_HALF, yb, ROAD_Z, "road", tile=4.0)
    for s in (-1, 1):
        # road along X: curb band and sidewalks, broken where the other road crosses
        for xa, xb in ((x0, cx - ROAD_HALF), (cx + ROAD_HALF, x1)):
            b.slab(xa, cy + s * ROAD_HALF, xb, cy + s * (ROAD_HALF + CURB_W), FLUSH_Z, "curb")
            b.slab(xa, cy + s * SIDEWALK_IN, xb, cy + s * SIDEWALK_OUT, FLUSH_Z, "sidewalk")
        # road along Y
        for ya, yb in ((y0, cy - SIDEWALK_OUT), (cy + SIDEWALK_OUT, y1)):
            b.slab(cx + s * SIDEWALK_IN, ya, cx + s * SIDEWALK_OUT, yb, FLUSH_Z, "sidewalk")
        for ya, yb in ((y0, cy - ROAD_HALF - CURB_W), (cy + ROAD_HALF + CURB_W, y1)):
            b.slab(cx + s * ROAD_HALF, ya, cx + s * (ROAD_HALF + CURB_W), yb, FLUSH_Z, "curb")
    return b.build()


def tile_grid(n_side=3, origin=(0.0, 0.0)):
    """Corners of an n x n block of tiles centred on `origin` (a tile centre)."""
    ox, oy = origin
    h = n_side // 2
    return [(ox - TILE_SIZE / 2 + i * TILE_SIZE, oy - TILE_SIZE / 2 + j * TILE_SIZE)
            for j in range(-h, h + 1) for i in range(-h, h + 1)]


# ---------------------------------------------------------------------------
# the starter corner lot (base 2, level 4, fence style C) on its tile
# ---------------------------------------------------------------------------
def lot_scene(base=2, toward=True, fence_style="splitrail"):
    """House + fence + street + walkway + driveway, everything but the lawn.
    Returns a dict; the lawn tiles are added by the caller per approach."""
    lot = FF.build_lot(base, toward)
    res = FF.check(lot)
    ends = FF.contact_ends(res)
    bpy.data.objects.remove(lot["driveway"])        # redrawn flush below
    runs = list(lot["runs"])
    lo, hi = ends["connector_low"], ends["connector_high"]
    runs[0] = (lo[0], lo[1], runs[0][2], runs[0][3])
    runs[4] = (runs[4][0], runs[4][1], hi[0], hi[1])
    Pl = lot["P"]
    fx0, fx1, _, rear = Pl["fence"]
    house_ends = {(round(lo[0], 4), round(lo[1], 4)), (round(hi[0], 4), round(hi[1], 4))}
    fence, _ = F.lay_runs(runs, fence_style, ((fx0 + fx1) / 2, rear / 2), house_ends, name="fence_" + fence_style)
    cx, cy = Pl["side_road_x"], Pl["road_cl_y"]
    streets = [street_tile(x0 + TILE_SIZE / 2, y0 + TILE_SIZE / 2, name="street_%d" % i)
               for i, (x0, y0) in enumerate(tile_grid(3, (cx, cy)))]
    B = lot["B"]
    door_x = getattr(B, "DOOR_X", 0.0)
    pv = Paving("lot_paving")
    pv.slab(door_x - WALK_HALF, cy + SIDEWALK_OUT, door_x + WALK_HALF, lot["bounds"][2] + 0.05, FLUSH_Z, "sidewalk")
    dx0, dx1 = lot["dinfo"]["x"]
    curb, y_apron = lot["dinfo"]["y"]
    pv.slab(dx0, cy + ROAD_HALF, dx1, cy + SIDEWALK_IN, FLUSH_Z, "drive")
    pv.slab(dx0, cy + SIDEWALK_OUT, dx1, y_apron, FLUSH_Z, "drive")
    paving = pv.build()
    dogs = [S.dog_box((fx0 + 2.2, rear - 3.0, 0), yaw_deg=120), S.dog_box((fx1 - 1.6, rear - 2.0, 0), yaw_deg=200)]
    return {"lot": lot, "fence": fence, "streets": streets, "paving": paving, "dogs": dogs,
            "tile_centre": (cx, cy), "tiles": tile_grid(3, (cx, cy))}


# ---------------------------------------------------------------------------
# edge close-up vignette: road along X (centreline y = 0), lot toward +Y
# ---------------------------------------------------------------------------
VIG_X = (-7.0, 12.0)
VIG_WALK_X = 2.0
VIG_DRIVE_X = (-5.0, -2.0)
VIG_PAVE_END = 10.5
VIG_FENCE = [(4.0, 8.0, 9.8, 8.0), (9.8, 8.0, 9.8, 13.5)]
VIG_YARD_C = (7.5, 11.5)


def _minus(span, holes):
    """1-D interval minus a list of intervals."""
    out = [span]
    for h0, h1 in holes:
        nxt = []
        for a, b in out:
            if h1 <= a or h0 >= b:
                nxt.append((a, b))
                continue
            if h0 > a:
                nxt.append((a, h0))
            if h1 < b:
                nxt.append((h1, b))
        out = nxt
    return out


def _fringe(b, segs):
    """Grass-fringe cards along lawn edges. segs: (ax, ay, bx, by, nx, ny,
    z) with (nx, ny) pointing INTO the lawn and z the paving top."""
    for ax, ay, bx, by, nx, ny, z in segs:
        L = math.hypot(bx - ax, by - ay)
        root = (nx * FRINGE_IN, ny * FRINGE_IN)
        tip = (-nx * FRINGE_OUT, -ny * FRINGE_OUT)
        vs = [(ax + root[0], ay + root[1], 0.0), (bx + root[0], by + root[1], 0.0),
              (bx + tip[0], by + tip[1], z + GT.FRINGE_H), (ax + tip[0], ay + tip[1], z + GT.FRINGE_H)]
        uv = [(0, 0), (L / GT.FRINGE_M, 0), (L / GT.FRINGE_M, 1), (0, 1)]
        b.oface(vs, uv, "fringe", (-nx, -ny, 0.3))


def vignette(variant, look):
    """Everything but the base lawn tile for one edge variant. Returns
    (objects, {framing: plan points})."""
    E = EDGE_VARIANTS[variant]
    x0, x1 = VIG_X
    raised = E["curb"] == "raised"
    top = {"flush": FLUSH_Z, "fringe": FLUSH_Z, "slab": SLAB_Z}[E["paving"]]
    band = RAISED_Z if raised else top          # curb / verge / sidewalk band
    pv = Paving("vig_paving_" + variant)
    objs = []
    pv.slab(x0, -ROAD_HALF, x1, ROAD_HALF, ROAD_Z, "road", tile=4.0)
    dxa, dxb = VIG_DRIVE_X
    if raised:
        for xa, xb in _minus((x0, x1), [VIG_DRIVE_X]):
            pv.slab(xa, ROAD_HALF, xb, ROAD_HALF + CURB_W, RAISED_Z, "curb")
        pv.ramp_y(dxa, dxb, ROAD_HALF, SIDEWALK_IN, ROAD_Z + 0.004, RAISED_Z, "drive")
        # raised verge: the same world-space lawn, lifted to the curb top
        for xa, xb in _minus((x0, x1), [VIG_DRIVE_X]):
            objs.append(_raised_lawn(look, xa, ROAD_HALF + CURB_W, xb, SIDEWALK_IN, RAISED_Z))
    else:
        for xa, xb in _minus((x0, x1), [VIG_DRIVE_X]):
            pv.slab(xa, ROAD_HALF, xb, ROAD_HALF + CURB_W, top, "curb")
        pv.slab(dxa, ROAD_HALF, dxb, SIDEWALK_IN, top, "drive")
    pv.slab(x0, SIDEWALK_IN, x1, SIDEWALK_OUT, band, "sidewalk")
    pv.slab(VIG_WALK_X - WALK_HALF, SIDEWALK_OUT, VIG_WALK_X + WALK_HALF, VIG_PAVE_END, top, "sidewalk")
    pv.slab(dxa, SIDEWALK_OUT, dxb, VIG_PAVE_END, top, "drive")
    if E["paving"] == "fringe":
        segs = []
        holes = [VIG_DRIVE_X, (VIG_WALK_X - WALK_HALF, VIG_WALK_X + WALK_HALF)]
        for xa, xb in _minus((x0, x1), holes):
            segs.append((xa, SIDEWALK_OUT, xb, SIDEWALK_OUT, 0, 1, band))
        for xa, xb in _minus((x0, x1), [VIG_DRIVE_X]):
            segs.append((xa, SIDEWALK_IN, xb, SIDEWALK_IN, 0, -1, band))
            segs.append((xa, ROAD_HALF + CURB_W, xb, ROAD_HALF + CURB_W, 0, 1, band))
        for xa, xb in ((VIG_WALK_X - WALK_HALF, VIG_WALK_X + WALK_HALF), VIG_DRIVE_X):
            segs.append((xa, SIDEWALK_OUT, xa, VIG_PAVE_END, -1, 0, top))
            segs.append((xb, SIDEWALK_OUT, xb, VIG_PAVE_END, 1, 0, top))
            segs.append((xa, VIG_PAVE_END, xb, VIG_PAVE_END, 0, 1, top))
        for xa in VIG_DRIVE_X:
            n = -1 if xa == VIG_DRIVE_X[0] else 1
            segs.append((xa, ROAD_HALF + CURB_W, xa, SIDEWALK_IN, n, 0, top))
        _fringe(pv, segs)
    # under the fence
    for ax, ay, bx, by in VIG_FENCE:
        if E["fence"] == "none":
            continue
        w, key, z = (MULCH_W, "mulch", MULCH_Z) if E["fence"] == "mulch" else (MOW_W, "mowstrip", FLUSH_Z)
        h = w / 2
        lo = (min(ax, bx) - h, min(ay, by) - h)
        hi = (max(ax, bx) + h, max(ay, by) + h)
        pv.slab(lo[0], lo[1], hi[0], hi[1], z, key, tile=GT.MULCH_M if key == "mulch" else GT.CONCRETE_M)
    mats = paving_materials()
    mats["fringe"] = fringe_material(look)
    objs.append(pv.build(mats))
    fence, _ = F.lay_runs(VIG_FENCE, "splitrail", VIG_YARD_C, set(), name="vig_fence_" + variant)
    objs += fence
    objs.append(S.dog_box((0.4, 8.6, 0), yaw_deg=150))
    return objs, VIG_FRAMES


# close-up framings (plan rectangles) for the vignette
VIG_FRAMES = {
    "street": [(-5.6, 1.6), (3.4, 1.6), (-5.6, 8.8), (3.4, 8.8)],     # curb, verge, sidewalk, walkway, drive
    "fence": [(3.2, 6.6), (11.0, 6.6), (3.2, 14.0), (11.0, 14.0)],    # under the fence line
}


def _raised_lawn(look, x0, y0, x1, y1, z):
    """A raised lawn block (the verge in the raised-curb variant): top in the
    same world-space UVs as the tile lawn, so it continues its texture."""
    Pm = GT.LAWNS[look]["tile_m"]
    verts = [(x0, y0, 0.0), (x1, y0, 0.0), (x1, y1, 0.0), (x0, y1, 0.0),
             (x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
    faces = [(4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    uv0 = []
    for f in faces:
        if f == (4, 5, 6, 7):
            uv0.append([(verts[i][0] / Pm, verts[i][1] / Pm) for i in f])
        else:   # sides: planar along the edge, V up
            ax = 0 if abs(verts[f[0]][1] - verts[f[1]][1]) < 1e-9 else 1
            uv0.append([(verts[i][ax] / Pm, verts[i][2] / Pm) for i in f])
    uv1 = [(v[0] / GT.MACRO_M, v[1] / GT.MACRO_M) for v in verts]
    return _mesh("raised_verge", verts, faces, uv0, uv1, lawn_material(look, "plain"))
