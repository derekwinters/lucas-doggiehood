namespace Doggiehood.Core.World
{
    /// <summary>A permanently placed world item from a completed quest (#27).</summary>
    public sealed class PlacedItem
    {
        public int HouseId { get; }
        public string ItemName { get; }

        public PlacedItem(int houseId, string itemName)
        {
            HouseId = houseId;
            ItemName = itemName;
        }
    }
}
