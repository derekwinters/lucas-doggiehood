"""Textured-tree sampler: reuses render_samplers.py with the textured trees.
Usage: python3 render_samplers_tex.py <out.png>"""
import os, runpy, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_trees_tex as TT  # noqa
import dh_env as E  # noqa
E.PIECES = {k: (fn, "trees", label) for k, (fn, label) in TT.PIECES.items()}
sys.argv = [os.path.join(HERE, "render_samplers.py"), "trees", sys.argv[1]]
runpy.run_path(os.path.join(HERE, "render_samplers.py"), run_name="__main__")
