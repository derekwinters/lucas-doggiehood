"""The continuous skin (round 3): field -> mesh -> retopology -> mouth cut.

skin_part(q) returns a dh_dog.Part holding the skin bmesh in authored
coordinates (before the OY shift), with:
  - fur faces (material FUR) and mouth / lip faces (material SOFT, trim UVs)
  - an int vertex layer "jaw": 1 = rigidly on the jaw (lower lip, mouth floor,
    side webs), 2 = upper lip (never on the jaw), 0 = everything else
  - part.fixed: the double-sided web faces, whose normals are set by hand

The mouth is the round-2 mouth on the continuous mesh: the skin is cut open
along the lip line (the lip plane in front of the hinge), from one mouth
corner to the other. The corners sit on the hinge axis and are not split, so
the upper and lower lips always meet there. The roof and floor of the mouth are
caps on the lip plane that share the hinge chord between the corners; side
webs on the jaw close the sides when it opens. The lower jaw is no longer a
separate piece: it is part of the skin, weighted to the jaw bone, and the
skin behind the mouth corners and under the throat stretches with it.

Invariant - the mouth corners lie on the jaw hinge axis, and the lip cut is the
only opening in the skin.
"""
import math

import bmesh
import bpy
import numpy as np
from mathutils import Vector

import dh_dog as D
import dh_dog_mesh as M
import dh_dog_shape as S

_G = D.B.FIELD_GRID
GRID_H = _G["h"]                  # field grid spacing (m)
ORIGIN = np.array(list(_G["origin"]))
EXTENT = tuple(_G["extent"])
VOXEL = _G["voxel"]               # manifold clean-up voxel size (m)
ZONE = D.B.MOUTH_ZONE             # the lip cut stays within this distance below / behind the hinge
_cache = {}


def hinge():
    return D.HINGE[0] + D.HEAD_DY, D.HINGE[1]


def lip_plane():
    hy, hz = hinge()
    d = Vector((0, -D.LIP_RUN, -D.LIP_DROP)).normalized()
    n = Vector((0, d.z, -d.y))
    if n.z < 0:
        n = -n
    return Vector((0, hy, hz)), n.normalized()


def field():
    """The merged signed distance field (cached per process; deterministic)."""
    if "field" not in _cache:
        shape = tuple(int(math.ceil(s / GRID_H)) + 1 for s in EXTENT)
        vols = S.volumes()
        fields = {k: M.part_sdf(p.bm, ORIGIN, GRID_H, shape, 0.08) for k, p in vols.items()}
        F = None
        for _g, kg, items in S.FIELD:
            G = None
            for name, k in items:
                G = fields[name] if G is None else M.smin(G, fields[name], k)
            F = G if F is None else M.smin(F, G, kg)
        for p in vols.values():
            p.bm.free()
        _cache["field"] = F
    return _cache["field"]


def retopo(target_faces):
    """Quad mesh of the field's surface, ~target_faces quads. Returns (verts, faces)."""
    key = ("retopo", target_faces)
    if key not in _cache:
        v, q = M.surface_nets(field(), ORIGIN, GRID_H)
        v, q = M.largest_component(np.asarray(v), q)
        col = bpy.context.scene.collection
        ob = M.mesh_from("_skin_tmp", v, q, col)
        M.taubin(ob, 10)
        M.voxel_clean(ob, VOXEL)
        M.taubin(ob, 4)
        M.quadriflow(ob, target_faces)
        M.taubin(ob, 2, lam=0.4, mu=-0.42)
        me = ob.data
        verts = np.array([vv.co[:] for vv in me.vertices])
        faces = [list(p.vertices) for p in me.polygons]
        verts[:, 2] -= verts[:, 2].min()          # paws exactly on the ground
        bpy.data.objects.remove(ob)
        bpy.data.meshes.remove(me)
        _cache[key] = (verts, faces)
    return _cache[key]


def decimated(target_faces, tris):
    """The full retopology collapsed (quadric decimation, mirror-symmetric) to ~tris
    triangles. Used for the LOD: QuadriFlow at very low counts can drop thin parts
    such as the tail tip, decimating the full mesh keeps the silhouette."""
    key = ("dec", target_faces, tris)
    if key not in _cache:
        verts, faces = retopo(target_faces)
        ob = M.mesh_from("_skin_lod_tmp", verts, faces, bpy.context.scene.collection)
        md = ob.modifiers.new("dec", "DECIMATE")
        md.decimate_type = "COLLAPSE"
        md.ratio = tris / float(sum(len(f) - 2 for f in faces))
        md.use_symmetry = True
        md.symmetry_axis = "X"
        md.use_collapse_triangulate = True
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier=md.name)
        me = ob.data
        v = np.array([vv.co[:] for vv in me.vertices])
        f = [list(p.vertices) for p in me.polygons]
        v[:, 2] -= v[:, 2].min()
        bpy.data.objects.remove(ob)
        bpy.data.meshes.remove(me)
        _cache[key] = (v, f)
    return _cache[key]


def _bisect_faces(bm, pred, co, no):
    bm.faces.ensure_lookup_table()
    D._bisect(bm, [f for f in bm.faces if pred(f.calc_center_median())], co, no)


def skin_part(q):
    if q.get("skin_tris"):
        verts, faces = decimated(q["skin_faces"], q["skin_tris"])
    else:
        verts, faces = retopo(q["skin_faces"])
    part = D.Part("%s_body" % D.NAME)
    bm = part.bm
    bv = [bm.verts.new(v) for v in verts]
    for f in faces:
        try:
            ff = bm.faces.new([bv[i] for i in f])
        except ValueError:
            continue
        ff.material_index = D.FUR
        ff.smooth = True
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mouth_cut(part, q)
    return part


def _walk(edges, start):
    adj = {}
    for e in edges:
        a, b = e.verts
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    path, prev = [start], None
    while True:
        nxt = [v for v in adj[path[-1]] if v is not prev]
        if not nxt:
            break
        prev = path[-1]
        path.append(nxt[0])
        if len(path) > len(edges) + 1:
            raise RuntimeError("lip line is not a simple path")
    return path


def mouth_cut(part, q):
    bm, uv = part.bm, part.uv
    jl = bm.verts.layers.int.new("jaw")
    hy, hz = hinge()
    co, no = lip_plane()

    def sd(p):
        return (p - co).dot(no)

    def head_zone(c):
        return c.z > hz - ZONE["below_hinge"] and c.y < hy + ZONE["behind_hinge"]

    # 1. the lip cut, then a cut across it at the hinge (makes the corner vertices)
    _bisect_faces(bm, lambda c: c.y < hy + 0.06 and head_zone(c), co, no)
    bm.faces.ensure_lookup_table()
    D._bisect(bm, [f for f in bm.faces if head_zone(f.calc_center_median())
                   and abs(sd(f.calc_center_median())) < 0.03
                   and min(v.co.y for v in f.verts) < hy < max(v.co.y for v in f.verts)], co, Vector((0, 1, 0)))
    # 2. dark lip bands either side of the lip line
    _bisect_faces(bm, lambda c: c.y < hy and head_zone(c) and 0 < sd(c) < 0.06, co + no * D.LIP_BAND, no)
    _bisect_faces(bm, lambda c: c.y < hy and head_zone(c) and -0.06 < sd(c) < 0, co - no * D.LIP_BAND, no)
    eps = 1e-5

    def on_lip(v):
        return abs(sd(v.co)) < eps and v.co.y <= hy + eps and v.co.z > hz - ZONE["below_hinge"]

    corners = [v for v in bm.verts if on_lip(v) and abs(v.co.y - hy) < eps]
    if len(corners) != 2:
        raise RuntimeError("expected 2 mouth corners, found %d" % len(corners))
    lip_edges = [e for e in bm.edges if on_lip(e.verts[0]) and on_lip(e.verts[1])]
    path = _walk(lip_edges, corners[0])
    if path[-1] is not corners[1] or len(path) != len(lip_edges) + 1:
        raise RuntimeError("lip line does not run corner to corner")
    for f in bm.faces:
        c = f.calc_center_median()
        if c.y < hy - 0.004 and abs(sd(c)) < D.LIP_BAND and head_zone(c):
            f.material_index = D.SOFT
            for lp in f.loops:
                lp[uv].uv = D._trim_uv("lip")
    # 3. open the lips: split the lip line (the corners stay shared)
    bmesh.ops.split_edges(bm, edges=lip_edges)
    bnd = [e for e in bm.edges if e.is_boundary]
    up = [e for e in bnd if sd(e.link_faces[0].calc_center_median()) > 0]
    lo = [e for e in bnd if sd(e.link_faces[0].calc_center_median()) < 0]
    if len(up) + len(lo) != len(bnd) or len(up) != len(lo):
        raise RuntimeError("lip split: %d upper / %d lower edges of %d" % (len(up), len(lo), len(bnd)))
    up_path, lo_path = _walk(up, corners[0]), _walk(lo, corners[0])
    for v in up_path[1:-1]:
        v[jl] = 2
    for v in lo_path[1:-1]:
        v[jl] = 1
    # 4. roof (head) and floor (jaw) of the mouth, sharing the hinge chord
    caps = []
    for path_ in (up_path, lo_path):
        f = bm.faces.new(path_)
        f.material_index = D.SOFT
        caps.append(f)
    res = bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="BEAUTY")
    for f in res["faces"]:
        f.smooth = False
        for lp in f.loops:
            lp[uv].uv = D._trim_uv("mouth")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # 5. side webs on the jaw (as round 2): lower-lip line swept up around the hinge
    _webs(part, [v.co.copy() for v in lo_path], co, no, hy, hz, q, jl)
    bm.normal_update()


def _webs(part, lip, co, no, hy, hz, q, jl):
    bm, uv = part.bm, part.uv
    wmax = max(abs(p.x) for p in lip)
    piv = Vector((0, hy, hz))
    steps = q["web_steps"]
    part.fixed = []
    for sx in (1, -1):
        side = sorted([p for p in lip if p.x * sx > 0.55 * wmax or (abs(p.y - hy) < 1e-4 and p.x * sx > 0)],
                      key=lambda p: -p.y)
        if len(side) < 2:
            continue
        pts = [side[0]] + [Vector((p.x * 0.975, p.y, p.z)) for p in side[1:]]
        rows = []
        for k in range(steps + 1):
            ang = -D.MOUTH_MAX_OPEN_DEG * k / steps
            row = []
            for i, p in enumerate(pts):
                if i == 0 and k > 0:
                    row.append(None)
                    continue
                v = bm.verts.new(D._rot_about(p, piv, D.X, ang))
                v[jl] = 1
                row.append(v)
            rows.append(row)
        for k in range(steps):
            for i in range(len(pts) - 1):
                a0 = rows[k][i] if i > 0 else rows[0][0]
                a1 = rows[k + 1][i] if i > 0 else rows[0][0]
                b0, b1 = rows[k][i + 1], rows[k + 1][i + 1]
                quad = [a0, b0, b1, a1] if i > 0 else [a0, b0, b1]
                for layer, key in ((0, "lip"), (1, "mouth")):
                    vs = quad if layer == 0 else list(reversed(quad))
                    try:
                        f = bm.faces.new(vs)
                    except ValueError:
                        continue
                    f.material_index = D.SOFT
                    f.smooth = False
                    f.normal_update()
                    want = sx if layer == 0 else -sx
                    if f.normal.x * want < 0:
                        f.normal_flip()
                    for lp in f.loops:
                        lp[uv].uv = D._trim_uv(key)
                    part.fixed.append(f)


def unwrap(ob, u_scale):
    """Smart-project the fur faces into [0, u_scale] x [0, 1] (the ears get the rest)."""
    me = ob.data
    vl = bpy.context.view_layer
    for o in list(bpy.context.selected_objects):     # edit mode must hold only this object
        o.select_set(False)
    vl.objects.active = ob
    ob.select_set(True)
    with bpy.context.temp_override(active_object=ob, object=ob, selected_objects=[ob],
                                   selected_editable_objects=[ob]):
        bpy.ops.object.mode_set(mode="EDIT")
        bm = bmesh.from_edit_mesh(me)
        for f in bm.faces:
            f.select_set(f.material_index == D.FUR)
        bmesh.update_edit_mesh(me)
        bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=0.012, area_weight=0.0,
                                 correct_aspect=True, scale_to_bounds=False)
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(udim_source="CLOSEST_UDIM", rotate=True, margin_method="FRACTION",
                                margin=0.008, shape_method="CONCAVE")
        bpy.ops.object.mode_set(mode="OBJECT")
    uvl = me.uv_layers.active.data
    for p in me.polygons:
        if p.material_index != D.FUR:
            continue
        for li in p.loop_indices:
            u, v = uvl[li].uv
            uvl[li].uv = (u * u_scale, v)
