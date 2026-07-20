using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #109: world-space positions (roads, lots) derive from a tile's grid
    /// coordinate plus the #105 standard dimensions - never a separately
    /// hand-picked value.
    /// </summary>
    public class TileGeometryTests
    {
        [Test]
        public void CenterOf_TheOriginTile_IsTheWorldOrigin()
        {
            var center = TileGeometry.CenterOf(new TileCoordinate(0, 0));

            Assert.That(center.X, Is.EqualTo(0f));
            Assert.That(center.Z, Is.EqualTo(0f));
        }

        [Test]
        public void CenterOf_DerivesFromGridCoordinateTimesTileSize()
        {
            var east = TileGeometry.CenterOf(new TileCoordinate(1, 0));
            Assert.That(east.X, Is.EqualTo(WorldDimensions.TileSize));
            Assert.That(east.Z, Is.EqualTo(0f));

            var northWest = TileGeometry.CenterOf(new TileCoordinate(-1, 1));
            Assert.That(northWest.X, Is.EqualTo(-WorldDimensions.TileSize));
            Assert.That(northWest.Z, Is.EqualTo(WorldDimensions.TileSize));
        }

        [Test]
        public void EdgeMidpoint_IsHalfATileFromCenterAlongTheEdgesAxis()
        {
            var coordinate = new TileCoordinate(0, 0);
            float half = WorldDimensions.TileSize / 2f;

            var north = TileGeometry.EdgeMidpoint(coordinate, TileEdge.North);
            Assert.That(north.X, Is.EqualTo(0f));
            Assert.That(north.Z, Is.EqualTo(half));

            var east = TileGeometry.EdgeMidpoint(coordinate, TileEdge.East);
            Assert.That(east.X, Is.EqualTo(half));
            Assert.That(east.Z, Is.EqualTo(0f));
        }

        [Test]
        public void EdgeMidpoint_ShiftsWithTheTilesGridCoordinate()
        {
            var coordinate = new TileCoordinate(1, 0);
            float half = WorldDimensions.TileSize / 2f;

            var north = TileGeometry.EdgeMidpoint(coordinate, TileEdge.North);

            Assert.That(north.X, Is.EqualTo(WorldDimensions.TileSize));
            Assert.That(north.Z, Is.EqualTo(half));
        }
    }
}
