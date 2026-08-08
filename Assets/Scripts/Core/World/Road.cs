using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// A finite straight road segment (#106): an axis-aligned line through
    /// <see cref="Center"/>, extending <see cref="HalfLength"/> in both
    /// directions along <see cref="Orientation"/>. Declares a
    /// <see cref="Sidewalk"/> on both sides, offset per the locked #105
    /// <see cref="WorldDimensions"/> constants only.
    /// </summary>
    public sealed class Road
    {
        public StreetOrientation Orientation { get; }
        public GridPoint Center { get; }
        public float HalfLength { get; }

        public float Width
        {
            get { return WorldDimensions.RoadWidth; }
        }

        public Road(StreetOrientation orientation, GridPoint center, float halfLength)
        {
            Orientation = orientation;
            Center = center;
            HalfLength = halfLength;
        }

        /// <summary>Both sidewalks flanking this road — one per side.</summary>
        public IReadOnlyList<Sidewalk> Sidewalks
        {
            get
            {
                return new[]
                {
                    new Sidewalk(this, RoadSide.Positive),
                    new Sidewalk(this, RoadSide.Negative),
                };
            }
        }

        /// <summary>
        /// True when <paramref name="point"/> lies on this road's paved
        /// surface (#538): within <see cref="HalfLength"/> along the road's
        /// axis of <see cref="Center"/> and within half the road
        /// <see cref="Width"/> perpendicular to it. Used to guard the
        /// "a delivery truck never leaves the roadway" invariant.
        /// </summary>
        public bool Contains(GridPoint point)
        {
            var alongAxis = Orientation == StreetOrientation.NorthSouth
                ? point.Z - Center.Z
                : point.X - Center.X;
            var perpendicular = Orientation == StreetOrientation.NorthSouth
                ? point.X - Center.X
                : point.Z - Center.Z;

            return System.Math.Abs(alongAxis) <= HalfLength + Epsilon
                   && System.Math.Abs(perpendicular) <= (Width / 2f) + Epsilon;
        }

        private const float Epsilon = 0.0001f;

        /// <summary>
        /// The signed distance of <paramref name="point"/> along this road's own
        /// axis from <see cref="Center"/> (Z for a north-south road, X for
        /// east-west) — the inverse of <see cref="PointAt"/>'s alongAxis
        /// argument. Used by vehicle right-of-way (#546) to place crosswalks and
        /// the driving position on a common along-road number line.
        /// </summary>
        public float AlongAxis(GridPoint point)
        {
            return Orientation == StreetOrientation.NorthSouth
                ? point.Z - Center.Z
                : point.X - Center.X;
        }

        /// <summary>
        /// A world point on this road's line: <paramref name="alongAxis"/>
        /// is the signed distance from <see cref="Center"/> along the
        /// road's own axis (Z for a north-south road, X for east-west);
        /// <paramref name="perpendicularOffset"/> is the signed distance
        /// perpendicular to that axis.
        /// </summary>
        public GridPoint PointAt(float alongAxis, float perpendicularOffset)
        {
            return Orientation == StreetOrientation.NorthSouth
                ? new GridPoint(Center.X + perpendicularOffset, Center.Z + alongAxis)
                : new GridPoint(Center.X + alongAxis, Center.Z + perpendicularOffset);
        }

        /// <summary>
        /// #672: the point a vehicle actually drives through at
        /// <paramref name="alongAxis"/> — the centre of the RIGHT-hand lane for
        /// the direction it is travelling (<paramref name="travelSign"/>: +1
        /// toward increasing along-coordinates, -1 toward decreasing). Reversing
        /// direction puts it on the other side of the centerline, because "right"
        /// is relative to travel, not to the road's axis. See
        /// <see cref="RoadLane"/> for the sign convention and the lane invariant.
        /// </summary>
        public GridPoint LanePointAt(float alongAxis, float travelSign)
        {
            return PointAt(alongAxis, RoadLane.PerpendicularOffsetFor(Orientation, travelSign));
        }
    }
}
