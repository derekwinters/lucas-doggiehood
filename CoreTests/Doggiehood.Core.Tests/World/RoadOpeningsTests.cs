using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #599: an off-map road opening is an outer tile edge that carries a road
    /// but has no placed neighbor across it — the same "road end at the map
    /// boundary" the expansion frontier keys on (TileMap.HasRoadConnectionAt /
    /// TileTypeDefinition.HasRoadOn). Delivery trucks enter the live map at the
    /// opening nearest the destination door, ties broken deterministically
    /// (compass order N→E→S→W, then tile coordinate).
    /// </summary>
    public class RoadOpeningsTests
    {
        private const float Half = WorldDimensions.TileSize / 2f;

        [Test]
        public void Detect_SingleFourWay_ReturnsAllFourEdgesAsOpenings()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            var openings = RoadOpenings.Detect(map);

            var edges = openings.Select(o => o.Edge).ToList();
            Assert.That(openings.Count, Is.EqualTo(4));
            Assert.That(edges, Is.EquivalentTo(new[]
            {
                TileEdge.North, TileEdge.East, TileEdge.South, TileEdge.West,
            }));

            var north = openings.Single(o => o.Edge == TileEdge.North);
            Assert.That(north.Point.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(north.Point.Z, Is.EqualTo(Half).Within(0.0001f));
        }

        [Test]
        public void Detect_OmitsEdgesThatCarryNoRoad()
        {
            // StraightNS carries a road on N and S only — E and W are grass, so
            // they are never openings even though they are outer edges.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.StraightNS);

            var openings = RoadOpenings.Detect(map);

            Assert.That(openings.Select(o => o.Edge), Is.EquivalentTo(new[]
            {
                TileEdge.North, TileEdge.South,
            }));
        }

        [Test]
        public void Detect_OmitsInteriorEdgesWithAPlacedNeighborAcross()
        {
            // Two StraightNS stacked: the shared boundary (north of (0,0) ==
            // south of (0,1)) is interior, not an opening. Only the outer south
            // of (0,0) and north of (0,1) remain.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.StraightNS);
            map.Place(new TileCoordinate(0, 1), TileType.StraightNS);

            var openings = RoadOpenings.Detect(map);

            Assert.That(openings.Count, Is.EqualTo(2));
            // The interior boundary point (0, +Half) must not appear.
            Assert.That(openings.Any(o => o.Point.Z > 0f && o.Point.Z < WorldDimensions.TileSize), Is.False);
            var south = openings.Single(o => o.Tile.Equals(new TileCoordinate(0, 0)));
            Assert.That(south.Edge, Is.EqualTo(TileEdge.South));
            var north = openings.Single(o => o.Tile.Equals(new TileCoordinate(0, 1)));
            Assert.That(north.Edge, Is.EqualTo(TileEdge.North));
        }

        [Test]
        public void Nearest_PicksOpeningClosestToTheDoor()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var openings = RoadOpenings.Detect(map);

            // A door out east of the tile — the east opening is nearest.
            var door = new GridPoint(200f, 5f);

            var nearest = RoadOpenings.Nearest(openings, door);

            Assert.That(nearest.Edge, Is.EqualTo(TileEdge.East));
        }

        [Test]
        public void Nearest_EquidistantOpenings_TieBrokenByCompassOrderNorthFirst()
        {
            // Door dead-centre: all four FourWay openings are exactly Half away.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var openings = RoadOpenings.Detect(map);

            var nearest = RoadOpenings.Nearest(openings, new GridPoint(0f, 0f));

            Assert.That(nearest.Edge, Is.EqualTo(TileEdge.North),
                "compass tiebreak is N→E→S→W, so North wins an exact four-way tie");
        }

        [Test]
        public void Nearest_SameEdgeEquidistant_TieBrokenByTileCoordinate()
        {
            // Two North openings equidistant from a door on the X=0 axis; the
            // lower tile-coordinate (Col ascending, then Row) wins.
            var left = new RoadOpening(new TileCoordinate(-1, 0), TileEdge.North, new GridPoint(-60f, 30f));
            var right = new RoadOpening(new TileCoordinate(1, 0), TileEdge.North, new GridPoint(60f, 30f));
            var door = new GridPoint(0f, 200f);

            var nearest = RoadOpenings.Nearest(new[] { right, left }, door);

            Assert.That(nearest.Tile, Is.EqualTo(new TileCoordinate(-1, 0)),
                "same edge + equidistant falls through to tile coordinate, Col ascending");
        }
    }
}
