"""Write the single-file three.js viewer for the 3D splash street (no bpy).

    python3 art-source/splash-3d/build_viewer.py EXPORT_DIR OUT_HTML

Embeds EXPORT_DIR/splash_houses.glb (base64) and splash_camera.json into one HTML page.
three.js 0.160.0 loads from jsDelivr through an import map.
"""
import base64
import json
import os
import sys

TEMPLATE = r"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Splash Street 3D</title>
<style>
:root {
  --bg: #eef4f8; --panel: #ffffff; --text: #1d2733; --muted: #5b6878; --accent: #2f7fd1;
  --accent-text: #ffffff; --border: #d3dde7; --sky: #8fd6f2;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --bg: #141a21; --panel: #1e2630; --text: #e6edf3; --muted: #9aa8b6; --accent: #5aa6ef;
    --accent-text: #0d1620; --border: #2e3a47; --sky: #2b5f78;
  }
}
:root[data-theme="dark"] {
  --bg: #141a21; --panel: #1e2630; --text: #e6edf3; --muted: #9aa8b6; --accent: #5aa6ef;
  --accent-text: #0d1620; --border: #2e3a47; --sky: #2b5f78;
}
* { box-sizing: border-box; }
html, body { margin: 0; }
body { background: var(--bg); color: var(--text); font: 15px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif; }
main { max-width: 1280px; margin: 0 auto; padding: 16px; }
h1 { font-size: 20px; margin: 0 0 4px; }
p.lead { margin: 0 0 12px; color: var(--muted); }
.stage { position: relative; width: 100%; aspect-ratio: 2816 / 1536; background: var(--sky);
  border: 1px solid var(--border); border-radius: 10px; overflow: hidden; }
.stage canvas { display: block; width: 100%; height: 100%; touch-action: none; }
.bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin: 12px 0; }
button, select { font: inherit; padding: 7px 12px; border-radius: 8px; border: 1px solid var(--border);
  background: var(--panel); color: var(--text); cursor: pointer; min-height: 36px; }
button.primary { background: var(--accent); color: var(--accent-text); border-color: var(--accent); }
label { display: inline-flex; gap: 6px; align-items: center; color: var(--muted); }
.note { color: var(--muted); font-size: 13px; }
#status { position: absolute; left: 12px; top: 10px; font-size: 13px; color: #10202c;
  background: rgba(255,255,255,0.75); padding: 2px 8px; border-radius: 6px; }
</style>
</head>
<body>
<main>
  <h1>Splash street in 3D</h1>
  <p class="lead">The 11 splash houses rebuilt as real 3D models, each facing the street. Drag to orbit, scroll or pinch to zoom.
  "Splash camera" snaps back to the camera solved from the painting.</p>
  <div class="bar">
    <button class="primary" id="splash">Splash camera</button>
    <select id="house" aria-label="House"><option value="">Pick a house...</option></select>
    <label><input type="checkbox" id="isolate"> Show only this house</label>
    <label><input type="checkbox" id="toon" checked> Toon shading</label>
  </div>
  <div class="stage" id="stage"><div id="status">Loading...</div></div>
  <p class="note">Camera: __CAMNOTE__. Models: Blender scripts in art-source/splash-3d.</p>
</main>
<script type="importmap">
{ "imports": {
  "three": "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js",
  "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"
} }
</script>
<script type="module">
import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";

const CAM = __CAMJSON__;
const GLB = "__GLB__";
const W = 2816, H = 1536;

const stage = document.getElementById("stage");
const status = document.getElementById("status");
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
stage.appendChild(renderer.domElement);
const scene = new THREE.Scene();
scene.add(new THREE.HemisphereLight(0xdff3ff, 0x6f8f3a, 1.4));
const sun = new THREE.DirectionalLight(0xffffff, 2.2);
sun.position.set(0.22, 0.75, 0.62).multiplyScalar(50);   // behind the splash camera, above
scene.add(sun);

const vfov = 2 * Math.atan(H / 2 / CAM.f) * 180 / Math.PI;
const camera = new THREE.PerspectiveCamera(vfov, W / H, 0.1, 2000);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.addEventListener("start", () => camera.clearViewOffset());

function splash() {
  // the painting's camera: level, looking down the street, lens shifted so the horizon
  // and the vanishing point sit where the painting puts them
  camera.fov = vfov; camera.aspect = W / H;
  camera.position.set(0, CAM.h, 0);
  controls.target.set(0, CAM.h, -60);
  camera.lookAt(controls.target);
  camera.setViewOffset(W, H, W / 2 - CAM.cx, H / 2 - CAM.vy, W, H);
  camera.updateProjectionMatrix();
}

const gradient = new THREE.DataTexture(new Uint8Array([150, 150, 150, 255, 215, 215, 215, 255, 255, 255, 255, 255]), 3, 1);
gradient.minFilter = gradient.magFilter = THREE.NearestFilter;
gradient.needsUpdate = true;
const originals = new Map();
function setToon(on) {
  scene.traverse(o => {
    if (!o.isMesh) return;
    if (!originals.has(o)) originals.set(o, o.material);
    const m = originals.get(o);
    o.material = on ? (m.userData.toon ||= new THREE.MeshToonMaterial({ color: m.color, gradientMap: gradient })) : m;
  });
}

const houses = {};
const sel = document.getElementById("house");
const bytes = Uint8Array.from(atob(GLB), c => c.charCodeAt(0));
new GLTFLoader().parse(bytes.buffer, "", gltf => {
  scene.add(gltf.scene);
  gltf.scene.traverse(o => {
    if (o.name.startsWith("house_") && !o.isMesh && o.parent === gltf.scene) houses[o.name.slice(6)] = o;
  });
  for (const [k, v] of Object.entries(CAM.houses)) {
    const opt = document.createElement("option"); opt.value = k; opt.textContent = v.label; sel.appendChild(opt);
  }
  setToon(true);
  splash();
  status.textContent = "";
  status.style.display = "none";
}, err => { status.textContent = "Could not load the model: " + err; });

function showOnly(key) {
  for (const o of gltf_children()) {
    const isHouse = o.name.startsWith("house_");
    o.visible = !key || (isHouse ? o.name === "house_" + key : o.name.startsWith("ground"));
  }
}
function gltf_children() { return scene.children.flatMap(c => c.isGroup || c.type === "Group" ? c.children : []); }

sel.addEventListener("change", () => {
  const k = sel.value;
  showOnly(document.getElementById("isolate").checked ? k : "");
  if (!k) return splash();
  const box = new THREE.Box3().setFromObject(houses[k]);
  const c = box.getCenter(new THREE.Vector3());
  const r = box.getSize(new THREE.Vector3()).length();
  const street = c.x < 0 ? 1 : -1;   // the house's front faces the road (toward x = 0)
  camera.clearViewOffset();
  controls.target.copy(c);
  camera.position.set(c.x + street * r * 1.1, c.y + r * 0.55, c.z + r * 0.6);
  camera.updateProjectionMatrix();
});
document.getElementById("isolate").addEventListener("change", e => showOnly(e.target.checked ? sel.value : ""));
document.getElementById("splash").addEventListener("click", () => { sel.value = ""; showOnly(""); splash(); });
document.getElementById("toon").addEventListener("change", e => setToon(e.target.checked));

function resize() {
  const w = stage.clientWidth, h = stage.clientHeight;
  renderer.setSize(w, h, false);
}
new ResizeObserver(resize).observe(stage);
resize();
renderer.setAnimationLoop(() => { controls.update(); renderer.render(scene, camera); });
</script>
</body>
</html>
"""


def main():
    exp, out = sys.argv[1], sys.argv[2]
    with open(os.path.join(exp, "splash_houses.glb"), "rb") as f:
        glb = base64.b64encode(f.read()).decode()
    with open(os.path.join(exp, "splash_camera.json")) as f:
        cam = json.load(f)
    note = (f"{cam['lens_mm']:.1f} mm lens on a 36 mm sensor ({cam['hfov_deg']:.1f} deg wide), "
            f"{cam['h']:.1f} m above the sidewalk, level, vanishing point at ({cam['cx']:.0f}, {cam['vy']:.0f}) px")
    html = (TEMPLATE.replace("__GLB__", glb).replace("__CAMJSON__", json.dumps(cam))
            .replace("__CAMNOTE__", note))
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w") as f:
        f.write(html)
    print("wrote", out, len(html) // 1024, "KB")


if __name__ == "__main__":
    main()
