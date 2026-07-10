using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// Automatic decoration placement (#48): a small deterministic grid of
    /// spots beside each house, on the side away from the intersection so
    /// decorations never land in the street. No manual arranging exists.
    /// </summary>
    public static class YardPlacement
    {
        private const float SideOffset = 4.5f;
        private const float SlotSpacing = 2f;

        public static GridPoint PositionFor(int houseId, int slotIndex)
        {
            var lot = NeighborhoodLayout.GetHouseLot(houseId);

            // Push outward from the intersection, fan slots along the house.
            var outwardX = Math.Sign(lot.Position.X) * SideOffset;
            var alongZ = (slotIndex - 1) * SlotSpacing;

            return new GridPoint(lot.Position.X + outwardX, lot.Position.Z + alongZ);
        }
    }
}
