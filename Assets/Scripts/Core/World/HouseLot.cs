namespace Doggiehood.Core.World
{
    /// <summary>A buildable house position in the neighborhood (#7, #38).</summary>
    public sealed class HouseLot
    {
        public int HouseId { get; }
        public Quadrant Quadrant { get; }
        public GridPoint Position { get; }

        /// <summary>Whether this lot renders its backyard fence (#129,
        /// reshaped by #146). Defaults OFF since #146 — every lot's fence
        /// is defined but hidden until a future quest purchases it (#147).
        /// A lot with the flag off contributes no built fence, but its
        /// geometry stays queryable (see LotFence.GeometryFor).</summary>
        public bool HasFence { get; }

        public HouseLot(int houseId, Quadrant quadrant, GridPoint position, bool hasFence = false)
        {
            HouseId = houseId;
            Quadrant = quadrant;
            Position = position;
            HasFence = hasFence;
        }
    }
}
