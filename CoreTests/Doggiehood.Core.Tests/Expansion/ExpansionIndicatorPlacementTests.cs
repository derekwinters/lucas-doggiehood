using System;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #178: where the map-expansion lock indicator hovers — just past the
    /// end of the road at the boundary between the currently placed map
    /// and the next locked zone's entrance tile (Derek, 2026-07-18, on
    /// #178: "The icon could be hovering just passed the end of the
    /// road."). Position derives from the #109 tile layout: the entrance
    /// tile's shared edge with the existing map, pushed further out by
    /// <see cref="ExpansionIndicatorNumbers.HoverOffset"/>.
    /// </summary>
    public class ExpansionIndicatorPlacementTests
    {
        [Test]
        public void Resolve_ForTheFirstZone_HoversPastTheNorthEdgeOfTheStartingIntersection()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            var position = ExpansionIndicatorPlacement.Resolve(map, ZoneCatalog.FirstZone);

            // The zone's entry tile (TurnSW at (0,1)) borders the starting
            // FourWay across the FourWay's North edge — the boundary is
            // that edge's midpoint, pushed further north (past the road's
            // end, into the not-yet-placed locked zone) by HoverOffset.
            var boundary = TileGeometry.EdgeMidpoint(new TileCoordinate(0, 0), TileEdge.North);
            Assert.That(position.X, Is.EqualTo(boundary.X));
            Assert.That(position.Z, Is.EqualTo(boundary.Z + ExpansionIndicatorNumbers.HoverOffset));
        }

        [Test]
        public void Resolve_Throws_WhenTheZonesEntranceDoesNotBorderTheGivenMap()
        {
            // A caller error: a map with nothing adjacent to the zone's
            // first placement has no boundary to hover past.
            var disconnectedMap = new TileMap(new TileCoordinate(5, 5), TileType.FourWay);

            Assert.Throws<InvalidOperationException>(
                () => ExpansionIndicatorPlacement.Resolve(disconnectedMap, ZoneCatalog.FirstZone));
        }
    }
}
