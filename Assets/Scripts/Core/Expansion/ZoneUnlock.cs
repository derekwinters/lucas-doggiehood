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

        /// <summary>
        /// Whether <paramref name="balance"/> covers the nth zone's unlock
        /// cost (#178, docs/specs/expansion.md "Expansion indicator") —
        /// the affordability check the discoverability indicator tints
        /// gold (true) or grey/black (false) on, without spending anything.
        /// </summary>
        public static bool IsAffordable(int balance, int zoneNumber)
        {
            return balance >= CostForZoneNumber(zoneNumber);
        }
    }
}
