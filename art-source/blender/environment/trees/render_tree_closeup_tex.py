"""Textured maple + cherry at the round-2 close-up framing.
Usage: python3 render_tree_closeup_tex.py <out.png>"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_trees_tex as TT  # noqa
import dh_env as E  # noqa
import dh_house_kit as K  # noqa
import dh_scene as S  # noqa
import bpy  # noqa
OUT = sys.argv[1]
RES = (1800, 1200)
K.reset_scene()
m = TT.tree_maple("large"); m.location = (-4.2, 0, 0)
c = TT.tree_cherry("large"); c.location = (4.2, -0.5, 0)
S.dog_box((0.3, -2.6, 0), yaw_deg=-25)
E.lawn_patch(-30, 30, -30, 30)
K.setup_render(OUT, res=RES, bg="#E8EEF2", samples=64)
K.frame_camera([m, c], margin=1.08, yaw=-30, pitch=24, res=RES)
K.render()
print("WROTE", OUT, "tris", K.tri_count(m), K.tri_count(c))
