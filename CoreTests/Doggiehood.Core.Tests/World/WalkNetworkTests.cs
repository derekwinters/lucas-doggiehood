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
            return model.FrontDoorWorldPosition(
                HousePlacement.Position(lot, HousePlacement.KitScale),
                HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot)),
                HousePlacement.KitScale);
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
        public void FrontWalkways_LengthsVaryPerHouse_ByTheDoorsRecessBehindTheFacade()
        {
            // Gallery pass 1 (2026-07-14): the authored doors are recessed
            // behind the front facade (porches), so walkway lengths are no
            // longer a uniform 3.75m — each runs FrontSetback +
            // SidewalkWidth/2 (facade to sidewalk centerline) PLUS the
            // model's scaled door recess depth. Guards against anything
            // quietly re-pinning the old uniform length.
            var network = BuildStartingNetwork();
            var baseLength = HousePlacement.FrontSetback + WorldDimensions.SidewalkWidth / 2f;

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var walkway), Is.True);

                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var recess = HousePlacement.KitScale * (model.FrontDoorLocalZ + model.FootprintZ / 2f);

                Assert.That(recess, Is.GreaterThan(0f),
                    $"house {lot.HouseId}'s authored door should be recessed behind the facade");
                Assert.That(walkway.Length, Is.EqualTo(baseLength + recess).Within(0.001f),
                    $"house {lot.HouseId} walkway length");
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

        /// <summary>The lot's front-door position at a given level, via the
        /// same level-aware placement/catalog chain WorldBuilder uses (#454).</summary>
        private static GridPoint DoorAt(HouseLot lot, int level)
        {
            var model = HouseModelCatalog.ForHouse(lot.HouseId, level);
            return model.FrontDoorWorldPosition(
                HousePlacement.Position(lot, HousePlacement.KitScale, level),
                HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot)),
                HousePlacement.KitScale);
        }

        [Test]
        public void RefreshFrontWalkway_AfterALevelChange_MovesTheDoorNode_AndReProjectsTheAttach()
        {
            // #454: the front-walkway edge is baked once at build time from the
            // level-1 door and never recomputed, so an upgraded house's walkway
            // still ran to the stale level-1 door. RefreshFrontWalkway recomputes
            // the door-side node from the level-aware door position and
            // re-projects the sidewalk attach point.
            var network = BuildStartingNetwork();
            var lot = NeighborhoodLayout.GetHouseLot(1); // r -> c: door moves L1->L2
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var before), Is.True);

            const int newLevel = 2;
            var l1Door = DoorAt(lot, 1);
            var l2Door = DoorAt(lot, newLevel);
            Assert.That(l2Door, Is.Not.EqualTo(l1Door),
                "sanity: house 1's level-2 door differs from level 1");

            Assert.That(network.RefreshFrontWalkway(lot, newLevel), Is.True);
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var after), Is.True);

            // The door-side node A moved to the level-2 door.
            Assert.That(after.A.X, Is.EqualTo(l2Door.X).Within(0.001f),
                "walkway door node must move to the level-2 door (X)");
            Assert.That(after.A.Z, Is.EqualTo(l2Door.Z).Within(0.001f),
                "walkway door node must move to the level-2 door (Z)");
            Assert.That(after.A, Is.Not.EqualTo(before.A),
                "the door node must actually move on a level change");

            // The attach point re-projects onto the sidewalk, perpendicular to
            // the street (this map is axis-aligned).
            var facing = HousePlacement.FrontFacing(lot);
            if (facing.X != 0f)
            {
                Assert.That(after.B.Z, Is.EqualTo(after.A.Z).Within(0.001f),
                    "walkway must stay perpendicular to its street after refresh");
            }
            else
            {
                Assert.That(after.B.X, Is.EqualTo(after.A.X).Within(0.001f),
                    "walkway must stay perpendicular to its street after refresh");
            }

            // Exactly one front walkway remains for the house, and the graph is
            // still fully connected (the re-attach re-splits the sidewalk).
            Assert.That(
                network.Edges.Count(e => e.Kind == WalkEdgeKind.FrontWalkway
                    && e.A.Equals(after.A) && e.B.Equals(after.B)),
                Is.EqualTo(1), "exactly one refreshed front walkway for the house");
            Assert.That(network.Edges.Count(e => e.Kind == WalkEdgeKind.FrontWalkway),
                Is.EqualTo(NeighborhoodLayout.HouseLots.Count),
                "no extra or dropped front walkways after refresh");
            Assert.That(network.IsFullyConnected(), Is.True,
                "the network stays fully connected after a walkway refresh");
        }

        [Test]
        public void RefreshFrontWalkway_WhenTheDoorShiftsLaterally_SlidesTheAttach_AndStaysConnected()
        {
            // #454: house 1's level-4 mesh (building-type-b) has an off-centre
            // door, so the perpendicular attach slides ALONG the sidewalk from
            // the level-1 attach. The refresh must move that sidewalk split node
            // (not orphan the door) and keep the graph fully connected.
            var network = BuildStartingNetwork();
            var lot = NeighborhoodLayout.GetHouseLot(1);
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var before), Is.True);

            const int topLevel = 4;
            var l4Door = DoorAt(lot, topLevel);
            var facing = HousePlacement.FrontFacing(lot);
            // The off-centre door genuinely shifts the attach laterally.
            var attachMovesAlong = facing.X != 0f
                ? Math.Abs(l4Door.Z - before.B.Z) > 0.01f
                : Math.Abs(l4Door.X - before.B.X) > 0.01f;
            Assert.That(attachMovesAlong, Is.True,
                "sanity: house 1's level-4 door sits off-centre, sliding the attach");

            Assert.That(network.RefreshFrontWalkway(lot, topLevel), Is.True);
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var after), Is.True);

            Assert.That(after.A.X, Is.EqualTo(l4Door.X).Within(0.001f));
            Assert.That(after.A.Z, Is.EqualTo(l4Door.Z).Within(0.001f));
            if (facing.X != 0f)
            {
                Assert.That(after.B.Z, Is.EqualTo(after.A.Z).Within(0.001f),
                    "attach re-projects perpendicular after sliding along the sidewalk");
                Assert.That(after.B.Z, Is.Not.EqualTo(before.B.Z).Within(0.001f),
                    "the attach actually slid along the sidewalk");
            }
            else
            {
                Assert.That(after.B.X, Is.EqualTo(after.A.X).Within(0.001f),
                    "attach re-projects perpendicular after sliding along the sidewalk");
                Assert.That(after.B.X, Is.Not.EqualTo(before.B.X).Within(0.001f),
                    "the attach actually slid along the sidewalk");
            }

            Assert.That(network.IsFullyConnected(), Is.True,
                "moving the sidewalk split node keeps the network fully connected");

            // A dog can still path from across the network to the upgraded door.
            var start = new GridPoint(-SidewalkOffsetMagnitude(), 20f);
            var path = network.FindPath(start, after.A);
            Assert.That(path.Count, Is.GreaterThan(1));
            Assert.That(path[path.Count - 1], Is.EqualTo(after.A),
                "the refreshed door is still reachable over the network");
        }

        [Test]
        public void RefreshFrontWalkway_AtTheSameLevel_LeavesTheWalkwayInPlace()
        {
            // #454 guard: refreshing at level 1 (the as-built level) must
            // reproduce the same walkway edge — the refresh path is a superset,
            // not a behavior change for a never-upgraded house.
            var network = BuildStartingNetwork();
            var lot = NeighborhoodLayout.GetHouseLot(2);
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var before), Is.True);

            Assert.That(network.RefreshFrontWalkway(lot, 1), Is.True);
            Assert.That(network.TryGetFrontWalkway(lot.HouseId, out var after), Is.True);

            Assert.That(after.A.X, Is.EqualTo(before.A.X).Within(0.0001f));
            Assert.That(after.A.Z, Is.EqualTo(before.A.Z).Within(0.0001f));
            Assert.That(after.B.X, Is.EqualTo(before.B.X).Within(0.0001f));
            Assert.That(after.B.Z, Is.EqualTo(before.B.Z).Within(0.0001f));
            Assert.That(network.IsFullyConnected(), Is.True);
        }

        [Test]
        public void GroundHeight_OnASidewalkEdge_IsTheSidewalkSurfaceHeight()
        {
            // #151: the Kenney City Kit road tiles model the sidewalk band
            // raised above the road surface — a dog hopping between two
            // nodes joined by a Sidewalk edge must snap up to that surface,
            // not stay at the road's Y.
            var network = BuildStartingNetwork();
            var sidewalkEdge = network.Edges.First(e => e.Kind == WalkEdgeKind.Sidewalk);

            Assert.That(network.GroundHeight(sidewalkEdge.A, sidewalkEdge.B),
                Is.EqualTo(WorldDimensions.SidewalkSurfaceHeight));
            Assert.That(network.GroundHeight(sidewalkEdge.B, sidewalkEdge.A),
                Is.EqualTo(WorldDimensions.SidewalkSurfaceHeight),
                "must resolve the same edge regardless of query direction");
        }

        [Test]
        public void GroundHeight_OnACrosswalkEdge_IsTheRoadSurfaceHeight()
        {
            // Crosswalks are painted flat onto the road itself (#106) —
            // never raised — so crossing one must keep a dog at road level,
            // even though both of a crosswalk's endpoint nodes also carry
            // sidewalk edges of their own (the box-corner nodes).
            var network = BuildStartingNetwork();
            var crosswalkEdge = network.Edges.First(e => e.Kind == WalkEdgeKind.Crosswalk);

            Assert.That(network.GroundHeight(crosswalkEdge.A, crosswalkEdge.B),
                Is.EqualTo(WorldDimensions.RoadSurfaceHeight));
        }

        [Test]
        public void GroundHeight_OnAFrontWalkwayEdge_IsTheSidewalkSurfaceHeight()
        {
            // The paver walkway attaches directly to the sidewalk it
            // connects to, so it shares that raised surface.
            var network = BuildStartingNetwork();
            Assert.That(network.TryGetFrontWalkway(1, out var walkway), Is.True);

            Assert.That(network.GroundHeight(walkway.A, walkway.B),
                Is.EqualTo(WorldDimensions.SidewalkSurfaceHeight));
        }

        [Test]
        public void GroundHeight_BetweenUnconnectedPoints_FallsBackToTheRoadSurfaceHeight()
        {
            var network = BuildStartingNetwork();
            var farAway = new GridPoint(9999f, 9999f);

            Assert.That(network.GroundHeight(farAway, farAway),
                Is.EqualTo(WorldDimensions.RoadSurfaceHeight));
        }
    }
}
