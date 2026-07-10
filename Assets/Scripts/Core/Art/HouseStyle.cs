namespace Doggiehood.Core.Art
{
    public enum RoofShape
    {
        Gable,
        Hip,
        Gambrel,
        Shed,
    }

    /// <summary>
    /// A cottage silhouette variant (#64): distinct roof shape, porch
    /// treatment, and bright/saturated colors per starting house.
    /// </summary>
    public sealed class HouseStyle
    {
        public int StyleId { get; }
        public RoofShape RoofShape { get; }
        public bool HasPorch { get; }
        public string WallColorHex { get; }
        public string RoofColorHex { get; }

        public HouseStyle(int styleId, RoofShape roofShape, bool hasPorch, string wallColorHex, string roofColorHex)
        {
            StyleId = styleId;
            RoofShape = roofShape;
            HasPorch = hasPorch;
            WallColorHex = wallColorHex;
            RoofColorHex = roofColorHex;
        }
    }
}
