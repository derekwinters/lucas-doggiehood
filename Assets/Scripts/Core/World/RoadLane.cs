namespace Doggiehood.Core.World
{
    /// <summary>
    /// #672: the road model's lane rule — a road carries two lanes, and a vehicle
    /// keeps to the RIGHT-hand one for its own direction of travel.
    ///
    /// Before this the road model had no lane concept at all: every drivable road
    /// position was the centerline (<see cref="Road.PointAt"/> with a zero
    /// perpendicular offset), so the delivery truck straddled the middle of the
    /// street the whole way down and left no half of the road clear for oncoming
    /// traffic. The rule lives here, on the ROAD model, rather than on the truck
    /// so any future road user inherits it instead of re-deriving it. (Dogs are
    /// not lane users — they walk sidewalks and crosswalks, never the roadway;
    /// see docs/specs/dogs/behavior.md.)
    ///
    /// <b>Invariant — a vehicle stays in the right-hand half of the roadway on a
    /// road leg.</b> Its lateral offset from the centerline never changes sign
    /// and is never zero while it drives a leg. Intersection interiors are out of
    /// scope here: a left turn inherently crosses the oncoming lane, and turn
    /// geometry belongs with the intersection manoeuvre work (#673).
    ///
    /// The sign convention is the trap this type exists to close.
    /// <see cref="Road.PointAt"/>'s perpendicular offset is <b>+X</b> on a
    /// north-south road but <b>+Z</b> on an east-west one, while "right" is the
    /// right-hand normal of the HEADING — <c>(dx, dz) -> (dz, -dx)</c> in Unity's
    /// XZ plane, i.e. heading +Z (north) has its right hand pointing +X (east).
    /// Substituting the four cases:
    ///
    /// <list type="bullet">
    /// <item>north-south, travelling +Z (north): right is +X -> <b>+offset</b></item>
    /// <item>north-south, travelling -Z (south): right is -X -> <b>-offset</b></item>
    /// <item>east-west, travelling +X (east): right is -Z -> <b>-offset</b></item>
    /// <item>east-west, travelling -X (west): right is +Z -> <b>+offset</b></item>
    /// </list>
    ///
    /// So the sign FLIPS between the two orientations. An implementation that
    /// assumes <c>travelSign * offset</c> everywhere drives correctly down one
    /// street and into oncoming traffic on the other.
    /// </summary>
    public static class RoadLane
    {
        /// <summary>
        /// Distance from the road centerline to a lane's own centre: half of one
        /// lane's width, and a lane is half the road — so a quarter of
        /// <see cref="WorldDimensions.RoadWidth"/> (1.5m at the locked 6m road).
        /// Derived rather than written as a literal (#161/#105) so a road-width
        /// change carries the lane with it, and comfortably inside both the kit
        /// art's paved band and <see cref="Road.Contains"/>' half-width guard.
        /// </summary>
        public const float Offset = WorldDimensions.RoadWidth / 4f;

        /// <summary>
        /// The signed <see cref="Road.PointAt"/> perpendicular offset that places
        /// a vehicle in its right-hand lane on a road of
        /// <paramref name="orientation"/> travelling in
        /// <paramref name="travelSign"/> (+1 toward increasing along-coordinates,
        /// -1 toward decreasing — the same sign
        /// <see cref="RoadCrossingTraversal"/> and <see cref="CarFollowing"/>
        /// read).
        /// </summary>
        public static float PerpendicularOffsetFor(StreetOrientation orientation, float travelSign)
        {
            var sign = travelSign < 0f ? -1f : 1f;

            // Right-hand normal of the heading, expressed in the road's own
            // perpendicular axis — see the sign table in the type doc.
            return orientation == StreetOrientation.NorthSouth
                ? sign * Offset
                : -sign * Offset;
        }
    }
}
