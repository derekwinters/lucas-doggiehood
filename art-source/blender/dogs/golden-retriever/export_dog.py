"""Export the Golden Retriever concept (round 4: one skinned body) and check what was written.

  python3 export_dog.py [root]        (root defaults to this script's folder)

Writes, under <root>:
  textures/                            trim palette, then the fur baked onto the full and the LOD unwrap
  exports/dog_golden_<coat>.gltf+.bin  default, light, dark: the same rigged mesh, each with its coat's fur
  exports/dog_golden_lod1.gltf+.bin    the ~1,500-triangle LOD (default coat, its own 512 fur atlas)
  exports/dog_golden_skeleton.json     bones (head, tail, parent), accessory attachments, test poses
  exports/dog_stats.json               triangles per part, sizes, bones, weights, check results
  dog-golden-viewer.glb                one self-contained file (textures embedded, default coat); checked,
                                       then git-ignored

Each file holds the armature (32 joints, one skin), the skinned body and ears,
and the rigid accessories as children of their bones. The default pose in the
file is the "rest" pose (jaw 12 deg open); the inverse bind matrices are the
bind pose (mouth closed). No animation clips.

Every file is read back and must pass: relative image URIs that resolve in
textures/; fur material has colour + normal maps, trim materials a colour map;
no material double-sided; <= 5 materials, all DH_Dog_*; one skin with the 32
expected joints in the expected hierarchy; body and ears skinned, with
JOINTS_0/WEIGHTS_0 only (<= 4 influences) and every vertex's weights summing
to 1; eyes / nose / collar / tongue parented to their bones; no animations;
the skinned default pose (computed from the file) stands on the ground and
matches Blender's own deformed mesh; triangle budget met; in the "sit" pose the
lowest skinned vertex of the rump/haunches, hocks, hind feet, front paws and tail
is within 1 cm of z = 0 and nothing goes more than 1 cm below it (printed, and
stored in dog_stats.json as sit_contact_m).
Exits non-zero on any failure.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import numpy as np  # noqa: E402

import dh_dog as D  # noqa: E402
import dh_dog_rig as RG  # noqa: E402
import dh_dog_tex as T  # noqa: E402
import export_viewer as V  # noqa: E402

ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
BUDGET = {"full": 4000, "lod1": 1500}
SKINNED = ("body", "ear_L", "ear_R")
os.makedirs(EXP, exist_ok=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(path, parts, fmt="GLTF_SEPARATE"):
    bpy.ops.object.select_all(action="DESELECT")
    for o in list(parts.values()) + [D.build.armature]:
        o.select_set(True)
    kw = dict(filepath=path, export_format=fmt, use_selection=True, export_apply=True, export_yup=True,
              export_skins=True, export_influence_nb=4, export_all_influences=False, export_animations=False,
              export_rest_position_armature=False, export_def_bones=False, export_leaf_bone=False)
    if fmt == "GLTF_SEPARATE":
        kw["export_keep_originals"] = True
    bpy.ops.export_scene.gltf(**kw)


def yup(p):
    """Blender (x, y, z) -> glTF Y-up (x, z, -y)."""
    return (p[0], p[2], -p[1])


def blender_posed(parts):
    """World-space deformed vertices of every part, Blender axes."""
    dg = bpy.context.evaluated_depsgraph_get()
    out = {}
    for k, o in parts.items():
        oe = o.evaluated_get(dg)
        me = oe.to_mesh()
        out[k] = np.array([(oe.matrix_world @ v.co)[:] for v in me.vertices])
        oe.to_mesh_clear()
    return out


def check(path, sfx, lod, parts):
    errs = []
    j, binb = V.load(path)
    glb = path.endswith(".glb")
    for im in j.get("images", []):
        if glb:
            if "bufferView" not in im:
                errs.append("glb image not embedded")
            continue
        uri = im.get("uri")
        if uri is None or os.path.isabs(uri) or ":" in uri:
            errs.append("bad uri %r" % uri)
            continue
        f = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.dirname(f) != TEX or not os.path.isfile(f):
            errs.append("uri does not resolve into textures/: %s" % uri)
    mats = j.get("materials", [])
    if len(mats) > 5:
        errs.append("%d materials" % len(mats))
    for m in mats:
        pbr = m.get("pbrMetallicRoughness", {})
        if not m["name"].startswith("DH_Dog_"):
            errs.append("material name %s" % m["name"])
        if m.get("doubleSided"):
            errs.append("double-sided %s" % m["name"])
        if "baseColorTexture" not in pbr:
            errs.append("no colour map on %s" % m["name"])
        if m["name"].startswith("DH_Dog_Fur") and "normalTexture" not in m:
            errs.append("no normal map on %s" % m["name"])
    if j.get("animations"):
        errs.append("file has animations")
    nodes = j["nodes"]
    by_name = {n["name"]: i for i, n in enumerate(nodes)}
    _, parent = V.world_matrices(j)
    skins = j.get("skins", [])
    if len(skins) != 1:
        errs.append("%d skins" % len(skins))
        return errs, 0, None
    jn = [nodes[i]["name"] for i in skins[0]["joints"]]
    if sorted(jn) != sorted(RG.BONE_NAMES):
        errs.append("joints %s" % sorted(set(jn) ^ set(RG.BONE_NAMES)))
    for name, _h, _t, par in RG.bone_table():
        if name in by_name:
            i = by_name[name]
            got = nodes[parent[i]]["name"] if i in parent else None
            if par and got != par:
                errs.append("joint %s parent %s, want %s" % (name, got, par))
    if V.accessor(j, binb, skins[0]["inverseBindMatrices"]).shape[0] != len(jn):
        errs.append("inverse bind matrix count")
    for key in parts:
        name = "%s_%s%s" % (D.NAME, key, sfx)
        if name not in by_name:
            errs.append("missing node %s" % name)
            continue
        n = nodes[by_name[name]]
        if key in SKINNED:
            if "skin" not in n:
                errs.append("%s not skinned" % name)
            for p in j["meshes"][n["mesh"]]["primitives"]:
                at = p["attributes"]
                if "JOINTS_0" not in at or "WEIGHTS_0" not in at or "JOINTS_1" in at or "WEIGHTS_1" in at:
                    errs.append("%s joint/weight attributes %s" % (name, sorted(at)))
                    continue
                w = V.accessor(j, binb, at["WEIGHTS_0"])
                if np.abs(w.sum(1) - 1).max() > 2e-3:
                    errs.append("%s weights do not sum to 1 (max err %.4f)" % (name, np.abs(w.sum(1) - 1).max()))
        else:
            want = D.ACCESSORY_BONE[key]
            got = nodes[parent[by_name[name]]]["name"] if by_name[name] in parent else None
            if got != want:
                errs.append("%s parent %s, want bone %s" % (name, got, want))
    tris, ymin = 0, 1e9
    posed = V.posed_primitives(j, binb)
    for ni, p, wp, wn in posed:
        if p.get("mode", 4) != 4:
            errs.append("non-triangle primitive")
        tris += j["accessors"][p["indices"]]["count"] // 3
        ymin = min(ymin, wp[:, 1].min())
    if abs(ymin) > 0.004:
        errs.append("lowest point at y=%.4f, not on the ground" % ymin)
    if tris > BUDGET["lod1" if lod else "full"]:
        errs.append("%d triangles over budget" % tris)
    # the file's own skinned pose must match Blender's deformed mesh (same bounds per part)
    bp = blender_posed(parts)
    for key, pts in bp.items():
        name = "%s_%s%s" % (D.NAME, key, sfx)
        if name not in by_name:
            continue
        g = np.concatenate([wp for ni, p, wp, wn in posed if ni == by_name[name]])
        b = np.array([yup(p) for p in pts])
        d = max(np.abs(g.min(0) - b.min(0)).max(), np.abs(g.max(0) - b.max(0)).max())
        if d > 2e-3:
            errs.append("%s: glTF skinned bounds differ from Blender by %.4f m" % (name, d))
    return errs, tris, posed


SIT_TOL = 0.01      # metres: nothing hovers or sinks more than 1 cm


def sit_contact(parts):
    """Pose "sit", measure the lowest skinned vertex of each contact group (rump/haunches,
    hocks, hind feet, front paws, tail) against z = 0, then return to the rest pose."""
    arm = D.build.armature
    RG.apply_pose(arm, parts, "sit")
    c = RG.contacts(parts["body"], D.build.weights)
    every = np.concatenate(list(blender_posed(parts).values()))
    c["lowest_any_part"] = float(every[:, 2].min())
    RG.apply_pose(arm, parts, "rest")
    c = {k: round(v, 4) for k, v in c.items()}
    errs = ["sit: %s at z=%+.4f (more than %.0f cm off the ground)" % (k, v, SIT_TOL * 100)
            for k, v in c.items() if k in RG.CONTACT_GROUPS and abs(v) > SIT_TOL]
    errs += ["sit: %s at z=%+.4f sinks below the ground" % (k, v)
             for k, v in c.items() if k.startswith("lowest") and v < -SIT_TOL]
    return c, errs


def weight_stats(W):
    n = (W > 0).sum(1)
    return {"vertices": int(len(W)), "max_influences": int(n.max()),
            "influence_histogram": {str(k): int((n == k).sum()) for k in range(1, 5)},
            "max_sum_error": float(np.abs(W.sum(1) - 1).max())}


def weight_sanity(ob, W):
    """Leg bones only on their own side, head / jaw / ear bones only up at the head,
    tail bones only behind the hips. Catches a mis-assigned region."""
    errs = []
    co = np.array([(ob.matrix_world @ v.co)[:] for v in ob.data.vertices])
    for j, n in enumerate(RG.BONE_NAMES):
        m = W[:, j] > 0.05
        if not m.any():
            continue
        if n.startswith(("front_", "back_")) and n.endswith("_L") and co[m, 0].min() < -0.02:
            errs.append("%s weights on the right side" % n)
        if n.startswith(("front_", "back_")) and n.endswith("_R") and co[m, 0].max() > 0.02:
            errs.append("%s weights on the left side" % n)
        if n in ("head", "jaw") and co[m, 2].min() < 0.85:
            errs.append("%s weights below z=0.85" % n)
        if n.startswith("tail") and co[m, 1].min() < 0.45:
            errs.append("%s weights in front of the hips" % n)
    return errs


def main():
    T.build_all(ROOT)
    for lod in (False, True):           # bake the fur onto each unwrap
        reset()
        D.build("default", lod=lod, tex_dir=TEX, bake=True)
        print("baked %s fur, %.0f%% of the atlas covered" % ("LOD" if lod else "full", 100 * D.build.coverage))
    stats = {"name": "Golden Retriever (adult) concept, round 4", "units": "metres, Blender axes (front -Y, up +Z); "
             "glTF files are Y-up (front +Z)", "files": {}, "budget": BUDGET}
    failures = []
    for variant in ("default", "light", "dark", "lod1"):
        reset()
        lod = variant == "lod1"
        sfx = "_lod1" if lod else ""
        parts = D.build("default" if lod else variant, lod=lod, tex_dir=TEX)
        path = os.path.join(EXP, "dog_golden_%s.gltf" % variant)
        export(path, parts)
        errs, tris, posed = check(path, sfx, lod, parts)
        errs += weight_sanity(parts["body"], D.build.weights)
        per = {k: D.tri_count(o) for k, o in parts.items()}
        if sum(per.values()) != tris:
            errs.append("Blender count %d != glTF count %d" % (sum(per.values()), tris))
        j = json.load(open(path))
        stats["files"][os.path.basename(path)] = {
            "tris": tris, "errors": errs, "materials": [m["name"] for m in j["materials"]],
            "images": sorted(im.get("uri") for im in j.get("images", [])), "joints": len(j["skins"][0]["joints"]),
            "gltf_bytes": os.path.getsize(path), "bin_bytes": os.path.getsize(path[:-5] + ".bin")}
        print("%-26s %5d tris  %s" % (os.path.basename(path), tris, "OK" if not errs else errs))
        failures += errs
        if variant == "default" or lod:      # the sit pose must actually sit (all coats share the mesh)
            sit, serr = sit_contact(parts)
            stats.setdefault("sit_contact_m", {})["lod1" if lod else "full"] = sit
            print("  sit contact (lowest skinned vertex, m; 0 = on the ground):",
                  "  ".join("%s %+.4f" % kv for kv in sit.items()), "OK" if not serr else serr)
            failures += serr
        bp = blender_posed(parts)
        allp = np.concatenate(list(bp.values()))
        if variant == "default" or lod:
            stats["lod1" if lod else "full"] = {
                "total_tris": tris,
                "size_xyz_m": [round(float(v), 3) for v in allp.max(0) - allp.min(0)],
                "bones": len(RG.BONE_NAMES),
                "skin_weights": weight_stats(D.build.weights),
                "parts": {k: {"object": o.name, "tris": per[k],
                              "skinned": k in SKINNED, "bone": D.ACCESSORY_BONE.get(k),
                              "materials": [m.name for m in o.data.materials]} for k, o in parts.items()},
            }
        if variant == "default":
            body = bp["body"]
            stats["measurements_m"] = {
                # rump = back of the body beside the tail (the tail is under 0.09 m from the midline)
                "nose_to_rump": round(float(body[np.abs(body[:, 0]) > 0.10, 1].max() - allp[:, 1].min()), 3),
                "nose_to_tail_tip": round(float(allp[:, 1].max() - allp[:, 1].min()), 3),
                "top_of_head": round(float(allp[:, 2].max()), 3),
                "width_incl_ears": round(float(allp[:, 0].max() - allp[:, 0].min()), 3),
                "back_height": round(float(body[(body[:, 1] > 0.0) & (body[:, 1] < 0.6), 2].max()), 3),
                "in_game_stand_in_wlh": [1.26, 1.50, 1.58],
            }
            arm = D.build.armature
            bones = {}
            for name, _h, _t, par in RG.bone_table():
                b = arm.data.bones[name]
                bones[name] = {"parent": par, "head_blender": [round(v, 4) for v in b.head_local],
                               "tail_blender": [round(v, 4) for v in b.tail_local],
                               "head_gltf": [round(v, 4) for v in yup(b.head_local)]}
            skel = {"note": "Bone heads are the joint pivots, metres. 'blender' = Blender axes (front -Y, up +Z); "
                            "'gltf' = the exported Y-up axes (front +Z). Bind pose = standing, mouth closed; "
                            "the files' default pose opens the jaw by jaw_rest_open_deg.",
                    "armature": arm.name, "bone_count": len(bones), "bones": bones,
                    "skinned_meshes": {k: "%s_%s" % (D.NAME, k) for k in SKINNED},
                    "max_influences": RG.MAX_INF,
                    "accessories": {k: {"object": "%s_%s" % (D.NAME, k), "bone": b} for k, b in D.ACCESSORY_BONE.items()},
                    "jaw_rest_open_deg": D.JAW_REST_OPEN_DEG,
                    "test_poses": {k: {b: v for b, v in p.items()} for k, p in RG.poses().items()}}
            json.dump(skel, open(os.path.join(EXP, "dog_golden_skeleton.json"), "w"), indent=1)
            glb = os.path.join(ROOT, "dog-golden-viewer.glb")
            export(glb, parts, fmt="GLB")
            gerr, gt, _ = check(glb, "", False, parts)
            mb = os.path.getsize(glb) / 1e6
            if mb > 3.0:
                gerr.append("viewer glb %.2f MB" % mb)
            stats["files"]["dog-golden-viewer.glb"] = {"tris": gt, "errors": gerr, "bytes": os.path.getsize(glb)}
            print("%-26s %5d tris  %.2f MB  %s" % ("dog-golden-viewer.glb", gt, mb, "OK" if not gerr else gerr))
            failures += gerr
    stats["textures"] = {}
    from PIL import Image
    for f in sorted(os.listdir(TEX)):
        im = Image.open(os.path.join(TEX, f))
        stats["textures"][f] = {"px": list(im.size), "bytes": os.path.getsize(os.path.join(TEX, f))}
    stats["skeleton"] = "dog_golden_skeleton.json"
    stats["all_checks_ok"] = not failures
    json.dump(stats, open(os.path.join(EXP, "dog_stats.json"), "w"), indent=1)
    print("ALL OK" if not failures else "FAILED")
    sys.exit(0 if not failures else 1)


if __name__ == "__main__":
    main()
