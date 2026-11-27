"""Write a self-contained web viewer for the bug: viewer.html with the export embedded.

  python3 art-source/blender/pests/bug/make_viewer.py [out.html]     (default: out/viewer.html)

The committed exports/pest_bug.gltf is embedded with its .bin as a base64 data URI, so the one
HTML file works on its own (three.js 0.160.0 and its GLTFLoader / OrbitControls load from
cdn.jsdelivr.net). It plays the WingFlap clip on a loop and can be orbited and zoomed. The viewer
is for review only and is not committed (out/ is git-ignored). Needs no Blender.
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
<title>Bug model viewer</title>
<style>
:root { --bg: #f1ece3; --ink: #28241f; --sub: #69625a; --panel: rgba(255,255,255,0.78); }
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) { --bg: #1f2124; --ink: #ece7de; --sub: #a59e94; --panel: rgba(30,32,36,0.8); }
}
:root[data-theme="dark"] { --bg: #1f2124; --ink: #ece7de; --sub: #a59e94; --panel: rgba(30,32,36,0.8); }
html, body { margin: 0; height: 100%%; background: var(--bg); color: var(--ink);
  font: 15px/1.4 system-ui, -apple-system, "Segoe UI", sans-serif; overflow: hidden; }
#view { position: fixed; inset: 0; }
#info { position: fixed; left: 16px; right: 16px; top: 12px; max-width: 520px; padding: 10px 14px;
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
<div id="info"><h1>Bug (#334): chunky cartoon fly</h1>
<p>%(tris)d triangles &middot; %(clip)s clip, %(frames)d frames at %(fps)d fps, looping. Drag to orbit, pinch or scroll to zoom.</p></div>
<div id="controls"><button id="play">Pause</button><button id="slow">Slow motion: off</button></div>
<script type="importmap">
{ "imports": { "three": "%(three)s/build/three.module.js", "three/addons/": "%(three)s/examples/jsm/" } }
</script>
<script type="module">
import * as THREE from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
const GLTF = %(gltf)s;
const el = document.getElementById("view");
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
el.appendChild(renderer.domElement);
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(35, 1, 0.01, 50);
camera.position.set(1.1, 0.7, 1.4);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 0, 0);
controls.enableDamping = true;
scene.add(new THREE.HemisphereLight(0xffffff, 0x8a7f70, 1.6));
const sun = new THREE.DirectionalLight(0xfff2dd, 2.2);
sun.position.set(-1.5, 2.5, 2);
scene.add(sun);
const ground = new THREE.Mesh(new THREE.CircleGeometry(0.6, 48),
  new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.12 }));
ground.rotation.x = -Math.PI / 2; ground.position.y = -0.45; scene.add(ground);
let mixer = null, action = null, speed = 1;
new GLTFLoader().parse(JSON.stringify(GLTF), "", (g) => {
  scene.add(g.scene);
  mixer = new THREE.AnimationMixer(g.scene);
  action = mixer.clipAction(g.animations[0]);
  action.setLoop(THREE.LoopRepeat, Infinity).play();
}, (e) => { document.querySelector("#info p").textContent = "Could not load the model: " + e; });
function resize() {
  const w = el.clientWidth, h = el.clientHeight;
  renderer.setSize(w, h); camera.aspect = w / h;
  camera.fov = w < h ? 50 : 35; camera.updateProjectionMatrix();
}
window.addEventListener("resize", resize); resize();
const clock = new THREE.Clock();
renderer.setAnimationLoop(() => {
  const dt = clock.getDelta();
  if (mixer) mixer.update(dt * speed);
  controls.update(); renderer.render(scene, camera);
});
document.getElementById("play").onclick = (e) => {
  if (!action) return; action.paused = !action.paused; e.target.textContent = action.paused ? "Play" : "Pause";
};
document.getElementById("slow").onclick = (e) => {
  speed = speed === 1 ? 0.15 : 1; e.target.textContent = "Slow motion: " + (speed === 1 ? "off" : "on");
};
</script>
</body>
</html>
"""


def main():
    j = json.load(open(os.path.join(EXP, "pest_bug.gltf")))
    binb = open(os.path.join(EXP, j["buffers"][0]["uri"]), "rb").read()
    j["buffers"][0]["uri"] = "data:application/octet-stream;base64," + base64.b64encode(binb).decode()
    st = json.load(open(os.path.join(EXP, "bug_stats.json")))
    html = PAGE % {"gltf": json.dumps(j, separators=(",", ":")), "three": THREE, "tris": st["tris"]["total"],
                   "clip": st["clip"]["name"], "frames": st["clip"]["frames"], "fps": st["clip"]["fps"]}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as fh:
        fh.write(html)
    print("wrote %s (%.0f KB)" % (OUT, os.path.getsize(OUT) / 1024))


if __name__ == "__main__":
    main()
