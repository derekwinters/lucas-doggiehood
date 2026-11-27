"""Export the 3D splash street as glTF binaries.

    python3 art-source/splash-3d/export_glb.py OUT_DIR [--toe-in 0]

Writes OUT_DIR/splash_houses.glb (the whole street: houses, ground, props, the splash
camera), OUT_DIR/house_<key>.glb per house (pivot at the centre of its front wall's foot,
front facing -Y in Blender / +Z in glTF), and OUT_DIR/splash_camera.json (the solved
camera, for viewers that need the lens shift glTF cannot carry).
"""
import argparse
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_scene as B  # noqa: E402
import splash_data as SD  # noqa: E402


def export(path, selection=False):
    bpy.ops.export_scene.gltf(
        filepath=path, export_format="GLB", use_selection=selection, export_apply=True,
        export_yup=True, export_cameras=not selection, export_lights=False,
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out_dir")
    ap.add_argument("--toe-in", type=float, default=0.0)
    ap.add_argument("--focal", type=float, default=None)
    a = ap.parse_args()
    os.makedirs(a.out_dir, exist_ok=True)
    cam, houses = B.build("export", a.focal, a.toe_in)
    export(os.path.join(a.out_dir, "splash_houses.glb"))
    for key, root in houses.items():
        bpy.ops.object.select_all(action="DESELECT")
        loc, rot = root.location.copy(), root.rotation_euler.copy()
        root.location = (0, 0, 0)
        root.rotation_euler = (0, 0, 0)
        root.select_set(True)
        for ch in root.children_recursive:
            ch.select_set(True)
        bpy.context.view_layer.update()
        export(os.path.join(a.out_dir, f"house_{key}.glb"), selection=True)
        root.location, root.rotation_euler = loc, rot
    info = {k: v for k, v in cam.items()}
    info["houses"] = {
        k: {"label": next(h["label"] for h in SD.HOUSES if h["key"] == k),
            "front_centre_blender": [r.location.x, r.location.y, 0.0]}
        for k, r in houses.items()
    }
    with open(os.path.join(a.out_dir, "splash_camera.json"), "w") as f:
        json.dump(info, f, indent=1)
    print("exported", len(houses), "houses to", a.out_dir)


if __name__ == "__main__":
    main()
