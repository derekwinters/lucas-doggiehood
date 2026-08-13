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
        public void TreeWorldPositionsFor_PlantsEachTreeQuadrantsCluster_InsideThatQuadrant()
        {
            // #385: a cul-de-sac's two bulb-side quadrants render as open space
            // with trees. #700: each of those quadrants gets a CLUSTER of trees
            // rather than one at the corner offset — every tree lands inside its
            // own quadrant's world bounds, shifted by the tile's center.
            var coordinate = new TileCoordinate(0, 1);
            var treeQuadrants = TileLotCatalog.TreeQuadrantsFor(TileType.CulDeSacSouth).Keys.ToList();
            Assert.That(treeQuadrants.Count, Is.EqualTo(2), "precondition: two bulb-side tree quadrants");

            var worldTrees = TileGeometry.TreeWorldPositionsFor(TileType.CulDeSacSouth, coordinate);

            Assert.That(worldTrees.Count, Is.GreaterThan(treeQuadrants.Count),
                "the tile plants more than one tree per open-space quadrant");
            foreach (var quadrant in treeQuadrants)
            {
                var bounds = TileGeometry.QuadrantWorldBounds(coordinate, quadrant);
                var inQuadrant = worldTrees.Count(t => bounds.Contains(t.Position));
                Assert.That(inQuadrant, Is.GreaterThanOrEqualTo(YardLandscaping.OpenSpaceSelectMin),
                    $"{quadrant}: its cluster lands inside its own quadrant bounds");
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
            // become open space with trees. #700: each gets a cluster, and every
            // tree in it is kept clear of the bend's road via the tile-aware
            // clearance (LotBounds.RoadsFor/ClearRoadCorridors).
            const TileType type = TileType.TurnNE;
            var coordinate = new TileCoordinate(2, 3);

            var placements = TileGeometry.TreeWorldPositionsFor(type, coordinate);

            Assert.That(TileGeometry.OpenSpaceTreesFor(type, coordinate, Quadrant.NorthEast),
                Is.Not.Empty, "the cupped corner is planted (it has clean grass beyond the arc)");
            Assert.That(TileGeometry.OpenSpaceTreesFor(type, coordinate, Quadrant.SouthWest),
                Is.Not.Empty, "the diagonal-opposite quadrant is planted");

            var roads = LotBounds.RoadsFor(coordinate, type);
            foreach (var placement in placements)
            {
                foreach (var road in roads)
                {
                    Assert.That(road.Contains(placement.Position), Is.False,
                        "an open-space tree never lands on the tile's road");
                }
            }
        }

        [Test]
        public void OpenSpaceGrassFor_TrimsTheQuadrantBackFromTheTilesRoads()
        {
            // #614/#700: the plantable grass of an open-space quadrant is its full
            // quadrant bounds pulled back off every road corridor it borders — the
            // region the cluster is rejection-sampled inside, so no tree can be
            // generated onto pavement in the first place.
            const TileType type = TileType.TurnNE;
            var coordinate = new TileCoordinate(2, 3);

            var bounds = TileGeometry.QuadrantWorldBounds(coordinate, Quadrant.NorthEast);
            var grass = TileGeometry.OpenSpaceGrassFor(type, coordinate, Quadrant.NorthEast);

            Assert.That(bounds.Contains(grass), Is.True, "the grass never reaches outside its quadrant");
            Assert.That(grass.Width, Is.LessThan(bounds.Width),
                "the cupped corner borders the bend's road on its X edge, so the grass is trimmed");
            Assert.That(grass.Depth, Is.LessThan(bounds.Depth),
                "and on its Z edge");
            Assert.That(grass.Width, Is.GreaterThan(0f), "clean grass is left to plant in");
        }
    }
}
