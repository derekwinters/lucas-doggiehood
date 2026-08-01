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

            // CulDeSacSouth keeps the 2 lots adjacent to its south roaded edge
            // (SE, SW); its two north bulb-side quadrants become open space with
            // trees ("Property lots per tile", #385). So the zone has 2 lots
            // with ids 5-6 continuing on from the 4 starting houses (ids 1-4).
            Assert.That(zone.Lots.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(Enumerable.Range(5, 2), zone.Lots.Select(lot => lot.HouseId));

            var tileCenter = TileGeometry.CenterOf(new TileCoordinate(0, 1));
            var offsets = TileLotCatalog.LotsFor(TileType.CulDeSacSouth);
            var firstLot = zone.Lots[0];

            // SE is first among the kept SE,SW in Zone's fixed NE,NW,SE,SW order.
            Assert.That(firstLot.Quadrant, Is.EqualTo(Quadrant.SouthEast));
            Assert.That(firstLot.Position.X, Is.EqualTo(tileCenter.X + offsets[Quadrant.SouthEast].X));
            Assert.That(firstLot.Position.Z, Is.EqualTo(tileCenter.Z + offsets[Quadrant.SouthEast].Z));
        }

        [Test]
        public void Zones_ContainsExactlyTheFirstZoneSoFar()
        {
            Assert.That(ZoneCatalog.Zones.Count, Is.EqualTo(1));
            Assert.That(ZoneCatalog.Zones[0], Is.SameAs(ZoneCatalog.FirstZone));
        }
    }
}
