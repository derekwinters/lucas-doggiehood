using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #56: a zone is authored data — tile placements plus the buildable
    /// lots those tiles carry (per-type slots from TileLotCatalog, #109).
    /// Unlocking adds the tiles to a TileMap through #109's placement,
    /// which validates adjacency.
    /// </summary>
    public class ZoneTests
    {
        private static Zone TwoTileZone(int firstHouseId = 5)
        {
            return new Zone(
                new[]
                {
                    new ZoneTilePlacement(new TileCoordinate(0, 1), TileType.TurnSW),
                    new ZoneTilePlacement(new TileCoordinate(-1, 1), TileType.CulDeSacEast),
                },
                firstHouseId);
        }

        [Test]
        public void TilePlacements_ExposesTheAuthoredCoordinatesAndTypesInOrder()
        {
            var zone = TwoTileZone();

            Assert.That(zone.TilePlacements.Count, Is.EqualTo(2));
            Assert.That(zone.TilePlacements[0].Coordinate, Is.EqualTo(new TileCoordinate(0, 1)));
            Assert.That(zone.TilePlacements[0].Type, Is.EqualTo(TileType.TurnSW));
            Assert.That(zone.TilePlacements[1].Coordinate, Is.EqualTo(new TileCoordinate(-1, 1)));
            Assert.That(zone.TilePlacements[1].Type, Is.EqualTo(TileType.CulDeSacEast));
        }

        [Test]
        public void Lots_FollowThePerTypeLotRules_WithSequentialUniqueHouseIdsFromTheGivenStart()
        {
            var zone = TwoTileZone(firstHouseId: 5);

            // TurnSW keeps its 2 road-facing lots and CulDeSacEast keeps its 2
            // (drop cupped+opposite / bulb-side quadrants respectively -
            // "Property lots per tile", #385) -> 4 lots total. Ids stay
            // sequential.
            Assert.That(zone.Lots.Count, Is.EqualTo(4));
            CollectionAssert.AreEqual(Enumerable.Range(5, 4), zone.Lots.Select(lot => lot.HouseId));
        }

        [Test]
        public void Lots_WorldPositionsMatchTileGeometryPlusTheCatalogOffset()
        {
            var zone = TwoTileZone(firstHouseId: 5);

            var turnSwCenter = TileGeometry.CenterOf(new TileCoordinate(0, 1));
            var turnSwOffsets = TileLotCatalog.LotsFor(TileType.TurnSW);
            var firstLot = zone.Lots[0];

            // TurnSW keeps NW and SE (drops SW cup + its NE opposite); NW is
            // first in Zone's fixed NE,NW,SE,SW enumeration order.
            Assert.That(firstLot.Quadrant, Is.EqualTo(Quadrant.NorthWest));
            Assert.That(firstLot.Position.X, Is.EqualTo(turnSwCenter.X + turnSwOffsets[Quadrant.NorthWest].X));
            Assert.That(firstLot.Position.Z, Is.EqualTo(turnSwCenter.Z + turnSwOffsets[Quadrant.NorthWest].Z));
        }

        [Test]
        public void PlaceOnto_AddsEveryTileToTheMap_WhenTheAuthoredLayoutIsAdjacencyValid()
        {
            var zone = TwoTileZone();
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            zone.PlaceOnto(map);

            Assert.That(map.GetTileAt(new TileCoordinate(0, 1)), Is.EqualTo(TileType.TurnSW));
            Assert.That(map.GetTileAt(new TileCoordinate(-1, 1)), Is.EqualTo(TileType.CulDeSacEast));
        }

        [Test]
        public void PlaceOnto_Throws_WhenAPlacementFailsAdjacencyValidation()
        {
            // CulDeSacEast placed directly east of the FourWay origin
            // mismatches: FourWay's East edge has a road, CulDeSacEast's
            // West edge does not.
            var zone = new Zone(
                new[] { new ZoneTilePlacement(new TileCoordinate(1, 0), TileType.CulDeSacEast) },
                firstHouseId: 5);
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            Assert.Throws<InvalidOperationException>(() => zone.PlaceOnto(map));
        }
    }
}
