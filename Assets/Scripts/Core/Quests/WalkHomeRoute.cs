using System;
using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// The walk home a dog takes after its "buy me X" quest is accepted (#30,
    /// #106, #128): the ordered waypoints from wherever it is standing to its OWN
    /// front door, over the LIVE map-spanning walk network.
    ///
    /// #677 moved this planning out of the Unity director and onto live state. It
    /// used to run against the origin-tile-only <c>NeighborhoodLayout</c>
    /// singleton while dogs wander the whole unlocked map (#398) and live in
    /// houses built on unlocked tiles (#453) — so both ends of the route could be
    /// off the network it was planned on, and <c>FindPath</c>'s silent
    /// nearest-node snap turned that into a route that ENDED on a sidewalk in the
    /// middle of the street (where the dog then sat in the waiting pose) and
    /// STARTED with a 30m beeline diagonally across the neighbours' yards.
    ///
    /// Both ends are therefore pinned here:
    /// <list type="bullet">
    /// <item>the destination is the dog's own front-door node or nothing —
    /// resolved via <see cref="WalkNetwork.TryGetFrontWalkway"/> and routed with
    /// the strict <see cref="WalkNetwork.FindPathBetween"/>, so a lookup miss is a
    /// loud failure the caller contains, never a substitute destination;</item>
    /// <item>the entry is the nearest point on the nearest walk EDGE
    /// (<see cref="WalkNetwork.TryProjectOntoNearestEdge"/>), so the first hop
    /// runs along the pavement the dog is already standing on.</item>
    /// </list>
    ///
    /// Engine-free: the Unity layer walks <see cref="Waypoints"/> frame by frame.
    /// </summary>
    public sealed class WalkHomeRoute
    {
        /// <summary>How close (metres) counts as already being on a waypoint —
        /// the same slack the walk itself uses (#161).</summary>
        public const float ArriveDistance = 0.05f;

        private readonly List<GridPoint> waypoints;

        /// <summary>The ordered waypoints, ending at <see cref="FrontDoor"/>.
        /// Never empty: a dog already at its door gets the door itself.</summary>
        public IReadOnlyList<GridPoint> Waypoints
        {
            get { return waypoints; }
        }

        /// <summary>The dog's own front door — the lot-side node of its house's
        /// front walkway, and the only place this route may end.</summary>
        public GridPoint FrontDoor { get; }

        private WalkHomeRoute(List<GridPoint> waypoints, GridPoint frontDoor)
        {
            this.waypoints = waypoints;
            FrontDoor = frontDoor;
        }

        /// <summary>
        /// Plans the walk home for a dog of <paramref name="houseId"/> standing at
        /// <paramref name="dogPosition"/>, over <paramref name="state"/>'s live
        /// walk network. Throws <see cref="InvalidOperationException"/> when the
        /// house has no front door on that network, or when no walk route reaches
        /// it — deliberately loud, so a caller contains one quest's failure rather
        /// than walking the dog to a stand-in destination.
        /// </summary>
        public static WalkHomeRoute Plan(GameState state, int houseId, GridPoint dogPosition)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var network = state.WalkNetwork;

            if (!network.TryGetFrontWalkway(houseId, out var walkway))
            {
                throw new InvalidOperationException(
                    $"House {houseId} has no front walkway on the live walk network, so there is no front "
                    + "door for its dog to walk home to. A dog is never routed to a substitute destination.");
            }

            var door = walkway.A;

            if (!network.TryProjectOntoNearestEdge(dogPosition, out var foot, out var entryEdge))
            {
                throw new InvalidOperationException(
                    "The live walk network carries no edges, so there is nothing to walk home over.");
            }

            var path = ShortestPathVia(network, entryEdge, foot, door);
            if (path == null)
            {
                throw new InvalidOperationException(
                    $"No walk route connects ({dogPosition.X}, {dogPosition.Z}) to house {houseId}'s front "
                    + "door on the live walk network.");
            }

            var route = new List<GridPoint>();

            // Step onto the pavement first only when the dog is actually off it —
            // a dog already on an edge starts straight along that edge instead of
            // taking a zero-length hop.
            if (Distance(dogPosition, foot) > ArriveDistance)
            {
                route.Add(foot);
            }

            foreach (var node in path)
            {
                if (route.Count == 0 && Distance(dogPosition, node) <= ArriveDistance)
                {
                    // The dog is already standing on this node — nothing to walk.
                    continue;
                }

                if (route.Count > 0 && Distance(route[route.Count - 1], node) <= ArriveDistance)
                {
                    continue;
                }

                route.Add(node);
            }

            // A dog that is already at its own door still gets the door as its
            // single waypoint, so arrival stays "consumed the last waypoint".
            if (route.Count == 0)
            {
                route.Add(door);
            }

            return new WalkHomeRoute(route, door);
        }

        /// <summary>
        /// The cheaper of the two ways off the edge the dog is standing on: along
        /// it to <see cref="WalkEdge.A"/> then over the graph, or along it to
        /// <see cref="WalkEdge.B"/> then over the graph. Both hops run on that
        /// edge's own pavement, which is what keeps the entry leg on the network.
        /// Null when neither endpoint can reach the door.
        /// </summary>
        private static IReadOnlyList<GridPoint> ShortestPathVia(
            WalkNetwork network, WalkEdge entryEdge, GridPoint foot, GridPoint door)
        {
            IReadOnlyList<GridPoint> best = null;
            var bestCost = float.MaxValue;

            foreach (var endpoint in new[] { entryEdge.A, entryEdge.B })
            {
                var path = network.FindPathBetween(endpoint, door);
                if (path.Count == 0)
                {
                    continue;
                }

                var cost = Distance(foot, endpoint) + Length(path);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = path;
                }
            }

            return best;
        }

        private static float Length(IReadOnlyList<GridPoint> path)
        {
            var total = 0f;
            for (var i = 1; i < path.Count; i++)
            {
                total += Distance(path[i - 1], path[i]);
            }

            return total;
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
