using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// #673: the <see cref="RoadManoeuvre"/>s a driven route contains — one per
    /// intersection the route passes through, resolved once for the whole route
    /// rather than rediscovered a leg at a time.
    ///
    /// A route's waypoints are road-centerline junctions
    /// (<see cref="RoadNetworkGraph"/>), so a vehicle crossing an intersection
    /// always does it as TWO legs meeting at the intersection's centre — whether
    /// it turns or goes straight. That is why per-leg right-of-way could not see
    /// a whole crossing: the incoming band belongs to the leg before the
    /// waypoint and the outgoing band to the leg after it. Resolving here, at the
    /// waypoint, puts both in one manoeuvre, with no predicate anywhere asking
    /// "is this a turn?" — a straight run through a four-way is the same two-band
    /// manoeuvre a turn is (Derek, 2026-08-07: "if the vehicle can't cross the
    /// intersection cleanly, it stops before entering").
    ///
    /// Because the manoeuvre objects are shared by both legs, a claim taken on
    /// the approach is still held after the turn, and the release happens where
    /// it belongs — when the vehicle's tail clears the manoeuvre's final band —
    /// instead of at the waypoint hand-off.
    /// </summary>
    public sealed class RouteManoeuvres
    {
        /// <summary>Empty stand-in for a route with no resolvable manoeuvres —
        /// so callers never have to null-check.</summary>
        public static RouteManoeuvres None { get; } =
            new RouteManoeuvres(new RoadManoeuvre[0], new List<RoadManoeuvre>[0]);

        // Waypoints sit on exact centerlines and bands at exact offsets from
        // them, so this only absorbs float representation error (#161).
        private const float OnRoadEpsilon = 0.01f;

        private readonly RoadManoeuvre[] atWaypoint;
        private readonly List<RoadManoeuvre>[] looseBandsOnLeg;

        private RouteManoeuvres(RoadManoeuvre[] atWaypoint, List<RoadManoeuvre>[] looseBandsOnLeg)
        {
            this.atWaypoint = atWaypoint;
            this.looseBandsOnLeg = looseBandsOnLeg;
        }

        /// <summary>Every non-empty manoeuvre on the route, in route order.</summary>
        public IReadOnlyList<RoadManoeuvre> All
        {
            get
            {
                var all = new List<RoadManoeuvre>();
                foreach (var manoeuvre in atWaypoint)
                {
                    if (manoeuvre != null)
                    {
                        all.Add(manoeuvre);
                    }
                }

                foreach (var leg in looseBandsOnLeg)
                {
                    if (leg != null)
                    {
                        all.AddRange(leg);
                    }
                }

                return all;
            }
        }

        /// <summary>
        /// Resolves the manoeuvre at every waypoint of <paramref name="waypoints"/>
        /// that is a real intersection — a point where a north-south and an
        /// east-west road cross. A waypoint that is merely a road opening, a
        /// delivery stop, or a turnaround pivot carries no manoeuvre.
        /// </summary>
        public static RouteManoeuvres Resolve(
            IReadOnlyList<Road> roads, WalkNetwork network, IReadOnlyList<GridPoint> waypoints)
        {
            if (roads == null || network == null || waypoints == null || waypoints.Count < 2)
            {
                return None;
            }

            var resolved = new RoadManoeuvre[waypoints.Count];
            for (var i = 0; i < waypoints.Count; i++)
            {
                var junction = waypoints[i];
                if (!IsIntersection(roads, junction))
                {
                    continue;
                }

                var bands = new List<WalkEdge>();
                if (i > 0
                    && RoadLeg.TryResolve(roads, waypoints[i - 1], junction, out var incoming)
                    && TryBandNearJunction(network, incoming, junction, out var incomingBand))
                {
                    bands.Add(incomingBand);
                }

                if (i + 1 < waypoints.Count
                    && RoadLeg.TryResolve(roads, junction, waypoints[i + 1], out var outgoing)
                    && TryBandNearJunction(network, outgoing, junction, out var outgoingBand))
                {
                    bands.Add(outgoingBand);
                }

                if (bands.Count > 0)
                {
                    // A route can pass the same intersection twice (a cul-de-sac
                    // retrace). Each pass gets its OWN manoeuvre, because the two
                    // passes cross the bands in opposite order and it is the LAST
                    // band that decides where the set is released — sharing one
                    // object would release the second pass at the band it entered
                    // by. They are never live at the same time (a leg only sees
                    // the manoeuvres at its own two endpoints, and those are
                    // different intersections), so the duplicate is harmless.
                    resolved[i] = new RoadManoeuvre(bands);
                }
            }

            return new RouteManoeuvres(resolved, LooseBands(roads, network, waypoints, resolved));
        }

        /// <summary>
        /// Safety net: any band a leg drives over that no intersection manoeuvre
        /// covers gets a one-band manoeuvre of its own, so it is still claimed
        /// first-come exactly as it was before #673. A routed path splits at every
        /// crossing, so this should stay empty in practice — but "should" is how
        /// a band silently stops being claimed at all, which is worse than the
        /// bug this issue fixes.
        /// </summary>
        private static List<RoadManoeuvre>[] LooseBands(
            IReadOnlyList<Road> roads, WalkNetwork network,
            IReadOnlyList<GridPoint> waypoints, RoadManoeuvre[] atWaypoint)
        {
            var onLeg = new List<RoadManoeuvre>[waypoints.Count];
            for (var target = 1; target < waypoints.Count; target++)
            {
                if (!RoadLeg.TryResolve(roads, waypoints[target - 1], waypoints[target], out var leg))
                {
                    continue;
                }

                var low = Math.Min(leg.EntryAlong, leg.ExitAlong);
                var high = Math.Max(leg.EntryAlong, leg.ExitAlong);
                foreach (var band in RoadManoeuvre.BandsOn(leg.Road, network))
                {
                    if (band.Along < low - OnRoadEpsilon || band.Along > high + OnRoadEpsilon)
                    {
                        continue;
                    }

                    if (CoveredBy(atWaypoint[target - 1], band.Edge)
                        || CoveredBy(atWaypoint[target], band.Edge))
                    {
                        continue;
                    }

                    if (onLeg[target] == null)
                    {
                        onLeg[target] = new List<RoadManoeuvre>();
                    }

                    onLeg[target].Add(new RoadManoeuvre(new[] { band.Edge }));
                }
            }

            return onLeg;
        }

        private static bool CoveredBy(RoadManoeuvre manoeuvre, WalkEdge band)
        {
            if (manoeuvre == null)
            {
                return false;
            }

            foreach (var member in manoeuvre.Bands)
            {
                if (member.A.Equals(band.A) && member.B.Equals(band.B))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The manoeuvres a leg ending at <paramref name="targetIndex"/> has to
        /// reason about: the intersection it is driving INTO (so it can acquire
        /// the whole crossing before entering it), and the one it has just come
        /// out of (so it can release that set once its tail is clear).
        /// </summary>
        public IReadOnlyList<RoadManoeuvre> ForLeg(int targetIndex)
        {
            var relevant = new List<RoadManoeuvre>(2);
            AddIfPresent(relevant, targetIndex - 1);
            AddIfPresent(relevant, targetIndex);

            if (targetIndex >= 0 && targetIndex < looseBandsOnLeg.Length
                && looseBandsOnLeg[targetIndex] != null)
            {
                relevant.AddRange(looseBandsOnLeg[targetIndex]);
            }

            return relevant;
        }

        /// <summary>Drops every claim the route still holds — used when a
        /// vehicle is torn down mid-route so its claims can't strand a dog.</summary>
        public void ReleaseAll(RoadCrossingGate gate, object occupant)
        {
            foreach (var manoeuvre in All)
            {
                manoeuvre.Release(gate, occupant);
            }
        }

        private void AddIfPresent(List<RoadManoeuvre> into, int index)
        {
            if (index < 0 || index >= atWaypoint.Length)
            {
                return;
            }

            var manoeuvre = atWaypoint[index];
            if (manoeuvre != null && !into.Contains(manoeuvre))
            {
                into.Add(manoeuvre);
            }
        }

        /// <summary>
        /// The band <paramref name="leg"/> crosses at
        /// <paramref name="junction"/>: the one on the leg's own road, between
        /// the leg's ends, sitting a <see cref="TileCrosswalkGeometry.CrosswalkOffset"/>
        /// from the junction. A Tee's closed arm has none, and a leg that starts
        /// or stops short of the band has none either.
        /// </summary>
        private static bool TryBandNearJunction(
            WalkNetwork network, RoadLeg leg, GridPoint junction, out WalkEdge band)
        {
            band = default;
            var junctionAlong = leg.Road.AlongAxis(junction);
            var low = Math.Min(leg.EntryAlong, leg.ExitAlong);
            var high = Math.Max(leg.EntryAlong, leg.ExitAlong);

            foreach (var candidate in RoadManoeuvre.BandsOn(leg.Road, network))
            {
                if (candidate.Along < low - OnRoadEpsilon || candidate.Along > high + OnRoadEpsilon)
                {
                    continue;
                }

                if (Math.Abs(Math.Abs(candidate.Along - junctionAlong)
                             - TileCrosswalkGeometry.CrosswalkOffset) > OnRoadEpsilon)
                {
                    continue;
                }

                band = candidate.Edge;
                return true;
            }

            return false;
        }

        /// <summary>True when a north-south and an east-west road both run
        /// through <paramref name="point"/> — i.e. it is an intersection centre
        /// rather than an opening, a stop, or a plain bend.</summary>
        private static bool IsIntersection(IReadOnlyList<Road> roads, GridPoint point)
        {
            var northSouth = false;
            var eastWest = false;
            foreach (var road in roads)
            {
                var perpendicular = road.Orientation == StreetOrientation.NorthSouth
                    ? point.X - road.Center.X
                    : point.Z - road.Center.Z;
                if (Math.Abs(perpendicular) > OnRoadEpsilon)
                {
                    continue;
                }

                if (Math.Abs(road.AlongAxis(point)) > road.HalfLength + OnRoadEpsilon)
                {
                    continue;
                }

                if (road.Orientation == StreetOrientation.NorthSouth)
                {
                    northSouth = true;
                }
                else
                {
                    eastWest = true;
                }
            }

            return northSouth && eastWest;
        }
    }
}
