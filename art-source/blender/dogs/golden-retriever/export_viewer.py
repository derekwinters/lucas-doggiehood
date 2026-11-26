"""Web-viewer data from the exported default-coat glTF, with the skin applied.

  python3 export_viewer.py [root]

The web viewer draws plain three.js meshes without skinning, so this bakes the
glTF's default pose (standing, mouth open 12 deg) into world-space vertices:
skinned primitives are deformed on the CPU (joint world matrix x inverse bind
matrix, weighted), rigid ones get their node's world matrix. The JSON keeps the
round-1/2 structure: {"scene", "nodes" [{name, t, r, s, mesh, children}],
"meshes" [[{mat, pos, nrm, uv, tan, idx}]], "materials"}; here one root node
holds one child node per mesh, all with identity transforms.

Writes <root>/viewer/dog.json and <root>/viewer/tex/: fur_default.jpg,
fur_light.jpg, fur_dark.jpg (JPEG quality 90), dog_fur_normal.png,
dog_trim_color.png. Positions and UVs rounded to 4 decimals, normals to 3.
Run after export_dog.py. viewer/ is git-ignored.
"""
import json
import os
import shutil
import struct
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 and __name__ == "__main__" else HERE
COMP = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def load(path):
    """(json, binary buffer) of a .gltf (separate .bin) or .glb."""
    if path.endswith(".glb"):
        data = open(path, "rb").read()
        ln = struct.unpack_from("<I", data, 12)[0]
        j = json.loads(data[20:20 + ln].decode())
        off = 20 + ln
        bl = struct.unpack_from("<I", data, off)[0]
        return j, data[off + 8:off + 8 + bl]
    j = json.load(open(path))
    return j, open(os.path.join(os.path.dirname(path), j["buffers"][0]["uri"]), "rb").read()


def accessor(j, binb, i):
    a = j["accessors"][i]
    bv = j["bufferViews"][a["bufferView"]]
    n = NCOMP[a["type"]]
    fmt = COMP[a["componentType"]]
    size = struct.calcsize(fmt)
    stride = bv.get("byteStride", size * n)
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    dt = np.dtype("<" + fmt)
    if stride == size * n:
        arr = np.frombuffer(binb, dtype=dt, count=a["count"] * n, offset=off).reshape(a["count"], n)
    else:
        arr = np.stack([np.frombuffer(binb, dtype=dt, count=n, offset=off + k * stride) for k in range(a["count"])])
    arr = arr.astype(np.float64)
    if a.get("normalized"):
        arr = arr / float(np.iinfo(dt).max)
    return arr


def _trs(n):
    if "matrix" in n:
        return np.array(n["matrix"], float).reshape(4, 4).T
    t = n.get("translation", [0, 0, 0])
    x, y, z, w = n.get("rotation", [0, 0, 0, 1])
    s = n.get("scale", [1, 1, 1])
    R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    M = np.eye(4)
    M[:3, :3] = R * np.array(s)[None, :]
    M[:3, 3] = t
    return M


def world_matrices(j):
    nodes = j["nodes"]
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = i
    W = {}

    def w(i):
        if i not in W:
            W[i] = _trs(nodes[i]) if i not in parent else w(parent[i]) @ _trs(nodes[i])
        return W[i]
    for i in range(len(nodes)):
        w(i)
    return W, parent


def posed_primitives(j, binb):
    """[(node index, primitive dict, world pos (n,3), world nrm (n,3))] in the default pose."""
    Wm, _ = world_matrices(j)
    out = []
    for ni, n in enumerate(j["nodes"]):
        if "mesh" not in n:
            continue
        for p in j["meshes"][n["mesh"]]["primitives"]:
            at = p["attributes"]
            pos = accessor(j, binb, at["POSITION"])
            nrm = accessor(j, binb, at["NORMAL"])
            if "skin" in n and "JOINTS_0" in at:
                sk = j["skins"][n["skin"]]
                ibm = accessor(j, binb, sk["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
                jm = np.stack([Wm[jn] @ ibm[k] for k, jn in enumerate(sk["joints"])])
                jt = accessor(j, binb, at["JOINTS_0"]).astype(int)
                wt = accessor(j, binb, at["WEIGHTS_0"])
                M = np.einsum("vk,vkab->vab", wt, jm[jt])
            else:
                M = np.repeat(Wm[ni][None], len(pos), 0)
            ph = np.concatenate([pos, np.ones((len(pos), 1))], 1)
            wp = np.einsum("vab,vb->va", M, ph)[:, :3]
            wn = np.einsum("vab,vb->va", M[:, :3, :3], nrm)
            wn /= np.maximum(np.linalg.norm(wn, axis=1, keepdims=True), 1e-12)
            out.append((ni, p, wp, wn))
    return out


def _r(a, n):
    return [float(v) for v in np.round(np.asarray(a, dtype=np.float32).ravel(), n)]


def convert(gltf_path):
    j, binb = load(gltf_path)
    images = [os.path.splitext(os.path.basename(im["uri"]))[0] for im in j.get("images", [])]

    def tex_name(info):
        if not info:
            return None
        return images[j["textures"][info["index"]]["source"]]

    mats = []
    for m in j["materials"]:
        pbr = m.get("pbrMetallicRoughness", {})
        mats.append({"name": m["name"], "color": pbr.get("baseColorFactor", [1, 1, 1, 1]),
                     "map": tex_name(pbr.get("baseColorTexture")), "normal": tex_name(m.get("normalTexture")),
                     "rough": pbr.get("roughnessFactor", 1), "metal": pbr.get("metallicFactor", 1)})
    meshes, nodes = [], [{"name": "dog_golden", "t": [0, 0, 0], "r": None, "s": None, "mesh": None, "children": []}]
    by_node = {}
    for ni, p, wp, wn in posed_primitives(j, binb):
        at = p["attributes"]
        by_node.setdefault(ni, []).append({
            "mat": p.get("material", 0), "pos": _r(wp, 4), "nrm": _r(wn, 3),
            "uv": _r(accessor(j, binb, at["TEXCOORD_0"]), 4) if "TEXCOORD_0" in at else None,
            "tan": None, "idx": [int(v) for v in accessor(j, binb, p["indices"]).ravel()]})
    for ni, prims in by_node.items():
        meshes.append(prims)
        nodes[0]["children"].append(len(nodes))
        nodes.append({"name": j["nodes"][ni]["name"], "t": [0, 0, 0], "r": None, "s": None,
                      "mesh": len(meshes) - 1, "children": []})
    return {"scene": [0], "nodes": nodes, "meshes": meshes, "materials": mats}


def main(root=ROOT):
    out = os.path.join(root, "viewer")
    tex = os.path.join(root, "textures")
    os.makedirs(os.path.join(out, "tex"), exist_ok=True)
    d = convert(os.path.join(root, "exports", "dog_golden_default.gltf"))
    with open(os.path.join(out, "dog.json"), "w") as f:
        json.dump(d, f, separators=(",", ":"))
    for coat in ("default", "light", "dark"):
        Image.open(os.path.join(tex, "dog_fur_%s_color.png" % coat)).convert("RGB").save(
            os.path.join(out, "tex", "fur_%s.jpg" % coat), quality=90)
    for f in ("dog_fur_normal.png", "dog_trim_color.png"):
        shutil.copyfile(os.path.join(tex, f), os.path.join(out, "tex", f))
    print("viewer ->", out, "%d nodes, %d meshes" % (len(d["nodes"]), len(d["meshes"])))


if __name__ == "__main__":
    main()
