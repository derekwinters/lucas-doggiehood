"""The dog builder of the shared kit (one skinned body), for the active breed (dh_breed).

Every shape, size, colour and name comes from the breed definition (<breed>/breed.py); this
module and the others in kit/ hold no breed's numbers.

The torso, neck, head, legs and tail are ONE continuous mesh, the "skin"
(<NAME>_body). It is not modelled directly: the round-2 lofted shapes (plus
the new hind leg: thigh/haunch, lower thigh, hock, foot, and a front leg with
an elbow and a wrist) become signed distance fields that are merged with a
smooth minimum, so every junction is a rounded fillet. The merged field is
meshed and retopologised with QuadriFlow (dh_dog_mesh.py, dh_dog_skin.py), the
round-2 mouth is cut into the result, then it is unwrapped and the fur is
baked onto it from 3D (dh_dog_tex.py), so the fur has no seams at the joins.

The skin is bound to an armature (dh_dog_rig.py: 32 bones) with smooth
weights, at most 4 per vertex. The ears are their own skinned meshes (2 bones
each). Eyes, nose, collar and tongue are rigid pieces parented to a bone.

Invariant - nothing a viewer can see is two meshes intersecting along a straight
line: body parts meet through the field's smooth minimum, and only the small
rigid accessories (eyes, nose, collar, tongue) and the ears are separate pieces.

Conventions (docs/engineering/blender-pipeline.md): 1 unit = 1 m, front -Y,
up +Z, dog's left = +X. Origin on the ground under the middle of the
nose-to-rump span. Bind pose = standing, mouth closed; the default ("rest")
pose opens the jaw 12 degrees, as in round 2.

build(coat=None, lod=False) -> dict part -> object (plus "armature"); coat None = the breed's first coat
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import dh_breed  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import dh_dog_tex as T  # noqa: E402

B = dh_breed.get()
ROOT = dh_breed.DIR             # the breed's folder: textures/ and exports/ are written there
TEX_DIR = os.path.join(ROOT, "textures")
NAME = B.NAME
DEFAULT_COAT = list(B.COATS)[0]     # the first coat is the default
X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
Z = Vector((0, 0, 1))

# --------------------------------------------------------------------------
# Joint pivots (metres, Blender axes). This is the future skeleton.
# --------------------------------------------------------------------------
HEAD_DY = B.HEAD_DY            # head group (head, jaw, ears, eyes, nose) moved back by this after building
OY = B.OY                      # authored coordinates are shifted by this so the origin is mid-span
JAW_REST_OPEN_DEG = B.JAW_REST_OPEN_DEG

# --------------------------------------------------------------------------
# Materials
# --------------------------------------------------------------------------
FUR, GLOSS, SOFT = 0, 1, 2
MAT_NAMES = list(B.MATERIAL_NAMES)
SHADING = B.MATERIAL_SHADING


def _srgb_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def _img(path):
    im = bpy.data.images.load(path, check_existing=True)
    return im


def make_materials(coat=None, tex_dir=None, lod=False):
    coat = coat or DEFAULT_COAT
    tex_dir = tex_dir or TEX_DIR
    pre = "dog_fur_lod1" if lod else "dog_fur"
    mats = []
    # fur: colour + normal
    m = bpy.data.materials.new(MAT_NAMES[0] % coat.capitalize())
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = _img(os.path.join(tex_dir, "%s_%s_color.png" % (pre, coat)))
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = _img(os.path.join(tex_dir, "%s_normal.png" % pre))
    tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 1.0
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    m.use_backface_culling = True
    bsdf.inputs["Roughness"].default_value = SHADING["fur"][0]
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = SHADING["fur"][1]
    mats.append(m)
    trim = os.path.join(tex_dir, "dog_trim_color.png")
    for name, (rough, spec) in ((MAT_NAMES[1], SHADING["gloss"]), (MAT_NAMES[2], SHADING["soft"])):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        tc = nt.nodes.new("ShaderNodeTexImage")
        tc.image = _img(trim)
        nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
        m.use_backface_culling = True
        bsdf.inputs["Roughness"].default_value = rough
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = spec
        mats.append(m)
    return mats


# --------------------------------------------------------------------------
# Loft
# --------------------------------------------------------------------------
class Ring:
    def __init__(self, c, ra, rt, rb=None, e=2.0, rside=None):
        self.c = Vector(c)
        self.ra = ra            # half width along the side axis
        self.rt = rt            # along +b
        self.rb = rt if rb is None else rb   # along -b
        self.e = e              # superellipse exponent (2 = ellipse, higher = boxier)


def _sp(x, e):
    return math.copysign(abs(x) ** (2.0 / e), x)


class Part:
    """Accumulates loft geometry for one object, in world coordinates."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _face(self, verts, uvs, mat):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = mat
        f.smooth = True
        for lp, uv in zip(f.loops, uvs):
            lp[self.uv].uv = uv
        return f

    def loft(self, rings, n, side, rect, mat=FUR, caps=(True, True), closed=False, dome=0.35, tangents=None):
        """Sweep superellipse rings along their centres.
        side: fixed side axis (vector); b = t x side, a = b x t.
        rect: (u0, v0, u1, v1) UV rectangle. U = around (seam at -b), V = along."""
        side = Vector(side).normalized()
        m = len(rings)
        cs = [r.c for r in rings]
        ts = []
        for i in range(m):
            if tangents:
                ts.append(Vector(tangents[i]).normalized())
                continue
            if closed:
                t = cs[(i + 1) % m] - cs[i - 1]
            elif i == 0:
                t = cs[1] - cs[0]
            elif i == m - 1:
                t = cs[-1] - cs[-2]
            else:
                t = (cs[i + 1] - cs[i]).normalized() + (cs[i] - cs[i - 1]).normalized()
            ts.append(t.normalized())
        # arc length for V
        s = [0.0]
        for i in range(1, m):
            s.append(s[-1] + (cs[i] - cs[i - 1]).length)
        if closed:
            total = s[-1] + (cs[0] - cs[-1]).length
        else:
            total = s[-1] if s[-1] > 0 else 1.0
        u0, v0, u1, v1 = rect
        grid = []
        for i, r in enumerate(rings):
            t = ts[i]
            b = t.cross(side).normalized()
            a = b.cross(t).normalized()
            row = []
            for k in range(n):
                th = 2 * math.pi * k / n - math.pi / 2
                cx, sy = math.cos(th), math.sin(th)
                rr = r.rt if sy > 0 else r.rb
                p = r.c + a * (r.ra * _sp(cx, r.e)) + b * (rr * _sp(sy, r.e))
                row.append(self.bm.verts.new(p))
            grid.append(row)

        def U(k):
            return u0 + (u1 - u0) * k / n

        def V(i):
            return v0 + (v1 - v0) * (s[i] / total if i < m else 1.0)

        rng_i = range(m if closed else m - 1)
        self.last_faces = []
        for i in rng_i:
            j = (i + 1) % m
            vi, vj = V(i), V(i + 1)
            row_f = []
            self.last_faces.append(row_f)
            for k in range(n):
                k2 = (k + 1) % n
                row_f.append(self._face([grid[i][k], grid[i][k2], grid[j][k2], grid[j][k]],
                           [(U(k), vi), (U(k + 1), vi), (U(k + 1), vj), (U(k), vj)], mat))
        if not closed:
            for end, (idx, sign) in enumerate(((0, -1), (m - 1, 1))):
                if not caps[end]:
                    continue
                r = rings[idx]
                cen = r.c + ts[idx] * sign * dome * min(r.ra, r.rt, r.rb)
                cv = self.bm.verts.new(cen)
                vv = V(idx)
                for k in range(n):
                    k2 = (k + 1) % n
                    self._face([grid[idx][k], grid[idx][k2], cv],
                               [(U(k), vv), (U(k + 1), vv), ((u0 + u1) / 2, vv)], mat)
        # consistent outward normals now, so callers can pick faces by normal
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.normal_update()
        return grid

    def ellipsoid(self, c, rx, ry, rz, n, m, axis, side, rect, mat, e=2.0):
        """Ellipsoid as a loft along `axis` (unit), rx across `side`, rz across the other."""
        axis = Vector(axis).normalized()
        rings = []
        for i in range(m):
            ph = math.pi * (i + 0.5) / m
            d = -math.cos(ph) * ry
            k = math.sin(ph)
            rings.append(Ring(Vector(c) + axis * d, rx * k, rz * k, rz * k, e=e))
        return self.loft(rings, n, side, rect, mat, tangents=[axis] * m, dome=0.6)

    def finish(self, pivot, mats, collection):
        bm = self.bm
        fixed = set(getattr(self, "fixed", []))
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in fixed])
        bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0, OY, 0)) - Vector(pivot))
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for mt in mats:
            me.materials.append(mt)
        ob = bpy.data.objects.new(self.name, me)
        ob.location = Vector(pivot)
        collection.objects.link(ob)
        return ob


def _rot_about(p, pivot, axis, deg):
    R = Matrix.Rotation(math.radians(deg), 3, axis)
    return Vector(pivot) + R @ (Vector(p) - Vector(pivot))


# --------------------------------------------------------------------------
# Parts
# --------------------------------------------------------------------------
# --------------------------------------------------------------------------
# Head + mouth (round 2)
# --------------------------------------------------------------------------
# The head is lofted as one closed shape that includes the lower jaw. The jaw
# is then cut out of it as a wedge whose edge is the hinge axis, so the upper
# and lower lip lines are the same cut and meet at the mouth corners, which
# sit on the hinge axis. Opening the jaw opens a wedge that is closed at the
# corners; side webs on the jaw (swept around the hinge, hidden inside the
# head when shut) close the wedge's sides, so the mouth interior only shows
# through the front opening.
#
# Invariant - the mouth corners lie on the jaw hinge axis, and no opening
# angle up to MOUTH_MAX_OPEN_DEG shows a gap at the sides of the mouth.
HINGE = tuple(B.HINGE)         # (y, z) of the hinge axis = the mouth corners, authored head coordinates
LIP_RUN = B.LIP_RUN            # the lip line runs this far forward from the corner ...
LIP_DROP = B.LIP_DROP          # ... and falls this much
LIP_BAND = B.LIP_BAND          # dark lip band either side of the lip line
MOUTH_MAX_OPEN_DEG = B.MOUTH_MAX_OPEN_DEG    # side webs cover openings up to this (the default pose + tests)


def _lip_plane():
    hy, hz = HINGE
    d = Vector((0, -LIP_RUN, -LIP_DROP)).normalized()     # forward along the lip line
    n = Vector((0, -d.z, d.y)) * -1                      # up, normal to the lip plane
    if n.z < 0:
        n = -n
    return Vector((0, hy, hz)), n.normalized()


def _sd(p, co, no):
    return (p - co).dot(no)


def _bisect(bm, faces, co, no):
    geom = list({e for f in faces for e in f.edges}) + list(faces) + list({v for f in faces for v in f.verts})
    if faces:
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=co, plane_no=no)


def tongue_part(q):
    """The tongue is its own part (child of the jaw) so a closed mouth can hide or
    retract it. It lies on the mouth floor and lolls over the lower lip."""
    p = Part("%s_tongue" % NAME)
    co, no = _lip_plane()
    hy, hz = HINGE

    def floor_z(y):
        return hz - (hy - y) * (LIP_DROP / LIP_RUN)    # the lip plane, closed

    tspec = list(B.TONGUE["rings"])
    if q["lod"]:
        tspec = [tspec[i] for i in B.TONGUE["lod_rings"]]
    rings = [Ring((0, y, floor_z(y) + dz), a, t, t, B.TONGUE["e"]) for y, dz, a, t in tspec]
    p.loft(rings, q["tongue_n"], X, _remap("tongue"), mat=SOFT, dome=0.6)
    return p


def _trim_uv(key, u=0.5, v=0.5):
    u0, v0, u1, v1 = T.TRIM[key]
    return (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)


def _remap(rect_key):
    u0, v0, u1, v1 = T.TRIM[rect_key]
    pad = 0.08
    return (u0 + (u1 - u0) * pad, v0 + (v1 - v0) * pad, u1 - (u1 - u0) * pad, v1 - (v1 - v0) * pad)


def ear_part(q, sx):
    p = Part("%s_ear_%s" % (NAME, "L" if sx > 0 else "R"))
    R = T.ISLANDS["ear"][0]
    # root on the upper side of the skull, hanging beside the cheek
    spec = list(B.EARS["rings"])
    if q["lod"]:
        spec = [spec[i] for i in B.EARS["lod_rings"]]
    e, dome = B.LOFT_STYLE["ear"]
    rings = [Ring((sx * x, y, z), w, t, t, e) for x, y, z, w, t in spec]
    # side axis = forward (width of the ear runs front-to-back)
    p.loft(rings, q["ear_n"], Y, R, dome=dome)
    return p


def eye_part(q, sx, head_obj_bvh):
    p = Part("%s_eye_%s" % (NAME, "L" if sx > 0 else "R"))
    # sit the eye on the face: ray from the front onto the head surface
    E = B.EYES
    o = Vector((sx * E["ray_x"], -1.5, E["ray_z"]))
    d = Y
    hit, nrm, _, _ = head_obj_bvh.ray_cast(o, d)
    nrm = (nrm + Vector((0, -E["face_bias"], 0))).normalized()
    c = hit - nrm * E["inset"]
    p.ellipsoid(c, *E["radii"], q["eye_n"], q["eye_m"], -nrm, Z, _remap("eye"), GLOSS)
    # catch-light: a small white bead up and toward the outside of the eye
    up = Z - nrm * Z.dot(nrm)
    outward = X * sx - nrm * (X * sx).dot(nrm)
    so = E["shine_offset"]
    hc = c + nrm * so[0] + up.normalized() * so[1] + outward.normalized() * so[2]
    p.ellipsoid(hc, *E["shine_radii"], q["shine_n"], q["shine_m"], -nrm, Z, _remap("shine"), GLOSS)
    return p, c


def nose_part(q, head_bvh):
    p = Part("%s_nose" % NAME)
    N = B.NOSE
    hit, nrm, _, _ = head_bvh.ray_cast(Vector((0, -1.5, N["ray_z"])), Y)
    c = hit + Vector((0, N["offset"][0], N["offset"][1]))
    # rounded, slightly triangular leather: wider at the top
    rings = []
    m = q["nose_m"]
    ra, rt, rb = N["radii"]
    for i in range(m):
        ph = math.pi * (i + 0.5) / m
        k = math.sin(ph)
        rings.append(Ring(c + Vector((0, -math.cos(ph) * N["length"], 0)), ra * k, rt * k, rb * k, N["e"]))
    p.loft(rings, q["nose_n"], X, _remap("nose"), GLOSS, tangents=[-Y] * m, dome=0.6)
    return p, c


QUALITY = {
    False: dict(lod=False, body_n=18, head_n=16, web_steps=3, tongue_n=8, ear_n=8, eye_n=10, eye_m=5,
                shine_n=6, shine_m=3, nose_n=10, nose_m=4, collar_seg=16, collar_x=4, tag_n=8, tail_n=8,
                leg_n=10, paw_m=5),
    True: dict(lod=True, body_n=10, head_n=10, web_steps=2, tongue_n=6, ear_n=6, eye_n=8, eye_m=3,
               shine_n=4, shine_m=2, nose_n=6, nose_m=3, collar_seg=10, collar_x=4, tag_n=5, tail_n=6,
               leg_n=7, paw_m=3),
}


def _drop_unused_slots(ob):
    used = sorted({p.material_index for p in ob.data.polygons})
    mats = [ob.data.materials[i] for i in used]
    remap = {old: new for new, old in enumerate(used)}
    idx = [remap[p.material_index] for p in ob.data.polygons]
    ob.data.materials.clear()          # (this resets the face indices, so remap after)
    for m in mats:
        ob.data.materials.append(m)
    ob.data.polygons.foreach_set("material_index", idx)
    ob.data.update()


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)




QUALITY[False].update(skin_faces=B.SKIN_FACES)
QUALITY[True].update(skin_faces=B.SKIN_FACES, skin_tris=B.LOD_SKIN_TRIS)    # LOD: the full retopology, decimated
TEX_SIZE = {False: 1024, True: 512}
ACCESSORY_BONE = {"eye_L": "head", "eye_R": "head", "nose": "head", "collar": "neck", "tongue": "jaw"}
REGION_OF = {"torso": "body", "head": "head", "tail": "tail"}
for _s in "LR":
    REGION_OF.update({"front_upper_" + _s: "fleg", "front_fore_" + _s: "fleg", "front_pastern_" + _s: "fleg",
                      "front_paw_" + _s: "paw", "back_thigh_" + _s: "thigh", "back_gaskin_" + _s: "bleg",
                      "back_meta_" + _s: "bleg", "back_paw_" + _s: "paw"})


def collar_part(q, bvh):
    """A strap that hugs the actual skin around the collar loop, plus a tag at the throat."""
    import dh_dog_shape as S
    p = Part("%s_collar" % NAME)
    C = B.COLLAR
    rows = S.TORSO
    i = C["ring"]
    c = Vector((0, rows[i][0], rows[i][1]))
    tilt = (Vector((0, rows[i + 1][0], rows[i + 1][1])) - Vector((0, rows[i - 1][0], rows[i - 1][1]))).normalized()
    b = tilt.cross(X).normalized()
    n = q["collar_seg"]
    rings, tangents = [], []
    surf = []
    for k in range(n):
        th = 2 * math.pi * k / n
        d = (X * math.cos(th) + b * math.sin(th)).normalized()
        hit = bvh.ray_cast(c + d * 0.6, -d)[0]
        if hit is None:
            hit = c + d * 0.2
        surf.append(hit)
    for k in range(n):
        th = 2 * math.pi * k / n
        d = (X * math.cos(th) + b * math.sin(th)).normalized()
        rings.append(Ring(surf[k] + d * (C["gap_lod"] if q["lod"] else C["gap"]), C["strap"][0], C["strap"][1],
                          C["strap"][1], C["e"]))
        tangents.append((surf[(k + 1) % n] - surf[k - 1]).normalized())
    p.loft(rings, q["collar_x"], tilt, _remap("collar"), SOFT, closed=True, tangents=tangents)
    th = -math.pi / 2
    d = (X * math.cos(th) + b * math.sin(th)).normalized()
    hit = bvh.ray_cast(c + d * 0.6, -d)[0] or (c + d * 0.2)
    throat = hit + d * C["tag_out"] + Vector((0, 0, -C["tag_drop"]))
    tr = C["tag_radii"]
    p.ellipsoid(throat, tr[0], tr[1], tr[0], q["tag_n"], 3, d, X, _remap("tag"), GLOSS, e=2.0)
    return p, c


def _tris_for_bake(ob, attrs, offset, fur_only=True):
    """Fur triangles of a mesh object (bind pose) for dh_dog_tex.bake_fur."""
    me = ob.data
    me.calc_loop_triangles()
    mw = ob.matrix_world
    co = np.array([(mw @ v.co)[:] for v in me.vertices]) - np.asarray(offset)
    no = np.array([(mw.to_3x3() @ v.normal).normalized()[:] for v in me.vertices])
    uvl = me.uv_layers.active.data
    P, N, UV, A = [], [], [], []
    for t in me.loop_triangles:
        if fur_only and me.polygons[t.polygon_index].material_index != FUR:
            continue
        vi = list(t.vertices)
        P.append(co[vi])
        N.append(no[vi])
        UV.append([uvl[li].uv[:] for li in t.loops])
        A.append(attrs[vi])
    return {"P": np.array(P), "N": np.array(N), "UV": np.array(UV), "A": np.array(A)}


def build(coat=None, lod=False, tex_dir=None, collection=None, bake=False, pose="rest", materials=True):
    """Build the dog. Returns {part: mesh object}; build.armature is the armature.
    bake=True (re)bakes this quality's fur textures into tex_dir first. materials=False builds
    without materials (no textures needed: solve_sit.py runs before a new breed's first export)."""
    from mathutils.bvhtree import BVHTree
    import dh_dog_rig as RG
    import dh_dog_skin as K
    q = QUALITY[bool(lod)]
    col = collection or bpy.context.scene.collection
    tex_dir = tex_dir or TEX_DIR
    sfx = "_lod1" if lod else ""
    shift = Vector((0, OY, 0))

    skin = K.skin_part(q)
    jl = skin.bm.verts.layers.int["jaw"]
    skin.bm.verts.ensure_lookup_table()
    jawflag = np.array([v[jl] for v in skin.bm.verts])
    bvh = BVHTree.FromBMesh(skin.bm)
    hy, hz = K.hinge()
    builders = {"body": (skin, (0, -OY, 0))}
    tongue = tongue_part(q)
    builders["tongue"] = (tongue, (0, B.TONGUE["pivot"][0] + HEAD_DY, B.TONGUE["pivot"][1]))
    ex, ey, ez = B.EARS["bones"][0]
    for sx, s in ((1, "L"), (-1, "R")):
        e = ear_part(q, sx)
        builders["ear_" + s] = (e, (sx * ex, ey + HEAD_DY, ez))
        ep, ec = eye_part(q, sx, bvh)
        builders["eye_" + s] = (ep, tuple(round(v, 4) for v in ec))
    for key in ("tongue", "ear_L", "ear_R"):
        bm = builders[key][0].bm
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, HEAD_DY, 0))
    nose, nose_c = nose_part(q, bvh)
    builders["nose"] = (nose, tuple(round(v, 4) for v in nose_c))
    collar, collar_c = collar_part(q, bvh)
    builders["collar"] = (collar, tuple(collar_c))
    parts, pivots = {}, {}
    for key, (pb, piv) in builders.items():
        pb.name = pb.name + sfx
        pv = tuple(round(v, 4) for v in (Vector(piv) + shift))
        pivots[key] = pv
        parts[key] = pb.finish(pv, [], col)
    K.unwrap(parts["body"], T.SKIN_U)
    # armature + weights (bind pose = as built, mouth closed)
    arm = RG.make_armature("%s_armature%s" % (NAME, sfx), col)
    me = parts["body"].data
    co = np.array([v.co[:] for v in me.vertices])          # object = world (origin at 0)
    edges = np.array([e.vertices[:] for e in me.edges])
    W, memb = RG.skin_weights(co - np.array(shift), jawflag, edges)
    RG.bind(parts["body"], arm, W)
    for s in "LR":
        ob = parts["ear_" + s]
        eco = np.array([(ob.matrix_world @ v.co)[:] for v in ob.data.vertices]) - np.array(shift)
        RG.bind(ob, arm, RG.ear_weights(eco, s))
        ob.matrix_parent_inverse = Matrix.Identity(4)
        ob.location = Vector(pivots["ear_" + s])
    parts["body"].matrix_parent_inverse = Matrix.Identity(4)
    for key, bone in ACCESSORY_BONE.items():
        RG.attach(parts[key], arm, bone)
    bpy.context.view_layer.update()
    if bake:
        attrs = np.zeros((len(co), len(T.ATTRS)))
        for v, mm in memb.items():
            attrs[:, T.ATTRS.index(REGION_OF[v])] += mm
        tris = _tris_for_bake(parts["body"], attrs, shift)
        eob = parts["ear_L"]
        ea = np.zeros((len(eob.data.vertices), len(T.ATTRS)))
        ea[:, T.ATTRS.index("ear")] = 1
        et = _tris_for_bake(eob, ea, shift)
        tris = {k: np.concatenate([tris[k], et[k]]) for k in tris}
        build.coverage = T.bake_fur(tex_dir, "dog_fur_lod1" if lod else "dog_fur", TEX_SIZE[bool(lod)], tris)
    if materials:
        mats = make_materials(coat, tex_dir, lod)
        for ob in parts.values():
            for m in mats:
                ob.data.materials.append(m)
            _drop_unused_slots(ob)
    build.armature = arm
    build.pivots = pivots
    build.weights = W
    RG.apply_pose(arm, parts, pose)
    return parts
