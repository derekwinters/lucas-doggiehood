using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #109: the grid-coordinate tile map — placement adjacent to the
    /// existing map, rejected on any shared-edge road/no-road mismatch.
    /// </summary>
    public class TileMapTests
    {
        private static TileMap NewMapSeededWithFourWay()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        [Test]
        public void SeedTile_IsPresentAtItsCoordinate()
        {
            var map = NewMapSeededWithFourWay();

            Assert.That(map.HasTileAt(new TileCoordinate(0, 0)), Is.True);
            Assert.That(map.GetTileAt(new TileCoordinate(0, 0)), Is.EqualTo(TileType.FourWay));
        }

        [Test]
        public void CanPlace_RejectsACoordinateNotAdjacentToTheExistingMap()
        {
            var map = NewMapSeededWithFourWay();

            Assert.That(map.CanPlace(new TileCoordinate(5, 5), TileType.FourWay), Is.False);
        }

        [Test]
        public void CanPlace_RejectsAnAlreadyOccupiedCoordinate()
        {
            var map = NewMapSeededWithFourWay();

            Assert.That(map.CanPlace(new TileCoordinate(0, 0), TileType.StraightNS), Is.False);
        }

        [Test]
        public void CanPlace_AcceptsAMatchingRoadEdgeNextToTheMap()
        {
            var map = NewMapSeededWithFourWay();

            // FourWay's East edge has a road; StraightEW's West edge (the
            // shared boundary) also has a road - roads meet.
            Assert.That(map.CanPlace(new TileCoordinate(1, 0), TileType.StraightEW), Is.True);
        }

        [Test]
        public void CanPlace_RejectsARoadMeetingNoRoadAcrossTheSharedEdge()
        {
            var map = NewMapSeededWithFourWay();

            // FourWay's East edge has a road; CulDeSacNorth's only road
            // edge is North, so its West edge (the shared boundary) has
            // no road - mismatch.
            Assert.That(map.CanPlace(new TileCoordinate(1, 0), TileType.CulDeSacNorth), Is.False);
        }

        [Test]
        public void Place_AddsTheTileWhenValid_AndThrowsWhenNot()
        {
            var map = NewMapSeededWithFourWay();

            map.Place(new TileCoordinate(1, 0), TileType.StraightEW);
            Assert.That(map.GetTileAt(new TileCoordinate(1, 0)), Is.EqualTo(TileType.StraightEW));

            Assert.Throws<System.InvalidOperationException>(
                () => map.Place(new TileCoordinate(1, 0), TileType.StraightEW));
        }

        [Test]
        public void ExpansionMd_NorthwestCulDeSacStreet_PlacesValidly()
        {
            // docs/specs/expansion.md's confirmed first-zone layout: from
            // the starting FourWay at (0,0), TurnSW at (0,1), CulDeSacEast
            // at (-1,1) - the road runs north, turns west, ends in the bulb.
            var map = NewMapSeededWithFourWay();

            Assert.That(map.CanPlace(new TileCoordinate(0, 1), TileType.TurnSW), Is.True);
            map.Place(new TileCoordinate(0, 1), TileType.TurnSW);

            Assert.That(map.CanPlace(new TileCoordinate(-1, 1), TileType.CulDeSacEast), Is.True);
            map.Place(new TileCoordinate(-1, 1), TileType.CulDeSacEast);

            Assert.That(map.HasTileAt(new TileCoordinate(-1, 1)), Is.True);
        }

        [Test]
        public void CanPlace_RejectsMismatchAgainstANewlyPlacedNeighbor_NotJustTheSeed()
        {
            var map = NewMapSeededWithFourWay();
            map.Place(new TileCoordinate(0, 1), TileType.TurnSW);

            // TurnSW's road edges are South, West. Its East edge has no
            // road, so a tile placed to its east requiring a road there
            // (e.g. StraightEW, whose West edge has a road) must be
            // rejected.
            Assert.That(map.CanPlace(new TileCoordinate(1, 1), TileType.StraightEW), Is.False);
        }
    }
}
