namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for house-build pricing constants (#57).
    /// Decided 2026-07-14 (Derek, in conversation;
    /// docs/specs/expansion.md#pricing: "Build a house — 50 coins, flat").
    /// Expect adjustment during playtesting — tune here (and only here).
    /// </summary>
    public static class HouseBuildNumbers
    {
        /// <summary>Flat cost to build one house on an empty lot.</summary>
        public const int Cost = 50;
    }
}
