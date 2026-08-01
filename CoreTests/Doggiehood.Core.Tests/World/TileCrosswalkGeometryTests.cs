using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #508: crosswalk patches for an intersection tile derived from its
    /// <see cref="TileCatalog"/> road edges (the #109 geometry) — one band
    /// across each road ARM, the same way <see cref="TileRoadGeometry"/> derives
    /// the road arms — so the graybox fallback paints correct crosswalks on
    /// every unlocked intersection (a Tee as well as the origin 4-way), not just
    /// the hardcoded origin from <see cref="NeighborhoodLayout.WalkNetwork"/>.
    /// A three-way tile gets exactly three patches (one per real arm), never a
    /// phantom fourth over its non-road edge.
    /// </summary>
    public class TileCrosswalkGeometryTests
    {
        // Sidewalk-centre magnitude a crosswalk sits at, RoadWidth/2 + verge +
        // SidewalkWidth/2 = 4.75m (matches WalkNetwork's crosswalk placement).
        private const float Offset =
            WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;

        // Across-the-road clip: RoadWidth + both verges = 7.5m, so the patch
        // stops at the sidewalk boundary and never paints over sidewalk pavement.
        private const float AcrossSpan = WorldDimensions.RoadWidth + 2f * WorldDimensions.GrassVergeWidth;

        [Test]
        public void FourWay_HasFourPatches_OnePerArm_MatchingTheOriginBox()
        {
            var rects = TileCrosswalkGeometry.RectanglesFor(new TileCoordinate(0, 0), TileType.FourWay);

            Assert.That(rects.Count, Is.EqualTo(4));

            // North & south patches: thin (CrosswalkWidth) in Z, wide across in X.
            AssertPatch(rects, 0f, Offset, AcrossSpan, WorldDimensions.CrosswalkWidth);
            AssertPatch(rects, 0f, -Offset, AcrossSpan, WorldDimensions.CrosswalkWidth);
            // East & west patches: thin in X, wide across in Z.
            AssertPatch(rects, Offset, 0f, WorldDimensions.CrosswalkWidth, AcrossSpan);
            AssertPatch(rects, -Offset, 0f, WorldDimensions.CrosswalkWidth, AcrossSpan);
        }

        [Test]
        public void TeeNorth_HasThreePatches_OnItsThreeArms_NoneOnTheClosedSouthEdge()
        {
            var rects = TileCrosswalkGeometry.RectanglesFor(new TileCoordinate(0, 0), TileType.TeeNorth);

            Assert.That(rects.Count, Is.EqualTo(3), "roads on N/E/W -> three crosswalk patches");
            AssertPatch(rects, 0f, Offset, AcrossSpan, WorldDimensions.CrosswalkWidth);   // north arm
            AssertPatch(rects, Offset, 0f, WorldDimensions.CrosswalkWidth, AcrossSpan);   // east arm
            AssertPatch(rects, -Offset, 0f, WorldDimensions.CrosswalkWidth, AcrossSpan);  // west arm

            Assert.That(rects.Any(r => r.Center.Z < 0f), Is.False,
                "no phantom crosswalk over the closed (roadless) south edge");
        }

        [Test]
        public void Patches_AreOffsetToTheTilesOwnCentre_NotTheOrigin()
        {
            var coordinate = new TileCoordinate(1, 2);
            var center = TileGeometry.CenterOf(coordinate);

            var rects = TileCrosswalkGeometry.RectanglesFor(coordinate, TileType.FourWay);

            foreach (var rect in rects)
            {
                var dx = rect.Center.X - center.X;
                var dz = rect.Center.Z - center.Z;
                Assert.That(Mathf(dx) + Mathf(dz), Is.EqualTo(Offset).Within(0.001f),
                    "each patch sits one crosswalk-offset out from the tile centre along one axis");
            }
        }

        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        [TestCase(TileType.TurnNW)]
        [TestCase(TileType.CulDeSacSouth)]
        [TestCase(TileType.OpposingTurnsNS)]
        public void NonIntersectionTiles_HaveNoCrosswalkPatches(TileType type)
        {
            // Only true crossings (FourWay + the Tees) carry crosswalks; a
            // straight, turn, cul-de-sac, or (deferred) opposing-turns tile
            // has no crossing to paint.
            var rects = TileCrosswalkGeometry.RectanglesFor(new TileCoordinate(0, 0), type);
            Assert.That(rects.Count, Is.EqualTo(0));
        }

        private static float Mathf(float value)
        {
            return value < 0f ? -value : value;
        }

        private static void AssertPatch(
            System.Collections.Generic.IReadOnlyList<TileCrosswalkRect> rects,
            float x, float z, float spanX, float spanZ)
        {
            var match = rects.FirstOrDefault(r =>
                Mathf(r.Center.X - x) < 0.001f && Mathf(r.Center.Z - z) < 0.001f);
            Assert.That(rects.Any(r => Mathf(r.Center.X - x) < 0.001f && Mathf(r.Center.Z - z) < 0.001f),
                Is.True, $"a patch centred at ({x}, {z})");
            Assert.That(match.SpanX, Is.EqualTo(spanX).Within(0.001f), $"patch ({x},{z}) SpanX");
            Assert.That(match.SpanZ, Is.EqualTo(spanZ).Within(0.001f), $"patch ({x},{z}) SpanZ");
        }
    }
}
