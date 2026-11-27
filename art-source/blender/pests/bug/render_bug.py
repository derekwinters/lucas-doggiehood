"""Review renders for the bug (#334) (EEVEE, headless; Mesa lavapipe when there is no GPU).

  python3 render_bug.py <shot> [outdir]     shot = gamecam | closeup | video | sheet

  gamecam   a swarm of SWARM_COUNT bugs over the approved base-1 house (level 1) beside the approved
            adult Golden Retriever, at the game camera (ortho, yaw -40, pitch 45): wide and zoomed
  closeup   the bug from several angles (turnaround), with its size and triangle count
  video     flap.mp4: the WingFlap clip, 3/4 view, 1280x720, H.264 yuv420p, the loop repeated
            VIDEO_LOOPS times (each frame rendered once, so every pass is identical)
  sheet     flap-contact-sheet.png: every frame of one loop, front view, plus frame 24 (== frame 0)

outdir defaults to out/ here (git-ignored); the committed renders are in review/. The house and
dog are read from their committed exports under art-source/blender. The bug is built by dh_bug.py
and posed with its own clip function (the same numbers the export samples). Renders are not
byte-reproducible. PNGs are re-saved with Pillow, which writes no metadata chunks.
"""
import math
import os
import shutil
import subprocess
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))          # art-source/blender
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ART, "dogs", "kit"))           # the shared render helpers (read-only)
import dh_bug as B  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import dh_dog_render as R  # noqa: E402

OUT = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(HERE, "out")
TMP = os.path.join(OUT, "_parts")
HOUSE = os.path.join(ART, "exports", "base-01-level1.glb")
DOG = os.path.join(ART, "dogs", "golden-retriever", "exports", "dog_golden_default.gltf")
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)
GAME_YAW, GAME_PITCH = -40, 45

# the review swarm: a loose ring over the roof, like today's BugSwarmView (5 bugs, 1.1 m spread)
SWARM_COUNT = 5
SWARM_RADIUS = 1.1
SWARM_CLEARANCE = 0.9        # above the measured roof top
SWARM_HEIGHTS = (0.0, 0.35, 0.12, 0.45, 0.22)
SWARM_FRAMES = (0, 5, 9, 14, 20)     # each bug at a different point of its flap
CLOSE_HALF = 2.4             # half-size of the box the zoomed shot frames, around the roof top
DOG_AT, DOG_YAW = (1.6, -5.6, 0.0), -25

VIDEO_RES = (1280, 720)
VIDEO_LOOPS = 4              # 4 x 0.8 s = 3.2 s
SHADOW_DROP = 0.45           # the floor under the hovering bug in the close-ups


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def header(img, title, sub=None, h=96):
    out = Image.new("RGB", (img.width, img.height + h), BG)
    out.paste(img, (0, h))
    d = ImageDraw.Draw(out)
    d.text((28, 18), title, fill=INK, font=font(38, True))
    if sub:
        d.text((30, 62), sub, fill=SUB, font=font(22))
    return out


def save(img, name):
    """Save through Pillow: IHDR / IDAT / IEND only, no text or C2PA chunks."""
    p = os.path.join(OUT, name)
    img.convert("RGB").save(p, "PNG", optimize=True)
    print("wrote", p)


def scene(path, res, samples=64, floor_hex=R.FLOOR, floor_z=0.0, **kw):
    R.reset()
    R.setup(path, res=res, samples=samples, **kw)
    if floor_hex:
        R.floor(60, floor_hex, z=floor_z)


def bug(at=(0, 0, 0), yaw=0.0, frame=0, name=B.NAME):
    parts = B.build(name)
    B.pose(parts, frame)
    parts["root"].location = at
    parts["root"].rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()
    return parts


def meshes(parts):
    return [o for o in parts.values() if o.type == "MESH"]


def import_gltf(path, at=(0, 0, 0), yaw=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler.z += math.radians(yaw)
    bpy.context.view_layer.update()
    return new


def stats():
    import json
    p = os.path.join(HERE, "exports", "bug_stats.json")
    return json.load(open(p)) if os.path.exists(p) else None


# ---------------------------------------------------------------------------
def gamecam():
    shots = []
    for tag, res, close in (("wide", (1500, 1100), False), ("close", (1500, 1000), True)):
        path = os.path.join(TMP, "game_%s.png" % tag)
        scene(path, res, samples=48, floor_hex="#8E9E68")
        house = [o for o in import_gltf(HOUSE) if o.type == "MESH"]
        pts = R.world_points(house)
        roof = max(p.z for p in pts)
        cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
        cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
        dog = [o for o in import_gltf(DOG, at=DOG_AT, yaw=DOG_YAW) if o.type == "MESH"]
        swarm = []
        for i in range(SWARM_COUNT):
            a = 2 * math.pi * i / SWARM_COUNT
            at = (cx + SWARM_RADIUS * math.cos(a), cy + SWARM_RADIUS * math.sin(a),
                  roof + SWARM_CLEARANCE + SWARM_HEIGHTS[i])
            swarm += meshes(bug(at, yaw=math.degrees(a) + 90 + 25 * (i % 2), frame=SWARM_FRAMES[i],
                                name="%s_%d" % (B.NAME, i)))
        frame_objs = (house + dog + swarm) if not close else \
            [R.box_object((cx - CLOSE_HALF, cy - CLOSE_HALF, roof - CLOSE_HALF), (cx + CLOSE_HALF, cy + CLOSE_HALF,
                                                                               roof + CLOSE_HALF))]
        R.camera(frame_objs, GAME_YAW, GAME_PITCH, res, margin=1.06 if not close else 1.0)
        R.render()
        shots.append(Image.open(path).convert("RGB"))
    W = 1600
    wide, close = [s.resize((W, int(s.height * W / s.width)), Image.LANCZOS) for s in shots]
    sheet = Image.new("RGB", (W, wide.height + close.height + 60), BG)
    sheet.paste(wide, (0, 0))
    d = ImageDraw.Draw(sheet)
    d.text((20, wide.height + 14), "Same camera, zoomed in on the roof: %d bugs, each at a different point "
           "of its wing beat" % SWARM_COUNT, fill=INK, font=font(21, True))
    sheet.paste(close, (0, wide.height + 60))
    st = stats()
    tri = st["tris"]["total"] if st else 0
    save(header(sheet, "Bug swarm at the game camera (ortho, yaw -40, pitch 45)",
                "%d bugs %.1f m over the base-1 roof, beside the adult Golden Retriever; %d triangles per bug"
                % (SWARM_COUNT, SWARM_CLEARANCE, tri)), "gamecam.png")


def closeup():
    st = stats()
    views = [("3/4 front", -40, 18, (1000, 760), (0, 0), 9), ("Side", -90, 0, (600, 380), (1000, 0), 9),
             ("Top", 0, 89.9, (600, 380), (1000, 380), 9), ("Front, wings up", 0, 4, (533, 420), (0, 770), 0),
             ("Front, wings down", 0, 4, (533, 420), (533, 770), 3), ("Back 3/4", 145, 24, (534, 420), (1066, 770), 9)]
    W, H = 1600, 1190
    sheet = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    for i, (name, yaw, pitch, res, (x, y), frame) in enumerate(views):
        path = os.path.join(TMP, "close_%d.png" % i)
        scene(path, res, samples=48, floor_z=-SHADOW_DROP)
        parts = bug(frame=frame)
        cam = R.camera(meshes(parts), yaw, pitch, res, margin=1.18)
        R.render()
        im = Image.open(path).convert("RGB")
        if name == "Side":
            di = ImageDraw.Draw(im)
            ppm = res[0] / cam.data.ortho_scale * 0.1
            x0, y0 = res[0] - 30 - ppm, res[1] - 30
            di.rectangle((x0, y0, x0 + ppm, y0 + 8), fill=INK)
            di.text((x0 - 10, y0 - 28), "10 cm", fill=INK, font=font(20, True))
        sheet.paste(im, (x, y))
        d.text((x + 16, y + 10), name, fill=INK, font=font(26, True))
    sub = ("%d triangles (body %d, each wing %d); %.2f x %.2f x %.2f m (wingspan x length x height, at rest)"
           % (st["tris"]["total"], st["tris"]["body"], st["tris"]["wing_L"], *st["size_xyz_m"])) if st else None
    save(header(sheet, "Bug close-up: a chunky cartoon fly", sub), "closeup.png")


def stage(path, res, yaw, pitch, samples=32):
    scene(path, res, samples=samples, floor_z=-SHADOW_DROP)
    parts = bug(frame=0)
    # frame on the bug's full flap envelope, so the camera does not move between frames
    env = []
    for f in (0, 3):
        B.pose(parts, f)
        bpy.context.view_layer.update()
        env += R.world_points(meshes(parts))
    lo = [min(p[i] for p in env) for i in range(3)]
    hi = [max(p[i] for p in env) for i in range(3)]
    box = R.box_object(lo, hi)
    R.camera([box], yaw, pitch, res, margin=1.5)
    box.hide_render = True
    return parts


def video():
    tmp = os.path.join(OUT, "_video")
    os.makedirs(tmp, exist_ok=True)
    parts = stage(os.path.join(tmp, "f.png"), VIDEO_RES, -40, 18)
    sc = bpy.context.scene
    for f in range(B.CLIP_FRAMES):
        sc.render.filepath = os.path.join(tmp, "f%03d.png" % f)
        if os.path.exists(sc.render.filepath):     # resume: EEVEE on lavapipe can crash mid-run
            continue
        B.pose(parts, f)
        bpy.context.view_layer.update()
        R.render()
    lst = os.path.join(tmp, "list.txt")
    with open(lst, "w") as fh:
        for _ in range(VIDEO_LOOPS):
            for f in range(B.CLIP_FRAMES):
                fh.write("file 'f%03d.png'\nduration %.6f\n" % (f, 1.0 / B.FPS))
        fh.write("file 'f000.png'\n")
    mp4 = os.path.join(OUT, "flap.mp4")
    subprocess.run([shutil.which("ffmpeg"), "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst,
                    "-r", str(B.FPS), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-preset", "medium",
                    "-map_metadata", "-1", "-movflags", "+faststart", "-vf", "scale=%d:%d" % VIDEO_RES, mp4],
                   check=True)
    shutil.rmtree(tmp)
    print("wrote", mp4)


def sheet():
    tmp = os.path.join(OUT, "_sheet")
    os.makedirs(tmp, exist_ok=True)
    res = (360, 300)
    parts = stage(os.path.join(tmp, "f.png"), res, 0, 6, samples=16)
    sc = bpy.context.scene
    frames = list(range(B.CLIP_FRAMES + 1))
    cols = 6
    rows = (len(frames) + cols - 1) // cols
    W, H = cols * res[0], rows * (res[1] + 34)
    out = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(out)
    for k, f in enumerate(frames):
        B.pose(parts, f)
        bpy.context.view_layer.update()
        p = os.path.join(tmp, "f%03d.png" % f)
        sc.render.filepath = p
        R.render()
        x, y = (k % cols) * res[0], (k // cols) * (res[1] + 34)
        out.paste(Image.open(p).convert("RGB"), (x, y + 34))
        bob, pitch, flap = B.clip_values(f)
        lab = "f%d%s  wings %+.0f\u00b0  bob %+.1f cm" % (f, " (= f0)" if f == B.CLIP_FRAMES else "",
                                                         -math.degrees(flap), bob * 100)
        d.text((x + 10, y + 8), lab, fill=INK if f < B.CLIP_FRAMES else (180, 40, 40), font=font(16, True))
    shutil.rmtree(tmp)
    save(header(out, "WingFlap: every frame of one loop (front view)",
                "%d frames at %d fps (%.1f s), %d wing beats and one body bob per loop; frame %d equals frame 0. "
                "Wings: + is up"
                % (B.CLIP_FRAMES, B.FPS, B.CLIP_FRAMES / B.FPS, B.FLAP_BEATS, B.CLIP_FRAMES)),
         "flap-contact-sheet.png")


SHOTS = {"gamecam": gamecam, "closeup": closeup, "video": video, "sheet": sheet}

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in SHOTS:
        sys.exit(__doc__)
    os.makedirs(TMP, exist_ok=True)
    SHOTS[sys.argv[1]]()
