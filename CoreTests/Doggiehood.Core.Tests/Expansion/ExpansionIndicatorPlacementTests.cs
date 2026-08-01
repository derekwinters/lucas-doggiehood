using System;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #178/#453: where a map-expansion lock indicator hovers — just past the
    /// end of the road at the boundary between the currently placed map and a
    /// frontier coordinate (Derek, 2026-07-18, on #178: "The icon could be
    /// hovering just passed the end of the road."). Position derives from the
    /// #109 tile layout: the frontier coordinate's shared edge with the placed
    /// map, pushed further out by <see cref="ExpansionIndicatorNumbers.HoverOffset"/>.
    /// Keyed on a coordinate (#453 multi-lock frontier), not a retired
    /// <c>Zone</c>.
    /// </summary>
    public class ExpansionIndicatorPlacementTests
    {
        [Test]
        public void Resolve_ForANorthFrontierTile_HoversPastTheNorthEdgeOfTheStartingIntersection()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var frontier = new TileCoordinate(0, 1);

            var position = ExpansionIndicatorPlacement.Resolve(map, frontier);

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

            var position = ExpansionIndicatorPlacement.Resolve(map, frontier);

            var boundary = TileGeometry.EdgeMidpoint(new TileCoordinate(0, 0), TileEdge.East);
            Assert.That(position.X, Is.EqualTo(boundary.X + ExpansionIndicatorNumbers.HoverOffset));
            Assert.That(position.Z, Is.EqualTo(boundary.Z));
        }

        [Test]
        public void Resolve_Throws_WhenTheCoordinateDoesNotBorderTheGivenMap()
        {
            // A caller error: a coordinate with nothing adjacent on the map has
            // no boundary to hover past.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            Assert.Throws<InvalidOperationException>(
                () => ExpansionIndicatorPlacement.Resolve(map, new TileCoordinate(5, 5)));
        }
    }
}
