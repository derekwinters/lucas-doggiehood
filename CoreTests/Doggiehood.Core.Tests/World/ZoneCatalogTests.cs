using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #56: the first zone authored per Derek's confirmed layout
    /// (docs/specs/expansion.md "Map shape") — from the starting FourWay at
    /// (0,0), TurnSW at (0,1), CulDeSacEast at (-1,1).
    /// </summary>
    public class ZoneCatalogTests
    {
        [Test]
        public void FirstZone_MatchesTheConfirmedNorthwestCulDeSacLayout()
        {
            var zone = ZoneCatalog.FirstZone;

            Assert.That(zone.TilePlacements.Count, Is.EqualTo(2));
            Assert.That(zone.TilePlacements[0], Is.EqualTo(
                new ZoneTilePlacement(new TileCoordinate(0, 1), TileType.TurnSW)));
            Assert.That(zone.TilePlacements[1], Is.EqualTo(
                new ZoneTilePlacement(new TileCoordinate(-1, 1), TileType.CulDeSacEast)));
        }

        [Test]
        public void FirstZone_PlacesValidlyOntoAFreshlySeededMap()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            ZoneCatalog.FirstZone.PlaceOnto(map);

            Assert.That(map.HasTileAt(new TileCoordinate(-1, 1)), Is.True);
        }

        [Test]
        public void Zones_ContainsExactlyTheFirstZoneSoFar()
        {
            Assert.That(ZoneCatalog.Zones.Count, Is.EqualTo(1));
            Assert.That(ZoneCatalog.Zones[0], Is.SameAs(ZoneCatalog.FirstZone));
        }
    }
}
