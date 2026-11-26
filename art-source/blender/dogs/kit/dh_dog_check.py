"""Per-clip ground-contact check on the skinned mesh (every frame).

For each frame of an action it evaluates the deformed body, ears and rigid pieces and reports:
  - the lowest vertex of the whole dog (and which bone region it belongs to), per frame range;
  - for every planted interval of every paw: the paw's lowest point (must be within TOL of z = 0)
    and its slide relative to the ground (the contact vertices must move +Y at the clip's ground
    speed and nothing else);
  - for named contact groups that must touch at given frames (e.g. the sit's end pose), their
    lowest vertex.
Fails (returns errors) when anything sinks more than TOL below z = 0, a planted paw is more than
TOL off the ground, or a planted paw slides more than SLIDE_TOL.
"""
import numpy as np
import bpy

import dh_dog_anim as A
import dh_dog_rig as RG

TOL = 0.010
SLIDE_TOL = 0.010
PAW_BONES = {"FL": ("front_paw_L",), "FR": ("front_paw_R",), "HL": ("back_foot_L",), "HR": ("back_foot_R",)}


def frame_points(rig):
    """{part: (N,3) world verts} as currently evaluated."""
    dg = bpy.context.evaluated_depsgraph_get()
    out = {}
    for k, o in rig.parts.items():
        if o.hide_render:
            continue
        oe = o.evaluated_get(dg)
        me = oe.to_mesh()
        n = len(me.vertices)
        co = np.empty(n * 3)
        me.vertices.foreach_get("co", co)
        co = co.reshape(n, 3)
        M = np.array(oe.matrix_world)
        out[k] = co @ M[:3, :3].T + M[:3, 3]
        oe.to_mesh_clear()
    return out


def sample(rig, act, frames):
    """[{part: verts}] for frames 0..frames."""
    rig.arm.animation_data.action = act
    sc = bpy.context.scene
    res = []
    for f in range(frames + 1):
        sc.frame_set(f)
        res.append(frame_points(rig))
    rig.arm.animation_data.action = None
    return res


def check_clip(rig, name, frames, speed=0.0, plants=None, touch=None, chunk=None, samples=None):
    """Returns (report lines, errors, summary dict)."""
    act = bpy.data.actions[name]
    S = samples or sample(rig, act, frames)
    dom = np.array(RG.BONE_NAMES)[rig.W.argmax(1)]
    lines, errs = [], []
    summ = {"clip": name, "frames": frames, "ground_speed_mps": speed}
    # lowest vertex per frame (whole dog)
    low = []
    for f, P in enumerate(S):
        best = (1e9, "")
        for k, v in P.items():
            i = int(v[:, 2].argmin())
            if v[i, 2] < best[0]:
                best = (float(v[i, 2]), dom[i] if k == "body" else k)
        low.append(best)
    chunk = chunk or max(1, (frames + 1) // 6)
    lines.append("%s: %d frames (%.2f s) at %d fps, ground speed %.3f m/s" %
                 (name, frames, frames / A.FPS, A.FPS, speed))
    lines.append("  lowest vertex of the whole dog, per frame range (m; 0 = ground):")
    for a in range(0, frames + 1, chunk):
        b = min(frames, a + chunk - 1)
        seg = low[a:b + 1]
        z, where = min(seg)
        f = a + seg.index((z, where))
        lines.append("    f%3d-%3d  min %+.4f  (f%d, %s)" % (a, b, z, f, where))
    zmin = min(l[0] for l in low)
    summ["lowest_m"] = round(zmin, 4)
    if zmin < -TOL:
        errs.append("%s: lowest vertex %+.4f m (sinks more than %.0f cm)" % (name, zmin, TOL * 100))
    # planted paws
    if plants:
        lines.append("  planted paws (flat part of each stance): height of the paw's lowest vertex, "
                     "slide of its contact vertices vs the ground (m)")
        worst_slide, worst_h = 0.0, 0.0
        for leg, ivs in plants.items():
            m = np.isin(dom, PAW_BONES[leg])
            for f0, f1 in ivs:
                if f1 - f0 < 1:
                    continue
                fm = (f0 + f1) // 2
                body = S[fm]["body"]
                zz = body[m, 2]
                idx = np.nonzero(m)[0][zz < zz.min() + 0.004]
                ref = S[f0]["body"][idx].mean(0)
                hs, sl = [], []
                for f in range(f0, f1 + 1):
                    v = S[f]["body"]
                    hs.append(float(v[m, 2].min()))
                    c = v[idx].mean(0) - np.array([0, speed * (f - f0) / A.FPS, 0]) - ref
                    sl.append(float(np.linalg.norm(c[:2])))
                h_lo, h_hi, s = min(hs), max(hs), max(sl)
                worst_slide = max(worst_slide, s)
                worst_h = max(worst_h, abs(h_lo), abs(h_hi))
                flag = ""
                if s > SLIDE_TOL:
                    flag += " SLIDE"
                    errs.append("%s %s f%d-%d slides %.4f m" % (name, leg, f0, f1, s))
                if h_hi > TOL or h_lo < -TOL:
                    flag += " OFF-GROUND"
                    errs.append("%s %s f%d-%d paw height %+.4f..%+.4f" % (name, leg, f0, f1, h_lo, h_hi))
                lines.append("    %s f%3d-%3d  paw low %+.4f..%+.4f  slide %.4f%s" % (leg, f0, f1, h_lo, h_hi, s, flag))
        summ["worst_plant_slide_m"] = round(worst_slide, 4)
        summ["worst_plant_height_m"] = round(worst_h, 4)
    if touch:
        for f, groups in touch.items():
            P = S[f]
            body = P["body"]
            got = {}
            for g in groups:
                bs = RG.CONTACT_GROUPS[g]
                got[g] = float(body[np.isin(dom, bs), 2].min())
            lines.append("  frame %d contact (lowest skinned vertex per group): %s" %
                         (f, "  ".join("%s %+.4f" % kv for kv in got.items())))
            for g, z in got.items():
                if abs(z) > TOL:
                    errs.append("%s f%d: %s at %+.4f (not touching)" % (name, f, g, z))
            summ.setdefault("touch_m", {})[str(f)] = {g: round(z, 4) for g, z in got.items()}
    lines.append("  -> %s" % ("OK" if not errs else "FAIL: " + "; ".join(errs)))
    summ["ok"] = not errs
    return lines, errs, summ
