namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for the player-choice frontier tile-unlock
    /// pricing (#295). Derek's 2026-07-31 decision: every frontier tile costs
    /// the same flat amount today, defaulting to the 100-coin base that the
    /// onboarding "expand the map" step already used. A future per-existing-tile
    /// scaling ("+10 per existing tile") is explicitly a later tuning decision —
    /// see <see cref="TileUnlock"/> for the swappable cost function that keeps
    /// flat-vs-scaling a one-place change. Tune here (and only here).
    /// </summary>
    public static class TileUnlockNumbers
    {
        /// <summary>Flat coin cost of unlocking one frontier tile.</summary>
        public const int BaseCost = 100;
    }
}
