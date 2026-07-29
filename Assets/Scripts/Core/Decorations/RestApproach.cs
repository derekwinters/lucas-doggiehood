using System;
using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// An in-progress "walk over and settle onto the comfort item" motion
    /// (#52, #112): the ordered waypoints a dog follows from its current
    /// position, over the #106/#128 walk network, to a comfort
    /// <see cref="Decoration"/>'s yard spot, plus the dog's live position as
    /// it advances. Replaces the old instant flip into the Rest pose — the
    /// dog only enters Rest once <see cref="HasArrived"/>. Engine-free: the
    /// Unity layer applies <see cref="Position"/> to the transform each frame
    /// and commits the Rest flip on arrival.
    /// </summary>
    public sealed class RestApproach
    {
        /// <summary>Ground speed of the walk-over, meters/second (#161) —
        /// the same medium pace a dog uses walking home.</summary>
        public const float ApproachSpeed = 1.6f;

        /// <summary>How close (meters) counts as being "on" a waypoint, or
        /// already standing on the decoration (#161).</summary>
        public const float ArriveDistance = 0.05f;

        private readonly List<GridPoint> waypoints;
        private int index;

        /// <summary>The comfort decoration the dog is heading for.</summary>
        public Decoration Target { get; }

        /// <summary>The dog's current position along the route.</summary>
        public GridPoint Position { get; private set; }

        /// <summary>The remaining ordered waypoints of the route, ending at
        /// the decoration's yard spot. Empty when the dog was already there.</summary>
        public IReadOnlyList<GridPoint> Waypoints
        {
            get { return waypoints; }
        }

        /// <summary>True once every waypoint has been consumed — the dog is on
        /// the decoration and may enter the Rest pose.</summary>
        public bool HasArrived
        {
            get { return index >= waypoints.Count; }
        }

        private RestApproach(GridPoint start, List<GridPoint> waypoints, Decoration target)
        {
            Position = start;
            this.waypoints = waypoints;
            Target = target;
        }

        /// <summary>
        /// Computes the walk-over route from <paramref name="dogPosition"/> to
        /// <paramref name="decoration"/>'s yard spot: the shortest path over
        /// the walk network toward the house's front door
        /// (<see cref="WalkNetwork.FindPath"/>), then a final off-network leg
        /// onto the actual yard position. A dog already standing on the
        /// decoration gets an empty route and arrives immediately.
        /// </summary>
        public static RestApproach Begin(GridPoint dogPosition, Decoration decoration, WalkNetwork network)
        {
            if (decoration == null)
            {
                throw new ArgumentNullException(nameof(decoration));
            }

            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var target = decoration.YardPosition;
            var route = new List<GridPoint>();

            if (Distance(dogPosition, target) > ArriveDistance)
            {
                foreach (var node in network.FindPath(dogPosition, target))
                {
                    // Skip a leading node the dog already stands on.
                    if (Distance(dogPosition, node) > ArriveDistance)
                    {
                        route.Add(node);
                    }
                }

                // Final off-network leg onto the actual yard spot (the network
                // path ends at the house's front-door node, not inside the yard).
                if (route.Count == 0 || Distance(route[route.Count - 1], target) > ArriveDistance)
                {
                    route.Add(target);
                }
            }

            return new RestApproach(dogPosition, route, decoration);
        }

        /// <summary>Walks up to <paramref name="distance"/> meters further
        /// along the route, snapping onto and consuming each waypoint reached.
        /// A no-op once <see cref="HasArrived"/>.</summary>
        public void Advance(float distance)
        {
            while (distance > 0f && index < waypoints.Count)
            {
                var next = waypoints[index];
                var remaining = Distance(Position, next);

                if (remaining <= distance)
                {
                    Position = next;
                    distance -= remaining;
                    index++;
                }
                else
                {
                    Position = MoveToward(Position, next, distance);
                    distance = 0f;
                }
            }
        }

        private static GridPoint MoveToward(GridPoint from, GridPoint to, float distance)
        {
            var dx = to.X - from.X;
            var dz = to.Z - from.Z;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);

            if (length <= distance || length < Epsilon)
            {
                return to;
            }

            var t = distance / length;
            return new GridPoint(from.X + dx * t, from.Z + dz * t);
        }

        private const float Epsilon = 1e-6f;

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
