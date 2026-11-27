"""Export the bug (#334) with its WingFlap clip to .gltf + .bin, read it back and check it.

  python3 art-source/blender/pests/bug/export_bug.py [outroot]     (outroot defaults to this folder)

Writes <outroot>/exports/:
  pest_bug.gltf + pest_bug.bin   the bug (root empty, body, two wings) and the WingFlap clip as one
                                 glTF animation, 30 fps, sampled every frame; flat material colours,
                                 no images
  bug_stats.json                 triangles per part, size, materials, the clip and the checks

Checks on the file read back (exit non-zero on failure): exactly the four bug nodes and three
meshes, nothing else (no collider, camera, light, skin, image or extension); one animation,
WingFlap, frames + 1 samples per channel, every channel's last sample equal to its first, the root
not animated; triangles within the provisional per-mesh backstop (#799); every material
single-sided. Prints ALL OK when everything passes. Two runs give byte-identical files.
"""
import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_bug as B  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

K = B.K
ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
GLTF = os.path.join(EXP, B.NAME + ".gltf")
BIN = os.path.join(EXP, B.NAME + ".bin")
STATS = os.path.join(EXP, "bug_stats.json")
MAX_TRIS = 6000          # provisional per-mesh backstop (#799), applied to the whole bug
LOOP_TOL = 1e-6
NODES = {B.NAME, B.NAME + "_body", B.NAME + "_wing_L", B.NAME + "_wing_R"}

COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def accessor(j, binb, i):
    a = j["accessors"][i]
    v = j["bufferViews"][a["bufferView"]]
    fmt, size = COMP[a["componentType"]]
    n = NCOMP[a["type"]]
    stride = v.get("byteStride", size * n)
    off = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<%d%s" % (n, fmt), binb, off + k * stride) for k in range(a["count"])]


def quat_deg(p, q):
    return math.degrees(2 * math.acos(min(1.0, abs(sum(a * b for a, b in zip(p, q))))))


def export(parts):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts.values():
        o.select_set(True)
    bpy.context.scene.frame_set(0)
    bpy.ops.export_scene.gltf(
        filepath=GLTF, export_format="GLTF_SEPARATE", use_selection=True, export_apply=True, export_yup=True,
        export_animations=True, export_animation_mode="ACTIONS", export_force_sampling=True,
        export_frame_step=1, export_optimize_animation_size=False, export_materials="EXPORT",
        export_cameras=False, export_lights=False, export_extras=False)


def check(j, binb, frames):
    errs = []
    names = [n.get("name") for n in j["nodes"]]
    if sorted(names) != sorted(NODES):
        errs.append("nodes %s" % names)
    if [names[i] for i in j["scenes"][0]["nodes"]] != [B.NAME]:
        errs.append("scene roots")
    if len(j.get("meshes", [])) != 3:
        errs.append("%d meshes" % len(j.get("meshes", [])))
    for key in ("cameras", "skins", "images", "textures", "extensionsUsed", "extensionsRequired"):
        if key in j:
            errs.append("unexpected %s" % key)
    if any("extensions" in n or "collider" in n.get("name", "").lower() for n in j["nodes"]):
        errs.append("node extensions / collider")
    if any(m.get("doubleSided") for m in j.get("materials", [])):
        errs.append("double-sided material")
    anims = j.get("animations", [])
    if [a["name"] for a in anims] != [B.CLIP]:
        errs.append("animations %s" % [a["name"] for a in anims])
        return errs, {}
    a = anims[0]
    ranges = {}
    for ch in a["channels"]:
        node, path = names[ch["target"]["node"]], ch["target"]["path"]
        if node == B.NAME:
            errs.append("root is animated (%s)" % path)
        s = a["samplers"][ch["sampler"]]
        t, out = accessor(j, binb, s["input"]), accessor(j, binb, s["output"])
        if len(t) != frames + 1 or abs(t[-1][0] - frames / B.FPS) > 1e-4:
            errs.append("%s %s: %d samples" % (node, path, len(t)))
        if max(abs(x - y) for x, y in zip(out[0], out[-1])) > LOOP_TOL:
            errs.append("%s %s does not loop" % (node, path))
        if path == "translation":
            ys = [p[1] for p in out]
            ranges["%s bob_m" % node] = round(max(ys) - min(ys), 4)
        if path == "rotation":
            ranges["%s swing_deg" % node] = round(max(quat_deg(p, q) for p in out for q in out), 2)
    return errs, ranges


def main():
    os.makedirs(EXP, exist_ok=True)
    K.reset_scene()
    parts = B.build()
    meta = B.make_clip(parts)
    bpy.context.view_layer.update()
    tris = {k: K.tri_count(o) for k, o in parts.items() if o.type == "MESH"}
    tris["total"] = sum(tris.values())
    pts = [o.matrix_world @ Vector(c) for o in parts.values() if o.type == "MESH" for c in o.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
    export(parts)
    j = json.load(open(GLTF))
    binb = open(BIN, "rb").read()
    errs, ranges = check(j, binb, meta["frames"])
    gl_tris = sum(j["accessors"][p["indices"]]["count"] // 3 for m in j["meshes"] for p in m["primitives"])
    if gl_tris != tris["total"]:
        errs.append("glTF has %d triangles, Blender %d" % (gl_tris, tris["total"]))
    if tris["total"] > MAX_TRIS:
        errs.append("%d triangles over the %d backstop" % (tris["total"], MAX_TRIS))
    extras = {k: meta[k] for k in ("loop", "frames", "fps", "duration_s")}
    j["animations"][0]["extras"] = extras
    j.setdefault("asset", {})["extras"] = {
        "dh_note": "Doggiehood bug (#334), design build pending approval; in place, the game moves the "
                   "root along its flight path; 30 fps; no collider"}
    with open(GLTF, "w") as fh:
        json.dump(j, fh, indent=1)
    mats = sorted(m["name"] for m in j["materials"])
    stats = {"name": B.NAME, "tris": tris, "tri_backstop": MAX_TRIS,
             "size_xyz_m": size, "units": "metres; Blender axes front -Y up +Z (glTF: front +Z, up +Y)",
             "pivot": "centre of the thorax", "materials": mats, "clip": dict(meta, measured=ranges),
             "gltf_bytes": os.path.getsize(GLTF), "bin_bytes": os.path.getsize(BIN),
             "checks_ok": not errs}
    with open(STATS, "w") as fh:
        json.dump(stats, fh, indent=1)
    print("%s: %d triangles %s, size %s m" % (B.NAME, tris["total"], tris, size))
    print("clip %s: %d frames @ %d fps, %s" % (meta["name"], meta["frames"], meta["fps"], ranges))
    print("ALL OK" if not errs else "FAILED: %s" % errs)
    sys.exit(0 if not errs else 1)


if __name__ == "__main__":
    main()
