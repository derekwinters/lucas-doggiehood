"""A self-contained web viewer for the exported dog bed (#411): one HTML file with the mesh and
textures embedded, three.js r128 from cdnjs and a small built-in orbit control (drag to turn,
wheel or pinch to zoom). For review only; the glTF in exports/ is the source of truth.

  python3 art-source/blender/items/bed/export_viewer.py [out.html]

Default output: this folder's out/viewer.html (git-ignored). Needs numpy and Pillow, not bpy.
The geometry comes from exports/dog_bed.gltf through the dog kit's glTF reader; glTF is Y-up,
like three.js. Colour maps are embedded as JPEG (quality 90), normal maps as PNG.
"""
import base64
import io
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "dogs", "kit")))
import export_viewer as GV  # noqa: E402  (load / accessor: the dog kit's glTF reader)

GLTF = os.path.join(HERE, "exports", "dog_bed.gltf")
THREE = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"
JPEG_QUALITY = 90
ROUND = 5


def data_uri(path, colour):
    im = Image.open(path)
    buf = io.BytesIO()
    if colour:
        im.convert("RGB").save(buf, "JPEG", quality=JPEG_QUALITY)
        return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()
    im.save(buf, "PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def model():
    j, binb = GV.load(GLTF)
    base = os.path.dirname(GLTF)
    images = [os.path.normpath(os.path.join(base, im["uri"])) for im in j["images"]]
    tex = {}

    def tex_uri(info, colour):
        if not info:
            return None
        p = images[j["textures"][info["index"]]["source"]]
        key = (p, colour)
        if key not in tex:
            tex[key] = "t%d" % len(tex)
        return tex[key]
    mats = []
    for m in j["materials"]:
        pbr = m.get("pbrMetallicRoughness", {})
        mats.append({"name": m["name"], "color": pbr.get("baseColorFactor", [1, 1, 1, 1])[:3],
                     "rough": pbr.get("roughnessFactor", 1), "metal": pbr.get("metallicFactor", 0),
                     "map": tex_uri(pbr.get("baseColorTexture"), True), "normal": tex_uri(m.get("normalTexture"), False)})
    prims = []
    for node in j["nodes"]:
        if "mesh" not in node:
            continue
        for p in j["meshes"][node["mesh"]]["primitives"]:
            at = p["attributes"]
            prims.append({"mat": p.get("material", 0),
                          "pos": GV._r(GV.accessor(j, binb, at["POSITION"]), ROUND),
                          "nrm": GV._r(GV.accessor(j, binb, at["NORMAL"]), 3),
                          "uv": GV._r(GV.accessor(j, binb, at["TEXCOORD_0"]), ROUND),
                          "idx": [int(v) for v in GV.accessor(j, binb, p["indices"]).ravel()]})
    textures = {k: data_uri(p, colour) for (p, colour), k in tex.items()}
    return {"materials": mats, "prims": prims, "textures": textures}


PAGE = r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Dog Bed Viewer</title>
<style>
:root { --bg: #F1ECE3; --ink: #28241F; --sub: #69625A; }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { --bg: #1E1C1A; --ink: #EDE8E0; --sub: #A9A196; } }
:root[data-theme="dark"] { --bg: #1E1C1A; --ink: #EDE8E0; --sub: #A9A196; }
html, body { margin: 0; height: 100%; background: var(--bg); color: var(--ink);
  font: 15px/1.4 system-ui, -apple-system, "Segoe UI", sans-serif; overflow: hidden; }
#c { position: fixed; inset: 0; width: 100%; height: 100%; touch-action: none; display: block; }
header { position: fixed; top: 0; left: 0; right: 0; padding: 14px 16px; pointer-events: none; }
h1 { margin: 0; font-size: 18px; }
p { margin: 4px 0 0; color: var(--sub); font-size: 13px; max-width: 46em; }
</style></head><body>
<canvas id="c"></canvas>
<header><h1>Dog bed (#411)</h1>
<p>__SUB__ Drag to turn, scroll or pinch to zoom. The ground grid is 0.5 m; the red outline is today's 1.4 x 1.0 m graybox slab.</p></header>
<script src="__THREE__"></script>
<script>
const M = __MODEL__;
const canvas = document.getElementById("c");
const renderer = new THREE.WebGLRenderer({canvas, antialias: true});
renderer.outputEncoding = THREE.sRGBEncoding;
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
const scene = new THREE.Scene();
scene.background = new THREE.Color(getComputedStyle(document.documentElement).getPropertyValue("--bg").trim() || "#F1ECE3");
const camera = new THREE.PerspectiveCamera(35, 1, 0.05, 100);
scene.add(new THREE.HemisphereLight(0xf4f0ff, 0x8e9e68, 0.75));
const sun = new THREE.DirectionalLight(0xfff2de, 1.1); sun.position.set(-2, 4, 3); scene.add(sun);
const fill = new THREE.DirectionalLight(0xe0ebff, 0.35); fill.position.set(3, 2, -2); scene.add(fill);
const loader = new THREE.TextureLoader();
const tex = {};
for (const [k, uri] of Object.entries(M.textures)) {
  const t = loader.load(uri); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.flipY = false;
  t.anisotropy = renderer.capabilities.getMaxAnisotropy(); tex[k] = t;
}
const mats = M.materials.map(m => {
  const mat = new THREE.MeshStandardMaterial({color: new THREE.Color().fromArray(m.color),
    roughness: m.rough, metalness: m.metal});
  if (m.map) { mat.map = tex[m.map]; mat.map.encoding = THREE.sRGBEncoding; }
  if (m.normal) { mat.normalMap = tex[m.normal]; mat.normalScale.set(1, -1); }
  return mat;
});
for (const p of M.prims) {
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.Float32BufferAttribute(p.pos, 3));
  g.setAttribute("normal", new THREE.Float32BufferAttribute(p.nrm, 3));
  g.setAttribute("uv", new THREE.Float32BufferAttribute(p.uv, 2));
  g.setIndex(p.idx);
  scene.add(new THREE.Mesh(g, mats[p.mat]));
}
const grid = new THREE.GridHelper(4, 8, 0x9a9184, 0xc9c1b4); grid.position.y = -0.001; scene.add(grid);
const hw = 0.7, hd = 0.5, y = 0.002;
const slab = new THREE.BufferGeometry().setFromPoints([[-hw, -hd], [hw, -hd], [hw, hd], [-hw, hd], [-hw, -hd]]
  .map(([x, z]) => new THREE.Vector3(x, y, z)));
scene.add(new THREE.Line(slab, new THREE.LineBasicMaterial({color: 0xd63031})));
// a minimal orbit control: yaw/pitch round the bed, distance by wheel or pinch
const target = new THREE.Vector3(0, 0.12, 0);
let yaw = -0.7, pitch = 0.55, dist = 3.2;
const ptrs = new Map(); let pinch0 = 0, dist0 = dist;
function place() {
  pitch = Math.max(-0.1, Math.min(1.5, pitch)); dist = Math.max(0.8, Math.min(9, dist));
  camera.position.set(target.x + dist * Math.cos(pitch) * Math.sin(yaw), target.y + dist * Math.sin(pitch),
                      target.z + dist * Math.cos(pitch) * Math.cos(yaw));
  camera.lookAt(target);
}
canvas.addEventListener("pointerdown", e => { canvas.setPointerCapture(e.pointerId); ptrs.set(e.pointerId, [e.clientX, e.clientY]);
  if (ptrs.size === 2) { const [a, b] = [...ptrs.values()]; pinch0 = Math.hypot(a[0] - b[0], a[1] - b[1]); dist0 = dist; } });
canvas.addEventListener("pointermove", e => {
  if (!ptrs.has(e.pointerId)) return;
  const [px, py] = ptrs.get(e.pointerId); ptrs.set(e.pointerId, [e.clientX, e.clientY]);
  if (ptrs.size === 1) { yaw -= (e.clientX - px) * 0.008; pitch += (e.clientY - py) * 0.008; }
  else if (ptrs.size === 2 && pinch0 > 0) { const [a, b] = [...ptrs.values()]; dist = dist0 * pinch0 / Math.max(1, Math.hypot(a[0] - b[0], a[1] - b[1])); }
  place(); });
const up = e => { ptrs.delete(e.pointerId); pinch0 = 0; };
canvas.addEventListener("pointerup", up); canvas.addEventListener("pointercancel", up);
canvas.addEventListener("wheel", e => { e.preventDefault(); dist *= Math.exp(e.deltaY * 0.001); place(); }, {passive: false});
function resize() { const w = window.innerWidth, h = window.innerHeight; renderer.setSize(w, h, false);
  camera.aspect = w / h; camera.updateProjectionMatrix(); }
window.addEventListener("resize", resize); resize(); place();
renderer.setAnimationLoop(() => renderer.render(scene, camera));
</script></body></html>
"""


def main(out):
    m = model()
    with open(os.path.join(HERE, "exports", "bed_stats.json")) as fh:
        s = json.load(fh)
    sub = ("Padded bolster bed with a flat fleece cushion: %d triangles, %.2f x %.2f x %.2f m, cushion top at %.2f m."
           % (s["tris"], *s["size_xyz_m"], s["rest_point_m"][2]))
    html = PAGE.replace("__THREE__", THREE).replace("__SUB__", sub).replace(
        "__MODEL__", json.dumps(m, separators=(",", ":")))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as fh:
        fh.write(html)
    print("viewer ->", out, "%.2f MB" % (os.path.getsize(out) / 1e6))


if __name__ == "__main__":
    main(os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "out", "viewer.html"))
