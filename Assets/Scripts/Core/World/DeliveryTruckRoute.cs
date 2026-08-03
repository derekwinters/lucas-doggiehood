using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The on-road route a delivery truck drives to a dog's front door
    /// (#538). Derived from the real <see cref="Road"/> geometry rather than
    /// a bee-line: the truck enters at a road end, drives along the road
    /// centerline to the point nearest the door, stops there to drop the
    /// package (the package is placed AT the door by the Unity layer; the
    /// truck itself does not leave the road), then continues along the same
    /// road to the far end and exits.
    ///
    /// Hard invariant (#538): the truck never leaves the roadway. Because
    /// <see cref="Entry"/>, <see cref="Stop"/>, and <see cref="Exit"/> all
    /// lie on a single road's centerline, every point the truck drives
    /// through is on that road's paved surface.
    /// </summary>
    public readonly struct DeliveryTruckRoute
    {
        /// <summary>Where the truck enters — a road end at the world edge.</summary>
        public GridPoint Entry { get; }

        /// <summary>Where the truck stops to deliver — the road point nearest
        /// the door. On the road, short of the waiting dog.</summary>
        public GridPoint Stop { get; }

        /// <summary>Where the truck exits — the opposite road end.</summary>
        public GridPoint Exit { get; }

        private DeliveryTruckRoute(GridPoint entry, GridPoint stop, GridPoint exit)
        {
            Entry = entry;
            Stop = stop;
            Exit = exit;
        }

        /// <summary>
        /// Computes the route to <paramref name="door"/> over the given
        /// <paramref name="roads"/>: picks the road whose centerline passes
        /// nearest the door, and returns entry/stop/exit all on that road.
        /// </summary>
        public static DeliveryTruckRoute ToDoor(IReadOnlyList<Road> roads, GridPoint door)
        {
            if (roads == null || roads.Count == 0)
            {
                throw new ArgumentException("At least one road is required to route a delivery.", nameof(roads));
            }

            Road bestRoad = null;
            var bestAlong = 0f;
            var bestDistanceSquared = float.MaxValue;

            foreach (var road in roads)
            {
                var along = AlongAxis(road, door);
                var clampedAlong = Clamp(along, -road.HalfLength, road.HalfLength);
                var stop = road.PointAt(clampedAlong, 0f);
                var distanceSquared = DistanceSquared(stop, door);

                // Strictly-less keeps the tie-break deterministic: the first
                // road in the list wins when two are equidistant.
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestRoad = road;
                    bestAlong = clampedAlong;
                }
            }

            var stopPoint = bestRoad.PointAt(bestAlong, 0f);

            // Enter from the road end on the stop's side (a natural short
            // approach), then continue to the opposite end and leave. When the
            // stop sits at the centre, default to entering from the positive
            // end so the route stays deterministic.
            var entrySign = bestAlong >= 0f ? 1f : -1f;
            var entry = bestRoad.PointAt(entrySign * bestRoad.HalfLength, 0f);
            var exit = bestRoad.PointAt(-entrySign * bestRoad.HalfLength, 0f);

            return new DeliveryTruckRoute(entry, stopPoint, exit);
        }

        private static float AlongAxis(Road road, GridPoint point)
        {
            return road.Orientation == StreetOrientation.NorthSouth
                ? point.Z - road.Center.Z
                : point.X - road.Center.X;
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
