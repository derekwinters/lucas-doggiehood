using System;
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
            // #614: full-lot types (FourWay/Straight*/Tee*) drop no quadrant, and
            // the whole-tile GreenSpace park (#539) is out of scope — all bare.
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.StraightNS, new TileCoordinate(0, 1)), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.FourWay, new TileCoordinate(0, 1)), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.TeeNorth, new TileCoordinate(0, 1)), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.GreenSpace, new TileCoordinate(0, 1)), Is.Empty);
        }

        [Test]
        public void TreeWorldPositionsFor_Bend_PlantsBothDroppedQuadrantsClearOfTheRoad()
        {
            // #614: a bend drops the cupped corner AND its diagonal opposite; both
            // become open space with trees. The cupped-corner tree is kept clear
            // of the bend's road via the tile-aware clearance; the
            // diagonal-opposite quadrant borders neither roaded edge and is
            // unaffected, keeping its exact corner offset.
            const TileType type = TileType.TurnNE;
            var coordinate = new TileCoordinate(2, 3);
            var center = TileGeometry.CenterOf(coordinate);
            float d = NeighborhoodLayout.LotDistanceFromCenter;

            var positions = TileGeometry.TreeWorldPositionsFor(type, coordinate);

            Assert.That(positions.Count, Is.EqualTo(2));

            var diagonal = new GridPoint(center.X - d, center.Z - d);
            Assert.That(positions.Any(p => Approximately(p, diagonal)), Is.True,
                "the diagonal-opposite quadrant always gets a tree at the corner offset");

            var cupped = new GridPoint(center.X + d, center.Z + d);
            Assert.That(positions.Any(p => Approximately(p, cupped)), Is.True,
                "the cupped corner gets a tree (it has clean grass beyond the arc)");

            var roads = LotBounds.RoadsFor(coordinate, type);
            foreach (var road in roads)
            {
                Assert.That(road.Contains(cupped), Is.False,
                    "the cupped-corner tree never lands on the tile's road");
            }
        }

        [Test]
        public void TreeWorldPositionsFor_TwinBend_PlantsAllFourQuadrants()
        {
            // #614: twin bends carry no lots, so all four quadrants render trees.
            var positions = TileGeometry.TreeWorldPositionsFor(
                TileType.OpposingTurnsNS, new TileCoordinate(0, 1));

            Assert.That(positions.Count, Is.EqualTo(4));
        }

        [Test]
        public void OpenSpaceTreeHasClearGrass_IsTrue_WhenTheQuadrantHasGrassClearOfRoads()
        {
            var quadrant = new LotRect(0f, 15f, 0f, 15f);
            var position = new GridPoint(7.5f, 7.5f);

            Assert.That(
                TileGeometry.OpenSpaceTreeHasClearGrass(quadrant, position, System.Array.Empty<Road>()),
                Is.True);
        }

        [Test]
        public void OpenSpaceTreeHasClearGrass_IsFalse_WhenRoadsLeaveNoCleanGrass()
        {
            // #614: a narrow quadrant bordered by a road on each side — both edges
            // inset by StreetCorridorInset collapse the clean-grass rect, so the
            // tree is skipped rather than force-placed into the road.
            var quadrant = new LotRect(0f, 5f, 0f, 30f);
            var position = new GridPoint(2.5f, 15f);
            var roads = new[]
            {
                new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 15f), 15f),
                new Road(StreetOrientation.NorthSouth, new GridPoint(5f, 15f), 15f),
            };

            Assert.That(TileGeometry.OpenSpaceTreeHasClearGrass(quadrant, position, roads), Is.False);
        }

        private static bool Approximately(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Z - b.Z) < 0.001f;
        }
    }
}
