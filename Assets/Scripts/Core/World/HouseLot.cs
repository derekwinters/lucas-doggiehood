namespace Doggiehood.Core.World
{
    /// <summary>A buildable house position in the neighborhood (#7, #38).</summary>
    public sealed class HouseLot
    {
        public int HouseId { get; }
        public Quadrant Quadrant { get; }
        public GridPoint Position { get; }

        /// <summary>Whether this lot renders a boundary fence (#129).
        /// Defaults on — Derek's request has all four starting lots fenced
        /// — but exists per lot so fences can later become a buyable
        /// decoration or house-level upgrade (a separate design decision,
        /// not built yet). A lot with the flag off contributes no fence
        /// geometry (see LotFence).</summary>
        public bool HasFence { get; }

        public HouseLot(int houseId, Quadrant quadrant, GridPoint position, bool hasFence = true)
        {
            HouseId = houseId;
            Quadrant = quadrant;
            Position = position;
            HasFence = hasFence;
        }
    }
}
