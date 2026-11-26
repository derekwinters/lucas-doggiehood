"""Review renders for the motion clips (EEVEE, headless; lavapipe when there is no GPU).

  python3 render_motion.py video <Clip> [outdir]     MP4: 1280x720, 3/4 view, H.264 yuv420p, loops 3x
                                                      (one-shot clips: 2x, with a short hold)
  python3 render_motion.py sheet <Clip> [outdir]     side-view contact sheet PNG (locomotion clips)
  python3 render_motion.py still <Clip> <frame> [out.png]

<Clip> is a clip name from dh_dog_clips (Idle, Walk, Run, SitDown, SitIdle, TailWag, PlayBow,
LieDown, RestIdle). Frames are rendered once per clip and the loops are repeated by ffmpeg, so
every pass of the loop is identical. For Walk and Run the floor's grid moves with the ground at
the clip's ground speed, so planted paws should look glued to it.
"""
import math
import os
import shutil
import subprocess
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_dog_anim as A  # noqa: E402  (puts the approved scripts on the path)
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import dh_dog_check as K  # noqa: E402
import dh_dog_clips as C  # noqa: E402
import dh_dog_render as R  # noqa: E402

OUTDEF = os.path.join(A.MOTION, "review")
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
RES = (1280, 720)
BG = "#E9E4DA"
FLOOR_A, FLOOR_B = "#C9C1B3", "#C0B7A8"
TILE = 0.5


def ffmpeg():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def build_clip(name):
    rig = A.Rig()
    meta = C.make(rig, name)
    return rig, meta


def floor_grid(size=40):
    """A light floor with a faint 0.5 m checker (object-space, so moving the object moves it)."""
    me = bpy.data.meshes.new("floor")
    s = size / 2
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    m = bpy.data.materials.new("floor")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.95
    tc = nt.nodes.new("ShaderNodeTexCoord")
    ck = nt.nodes.new("ShaderNodeTexChecker")
    ck.inputs["Scale"].default_value = 1.0
    ck.inputs["Color1"].default_value = R.hex_lin(FLOOR_A)
    ck.inputs["Color2"].default_value = R.hex_lin(FLOOR_B)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1 / (2 * TILE),) * 3
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], ck.inputs["Vector"])
    nt.links.new(ck.outputs["Color"], b.inputs["Base Color"])
    me.materials.append(m)
    ob = bpy.data.objects.new("floor", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def stage(rig, out_png, samples=8, sun_angle=14):
    sc = R.setup(out_png, res=RES, samples=samples, bg=BG, sun_yaw=-30, sun_pitch=55, energy=2.7)
    sc.render.fps = A.FPS
    bpy.data.objects["Sun"].data.angle = math.radians(sun_angle)    # soft shadow edge
    return sc, floor_grid()


def all_points(rig, act, frames, step=1):
    pts = []
    rig.arm.animation_data.action = act
    for f in range(0, frames + 1, step):
        bpy.context.scene.frame_set(f)
        for v in K.frame_points(rig).values():
            pts.append(v)
    return np.concatenate(pts)


def frame_camera(rig, act, frames, yaw, pitch, ortho=False, margin=1.12, lens=60):
    P = all_points(rig, act, frames, max(1, frames // 24))
    lo, hi = P.min(0), P.max(0)
    lo[2] = min(lo[2], 0.0) - 0.04
    box = R.box_object(tuple(lo), tuple(hi))
    return R.camera([box], yaw, pitch, RES, margin=margin, ortho=ortho, lens=lens)


def project(cam, p):
    co = world_to_camera_view(bpy.context.scene, cam, Vector(p))
    return co.x * RES[0], (1 - co.y) * RES[1]


def render_frames(rig, meta, cam, floor, tmp, frames):
    os.makedirs(tmp, exist_ok=True)
    sc = bpy.context.scene
    act = bpy.data.actions[meta["name"]]
    rig.arm.animation_data.action = act
    v = meta.get("ground_speed", 0.0)
    out = []
    for f in frames:
        sc.frame_set(f)
        if v:
            floor.location.y = (v * f / A.FPS) % (2 * TILE)
        p = os.path.join(tmp, "f%04d.png" % f)
        out.append(p)
        if os.path.exists(p):           # resume: EEVEE on lavapipe can crash after ~100 renders in
            continue                    # one process; rerunning the command picks up where it stopped
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
    return out


def video(name, outdir):
    rig, meta = build_clip(name)
    tmp = os.path.join(outdir, "_frames_%s" % name)
    sc, floor = stage(rig, os.path.join(tmp, "x.png"))
    act = bpy.data.actions[meta["name"]]
    cam = frame_camera(rig, act, meta["frames"], yaw=-52, pitch=16, margin=0.92)
    loop = meta["loop"]
    frames = list(range(meta["frames"] if loop else meta["frames"] + 1))
    paths = render_frames(rig, meta, cam, floor, tmp, frames)
    # sequence: loops x3 (a loop's last frame equals its first, so it is left out), or a one-shot
    # clip twice with a 0.5 s hold on its end pose
    seq = paths * 3 if loop else (paths + [paths[-1]] * 15) * 2
    lst = os.path.join(tmp, "list.txt")
    with open(lst, "w") as fh:
        for p in seq:
            fh.write("file '%s'\nduration %.6f\n" % (p, 1.0 / A.FPS))
    mp4 = os.path.join(outdir, "%s.mp4" % name.lower())
    subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst,
                    "-r", str(A.FPS), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
                    "-preset", "medium", "-movflags", "+faststart", "-vf", "scale=1280:720", mp4], check=True)
    shutil.rmtree(tmp)
    print("wrote", mp4)


def sheet(name, outdir, n=None):
    """Side view (ortho, level camera at ground height), frames along one cycle, ground line."""
    rig, meta = build_clip(name)
    tmp = os.path.join(outdir, "_sheet_%s" % name)
    sc, floor = stage(rig, os.path.join(tmp, "x.png"), samples=16)
    act = bpy.data.actions[meta["name"]]
    cam = frame_camera(rig, act, meta["frames"], yaw=-90, pitch=0, ortho=True, margin=1.06)
    N = meta["frames"]
    n = n or (10 if N >= 20 else 8)
    frames = sorted({int(round(i * N / n)) for i in range(n)})
    paths = render_frames(rig, meta, cam, floor, tmp, frames)
    plants = meta.get("plants", {})
    y0 = project(cam, (0, 0, 0))[1]
    tw, th, lab = 640, 360, 44
    cols = 4 if len(frames) > 9 else (4 if len(frames) == 8 else 3)
    rows = (len(frames) + cols - 1) // cols
    head = 92
    W, H = cols * tw, head + rows * (th + lab + 8)
    sheet_im = Image.new("RGB", (W, H), (244, 241, 234))
    d = ImageDraw.Draw(sheet_im)
    d.text((16, 12), "%s - side view, one cycle (%d frames, %.2f s), ground speed %.2f m/s" %
           (name, N, N / A.FPS, meta.get("ground_speed", 0.0)), fill=(40, 36, 32), font=font(30, True))
    d.text((16, 54), "Red line: ground z = 0 (orthographic camera, level with the ground). Paws: P = planted, "
           "- = swinging, for LF RF LH RH (L = the dog's left, the side facing the camera).",
           fill=(105, 98, 90), font=font(19))
    names = {"FL": "LF", "FR": "RF", "HL": "LH", "HR": "RH"}
    for i, (f, p) in enumerate(zip(frames, paths)):
        im = Image.open(p).convert("RGB")
        dr = ImageDraw.Draw(im, "RGBA")
        dr.line((0, y0, RES[0], y0), fill=(214, 48, 49, 255), width=3)
        st = []
        for leg in ("FL", "FR", "HL", "HR"):
            on = any(a <= f <= b for a, b in plants.get(leg, []))
            st.append("%s %s" % (names[leg], "P" if on else "-"))
        im = im.resize((tw, th), Image.LANCZOS)
        x0, yy = (i % cols) * tw, head + (i // cols) * (th + lab + 8)
        d.text((x0 + 10, yy + 8), "f%d (%.0f%%)" % (f, 100.0 * f / N), fill=(40, 36, 32), font=font(24, True))
        d.text((x0 + 190, yy + 11), "   ".join(st), fill=(40, 36, 32), font=font(20))
        sheet_im.paste(im, (x0, yy + lab))
    out = os.path.join(outdir, "%s-contact-sheet.png" % name.lower())
    sheet_im.save(out)
    shutil.rmtree(tmp)
    print("wrote", out)


def still(name, frame, out):
    rig, meta = build_clip(name)
    sc, floor = stage(rig, out)
    act = bpy.data.actions[meta["name"]]
    cam = frame_camera(rig, act, meta["frames"], yaw=-52, pitch=16, margin=0.92)
    rig.arm.animation_data.action = act
    sc.frame_set(frame)
    v = meta.get("ground_speed", 0.0)
    if v:
        floor.location.y = (v * frame / A.FPS) % (2 * TILE)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("wrote", out)


if __name__ == "__main__":
    mode, name = sys.argv[1], sys.argv[2]
    if mode == "still":
        still(name, int(sys.argv[3]), os.path.abspath(sys.argv[4]) if len(sys.argv) > 4 else "/tmp/still.png")
    else:
        od = os.path.abspath(sys.argv[3]) if len(sys.argv) > 3 else OUTDEF
        os.makedirs(od, exist_ok=True)
        {"video": video, "sheet": sheet}[mode](name, od)
