namespace Doggiehood.Core.World
{
    public enum StreetOrientation
    {
        NorthSouth,
        EastWest,
    }

    /// <summary>An axis-aligned street through the neighborhood (#7).
    /// Streets have no display names — decided in #101.</summary>
    public sealed class Street
    {
        public StreetOrientation Orientation { get; }

        public Street(StreetOrientation orientation)
        {
            Orientation = orientation;
        }
    }
}
