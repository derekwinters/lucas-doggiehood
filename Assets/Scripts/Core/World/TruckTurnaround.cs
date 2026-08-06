using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The delivery truck's road-centerline turnaround (#599): what the truck
    /// does at its stop when the only reachable off-map opening is the one it
    /// entered by (a spur / cul-de-sac), so it must retrace its path back out.
    ///
    /// v1 is a simple in-place reorient — the truck pivots on the spot and drives
    /// back the way it came. The behavior is deliberately kept as tunable data:
    /// the maneuver geometry is a function of <see cref="InPlaceReorientRadius"/>,
    /// so a later version can swing the truck around an arc (e.g. following the
    /// cul-de-sac bulb loop) by raising that radius alone, without changing the
    /// routing in <see cref="DeliveryTruckRoute"/>. This is the truck's own
    /// turnaround on the road centerline — distinct from the sidewalk cul-de-sac
    /// bulb arc dogs follow (#581 <c>CulDeSacBulbRadius</c>).
    /// </summary>
    public static class TruckTurnaround
    {
        /// <summary>v1 turnaround radius: 0 = pivot in place. The single tunable
        /// that controls how the truck swings around; raise it to sweep an arc.</summary>
        public const float InPlaceReorientRadius = 0f;

        /// <summary>How many points make up a swept-arc turnaround (only used
        /// when the radius is positive).</summary>
        public const int ArcSegments = 6;

        private const float Epsilon = 0.0001f;

        /// <summary>True when <paramref name="radius"/> calls for a swept
        /// maneuver rather than a pivot-in-place reorient.</summary>
        public static bool RequiresManeuver(float radius)
        {
            return radius > Epsilon;
        }

        /// <summary>
        /// The turnaround waypoints at <paramref name="pivot"/> for a truck
        /// arriving on <paramref name="incomingHeading"/>. For the in-place
        /// radius this is just the pivot itself (the truck reverses without
        /// moving); for a positive radius it is a half-circle arc of
        /// <see cref="ArcSegments"/> points swung to the truck's side, ending
        /// back at the pivot pointed the opposite way.
        /// </summary>
        public static IReadOnlyList<GridPoint> Waypoints(GridPoint pivot, GridPoint incomingHeading, float radius)
        {
            if (!RequiresManeuver(radius))
            {
                return new[] { pivot };
            }

            // Unit heading; fall back to +Z if a zero heading is passed.
            var length = (float)Math.Sqrt((incomingHeading.X * incomingHeading.X)
                                          + (incomingHeading.Z * incomingHeading.Z));
            var hx = length > Epsilon ? incomingHeading.X / length : 0f;
            var hz = length > Epsilon ? incomingHeading.Z / length : 1f;

            // Arc centre sits one radius to the truck's left (perpendicular to
            // heading). Sweep 180 degrees so it exits antiparallel.
            var leftX = -hz;
            var leftZ = hx;
            var centerX = pivot.X + (leftX * radius);
            var centerZ = pivot.Z + (leftZ * radius);

            var points = new List<GridPoint>(ArcSegments + 1);
            for (var i = 0; i <= ArcSegments; i++)
            {
                var t = (float)i / ArcSegments;
                var angle = (float)Math.PI * t;
                // Rotate the pivot around the arc centre by -angle (turning right
                // out of a left-offset centre yields a clean U-turn).
                var offsetX = pivot.X - centerX;
                var offsetZ = pivot.Z - centerZ;
                var cos = (float)Math.Cos(-angle);
                var sin = (float)Math.Sin(-angle);
                var rotatedX = (offsetX * cos) - (offsetZ * sin);
                var rotatedZ = (offsetX * sin) + (offsetZ * cos);
                points.Add(new GridPoint(centerX + rotatedX, centerZ + rotatedZ));
            }

            return points;
        }
    }
}
