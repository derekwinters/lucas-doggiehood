"""The blanket decoration (#413): a soft wool plaid throw lying rumpled on the grass, fringed on
its two short ends, with its front-right corner folded back over itself so the cream fleece
reverse shows. Built from the named dimensions below; no randomness except a fixed seed for the
fringe's small splay, so every run builds the same mesh.

Coordinates: Blender units = metres at in-game size, front -Y, up +Z, pivot on the ground at the
centre of the footprint (the graybox slab's centre in DecorationView).

How it is built:
  1. A flat grid over the cloth (BODY_W x BODY_D, about CELL metres per quad) with UVs in metres
     over PLAID_TILE, so the plaid's scale is real.
  2. Each grid point gets a height above the ground: a few soft ridges (RIDGES) where the throw
     has bunched up, plus a gentle low ripple. The ridges fade out near the fold.
  3. The corner beyond FOLD_LINE is folded back over the cloth: it rolls round a half cylinder
     of radius FOLD_R and lies upside down on top, so its reverse side faces up.
  4. A Solidify modifier gives the cloth THICK of thickness: the top shell keeps the plaid, the
     underside and the cut edge get the cream fleece. It is applied before export.
  5. Fringe: a row of tapered tassels on each short end, lying on the grass, folded with the
     corner where they hang off it.

Object name "blanket"; materials DH_Item_blanket_plaid, DH_Item_blanket_back,
DH_Item_blanket_fringe.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector

NAME = "blanket"

BODY_W = 1.24               # cloth across (X), fringe excluded
BODY_D = 0.92               # cloth deep (Y)
CELL = 0.04                 # target quad size of the cloth grid
THICK = 0.012               # cloth thickness (wool throw)
RIPPLE_H = 0.007            # amplitude of the gentle all-over ripple
RIPPLE_WAVES = ((2.3, 1.1, 0.4), (1.2, 3.1, 1.9), (3.7, 2.2, 4.1))   # (cycles over W, over D, phase)

# Soft ridges where the throw has bunched: (x0, y0, x1, y1, height, half-width), metres.
RIDGES = (
    (-0.56, 0.32, -0.02, 0.04, 0.046, 0.065),
    (-0.42, -0.40, 0.06, -0.10, 0.032, 0.050),
    (0.02, 0.42, 0.44, 0.16, 0.038, 0.060),
    (-0.20, 0.44, -0.36, 0.12, 0.024, 0.040),
)
RIDGE_FADE = 0.10           # ridges fade to nothing this close to the fold line

# The folded corner: everything beyond the line through these two points (on the right short
# edge and on the front long edge) folds back over the cloth.
FOLD_A = (0.62, -0.06)
FOLD_B = (0.22, -0.46)
FOLD_R = 0.026              # radius of the roll along the fold

FRINGE_LEN = 0.066          # tassel length past the cloth edge
FRINGE_PITCH = 0.030        # tassel spacing along the edge
FRINGE_W0 = 0.016           # tassel width at the cloth
FRINGE_W1 = 0.005           # ... at the tip
FRINGE_SEGS = 3             # segments per tassel
FRINGE_SPLAY_DEG = 9.0      # +- random splay of each tassel in the ground plane
FRINGE_DROOP = 0.6          # fraction of the cloth's mid-height the tassel loses by its tip
FRINGE_SEED = 413

PLAID_TILE = 0.32           # metres per plaid repeat (dh_blanket_tex.PLAID_TILE)
BACK_TILE = 0.25            # metres per fleece repeat (dh_blanket_tex.BACK_TILE)
FRINGE_U = 442.5 / 512        # the fringe samples the plaid in the middle of a cream warp stripe

ROUGH_PLAID = 0.92
ROUGH_BACK = 0.98
SPECULAR = 0.08


# ---------------------------------------------------------------------------
# shape
# ---------------------------------------------------------------------------
def _fold_frame():
    ax, ay = FOLD_A
    bx, by = FOLD_B
    tx, ty = bx - ax, by - ay
    ln = math.hypot(tx, ty)
    tx, ty = tx / ln, ty / ln
    nx, ny = ty, -tx                        # unit normal of the line
    # point it at the corner that folds (+X, -Y)
    cx, cy = BODY_W / 2, -BODY_D / 2
    if (cx - ax) * nx + (cy - ay) * ny < 0:
        nx, ny = -nx, -ny
    return (ax, ay), (nx, ny)


def fold_distance(x, y):
    """Signed distance of a flat point beyond the fold line (positive = on the folded corner)."""
    (ax, ay), (nx, ny) = _fold_frame()
    return (x - ax) * nx + (y - ay) * ny


def _ridge(x, y):
    h = 0.0
    for x0, y0, x1, y1, height, hw in RIDGES:
        dx, dy = x1 - x0, y1 - y0
        L2 = dx * dx + dy * dy
        t = max(0.0, min(1.0, ((x - x0) * dx + (y - y0) * dy) / L2))
        px, py = x0 + t * dx, y0 + t * dy
        d = math.hypot(x - px, y - py)
        along = math.sin(math.pi * t)               # fades out at both ends
        h += height * along * math.exp(-(d / hw) ** 2)
    return h


def _ripple(x, y):
    s = 0.0
    for cw, cd, ph in RIPPLE_WAVES:
        s += math.sin(2 * math.pi * (cw * x / BODY_W + cd * y / BODY_D) + ph)
    return RIPPLE_H * (0.5 + 0.5 * s / len(RIPPLE_WAVES))     # 0 .. RIPPLE_H


def lift(x, y):
    """Height of the cloth's mid-surface above the flat (unfolded) position, before folding."""
    d = fold_distance(x, y)
    fade = max(0.0, min(1.0, -d / RIDGE_FADE))      # 0 on and beyond the fold line
    return THICK / 2 + fade * (_ridge(x, y) + _ripple(x, y))


def place(x, y, h):
    """3-D position of the flat point (x, y) raised h along the cloth's up-normal, after folding."""
    d = fold_distance(x, y)
    if d <= 0:
        return Vector((x, y, h))
    _, (nx, ny) = _fold_frame()
    fx, fy = x - d * nx, y - d * ny                 # foot on the fold line
    if d < math.pi * FOLD_R:
        th = d / FOLD_R
        s = FOLD_R * math.sin(th) - h * math.sin(th)
        z = FOLD_R * (1 - math.cos(th)) + h * math.cos(th)
    else:
        s = -(d - math.pi * FOLD_R)
        z = 2 * FOLD_R - h
    return Vector((fx + s * nx, fy + s * ny, z))


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


def materials(tex):
    """tex = dh_blanket_tex.make_all(root)."""
    return {
        "plaid": _material("DH_Item_blanket_plaid", tex["plaid"], ROUGH_PLAID, tex["normal"]),
        "back": _material("DH_Item_blanket_back", tex["back"], ROUGH_BACK),
        "fringe": _material("DH_Item_blanket_fringe", tex["plaid"], ROUGH_PLAID, double_sided=True),
    }


# ---------------------------------------------------------------------------
# mesh
# ---------------------------------------------------------------------------
def _cloth(bm, uv, mi_plaid):
    nx = max(1, round(BODY_W / CELL))
    ny = max(1, round(BODY_D / CELL))
    x0, y0 = -BODY_W / 2, -BODY_D / 2
    grid = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = x0 + BODY_W * i / nx
            y = y0 + BODY_D * j / ny
            row.append((bm.verts.new(place(x, y, lift(x, y))), x, y))
        grid.append(row)
    for j in range(ny):
        for i in range(nx):
            cs = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            f = bm.faces.new([c[0] for c in cs])
            f.material_index = mi_plaid
            f.smooth = True
            for loop, (_, x, y) in zip(f.loops, cs):
                loop[uv].uv = (x / PLAID_TILE, y / PLAID_TILE)
    return grid


def _fringe(bm, uv, mi_fringe):
    rng = random.Random(FRINGE_SEED)
    n = int(BODY_D // FRINGE_PITCH)
    span = (n - 1) * FRINGE_PITCH
    for side in (-1, 1):
        xe = side * BODY_W / 2
        for k in range(n):
            yc = -span / 2 + k * FRINGE_PITCH
            a = math.radians(rng.uniform(-FRINGE_SPLAY_DEG, FRINGE_SPLAY_DEG))
            dx, dy = side * math.cos(a), math.sin(a)
            px, py = -dy, dx                               # across the tassel
            h0 = lift(xe, yc)
            rows = []
            for s in range(FRINGE_SEGS + 1):
                t = s / FRINGE_SEGS
                w = FRINGE_W0 + (FRINGE_W1 - FRINGE_W0) * t
                # start just inside the edge so the tassel tucks under the cut
                along = -0.004 + (FRINGE_LEN + 0.004) * t
                cx, cy = xe + dx * along, yc + dy * along
                h = h0 * (1 - FRINGE_DROOP * t)
                pts = [(cx - px * w / 2, cy - py * w / 2), (cx + px * w / 2, cy + py * w / 2)]
                rows.append([(bm.verts.new(place(qx, qy, h)), qy) for qx, qy in pts])
            for s in range(FRINGE_SEGS):
                a0, a1 = rows[s]
                b0, b1 = rows[s + 1]
                quad = [a0, a1, b1, b0] if side > 0 else [a1, a0, b0, b1]
                f = bm.faces.new([q[0] for q in quad])
                f.material_index = mi_fringe
                f.smooth = True
                for loop, (_, qy) in zip(f.loops, quad):
                    loop[uv].uv = (FRINGE_U, yc / PLAID_TILE)


def build(tex, collection=None):
    """Build the blanket object (modifiers applied) from the textures in tex; returns it."""
    mats = materials(tex)
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
