using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #373 (Gap 1): road surfaces for a tile derived from its
    /// <see cref="TileCatalog"/> road edges (the #109 geometry) — one straight
    /// segment reaching from the tile centre to each road-carrying edge, so an
    /// unlocked tile gets road the same way the starting intersection's arms do
    /// rather than depending on the hardcoded <see cref="NeighborhoodLayout"/>.
    /// </summary>
    public class TileRoadGeometryTests
    {
        private static readonly float HalfTile = WorldDimensions.TileSize / 2f;

        [Test]
        public void SegmentsFor_CulDeSacSouth_IsOneSegmentReachingItsSouthEdge()
        {
            var coordinate = new TileCoordinate(0, 1); // the #360 north zone tile

            var segments = TileRoadGeometry.SegmentsFor(coordinate, TileType.CulDeSacSouth);

            Assert.That(segments.Count, Is.EqualTo(1), "a cul-de-sac carries a road on exactly one edge");
            var segment = segments[0];
            Assert.That(segment.Orientation, Is.EqualTo(StreetOrientation.NorthSouth),
                "the south road runs along the north-south axis");
            Assert.That(segment.Length, Is.EqualTo(HalfTile), "it reaches from the tile centre to the edge");
            Assert.That(segment.Width, Is.EqualTo(WorldDimensions.RoadWidth));

            // Tile centre (0, 60) to south-edge midpoint (0, 30): the segment
            // centres halfway, at (0, 45), so it meets the origin tile's road.
            Assert.That(segment.Center.X, Is.EqualTo(0f));
            Assert.That(segment.Center.Z, Is.EqualTo(WorldDimensions.TileSize - HalfTile / 2f));
        }

        [Test]
        public void SegmentsFor_FourWay_IsFourArms_TwoPerAxis()
        {
            var segments = TileRoadGeometry.SegmentsFor(new TileCoordinate(0, 0), TileType.FourWay);

            Assert.That(segments.Count, Is.EqualTo(4));
            Assert.That(segments.Count(s => s.Orientation == StreetOrientation.NorthSouth), Is.EqualTo(2));
            Assert.That(segments.Count(s => s.Orientation == StreetOrientation.EastWest), Is.EqualTo(2));
        }

        [Test]
        public void SegmentsFor_StraightEW_RunsAlongTheEastWestAxis()
        {
            var segments = TileRoadGeometry.SegmentsFor(new TileCoordinate(0, 0), TileType.StraightEW);

            Assert.That(segments.Count, Is.EqualTo(2));
            Assert.That(segments.All(s => s.Orientation == StreetOrientation.EastWest), Is.True);
            Assert.That(segments.Select(s => s.Center.X).OrderBy(x => x),
                Is.EqualTo(new[] { -HalfTile / 2f, HalfTile / 2f }));
        }
    }
}
