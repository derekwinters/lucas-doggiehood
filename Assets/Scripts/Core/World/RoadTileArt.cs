using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The single City Kit Roads mesh a junction/terminus tile renders at its
    /// centre (#508): a <see cref="ResourceKey"/> loaded by bare name from
    /// <c>Resources/</c>, the <see cref="YawDegrees"/> that rotates the mesh's
    /// authored orientation onto the tile's declared edges, and whether the mesh
    /// bakes its own crosswalk stripes (so the graybox fallback knows where to
    /// paint crosswalk patches to match).
    /// </summary>
    public readonly struct RoadTilePiece
    {
        public string ResourceKey { get; }
        public float YawDegrees { get; }
        public bool HasBakedCrosswalks { get; }

        public RoadTilePiece(string resourceKey, float yawDegrees, bool hasBakedCrosswalks)
        {
            ResourceKey = resourceKey;
            YawDegrees = yawDegrees;
            HasBakedCrosswalks = hasBakedCrosswalks;
        }
    }

    /// <summary>
    /// Resolves a <see cref="TileType"/> to the single Kenney City Kit Roads
    /// mesh that renders at the tile centre (#508), replacing the old approach
    /// of composing straight pieces at a junction. Only the junction/terminus
    /// types have a dedicated centre mesh; a straight tile is just tiled
    /// <c>road-straight</c> arms, so it resolves nothing here.
    ///
    /// Each staged piece is authored in one fixed orientation, read empirically
    /// from the kit OBJ vertices:
    /// <list type="bullet">
    /// <item><c>road-crossroad-path</c> — the symmetric 4-way (baked crosswalks).</item>
    /// <item><c>road-intersection-path</c> — a 3-way whose omitted arm is SOUTH,
    /// i.e. roads on N/E/W == <see cref="TileType.TeeNorth"/> (baked crosswalks).</item>
    /// <item><c>road-bend</c> — a 90-degree turn. Its raw kit OBJ/FBX connects
    /// its NORTH and WEST edges, but the same handedness (X-axis) mirror Unity's
    /// FBX import applies (W↔E) flips it, so the <em>imported</em> bend connects
    /// NORTH and EAST at 0-yaw == <see cref="TileType.TurnNE"/> (#515,
    /// correcting the #508 reading that trusted the un-imported OBJ pose and
    /// left one arm of every turn disconnected). Because the mirror also
    /// reverses the rotation sense, the four Turn yaws are NOT a uniform offset
    /// from the cul-de-sac's: each is derived from the imported N+E pair —
    /// <see cref="TileType.TurnNE"/> 0, <see cref="TileType.TurnSE"/> 90,
    /// <see cref="TileType.TurnSW"/> 180, <see cref="TileType.TurnNW"/> 270.</item>
    /// <item><c>road-end-round</c> — a rounded dead-end. In the raw kit OBJ/FBX
    /// its single open road exits +X, but that same FBX-import handedness
    /// (X-axis) mirror only shows on chirally-asymmetric pieces, so the
    /// <em>imported</em> mesh's open road exits WEST at 0-yaw. Its bulb
    /// is symmetric about the road axis, so that mirror is exactly a half-turn:
    /// <see cref="TileType.CulDeSacEast"/> yaws the open road back to the EAST
    /// edge at <see cref="YawHalf"/> (#514, correcting the #508 reading that
    /// trusted the un-imported OBJ pose and shipped every CulDeSac 180-degrees
    /// off — its cap facing the connecting road). The <c>road-bend</c> turn is
    /// the sibling half of the same import mirror (#515).</item>
    /// </list>
    /// The yaw for any other rotation of that family is the clockwise (about +Y)
    /// step count from that imported orientation — the same
    /// <see cref="StreetOrientation.NorthSouth"/> == 90-degree convention the
    /// existing kit road tiles already use.
    /// </summary>
    public static class RoadTileArt
    {
        public const string CrossroadPathKey = "road-crossroad-path";
        public const string IntersectionPathKey = "road-intersection-path";
        public const string BendKey = "road-bend";
        public const string EndRoundKey = "road-end-round";

        // Clockwise yaw (degrees, about +Y) rotating a piece's authored
        // orientation onto each rotation of its family — one quarter-turn per
        // compass step. Named per #161 (no inline geometry literals).
        private const float YawNone = 0f;
        private const float YawQuarterCW = 90f;
        private const float YawHalf = 180f;
        private const float YawThreeQuarterCW = 270f;

        private static readonly Dictionary<TileType, RoadTilePiece> Pieces = new Dictionary<TileType, RoadTilePiece>
        {
            { TileType.FourWay, new RoadTilePiece(CrossroadPathKey, YawNone, true) },

            { TileType.TeeNorth, new RoadTilePiece(IntersectionPathKey, YawNone, true) },
            { TileType.TeeEast, new RoadTilePiece(IntersectionPathKey, YawQuarterCW, true) },
            { TileType.TeeSouth, new RoadTilePiece(IntersectionPathKey, YawHalf, true) },
            { TileType.TeeWest, new RoadTilePiece(IntersectionPathKey, YawThreeQuarterCW, true) },

            { TileType.TurnNE, new RoadTilePiece(BendKey, YawNone, false) },
            { TileType.TurnSE, new RoadTilePiece(BendKey, YawQuarterCW, false) },
            { TileType.TurnSW, new RoadTilePiece(BendKey, YawHalf, false) },
            { TileType.TurnNW, new RoadTilePiece(BendKey, YawThreeQuarterCW, false) },

            { TileType.CulDeSacEast, new RoadTilePiece(EndRoundKey, YawHalf, false) },
            { TileType.CulDeSacSouth, new RoadTilePiece(EndRoundKey, YawThreeQuarterCW, false) },
            { TileType.CulDeSacWest, new RoadTilePiece(EndRoundKey, YawNone, false) },
            { TileType.CulDeSacNorth, new RoadTilePiece(EndRoundKey, YawQuarterCW, false) },
        };

        /// <summary>The centre mesh for <paramref name="type"/>, or false when the
        /// type has none (straight tiles and the roadless GreenSpace).</summary>
        public static bool TryGetCenterPiece(TileType type, out RoadTilePiece piece)
        {
            return Pieces.TryGetValue(type, out piece);
        }
    }
}
