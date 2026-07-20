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

        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        [TestCase(TileType.TurnNE)]
        [TestCase(TileType.TurnNW)]
        [TestCase(TileType.TurnSE)]
        [TestCase(TileType.TurnSW)]
        [TestCase(TileType.TeeNorth)]
        [TestCase(TileType.TeeSouth)]
        [TestCase(TileType.TeeEast)]
        [TestCase(TileType.TeeWest)]
        [TestCase(TileType.CulDeSacNorth)]
        [TestCase(TileType.CulDeSacSouth)]
        [TestCase(TileType.CulDeSacEast)]
        [TestCase(TileType.CulDeSacWest)]
        [TestCase(TileType.OpposingTurnsNS)]
        [TestCase(TileType.OpposingTurnsEW)]
        public void EachType_HasOneLotPerQuadrant_AtTheFourWayCornerDistance(TileType type)
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

        [Test]
        public void LotOffsetsFor_ReturnsTheSameFourPositionsAsLotsFor()
        {
            var lots = TileLotCatalog.LotsFor(TileType.TurnNE);
            var offsets = TileLotCatalog.LotOffsetsFor(TileType.TurnNE);

            CollectionAssert.AreEquivalent(lots.Values, offsets);
        }
    }
}
