"""Fit check: the five LotFence runs around a level-4 house on the starter
corner lot, for the given bases, with the garage on the road side and on the
neighbour side. Prints a summary and writes fit_check.json.
Usage: python3 fit_check.py [OUT_DIR] [--bases 2,7]
OUT_DIR defaults to this folder's out/ (git-ignored)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_fence_fit as FF  # noqa: E402
import dh_fences as F  # noqa: E402

args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = args[0] if args else os.path.join(HERE, "out")
os.makedirs(OUT, exist_ok=True)
BASES = [2, 7]
for i, a in enumerate(sys.argv):
    if a == "--bases":
        BASES = [int(x) for x in sys.argv[i + 1].split(",")]

results = []
for n in BASES:
    for toward in (True, False):
        lot = FF.build_lot(n, toward)
        r = FF.check(lot)
        for run in r["runs"]:
            c = F.piece_count(run["length_m"])
            run["pieces"] = c
            run["piece_len_m"] = round(run["length_m"] / c, 3)
            run["compression"] = round(run["length_m"] / c / F.NOMINAL_LEN, 3)
        results.append(r)
        print("\n=== base %d %s, garage %s ===" % (n, r["name"], "toward side road" if toward else "toward neighbour"))
        print("fence rect x %s..%s, rear y %s; yard width %.2f; placement %s" % (
            r["fence_rect"][0], r["fence_rect"][1], r["fence_rect"][3], r["yard_width_m"], r["placement_clearance"]))
        for run in r["runs"]:
            print("  %-15s L=%6.3f  pieces=%d @ %.3f (x%.3f)  %s" % (
                run["run"], run["length_m"], run["pieces"], run["piece_len_m"], run["compression"],
                run.get("clearance_m", "")))
        for k, c in r["connectors"].items():
            print("  %s: first contact %s at x=%s (%.3f m from corner; literal %.3f m, %.3f m past contact);"
                  " literal end inside %s; side %s" % (
                      k, c.get("first_contact"), c.get("first_contact_x"), c.get("length_to_first_contact_m", -1),
                      c["literal_length_m"], c.get("overlap_past_first_contact_m", 0), c["literal_end_inside"],
                      c["side_clearance_m"]))
        print("  ground (plan) %s; crosses driveway/walkway: %s" % (r["ground"], r["crosses_driveway_or_walkway"]))
with open(os.path.join(OUT, "fit_check.json"), "w") as f:
    json.dump(results, f, indent=1)
print("\nwrote", os.path.join(OUT, "fit_check.json"))
