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
    }
}
