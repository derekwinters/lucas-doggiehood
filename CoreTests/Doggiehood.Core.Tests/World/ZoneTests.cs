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
        public void Lots_HasFourPerTile_WithSequentialUniqueHouseIdsFromTheGivenStart()
        {
            var zone = TwoTileZone(firstHouseId: 5);

            Assert.That(zone.Lots.Count, Is.EqualTo(8));
            CollectionAssert.AreEqual(Enumerable.Range(5, 8), zone.Lots.Select(lot => lot.HouseId));
        }

        [Test]
        public void Lots_WorldPositionsMatchTileGeometryPlusTheCatalogOffset()
        {
            var zone = TwoTileZone(firstHouseId: 5);

            var turnSwCenter = TileGeometry.CenterOf(new TileCoordinate(0, 1));
            var turnSwOffsets = TileLotCatalog.LotsFor(TileType.TurnSW);
            var firstLot = zone.Lots[0];

            Assert.That(firstLot.Quadrant, Is.EqualTo(Quadrant.NorthEast));
            Assert.That(firstLot.Position.X, Is.EqualTo(turnSwCenter.X + turnSwOffsets[Quadrant.NorthEast].X));
            Assert.That(firstLot.Position.Z, Is.EqualTo(turnSwCenter.Z + turnSwOffsets[Quadrant.NorthEast].Z));
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
