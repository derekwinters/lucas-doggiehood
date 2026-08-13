using System;
using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tests.World;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #677: the walk home after accepting a "buy me X" quest is planned over the
    /// LIVE map-spanning walk network (<see cref="GameState.WalkNetwork"/>), from
    /// wherever the dog is standing to its OWN front door — never over the
    /// origin-tile-only <c>NeighborhoodLayout</c> singleton, and never onto a
    /// substitute destination that <c>NearestNode</c> happened to snap to.
    ///
    /// The reported bug had both ends wrong at once: a dog off the origin tile
    /// "arrived" (and sat in the waiting pose) on a sidewalk nowhere near its
    /// house, and it got there by beelining diagonally across yards to the first
    /// waypoint. Both symptoms come from planning on a network the dog and/or its
    /// house are not on, so both are covered here.
    /// </summary>
    public class WalkHomeRouteTests
    {
        /// <summary>How close two points must be to count as the same place (m).</summary>
        private const float Tolerance = 0.01f;

        [Test]
        public void ADogOffTheOriginTile_RoutesToItsOwnFrontDoor_NotASnappedSidewalkNode()
        {
            // The dog lives in a PLAYER-BUILT house on an unlocked tile (id >= 5),
            // which the origin-only NeighborhoodLayout cannot resolve at all — the
            // exact case that snapped "home" onto the nearest sidewalk node (or
            // threw outright) and made the dog sit down in the street.
            var state = FrontierTestWorld.WithFirstTileUnlocked(500);
            Assert.That(state.TryBuildHouse(FrontierTestWorld.FirstLotId), Is.True,
                "precondition: the frontier lot must build");
            var houseId = FrontierTestWorld.FirstLotId;

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(houseId, out var walkway), Is.True,
                "precondition: the player-built house has a front walkway on the live network");

            // Standing way down on the origin tile's south sidewalk, as if it had
            // been out wandering the whole unlocked map (#398).
            var dogPosition = new GridPoint(4.75f, -30f);
            var route = WalkHomeRoute.Plan(state, houseId, dogPosition);

            Assert.That(Distance(route.Waypoints[route.Waypoints.Count - 1], walkway.A), Is.LessThan(Tolerance),
                "the route must END at the dog's own front door, not at whatever node happened to be nearest");
            Assert.That(Distance(route.FrontDoor, walkway.A), Is.LessThan(Tolerance));
        }

        [Test]
        public void APlayerBuiltHouseResident_PlansItsWalkHome_WithoutThrowing()
        {
            // NeighborhoodLayout.GetHouseLot throws for every id >= 5, so the old
            // call site threw for any player-built house — the throw that then
            // took the whole director's Update down with it.
            var state = FrontierTestWorld.WithFirstTileUnlocked(500);
            Assert.That(state.TryBuildHouse(FrontierTestWorld.FirstLotId), Is.True);

            Assert.That(
                () => WalkHomeRoute.Plan(state, FrontierTestWorld.FirstLotId, new GridPoint(0f, 30f)),
                Throws.Nothing,
                "a dog living in a player-built house must be able to plan its walk home");
        }

        [Test]
        public void EveryLegOfTheWalkHome_StaysOnTheWalkNetwork_IncludingTheFirstStep()
        {
            // The second reported symptom: a dog on an unlocked tile walked
            // diagonally across a yard to reach route[0], because the START was
            // snapped to the nearest NODE of a network it wasn't standing on.
            // Every leg — the entry leg included — must lie on paved surface.
            var state = FrontierTestWorld.WithFirstTileUnlocked(500);
            var network = state.WalkNetwork;

            // The top of the unlocked cul-de-sac's bulb arc: on the live network,
            // 60m+ from any node of the origin-only one.
            var dogPosition = network.Nodes.OrderByDescending(n => n.Z).First();
            var houseId = 3; // an original starting house, far to the south

            var route = WalkHomeRoute.Plan(state, houseId, dogPosition);

            var previous = dogPosition;
            foreach (var waypoint in route.Waypoints)
            {
                Assert.That(network.SegmentStaysOnPavement(previous, waypoint), Is.True,
                    $"the leg ({previous.X}, {previous.Z}) -> ({waypoint.X}, {waypoint.Z}) leaves the walk "
                    + "network — a dog walking home never crosses open ground, a yard, or the roadway "
                    + "off a crosswalk");
                previous = waypoint;
            }
        }

        [Test]
        public void TheRouteEntersTheNetwork_AtTheEdgeTheDogIsStandingOn_NotTheNearestNodeAnywhere()
        {
            // "Nearest node" is the wrong snap for the start even on the live
            // network: mid-edge, the nearest node can be many metres away and the
            // straight line to it need not follow the pavement. The route's first
            // hop must therefore stay on the edge the dog is already on.
            var state = FrontierTestWorld.WithFirstTileUnlocked(500);
            var network = state.WalkNetwork;

            // Mid-way along the long west sidewalk arm running from the origin
            // tile up to the unlocked one — 15m from either endpoint node.
            var dogPosition = new GridPoint(-4.75f, 45f);
            var route = WalkHomeRoute.Plan(state, 3, dogPosition);

            Assert.That(network.SegmentStaysOnPavement(dogPosition, route.Waypoints[0]), Is.True,
                "the first hop must run along the walk edge the dog is standing on");
            Assert.That(Distance(dogPosition, network.NearestNode(dogPosition)), Is.GreaterThan(5f),
                "test sanity: the nearest NODE is far away here, so this discriminates node-snapping");
        }

        [Test]
        public void AHouseWithNoFrontWalkway_FailsLoudly_RatherThanSubstitutingADestination()
        {
            // The silent NearestNode snap is what turned a lookup miss into "the
            // dog sits down in the middle of the street". A home that cannot be
            // resolved to the dog's own front-door node is now a contained,
            // loud failure instead.
            var state = GameState.CreateNew();
            const int unknownHouseId = 4242;

            Assert.That(
                () => WalkHomeRoute.Plan(state, unknownHouseId, new GridPoint(0f, 30f)),
                Throws.InstanceOf<InvalidOperationException>(),
                "no front door for this house means no walk home — never a nearest-sidewalk stand-in");
        }

        [Test]
        public void AStartingHouseDogOnTheOriginTile_StillWalksToItsDoorOverTheOriginNodes()
        {
            // Regression guard: the everyday case must behave exactly as before —
            // a real network path ending at the dog's own front door.
            var state = GameState.CreateNew();
            var network = state.WalkNetwork;
            Assert.That(network.TryGetFrontWalkway(3, out var walkway), Is.True);

            var dogPosition = new GridPoint(-4.75f, 30f); // far NW sidewalk tip
            var route = WalkHomeRoute.Plan(state, 3, dogPosition);

            Assert.That(Distance(route.Waypoints[route.Waypoints.Count - 1], walkway.A), Is.LessThan(Tolerance),
                "the dog arrives at its own front door");
            Assert.That(route.Waypoints.Count, Is.GreaterThan(2),
                "the route detours over the network rather than cutting straight home");

            var previous = dogPosition;
            foreach (var waypoint in route.Waypoints)
            {
                Assert.That(network.SegmentStaysOnPavement(previous, waypoint), Is.True,
                    "every leg of the starting-house walk home stays on the network too");
                previous = waypoint;
            }
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
