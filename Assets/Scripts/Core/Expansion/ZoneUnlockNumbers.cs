namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for zone-unlock pricing constants (#56).
    /// Decided 2026-07-14 (Derek, in conversation;
    /// docs/specs/expansion.md#pricing: "100 coins for the first zone, +100
    /// per subsequent zone (100, 200, 300, ...)"). Expect adjustment during
    /// playtesting — tune here (and only here).
    /// </summary>
    public static class ZoneUnlockNumbers
    {
        /// <summary>Cost of the first zone.</summary>
        public const int BaseCost = 100;

        /// <summary>Added to the cost for each zone after the first.</summary>
        public const int CostStep = 100;
    }
}
