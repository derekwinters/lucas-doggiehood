using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #599: the road graph a delivery truck routes over. It is derived from the
    /// live map's <see cref="MapWalkNetwork.RoadsFrom"/> centerlines, so a route
    /// found on it always stays on the roadway across as many tiles as the path
    /// spans. Endpoints of a query (an off-map opening, a stop projected onto a
    /// road) may fall mid-segment; the graph injects them so the returned
    /// waypoints run opening→…→stop along centerlines.
    /// </summary>
    public class RoadNetworkGraphTests
    {
        private const float Half = WorldDimensions.TileSize / 2f;

        private static TileMap TwoStackedStraightNs()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.StraightNS);
            map.Place(new TileCoordinate(0, 1), TileType.StraightNS);
            return map;
        }

        [Test]
        public void ShortestPath_OnASingleRoad_IsADirectSegment()
        {
            var graph = new RoadNetworkGraph(MapWalkNetwork.RoadsFrom(
                new TileMap(new TileCoordinate(0, 0), TileType.StraightNS)));

            var path = graph.ShortestPath(new GridPoint(0f, -Half), new GridPoint(0f, 10f));

            Assert.That(path.Count, Is.EqualTo(2));
            Assert.That(path[0].Z, Is.EqualTo(-Half).Within(0.001f));
            Assert.That(path[path.Count - 1].Z, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void ShortestPath_AcrossTwoTiles_PassesThroughTheSharedBoundaryOnCenterline()
        {
            var graph = new RoadNetworkGraph(MapWalkNetwork.RoadsFrom(TwoStackedStraightNs()));

            // From the south off-map opening to a stop on the northern tile's road.
            var stop = new GridPoint(0f, 45f);
            var path = graph.ShortestPath(new GridPoint(0f, -Half), stop);

            Assert.That(path.Count, Is.GreaterThanOrEqualTo(3),
                "an opening→stop path across two tiles must include the boundary node");
            Assert.That(path[0].Z, Is.EqualTo(-Half).Within(0.001f));
            Assert.That(path[path.Count - 1].Z, Is.EqualTo(45f).Within(0.001f));
            // The shared boundary node (0, +Half) is on the way.
            Assert.That(path.Any(p => System.Math.Abs(p.Z - Half) < 0.001f), Is.True);
            // Everything stays on the x=0 centerline.
            Assert.That(path.All(p => System.Math.Abs(p.X) < 0.001f), Is.True);
        }

        [Test]
        public void ShortestPath_ThroughAFourWay_RoutesViaTheCrossingCentre()
        {
            // A four-way's centre is the midpoint of both roads, not an endpoint,
            // so the graph must find it as a crossing to connect the two roads.
            var graph = new RoadNetworkGraph(MapWalkNetwork.RoadsFrom(
                new TileMap(new TileCoordinate(0, 0), TileType.FourWay)));

            var path = graph.ShortestPath(new GridPoint(0f, Half), new GridPoint(Half, 0f));

            Assert.That(path[0].Z, Is.EqualTo(Half).Within(0.001f));
            Assert.That(path[path.Count - 1].X, Is.EqualTo(Half).Within(0.001f));
            Assert.That(
                path.Any(p => System.Math.Abs(p.X) < 0.001f && System.Math.Abs(p.Z) < 0.001f),
                Is.True,
                "the route from the north road to the east road must pass through the (0,0) crossing");
        }

        [Test]
        public void TryShortestPath_ReturnsFalse_WhenTheTargetIsOnADisconnectedRoadCluster()
        {
            // Two StraightNS clusters separated by a roadless GreenSpace tile:
            // their roads never touch, so one is unreachable from the other.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.StraightNS);
            map.Place(new TileCoordinate(1, 0), TileType.GreenSpace);
            map.Place(new TileCoordinate(2, 0), TileType.StraightNS);
            var graph = new RoadNetworkGraph(MapWalkNetwork.RoadsFrom(map));

            var reachable = graph.TryShortestPath(
                new GridPoint(0f, -Half),
                new GridPoint(2 * WorldDimensions.TileSize, Half),
                out _);

            Assert.That(reachable, Is.False);
        }
    }
}
