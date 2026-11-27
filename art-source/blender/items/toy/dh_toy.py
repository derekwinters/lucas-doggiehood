"""The dog toy (#332): a squeaky plush duck, built from named dimensions.

One mesh, `toy_duck`, made of sewn-on plush parts (each a deformed UV sphere):
a squat body whose back tapers up into a tail point, a round head, a flat
flared bill, two wings, two webbed feet peeking out in front, and button eyes.
The plush parts share one tileable fleece texture pair (dh_toy_tex.py), tinted
per material; the eyes are plain glossy colour.

Conventions (docs/engineering/blender-pipeline.md, same as the street props):
1 unit = 1 m at in-game size, front (the bill) faces -Y, up is +Z, and the
pivot is on the ground at the centre of the footprint.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

NAME = "toy_duck"
TRI_BUDGET = 6000          # provisional per-mesh backstop of the art budget (#799)
FLEECE_M = 0.12            # metres of fabric per repeat of the fleece texture

# Colours (sRGB hex). Blue and orange: neither is the lawn's green, the
# sidewalk's light grey, the tennis ball's yellow-green (#333) nor the red of
# the lost-item finder ring (#602), so the toy stands out on grass and paving.
COLOURS = {
    "plush_blue": "#2C6BDB",     # body and head
    "plush_navy": "#1E4596",     # wings
    "plush_orange": "#FF8A1C",   # bill and feet
    "eye_white": "#F6F3EC",
    "eye_black": "#141414",
}
ROUGHNESS = {"plush": 0.95, "eye": 0.25}
SPECULAR_LEVEL = 0.2

# Every part: (key, material, centre, radii, u_segments, v_segments).
# Metres in final position before the footprint is re-centred on the origin.
BODY = dict(c=(0.0, 0.03, 0.10), r=(0.150, 0.200, 0.120), seg=(24, 12))
BODY_FLAT_Z = 0.0          # the body's underside is cut flat here, so the toy sits on the ground
TAIL_LIFT = 0.120          # how far the back of the body rises into the tail point
TAIL_PINCH = 0.60          # how much the back narrows into the tail point (share of the width)
HEAD = dict(c=(0.0, -0.12, 0.29), r=(0.105, 0.100, 0.105), seg=(20, 10))
BILL = dict(c=(0.0, -0.235, 0.262), r=(0.070, 0.075, 0.028), seg=(12, 6))
BILL_FLARE = 0.30          # the bill widens this much towards its tip
BILL_UPTURN = 0.012        # and its tip turns up this far
WING = dict(c=(0.138, 0.06, 0.15), r=(0.028, 0.105, 0.060), seg=(12, 6))
WING_TILT_DEG = -14.0      # about X: the wing's back end rises
WING_SPLAY_DEG = 6.0       # about Z: the wing's back end leans out
FOOT = dict(c=(0.060, -0.160, 0.018), r=(0.050, 0.070, 0.018), seg=(10, 5))
FOOT_SPLAY_DEG = 14.0      # feet point a little outwards
EYE_WHITE = dict(c=(0.052, -0.205, 0.330), r=(0.026, 0.026, 0.026), seg=(8, 5))
PUPIL = dict(c=(0.058, -0.222, 0.335), r=(0.015, 0.015, 0.015), seg=(6, 4))


def _tail(co):
    """Body deformation: the back half lifts and narrows into a point; the underside is flat."""
    cy, ry = BODY["c"][1], BODY["r"][1]
    t = max(0.0, (co.y - cy) / ry)
    co.z += TAIL_LIFT * t * t
    co.x *= 1.0 - TAIL_PINCH * t * t
    co.z = max(co.z, BODY_FLAT_Z)


def _bill(co):
    cy, ry = BILL["c"][1], BILL["r"][1]
    t = max(0.0, (cy - co.y) / ry)
    co.x *= 1.0 + BILL_FLARE * t
    co.z += BILL_UPTURN * t * t


def _rot(deg_x=0.0, deg_z=0.0):
    return Matrix.Rotation(math.radians(deg_z), 3, "Z") @ Matrix.Rotation(math.radians(deg_x), 3, "X")


def _parts():
    """(material, spec, rotation, deform) for every part, mirrored pairs expanded."""
    parts = [("plush_blue", BODY, None, _tail), ("plush_blue", HEAD, None, None),
             ("plush_orange", BILL, None, _bill)]
    for side in (1, -1):
        parts += [("plush_navy", _mirror(WING, side), _rot(WING_TILT_DEG, side * WING_SPLAY_DEG), None),
                  ("plush_orange", _mirror(FOOT, side), _rot(0.0, -side * FOOT_SPLAY_DEG), None),
                  ("eye_white", _mirror(EYE_WHITE, side), None, None),
                  ("eye_black", _mirror(PUPIL, side), None, None)]
    return parts


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


def _plain_material(name, hex_):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = _hex_lin(hex_)
    bsdf.inputs["Roughness"].default_value = ROUGHNESS["eye"]
    mat.diffuse_color = _hex_lin(hex_)
    mat.use_backface_culling = True   # closed parts: exported single-sided
    return mat


def material_name(key):
    return "DH_Toy_" + key


def build(tex, collection=None):
    """Build the toy; tex = {"color": path, "normal": path} from dh_toy_tex.write(). Returns the object."""
    keys = list(COLOURS)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for mat_key, spec, rot, deform in _parts():
        mi = keys.index(mat_key)
        r, c = Vector(spec["r"]), Vector(spec["c"])
        rot = rot or Matrix.Identity(3)
        nu, nv = spec["seg"]
        ru, rv = _uv_repeats(spec["r"])

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

        def tri(vs, uvs):
            f = bm.faces.new(vs)
            f.material_index = mi
            for loop, (u, w) in zip(f.loops, uvs):
                loop[uv].uv = (u * ru, w * rv)

        for j in range(nu):
            k = (j + 1) % nu
            u0, u1, um = j / nu, (j + 1) / nu, (j + 0.5) / nu
            tri((top, rings[0][j], rings[0][k]), ((um, 1.0), (u0, 1 - 1 / nv), (u1, 1 - 1 / nv)))
            for i in range(nv - 2):
                a, b = rings[i], rings[i + 1]
                w0, w1 = 1 - (i + 1) / nv, 1 - (i + 2) / nv
                tri((a[j], b[j], b[k]), ((u0, w0), (u0, w1), (u1, w1)))
                tri((a[j], b[k], a[k]), ((u0, w0), (u1, w1), (u1, w0)))
            tri((rings[-1][j], bottom, rings[-1][k]), ((u0, 1 / nv), (um, 0.0), (u1, 1 / nv)))
    # pivot: on the ground at the centre of the footprint
    xs, ys, zs = [v.co.x for v in bm.verts], [v.co.y for v in bm.verts], [v.co.z for v in bm.verts]
    shift = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    for v in bm.verts:
        v.co -= shift
    me = bpy.data.meshes.new(NAME)
    bm.to_mesh(me)
    bm.free()
    for k in keys:
        name = material_name(k)
        me.materials.append(_plush_material(name, COLOURS[k], tex) if k.startswith("plush")
                            else _plain_material(name, COLOURS[k]))
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(NAME, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

