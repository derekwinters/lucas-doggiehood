"""A self-contained 3-D viewer page for the blanket export (#413), for review.

  python3 art-source/blender/items/blanket/make_viewer.py [out_dir]

Reads exports/blanket.glb (textures already embedded), inlines it as base64, and writes
<out_dir>/blanket-viewer.html (default: this folder's out/, git-ignored): three.js r128 from
cdnjs, GLTFLoader and OrbitControls from jsdelivr (three@0.128.0, as the dog viewers), the .glb
inline in the page, orbit/zoom controls, a ground disc with the 1.4 x 1.0 m slab outline, and a
light or dark background following the viewer's colour scheme. Plain Python, no Blender.
"""
import base64
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
EXP = os.path.join(HERE, "exports")
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "out")
THREE = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"
LOADER = "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js"
ORBIT = "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"


def glb_base64():
    with open(os.path.join(EXP, "blanket.glb"), "rb") as fh:
        return base64.b64encode(fh.read()).decode()


PAGE = r"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Blanket Model Viewer</title>
<script src="__THREE__"></script>
<script src="__LOADER__"></script>
<script src="__ORBIT__"></script>
<style>
:root { --bg: #f3f4ee; --surface: #ffffff; --stage: #e7eadf; --fg: #26291f; --muted: #64695a; --line: #d6d9cc;
  --accent: #2f5d8c; color-scheme: light; }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --bg: #1a1c16; --surface: #23261e; --stage: #2b2f25; --fg: #ecede4; --muted: #a3a895; --line: #3a3e32;
  --accent: #8fb6e0; color-scheme: dark; } }
:root[data-theme="dark"] { --bg: #1a1c16; --surface: #23261e; --stage: #2b2f25; --fg: #ecede4; --muted: #a3a895;
  --line: #3a3e32; --accent: #8fb6e0; color-scheme: dark; }
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--fg); font: 15px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
.wrap { max-width: 1100px; margin: 0 auto; padding: 24px 16px 40px; display: grid; gap: 18px; }
h1 { margin: 0; font-size: clamp(24px, 4vw, 34px); line-height: 1.15; }
.lead { margin: 4px 0 0; color: var(--muted); max-width: 70ch; }
.bench { display: grid; grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr); gap: 16px; }
@media (max-width: 760px) { .bench { grid-template-columns: minmax(0, 1fr); } }
.stage { position: relative; background: var(--stage); border-radius: 16px; overflow: hidden; min-width: 0; }
#view { display: block; width: 100%; height: clamp(320px, 60vh, 560px); touch-action: none; cursor: grab; }
.hint { position: absolute; right: 12px; top: 10px; font-size: 12px; color: var(--muted); }
.btns { position: absolute; left: 12px; bottom: 12px; display: flex; gap: 6px; flex-wrap: wrap; }
.btns button { font: 500 13px system-ui, sans-serif; border: 1px solid var(--line); background: var(--surface); color: var(--fg);
  border-radius: 999px; padding: 6px 12px; cursor: pointer; }
.btns button[aria-pressed="true"] { box-shadow: 0 0 0 2px var(--accent) inset; border-color: var(--accent); }
.sheet { background: var(--surface); border: 1px solid var(--line); border-radius: 16px; padding: 18px; min-width: 0; }
.sheet dl { margin: 0; display: grid; grid-template-columns: auto 1fr; gap: 8px 14px; }
.sheet dt { color: var(--muted); font-size: 13px; }
.sheet dd { margin: 0; text-align: right; font-variant-numeric: tabular-nums; font-weight: 500; }
.sheet p { color: var(--muted); font-size: 13px; margin: 14px 0 0; }
</style>
</head>
<body>
<div class="wrap">
  <header>
    <h1>Blanket</h1>
    <p class="lead">Plaid wool throw for the comfort decorations (#413): fringed short ends, a rumpled lie and the
    front-right corner folded back to show the cream fleece reverse. The white line is today's 1.4 &times; 1.0 m graybox slab footprint.</p>
  </header>
  <section class="bench">
    <div class="stage">
      <canvas id="view" aria-label="3-D view of the blanket; drag to orbit, scroll or pinch to zoom"></canvas>
      <div class="hint">drag to orbit &middot; scroll to zoom</div>
      <div class="btns">
        <button id="b-game" aria-pressed="true">Game camera</button>
        <button id="b-close" aria-pressed="false">Close-up</button>
        <button id="b-top" aria-pressed="false">Top</button>
        <button id="b-slab" aria-pressed="true">Slab outline</button>
      </div>
    </div>
    <aside class="sheet">
      <dl>
        <dt>Size (incl. fringe)</dt><dd>__SIZE__</dd>
        <dt>Cloth</dt><dd>__CLOTH__</dd>
        <dt>Slab footprint</dt><dd>1.40 &times; 1.00 m</dd>
        <dt>Triangles</dt><dd>__TRIS__</dd>
        <dt>Provisional backstop (#799)</dt><dd>6,000</dd>
        <dt>Materials</dt><dd>plaid, fleece back, fringe</dd>
      </dl>
      <p>Units are metres at in-game size. Pivot on the ground at the footprint centre. The export is
      <code>exports/blanket.glb</code>, embedded here so the page stands alone.</p>
    </aside>
  </section>
</div>
<script>
const GLB_B64 = "__GLB__";
const canvas = document.getElementById('view');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
renderer.outputEncoding = THREE.sRGBEncoding;
renderer.shadowMap.enabled = true;
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(32, 1, 0.01, 100);
const controls = new THREE.OrbitControls(camera, canvas);
controls.target.set(0, 0.02, 0);
controls.enableDamping = true;
controls.minDistance = 0.5; controls.maxDistance = 8;
controls.maxPolarAngle = Math.PI / 2 - 0.02;
scene.add(new THREE.HemisphereLight(0xf4f1ea, 0x5a6b3a, 0.75));
const sun = new THREE.DirectionalLight(0xfff4e0, 0.9);
sun.position.set(-1.6, 3.0, 2.0); sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
Object.assign(sun.shadow.camera, { left: -1.5, right: 1.5, top: 1.5, bottom: -1.5 });
scene.add(sun);
const ground = new THREE.Mesh(new THREE.CircleGeometry(2.2, 64), new THREE.MeshStandardMaterial({ color: 0x3f6a28, roughness: 1 }));
ground.rotation.x = -Math.PI / 2; ground.position.y = -0.001; ground.receiveShadow = true; scene.add(ground);
const slabPts = [[-0.7, 0.5], [0.7, 0.5], [0.7, -0.5], [-0.7, -0.5], [-0.7, 0.5]].map(p => new THREE.Vector3(p[0], 0.002, p[1]));
const slab = new THREE.Line(new THREE.BufferGeometry().setFromPoints(slabPts), new THREE.LineBasicMaterial({ color: 0xffffff }));
scene.add(slab);
const glbBytes = Uint8Array.from(atob(GLB_B64), c => c.charCodeAt(0));
new THREE.GLTFLoader().parse(glbBytes.buffer, '', g => {
  g.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  scene.add(g.scene);
}, e => console.error(e));
// game camera: orthographic look at pitch 45, yaw -40 (front-left), shown here in perspective
const views = {
  // Blender (x, y, z) is glTF (x, z, -y); the game camera sits front-left, so at Blender -x, -y
  game: () => { const d = 3.2, yaw = 40 * Math.PI / 180, p = 45 * Math.PI / 180;
    camera.position.set(-d * Math.cos(p) * Math.sin(yaw), d * Math.sin(p), d * Math.cos(p) * Math.cos(yaw)); },
  close: () => camera.position.set(0.9, 0.55, 1.25),
  top: () => camera.position.set(0, 3.0, 0.0001),
};
function setView(k) {
  views[k](); controls.target.set(0, 0.02, 0); controls.update();
  ['game', 'close', 'top'].forEach(n => document.getElementById('b-' + n).setAttribute('aria-pressed', String(n === k)));
}
['game', 'close', 'top'].forEach(n => document.getElementById('b-' + n).addEventListener('click', () => setView(n)));
document.getElementById('b-slab').addEventListener('click', e => {
  slab.visible = !slab.visible; e.currentTarget.setAttribute('aria-pressed', String(slab.visible)); });
setView('game');
function resize() {
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (canvas.width !== Math.round(w * renderer.getPixelRatio()) || canvas.height !== Math.round(h * renderer.getPixelRatio())) {
    renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }
}
(function loop() { resize(); controls.update(); renderer.render(scene, camera); requestAnimationFrame(loop); })();
</script>
</body>
</html>
"""


def main():
    with open(os.path.join(EXP, "blanket_stats.json")) as fh:
        st = json.load(fh)
    sx, sy, sz = st["size_xyz_m"]
    page = (PAGE.replace("__THREE__", THREE).replace("__LOADER__", LOADER).replace("__ORBIT__", ORBIT)
            .replace("__SIZE__", "%.2f &times; %.2f &times; %.3f m" % (sx, sy, sz))
            .replace("__CLOTH__", "1.24 &times; 0.92 m")
            .replace("__TRIS__", "{:,}".format(st["tris"]))
            .replace("__GLB__", glb_base64()))
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, "blanket-viewer.html")
    with open(p, "w") as fh:
        fh.write(page)
    print("wrote", p, os.path.getsize(p), "bytes")


if __name__ == "__main__":
    main()
