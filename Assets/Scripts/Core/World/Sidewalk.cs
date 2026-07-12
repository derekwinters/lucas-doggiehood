namespace Doggiehood.Core.World
{
    /// <summary>Which side of a <see cref="Road"/> a <see cref="Sidewalk"/> is on.</summary>
    public enum RoadSide
    {
        Positive,
        Negative,
    }

    /// <summary>
    /// One side's sidewalk for a <see cref="Road"/> (#106): a grass verge
    /// then the sidewalk strip, offset from the road centerline using only
    /// the locked #105 <see cref="WorldDimensions"/> constants.
    /// </summary>
    public sealed class Sidewalk
    {
        public Road Road { get; }
        public RoadSide Side { get; }

        public float VergeWidth
        {
            get { return WorldDimensions.GrassVergeWidth; }
        }

        public float Width
        {
            get { return WorldDimensions.SidewalkWidth; }
        }

        /// <summary>Signed perpendicular distance from the road centerline
        /// to this sidewalk's own centerline.</summary>
        public float CenterOffset
        {
            get
            {
                var magnitude = Road.Width / 2f + VergeWidth + Width / 2f;
                return Side == RoadSide.Positive ? magnitude : -magnitude;
            }
        }

        public Sidewalk(Road road, RoadSide side)
        {
            Road = road;
            Side = side;
        }
    }
}
