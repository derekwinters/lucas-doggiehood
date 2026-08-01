namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The per-tile frontier unlock cost (#295) — a single, named, swappable
    /// function so flat-vs-scaling is a one-place change later. Today it returns
    /// the flat <see cref="TileUnlockNumbers.BaseCost"/> and ignores
    /// <paramref name="placedTileCount"/>; the count is threaded through so
    /// Derek's future "+10 coins per existing tile" scaling is a one-line edit
    /// here (e.g. <c>BaseCost + placedTileCount * Step</c>) with no caller
    /// changes.
    /// </summary>
    public static class TileUnlock
    {
        /// <summary><paramref name="placedTileCount"/> is the number of tiles
        /// already on the live map; unused today (flat cost) but available so a
        /// future scaling formula can price by neighborhood size.</summary>
        public static int Cost(int placedTileCount)
        {
            return TileUnlockNumbers.BaseCost;
        }
    }
}
