"""Render "before" tiles with the round-3 builder (<DH_DOG_ROUNDS>/round3/scripts; not in the repo).

  python3 render_round3.py <round3 scripts dir> <out dir> '<json job>'
job = [[file stem, [box lo, box hi], yaw, pitch, lens (0 = orthographic), [w, h], margin, pose, cut_y], ...];
cut_y (optional, front views): drop everything behind the plane y = cut_y (camera clip), to show a section;
each tile frames the same world box as the round-4 tile it is compared with.
"""
import json
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"
sys.path.insert(0, sys.argv[1])
import bpy  # noqa: E402

import dh_dog as D3  # noqa: E402  (round-3 builder)
import dh_dog_render as R3  # noqa: E402

out, job = sys.argv[2], json.loads(sys.argv[3])
for stem, (lo, hi), yaw, pitch, lens, res, margin, pose, *cut in job:
    R3.reset()
    R3.setup(os.path.join(out, stem + ".png"), res=tuple(res), samples=48)
    R3.floor(60)
    D3.build("default", pose=pose)
    box = R3.box_object(lo, hi)
    cam = R3.camera([box], yaw, pitch, tuple(res), ortho=not lens, lens=lens or 85, margin=margin)
    if cut and cut[0] is not None:
        cam.data.clip_end = cut[0] - cam.location.y
    R3.render()
