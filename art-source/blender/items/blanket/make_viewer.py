"""A self-contained 3-D viewer page for the blanket exports (#413), for review.

  python3 art-source/blender/items/blanket/make_viewer.py [out_dir]

Reads every exports/blanket_<colourway>.glb (textures already embedded), inlines them as base64,
and writes <out_dir>/blanket-viewer.html (default: this folder's out/, git-ignored): three.js
r128 from cdnjs, GLTFLoader and OrbitControls from jsdelivr (three@0.128.0, as the dog viewers),
orbit/zoom controls, a colourway switcher, a ground disc with the placement footprint drawn and
labelled at its size from blanket_spec, and a light or dark page following the viewer's colour
scheme. Plain Python, no Blender.
"""
import base64
import html
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import blanket_spec as S  # noqa: E402

EXP = os.path.join(HERE, "exports")
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "out")
THREE = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"
LOADER = "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js"
ORBIT = "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"
GROUND_R = 2.6                  # metres, the ground disc under the blanket


def glb_base64(key):
    with open(os.path.join(EXP, "blanket_%s.glb" % key), "rb") as fh:
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
.row { position: absolute; left: 12px; right: 12px; display: flex; gap: 6px; flex-wrap: wrap; }
.row.top { top: 10px; right: 150px; }
.row.bottom { bottom: 12px; }
.row button { font: 500 13px system-ui, sans-serif; border: 1px solid var(--line); background: var(--surface); color: var(--fg);
  border-radius: 999px; padding: 6px 12px; cursor: pointer; display: inline-flex; align-items: center; gap: 7px; }
.row button[aria-pressed="true"] { box-shadow: 0 0 0 2px var(--accent) inset; border-color: var(--accent); }
.row button:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.sw { width: 14px; height: 14px; border-radius: 50%; border: 1px solid rgba(0,0,0,.2); flex: none; }
.sheet { background: var(--surface); border: 1px solid var(--line); border-radius: 16px; padding: 18px; min-width: 0; }
.sheet dl { margin: 0; display: grid; grid-template-columns: auto 1fr; gap: 8px 14px; }
.sheet dt { color: var(--muted); font-size: 13px; }
.sheet dd { margin: 0; text-align: right; font-variant-numeric: tabular-nums; font-weight: 500; }
.sheet p { color: var(--muted); font-size: 13px; margin: 14px 0 0; }
.key { display: inline-block; width: 18px; height: 3px; background: #fff; border: 1px solid #999; vertical-align: middle; margin-right: 6px; }
</style>
</head>
<body>
<div class="wrap">
  <header>
    <h1>Blanket</h1>
    <p class="lead">Plaid wool throw for the comfort decorations (#413): __CLOTH__ cloth, gently rumpled, a short fringe on the
    short ends and a cream fleece underside. One mesh in __N__ colourways; the game picks one at random when a blanket is gifted (#932).</p>
  </header>
  <section class="bench">
    <div class="stage">
      <canvas id="view" aria-label="3-D view of the blanket; drag to orbit, scroll or pinch to zoom"></canvas>
      <div class="hint">drag to orbit &middot; scroll to zoom</div>
      <div class="row top" role="group" aria-label="Colourway">__SWATCHES__</div>
      <div class="row bottom">
        <button id="b-game" aria-pressed="true">Game camera</button>
        <button id="b-close" aria-pressed="false">Close-up</button>
        <button id="b-top" aria-pressed="false">Top</button>
        <button id="b-slab" aria-pressed="true">Footprint outline</button>
      </div>
    </div>
    <aside class="sheet">
      <dl>
        <dt>Cloth</dt><dd>__CLOTH__</dd>
        <dt>Placement footprint</dt><dd>__FOOT__</dd>
        <dt>Measured size</dt><dd>__SIZE__</dd>
        <dt>Triangles</dt><dd>__TRIS__</dd>
        <dt>Provisional backstop (#799)</dt><dd>6,000</dd>
        <dt>Colourway</dt><dd id="cw-name"></dd>
        <dt>File</dt><dd id="cw-file"></dd>
      </dl>
      <p><span class="key"></span>The white line on the grass is the placement footprint, __FOOT__ (cloth plus fringe).
      Today's graybox slab is 1.40 &times; 1.00 m, so #932 needs the larger footprint.</p>
      <p>Units are metres at in-game size. Pivot on the ground at the footprint centre. The exports are
      embedded here so the page stands alone.</p>
    </aside>
  </section>
</div>
<script>
const COLOURWAYS = __COLOURWAYS__;
const FOOT = __FOOTJS__;
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
controls.minDistance = 0.6; controls.maxDistance = 10;
controls.maxPolarAngle = Math.PI / 2 - 0.02;
scene.add(new THREE.HemisphereLight(0xf4f1ea, 0x5a6b3a, 0.75));
const sun = new THREE.DirectionalLight(0xfff4e0, 0.9);
sun.position.set(-2.0, 3.6, 2.4); sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
Object.assign(sun.shadow.camera, { left: -2, right: 2, top: 2, bottom: -2 });
scene.add(sun);
const ground = new THREE.Mesh(new THREE.CircleGeometry(__GROUND_R__, 64), new THREE.MeshStandardMaterial({ color: 0x3f6a28, roughness: 1 }));
ground.rotation.x = -Math.PI / 2; ground.position.y = -0.001; ground.receiveShadow = true; scene.add(ground);
// the placement footprint, outlined on the grass and labelled
const hw = FOOT[0] / 2, hd = FOOT[1] / 2;
const outline = new THREE.Group();
outline.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(
  [[-hw, hd], [hw, hd], [hw, -hd], [-hw, -hd], [-hw, hd]].map(p => new THREE.Vector3(p[0], 0.002, p[1]))),
  new THREE.LineBasicMaterial({ color: 0xffffff })));
(function label() {
  const c = document.createElement('canvas'); c.width = 512; c.height = 64;
  const x = c.getContext('2d');
  x.font = '600 34px system-ui, sans-serif'; x.fillStyle = '#ffffff'; x.textAlign = 'center'; x.textBaseline = 'middle';
  x.fillText('footprint ' + FOOT[0].toFixed(2) + ' × ' + FOOT[1].toFixed(2) + ' m', 256, 32);
  const tex = new THREE.CanvasTexture(c); tex.encoding = THREE.sRGBEncoding;
  const m = new THREE.Mesh(new THREE.PlaneGeometry(1.2, 0.15), new THREE.MeshBasicMaterial({ map: tex, transparent: true }));
  m.rotation.x = -Math.PI / 2; m.position.set(0, 0.003, hd + 0.13); outline.add(m);
})();
scene.add(outline);
const loader = new THREE.GLTFLoader();
const models = {};
let current = null;
function show(key) {
  const cw = COLOURWAYS.find(c => c.key === key);
  document.querySelectorAll('[data-cw]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.cw === key)));
  document.getElementById('cw-name').textContent = cw.label;
  document.getElementById('cw-file').textContent = 'blanket_' + key + '.glb';
  const swap = obj => { if (current) scene.remove(current); current = obj; scene.add(obj); };
  if (models[key]) { swap(models[key]); return; }
  const bytes = Uint8Array.from(atob(cw.glb), ch => ch.charCodeAt(0));
  loader.parse(bytes.buffer, '', g => {
    g.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
    models[key] = g.scene; swap(g.scene);
  }, e => console.error(e));
}
document.querySelectorAll('[data-cw]').forEach(b => b.addEventListener('click', () => show(b.dataset.cw)));
// Blender (x, y, z) is glTF (x, z, -y); the game camera (pitch 45, yaw -40) sits front-left, at Blender -x, -y
const views = {
  game: () => { const d = 4.6, yaw = 40 * Math.PI / 180, p = 45 * Math.PI / 180;
    camera.position.set(-d * Math.cos(p) * Math.sin(yaw), d * Math.sin(p), d * Math.cos(p) * Math.cos(yaw)); },
  close: () => camera.position.set(1.3, 0.7, 1.7),
  top: () => camera.position.set(0, 4.4, 0.0001),
};
function setView(k) {
  views[k](); controls.target.set(0, 0.02, 0); controls.update();
  ['game', 'close', 'top'].forEach(n => document.getElementById('b-' + n).setAttribute('aria-pressed', String(n === k)));
}
['game', 'close', 'top'].forEach(n => document.getElementById('b-' + n).addEventListener('click', () => setView(n)));
document.getElementById('b-slab').addEventListener('click', e => {
  outline.visible = !outline.visible; e.currentTarget.setAttribute('aria-pressed', String(outline.visible)); });
setView('game');
show(COLOURWAYS[0].key);
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


def swatch(key):
    """The ground colour of a colourway's plaid, for its button."""
    import dh_blanket_tex as BT
    return BT.SETTS[key]["G"]


def main():
    with open(os.path.join(EXP, "blanket_stats.json")) as fh:
        st = json.load(fh)
    d = st["colourways"][S.DEFAULT]
    sx, sy, sz = d["size_xyz_m"]
    cws = [{"key": k, "label": label, "glb": glb_base64(k)} for k, label in S.COLOURWAYS]
    buttons = "".join('<button data-cw="%s" aria-pressed="false"><span class="sw" style="background:%s"></span>%s</button>'
                      % (k, swatch(k), html.escape(label)) for k, label in S.COLOURWAYS)
    page = (PAGE.replace("__THREE__", THREE).replace("__LOADER__", LOADER).replace("__ORBIT__", ORBIT)
            .replace("__SIZE__", "%.2f &times; %.2f &times; %.3f m" % (sx, sy, sz))
            .replace("__CLOTH__", "%.1f &times; %.1f m" % (S.CLOTH_W, S.CLOTH_D))
            .replace("__FOOTJS__", json.dumps([S.FOOTPRINT_W, S.FOOTPRINT_D]))
            .replace("__FOOT__", "%.2f &times; %.2f m" % (S.FOOTPRINT_W, S.FOOTPRINT_D))
            .replace("__TRIS__", "{:,}".format(d["tris"]))
            .replace("__N__", str(len(S.COLOURWAYS)))
            .replace("__GROUND_R__", str(GROUND_R))
            .replace("__SWATCHES__", buttons)
            .replace("__COLOURWAYS__", json.dumps(cws, separators=(",", ":"))))
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, "blanket-viewer.html")
    with open(p, "w") as fh:
        fh.write(page)
    print("wrote", p, os.path.getsize(p), "bytes")


if __name__ == "__main__":
    main()
