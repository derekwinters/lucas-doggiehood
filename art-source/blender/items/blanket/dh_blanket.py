"""The blanket decoration (#413): a soft wool plaid throw lying gently rumpled on the grass, with
a cream fleece underside and a short fringe on its two short ends. Kept simple on purpose (Derek,
2026-11-27): a coarse grid with a few gentle rumples, no folded corner, one-quad tassels.

The size and the colourways live in blanket_spec.py. Every colourway builds the same mesh; only
the plaid material (and its texture) changes, so one mesh with a swapped material is enough
in the game (#932).

Coordinates: Blender units = metres at in-game size, front -Y, up +Z, pivot on the ground at the
centre of the footprint.

How it is built:
  1. A flat grid over the cloth (CLOTH_W x CLOTH_D, about CELL metres per quad) with UVs in metres
     over PLAID_TILE, so the plaid's scale is real.
  2. Each grid point is lifted by a few soft, low ridges (RIDGES, placed as fractions of the cloth
     so they follow a size change) plus a gentle ripple.
  3. A Solidify modifier gives the cloth THICK of thickness: the top shell keeps the plaid, the
     underside and the cut edge get the cream fleece. It is applied before export.
  4. Fringe: one tapered quad per tassel on each short end, lying on the grass. The splay comes
     from a fixed seed, so every run builds the same mesh.

Object name "blanket"; materials DH_Item_blanket_plaid_<Colourway>, DH_Item_blanket_back,
DH_Item_blanket_fringe_<Colourway>.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blanket_spec as S  # noqa: E402

NAME = "blanket"

CELL = 0.08                 # target quad size of the cloth grid (gentle rumples need no more)
THICK = 0.012               # cloth thickness (wool throw)
RIPPLE_H = 0.005            # amplitude of the gentle all-over ripple
RIPPLE_WAVES = ((2.3, 1.1, 0.4), (1.2, 3.1, 1.9), (3.7, 2.2, 4.1))   # (cycles over W, over D, phase)

# Soft, low ridges where the throw has settled unevenly: (u0, v0, u1, v1, height m, half-width m),
# with u, v as fractions of the cloth from its centre (-0.5 .. 0.5), so they scale with the size.
RIDGES = (
    (-0.42, 0.30, -0.05, 0.05, 0.022, 0.11),
    (0.05, -0.35, 0.38, -0.12, 0.018, 0.10),
    (0.10, 0.38, 0.40, 0.20, 0.016, 0.09),
)
EDGE_SETTLE = 0.06          # ridges fade out this close to the edges, so the hem lies on the grass

FRINGE_PITCH = 0.05         # tassel spacing along the edge
FRINGE_W0 = 0.022           # tassel width at the cloth
FRINGE_W1 = 0.008           # ... at the tip
FRINGE_SPLAY_DEG = 8.0      # +- random splay of each tassel in the ground plane
FRINGE_TUCK = 0.004         # the tassel starts this far under the cloth's cut edge
FRINGE_TIP_H = 0.002        # tip height above the ground
FRINGE_SEED = 413

PLAID_TILE = 0.32           # metres per plaid repeat (dh_blanket_tex.PLAID_TILE)
BACK_TILE = 0.25            # metres per fleece repeat (dh_blanket_tex.BACK_TILE)
FRINGE_U = 442.5 / 512      # the fringe samples the plaid in the middle of a light warp stripe

ROUGH_PLAID = 0.92
ROUGH_BACK = 0.98
SPECULAR = 0.08


# ---------------------------------------------------------------------------
# shape
# ---------------------------------------------------------------------------
def _ridge(x, y):
    h = 0.0
    for u0, v0, u1, v1, height, hw in RIDGES:
        x0, y0, x1, y1 = u0 * S.CLOTH_W, v0 * S.CLOTH_D, u1 * S.CLOTH_W, v1 * S.CLOTH_D
        dx, dy = x1 - x0, y1 - y0
        L2 = dx * dx + dy * dy
        t = max(0.0, min(1.0, ((x - x0) * dx + (y - y0) * dy) / L2))
        d = math.hypot(x - (x0 + t * dx), y - (y0 + t * dy))
        h += height * math.sin(math.pi * t) * math.exp(-(d / hw) ** 2)
    return h


def _ripple(x, y):
    s = 0.0
    for cw, cd, ph in RIPPLE_WAVES:
        s += math.sin(2 * math.pi * (cw * x / S.CLOTH_W + cd * y / S.CLOTH_D) + ph)
    return RIPPLE_H * (0.5 + 0.5 * s / len(RIPPLE_WAVES))     # 0 .. RIPPLE_H


def lift(x, y):
    """Height of the cloth's mid-surface above the ground."""
    edge = min(S.CLOTH_W / 2 - abs(x), S.CLOTH_D / 2 - abs(y))
    settle = max(0.0, min(1.0, edge / EDGE_SETTLE))
    return THICK / 2 + settle * (_ridge(x, y) + _ripple(x, y))


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def _material(name, color_path, rough, normal_path=None, double_sided=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = SPECULAR
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(color_path, check_existing=True)
    tn.interpolation = "Linear"
    nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
    if normal_path:
        nn = nt.nodes.new("ShaderNodeTexImage")
        nn.image = bpy.data.images.load(normal_path, check_existing=True)
        nn.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(nn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    mat.use_backface_culling = not double_sided
    return mat


def material_names(colourway):
    c = colourway.capitalize()
    return {"plaid": "DH_Item_blanket_plaid_" + c, "back": "DH_Item_blanket_back",
            "fringe": "DH_Item_blanket_fringe_" + c}


def materials(tex, colourway):
    """tex = dh_blanket_tex.make_all(root)."""
    n = material_names(colourway)
    plaid = tex["plaid"][colourway]
    return {
        "plaid": _material(n["plaid"], plaid, ROUGH_PLAID, tex["normal"]),
        "back": _material(n["back"], tex["back"], ROUGH_BACK),
        "fringe": _material(n["fringe"], plaid, ROUGH_PLAID, double_sided=True),
    }


# ---------------------------------------------------------------------------
# mesh
# ---------------------------------------------------------------------------
def _cloth(bm, uv, mi_plaid):
    nx = max(1, round(S.CLOTH_W / CELL))
    ny = max(1, round(S.CLOTH_D / CELL))
    x0, y0 = -S.CLOTH_W / 2, -S.CLOTH_D / 2
    grid = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = x0 + S.CLOTH_W * i / nx
            y = y0 + S.CLOTH_D * j / ny
            row.append((bm.verts.new(Vector((x, y, lift(x, y)))), x, y))
        grid.append(row)
    for j in range(ny):
        for i in range(nx):
            cs = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            f = bm.faces.new([c[0] for c in cs])
            f.material_index = mi_plaid
            f.smooth = True
            for loop, (_, x, y) in zip(f.loops, cs):
                loop[uv].uv = (x / PLAID_TILE, y / PLAID_TILE)


def _fringe(bm, uv, mi_fringe):
    rng = random.Random(FRINGE_SEED)
    n = int(S.CLOTH_D // FRINGE_PITCH)
    span = (n - 1) * FRINGE_PITCH
    for side in (-1, 1):
        xe = side * S.CLOTH_W / 2
        for k in range(n):
            yc = -span / 2 + k * FRINGE_PITCH
            a = math.radians(rng.uniform(-FRINGE_SPLAY_DEG, FRINGE_SPLAY_DEG))
            dx, dy = side * math.cos(a), math.sin(a)
            px, py = -dy, dx                               # across the tassel
            h0 = lift(xe, yc)
            ends = []
            # the tip's outer corner, splay included, ends exactly FRINGE_LEN past the edge
            tip = (S.FRINGE_LEN - abs(math.sin(a)) * FRINGE_W1 / 2) / math.cos(a)
            for along, w, h in ((-FRINGE_TUCK, FRINGE_W0, h0), (tip, FRINGE_W1, FRINGE_TIP_H)):
                cx, cy = xe + dx * along, yc + dy * along
                ends.append([bm.verts.new(Vector((cx - px * w / 2, cy - py * w / 2, h))),
                             bm.verts.new(Vector((cx + px * w / 2, cy + py * w / 2, h)))])
            (a0, a1), (b0, b1) = ends
            quad = [a0, a1, b1, b0] if side > 0 else [a1, a0, b0, b1]
            f = bm.faces.new(quad)
            f.material_index = mi_fringe
            f.smooth = True
            for loop in f.loops:
                loop[uv].uv = (FRINGE_U, yc / PLAID_TILE)


def build(tex, colourway=S.DEFAULT, collection=None):
    """Build the blanket object in one colourway (modifiers applied); returns it."""
    mats = materials(tex, colourway)
    order = ["plaid", "back", "fringe"]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    _cloth(bm, uv, order.index("plaid"))
    cloth_me = bpy.data.meshes.new(NAME + "_cloth")
    bm.to_mesh(cloth_me)
    bm.free()
    for k in order:
        cloth_me.materials.append(mats[k])
    coll = collection or bpy.context.scene.collection
    cloth = bpy.data.objects.new(NAME + "_cloth", cloth_me)
    coll.objects.link(cloth)
    sol = cloth.modifiers.new("solidify", "SOLIDIFY")
    sol.thickness = THICK
    sol.offset = 0.0
    sol.use_even_offset = True
    sol.use_rim = True
    sol.material_offset = order.index("back")
    sol.material_offset_rim = order.index("back")
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(cloth.evaluated_get(dg))
    bpy.data.objects.remove(cloth)
    bpy.data.meshes.remove(cloth_me)
    me.name = NAME
    # fleece UVs on the underside and rim: same layout, scaled to the fleece repeat
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    back_scale = PLAID_TILE / BACK_TILE
    # Solidify offsets along the normals, so where a rumple slopes at the hem the cut edge
    # leans out by a fraction of a millimetre: pin it back onto the cloth's outline.
    hw, hd = S.CLOTH_W / 2, S.CLOTH_D / 2
    for v in bm.verts:
        v.co.x = max(-hw, min(hw, v.co.x))
        v.co.y = max(-hd, min(hd, v.co.y))
    for f in bm.faces:
        if f.material_index == order.index("back"):
            for loop in f.loops:
                u, v = loop[uv].uv
                loop[uv].uv = (u * back_scale, v * back_scale)
    _fringe(bm, uv, order.index("fringe"))
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="FIXED", ngon_method="BEAUTY")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(NAME, me)
    coll.objects.link(ob)
    return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)
