using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #385/#607: per-type property-lot definitions for the 17 lotted tile
    /// types (every type except the house-free GreenSpace, including the
    /// full-intersection FourWay), following the "Property lots per tile" rules
    /// settled in docs/specs/world/tile-catalog.md. House facing is settled as "remove"
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

        private static void AssertTreeQuadrantsAreExactly(TileType type, params Quadrant[] expected)
        {
            var trees = TileLotCatalog.TreeQuadrantsFor(type);

            Assert.That(trees.Count, Is.EqualTo(expected.Length));
            CollectionAssert.AreEquivalent(expected, trees.Keys);
            foreach (var quadrant in expected)
            {
                var offset = ExpectedOffset(quadrant);
                Assert.That(trees[quadrant].X, Is.EqualTo(offset.X));
                Assert.That(trees[quadrant].Z, Is.EqualTo(offset.Z));
            }
        }

        [Test]
        public void Types_CoversEveryLottedType_ExcludingGreenSpace()
        {
            // #539: GreenSpace is the only type with no per-quadrant catalog
            // lots, so it is the only one excluded from Types. #607: FourWay is
            // a full intersection with all four quadrant lots (wherever it
            // appears, not just at the origin), so it joins the lotted types
            // here — the origin's seeded lots are guarded by GameState, not by
            // excluding FourWay from the catalog.
            var expected = ((TileType[])Enum.GetValues(typeof(TileType)))
                .Where(t => t != TileType.GreenSpace)
                .ToList();

            Assert.That(expected.Count, Is.EqualTo(17));
            CollectionAssert.AreEquivalent(expected, TileLotCatalog.Types);
        }

        // Every type that keeps all four quadrant lots: the full intersection
        // (#607) and the straight-road families (no curve, so every quadrant
        // faces a road square-on).
        [TestCase(TileType.FourWay)]
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

        // #614: every quadrant with no kept house lot renders open-space trees,
        // not just cul-de-sacs. Tree quadrants are derived as "the tile's four
        // quadrants minus its LotsFor quadrants", so trees and lots can never
        // disagree. Full-lot types (FourWay/Straight*/Tee*) drop nothing, and
        // the whole-tile GreenSpace park (#539) is out of scope, so those all
        // stay bare.
        [TestCase(TileType.FourWay)]
        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.TeeSouth)]
        [TestCase(TileType.TeeEast)]
        [TestCase(TileType.TeeWest)]
        [TestCase(TileType.GreenSpace)]
        public void TreeQuadrantsFor_IsEmpty_ForFullLotAndGreenSpaceTypes(TileType type)
        {
            Assert.That(TileLotCatalog.TreeQuadrantsFor(type), Is.Empty);
        }

        // #614: each bend (Turn*) drops two quadrants — the cupped corner AND
        // its diagonal opposite — so both become open space with trees, at the
        // same corner offset a lot would use.
        [TestCase(TileType.TurnNE, Quadrant.NorthEast, Quadrant.SouthWest)]
        [TestCase(TileType.TurnNW, Quadrant.NorthWest, Quadrant.SouthEast)]
        [TestCase(TileType.TurnSE, Quadrant.SouthEast, Quadrant.NorthWest)]
        [TestCase(TileType.TurnSW, Quadrant.SouthWest, Quadrant.NorthEast)]
        public void TreeQuadrantsFor_Bend_AreTheCuppedCornerAndItsDiagonalOpposite(
            TileType type, Quadrant treeA, Quadrant treeB)
        {
            AssertTreeQuadrantsAreExactly(type, treeA, treeB);
        }

        // #614: twin bends carry no lots at all, so all four quadrants become
        // open space with trees.
        [TestCase(TileType.OpposingTurnsNS)]
        [TestCase(TileType.OpposingTurnsEW)]
        public void TreeQuadrantsFor_TwinBend_AreAllFourQuadrants(TileType type)
        {
            AssertTreeQuadrantsAreExactly(
                type,
                Quadrant.NorthEast, Quadrant.NorthWest, Quadrant.SouthEast, Quadrant.SouthWest);
        }

        // #614: for every lotted type, the kept lots and the tree quadrants are
        // disjoint and together account for all four quadrants — derived from
        // one LotsFor source of truth so they can never disagree. (FourWay's
        // lots come from the catalog too now, #607; GreenSpace is the only
        // no-lot, no-tree type and is excluded here.)
        [TestCase(TileType.FourWay)]
        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.TurnNE)]
        [TestCase(TileType.TurnSW)]
        [TestCase(TileType.CulDeSacNorth)]
        [TestCase(TileType.OpposingTurnsNS)]
        public void Lots_And_TreeQuadrants_PartitionAllFourQuadrants(TileType type)
        {
            var lots = TileLotCatalog.LotsFor(type).Keys;
            var trees = TileLotCatalog.TreeQuadrantsFor(type).Keys;

            CollectionAssert.IsEmpty(lots.Intersect(trees));
            CollectionAssert.AreEquivalent(
                (Quadrant[])Enum.GetValues(typeof(Quadrant)),
                lots.Concat(trees));
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
