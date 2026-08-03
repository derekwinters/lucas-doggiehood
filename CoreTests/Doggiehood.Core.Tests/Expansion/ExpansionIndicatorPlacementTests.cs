using System;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #178/#453/#537: where a map-expansion lock indicator hovers — just past
    /// the road at the boundary between the currently placed map and a frontier
    /// coordinate (Derek, 2026-07-18, on #178: "The icon could be hovering just
    /// passed the end of the road."). Position derives from the #109 tile
    /// layout: the frontier coordinate's shared edge that carries a road on BOTH
    /// sides (#537 — never a grass/non-road neighbour edge), pushed further out
    /// by <see cref="ExpansionIndicatorNumbers.HoverOffset"/>. Keyed on a
    /// coordinate (#453 multi-lock frontier), not a retired <c>Zone</c>.
    /// </summary>
    public class ExpansionIndicatorPlacementTests
    {
        [Test]
        public void Resolve_ForANorthFrontierTile_HoversPastTheNorthEdgeOfTheStartingIntersection()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var frontier = new TileCoordinate(0, 1);

            // A frontier tile carrying a road on its south edge, into the
            // starting FourWay — an unambiguous single-road-neighbour case.
            var position = ExpansionIndicatorPlacement.Resolve(map, frontier, TileType.CulDeSacSouth);

            // The frontier coordinate (0,1) borders the starting FourWay across
            // the FourWay's North edge — the boundary is that edge's midpoint,
            // pushed further north (past the road's end, into the not-yet-placed
            // frontier) by HoverOffset.
            var boundary = TileGeometry.EdgeMidpoint(new TileCoordinate(0, 0), TileEdge.North);
            Assert.That(position.X, Is.EqualTo(boundary.X));
            Assert.That(position.Z, Is.EqualTo(boundary.Z + ExpansionIndicatorNumbers.HoverOffset));
        }

        [Test]
        public void Resolve_ForAnEastFrontierTile_HoversPastTheEastEdge()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var frontier = new TileCoordinate(1, 0);

            var position = ExpansionIndicatorPlacement.Resolve(map, frontier, TileType.CulDeSacWest);

            var boundary = TileGeometry.EdgeMidpoint(new TileCoordinate(0, 0), TileEdge.East);
            Assert.That(position.X, Is.EqualTo(boundary.X + ExpansionIndicatorNumbers.HoverOffset));
            Assert.That(position.Z, Is.EqualTo(boundary.Z));
        }

        [Test]
        public void Resolve_AnchorsToTheRoadEdge_NotAGrassNeighbourEdge()
        {
            // #537 repro (Derek): placed {6,-4} and {7,-3} but not {6,-3}. The
            // frontier {6,-3} borders {6,-4} across a GRASS edge (its south) and
            // {7,-3} across an open ROAD edge (its east). The lock must anchor to
            // the road edge, never the grass one — even though the grass
            // neighbour is reached first by the fixed N/S/E/W scan.
            //
            // Build a valid connected map reaching both placed tiles without
            // going through {6,-3}: origin {7,-3} → {7,-4} south → {6,-4} west.
            var map = new TileMap(new TileCoordinate(7, -3), TileType.TurnSW); // roads S + W
            map.Place(new TileCoordinate(7, -4), TileType.TurnNW);             // roads N + W
            map.Place(new TileCoordinate(6, -4), TileType.CulDeSacEast);       // road E only (north = grass)

            var frontier = new TileCoordinate(6, -3);
            // The frontier tile carries a road only on its east edge (into
            // {7,-3}); its south edge, toward {6,-4}, is grass.
            var position = ExpansionIndicatorPlacement.Resolve(map, frontier, TileType.CulDeSacEast);

            // Anchored to the east (road) edge shared with {7,-3}, pushed
            // HoverOffset back into the frontier tile (away from the placed map,
            // which lies east) — NOT to the south (grass) edge shared with {6,-4}.
            var roadBoundary = TileGeometry.EdgeMidpoint(frontier, TileEdge.East);
            Assert.That(position.X, Is.EqualTo(roadBoundary.X - ExpansionIndicatorNumbers.HoverOffset),
                "the lock must hover just inside the frontier's road edge, not its grass edge");
            Assert.That(position.Z, Is.EqualTo(roadBoundary.Z));

            // The buggy (grass-edge) placement would have hovered past the south
            // edge shared with {6,-4}: same X as the frontier centre, and a Z
            // pushed north off that edge. Neither must hold.
            var grassBoundary = TileGeometry.EdgeMidpoint(frontier, TileEdge.South);
            Assert.That(position.X, Is.Not.EqualTo(grassBoundary.X),
                "the lock must never anchor to the grass edge shared with {6,-4}");
        }

        [Test]
        public void Resolve_Throws_WhenNoSharedEdgeCarriesARoad()
        {
            // A caller error: the frontier coordinate borders the map only across
            // a grass/non-road edge, so there is no road connection point to
            // hover past. CulDeSacNorth has no south road, so its (grass) south
            // edge with the FourWay is not a road-carrying connection.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            Assert.Throws<InvalidOperationException>(
                () => ExpansionIndicatorPlacement.Resolve(map, new TileCoordinate(0, 1), TileType.CulDeSacNorth));
        }
    }
}
