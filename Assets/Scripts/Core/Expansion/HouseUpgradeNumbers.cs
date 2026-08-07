using System;
using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for house-upgrade pricing and the level
    /// ceiling (#59), mirroring <see cref="HouseBuildNumbers"/>. Decided
    /// 2026-07-14 (Derek, in conversation;
    /// docs/specs/expansion.md#pricing): four levels total, upgrade costs
    /// double each step (100 / 200 / 400). As of #620 each value reads from the
    /// runtime-overridable <see cref="TuningConfig.Active"/>; the shipping
    /// defaults live on <see cref="TuningConfig"/>.
    /// </summary>
    public static class HouseUpgradeNumbers
    {
        /// <summary>The highest level a house can reach; a house is built at
        /// <see cref="World.House.InitialLevel"/> (1) and climbs to here.</summary>
        public static int MaxLevel => TuningConfig.Active.HouseMaxLevel;

        /// <summary>Coin cost of the level 1 -> 2 upgrade.</summary>
        public static int CostToLevel2 => TuningConfig.Active.HouseUpgradeCostToLevel2;

        /// <summary>Coin cost of the level 2 -> 3 upgrade.</summary>
        public static int CostToLevel3 => TuningConfig.Active.HouseUpgradeCostToLevel3;

        /// <summary>Coin cost of the level 3 -> 4 upgrade.</summary>
        public static int CostToLevel4 => TuningConfig.Active.HouseUpgradeCostToLevel4;

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
