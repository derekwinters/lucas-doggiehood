using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #109: the tile-type catalog as data — docs/specs/world/tile-catalog.md's
    /// 17 types, each declaring which edges carry a road.
    /// </summary>
    public class TileCatalogTests
    {
        [Test]
        public void Types_ExposesExactlyTheDefinedCatalogTypes()
        {
            var allEnumValues = (TileType[])Enum.GetValues(typeof(TileType));

            Assert.That(allEnumValues.Length, Is.EqualTo(17));
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

        [Test]
        public void OpposingTurnsNS_HasTwoIndependentAdjacentSideArcs_NoLoop()
        {
            // Resolved on #109 (overrides the #105 "loop/island" framing):
            // the two turns on an OpposingTurns tile do NOT connect into a
            // loop. Each arc joins only its own two adjacent (not opposite)
            // sides, so OpposingTurnsNS is a NE-corner arc and an unrelated
            // SW-corner arc — together touching all four edges, but never
            // routing from one arc into the other.
            var definition = TileCatalog.Get(TileType.OpposingTurnsNS);

            Assert.That(definition.Arcs.Count, Is.EqualTo(2));
            CollectionAssert.Contains(definition.Arcs, new TileArc(TileEdge.North, TileEdge.East));
            CollectionAssert.Contains(definition.Arcs, new TileArc(TileEdge.South, TileEdge.West));

            // The two arcs share no edge (fully independent).
            var firstArcEdges = new[] { definition.Arcs[0].First, definition.Arcs[0].Second };
            var secondArcEdges = new[] { definition.Arcs[1].First, definition.Arcs[1].Second };
            CollectionAssert.IsEmpty(firstArcEdges.Intersect(secondArcEdges));

            // Together the two arcs account for the type's full road-edge set.
            CollectionAssert.AreEquivalent(
                new[] { TileEdge.North, TileEdge.East, TileEdge.South, TileEdge.West },
                definition.RoadEdges);
        }

        [Test]
        public void OpposingTurnsEW_HasTwoIndependentAdjacentSideArcs_NoLoop()
        {
            // The 90-degree rotation of OpposingTurnsNS (docs/specs/world/tile-catalog.md):
            // a NW-corner arc and an unrelated SE-corner arc.
            var definition = TileCatalog.Get(TileType.OpposingTurnsEW);

            Assert.That(definition.Arcs.Count, Is.EqualTo(2));
            CollectionAssert.Contains(definition.Arcs, new TileArc(TileEdge.North, TileEdge.West));
            CollectionAssert.Contains(definition.Arcs, new TileArc(TileEdge.South, TileEdge.East));

            CollectionAssert.AreEquivalent(
                new[] { TileEdge.North, TileEdge.East, TileEdge.South, TileEdge.West },
                definition.RoadEdges);
        }

        [Test]
        public void OpposingTurns_AdjacencyTreatsEachArcsRoadEndsIndependently()
        {
            // A road end belongs to exactly one arc; querying "what does
            // this edge connect to inside the tile" must never leak the
            // other, unconnected arc's edges — this is what "no loop" means
            // for adjacency purposes.
            var ns = TileCatalog.Get(TileType.OpposingTurnsNS);
            CollectionAssert.AreEquivalent(new[] { TileEdge.East }, ns.EdgesConnectedVia(TileEdge.North));
            CollectionAssert.AreEquivalent(new[] { TileEdge.North }, ns.EdgesConnectedVia(TileEdge.East));
            CollectionAssert.AreEquivalent(new[] { TileEdge.West }, ns.EdgesConnectedVia(TileEdge.South));
            CollectionAssert.AreEquivalent(new[] { TileEdge.South }, ns.EdgesConnectedVia(TileEdge.West));

            var ew = TileCatalog.Get(TileType.OpposingTurnsEW);
            CollectionAssert.AreEquivalent(new[] { TileEdge.West }, ew.EdgesConnectedVia(TileEdge.North));
            CollectionAssert.AreEquivalent(new[] { TileEdge.East }, ew.EdgesConnectedVia(TileEdge.South));
        }

        [Test]
        public void NonOpposingTurnsTypes_HaveNoArcsModeled()
        {
            // Arcs only describe the OpposingTurns tiles' two-disconnected-
            // path shape (#109) — every other type's edges belong to one
            // single connected junction/dead-end, so Arcs stays empty there.
            Assert.That(TileCatalog.Get(TileType.FourWay).Arcs, Is.Empty);
            Assert.That(TileCatalog.Get(TileType.TurnNE).Arcs, Is.Empty);
            Assert.That(TileCatalog.Get(TileType.CulDeSacNorth).Arcs, Is.Empty);
        }
    }
}
