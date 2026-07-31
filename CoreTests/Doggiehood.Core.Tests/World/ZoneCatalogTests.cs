using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #360: the first zone is a single cul-de-sac due north of the starting
    /// intersection (docs/specs/expansion.md "Map shape") — from the starting
    /// FourWay at (0,0), a CulDeSacSouth at (0,1) whose road enters from the
    /// south edge and ends in a bulb pointing north. Supersedes the #56
    /// two-tile northwest (TurnSW + CulDeSacEast) layout.
    /// </summary>
    public class ZoneCatalogTests
    {
        [Test]
        public void FirstZone_IsASingleCulDeSacSouthDueNorthOfTheOrigin()
        {
            var zone = ZoneCatalog.FirstZone;

            Assert.That(zone.TilePlacements.Count, Is.EqualTo(1));
            Assert.That(zone.TilePlacements[0], Is.EqualTo(
                new ZoneTilePlacement(new TileCoordinate(0, 1), TileType.CulDeSacSouth)));
        }

        [Test]
        public void FirstZone_PlacesValidlyOntoAFreshlySeededMap()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            ZoneCatalog.FirstZone.PlaceOnto(map);

            Assert.That(map.HasTileAt(new TileCoordinate(0, 1)), Is.True);
            Assert.That(map.GetTileAt(new TileCoordinate(0, 1)), Is.EqualTo(TileType.CulDeSacSouth));
        }

        [Test]
        public void FirstZone_LotsDeriveFromTheCulDeSacSouthTileWithSequentialHouseIds()
        {
            var zone = ZoneCatalog.FirstZone;

            // One tile carries four quadrant lots (TileLotCatalog), so the
            // whole zone has 4 lots with ids 5-8 continuing on from the 4
            // starting houses (ids 1-4).
            Assert.That(zone.Lots.Count, Is.EqualTo(4));
            CollectionAssert.AreEqual(Enumerable.Range(5, 4), zone.Lots.Select(lot => lot.HouseId));

            var tileCenter = TileGeometry.CenterOf(new TileCoordinate(0, 1));
            var offsets = TileLotCatalog.LotsFor(TileType.CulDeSacSouth);
            var firstLot = zone.Lots[0];

            Assert.That(firstLot.Quadrant, Is.EqualTo(Quadrant.NorthEast));
            Assert.That(firstLot.Position.X, Is.EqualTo(tileCenter.X + offsets[Quadrant.NorthEast].X));
            Assert.That(firstLot.Position.Z, Is.EqualTo(tileCenter.Z + offsets[Quadrant.NorthEast].Z));
        }

        [Test]
        public void Zones_ContainsExactlyTheFirstZoneSoFar()
        {
            Assert.That(ZoneCatalog.Zones.Count, Is.EqualTo(1));
            Assert.That(ZoneCatalog.Zones[0], Is.SameAs(ZoneCatalog.FirstZone));
        }
    }
}
