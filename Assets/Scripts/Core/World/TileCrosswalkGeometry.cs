using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One crosswalk patch of an intersection tile (#508): an axis-aligned
    /// rectangle centred at <see cref="Center"/>, <see cref="SpanX"/> metres
    /// wide in world X and <see cref="SpanZ"/> metres deep in world Z. The Unity
    /// layer renders a flat quad per rect so the graybox fallback paints
    /// crosswalks on every unlocked intersection, not just the hardcoded origin.
    /// </summary>
    public readonly struct TileCrosswalkRect
    {
        public GridPoint Center { get; }
        public float SpanX { get; }
        public float SpanZ { get; }

        public TileCrosswalkRect(GridPoint center, float spanX, float spanZ)
        {
            Center = center;
            SpanX = spanX;
            SpanZ = spanZ;
        }
    }

    /// <summary>
    /// Crosswalk patches for a tile derived from its <see cref="TileCatalog"/>
    /// road edges (#508), the same per-tile way <see cref="TileRoadGeometry"/>
    /// derives road arms — replacing the old crosswalk derivation that read only
    /// the hardcoded origin <see cref="NeighborhoodLayout.WalkNetwork"/> and so
    /// painted nothing on an unlocked Tee.
    ///
    /// Only a true crossing carries crosswalks: the types whose centre mesh
    /// bakes crosswalk stripes (the 4-way and the four Tees, per
    /// <see cref="RoadTileArt"/>). A straight, turn, or cul-de-sac has no
    /// crossing to paint, so all of those yield none. Each intersection gets
    /// one patch per
    /// road-bearing edge — a Tee's three real arms, never a phantom fourth over
    /// its closed edge — placed a sidewalk-centre offset out from the tile
    /// centre and clipped across the road so it never covers sidewalk pavement.
    /// </summary>
    public static class TileCrosswalkGeometry
    {
        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West,
        };

        /// <summary>Signed distance from the tile centre to a crosswalk patch's
        /// centre along the crossed road: the crossing road's sidewalk-centre
        /// magnitude, RoadWidth/2 + verge + SidewalkWidth/2 = 4.75m (the same
        /// place <see cref="WalkNetwork"/> puts its crosswalk edges).</summary>
        private const float CrosswalkOffset =
            WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;

        /// <summary>Across-the-road extent of a patch: RoadWidth plus both
        /// verges, so it stops at the sidewalk boundary and never paints over
        /// sidewalk pavement (matching the origin's rendered clip).</summary>
        private const float AcrossRoadSpan = WorldDimensions.RoadWidth + 2f * WorldDimensions.GrassVergeWidth;

        public static IReadOnlyList<TileCrosswalkRect> RectanglesFor(TileCoordinate coordinate, TileType type)
        {
            var rects = new List<TileCrosswalkRect>();
            if (!RoadTileArt.TryGetCenterPiece(type, out var piece) || !piece.HasBakedCrosswalks)
            {
                return rects;
            }

            var definition = TileCatalog.Get(type);
            var center = TileGeometry.CenterOf(coordinate);

            foreach (var edge in AllEdges)
            {
                if (!definition.HasRoadOn(edge))
                {
                    continue;
                }

                var isNorthSouthArm = edge == TileEdge.North || edge == TileEdge.South;
                var patchCenter = isNorthSouthArm
                    ? new GridPoint(center.X, center.Z + SignToward(edge) * CrosswalkOffset)
                    : new GridPoint(center.X + SignToward(edge) * CrosswalkOffset, center.Z);

                // A north/south arm runs along Z, so its crossing band is wide
                // across X and thin (CrosswalkWidth) along Z; an east/west arm
                // is the mirror.
                var rect = isNorthSouthArm
                    ? new TileCrosswalkRect(patchCenter, AcrossRoadSpan, WorldDimensions.CrosswalkWidth)
                    : new TileCrosswalkRect(patchCenter, WorldDimensions.CrosswalkWidth, AcrossRoadSpan);

                rects.Add(rect);
            }

            return rects;
        }

        private static float SignToward(TileEdge edge)
        {
            switch (edge)
            {
                case TileEdge.North:
                case TileEdge.East:
                    return 1f;
                default:
                    return -1f;
            }
        }
    }
}
