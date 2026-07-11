using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The starting neighborhood (#7, #38): two streets crossing at the
    /// origin, one house lot per quadrant. Distances are meters on the
    /// ground plane; the Unity layer builds world geometry from this data.
    /// </summary>
    public static class NeighborhoodLayout
    {
        public const float StreetWidth = 6f;
        public const float LotDistanceFromCenter = 14f;

        public static readonly GridPoint Intersection = new GridPoint(0f, 0f);

        public static IReadOnlyList<Street> Streets { get; } = new[]
        {
            new Street("North-South Street", StreetOrientation.NorthSouth),
            new Street("East-West Street", StreetOrientation.EastWest),
        };

        public static IReadOnlyList<HouseLot> HouseLots { get; } = new[]
        {
            new HouseLot(1, Quadrant.NorthEast, new GridPoint(LotDistanceFromCenter, LotDistanceFromCenter)),
            new HouseLot(2, Quadrant.NorthWest, new GridPoint(-LotDistanceFromCenter, LotDistanceFromCenter)),
            new HouseLot(3, Quadrant.SouthEast, new GridPoint(LotDistanceFromCenter, -LotDistanceFromCenter)),
            new HouseLot(4, Quadrant.SouthWest, new GridPoint(-LotDistanceFromCenter, -LotDistanceFromCenter)),
        };

        public static HouseLot GetHouseLot(int houseId)
        {
            foreach (var lot in HouseLots)
            {
                if (lot.HouseId == houseId)
                {
                    return lot;
                }
            }

            throw new ArgumentException($"No house lot with id {houseId}.", nameof(houseId));
        }
    }
}
