using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #106: the walk network graph generated from NeighborhoodLayout's
    /// roads and house lots — sidewalks on both sides of every road
    /// segment, crosswalks wherever the network needs to continue across a
    /// road, and driveway stubs connecting each house lot to its nearest
    /// sidewalk edge. The resulting network must stay fully connected and
    /// support real shortest-path queries.
    /// </summary>
    public class WalkNetworkTests
    {
        private static WalkNetwork BuildStartingNetwork()
        {
            return WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, NeighborhoodLayout.HouseLots);
        }

        private static float SidewalkOffsetMagnitude()
        {
            return WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;
        }

        [Test]
        public void BuildFrom_DeclaresSidewalkEdgesOnBothSidesOfEveryRoad()
        {
            var network = BuildStartingNetwork();
            var offset = SidewalkOffsetMagnitude();

            foreach (var road in NeighborhoodLayout.Roads)
            {
                var sidewalkEdges = network.Edges.Where(e => e.Kind == WalkEdgeKind.Sidewalk).ToList();

                bool OnPositiveSide(WalkEdge e) => road.Orientation == StreetOrientation.NorthSouth
                    ? Math.Abs(e.A.X - offset) < 0.01f && Math.Abs(e.B.X - offset) < 0.01f
                    : Math.Abs(e.A.Z - offset) < 0.01f && Math.Abs(e.B.Z - offset) < 0.01f;

                bool OnNegativeSide(WalkEdge e) => road.Orientation == StreetOrientation.NorthSouth
                    ? Math.Abs(e.A.X + offset) < 0.01f && Math.Abs(e.B.X + offset) < 0.01f
                    : Math.Abs(e.A.Z + offset) < 0.01f && Math.Abs(e.B.Z + offset) < 0.01f;

                Assert.That(sidewalkEdges.Any(OnPositiveSide), Is.True,
                    $"expected a sidewalk edge on the positive side of {road.Orientation} road");
                Assert.That(sidewalkEdges.Any(OnNegativeSide), Is.True,
                    $"expected a sidewalk edge on the negative side of {road.Orientation} road");
            }
        }

        [Test]
        public void BuildFrom_SidewalkEdges_UseTheLockedSidewalkWidth()
        {
            var network = BuildStartingNetwork();

            foreach (var edge in network.Edges.Where(e => e.Kind == WalkEdgeKind.Sidewalk))
            {
                Assert.That(edge.Width, Is.EqualTo(WorldDimensions.SidewalkWidth));
            }
        }

        [Test]
        public void BuildFrom_CreatesExactlyFourCrosswalks_OnePerRoadArm()
        {
            // #106: the standard 4-crosswalk box at the one intersection —
            // N, S, E, W arms of the two crossing roads.
            var network = BuildStartingNetwork();

            var crosswalks = network.Edges.Where(e => e.Kind == WalkEdgeKind.Crosswalk).ToList();

            Assert.That(crosswalks.Count, Is.EqualTo(4));
            Assert.That(crosswalks.All(e => e.Width == WorldDimensions.CrosswalkWidth), Is.True);
        }

        [Test]
        public void BuildFrom_CreatesOneDrivewayStubPerHouseLot()
        {
            var network = BuildStartingNetwork();

            var driveways = network.Edges.Where(e => e.Kind == WalkEdgeKind.DrivewayStub).ToList();

            Assert.That(driveways.Count, Is.EqualTo(NeighborhoodLayout.HouseLots.Count));

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(driveways.Any(d => d.A.Equals(lot.Position) || d.B.Equals(lot.Position)), Is.True,
                    $"expected a driveway stub touching house lot {lot.HouseId}");
            }
        }

        [Test]
        public void BuildFrom_DrivewayStubs_ConnectToTheNearestSidewalkEdge()
        {
            // #106: for this map's symmetric NE lot at (14, 14), the
            // nearest sidewalk point sits exactly `offset` meters in from
            // the lot along either flanking sidewalk — a known, checkable
            // distance rather than an arbitrary one.
            var network = BuildStartingNetwork();
            var offset = SidewalkOffsetMagnitude();
            var lot = NeighborhoodLayout.GetHouseLot(1); // NorthEast, (14, 14)

            var driveway = network.Edges.Single(e =>
                e.Kind == WalkEdgeKind.DrivewayStub && (e.A.Equals(lot.Position) || e.B.Equals(lot.Position)));

            var expectedDistance = NeighborhoodLayout.LotDistanceFromCenter - offset;
            Assert.That(driveway.Length, Is.EqualTo(expectedDistance).Within(0.01f));
        }

        [Test]
        public void BuildFrom_ResultingNetwork_IsFullyConnected()
        {
            var network = BuildStartingNetwork();

            Assert.That(network.IsFullyConnected(), Is.True);
        }

        [Test]
        public void FindPath_FromAnywhereOnTheNetwork_ReachesAHouseLotViaItsDrivewayStub()
        {
            var network = BuildStartingNetwork();
            var lot = NeighborhoodLayout.GetHouseLot(3); // SouthEast

            // Start from a node on the far side of the network (a
            // north-west sidewalk arm point) to force a real multi-edge
            // route, not a trivial single hop.
            var start = new GridPoint(-SidewalkOffsetMagnitude(), 20f);
            var path = network.FindPath(start, lot.Position);

            Assert.That(path.Count, Is.GreaterThan(1));
            Assert.That(path[path.Count - 1], Is.EqualTo(lot.Position));

            // Every consecutive pair in the path must be a real edge in the network.
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                Assert.That(network.Edges.Any(e => (e.A.Equals(a) && e.B.Equals(b)) || (e.A.Equals(b) && e.B.Equals(a))),
                    Is.True, $"no network edge between {a} and {b}");
            }

            // The final hop must be the driveway stub (the only edge that
            // ever touches the house lot's own position).
            var lastEdgeKind = network.Edges.First(e =>
                (e.A.Equals(path[path.Count - 2]) && e.B.Equals(lot.Position))
                || (e.B.Equals(path[path.Count - 2]) && e.A.Equals(lot.Position))).Kind;
            Assert.That(lastEdgeKind, Is.EqualTo(WalkEdgeKind.DrivewayStub));
        }

        [Test]
        public void FindPath_NeverRoutesThroughAnotherHouseLot()
        {
            var network = BuildStartingNetwork();
            var otherLotPositions = NeighborhoodLayout.HouseLots
                .Where(l => l.HouseId != 3)
                .Select(l => l.Position)
                .ToHashSet();
            var lot = NeighborhoodLayout.GetHouseLot(3);

            var start = new GridPoint(-SidewalkOffsetMagnitude(), 20f);
            var path = network.FindPath(start, lot.Position);

            Assert.That(path.Any(otherLotPositions.Contains), Is.False);
        }
    }
}
