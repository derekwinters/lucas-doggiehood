"""Dressed street with only the trees swapped for the textured ones.
Usage: python3 render_street_tex.py <dressed|closeup> <out.png>"""
import os, runpy, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_trees_tex as TT  # noqa
TT.install_into_env()
sys.argv = [os.path.join(HERE, "render_street.py"), sys.argv[1], sys.argv[2]]
runpy.run_path(os.path.join(HERE, "render_street.py"), run_name="__main__")
