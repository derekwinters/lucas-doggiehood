using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The vehicle side of road right-of-way (#546): as a vehicle drives
    /// monotonically along one <see cref="Road"/> from an entry along-coordinate
    /// to an exit along-coordinate, it claims each crosswalk it reaches through a
    /// shared <see cref="RoadCrossingGate"/> (so a dog that arrives second must
    /// wait) and pauses at the near edge of any crosswalk a dog already holds,
    /// resuming once that dog clears it. Everything is expressed in along-road
    /// coordinates (<see cref="Road.AlongAxis"/>), so the Unity delivery-truck
    /// view only converts positions to/from along and drives — no decision logic
    /// leaks into the engine layer.
    ///
    /// A truck's whole entry -> stop -> exit route lies on one road's centerline
    /// and is monotonic in along, so crosswalks are met in a single fixed order;
    /// this type does not attempt to handle a mid-route reversal.
    /// </summary>
    public sealed class RoadCrossingTraversal
    {
        /// <summary>Half the along-road thickness of a crosswalk stripe the
        /// vehicle must stop short of — derived from the locked crosswalk width
        /// (#161: no bare geometry literals).</summary>
        public const float HalfCrosswalkAlong = WorldDimensions.CrosswalkWidth / 2f;

        private const float Epsilon = 0.0001f;

        private readonly RoadCrossingGate gate;
        private readonly object occupant;
        private readonly float travelSign;
        private readonly Crossing[] crossings;
        private readonly bool[] held;

        public RoadCrossingTraversal(
            RoadCrossingGate gate, object occupant, Road road, WalkNetwork network,
            float entryAlong, float exitAlong)
        {
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.occupant = occupant ?? throw new ArgumentNullException(nameof(occupant));
            if (road == null)
            {
                throw new ArgumentNullException(nameof(road));
            }

            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var direction = exitAlong - entryAlong;
            travelSign = direction < 0f ? -1f : 1f;

            var found = new List<Crossing>();
            foreach (var edge in network.Edges)
            {
                if (edge.Kind != WalkEdgeKind.Crosswalk)
                {
                    continue;
                }

                var midX = (edge.A.X + edge.B.X) / 2f;
                var midZ = (edge.A.Z + edge.B.Z) / 2f;
                var midpoint = new GridPoint(midX, midZ);

                // The crosswalk belongs to THIS road only if its midpoint sits on
                // the road's own centerline (zero perpendicular offset) within
                // the road's extent — this excludes crosswalks on the crossing
                // road, whose midpoints sit off to the side.
                var perpendicular = road.Orientation == StreetOrientation.NorthSouth
                    ? midX - road.Center.X
                    : midZ - road.Center.Z;
                if (Math.Abs(perpendicular) > Epsilon)
                {
                    continue;
                }

                var along = road.AlongAxis(midpoint);
                if (Math.Abs(along) > road.HalfLength + Epsilon)
                {
                    continue;
                }

                found.Add(new Crossing(along, edge));
            }

            // Order the crosswalks in the direction of travel, so the vehicle
            // meets them front to back.
            found.Sort((a, b) => (a.Along * travelSign).CompareTo(b.Along * travelSign));
            crossings = found.ToArray();
            held = new bool[crossings.Length];
        }

        /// <summary>
        /// Given the vehicle's current along-coordinate and the along-coordinate
        /// it intends to reach this tick, returns the along-coordinate it may
        /// actually advance to: the full target when the way is clear, or the
        /// near edge of the next crosswalk it may not enter (one a dog holds, or
        /// one it has not yet reached the boundary of to claim). Claims free
        /// crosswalks it reaches and releases crosswalks it has fully passed as a
        /// side effect.
        /// </summary>
        public float Advance(float currentAlong, float targetAlong)
        {
            ReleasePassed(currentAlong);

            var allowed = targetAlong;
            for (var i = 0; i < crossings.Length; i++)
            {
                if (held[i])
                {
                    // Already claimed — the vehicle may drive through it.
                    continue;
                }

                var along = crossings[i].Along;
                if (!IsAhead(currentAlong, along))
                {
                    continue;
                }

                var nearEdge = along - travelSign * HalfCrosswalkAlong;
                if (HasReached(currentAlong, nearEdge))
                {
                    if (gate.TryEnter(crossings[i].Edge, occupant))
                    {
                        held[i] = true;
                        continue;
                    }

                    // A dog holds it: pause at the near edge.
                    allowed = ClampAhead(allowed, nearEdge);
                    break;
                }

                // Not yet at the boundary: drive up to it, but no further, until
                // the claim can be resolved there.
                allowed = ClampAhead(allowed, nearEdge);
                break;
            }

            return allowed;
        }

        /// <summary>Releases every crosswalk this vehicle still holds — used when
        /// its view is torn down mid-route so the claim can't strand a dog.</summary>
        public void ReleaseAll()
        {
            for (var i = 0; i < crossings.Length; i++)
            {
                if (held[i])
                {
                    gate.Exit(crossings[i].Edge, occupant);
                    held[i] = false;
                }
            }
        }

        private void ReleasePassed(float currentAlong)
        {
            for (var i = 0; i < crossings.Length; i++)
            {
                if (!held[i])
                {
                    continue;
                }

                var farEdge = crossings[i].Along + travelSign * HalfCrosswalkAlong;
                if ((farEdge - currentAlong) * travelSign <= Epsilon)
                {
                    gate.Exit(crossings[i].Edge, occupant);
                    held[i] = false;
                }
            }
        }

        private bool IsAhead(float currentAlong, float along)
        {
            return (along - currentAlong) * travelSign > Epsilon;
        }

        private bool HasReached(float currentAlong, float boundary)
        {
            return (boundary - currentAlong) * travelSign <= Epsilon;
        }

        private float ClampAhead(float value, float cap)
        {
            return travelSign > 0f ? Math.Min(value, cap) : Math.Max(value, cap);
        }

        private readonly struct Crossing
        {
            public readonly float Along;
            public readonly WalkEdge Edge;

            public Crossing(float along, WalkEdge edge)
            {
                Along = along;
                Edge = edge;
            }
        }
    }
}
