using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #385: per-type property-lot definitions for the 16 non-FourWay tile
    /// types, following the "Property lots per tile" rules settled in
    /// docs/specs/world/tile-catalog.md. House facing is settled as "remove"
    /// (no rotation): every kept lot borders a straight roaded edge square-on,
    /// and the lots that can't are dropped:
    /// <list type="bullet">
    /// <item>Twin bends: 0 lots.</item>
    /// <item>Bends (Turn*): 2 lots - drop the cupped corner AND its diagonal
    /// opposite (which faces neither roaded edge).</item>
    /// <item>Cul-de-sacs (CulDeSac*): 2 lots - keep the two quadrants adjacent
    /// to the single roaded edge; the two bulb-side quadrants become open
    /// space with trees.</item>
    /// <item>FourWay/Straight*/Tee*: all four quadrant lots.</item>
    /// </list>
    /// </summary>
    public class TileLotCatalogTests
    {
        /// <summary>The local corner offset a quadrant's lot sits at, mirroring
        /// LotsFor's construction (the starting FourWay corner distance).</summary>
        private static GridPoint ExpectedOffset(Quadrant quadrant)
        {
            float d = NeighborhoodLayout.LotDistanceFromCenter;
            switch (quadrant)
            {
                case Quadrant.NorthEast: return new GridPoint(d, d);
                case Quadrant.NorthWest: return new GridPoint(-d, d);
                case Quadrant.SouthEast: return new GridPoint(d, -d);
                case Quadrant.SouthWest: return new GridPoint(-d, -d);
                default: throw new ArgumentOutOfRangeException(nameof(quadrant));
            }
        }

        private static void AssertLotsAreExactly(TileType type, params Quadrant[] expected)
        {
            var lots = TileLotCatalog.LotsFor(type);

            Assert.That(lots.Count, Is.EqualTo(expected.Length));
            CollectionAssert.AreEquivalent(expected, lots.Keys);
            foreach (var quadrant in expected)
            {
                var offset = ExpectedOffset(quadrant);
                Assert.That(lots[quadrant].X, Is.EqualTo(offset.X));
                Assert.That(lots[quadrant].Z, Is.EqualTo(offset.Z));
            }
        }

        [Test]
        public void Types_CoversEveryLottedType_ExcludingFourWayAndGreenSpace()
        {
            // #539: GreenSpace joins FourWay as a type with no per-quadrant
            // catalog lots, so it is excluded from Types too — leaving the same
            // 16 road tiles that carry buildable lot slots.
            var expected = ((TileType[])Enum.GetValues(typeof(TileType)))
                .Where(t => t != TileType.FourWay && t != TileType.GreenSpace)
                .ToList();

            Assert.That(expected.Count, Is.EqualTo(16));
            CollectionAssert.AreEquivalent(expected, TileLotCatalog.Types);
        }

        [Test]
        public void FourWay_IsNotDefinedHere_ItAlreadyHasNeighborhoodLayout()
        {
            Assert.Throws<ArgumentException>(() => TileLotCatalog.LotsFor(TileType.FourWay));
        }

        // Every type that keeps all four quadrant lots: the straight-road
        // families (no curve, so every quadrant faces a road square-on).
        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.TeeSouth)]
        [TestCase(TileType.TeeEast)]
        [TestCase(TileType.TeeWest)]
        public void FourLotTypes_HaveOneLotPerQuadrant_AtTheFourWayCornerDistance(TileType type)
        {
            AssertLotsAreExactly(
                type,
                Quadrant.NorthEast, Quadrant.NorthWest, Quadrant.SouthEast, Quadrant.SouthWest);
        }

        // Twin bends leave no clean buildable quadrant, so they carry no lots.
        [TestCase(TileType.OpposingTurnsNS)]
        [TestCase(TileType.OpposingTurnsEW)]
        public void TwinBends_HaveNoLots(TileType type)
        {
            Assert.That(TileLotCatalog.LotsFor(type), Is.Empty);
        }

        // #385: each bend keeps exactly two lots - dropping the cupped corner
        // (its own named corner) AND the corner diagonally opposite it, which
        // borders neither roaded edge and so can never face a road.
        [TestCase(TileType.TurnNE, Quadrant.NorthWest, Quadrant.SouthEast)]
        [TestCase(TileType.TurnNW, Quadrant.NorthEast, Quadrant.SouthWest)]
        [TestCase(TileType.TurnSE, Quadrant.NorthEast, Quadrant.SouthWest)]
        [TestCase(TileType.TurnSW, Quadrant.NorthWest, Quadrant.SouthEast)]
        public void Bends_KeepTheTwoRoadFacingLots(TileType type, Quadrant keptA, Quadrant keptB)
        {
            AssertLotsAreExactly(type, keptA, keptB);
        }

        // #385: each cul-de-sac keeps the two quadrants adjacent to its single
        // roaded edge; the two bulb-side quadrants are dropped (they become
        // open space with trees, TreeQuadrantsFor).
        [TestCase(TileType.CulDeSacNorth, Quadrant.NorthEast, Quadrant.NorthWest)]
        [TestCase(TileType.CulDeSacSouth, Quadrant.SouthEast, Quadrant.SouthWest)]
        [TestCase(TileType.CulDeSacEast, Quadrant.NorthEast, Quadrant.SouthEast)]
        [TestCase(TileType.CulDeSacWest, Quadrant.NorthWest, Quadrant.SouthWest)]
        public void CulDeSacs_KeepTheTwoLotsAdjacentToTheRoadedEdge(TileType type, Quadrant keptA, Quadrant keptB)
        {
            AssertLotsAreExactly(type, keptA, keptB);
        }

        // #385: the two dropped bulb-side quadrants of a cul-de-sac render as
        // open space with trees, at the same corner offset a lot would use.
        [TestCase(TileType.CulDeSacNorth, Quadrant.SouthEast, Quadrant.SouthWest)]
        [TestCase(TileType.CulDeSacSouth, Quadrant.NorthEast, Quadrant.NorthWest)]
        [TestCase(TileType.CulDeSacEast, Quadrant.NorthWest, Quadrant.SouthWest)]
        [TestCase(TileType.CulDeSacWest, Quadrant.NorthEast, Quadrant.SouthEast)]
        public void TreeQuadrantsFor_CulDeSac_AreTheTwoBulbSideQuadrants(TileType type, Quadrant treeA, Quadrant treeB)
        {
            var trees = TileLotCatalog.TreeQuadrantsFor(type);

            Assert.That(trees.Count, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { treeA, treeB }, trees.Keys);
            foreach (var quadrant in new[] { treeA, treeB })
            {
                var offset = ExpectedOffset(quadrant);
                Assert.That(trees[quadrant].X, Is.EqualTo(offset.X));
                Assert.That(trees[quadrant].Z, Is.EqualTo(offset.Z));
            }
        }

        // #385: a cul-de-sac's kept lots and its tree quadrants never overlap
        // and together account for all four quadrants.
        [TestCase(TileType.CulDeSacNorth)]
        [TestCase(TileType.CulDeSacSouth)]
        [TestCase(TileType.CulDeSacEast)]
        [TestCase(TileType.CulDeSacWest)]
        public void CulDeSac_LotsAndTreeQuadrants_PartitionAllFourQuadrants(TileType type)
        {
            var lots = TileLotCatalog.LotsFor(type).Keys;
            var trees = TileLotCatalog.TreeQuadrantsFor(type).Keys;

            CollectionAssert.IsEmpty(lots.Intersect(trees));
            CollectionAssert.AreEquivalent(
                (Quadrant[])Enum.GetValues(typeof(Quadrant)),
                lots.Concat(trees));
        }

        // #385: only cul-de-sacs get open-space trees. Bends' dropped quadrants
        // stay plain open space (grass), and full-lot types drop nothing.
        [TestCase(TileType.TurnNE)]
        [TestCase(TileType.TurnSW)]
        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.OpposingTurnsNS)]
        public void TreeQuadrantsFor_IsEmpty_ForEveryNonCulDeSacType(TileType type)
        {
            Assert.That(TileLotCatalog.TreeQuadrantsFor(type), Is.Empty);
        }

        // The cupped corner of a bend is also the corner that renders curved
        // (still exposed for the curved-corner rendering, #383/#385).
        [TestCase(TileType.TurnNE, Quadrant.NorthEast)]
        [TestCase(TileType.TurnNW, Quadrant.NorthWest)]
        [TestCase(TileType.TurnSE, Quadrant.SouthEast)]
        [TestCase(TileType.TurnSW, Quadrant.SouthWest)]
        public void TryGetCuppedCorner_ReturnsTheBendsOwnCorner(TileType type, Quadrant cupped)
        {
            Assert.That(TileLotCatalog.TryGetCuppedCorner(type, out var quadrant), Is.True);
            Assert.That(quadrant, Is.EqualTo(cupped));
        }

        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.CulDeSacSouth)]
        [TestCase(TileType.OpposingTurnsNS)]
        public void TryGetCuppedCorner_IsFalseForNonBends(TileType type)
        {
            Assert.That(TileLotCatalog.TryGetCuppedCorner(type, out _), Is.False);
        }

        [Test]
        public void LotOffsetsFor_ReturnsTheSamePositionsAsLotsFor()
        {
            var lots = TileLotCatalog.LotsFor(TileType.TurnNE);
            var offsets = TileLotCatalog.LotOffsetsFor(TileType.TurnNE);

            CollectionAssert.AreEquivalent(lots.Values, offsets);
        }
    }
}
