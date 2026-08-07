using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for house-build pricing (#57, rebalanced #540).
    /// Derek's balance direction (v0.10 playtest): a <see cref="BaseCost"/> of 50
    /// that rises by <see cref="PerBatchStep"/> (5) for every
    /// <see cref="HousesPerStep"/> (4) houses the player has already built —
    /// <c>BaseCost + PerBatchStep * floor(housesBuilt / HousesPerStep)</c>, with
    /// no upper cap. The count is player-built houses only (the caller excludes
    /// the 4 starting houses), so the first build is at the base. As of #620 the
    /// values read from the runtime-overridable <see cref="TuningConfig.Active"/>;
    /// the shipping defaults live on <see cref="TuningConfig"/>.
    /// </summary>
    public static class HouseBuildNumbers
    {
        /// <summary>Coin cost of the FIRST house the player builds — the flat
        /// base of the curve (unchanged from the earlier flat 50).</summary>
        public static int BaseCost => TuningConfig.Active.HouseBuildBaseCost;

        /// <summary>How much the build cost rises per completed batch of
        /// <see cref="HousesPerStep"/> houses (#540, Derek's "+5 per 4
        /// houses").</summary>
        public static int PerBatchStep => TuningConfig.Active.HouseBuildPerBatchStep;

        /// <summary>How many houses make up one cost step — the cost bumps once
        /// per this many player-built houses.</summary>
        public static int HousesPerStep => TuningConfig.Active.HouseBuildHousesPerStep;

        /// <summary>The cost to build the next house given how many the player has
        /// already built (<paramref name="housesBuilt"/>, excluding the 4 starting
        /// houses). Builds 1-4 are at the base, 5-8 at base+5, and so on.</summary>
        public static int Cost(int housesBuilt)
        {
            return BaseCost + PerBatchStep * (housesBuilt / HousesPerStep);
        }
    }
}
