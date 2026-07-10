namespace Doggiehood.Core.World
{
    /// <summary>A buildable house position in the neighborhood (#7, #38).</summary>
    public sealed class HouseLot
    {
        public int HouseId { get; }
        public Quadrant Quadrant { get; }
        public GridPoint Position { get; }

        public HouseLot(int houseId, Quadrant quadrant, GridPoint position)
        {
            HouseId = houseId;
            Quadrant = quadrant;
            Position = position;
        }
    }
}
