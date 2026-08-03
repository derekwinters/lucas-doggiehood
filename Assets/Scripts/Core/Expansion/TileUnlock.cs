using System;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The per-tile frontier unlock cost (#295, rebalanced #540) — a single,
    /// named, swappable balance function so the curve is a one-place tune later.
    /// It charges <see cref="TileUnlockNumbers.BaseCost"/> plus
    /// <see cref="TileUnlockNumbers.PerExistingTileStep"/> for every tile the
    /// player has already unlocked (Derek's "+10 per existing tile"). Callers
    /// thread the live <c>Map.Tiles.Count</c> (total placed tiles, origin
    /// included); the origin FourWay is subtracted
    /// (<see cref="TileUnlockNumbers.OriginTileCount"/>) so the scaling counts
    /// only player-unlocked tiles and the first unlock is at the base. No cap.
    /// </summary>
    public static class TileUnlock
    {
        /// <summary><paramref name="placedTileCount"/> is the number of tiles
        /// already on the live map, INCLUDING the seeded origin FourWay. The
        /// origin is excluded from the scaling here, so the first player unlock
        /// (only the origin placed, count == 1) is priced at the base.</summary>
        public static int Cost(int placedTileCount)
        {
            var playerUnlockedTiles = Math.Max(0, placedTileCount - TileUnlockNumbers.OriginTileCount);
            return TileUnlockNumbers.BaseCost + TileUnlockNumbers.PerExistingTileStep * playerUnlockedTiles;
        }
    }
}
