using Doggiehood.Core.World;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// A yard decoration (#45). Always scoped to exactly one house's yard
    /// (#46) — there is deliberately no way to target streets or common
    /// spaces. Placement position is assigned automatically (#48).
    /// </summary>
    public sealed class Decoration
    {
        public string ItemName { get; }
        public int HouseId { get; }
        public GridPoint YardPosition { get; }

        public Decoration(string itemName, int houseId, GridPoint yardPosition)
        {
            ItemName = itemName;
            HouseId = houseId;
            YardPosition = yardPosition;
        }
    }
}
