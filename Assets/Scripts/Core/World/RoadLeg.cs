using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// #672: one leg of a driven route — a straight run along a single
    /// <see cref="Road"/>, from one waypoint to the next — together with the
    /// direction it is driven and the lane that direction implies.
    ///
    /// A <see cref="DeliveryTruckRoute"/>'s waypoints are road CENTERLINE points,
    /// and they stay that way: an intersection waypoint belongs to two roads with
    /// two different right-hand sides, so a lane offset baked into the graph would
    /// be ambiguous exactly where routes turn. The lane is instead a property of
    /// the leg being driven, resolved here and applied by
    /// <see cref="PointAt"/> — so the road network keeps one unambiguous
    /// description of itself and the driving line is derived per leg.
    ///
    /// This resolution used to live privately inside the Unity delivery-truck view
    /// (which road does this leg lie on, and which way along it?). It is Core
    /// logic, not rendering, so it lives here where it can be tested without the
    /// engine — and where the intersection-manoeuvre work (#673) has a single
    /// place to reason about a leg.
    ///
    /// <b>Invariant — a vehicle keeps to the right-hand lane on a road leg.</b>
    /// Every point <see cref="PointAt"/> returns sits <see cref="RoadLane.Offset"/>
    /// to the right of the centerline for <see cref="TravelSign"/>, so the
    /// vehicle's lateral offset never changes sign and never reaches zero while
    /// the leg is driven. Intersection interiors are out of scope: a left turn
    /// inherently crosses the oncoming lane (#673).
    /// </summary>
    public readonly struct RoadLeg
    {
        // A route leg is axis-aligned on a road centerline; this tolerance decides
        // which axis (north-south vs east-west) a leg runs along when matching it
        // to the road it lies on (#161: no bare geometry literals in method
        // bodies).
        private const float AxisEpsilon = 0.01f;

        /// <summary>The road this leg runs along.</summary>
        public Road Road { get; }

        /// <summary>+1 when the leg is driven toward increasing along-road
        /// coordinates, -1 toward decreasing — the same sign
        /// <see cref="RoadCrossingTraversal"/> and <see cref="CarFollowing"/>
        /// read.</summary>
        public float TravelSign { get; }

        /// <summary>Where the leg starts, in the road's own along-coordinate.</summary>
        public float EntryAlong { get; }

        /// <summary>Where the leg ends, in the road's own along-coordinate.</summary>
        public float ExitAlong { get; }

        private RoadLeg(Road road, float travelSign, float entryAlong, float exitAlong)
        {
            Road = road;
            TravelSign = travelSign;
            EntryAlong = entryAlong;
            ExitAlong = exitAlong;
        }

        /// <summary>The signed perpendicular offset from the road centerline to
        /// this leg's lane centre (<see cref="RoadLane"/>).</summary>
        public float LaneOffset => RoadLane.PerpendicularOffsetFor(Road.Orientation, TravelSign);

        /// <summary>The world point a vehicle drives through at
        /// <paramref name="alongAxis"/> on this leg: the centre of its right-hand
        /// lane, not the centerline.</summary>
        public GridPoint PointAt(float alongAxis)
        {
            return Road.PointAt(alongAxis, LaneOffset);
        }

        /// <summary>
        /// Resolves the leg running from <paramref name="from"/> to
        /// <paramref name="to"/>: the road whose paved surface contains both
        /// endpoints and whose orientation matches the leg's axis. Returns false
        /// when the leg lies along no road — e.g. a turnaround manoeuvre's pivot,
        /// or a zero-length hop — in which case the caller has no lane to keep and
        /// drives the raw waypoints.
        /// </summary>
        public static bool TryResolve(
            IReadOnlyList<Road> roads, GridPoint from, GridPoint to, out RoadLeg leg)
        {
            leg = default;
            if (roads == null)
            {
                return false;
            }

            var runsNorthSouth = Abs(from.X - to.X) < AxisEpsilon;
            var runsEastWest = Abs(from.Z - to.Z) < AxisEpsilon;

            foreach (var road in roads)
            {
                if (!road.Contains(from) || !road.Contains(to))
                {
                    continue;
                }

                var matchesAxis =
                    (runsNorthSouth && road.Orientation == StreetOrientation.NorthSouth)
                    || (runsEastWest && road.Orientation == StreetOrientation.EastWest);
                if (!matchesAxis)
                {
                    continue;
                }

                var entryAlong = road.AlongAxis(from);
                var exitAlong = road.AlongAxis(to);
                leg = new RoadLeg(road, exitAlong - entryAlong < 0f ? -1f : 1f, entryAlong, exitAlong);
                return true;
            }

            return false;
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}
