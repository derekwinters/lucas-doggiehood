"""Armature, skin weights and static test poses for the active breed (positions from its breed.py).

32 bones (authored coordinates from the breed; the armature adds OY):
  root -> hips -> spine_1 -> spine_2 -> spine_3 -> neck -> head -> jaw, ear_<s>_1 -> ear_<s>_2
  hips -> tail_1 -> tail_2 -> tail_3 -> tail_4
  hips -> back_hip_<s> (thigh) -> back_stifle_<s> (lower thigh) -> back_hock_<s> (rear foot) -> back_foot_<s> (paw)
  spine_3 -> front_shoulder_<s> (upper arm) -> front_elbow_<s> (forearm) -> front_wrist_<s> (pastern)
             -> front_paw_<s> (paw)
Each bone's head is its joint pivot. Bone rolls are 0.

Weights: every skin vertex gets a soft membership in the field volumes it was
blended from (exp(-(d - d_min) / TAU) of its distance to each volume), each
volume maps to one bone or to a bone chain (torso -> hips..neck, tail ->
tail_1..4, by position along the chain), the jaw takes the head's share below
the lip plane, the weights are smoothed over the mesh, and the 4 largest are
kept and normalised.

Invariant - every skin vertex has 1 to 4 bone weights that sum to 1, and the
lower lip / mouth floor / side webs move rigidly with the jaw.
"""
import math

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

import dh_dog as D
import dh_dog_shape as S
import dh_dog_skin as K

TAU = 0.02
MAX_INF = 4
HDY = D.HEAD_DY
J = S.JOINTS
B = D.B
SP = B.SPINE


def _leg(prefix, names, pts, x, parent):
    out = []
    for i, n in enumerate(names):
        out.append(("%s_%s" % (prefix, n), (x,) + pts[i], (x,) + pts[i + 1], parent))
        parent = "%s_%s" % (prefix, n)
    return out


def bone_table():
    """[(name, head, tail, parent)] in authored coordinates."""
    hy, hz = K.hinge()

    def ax(yz, head_group=False):
        return (0, yz[0] + HDY, yz[1]) if head_group else (0, yz[0], yz[1])
    tb = B.TAIL_BONES
    b = [
        ("root", (0, -D.OY, 0.0), (0, -D.OY, 0.30), None),
        ("hips", ax(SP["hips"][0]), ax(SP["hips"][1]), "root"),
        ("spine_1", ax(SP["spine_1"][0]), ax(SP["spine_1"][1]), "hips"),
        ("spine_2", ax(SP["spine_2"][0]), ax(SP["spine_2"][1]), "spine_1"),
        ("spine_3", ax(SP["spine_3"][0]), ax(SP["spine_3"][1]), "spine_2"),
        ("neck", ax(SP["neck"][0]), ax(SP["neck"][1], True), "spine_3"),
        ("head", ax(SP["head"][0], True), ax(SP["head"][1], True), "neck"),
        ("jaw", (0, hy, hz), ax(B.JAW_TIP, True), "head"),
        ("tail_1", ax(tb[0]), ax(tb[1]), "hips"),
        ("tail_2", ax(tb[1]), ax(tb[2]), "tail_1"),
        ("tail_3", ax(tb[2]), ax(tb[3]), "tail_2"),
        ("tail_4", ax(tb[3]), ax(tb[4]), "tail_3"),
    ]
    e0, e1, e2 = B.EARS["bones"]
    for sx, s in ((1, "L"), (-1, "R")):
        b += [("ear_%s_1" % s, (sx * e0[0], e0[1] + HDY, e0[2]), (sx * e1[0], e1[1] + HDY, e1[2]), "head"),
              ("ear_%s_2" % s, (sx * e1[0], e1[1] + HDY, e1[2]), (sx * e2[0], e2[1] + HDY, e2[2]), "ear_%s_1" % s)]
        fpts = [J["front_shoulder"], J["front_elbow"], J["front_wrist"], J["front_paw"], J["front_toe"]]
        b += [("front_%s_%s" % (n, s), (sx * S.FRONT_X,) + fpts[i], (sx * S.FRONT_X,) + fpts[i + 1],
               "spine_3" if i == 0 else "front_%s_%s" % (("shoulder", "elbow", "wrist")[i - 1], s))
              for i, n in enumerate(("shoulder", "elbow", "wrist", "paw"))]
        bpts = [J["back_hip"], J["back_stifle"], J["back_hock"], J["back_foot"], J["back_toe"]]
        b += [("back_%s_%s" % (n, s), (sx * S.BACK_X,) + bpts[i], (sx * S.BACK_X,) + bpts[i + 1],
               "hips" if i == 0 else "back_%s_%s" % (("hip", "stifle", "hock")[i - 1], s))
              for i, n in enumerate(("hip", "stifle", "hock", "foot"))]
    return b


BONE_NAMES = [b[0] for b in bone_table()]

# volume -> bone (legs) ; torso / tail / head handled as chains below
VOLUME_BONE = {}
for _s in "LR":
    VOLUME_BONE.update({
        "front_upper_" + _s: "front_shoulder_" + _s, "front_fore_" + _s: "front_elbow_" + _s,
        "front_pastern_" + _s: "front_wrist_" + _s, "front_paw_" + _s: "front_paw_" + _s,
        "back_thigh_" + _s: "back_hip_" + _s, "back_gaskin_" + _s: "back_stifle_" + _s,
        "back_meta_" + _s: "back_hock_" + _s, "back_paw_" + _s: "back_foot_" + _s})
TORSO_CHAIN = ([tuple(p) for p in B.TORSO_CHAIN], ["hips", "spine_1", "spine_2", "spine_3", "neck"])
TAIL_CHAIN = ([tuple(p) for p in B.TAIL_BONES], ["tail_1", "tail_2", "tail_3", "tail_4"])


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def chain_weights(p2, pts, bones):
    """Weights (N, len(bones)) along a 2D polyline (y, z) by arc-length position,
    blending linearly between bone midpoints."""
    pts = np.asarray(pts, float)
    seg = pts[1:] - pts[:-1]
    ln = np.linalg.norm(seg, axis=1)
    cum = np.concatenate([[0], np.cumsum(ln)])
    best_d = np.full(len(p2), np.inf)
    s = np.zeros(len(p2))
    for i in range(len(seg)):
        t = np.clip(((p2 - pts[i]) @ seg[i]) / (ln[i] ** 2), 0, 1)
        d = np.linalg.norm(p2 - (pts[i] + t[:, None] * seg[i]), axis=1)
        m = d < best_d
        best_d[m] = d[m]
        s[m] = cum[i] + t[m] * ln[i]
    mids = (cum[:-1] + cum[1:]) / 2
    w = np.zeros((len(p2), len(bones)))
    w[s <= mids[0], 0] = 1
    w[s >= mids[-1], -1] = 1
    for i in range(len(bones) - 1):
        m = (s > mids[i]) & (s < mids[i + 1])
        t = _smooth(0, 1, (s[m] - mids[i]) / (mids[i + 1] - mids[i]))
        w[m, i] = 1 - t
        w[m, i + 1] = t
    return w


def volume_distances(co):
    """Signed distance of each point to each field volume: {name: (N,)}."""
    out = {}
    for name, p in S.volumes().items():
        p.bm.normal_update()
        bvh = BVHTree.FromBMesh(p.bm)
        vc = np.array([v.co[:] for v in p.bm.verts])
        lo, hi = vc.min(0) - 0.02, vc.max(0) + 0.02
        d = np.empty(len(co))
        for i, c in enumerate(co):
            v = Vector(c)
            loc, nrm, _, dist = bvh.find_nearest(v)
            # outside the volume's box it cannot be inside (the nearest-normal sign test
            # can flip far from a shape, at fan caps)
            outside = (c < lo).any() or (c > hi).any()
            d[i] = dist if outside or (v - loc).dot(nrm) >= 0 else -dist
        out[name] = d
        p.bm.free()
    return out


def skin_weights(co, jawflag, edges):
    """co: (N, 3) authored bind positions. Returns (W (N, nbones), memberships {volume: (N,)})."""
    nb = len(BONE_NAMES)
    bi = {n: i for i, n in enumerate(BONE_NAMES)}
    dist = volume_distances(co)
    names = list(dist)
    dm = np.stack([dist[n] for n in names], 1)
    m = np.exp(-(dm - dm.min(1, keepdims=True)) / TAU)
    m[m < 0.01] = 0
    m /= m.sum(1, keepdims=True)
    memb = {n: m[:, i] for i, n in enumerate(names)}
    W = np.zeros((len(co), nb))
    yz = co[:, 1:3]
    tw = chain_weights(yz, *TORSO_CHAIN)
    for j, b in enumerate(TORSO_CHAIN[1]):
        W[:, bi[b]] += memb["torso"] * tw[:, j]
    tw = chain_weights(yz, *TAIL_CHAIN)
    for j, b in enumerate(TAIL_CHAIN[1]):
        W[:, bi[b]] += memb["tail"] * tw[:, j]
    # head and jaw: the jaw takes the head's share below the lip plane (in front of the
    # hinge), fading out behind the mouth corners and down the throat
    hy, hz = K.hinge()
    co_, no_ = K.lip_plane()
    sd = (co - np.array(co_)) @ np.array(no_)
    front = _smooth(0.0, -0.012, sd)
    behind = _smooth(hy + 0.06, hy - 0.005, co[:, 1]) * _smooth(0.0, -0.04, sd)
    jaw = np.where(co[:, 1] < hy, front, behind)
    jaw[jawflag == 2] = 0
    W[:, bi["head"]] += memb["head"] * (1 - jaw)
    W[:, bi["jaw"]] += memb["head"] * jaw
    for v, b in VOLUME_BONE.items():
        W[:, bi[b]] += memb[v]
    # smooth over the mesh (does not cross the open lip line: the lips share no edges)
    # (edge weights fall off with length, so long edges of a coarse mesh don't carry
    # weights across the chest or belly from one leg to the other)
    ln = np.linalg.norm(co[edges[:, 0]] - co[edges[:, 1]], axis=1)
    ew = np.exp(-(ln / 0.035) ** 2)
    deg = np.zeros(len(co))
    np.add.at(deg, edges[:, 0], ew)
    np.add.at(deg, edges[:, 1], ew)
    deg = deg[:, None]
    for _ in range(3):
        acc = np.zeros_like(W)
        np.add.at(acc, edges[:, 0], W[edges[:, 1]] * ew[:, None])
        np.add.at(acc, edges[:, 1], W[edges[:, 0]] * ew[:, None])
        W = np.where(deg > 1e-6, (W + acc) / (1 + deg), W)
    # the jaw-rigid parts and the upper lip are exact
    rig = jawflag == 1
    W[rig] = 0
    W[rig, bi["jaw"]] = 1
    up = jawflag == 2
    W[up, bi["head"]] += W[up, bi["jaw"]]
    W[up, bi["jaw"]] = 0
    return limit(W), memb


def limit(W):
    W = W.copy()
    if W.shape[1] > MAX_INF:
        order = np.argsort(-W, axis=1)
        drop = order[:, MAX_INF:]
        np.put_along_axis(W, drop, 0, axis=1)
    W[W < 1e-3] = 0
    W /= W.sum(1, keepdims=True)
    return W


def ear_weights(co, s):
    pts = [(e[1] + HDY, e[2]) for e in B.EARS["bones"]]
    w = chain_weights(co[:, 1:3], pts, ["ear_%s_1" % s, "ear_%s_2" % s])
    W = np.zeros((len(co), len(BONE_NAMES)))
    W[:, BONE_NAMES.index("ear_%s_1" % s)] = w[:, 0]
    W[:, BONE_NAMES.index("ear_%s_2" % s)] = w[:, 1]
    return limit(W)


# --------------------------------------------------------------------------
def make_armature(name, collection):
    data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, data)
    collection.objects.link(arm)
    vl = bpy.context.view_layer
    for o in list(bpy.context.selected_objects):     # edit mode must hold only this object
        o.select_set(False)
    vl.objects.active = arm
    arm.select_set(True)
    with bpy.context.temp_override(active_object=arm, object=arm, selected_objects=[arm],
                                   selected_editable_objects=[arm]):
        bpy.ops.object.mode_set(mode="EDIT")
        for n, h, t, par in bone_table():
            eb = data.edit_bones.new(n)
            eb.head = Vector(h) + Vector((0, D.OY, 0))
            eb.tail = Vector(t) + Vector((0, D.OY, 0))
            eb.roll = 0.0
            if par:
                eb.parent = data.edit_bones[par]
                eb.use_connect = False
        bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def bind(ob, arm, W):
    """Vertex groups from W (N, nbones) + an armature modifier; parent to the armature."""
    for j, n in enumerate(BONE_NAMES):
        idx = np.nonzero(W[:, j] > 0)[0]
        if not len(idx):
            continue
        vg = ob.vertex_groups.new(name=n)
        for i in idx:
            vg.add([int(i)], float(W[i, j]), "REPLACE")
    md = ob.modifiers.new("Armature", "ARMATURE")
    md.object = arm
    ob.parent = arm


def attach(ob, arm, bone):
    """Rigid child of a bone, keeping its world transform."""
    mw = ob.matrix_world.copy()
    ob.parent = arm
    ob.parent_type = "BONE"
    ob.parent_bone = bone
    bpy.context.view_layer.update()
    ob.matrix_world = mw


# --------------------------------------------------------------------------
# Static test poses (pivot check only; no animation clips)
# rot = degrees about the WORLD axes at the bone's head, relative to its parent
# --------------------------------------------------------------------------
def _ang(v):
    return math.degrees(math.atan2(v[1], v[0]))


# Sit (the breed's SIT, solved per breed). The hips tilt by SIT["hips"] about the hip joint and the
# root drops by SIT["drop"], so the underside of the haunches rests on the ground; the front
# legs are turned back to vertical (SIT["shoulder"] tilts them a little further) so the front paws stay
# planted. The hind leg is re-solved from targets: stifle centre height, hock centre height
# (the hock rests on the ground) and the rear foot lying flat forward, paw flat.
# The values come from solve_sit.py, which measures the skinned mesh, and are pasted into the
# breed's breed.py; export_dog.py re-measures the contact heights on every export.
#
# Invariant - in the sit pose the haunches, hocks, hind feet and front paws all touch the
# ground (lowest vertices within 1 cm of z = 0), and nothing goes below it by more than 1 cm.
SIT = dict(B.SIT)


def _sit_hind(p=None):
    """Hind-leg rotations for the sit. Returns {bone: x-rotation} (relative, degrees)."""
    p = p or SIT
    th = p["hips"]
    piv = np.array(list(SP["hips"][0]))           # the hip tilt pivots about the hips bone's head

    def rot(pt, deg, c=piv):
        a = math.radians(deg)
        d = np.array(pt) - c
        return c + np.array([d[0] * math.cos(a) - d[1] * math.sin(a), d[0] * math.sin(a) + d[1] * math.cos(a)])
    jn = ["back_hip", "back_stifle", "back_hock", "back_foot", "back_toe"]
    rest = [np.array(J[n]) for n in jn]
    L = [np.linalg.norm(rest[i + 1] - rest[i]) for i in range(4)]
    phi_rest = [_ang(rest[i + 1] - rest[i]) for i in range(4)]
    hip = rot(rest[0], th) + np.array([0, p["drop"]])
    # thigh: forward (-y) and down to the stifle height
    dz = max(-L[0], min(L[0], p["stifle_z"] - hip[1]))
    stifle = hip + np.array([-math.sqrt(L[0] ** 2 - dz ** 2), dz])
    p1 = _ang(stifle - hip)
    # lower thigh: back (+y) and down to the hock resting on the ground
    dz = max(-L[1], min(L[1], p["hock_z"] - stifle[1]))
    p2 = _ang((math.sqrt(L[1] ** 2 - dz ** 2), dz))
    p3 = 180.0 - p["foot_pitch"]                                # rear foot: forward along the ground
    p4 = phi_rest[3]                                            # paw: flat, as standing
    phis = [p1, p2, p3, p4]
    out, acc = {}, th
    for i, n in enumerate(("hip", "stifle", "hock", "foot")):
        d = (phis[i] - phi_rest[i]) - acc
        d = (d + 180) % 360 - 180
        out[n] = d
        acc += d
    return out


def poses(sit_params=None):
    P = {"rest": {"jaw": dict(rot=(D.JAW_REST_OPEN_DEG, 0, 0))},
         "closed": {"jaw": dict(rot=(0, 0, 0)), "_hide": ["tongue"]}}
    SP_ = B.SIT_POSE
    sit = {"jaw": dict(rot=(D.JAW_REST_OPEN_DEG, 0, 0)),
           "root": dict(loc=(0, 0, (sit_params or SIT)["drop"])),
           "hips": dict(rot=((sit_params or SIT)["hips"], 0, 0)),
           "spine_3": dict(rot=(SP_["spine_3"], 0, 0)), "neck": dict(rot=(SP_["neck"], 0, 0)),
           "head": dict(rot=(SP_["head"], 0, 0)),
           "tail_1": dict(rot=((sit_params or SIT)["tail"], 0, 0)), "tail_2": dict(rot=(SP_["tail_2"], 0, 0)),
           "tail_3": dict(rot=(SP_["tail_3"], 0, 0))}
    sp = sit_params or SIT
    hind = _sit_hind(sp)
    for s in "LR":
        sit["front_shoulder_" + s] = dict(rot=(-sp["hips"] + sp["shoulder"], 0, 0))
        for n, d in hind.items():
            sit["back_%s_%s" % (n, s)] = dict(rot=(d, 0, 0))
    P["sit"] = sit
    P["wag"] = {"jaw": dict(rot=(D.JAW_REST_OPEN_DEG + 20, 0, 0)),
                "tail_1": dict(rot=(8, 0, 14)), "tail_2": dict(rot=(6, 0, 12)), "tail_3": dict(rot=(5, 0, 10)),
                "tail_4": dict(rot=(3, 0, 8)),
                "ear_L_1": dict(rot=(0, -40, 0)), "ear_L_2": dict(rot=(0, -18, 0)),
                "neck": dict(rot=(0, 0, -6)), "head": dict(rot=(0, 0, -6))}
    return P


def ear_swing(bone, rx, ry, rz):
    """An ear rotation scaled by the breed's optional EARS["swing"] (per world axis): the clips
    and poses are written for hanging ears, and an erect ear points the other way, so (-1, -1, 1)
    lays it back and flicks it out where a hanging ear swings back and out."""
    sw = B.EARS.get("swing")
    if sw is None or not bone.startswith("ear_"):
        return rx, ry, rz
    return rx * sw[0], ry * sw[1], rz * sw[2]


def apply_pose(arm, parts, name, sit_params=None):
    spec = poses(sit_params)[name]
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    for bname, d in spec.items():
        if bname == "_hide":
            continue
        pb = arm.pose.bones[bname]
        R = pb.bone.matrix_local.to_3x3()
        Ri = R.inverted()
        rx, ry, rz = (math.radians(a) for a in ear_swing(bname, *d.get("rot", (0, 0, 0))))
        Q = (Matrix.Rotation(rz, 3, "Z") @ Matrix.Rotation(ry, 3, "Y") @ Matrix.Rotation(rx, 3, "X"))
        pb.rotation_quaternion = (Ri @ Q @ R).to_quaternion()
        if "loc" in d:
            pb.location = Ri @ Vector(d["loc"])
    for k in spec.get("_hide", []):
        if k in parts:
            parts[k].hide_render = True
            parts[k].hide_viewport = True
    bpy.context.view_layer.update()


CONTACT_GROUPS = {
    "haunches": ("hips", "back_hip_L", "back_hip_R"),
    "hocks": ("back_hock_L", "back_hock_R"),
    "hind_feet": ("back_foot_L", "back_foot_R"),
    "front_paws": ("front_paw_L", "front_paw_R"),
    "tail": ("tail_2", "tail_3", "tail_4"),
}


def contacts(body, W):
    """Lowest deformed vertex (world z, m) of each contact group, grouped by each
    vertex's strongest bone. body = the skinned body object as currently posed."""
    dg = bpy.context.evaluated_depsgraph_get()
    oe = body.evaluated_get(dg)
    me = oe.to_mesh()
    co = np.array([(oe.matrix_world @ v.co)[:] for v in me.vertices])
    oe.to_mesh_clear()
    dom = np.array(BONE_NAMES)[W.argmax(1)]
    out = {g: float(co[np.isin(dom, bs), 2].min()) for g, bs in CONTACT_GROUPS.items()}
    out["lowest"] = float(co[:, 2].min())
    return out
