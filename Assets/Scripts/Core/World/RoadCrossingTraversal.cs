using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The vehicle side of road right-of-way (#546): as a vehicle drives
    /// monotonically along one <see cref="Road"/> from an entry along-coordinate
    /// to an exit along-coordinate, it claims each crosswalk it reaches through a
    /// shared <see cref="RoadCrossingGate"/> (so a dog that arrives second must
    /// wait) and pauses short of any crosswalk a dog already holds, resuming
    /// once that dog clears it. The pause is measured at the occupant's LEADING
    /// EDGE, not its pivot: a caller with a body length passes its own
    /// pivot-to-front-bumper setback so the whole footprint stays clear of the
    /// band (#639), while a point occupant passes nothing and stops at the near
    /// edge exactly as before. Everything is expressed in along-road
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
        private readonly float frontSetback;
        private readonly Crossing[] crossings;
        private readonly bool[] held;

        public RoadCrossingTraversal(
            RoadCrossingGate gate, object occupant, Road road, WalkNetwork network,
            float entryAlong, float exitAlong, float frontSetback = 0f)
        {
            // #639: frontSetback is the occupant's own pivot-to-leading-edge
            // distance (plus whatever stop gap it wants). It stays a caller-
            // supplied number so this type remains occupant-agnostic (#546): a
            // point occupant passes nothing and gets exactly the old behaviour.
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.occupant = occupant ?? throw new ArgumentNullException(nameof(occupant));
            this.frontSetback = frontSetback;
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

                var boundary = StopBoundary(along);
                if (HasReached(currentAlong, boundary))
                {
                    if (gate.TryEnter(crossings[i].Edge, occupant))
                    {
                        held[i] = true;
                        continue;
                    }

                    // A dog holds it: pause with the leading edge at the stripe.
                    allowed = ClampAhead(allowed, boundary);
                    break;
                }

                // Not yet at the boundary: drive up to it, but no further, until
                // the claim can be resolved there.
                allowed = ClampAhead(allowed, boundary);
                break;
            }

            // #639: with a non-zero setback the stop boundary can sit BEHIND an
            // occupant that began its leg already inside the setback zone (a leg
            // starting at an intersection waypoint, say). Holding position is
            // right there; reversing out of the zone is not.
            return NoFurtherBackThan(allowed, currentAlong);
        }

        /// <summary>
        /// The along-coordinate at which an occupant must stop for the crosswalk
        /// centred on <paramref name="along"/>: the stripe's own near edge pushed
        /// back by the occupant's front setback, so it is the occupant's LEADING
        /// EDGE — not its pivot — that comes to rest at the edge of the band
        /// (#639). With the default zero setback this is the near edge itself.
        /// </summary>
        private float StopBoundary(float along)
        {
            return along - travelSign * (HalfCrosswalkAlong + frontSetback);
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

        /// <summary>#639: never hand back an along-coordinate behind where the
        /// occupant already is — a clamp may only slow it down or hold it.</summary>
        private float NoFurtherBackThan(float value, float currentAlong)
        {
            return travelSign > 0f ? Math.Max(value, currentAlong) : Math.Min(value, currentAlong);
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
