"""Golden-only extra: render "before" tiles with the round-2 builder (<DH_DOG_ROUNDS>/round2/scripts;
not in the repo). The kit's render_dog.py runs it for the mouth sheet (breed.py RENDER["before"]).

  python3 render_round2.py <round2 scripts dir> <out dir> '<json job>'
job = [[file stem, [box lo, box hi], yaw, pitch, lens, [w, h], margin, pose, cut_y], ...] (the kit's
job format; pose and cut_y are ignored here); each tile frames the same world box as the tile it is
compared with.
"""
import json
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"
sys.path.insert(0, sys.argv[1])
import bpy  # noqa: E402

import dh_dog as D2  # noqa: E402  (round-2 builder)
import dh_dog_render as R2  # noqa: E402

out, job = sys.argv[2], json.loads(sys.argv[3])
for stem, (lo, hi), yaw, pitch, lens, res, margin, *_ in job:
    R2.reset()
    R2.setup(os.path.join(out, stem + ".png"), res=tuple(res), samples=48)
    R2.floor(60)
    D2.build("default")
    vs = [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    me = bpy.data.meshes.new("frame_box")
    me.from_pydata(vs, [], [])
    box = bpy.data.objects.new("frame_box", me)
    box.hide_render = True
    bpy.context.scene.collection.objects.link(box)
    R2.camera([box], yaw, pitch, tuple(res), ortho=False, lens=lens, margin=margin)
    R2.render()
