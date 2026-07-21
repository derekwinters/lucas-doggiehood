using System;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Zone-unlock cost formula (#56): the nth zone costs
    /// <see cref="ZoneUnlockNumbers.BaseCost"/> +
    /// (n-1) * <see cref="ZoneUnlockNumbers.CostStep"/> — 100, 200, 300, ...
    /// </summary>
    public static class ZoneUnlock
    {
        /// <summary><paramref name="zoneNumber"/> is 1-based (the first
        /// zone ever unlocked is zone 1).</summary>
        public static int CostForZoneNumber(int zoneNumber)
        {
            if (zoneNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneNumber), zoneNumber, "Zone number must be >= 1.");
            }

            return ZoneUnlockNumbers.BaseCost + (zoneNumber - 1) * ZoneUnlockNumbers.CostStep;
        }
    }
}
