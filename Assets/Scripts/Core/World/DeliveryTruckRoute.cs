using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The on-road route a delivery truck drives to a dog's front door over the
    /// LIVE multi-tile map (#599, superseding the single-origin-road #538 form).
    /// Computed once per delivery against the current <see cref="TileMap"/>:
    ///
    /// <list type="number">
    /// <item>the truck enters off-map at the road opening nearest the door
    /// (<see cref="RoadOpenings"/>), then</item>
    /// <item>routes IN over the live road network (<see cref="RoadNetworkGraph"/>
    /// on <see cref="MapWalkNetwork.RoadsFrom"/>) to <see cref="Stop"/> — the road
    /// point nearest the door, where the package is dropped, and</item>
    /// <item>routes OUT to a DIFFERENT opening when one is reachable, otherwise
    /// turns around in place and retraces the way it came (a spur / cul-de-sac).</item>
    /// </list>
    ///
    /// Hard invariant (#538, preserved): the truck never leaves the roadway.
    /// Every waypoint in <see cref="Inbound"/> and <see cref="Outbound"/> is on a
    /// live road centerline, and the graph only ever connects adjacent centerline
    /// nodes, so every point driven between them is paved too.
    ///
    /// This type is the road-routing seam #600 (concurrent trucks with
    /// car-following) extends — it consumes the produced waypoint path and adds
    /// spacing/queuing without changing how the path is found.
    /// </summary>
    public readonly struct DeliveryTruckRoute
    {
        /// <summary>The inbound waypoints: entry opening → … → <see cref="Stop"/>.</summary>
        public IReadOnlyList<GridPoint> Inbound { get; }

        /// <summary>The outbound waypoints: <see cref="Stop"/> → … → exit opening
        /// (or the reverse of <see cref="Inbound"/> on a retrace).</summary>
        public IReadOnlyList<GridPoint> Outbound { get; }

        /// <summary>True when the truck had to turn around and retrace because the
        /// entry was the only reachable opening (spur / cul-de-sac).</summary>
        public bool IsTurnaround { get; }

        /// <summary>Where the truck enters — the off-map opening nearest the door.</summary>
        public GridPoint Entry => Inbound[0];

        /// <summary>Where the truck stops to deliver — the road point nearest the door.</summary>
        public GridPoint Stop => Inbound[Inbound.Count - 1];

        /// <summary>Where the truck exits — a different opening, or the entry on a retrace.</summary>
        public GridPoint Exit => Outbound[Outbound.Count - 1];

        private DeliveryTruckRoute(IReadOnlyList<GridPoint> inbound, IReadOnlyList<GridPoint> outbound, bool isTurnaround)
        {
            Inbound = inbound;
            Outbound = outbound;
            IsTurnaround = isTurnaround;
        }

        /// <summary>
        /// Computes the route to <paramref name="door"/> over the live
        /// <paramref name="map"/>. Throws when the map carries no off-map road
        /// opening at all (nothing to enter through).
        /// </summary>
        public static DeliveryTruckRoute ToDoor(TileMap map, GridPoint door)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var openings = RoadOpenings.Detect(map);
            if (openings.Count == 0)
            {
                throw new InvalidOperationException(
                    "The live map has no off-map road opening for a delivery truck to enter through.");
            }

            var roads = MapWalkNetwork.RoadsFrom(map);
            var graph = new RoadNetworkGraph(roads);
            var stop = NearestRoadPoint(roads, door);

            var entry = RoadOpenings.Nearest(openings, door);
            var inbound = graph.ShortestPath(entry.Point, stop);

            if (TrySelectExit(graph, openings, entry, stop, door, out var exit))
            {
                var outbound = graph.ShortestPath(stop, exit.Point);
                return new DeliveryTruckRoute(inbound, outbound, false);
            }

            // Spur / cul-de-sac: the entry is the only reachable opening. Turn
            // around in place (v1) and retrace. The maneuver is produced through
            // the tunable turnaround seam so a later swept-arc radius drops in
            // without changing this routing.
            var incomingHeading = IncomingHeading(inbound);
            var maneuver = TruckTurnaround.Waypoints(stop, incomingHeading, TruckTurnaround.InPlaceReorientRadius);
            var retrace = BuildRetrace(maneuver, inbound);
            return new DeliveryTruckRoute(inbound, retrace, true);
        }

        /// <summary>
        /// Picks the exit opening: the opening nearest the door that is NOT the
        /// entry and is reachable from the stop. Returns false when the only
        /// reachable opening is the entry.
        /// </summary>
        private static bool TrySelectExit(
            RoadNetworkGraph graph, IReadOnlyList<RoadOpening> openings,
            RoadOpening entry, GridPoint stop, GridPoint door, out RoadOpening exit)
        {
            exit = default;
            var found = false;
            var bestDistance = float.MaxValue;

            foreach (var opening in openings)
            {
                if (opening.Equals(entry))
                {
                    continue;
                }

                if (!graph.TryShortestPath(stop, opening.Point, out _))
                {
                    continue;
                }

                var distance = DistanceSquared(opening.Point, door);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    exit = opening;
                }
            }

            return found;
        }

        private static GridPoint NearestRoadPoint(IReadOnlyList<Road> roads, GridPoint door)
        {
            var best = default(GridPoint);
            var bestDistance = float.MaxValue;

            foreach (var road in roads)
            {
                var along = road.Orientation == StreetOrientation.NorthSouth
                    ? door.Z - road.Center.Z
                    : door.X - road.Center.X;
                var clamped = Clamp(along, -road.HalfLength, road.HalfLength);
                var point = road.PointAt(clamped, 0f);
                var distance = DistanceSquared(point, door);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        private static GridPoint IncomingHeading(IReadOnlyList<GridPoint> inbound)
        {
            if (inbound.Count < 2)
            {
                return new GridPoint(0f, 1f);
            }

            var last = inbound[inbound.Count - 1];
            var previous = inbound[inbound.Count - 2];
            return new GridPoint(last.X - previous.X, last.Z - previous.Z);
        }

        /// <summary>
        /// The outbound path for a retrace: the turnaround maneuver at the stop,
        /// then the inbound reversed back to the entry. For the in-place radius
        /// the maneuver is just the stop itself, so this is simply the reversed
        /// inbound; a positive radius splices the swept arc in ahead of it.
        /// </summary>
        private static IReadOnlyList<GridPoint> BuildRetrace(
            IReadOnlyList<GridPoint> maneuver, IReadOnlyList<GridPoint> inbound)
        {
            var retrace = new List<GridPoint>(maneuver.Count + inbound.Count);
            retrace.AddRange(maneuver);

            // The reversed inbound starts at the stop, which the maneuver already
            // ends at — skip that duplicate.
            for (var i = inbound.Count - 2; i >= 0; i--)
            {
                retrace.Add(inbound[i]);
            }

            return retrace;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static float DistanceSquared(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (dx * dx) + (dz * dz);
        }
    }
}
