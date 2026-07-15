using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity.EditModeTests
{
    public class WorldBuilderTests
    {
        private GameObject root;

        [SetUp]
        public void BuildWorld()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            root = WorldBuilder.Build(GameState.CreateNew());
        }

        [TearDown]
        public void DestroyWorld()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Rebuilds the world through the graybox primitive path. The tests
        /// that assert primitive-specific geometry (verge/sidewalk strips,
        /// crosswalk quads, roof blocks, palette colors) now pin the
        /// fallback contract for when the Kenney kit assets can't be loaded
        /// — the kit path's equivalent contract lives in WorldKitArtTests.
        /// </summary>
        private void RebuildWithPrimitiveFallback()
        {
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            root = WorldBuilder.Build(GameState.CreateNew());
        }

        private IEnumerable<Transform> Children()
        {
            return root.transform.Cast<Transform>();
        }

        [Test]
        public void BuildsExactlyFourHouses_AtTheirCoreFrontSetbackPositions()
        {
            // #38: the scene contains exactly 4 houses on the #7 lots —
            // and since #127 each stands at Core's front-setback position
            // (pulled from the lot center toward its facing street so the
            // facade sits FrontSetback from the sidewalk's outer edge),
            // not on the raw lot center. The setback math itself is pinned
            // by HousePlacementTests in the Core suite; this pins that
            // WorldBuilder consumes it rather than lot.Position.
            var houses = Children().Where(t => t.name.StartsWith(WorldBuilder.HouseNamePrefix)).ToList();

            Assert.That(houses.Count, Is.EqualTo(4));

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var house = houses.SingleOrDefault(h => h.name == WorldBuilder.HouseNamePrefix + lot.HouseId);
                Assert.That(house, Is.Not.Null, $"missing house {lot.HouseId}");

                var expected = HousePlacement.Position(lot, WorldBuilder.HouseKitScale);
                Assert.That(house.position.x, Is.EqualTo(expected.X).Within(0.001f));
                Assert.That(house.position.z, Is.EqualTo(expected.Z).Within(0.001f));

                // Sanity that the contract is really the moved-toward-the-
                // street position, not the lot center it used to be.
                Assert.That(new Vector2(expected.X - lot.Position.X, expected.Z - lot.Position.Z).magnitude,
                    Is.GreaterThan(0.5f), $"house {lot.HouseId} should not sit on its lot center anymore");
            }
        }

        [Test]
        public void EveryHouse_HasAViewWithItsId_AndASinglePlainWallsBlock()
        {
            // #64: RoofShape/HasPorch (and their per-house hex colors) are
            // gone — the real per-house visual identity now comes from the
            // kit model + HouseStyleTable.TintVariant texture swap
            // (WorldKitArtTests), never built in this primitive-fallback
            // path. The fallback (only reached when the kit model itself
            // can't load) is simplified to one plain box — no procedural
            // roof/porch geometry keyed on removed fields.
            RebuildWithPrimitiveFallback();
            var views = root.GetComponentsInChildren<HouseView>();

            Assert.That(views.Length, Is.EqualTo(4));
            Assert.That(views.Select(v => v.HouseId), Is.EquivalentTo(new[] { 1, 2, 3, 4 }));

            foreach (var view in views)
            {
                var childNames = view.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
                Assert.That(childNames, Does.Contain("Walls"), $"house {view.HouseId} missing its fallback Walls block");
                Assert.That(childNames, Has.No.Member("Roof"), $"house {view.HouseId} still builds a removed Roof block");
                Assert.That(childNames, Has.No.Member("Porch"), $"house {view.HouseId} still builds a removed Porch block");
            }
        }

        [Test]
        public void BuildsBothRoads()
        {
            var roads = Children().Where(t => t.name.StartsWith(WorldBuilder.RoadNamePrefix)).ToList();

            Assert.That(roads.Count, Is.EqualTo(2));
        }

        [Test]
        public void BuildsSidewalkAndVergeStripsOnBothSidesOfEveryRoad()
        {
            // #106: symmetric placement — every road gets a sidewalk on
            // both sides, each split into two arm segments (one per
            // direction from the intersection) so the strip can stop at
            // the crossing road's own footprint instead of running through
            // it as one continuous piece. Primitive-fallback contract —
            // the kit tiles model their own sidewalks.
            //
            // Verge strips are back: GrassVergeWidth is 0.75m (Derek's
            // 2026-07-13 midpoint request — dogs at 4m sat "a little too
            // close to the road", so the walk line moved to 4.75m, between
            // the original 5.5m and the abutting 4m). In the kit path the
            // verge is a purely logical setback (the tiles render their
            // own pavement, no grass strip), but in THIS primitive
            // fallback the 0.75m grass strip legitimately renders again —
            // WorldBuilder's degenerate-geometry skip only applies at 0.
            RebuildWithPrimitiveFallback();
            var verges = Children().Where(t => t.name.StartsWith(WorldBuilder.VergeNamePrefix)).ToList();
            var sidewalks = Children().Where(t => t.name.StartsWith(WorldBuilder.SidewalkNamePrefix)).ToList();

            Assert.That(verges.Count, Is.EqualTo(NeighborhoodLayout.Roads.Count * 2 * 2),
                "verge strips render again in the fallback now that GrassVergeWidth is nonzero");
            Assert.That(sidewalks.Count, Is.EqualTo(NeighborhoodLayout.Roads.Count * 2 * 2));
        }

        [Test]
        public void SidewalkArms_NeverPaintOverTheCrossingRoadsOwnPavement()
        {
            // Regression: sidewalk strips used to run as one continuous
            // piece straight through the intersection, painting over the
            // crossing road's own pavement (Derek's playtest). Every
            // road's sidewalk line, sampled at the crossing road's own
            // centerline (squarely inside the crossing road's pavement
            // footprint), must now be covered by nothing.
            // Primitive-fallback contract (no strips exist in the kit
            // path). Verge strips are back (GrassVergeWidth 0.75m, Derek's
            // 2026-07-13 midpoint request) and are covered here too.
            RebuildWithPrimitiveFallback();
            var strips = Children()
                .Where(t => t.name.StartsWith(WorldBuilder.VergeNamePrefix) || t.name.StartsWith(WorldBuilder.SidewalkNamePrefix))
                .ToList();

            foreach (var road in NeighborhoodLayout.Roads)
            {
                foreach (var sidewalk in road.Sidewalks)
                {
                    AssertNothingCovers(strips, road.PointAt(0f, sidewalk.CenterOffset), $"sidewalk of {road.Orientation} {sidewalk.Side}");
                }
            }
        }

        private static void AssertNothingCovers(IEnumerable<Transform> strips, GridPoint point, string description)
        {
            var worldPoint = new Vector3(point.X, 0f, point.Z);

            foreach (var strip in strips)
            {
                var halfX = strip.localScale.x / 2f;
                var halfZ = strip.localScale.z / 2f;
                var covers = Mathf.Abs(worldPoint.x - strip.position.x) < halfX - 0.001f
                    && Mathf.Abs(worldPoint.z - strip.position.z) < halfZ - 0.001f;

                Assert.That(covers, Is.False,
                    $"{strip.name} paints over the crossing road's pavement at {worldPoint} ({description})");
            }
        }

        [Test]
        public void BuildsTheFourCrosswalks_OnePerRoadArm()
        {
            // Primitive-fallback contract — in the kit path the crosswalks
            // are road-crossing tiles (WorldKitArtTests).
            RebuildWithPrimitiveFallback();
            var crosswalks = Children().Where(t => t.name.StartsWith(WorldBuilder.CrosswalkNamePrefix)).ToList();

            Assert.That(crosswalks.Count, Is.EqualTo(4));
        }

        [Test]
        public void Crosswalks_NeverPaintOverSidewalkPavement()
        {
            // Regression (Derek's playtest, follow-up to the sidewalk
            // intersection fix): each Crosswalk edge in the walk network
            // runs sidewalk-center to sidewalk-center (+-4.75m at the
            // 0.75m verge) — that's the real distance a dog covers
            // crossing the road, and moving it would break graph
            // connectivity. But visually, the rendered crosswalk quad must
            // stop at the verge/sidewalk boundary
            // (RoadWidth/2 + GrassVergeWidth = 3.75m) and never cover the
            // sidewalk pavement itself (3.75m-5.75m band). Sample a point
            // in the inner half of that band (between the verge's outer
            // edge and the sidewalk's own centerline) at each crosswalk's
            // position. Primitive-fallback contract.
            RebuildWithPrimitiveFallback();
            var crosswalkObjects = Children().Where(t => t.name.StartsWith(WorldBuilder.CrosswalkNamePrefix)).ToList();
            var roadEdge = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth; // 3.75

            foreach (var edge in NeighborhoodLayout.WalkNetwork.Edges.Where(e => e.Kind == WalkEdgeKind.Crosswalk))
            {
                var alongX = Mathf.Abs(edge.A.Z - edge.B.Z) < 0.01f;
                var sidewalkCenterMagnitude = Mathf.Abs(alongX ? edge.A.X : edge.A.Z);
                var sampleMagnitude = (roadEdge + sidewalkCenterMagnitude) / 2f;
                var alongPosition = alongX ? (edge.A.Z + edge.B.Z) / 2f : (edge.A.X + edge.B.X) / 2f;

                foreach (var sign in new[] { 1f, -1f })
                {
                    var point = alongX
                        ? new GridPoint(sign * sampleMagnitude, alongPosition)
                        : new GridPoint(alongPosition, sign * sampleMagnitude);

                    AssertNothingCovers(crosswalkObjects, point, $"crosswalk sidewalk-band sample at {point}");
                }
            }
        }

        [Test]
        public void Road_Verge_Sidewalk_AndCrosswalk_AreVisuallyDistinctColors()
        {
            // #106: placeholder flat-colored surfaces, no literal striping,
            // but road/verge/sidewalk/crosswalk must each read as its own
            // distinct surface. Primitive-fallback contract — kit tiles
            // bring their own colormap texture instead. (Verge strips are
            // back: GrassVergeWidth is 0.75m, Derek's 2026-07-13 midpoint
            // request.)
            RebuildWithPrimitiveFallback();
            Color ColorOf(string prefix) => Children().First(t => t.name.StartsWith(prefix))
                .GetComponent<Renderer>().sharedMaterial.color;

            var road = ColorOf(WorldBuilder.RoadNamePrefix);
            var verge = ColorOf(WorldBuilder.VergeNamePrefix);
            var sidewalk = ColorOf(WorldBuilder.SidewalkNamePrefix);
            var crosswalk = ColorOf(WorldBuilder.CrosswalkNamePrefix);

            Assert.That(road, Is.EqualTo(CoreColors.FromHex(Palette.StreetHex)));
            Assert.That(verge, Is.EqualTo(CoreColors.FromHex(Palette.GrassVergeHex)));
            Assert.That(sidewalk, Is.EqualTo(CoreColors.FromHex(Palette.SidewalkHex)));
            Assert.That(crosswalk, Is.EqualTo(CoreColors.FromHex(Palette.CrosswalkHex)));

            var colors = new[] { road, verge, sidewalk, crosswalk };
            Assert.That(colors, Is.Unique);
        }

        [Test]
        public void SpawnedDogs_StandOnSidewalks_NeverOnARoadOrItsVerge()
        {
            // #106: dogs spawn outside both roads' pavement + grass verge
            // band — i.e. on a sidewalk, never on the road itself.
            DogSpawner.SpawnDogs(GameState.CreateNew(), root.transform);
            var roadAndVergeHalfWidth = NeighborhoodLayout.StreetWidth / 2f + WorldDimensions.GrassVergeWidth;

            foreach (var view in root.GetComponentsInChildren<DogView>())
            {
                var p = view.transform.position;
                Assert.That(Mathf.Abs(p.x) > roadAndVergeHalfWidth && Mathf.Abs(p.z) > roadAndVergeHalfWidth, Is.True,
                    $"{view.Dog.Name} spawned on the road or its verge at {p}");
            }
        }

        [Test]
        public void SunMatchesTheDaytimeLightingPreset()
        {
            // #39: single fixed daytime setup.
            var sun = root.GetComponentsInChildren<Light>().Single();

            Assert.That(sun.type, Is.EqualTo(LightType.Directional));
            Assert.That(sun.intensity, Is.EqualTo(LightingPreset.SunIntensity).Within(0.001f));

            var euler = sun.transform.rotation.eulerAngles;
            Assert.That(euler.x, Is.EqualTo(LightingPreset.SunPitchDegrees).Within(0.01f));
            Assert.That(euler.y, Is.EqualTo(LightingPreset.SunYawDegrees).Within(0.01f));

            var expected = CoreColors.FromHex(LightingPreset.SunColorHex);
            Assert.That(sun.color.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(sun.color.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(sun.color.b, Is.EqualTo(expected.b).Within(0.001f));
        }

        [Test]
        public void AmbientLighting_IsTheFlatDaytimeAmbient()
        {
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));

            var expected = CoreColors.FromHex(LightingPreset.AmbientColorHex);
            Assert.That(RenderSettings.ambientLight.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(RenderSettings.ambientLight.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(RenderSettings.ambientLight.b, Is.EqualTo(expected.b).Within(0.001f));
        }

        [Test]
        public void WorldContainsNoPlayerObjects()
        {
            // #19: no player avatar anywhere in the built world.
            var offenders = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.ToLowerInvariant().Contains("player")
                    || t.name.ToLowerInvariant().Contains("avatar"))
                .Select(t => t.name)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }
}
