namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for the player-choice frontier tile-unlock
    /// pricing (#295, rebalanced #540). Derek's balance direction (v0.10
    /// playtest): the FIRST tile unlock costs the same as a house — a
    /// <see cref="BaseCost"/> of 50 — and then rises by
    /// <see cref="PerExistingTileStep"/> (10) per tile the player has already
    /// unlocked, with no upper cap. The seeded origin FourWay is excluded from
    /// the scaling via <see cref="OriginTileCount"/>, so the first unlock (only
    /// the origin placed) is at the base. See <see cref="TileUnlock"/> for the
    /// swappable cost function. Tune here (and only here).
    /// </summary>
    public static class TileUnlockNumbers
    {
        /// <summary>Coin cost of unlocking the FIRST frontier tile — the same as
        /// a house build (#540), down from the earlier flat 100.</summary>
        public const int BaseCost = 50;

        /// <summary>How much each already-unlocked tile adds to the next unlock's
        /// cost (#540, Derek's "+10 per existing tile").</summary>
        public const int PerExistingTileStep = 10;

        /// <summary>The number of pre-seeded, non-player tiles on a fresh map —
        /// just the origin FourWay (<c>GameState</c> seeds exactly one). Excluded
        /// from the per-tile scaling so the first PLAYER unlock is at the base.</summary>
        public const int OriginTileCount = 1;
    }
}
