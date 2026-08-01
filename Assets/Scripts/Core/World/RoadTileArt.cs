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
    /// <c>road-straight</c> arms, and OpposingTurns is deferred (a follow-up
    /// composes two bends), so both resolve nothing here.
    ///
    /// Each staged piece is authored in one fixed orientation, read empirically
    /// from the kit OBJ vertices:
    /// <list type="bullet">
    /// <item><c>road-crossroad-path</c> — the symmetric 4-way (baked crosswalks).</item>
    /// <item><c>road-intersection-path</c> — a 3-way whose omitted arm is SOUTH,
    /// i.e. roads on N/E/W == <see cref="TileType.TeeNorth"/> (baked crosswalks).</item>
    /// <item><c>road-bend</c> — a 90-degree turn connecting its NORTH and WEST
    /// edges == <see cref="TileType.TurnNW"/>.</item>
    /// <item><c>road-end-round</c> — a rounded dead-end whose road exits EAST
    /// == <see cref="TileType.CulDeSacEast"/>.</item>
    /// </list>
    /// The yaw for any other rotation of that family is the clockwise (about +Y)
    /// step count from the authored member — the same
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

            { TileType.TurnNW, new RoadTilePiece(BendKey, YawNone, false) },
            { TileType.TurnNE, new RoadTilePiece(BendKey, YawQuarterCW, false) },
            { TileType.TurnSE, new RoadTilePiece(BendKey, YawHalf, false) },
            { TileType.TurnSW, new RoadTilePiece(BendKey, YawThreeQuarterCW, false) },

            { TileType.CulDeSacEast, new RoadTilePiece(EndRoundKey, YawNone, false) },
            { TileType.CulDeSacSouth, new RoadTilePiece(EndRoundKey, YawQuarterCW, false) },
            { TileType.CulDeSacWest, new RoadTilePiece(EndRoundKey, YawHalf, false) },
            { TileType.CulDeSacNorth, new RoadTilePiece(EndRoundKey, YawThreeQuarterCW, false) },

            // TODO(#508 follow-up): OpposingTurns compose two independent bends
            // (a NE + SW arc, or NW + SE) — no single centre mesh, and no
            // crossing, so no crosswalks. The authored map does place an
            // OpposingTurnsNS today, so until this is wired it keeps rendering as
            // plain tiled straight arms (its pre-#508 behaviour, unchanged).
        };

        /// <summary>The centre mesh for <paramref name="type"/>, or false when the
        /// type has none (straight tiles and the deferred OpposingTurns).</summary>
        public static bool TryGetCenterPiece(TileType type, out RoadTilePiece piece)
        {
            return Pieces.TryGetValue(type, out piece);
        }
    }
}
