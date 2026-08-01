using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #109: per-type property-lot definitions for the 16 non-FourWay tile
    /// types, following the starting FourWay tile's own pattern
    /// (NeighborhoodLayout): one lot per Quadrant, offset from tile center
    /// by the same corner distance used there.
    /// </summary>
    public class TileLotCatalogTests
    {
        [Test]
        public void Types_CoversAllSixteenNonFourWayTypes()
        {
            var expected = ((TileType[])Enum.GetValues(typeof(TileType)))
                .Where(t => t != TileType.FourWay)
                .ToList();

            Assert.That(expected.Count, Is.EqualTo(16));
            CollectionAssert.AreEquivalent(expected, TileLotCatalog.Types);
        }

        [Test]
        public void FourWay_IsNotDefinedHere_ItAlreadyHasNeighborhoodLayout()
        {
            Assert.Throws<ArgumentException>(() => TileLotCatalog.LotsFor(TileType.FourWay));
        }

        // #383 "Property lots per tile": every type that keeps all four
        // quadrant lots (all non-FourWay types except the bends and twin
        // bends handled below).
        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.TeeSouth)]
        [TestCase(TileType.TeeEast)]
        [TestCase(TileType.TeeWest)]
        [TestCase(TileType.CulDeSacNorth)]
        [TestCase(TileType.CulDeSacSouth)]
        [TestCase(TileType.CulDeSacEast)]
        [TestCase(TileType.CulDeSacWest)]
        public void FourLotTypes_HaveOneLotPerQuadrant_AtTheFourWayCornerDistance(TileType type)
        {
            var lots = TileLotCatalog.LotsFor(type);

            Assert.That(lots.Count, Is.EqualTo(4));
            // Same corner-offset value the starting FourWay tile places its
            // 4 house lots at (NeighborhoodLayout.LotDistanceFromCenter).
            float d = NeighborhoodLayout.LotDistanceFromCenter;

            Assert.That(lots[Quadrant.NorthEast].X, Is.EqualTo(d));
            Assert.That(lots[Quadrant.NorthEast].Z, Is.EqualTo(d));

            Assert.That(lots[Quadrant.NorthWest].X, Is.EqualTo(-d));
            Assert.That(lots[Quadrant.NorthWest].Z, Is.EqualTo(d));

            Assert.That(lots[Quadrant.SouthEast].X, Is.EqualTo(d));
            Assert.That(lots[Quadrant.SouthEast].Z, Is.EqualTo(-d));

            Assert.That(lots[Quadrant.SouthWest].X, Is.EqualTo(-d));
            Assert.That(lots[Quadrant.SouthWest].Z, Is.EqualTo(-d));
        }

        // #383 "Property lots per tile": twin bends leave no clean buildable
        // quadrant, so they carry no lots at all.
        [TestCase(TileType.OpposingTurnsNS)]
        [TestCase(TileType.OpposingTurnsEW)]
        public void TwinBends_HaveNoLots(TileType type)
        {
            Assert.That(TileLotCatalog.LotsFor(type), Is.Empty);
        }

        // #383 "Property lots per tile": each bend keeps three lots, dropping
        // the small corner the curve cups (its own named corner).
        [TestCase(TileType.TurnNE, Quadrant.NorthEast)]
        [TestCase(TileType.TurnNW, Quadrant.NorthWest)]
        [TestCase(TileType.TurnSE, Quadrant.SouthEast)]
        [TestCase(TileType.TurnSW, Quadrant.SouthWest)]
        public void Bends_HaveThreeLots_DroppingTheirOwnCuppedCorner(TileType type, Quadrant cupped)
        {
            var lots = TileLotCatalog.LotsFor(type);

            Assert.That(lots.Count, Is.EqualTo(3));
            Assert.That(lots.ContainsKey(cupped), Is.False);

            float d = NeighborhoodLayout.LotDistanceFromCenter;
            foreach (var kvp in lots)
            {
                Assert.That(System.Math.Abs(kvp.Value.X), Is.EqualTo(d));
                Assert.That(System.Math.Abs(kvp.Value.Z), Is.EqualTo(d));
            }
        }

        // #383: the cupped corner of a bend is also the corner that renders
        // curved. Data-only exposure for this issue (no rendering).
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
