"""Write a self-contained web viewer for the cushion: viewer.html with the export embedded.

  python3 art-source/blender/items/cushion/make_viewer.py [out.html]   (default: out/viewer.html)

The committed exports/item_cushion.gltf is embedded with its .bin as a base64 data URI, so the one
HTML file works on its own (three.js 0.160.0 and its GLTFLoader / OrbitControls load from
cdn.jsdelivr.net, pinned). It can be orbited and zoomed, and shows today's 1.4 x 1.0 m slab
footprint as an outline on the ground. The viewer is for review only and is not committed (out/ is
git-ignored). Needs no Blender.
"""
import base64
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
EXP = os.path.join(HERE, "exports")
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "out", "viewer.html")
THREE = "https://cdn.jsdelivr.net/npm/three@0.160.0"

PAGE = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Cushion model viewer</title>
<style>
:root { --bg: #f1ece3; --ink: #28241f; --sub: #69625a; --panel: rgba(255,255,255,0.78); --line: #8a8178; }
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) { --bg: #1f2124; --ink: #ece7de; --sub: #a59e94; --panel: rgba(30,32,36,0.8); --line: #8f8a82; }
}
:root[data-theme="dark"] { --bg: #1f2124; --ink: #ece7de; --sub: #a59e94; --panel: rgba(30,32,36,0.8); --line: #8f8a82; }
html, body { margin: 0; height: 100%%; background: var(--bg); color: var(--ink);
  font: 15px/1.4 system-ui, -apple-system, "Segoe UI", sans-serif; overflow: hidden; }
#view { position: fixed; inset: 0; }
#info { position: fixed; left: 16px; right: 16px; top: 12px; max-width: 540px; padding: 10px 14px;
  background: var(--panel); border-radius: 10px; }
#info h1 { font-size: 17px; margin: 0 0 2px; }
#info p { margin: 0; color: var(--sub); font-size: 13px; }
#controls { position: fixed; left: 16px; bottom: 16px; display: flex; gap: 8px; flex-wrap: wrap; }
button { font: inherit; font-size: 14px; padding: 8px 14px; border-radius: 8px; border: 1px solid var(--sub);
  background: var(--panel); color: var(--ink); }
</style>
</head>
<body>
<div id="view"></div>
<div id="info"><h1>Cushion (#412): puffy tufted floor cushion</h1>
<p>%(tris)d triangles &middot; %(sx).2f &times; %(sy).2f &times; %(sz).2f m, rest top %(top).2f m &middot;
dashed line: today's 1.4 &times; 1.0 m slab. Drag to orbit, pinch or scroll to zoom.</p></div>
<div id="controls"><button id="spin">Turntable: on</button><button id="slab">Slab outline: on</button>
<button id="cam">Game camera</button></div>
<script type="importmap">
{ "imports": { "three": "%(three)s/build/three.module.js", "three/addons/": "%(three)s/examples/jsm/" } }
</script>
<script type="module">
import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
const GLTF = %(gltf)s;
const css = (k) => getComputedStyle(document.documentElement).getPropertyValue(k).trim();
const el = document.getElementById("view");
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
el.appendChild(renderer.domElement);
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(35, 1, 0.01, 50);
camera.position.set(-1.8, 1.6, 2.2);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 0.12, 0);
controls.enableDamping = true;
controls.autoRotate = true;
controls.autoRotateSpeed = 1.2;
scene.add(new THREE.HemisphereLight(0xffffff, 0x8a7f70, 1.6));
const sun = new THREE.DirectionalLight(0xfff2dd, 2.2);
sun.position.set(-1.5, 2.5, 2);
scene.add(sun);
const ground = new THREE.Mesh(new THREE.CircleGeometry(1.2, 64),
  new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.10 }));
ground.rotation.x = -Math.PI / 2; ground.position.y = -0.001; scene.add(ground);
const slabPts = [[-0.7, -0.5], [0.7, -0.5], [0.7, 0.5], [-0.7, 0.5], [-0.7, -0.5]]
  .map(([x, z]) => new THREE.Vector3(x, 0.002, z));
const slab = new THREE.Line(new THREE.BufferGeometry().setFromPoints(slabPts),
  new THREE.LineDashedMaterial({ color: new THREE.Color(css("--line")), dashSize: 0.05, gapSize: 0.035 }));
slab.computeLineDistances(); scene.add(slab);
new GLTFLoader().parse(JSON.stringify(GLTF), "", (g) => { scene.add(g.scene); },
  (e) => { document.querySelector("#info p").textContent = "Could not load the model: " + e; });
function resize() {
  const w = el.clientWidth, h = el.clientHeight;
  renderer.setSize(w, h); camera.aspect = w / h;
  camera.fov = w < h ? 64 : 35; camera.updateProjectionMatrix();
}
window.addEventListener("resize", resize); resize();
renderer.setAnimationLoop(() => { controls.update(); renderer.render(scene, camera); });
document.getElementById("spin").onclick = (e) => {
  controls.autoRotate = !controls.autoRotate; e.target.textContent = "Turntable: " + (controls.autoRotate ? "on" : "off");
};
document.getElementById("slab").onclick = (e) => {
  slab.visible = !slab.visible; e.target.textContent = "Slab outline: " + (slab.visible ? "on" : "off");
};
document.getElementById("cam").onclick = () => {
  // the game camera's direction: pitch 45, yaw -40 (from the front-left); glTF front is +Z
  const d = 2.6, p = Math.PI / 4, y = -40 * Math.PI / 180;
  controls.autoRotate = false; document.getElementById("spin").textContent = "Turntable: off";
  camera.position.set(Math.sin(y) * Math.cos(p) * d, 0.12 + Math.sin(p) * d, Math.cos(y) * Math.cos(p) * d);
  controls.target.set(0, 0.12, 0);
};
</script>
</body>
</html>
"""


def main():
    with open(os.path.join(EXP, "item_cushion.gltf")) as fh:
        j = json.load(fh)
    with open(os.path.join(EXP, j["buffers"][0]["uri"]), "rb") as fh:
        binb = fh.read()
    j["buffers"][0]["uri"] = "data:application/octet-stream;base64," + base64.b64encode(binb).decode()
    with open(os.path.join(EXP, "cushion_stats.json")) as fh:
        st = json.load(fh)
    sx, sy, sz = st["size_xyz_m"]
    html = PAGE % {"gltf": json.dumps(j, separators=(",", ":")), "three": THREE, "tris": st["tris"]["total"],
                   "sx": sx, "sy": sy, "sz": sz, "top": st["rest_top_m"]}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as fh:
        fh.write(html)
    print("wrote %s (%.0f KB)" % (OUT, os.path.getsize(OUT) / 1024))


if __name__ == "__main__":
    main()
