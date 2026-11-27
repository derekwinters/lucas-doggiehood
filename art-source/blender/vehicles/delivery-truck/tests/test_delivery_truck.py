"""Checks on the committed delivery-truck export (#924).

  python3 -m unittest discover -s art-source/blender/vehicles/delivery-truck/tests

Invariant - the Blender truck has today's footprint: its length and width are
the Kenney "delivery" model's at DeliveryTruckFootprint.ModelScale, read from
Core, so routing, crosswalk stops and car-following do not change (#660, #672).

Everything except the rebuild test reads the committed .glb with plain Python
(no Blender). RebuildIsByteIdentical needs bpy==5.3.0 and is skipped without it.
"""
import hashlib
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
ASSET = os.path.dirname(TESTS)
sys.path.insert(0, ASSET)

import truck_check as C  # noqa: E402
import truck_dims as D  # noqa: E402

GLB = os.path.join(ASSET, "exports", "delivery_truck.glb")
TOL = 1e-3
WHEELS = ("wheel_front_left", "wheel_front_right", "wheel_rear_left", "wheel_rear_right")
DOOR = "door_rear"
ROOT = "delivery_truck"
DECALS = ("decal_delivery_left", "decal_delivery_right")
LETTERING_MAT = "DH_Truck_lettering"
BODY_MAT = "DH_Truck_body"
UNTINTED = ("DH_Truck_glass", "DH_Truck_tyre", "DH_Truck_trim")


class CoreFootprint(unittest.TestCase):
    """Today's truck, as Core declares it (DeliveryTruckFootprint.cs)."""

    def test_reads_todays_kenney_footprint_from_core(self):
        length, width = C.core_footprint()
        self.assertAlmostEqual(length, 3.25 * 1.5, places=6)   # 4.875 m
        self.assertAlmostEqual(width, 1.5 * 1.5, places=6)     # 2.25 m


class Footprint(unittest.TestCase):
    def setUp(self):
        self.g = C.load(GLB)

    def test_length_matches_todays_truck(self):
        length, _ = C.core_footprint()
        lo, hi = C.scene_bounds(self.g)
        # glTF is Y-up with the front at +Z: length runs along Z
        self.assertAlmostEqual(hi[2] - lo[2], length, delta=TOL)

    def test_width_matches_todays_truck(self):
        _, width = C.core_footprint()
        lo, hi = C.scene_bounds(self.g)
        self.assertAlmostEqual(hi[0] - lo[0], width, delta=TOL)

    def test_footprint_is_centred_on_the_pivot_and_sits_on_the_ground(self):
        lo, hi = C.scene_bounds(self.g)
        self.assertAlmostEqual(lo[0] + hi[0], 0.0, delta=TOL)
        self.assertAlmostEqual(lo[2] + hi[2], 0.0, delta=TOL)
        self.assertAlmostEqual(lo[1], 0.0, delta=TOL)

    def test_height_matches_todays_kenney_truck(self):
        # 1.65 m at import x ModelScale 1.5 = 2.475 m; nothing (door ribs, lamps) pokes above the roof
        lo, hi = C.scene_bounds(self.g)
        self.assertAlmostEqual(hi[1] - lo[1], 2.475, delta=TOL)

    def test_fits_inside_its_lane(self):
        # DeliveryTruckFootprint.FitsInsideItsLane: RoadWidth/4 + width/2 <= RoadWidth/2 (#672)
        road = C.core_road_width()
        lo, hi = C.scene_bounds(self.g)
        self.assertLessEqual(road / 4 + (hi[0] - lo[0]) / 2, road / 2 + 1e-6)


class Budget(unittest.TestCase):
    def setUp(self):
        self.g = C.load(GLB)

    def test_every_mesh_is_under_the_enforced_per_mesh_cap(self):
        cap = C.core_max_tris_per_mesh()
        for name, tris in C.tris_per_node(self.g).items():
            self.assertLessEqual(tris, cap, name)

    def test_whole_truck_is_within_the_provisional_art_budget(self):
        self.assertLessEqual(sum(C.tris_per_node(self.g).values()), D.BUDGET_TRIS)


class Parts(unittest.TestCase):
    def setUp(self):
        self.g = C.load(GLB)
        self.nodes = C.nodes_by_name(self.g)

    def test_node_set_is_body_wheels_door_and_lettering_only(self):
        self.assertEqual(sorted(self.nodes), sorted((ROOT,) + WHEELS + (DOOR,) + DECALS))
        kids = sorted(self.g["nodes"][i]["name"] for i in self.nodes[ROOT].get("children", []))
        self.assertEqual(kids, sorted(WHEELS + (DOOR,) + DECALS))

    def test_wheel_pivots_are_on_their_axles(self):
        for w in WHEELS:
            # the node sits at the recorded axle centre (Blender -> glTF axes)
            self.assertTrue(C.close(self.nodes[w]["translation"], C.to_gltf(D.WHEEL_PIVOTS[w])), w)
            # and the wheel is round about it: radial bounds centred on 0 (glTF Y and Z)
            lo, hi = C.mesh_bounds(self.g, self.nodes[w]["mesh"])
            self.assertAlmostEqual(lo[1] + hi[1], 0.0, delta=TOL, msg=w)
            self.assertAlmostEqual(lo[2] + hi[2], 0.0, delta=TOL, msg=w)
            self.assertAlmostEqual(hi[1], D.WHEEL_RADIUS, delta=TOL, msg=w)

    def test_wheels_touch_the_ground(self):
        for w in WHEELS:
            lo, _ = C.node_world_bounds(self.g, w)
            self.assertAlmostEqual(lo[1], 0.0, delta=TOL, msg=w)

    def test_door_pivot_is_on_its_hinge_edge(self):
        node = self.nodes[DOOR]
        self.assertTrue(C.close(node["translation"], C.to_gltf(D.DOOR_HINGE)))
        lo, hi = C.mesh_bounds(self.g, node["mesh"])
        # the door hangs off one vertical edge: that edge (local X = 0) is the hinge line
        self.assertAlmostEqual(hi[0], 0.0, delta=TOL)
        self.assertAlmostEqual(lo[0], -D.DOOR_WIDTH, delta=TOL)
        self.assertAlmostEqual(lo[1], 0.0, delta=TOL)

    def test_open_door_stays_inside_the_trucks_width(self):
        _, width = C.core_footprint()
        for x in C.door_open_x(self.g, D.DOOR_OPEN_DEG):
            self.assertLessEqual(abs(x), width / 2 + TOL)

    def test_exports_closed_and_at_rest(self):
        for n in WHEELS + (DOOR,) + DECALS:
            self.assertNotIn("rotation", self.nodes[n], n)
            self.assertNotIn("scale", self.nodes[n], n)


class Materials(unittest.TestCase):
    def setUp(self):
        self.g = C.load(GLB)

    def test_body_is_one_white_material_for_multiply_tint(self):
        mats = C.materials_by_name(self.g)
        self.assertIn(BODY_MAT, mats)
        factor = mats[BODY_MAT].get("pbrMetallicRoughness", {}).get("baseColorFactor", [1, 1, 1, 1])
        self.assertTrue(C.close(factor, [1, 1, 1, 1]))
        self.assertNotIn("baseColorTexture", mats[BODY_MAT].get("pbrMetallicRoughness", {}))

    def test_glass_tyres_and_trim_are_separate_untinted_materials(self):
        used = C.materials_per_node(self.g)
        for m in UNTINTED:
            self.assertIn(m, C.materials_by_name(self.g))
        for w in WHEELS:
            self.assertNotIn(BODY_MAT, used[w], w)
            self.assertIn("DH_Truck_tyre", used[w], w)
        self.assertIn("DH_Truck_glass", used[ROOT])
        self.assertIn("DH_Truck_trim", used[ROOT])

    def test_only_the_body_and_door_carry_the_tint_material(self):
        used = C.materials_per_node(self.g)
        tinted = sorted(n for n, ms in used.items() if BODY_MAT in ms)
        self.assertEqual(tinted, sorted((ROOT, DOOR)))


def _sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


class Lettering(unittest.TestCase):
    """"Delivery" on both long sides of the cargo box (#924, Derek's review)."""

    def setUp(self):
        self.g = C.load(GLB)

    def test_one_decal_on_each_long_side_of_the_cargo_box(self):
        _, width = C.core_footprint()
        lo, hi = C.node_world_bounds(self.g, "decal_delivery_left")
        self.assertAlmostEqual(hi[0], width / 2, delta=TOL)          # truck's left is +X
        self.assertGreater(lo[0], width / 2 - 0.02)
        lo, hi = C.node_world_bounds(self.g, "decal_delivery_right")
        self.assertAlmostEqual(lo[0], -width / 2, delta=TOL)
        self.assertLess(hi[0], -width / 2 + 0.02)
        for d in DECALS:
            lo, hi = C.node_world_bounds(self.g, d)
            self.assertGreater(lo[2], -D.BOX_REAR_Y, d)              # on the cargo box: behind the cab,
            self.assertLess(hi[2], -D.BOX_FRONT_Y, d)                # ahead of the rear face (glTF Z = -y)
            self.assertGreater(lo[1], D.AXLE_Z + D.ARCH_RADIUS, d)   # above the wheel arches
            self.assertLess(hi[1], D.HEIGHT, d)

    def test_lettering_faces_outward_on_each_side(self):
        # an outward face is what makes the word read correctly (not mirrored) from that side
        for d, sign in (("decal_delivery_left", 1), ("decal_delivery_right", -1)):
            normals = C.face_normals_world(self.g, d)
            self.assertTrue(normals, d)
            self.assertTrue(all(n[0] * sign > 0.99 for n in normals), d)

    def test_lettering_reads_front_to_back_on_the_left_and_back_to_front_on_the_right(self):
        # the word starts at the reader's left: toward the cab on the left side, toward the rear
        # on the right side. The only descender in "Delivery" is the final "y", so the lowest
        # point of the lettering marks the end of the word.
        for d, front_first in (("decal_delivery_left", True), ("decal_delivery_right", False)):
            self.assertEqual(C.word_starts_at_front(self.g, d), front_first, d)

    def test_lettering_has_its_own_untinted_materials(self):
        used = C.materials_per_node(self.g)
        for d in DECALS:
            self.assertIn(LETTERING_MAT, used[d], d)
            self.assertNotIn(BODY_MAT, used[d], d)
        self.assertEqual(sorted(n for n, ms in used.items() if LETTERING_MAT in ms), sorted(DECALS))


@unittest.skipUnless(importlib.util.find_spec("bpy"), "needs bpy==5.3.0")
class RebuildIsByteIdentical(unittest.TestCase):
    """Invariant - the committed export is reproducible from the script."""

    def test_export_twice_in_fresh_processes_matches_committed(self):
        want = _sha(GLB)
        for _ in range(2):
            with tempfile.TemporaryDirectory() as tmp:
                subprocess.run([sys.executable, os.path.join(ASSET, "export_truck.py"), tmp],
                               check=True, capture_output=True)
                got = _sha(os.path.join(tmp, "delivery_truck.glb"))
                self.assertEqual(got, want)


if __name__ == "__main__":
    unittest.main()
