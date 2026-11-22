"""Print the README table (tris, size, materials) from street_props_stats.json."""
import json, os, sys
st = json.load(open(sys.argv[1]))
print("| Piece | File | Tris | Size X x Y x Z (m) | Materials |")
print("|---|---|---:|---|---|")
tot = 0
for k, s in st.items():
    tot += s["tris"]
    mats = ", ".join(sorted(m.replace("DH_Prop_", "") for m in s["materials"]))
    x, y, z = s["size_xyz_m"]
    print("| %s | `%s.gltf` | %d | %.2f x %.2f x %.2f | %s |" % (s["label"], k, s["tris"], x, y, z, mats))
print("\nAll %d pieces: %d triangles in total." % (len(st), tot))
