using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// #673: one MANOEUVRE through an intersection — the set of crosswalk bands
    /// a vehicle's pass through that intersection will cross — held as a single
    /// all-or-nothing claim.
    ///
    /// This is the unit of right-of-way that <see cref="RoadCrossingGate"/>'s
    /// per-band claim is not. The gate's own doc used to reason that a per-band
    /// claim "generalizes for free to a vehicle that eventually turns across an
    /// intersection — each crosswalk span it drives over is one independent
    /// claim." Independence is exactly what is wrong: an intersection's incoming
    /// and outgoing bands are NOT independent, because a vehicle that takes the
    /// first without being sure of the second strands itself between them, in
    /// the middle of the box, holding nothing.
    ///
    /// <b>Invariant — a vehicle does not enter an intersection until the entire
    /// manoeuvre through it is clear.</b> <see cref="TryAcquire"/> takes every
    /// band or none: a partial acquire is rolled back before it returns, so a
    /// blocked vehicle waits outside the intersection holding nothing and a
    /// third occupant may still take the band it was denied. There is no
    /// hold-and-wait, and therefore no lock-ordering cycle.
    ///
    /// Bands are ATTEMPTED in <see cref="ClaimOrder"/> — one global order over
    /// band identity, independent of who is driving or which way — so two
    /// vehicles whose manoeuvres overlap always contend in the same sequence and
    /// one of them wins outright. A tie broken by timing would let the pair
    /// livelock, each rolling back what the other needs.
    ///
    /// <see cref="Bands"/> keeps ROUTE order instead, because the release is
    /// driven by the vehicle's tail clearing the manoeuvre's FINAL band (#658
    /// carried across a turn) — never by reaching a waypoint, which is the
    /// release that caused the bug.
    ///
    /// Not thread-safe: the simulation ticks on one thread.
    /// </summary>
    public sealed class RoadManoeuvre
    {
        private readonly WalkEdge[] bands;
        private readonly WalkEdge[] claimOrder;

        /// <summary>Builds a manoeuvre over <paramref name="routeOrderedBands"/>
        /// — the bands in the order the vehicle drives over them.</summary>
        public RoadManoeuvre(IReadOnlyList<WalkEdge> routeOrderedBands)
        {
            if (routeOrderedBands == null)
            {
                throw new ArgumentNullException(nameof(routeOrderedBands));
            }

            bands = new WalkEdge[routeOrderedBands.Count];
            for (var i = 0; i < routeOrderedBands.Count; i++)
            {
                bands[i] = routeOrderedBands[i];
            }

            claimOrder = (WalkEdge[])bands.Clone();
            Array.Sort(claimOrder, RoadCrossingGate.CompareClaimOrder);
        }

        /// <summary>The manoeuvre's bands in the order the vehicle crosses them.
        /// The LAST one is what the release is measured against.</summary>
        public IReadOnlyList<WalkEdge> Bands => bands;

        /// <summary>The same bands in the one global order every vehicle
        /// attempts them in — the deterministic tie-break that rules out
        /// livelock.</summary>
        public IReadOnlyList<WalkEdge> ClaimOrder => claimOrder;

        /// <summary>True once <see cref="TryAcquire"/> has taken the whole set
        /// and it has not been released yet.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>
        /// Claims EVERY band, or none. Returns true when the occupant now holds
        /// the whole manoeuvre (idempotent once held, so a re-check mid-drive
        /// never self-locks). Returns false when any band is held by a different
        /// occupant, having first released the bands this call had already
        /// taken — so a denied vehicle is left holding nothing and must wait
        /// outside the intersection.
        /// </summary>
        public bool TryAcquire(RoadCrossingGate gate, object occupant)
        {
            if (gate == null)
            {
                throw new ArgumentNullException(nameof(gate));
            }

            if (IsHeld)
            {
                return true;
            }

            var takenHere = new List<WalkEdge>(claimOrder.Length);
            foreach (var band in claimOrder)
            {
                // A band this occupant already held before the call is not this
                // call's to roll back — unwinding it would drop a claim the
                // vehicle is legitimately sitting on.
                if (gate.IsHeldBy(band, occupant))
                {
                    continue;
                }

                if (gate.TryEnter(band, occupant))
                {
                    takenHere.Add(band);
                    continue;
                }

                foreach (var claimed in takenHere)
                {
                    gate.Exit(claimed, occupant);
                }

                return false;
            }

            IsHeld = true;
            return true;
        }

        /// <summary>Hands every band of the manoeuvre back at once — the mirror
        /// of the all-or-nothing acquire, driven by the vehicle's tail clearing
        /// the final band.</summary>
        public void Release(RoadCrossingGate gate, object occupant)
        {
            if (gate == null)
            {
                throw new ArgumentNullException(nameof(gate));
            }

            foreach (var band in bands)
            {
                gate.Exit(band, occupant);
            }

            IsHeld = false;
        }

        /// <summary>
        /// The crosswalk bands lying on <paramref name="road"/>'s own centerline
        /// within its extent, each with its along-road coordinate. A band on a
        /// crossing road has its midpoint off to the side, so it is excluded.
        /// </summary>
        public static IReadOnlyList<RoadBand> BandsOn(Road road, WalkNetwork network)
        {
            if (road == null)
            {
                throw new ArgumentNullException(nameof(road));
            }

            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var found = new List<RoadBand>();
            foreach (var edge in network.Edges)
            {
                if (edge.Kind != WalkEdgeKind.Crosswalk)
                {
                    continue;
                }

                if (TryAlongOn(road, edge, out var along))
                {
                    found.Add(new RoadBand(along, edge));
                }
            }

            return found;
        }

        /// <summary>
        /// True when <paramref name="edge"/>'s midpoint sits on
        /// <paramref name="road"/>'s centerline within its extent, reporting
        /// where along the road it sits.
        /// </summary>
        public static bool TryAlongOn(Road road, WalkEdge edge, out float along)
        {
            along = 0f;
            if (road == null)
            {
                return false;
            }

            var midpoint = new GridPoint((edge.A.X + edge.B.X) / 2f, (edge.A.Z + edge.B.Z) / 2f);
            var perpendicular = road.Orientation == StreetOrientation.NorthSouth
                ? midpoint.X - road.Center.X
                : midpoint.Z - road.Center.Z;
            if (Math.Abs(perpendicular) > OnRoadEpsilon)
            {
                return false;
            }

            var candidate = road.AlongAxis(midpoint);
            if (Math.Abs(candidate) > road.HalfLength + OnRoadEpsilon)
            {
                return false;
            }

            along = candidate;
            return true;
        }

        /// <summary>
        /// Groups the bands of one road into per-intersection manoeuvres, in
        /// travel order: two bands on the same road belong to the same
        /// intersection exactly when they sit one
        /// <see cref="TileCrosswalkGeometry.BandSpacing"/> apart (one band
        /// either side of the same crossing). Used when a caller drives a single
        /// road with no route to consult — a route that TURNS crosses bands on
        /// two different roads, which only <see cref="RouteManoeuvres"/> can see.
        /// </summary>
        public static IReadOnlyList<RoadManoeuvre> GroupByIntersection(
            IReadOnlyList<RoadBand> bands, float travelSign)
        {
            var ordered = new List<RoadBand>(bands);
            ordered.Sort((a, b) => (a.Along * travelSign).CompareTo(b.Along * travelSign));

            var manoeuvres = new List<RoadManoeuvre>();
            var group = new List<WalkEdge>();
            var previousAlong = 0f;

            foreach (var band in ordered)
            {
                var joinsGroup = group.Count > 0
                    && Math.Abs(Math.Abs(band.Along - previousAlong) - TileCrosswalkGeometry.BandSpacing)
                       <= OnRoadEpsilon;
                if (!joinsGroup && group.Count > 0)
                {
                    manoeuvres.Add(new RoadManoeuvre(group));
                    group = new List<WalkEdge>();
                }

                group.Add(band.Edge);
                previousAlong = band.Along;
            }

            if (group.Count > 0)
            {
                manoeuvres.Add(new RoadManoeuvre(group));
            }

            return manoeuvres;
        }

        // Bands are placed on exact centerlines, so this only absorbs float
        // representation error (#161: named rather than a bare literal).
        private const float OnRoadEpsilon = 0.01f;
    }

    /// <summary>One crosswalk band located on a particular road: where it sits
    /// in that road's own along-coordinate, and the walk edge that identifies
    /// it to <see cref="RoadCrossingGate"/>.</summary>
    public readonly struct RoadBand
    {
        public float Along { get; }

        public WalkEdge Edge { get; }

        public RoadBand(float along, WalkEdge edge)
        {
            Along = along;
            Edge = edge;
        }
    }
}
