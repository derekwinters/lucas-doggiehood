using System.Linq;
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

        [Test]
        public void LotWorldPositionsFor_OffsetsTheTypesLocalLotsByTheTilesCenter()
        {
            var coordinate = new TileCoordinate(1, 0);
            var center = TileGeometry.CenterOf(coordinate);
            var localOffsets = TileLotCatalog.LotOffsetsFor(TileType.TurnNE);

            var worldLots = TileGeometry.LotWorldPositionsFor(TileType.TurnNE, coordinate);

            Assert.That(worldLots.Count, Is.EqualTo(localOffsets.Count));
            for (int i = 0; i < worldLots.Count; i++)
            {
                Assert.That(worldLots[i].X, Is.EqualTo(center.X + localOffsets[i].X));
                Assert.That(worldLots[i].Z, Is.EqualTo(center.Z + localOffsets[i].Z));
            }
        }

        [Test]
        public void TreeWorldPositionsFor_OffsetsTheTypesTreeQuadrantsByTheTilesCenter()
        {
            // #385: a cul-de-sac's two bulb-side quadrants render as open space
            // with trees; their world positions are the local tree offsets
            // shifted by the tile's center.
            var coordinate = new TileCoordinate(0, 1);
            var center = TileGeometry.CenterOf(coordinate);
            var localTrees = TileLotCatalog.TreeQuadrantsFor(TileType.CulDeSacSouth).Values.ToList();

            var worldTrees = TileGeometry.TreeWorldPositionsFor(TileType.CulDeSacSouth, coordinate);

            Assert.That(worldTrees.Count, Is.EqualTo(localTrees.Count));
            Assert.That(worldTrees.Count, Is.EqualTo(2));
            for (int i = 0; i < worldTrees.Count; i++)
            {
                Assert.That(worldTrees[i].X, Is.EqualTo(center.X + localTrees[i].X));
                Assert.That(worldTrees[i].Z, Is.EqualTo(center.Z + localTrees[i].Z));
            }
        }

        [Test]
        public void TreeWorldPositionsFor_IsEmpty_ForTypesWithoutOpenSpaceTrees()
        {
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.TurnNE, new TileCoordinate(0, 1)), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.StraightNS, new TileCoordinate(0, 1)), Is.Empty);
        }
    }
}
