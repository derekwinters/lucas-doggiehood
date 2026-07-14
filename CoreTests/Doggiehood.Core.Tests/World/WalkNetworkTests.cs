using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #106/#128: the walk network graph generated from NeighborhoodLayout's
    /// roads and house lots — sidewalks on both sides of every road
    /// segment, crosswalks wherever the network needs to continue across a
    /// road, and front walkways connecting each house's FRONT DOOR to its
    /// street-facing sidewalk. The walkways REPLACED the old driveway stubs
    /// (Derek's decision on #128: the neighborhood has no driveways), but
    /// keep the stubs' two contracts: general wander never enters one, and
    /// a walkway is the only way on/off a lot. The resulting network must
    /// stay fully connected and support real shortest-path queries.
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

        /// <summary>The lot's front-door position, derived only from public
        /// placement/catalog APIs — the same chain WorldBuilder and the #126
        /// gallery use — so the walkway tests can't drift from reality.</summary>
        private static GridPoint ExpectedDoor(HouseLot lot)
        {
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var scale = HousePlacement.HouseTargetFootprint / model.MaxFootprint;
            return model.FrontDoorWorldPosition(
                HousePlacement.Position(lot, HousePlacement.HouseTargetFootprint),
                HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot)),
                scale);
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
        public void BuildFrom_CreatesOneFrontWalkwayPerHouseLot()
        {
            // #128: every lot's single connection to the sidewalk network
            // is its front walkway, queryable by house id.
            var network = BuildStartingNetwork();

            var walkways = network.Edges.Where(e => e.Kind == WalkEdgeKind.FrontWalkway).ToList();

            Assert.That(walkways.Count, Is.EqualTo(NeighborhoodLayout.HouseLots.Count));

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var walkway), Is.True,
                    $"expected a front walkway for house {lot.HouseId}");
                Assert.That(walkways.Any(w => w.A.Equals(walkway.A) && w.B.Equals(walkway.B)), Is.True,
                    $"house {lot.HouseId}'s walkway must be one of the network's FrontWalkway edges");
                Assert.That(walkway.Width, Is.EqualTo(WorldDimensions.SidewalkWidth));
            }
        }

        [Test]
        public void FrontWalkways_RunFromTheFrontDoorToTheSidewalkLine_PerpendicularToTheStreet()
        {
            // #128: the walkway starts at the door position the #125
            // catalog defines for the #127 setback-adjusted house position,
            // and ends on the street-facing sidewalk's centerline, running
            // perpendicular to the street (this map's roads are
            // axis-aligned, so perpendicular = the lateral coordinate is
            // identical at both ends).
            var network = BuildStartingNetwork();
            var offset = SidewalkOffsetMagnitude();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var walkway), Is.True);

                var door = ExpectedDoor(lot);
                Assert.That(walkway.A.X, Is.EqualTo(door.X).Within(0.001f),
                    $"house {lot.HouseId} walkway must start at the front door (X)");
                Assert.That(walkway.A.Z, Is.EqualTo(door.Z).Within(0.001f),
                    $"house {lot.HouseId} walkway must start at the front door (Z)");

                var facing = HousePlacement.FrontFacing(lot);
                if (facing.X != 0f)
                {
                    Assert.That(Math.Abs(walkway.B.X), Is.EqualTo(offset).Within(0.001f),
                        $"house {lot.HouseId} walkway must end on the sidewalk centerline");
                    Assert.That(walkway.B.Z, Is.EqualTo(walkway.A.Z).Within(0.001f),
                        $"house {lot.HouseId} walkway must be perpendicular to its street");
                }
                else
                {
                    Assert.That(Math.Abs(walkway.B.Z), Is.EqualTo(offset).Within(0.001f),
                        $"house {lot.HouseId} walkway must end on the sidewalk centerline");
                    Assert.That(walkway.B.X, Is.EqualTo(walkway.A.X).Within(0.001f),
                        $"house {lot.HouseId} walkway must be perpendicular to its street");
                }
            }
        }

        [Test]
        public void BuildFrom_TheDrivewayStubIsGone_ReplacedByTheFrontWalkway()
        {
            // Decision (Derek, #128): the neighborhood has NO driveways —
            // the front walkway REPLACES the DrivewayStub edge outright
            // (rename, not coexistence), and the lot-side node of a lot's
            // connection is now the front door, not the lot center.
            Assert.That(Enum.GetNames(typeof(WalkEdgeKind)), Has.No.Member("DrivewayStub"));

            var network = BuildStartingNetwork();
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(network.Edges.Any(e => e.A.Equals(lot.Position) || e.B.Equals(lot.Position)),
                    Is.False, $"no edge should attach to house {lot.HouseId}'s lot center anymore");
            }
        }

        [Test]
        public void BuildFrom_ResultingNetwork_IsFullyConnected()
        {
            var network = BuildStartingNetwork();

            Assert.That(network.IsFullyConnected(), Is.True);
        }

        [Test]
        public void FindPath_FromAnywhereOnTheNetwork_ReachesAFrontDoorViaItsWalkway()
        {
            var network = BuildStartingNetwork();
            var lot = NeighborhoodLayout.GetHouseLot(3); // SouthEast
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var walkway), Is.True);
            var door = walkway.A;

            // Start from a node on the far side of the network (a
            // north-west sidewalk arm point) to force a real multi-edge
            // route, not a trivial single hop.
            var start = new GridPoint(-SidewalkOffsetMagnitude(), 20f);
            var path = network.FindPath(start, door);

            Assert.That(path.Count, Is.GreaterThan(1));
            Assert.That(path[path.Count - 1], Is.EqualTo(door));

            // Every consecutive pair in the path must be a real edge in the network.
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                Assert.That(network.Edges.Any(e => (e.A.Equals(a) && e.B.Equals(b)) || (e.A.Equals(b) && e.B.Equals(a))),
                    Is.True, $"no network edge between {a} and {b}");
            }

            // The final hop must be the front walkway (the only edge that
            // ever touches the house's door node).
            var lastEdgeKind = network.Edges.First(e =>
                (e.A.Equals(path[path.Count - 2]) && e.B.Equals(door))
                || (e.B.Equals(path[path.Count - 2]) && e.A.Equals(door))).Kind;
            Assert.That(lastEdgeKind, Is.EqualTo(WalkEdgeKind.FrontWalkway));
        }

        [Test]
        public void NearestWalkableNode_NeverReturnsAFrontDoorNode()
        {
            // #106/#128: wander must never snap onto a walkway-only node (a
            // house's front door), even if queried from right next to one.
            var network = BuildStartingNetwork();
            Assert.That(network.TryGetFrontWalkway(1, out var walkway), Is.True);

            var nearest = network.NearestWalkableNode(walkway.A);

            Assert.That(nearest, Is.Not.EqualTo(walkway.A));
        }

        [Test]
        public void FindPath_NeverRoutesThroughAnotherHousesFrontDoor()
        {
            var network = BuildStartingNetwork();
            var otherDoors = NeighborhoodLayout.HouseLots
                .Where(l => l.HouseId != 3)
                .Select(l =>
                {
                    network.TryGetFrontWalkway(l.HouseId, out var w);
                    return w.A;
                })
                .ToHashSet();
            Assert.That(network.TryGetFrontWalkway(3, out var target), Is.True);

            var start = new GridPoint(-SidewalkOffsetMagnitude(), 20f);
            var path = network.FindPath(start, target.A);

            Assert.That(path.Any(otherDoors.Contains), Is.False);
        }
    }
}
