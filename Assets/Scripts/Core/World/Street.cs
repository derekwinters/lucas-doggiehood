namespace Doggiehood.Core.World
{
    public enum StreetOrientation
    {
        NorthSouth,
        EastWest,
    }

    /// <summary>An axis-aligned street through the neighborhood (#7).</summary>
    public sealed class Street
    {
        public string Name { get; }
        public StreetOrientation Orientation { get; }

        public Street(string name, StreetOrientation orientation)
        {
            Name = name;
            Orientation = orientation;
        }
    }
}
