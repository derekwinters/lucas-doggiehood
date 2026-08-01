namespace Doggiehood.Core.World
{
    /// <summary>
    /// #453: a stable, collision-free house id for a player-choice frontier lot,
    /// derived purely from the lot's tile <see cref="TileCoordinate"/> and
    /// <see cref="Quadrant"/>. Because frontier tiles unlock in player-chosen
    /// order (not a fixed authored sequence), a sequential counter can't be
    /// replayed deterministically from the target map alone — so the id is a
    /// pure function of position instead, needing no counter and no extra
    /// persisted mapping (the same "pure function of the id" idea
    /// <see cref="Art.HouseVariantAssignment.ForHouse"/> already uses downstream).
    ///
    /// The scheme is injective by construction: zig-zag-encode the signed
    /// <c>col</c>/<c>row</c> to non-negative integers, Cantor-pair those into one
    /// non-negative integer, multiply by the quadrant count and add the quadrant
    /// index, then add a fixed base offset clear of every id already in use. So
    /// no two distinct (coordinate, quadrant) pairs ever collide, and every id is
    /// >= <see cref="BaseId"/> — well above the 4 starting-layout ids and above
    /// <see cref="Art.HouseVariantAssignment.FirstZoneHouseId"/>, so a frontier
    /// lot reads as a zone house that rolls its own art variant.
    /// </summary>
    public static class FrontierHouseId
    {
        /// <summary>Fixed base offset kept clear of every house id already in
        /// use (the 4 starting-layout ids 1-4, and any others), so the lowest
        /// frontier id — quadrant NE of the origin coordinate — is still
        /// unmistakably a frontier/zone house id.</summary>
        public const int BaseId = 1000;

        /// <summary>Number of quadrant slots a tile can carry (one house per
        /// <see cref="Quadrant"/>) — the multiplier that keeps each quadrant's id
        /// distinct within a tile.</summary>
        private const int QuadrantCount = 4;

        /// <summary>The stable id for the lot in <paramref name="quadrant"/> of
        /// the tile at <paramref name="coordinate"/>. Pure and collision-free —
        /// see the type summary.</summary>
        public static int For(TileCoordinate coordinate, Quadrant quadrant)
        {
            var paired = CantorPair(ZigZag(coordinate.Col), ZigZag(coordinate.Row));
            return BaseId + paired * QuadrantCount + (int)quadrant;
        }

        /// <summary>Maps a signed integer to a non-negative one bijectively
        /// (0,-1,1,-2,2,... -> 0,1,2,3,4,...), so negative coordinates encode
        /// without colliding with positive ones.</summary>
        private static int ZigZag(int value)
        {
            return value >= 0 ? value * 2 : -value * 2 - 1;
        }

        /// <summary>The Cantor pairing function — a bijection from a pair of
        /// non-negative integers to a single non-negative integer, so distinct
        /// (col, row) encodings map to distinct pair values.</summary>
        private static int CantorPair(int a, int b)
        {
            return (a + b) * (a + b + 1) / 2 + b;
        }
    }
}
