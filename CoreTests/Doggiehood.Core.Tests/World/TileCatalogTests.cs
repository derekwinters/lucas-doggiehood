using System;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #109: the tile-type catalog as data — docs/specs/world/tile-catalog.md's
    /// types, each declaring which edges carry a road. #539 added the roadless
    /// GreenSpace type; #583 removed the two OpposingTurns "twin bends",
    /// leaving 16.
    /// </summary>
    public class TileCatalogTests
    {
        // #583: the full TileType value set, pinned by name rather than by
        // count alone so neither the removed OpposingTurnsNS/OpposingTurnsEW
        // twin bends nor any other type can be reintroduced silently. Derek
        // settled the removal on #583 ("remove tile completely") — the two
        // types were unplaceable (Map Builder) and unplaced (live map), and
        // had no kit render path.
        private static readonly TileType[] ExpectedTypes =
        {
            TileType.FourWay,
            TileType.StraightNS,
            TileType.StraightEW,
            TileType.TurnNE,
            TileType.TurnNW,
            TileType.TurnSE,
            TileType.TurnSW,
            TileType.TeeNorth,
            TileType.TeeSouth,
            TileType.TeeEast,
            TileType.TeeWest,
            TileType.CulDeSacNorth,
            TileType.CulDeSacSouth,
            TileType.CulDeSacEast,
            TileType.CulDeSacWest,
            TileType.GreenSpace,
        };

        [Test]
        public void Types_ExposesExactlyTheDefinedCatalogTypes()
        {
            var allEnumValues = (TileType[])Enum.GetValues(typeof(TileType));

            Assert.That(allEnumValues.Length, Is.EqualTo(16));
            CollectionAssert.AreEquivalent(ExpectedTypes, allEnumValues);
            CollectionAssert.AreEquivalent(allEnumValues, TileCatalog.Types);
        }

        [TestCase(TileType.FourWay, new[] { TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West })]
        [TestCase(TileType.StraightNS, new[] { TileEdge.North, TileEdge.South })]
        [TestCase(TileType.StraightEW, new[] { TileEdge.East, TileEdge.West })]
        [TestCase(TileType.TurnNE, new[] { TileEdge.North, TileEdge.East })]
        [TestCase(TileType.TurnNW, new[] { TileEdge.North, TileEdge.West })]
        [TestCase(TileType.TurnSE, new[] { TileEdge.South, TileEdge.East })]
        [TestCase(TileType.TurnSW, new[] { TileEdge.South, TileEdge.West })]
        [TestCase(TileType.TeeNorth, new[] { TileEdge.East, TileEdge.West, TileEdge.North })]
        [TestCase(TileType.TeeSouth, new[] { TileEdge.East, TileEdge.West, TileEdge.South })]
        [TestCase(TileType.TeeEast, new[] { TileEdge.North, TileEdge.South, TileEdge.East })]
        [TestCase(TileType.TeeWest, new[] { TileEdge.North, TileEdge.South, TileEdge.West })]
        [TestCase(TileType.CulDeSacNorth, new[] { TileEdge.North })]
        [TestCase(TileType.CulDeSacSouth, new[] { TileEdge.South })]
        [TestCase(TileType.CulDeSacEast, new[] { TileEdge.East })]
        [TestCase(TileType.CulDeSacWest, new[] { TileEdge.West })]
        public void EachType_DeclaresItsDocumentedRoadEdges(TileType type, TileEdge[] expectedEdges)
        {
            var definition = TileCatalog.Get(type);

            CollectionAssert.AreEquivalent(expectedEdges, definition.RoadEdges);
        }

    }
}
