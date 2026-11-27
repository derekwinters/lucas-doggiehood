"""The delivery package (#410): a taped cardboard parcel, built from DIMS.

The box the delivery truck drops at a dog's front door (DeliveryTruckView.DropPackage, a 0.6 m
graybox cube today; #930 swaps this model in). One mesh, three materials:

  DH_Package_cardboard   kraft board, tileable colour + normal map (0.5 m per repeat)
  DH_Package_tape        brown packing tape, flat colour, a little glossy
  DH_Package_label       plain shipping label (bars and a barcode, no words or logo)

Shape: a box with chamfered edges; one strip of tape runs along the length over the top seam (where
the flaps meet) and down both ends; a label sits on the top beside the tape, where the 45 degree game
camera sees it.

Conventions (docs/engineering/blender-pipeline.md): metres, Z up, front -Y; the pivot is on the
ground at the footprint centre, like a street prop. DIMS is the outer size including the tape, so
the bounding box is exactly DIMS.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# the committed house kit (art-source/blender), for hex_rgba and tri_count
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_package_tex as PT  # noqa: E402

NAME = "package"

DIMS = {
    "length": 0.60,          # X, outer, tape included (the graybox cube's 0.6 m)
    "depth": 0.50,           # Y
    "height": 0.45,          # Z, top of the tape
}
CHAMFER = 0.015              # board edges and corners
TAPE_T = 0.003               # tape thickness, standing proud of the board
TAPE_W = 0.085               # tape width (a 48 mm roll, widened to read at the game camera)
TAPE_DROP = 0.13             # how far the tape runs down each end from the top edge
LABEL_T = 0.002
LABEL_SIZE = (0.20, 0.13)    # (X, Y) on the top face
LABEL_CENTRE = (0.135, -0.135)
LABEL_EDGE_UV = (0.01, 0.5)  # label edges show the label's grey border
CARD_TILE = 0.5              # metres per cardboard texture repeat

CARDBOARD_ROUGHNESS = 0.85
TAPE_HEX = "#8E6236"
TAPE_ROUGHNESS = 0.35
LABEL_ROUGHNESS = 0.7
NORMAL_STRENGTH = 1.0

MATERIALS = ("cardboard", "tape", "label")


def tex_dir():
    return os.environ.get("DH_PACKAGE_TEXTURES") or PT.TEX_DIR


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def _image(path, non_color=False):
    im = bpy.data.images.load(path, check_existing=True)
    if non_color:
        im.colorspace_settings.name = "Non-Color"
    return im


def _material(key, rough, colour=None, normal=None, hex_=None):
    mat = bpy.data.materials.new("DH_Package_" + key)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    if colour:
        tc = nt.nodes.new("ShaderNodeTexImage")
        tc.image = _image(colour)
        nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = K.hex_rgba(hex_)
        mat.diffuse_color = K.hex_rgba(hex_)
    if normal:
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = _image(normal, non_color=True)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.inputs["Strength"].default_value = NORMAL_STRENGTH
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = rough
    mat.use_backface_culling = True            # closed mesh: single-sided in glTF
    return mat


def materials():
    tx = PT.paths(tex_dir())
    col, nrm = tx["cardboard"]
    return [_material("cardboard", CARDBOARD_ROUGHNESS, colour=col, normal=nrm),
            _material("tape", TAPE_ROUGHNESS, hex_=TAPE_HEX),
            _material("label", LABEL_ROUGHNESS, colour=tx["label"])]


# ---------------------------------------------------------------------------
# geometry
# ---------------------------------------------------------------------------
def _box_uv(co, n):
    """Box-mapped UV in texture repeats; V runs up the sides and along Y on the top."""
    a = max(range(3), key=lambda i: abs(n[i]))
    if a == 2:
        u, v = co.x, co.y
    elif a == 0:
        u, v = co.y, co.z
    else:
        u, v = co.x, co.z
    return u / CARD_TILE, v / CARD_TILE


def _body(bm, uv, lb, db, hb):
    """A box with every edge chamfered by CHAMFER, from explicit faces in a fixed order (Blender's
    bevel operator gave a different face order from one process to the next): 6 sides, 12 edge
    strips and 8 corner triangles. V(s, k) is corner s's vertex on the side facing axis k."""
    half = (lb / 2, db / 2, hb / 2)
    centre = Vector((0, 0, hb / 2))
    signs = (-1, 1)

    def V(s, k):
        return centre + Vector([s[i] * (half[i] if i == k else half[i] - CHAMFER) for i in range(3)])

    def face(pts):
        c = sum(pts, Vector()) / len(pts)
        f = _face(bm, uv, pts, c - centre, "cardboard")
        for loop in f.loops:
            loop[uv].uv = _box_uv(loop.vert.co, f.normal)

    def corner(k, sk, j, sj, l, sl):
        s = [0, 0, 0]
        s[k], s[j], s[l] = sk, sj, sl
        return tuple(s)

    for k in range(3):                                   # the six sides
        j, l = (k + 1) % 3, (k + 2) % 3
        for sk in signs:
            face([V(corner(k, sk, j, a, l, b), k) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    for k in range(3):                                   # twelve edge strips, along axis k
        j, l = (k + 1) % 3, (k + 2) % 3
        for sj in signs:
            for sl in signs:
                c0, c1 = corner(k, -1, j, sj, l, sl), corner(k, 1, j, sj, l, sl)
                face([V(c0, j), V(c1, j), V(c1, l), V(c0, l)])
    for sx in signs:                                     # eight corners
        for sy in signs:
            for sz in signs:
                s = (sx, sy, sz)
                face([V(s, 0), V(s, 1), V(s, 2)])


def _face(bm, uv, pts, out, mat, uvs=None):
    """A flat face on `pts`, wound so its normal points along `out`."""
    pts = [Vector(p) for p in pts]
    n = Vector((0, 0, 0))
    for i in range(len(pts)):                            # Newell normal
        a, b = pts[i], pts[(i + 1) % len(pts)]
        n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
    if n.dot(Vector(out)) < 0:
        pts.reverse()
        uvs = uvs[::-1] if uvs else None
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    f.normal_update()
    f.material_index = MATERIALS.index(mat)
    for i, loop in enumerate(f.loops):
        loop[uv].uv = uvs[i] if uvs else (0.0, 0.0)
    return f


def _tape_profile(lb, hb, offset):
    """The tape's path over the board in XZ, from the bottom of the -X end, over the top, to the
    bottom of the +X end; `offset` pushes it out along the surface normal (0 = on the board)."""
    t = offset * math.tan(math.pi / 8)        # corner shift for a 45 degree chamfer
    x, z0, zc = lb / 2 + offset, hb - TAPE_DROP, hb - CHAMFER + t
    xc, ztop = lb / 2 - CHAMFER + t, hb + offset
    return [(-x, z0), (-x, zc), (-xc, ztop), (xc, ztop), (x, zc), (x, z0)]


def _tape(bm, uv, lb, hb):
    inner, outer = _tape_profile(lb, hb, 0.0), _tape_profile(lb, hb, TAPE_T)
    y0, y1 = -TAPE_W / 2, TAPE_W / 2
    for i in range(len(inner) - 1):
        (ax, az), (bx, bz) = outer[i], outer[i + 1]
        mid = Vector(((ax + bx) / 2, 0, (az + bz) / 2 - hb / 2))
        _face(bm, uv, [(ax, y0, az), (bx, y0, bz), (bx, y1, bz), (ax, y1, az)], mid, "tape")
        (cx, cz), (dx, dz) = inner[i], inner[i + 1]
        for y, out in ((y0, (0, -1, 0)), (y1, (0, 1, 0))):
            _face(bm, uv, [(cx, y, cz), (dx, y, dz), (bx, y, bz), (ax, y, az)], out, "tape")
    for j, sx in ((0, -1), (-1, 1)):            # the two cut ends of the strip
        (ix, iz), (ox, oz) = inner[j], outer[j]
        _face(bm, uv, [(ix, y0, iz), (ox, y0, oz), (ox, y1, oz), (ix, y1, iz)], (0, 0, -1), "tape")


def _label(bm, uv, hb):
    (cx, cy), (w, d) = LABEL_CENTRE, LABEL_SIZE
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2
    z0, z1 = hb, hb + LABEL_T
    # label image: its long side along X, top of the text toward +Y (readable from the front)
    _face(bm, uv, [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1), "label",
          uvs=[(0, 0), (1, 0), (1, 1), (0, 1)])
    e = [LABEL_EDGE_UV] * 4
    _face(bm, uv, [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0), "label", e)
    _face(bm, uv, [(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0), "label", e)
    _face(bm, uv, [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], (-1, 0, 0), "label", e)
    _face(bm, uv, [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], (1, 0, 0), "label", e)


def build(name=NAME, collection=None):
    lb = DIMS["length"] - 2 * TAPE_T
    db = DIMS["depth"]
    hb = DIMS["height"] - TAPE_T
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    _body(bm, uv, lb, db, hb)
    _tape(bm, uv, lb, hb)
    _label(bm, uv, hb)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in materials():
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob
