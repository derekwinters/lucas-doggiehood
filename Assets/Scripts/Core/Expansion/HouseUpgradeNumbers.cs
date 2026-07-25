using System;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for house-upgrade pricing and the level
    /// ceiling (#59), mirroring <see cref="HouseBuildNumbers"/>. Decided
    /// 2026-07-14 (Derek, in conversation;
    /// docs/specs/expansion.md#pricing): four levels total, upgrade costs
    /// double each step (100 / 200 / 400). Expect adjustment during
    /// playtesting — tune here (and only here).
    /// </summary>
    public static class HouseUpgradeNumbers
    {
        /// <summary>The highest level a house can reach; a house is built at
        /// <see cref="World.House.InitialLevel"/> (1) and climbs to here.</summary>
        public const int MaxLevel = 4;

        /// <summary>Coin cost of the level 1 -> 2 upgrade.</summary>
        public const int CostToLevel2 = 100;

        /// <summary>Coin cost of the level 2 -> 3 upgrade.</summary>
        public const int CostToLevel3 = 200;

        /// <summary>Coin cost of the level 3 -> 4 upgrade.</summary>
        public const int CostToLevel4 = 400;

        /// <summary>Coin cost to upgrade a house up to
        /// <paramref name="targetLevel"/> (the level it will be after the
        /// step). Throws for any level that isn't a reachable upgrade step
        /// (level 1 is as-built with nothing to pay; anything above
        /// <see cref="MaxLevel"/> is past the ceiling).</summary>
        public static int CostToReach(int targetLevel)
        {
            switch (targetLevel)
            {
                case 2:
                    return CostToLevel2;
                case 3:
                    return CostToLevel3;
                case 4:
                    return CostToLevel4;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(targetLevel), targetLevel,
                        "Only levels 2-4 are reachable upgrade steps.");
            }
        }
    }
}
