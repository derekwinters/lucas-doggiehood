"""Bake every motion clip for a breed, check ground contact on the skinned mesh, export, read back.

  python3 export_anim.py <breed>    (a folder name under dogs/, or a path to a breed folder)

Writes, under <breed>/motion/ (<NAME> is the breed's NAME, e.g. dog_golden):
  exports/<NAME>_anim.gltf + .bin       the breed's model (first coat), skinned, all clips as named
                                        glTF animations (30 fps, sampled every frame), textures by
                                        relative URI from ../../textures/ (the breed's textures/)
  exports/<NAME>_anim_clips.json        per clip: frames, duration, loop, ground speed, footfalls, checks
                                        (Swim: water_line_m, the water surface's height in the clip)
  viewer/dog_anim.json                  the same glTF with the buffer and images embedded as base64
                                        data URIs (colour map JPEG 1024, normal map PNG 1024), saved
                                        as .json for a web host that refuses .gltf/.glb (git-ignored)
  review/motion-contact.txt             the per-clip contact report

Checks (exit non-zero on failure):
  - per clip, every frame, skinned: nothing more than 1 cm below z = 0 (Swim instead: the head and
    nose above the breed's water line, the clip's water_line_m); planted paws within 1 cm
    of the ground and sliding less than 1 cm against the ground (which moves at the clip's
    ground speed); SitDown's last frame touches with rump/haunches, hocks, hind feet, front paws
    and tail like the approved SIT;
  - loops end on their first frame; one-shot clips start (and PlayBow also ends) on the standing
    rest pose and SitDown ends on dh_dog_rig SIT exactly (bone rotations within 0.01 deg);
  - the glTF read back: one skin, 32 joints, every clip present with frames+1 samples per channel,
    and the file's own skinned mesh at sampled frames matches Blender's (lowest point within 2 mm).
"""
import base64
import copy
import io
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_breed  # noqa: E402

if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    dh_breed.use(sys.argv[1])
import dh_dog_anim as A  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import dh_dog as D  # noqa: E402
import dh_dog_check as K  # noqa: E402
import dh_dog_clips as C  # noqa: E402
import dh_dog_rig as RG  # noqa: E402
import export_viewer as V  # noqa: E402

M = A.MOTION
EXP = os.path.join(M, "exports")
VIEW = os.path.join(M, "viewer")
REV = os.path.join(M, "review")
GLTF = os.path.join(EXP, "%s_anim.gltf" % D.NAME)
CLIPS_JSON = "%s_anim_clips.json" % D.NAME
for d in (EXP, VIEW, REV):
    os.makedirs(d, exist_ok=True)


def pose_quats(rig):
    return {pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in rig.arm.pose.bones}


def frame_quats(rig, act, f):
    rig.arm.animation_data.action = act
    bpy.context.scene.frame_set(f)
    q = pose_quats(rig)
    rig.arm.animation_data.action = None
    return q


def max_diff(qa, qb):
    w, wl = 0.0, 0.0
    for k in qa:
        a = math.degrees(qa[k][0].rotation_difference(qb[k][0]).angle)
        a = min(a, 360 - a)
        w = max(w, a)
        wl = max(wl, (qa[k][1] - qb[k][1]).length)
    return w, wl


def reference_poses(rig):
    # standing reference = the rest pose with every paw planted at z = 0 (as Idle's frame 0)
    rig.reset()
    rig.set("jaw", D.JAW_REST_OPEN_DEG)
    rig.update()
    C.plant_all(rig, "reference")
    rig.update()
    rest = pose_quats(rig)
    rig.reset()
    rig.apply_spec(rig.sit_spec)
    rig.update()
    sit = pose_quats(rig)
    rig.reset()
    rig.update()
    return rest, sit


def export(rig, path):
    RG.apply_pose(rig.arm, rig.parts, "rest")
    rig.arm.animation_data.action = None
    bpy.ops.object.select_all(action="DESELECT")
    for o in list(rig.parts.values()) + [rig.arm]:
        o.select_set(True)
    bpy.context.scene.frame_set(0)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format="GLTF_SEPARATE", use_selection=True, export_apply=True, export_yup=True,
        export_skins=True, export_influence_nb=4, export_all_influences=False, export_animations=True,
        export_animation_mode="ACTIONS", export_force_sampling=True, export_frame_step=1,
        export_optimize_animation_size=False, export_reset_pose_bones=False, export_rest_position_armature=False,
        export_def_bones=False, export_leaf_bone=False, export_keep_originals=True)


def sample_nodes(j, binb, anim, k):
    """Copy of the glTF nodes with the animation's k-th sample applied."""
    nodes = copy.deepcopy(j["nodes"])
    for ch in anim["channels"]:
        smp = anim["samplers"][ch["sampler"]]
        out = V.accessor(j, binb, smp["output"])
        n = nodes[ch["target"]["node"]]
        n.pop("matrix", None)
        n[{"translation": "translation", "rotation": "rotation", "scale": "scale"}[ch["target"]["path"]]] = \
            [float(x) for x in out[min(k, len(out) - 1)]]
    return nodes


def gltf_lowest(j, binb, nodes):
    jj = dict(j)
    jj["nodes"] = nodes
    zmin = 1e9
    for ni, p, wp, wn in V.posed_primitives(jj, binb):
        zmin = min(zmin, float(wp[:, 1].min()))          # Y-up
    return zmin


def readback(rig, metas, samples):
    errs = []
    j, binb = V.load(GLTF)
    skins = j.get("skins", [])
    if len(skins) != 1 or len(skins[0]["joints"]) != len(RG.BONE_NAMES):
        errs.append("skin / joint count")
    anims = {a["name"]: a for a in j.get("animations", [])}
    for m in metas:
        a = anims.get(m["name"])
        if a is None:
            errs.append("animation %s missing" % m["name"])
            continue
        for smp in a["samplers"]:
            n = j["accessors"][smp["input"]]["count"]
            if n != m["frames"] + 1:
                errs.append("%s: %d samples, want %d" % (m["name"], n, m["frames"] + 1))
                break
        for k in sorted({0, m["frames"] // 3, (2 * m["frames"]) // 3, m["frames"]}):
            zg = gltf_lowest(j, binb, sample_nodes(j, binb, a, k))
            zb = min(float(v[:, 2].min()) for v in samples[m["name"]][k].values())
            if abs(zg - zb) > 0.002:
                errs.append("%s f%d: glTF lowest %.4f vs Blender %.4f" % (m["name"], k, zg, zb))
    return errs, j


def viewer_json(j):
    """Self-contained glTF JSON: buffer + images as data URIs (fur colour JPEG, normal PNG)."""
    v = copy.deepcopy(j)
    binb = open(os.path.join(EXP, j["buffers"][0]["uri"]), "rb").read()
    v["buffers"][0]["uri"] = "data:application/octet-stream;base64," + base64.b64encode(binb).decode()
    for im in v.get("images", []):
        src = os.path.normpath(os.path.join(EXP, im["uri"]))
        img = Image.open(src)
        if max(img.size) > 1024:
            img = img.resize((1024, 1024), Image.LANCZOS)
        buf = io.BytesIO()
        if "normal" in os.path.basename(src):
            img.convert("RGB").save(buf, "PNG", optimize=True)
            mime = "image/png"
        elif "color" in os.path.basename(src) and "trim" not in os.path.basename(src):
            img.convert("RGB").save(buf, "JPEG", quality=88)
            mime = "image/jpeg"
        else:
            img.save(buf, "PNG", optimize=True)
            mime = "image/png"
        im["uri"] = "data:%s;base64,%s" % (mime, base64.b64encode(buf.getvalue()).decode())
        im["mimeType"] = mime
        im["name"] = os.path.splitext(os.path.basename(src))[0]
    return v


def main():
    rig = A.Rig()
    rest, sit = reference_poses(rig)
    metas = []
    for name in C.ORDER:
        try:
            m = C.make(rig, name)
        except KeyError:
            continue
        metas.append(m)
        print("baked", name, m["frames"], "frames")
    report, failures, summary, samples = [], [], [], {}
    report.append("%s motion clips - ground contact check (skinned mesh, every frame)" % D.B.DISPLAY_NAME)
    report.append("Tolerances: nothing below z = %.3f m; planted paws within %.3f m of z = 0 and sliding "
                  "< %.3f m against the ground (moving +Y at the clip's ground speed)." % (-K.TOL, K.TOL, K.SLIDE_TOL))
    report.append("Blender axes: front -Y, up +Z, metres. 30 fps. Frame N of a loop equals frame 0.")
    report.append("")
    for m in metas:
        act = bpy.data.actions[m["name"]]
        S = K.sample(rig, act, m["frames"])
        samples[m["name"]] = S
        lines, errs, summ = K.check_clip(rig, m["name"], m["frames"], m.get("ground_speed", 0.0),
                                         m.get("plants"), m.get("touch"), samples=S, water=m.get("water_z"))
        # pose continuity at the ends
        q0, qN = frame_quats(rig, act, 0), frame_quats(rig, act, m["frames"])
        if m["loop"]:
            d = max_diff(q0, qN)
            lines.insert(-1, "  loop: last frame vs first %.4f deg, root %.5f m" % d)
            if d[0] > 0.01 or d[1] > 1e-4:
                errs.append("%s does not loop" % m["name"])
        else:
            d0 = max_diff(q0, rest)
            ref = sit if m["name"] == "SitDown" else rest
            d1 = max_diff(qN, ref)
            lines.insert(-1, "  first frame vs standing rest pose %.4f deg / %.5f m; last frame vs %s %.4f deg / %.5f m"
                         % (d0[0], d0[1], "SIT" if m["name"] == "SitDown" else "standing rest pose", d1[0], d1[1]))
            if d0[0] > 0.01 or d1[0] > 0.01 or d0[1] > 1e-4 or d1[1] > 1e-4:
                errs.append("%s does not start/end on its reference pose" % m["name"])
            summ["end_pose_error_deg"] = round(d1[0], 5)
        if errs and lines[-1].endswith("OK"):
            lines[-1] = "  -> FAIL: " + "; ".join(errs)
        report += lines + [""]
        failures += errs
        summ["ok"] = not errs
        summary.append(summ)
        print("\n".join(lines))
    if rig.clamped:
        report.append("IK reach clamps: %s" % rig.clamped[:20])
        failures.append("IK could not reach %d targets" % len(rig.clamped))
    export(rig, GLTF)
    rerr, j = readback(rig, metas, samples)
    failures += rerr
    report.append("glTF read back (%s): %s" % (os.path.basename(GLTF), "OK" if not rerr else rerr))
    # clip metadata into the file (animation extras) and a sidecar
    info = {}
    for m, s in zip(metas, summary):
        e = {"loop": m["loop"], "frames": m["frames"], "fps": A.FPS, "duration_s": round(m["frames"] / A.FPS, 4),
             "ground_speed_mps": m.get("ground_speed", 0.0)}
        if "water_z" in m:
            e["water_line_m"] = round(m["water_z"], 4)
        if "stride_m" in m:
            e["stride_m"] = round(m["stride_m"], 4)
            e["footfalls_touchdown_phase"] = m["footfalls"]
            e["duty_factor"] = m["duty"]
        info[m["name"]] = dict(e, notes=m.get("notes", ""), check=s)
        for a in j["animations"]:
            if a["name"] == m["name"]:
                a["extras"] = e
    j.setdefault("asset", {})["extras"] = {"dh_note": "%s; in place, root motion out of the clips; 30 fps"
                                                      % D.B.MOTION_STATUS}
    with open(GLTF, "w") as fh:
        json.dump(j, fh, indent=1)
    with open(os.path.join(EXP, CLIPS_JSON), "w") as fh:
        json.dump({"fps": A.FPS, "units": "metres; Blender axes front -Y up +Z (glTF: front +Z, up +Y)",
                   "clips": info, "all_checks_ok": not failures}, fh, indent=1)
    vj = viewer_json(j)
    vp = os.path.join(VIEW, "dog_anim.json")
    with open(vp, "w") as fh:
        json.dump(vj, fh, separators=(",", ":"))
    report.append("viewer/dog_anim.json: %.2f MB (self-contained glTF JSON, buffer + images as data URIs)"
                  % (os.path.getsize(vp) / 1e6))
    report.append("")
    report.append("ALL OK" if not failures else "FAILED: %s" % failures)
    with open(os.path.join(REV, "motion-contact.txt"), "w") as fh:
        fh.write("\n".join(report) + "\n")
    print("\n".join(report[-4:]))
    sys.exit(0 if not failures else 1)


if __name__ == "__main__":
    main()
