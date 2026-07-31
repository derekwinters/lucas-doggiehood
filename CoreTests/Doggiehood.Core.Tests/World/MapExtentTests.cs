using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #373 (Gap 1): the world-space extent every placed tile covers, derived
    /// purely from the live <see cref="TileMap"/> — the shared basis the ground
    /// plane grows to cover and the camera pan bounds recompute from, so an
    /// unlocked zone is neither floating on void nor out of pan reach.
    /// </summary>
    public class MapExtentTests
    {
        private static readonly float Half = WorldDimensions.TileSize / 2f;

        [Test]
        public void Covering_StartingMap_SpansTheSingleOriginTile()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            var extent = MapExtent.Covering(map);

            Assert.That(extent.MinX, Is.EqualTo(-Half));
            Assert.That(extent.MaxX, Is.EqualTo(Half));
            Assert.That(extent.MinZ, Is.EqualTo(-Half));
            Assert.That(extent.MaxZ, Is.EqualTo(Half));
        }

        [Test]
        public void Covering_GrowsNorth_WhenTheNorthCulDeSacIsPlaced()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            ZoneCatalog.FirstZone.PlaceOnto(map);

            var extent = MapExtent.Covering(map);

            // The north tile (0,1) centres at z = TileSize and spans another
            // half-tile beyond that; the south edge of the origin tile is
            // unchanged.
            Assert.That(extent.MaxZ, Is.EqualTo(WorldDimensions.TileSize + Half));
            Assert.That(extent.MinZ, Is.EqualTo(-Half));
            Assert.That(extent.MinX, Is.EqualTo(-Half));
            Assert.That(extent.MaxX, Is.EqualTo(Half));
        }

        [Test]
        public void Covering_ReportsCenterWidthAndDepth()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            ZoneCatalog.FirstZone.PlaceOnto(map);

            var extent = MapExtent.Covering(map);

            Assert.That(extent.Width, Is.EqualTo(WorldDimensions.TileSize));
            Assert.That(extent.Depth, Is.EqualTo(WorldDimensions.TileSize * 2f));
            Assert.That(extent.CenterX, Is.EqualTo(0f));
            Assert.That(extent.CenterZ, Is.EqualTo(WorldDimensions.TileSize / 2f));
        }
    }
}
