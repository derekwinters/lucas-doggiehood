using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>One rendered path piece of a front walkway (#128): where
    /// it sits, which way its length axis points (Unity yaw convention:
    /// degrees, clockwise from above, 0 = local +Z on world +Z), and how
    /// much its model-local length must be scaled. Width always scales by
    /// <see cref="WalkwayTiling.WidthScale"/>.</summary>
    public readonly struct WalkwayPiece
    {
        public GridPoint Position { get; }
        public float YawDegrees { get; }
        public float LengthScale { get; }

        public WalkwayPiece(GridPoint position, float yawDegrees, float lengthScale)
        {
            Position = position;
            YawDegrees = yawDegrees;
            LengthScale = lengthScale;
        }
    }

    /// <summary>
    /// Layout math for rendering a front walkway (#128) as tiled City Kit
    /// Suburban path pieces (path-short: a 0.2 x 0.2 model-local square
    /// paver, ground pivot, flat). Same Core-computes/Unity-instantiates
    /// pattern as CatalogGalleryLayout, and the same scale derivation as
    /// WorldBuilder.RoadTileScale: x10 lands the piece's 0.2 model width
    /// exactly on WorldDimensions.SidewalkWidth (2m) — which is also the
    /// walkway edge's declared Width, so the visual and the graph agree.
    /// Along the walkway, pieces shrink slightly (never stretch past a
    /// full paver) so a whole number of them covers door → sidewalk
    /// exactly: no gap at the door, no overshoot onto the kit road tile's
    /// raised sidewalk band.
    /// </summary>
    public static class WalkwayTiling
    {
        /// <summary>Model-local size of the square path-short paver along
        /// both horizontal axes (parsed from the kit GLB geometry, the
        /// same source as the #125 house footprints).</summary>
        public const float PieceModelSize = 0.2f;

        /// <summary>Uniform width scale: the paver spans the walkway's
        /// full SidewalkWidth, matching the kit-sidewalk aesthetic.</summary>
        public const float WidthScale = WorldDimensions.SidewalkWidth / PieceModelSize;

        private const float Epsilon = 0.001f;

        /// <summary>The pieces tiling <paramref name="walkway"/> from its
        /// door end (A) to its sidewalk end (B), in order.</summary>
        public static IReadOnlyList<WalkwayPiece> PiecesAlong(WalkEdge walkway)
        {
            var length = walkway.Length;
            if (length < Epsilon)
            {
                return Array.Empty<WalkwayPiece>();
            }

            var nominalLength = PieceModelSize * WidthScale;

            // Enough whole pieces to cover the segment, each compressed to
            // exactly length/count (the small tolerance keeps an exact
            // multiple from picking up a superfluous piece to float error).
            var count = Math.Max(1, (int)Math.Ceiling(length / nominalLength - 0.0001f));
            var pieceLength = length / count;

            var dirX = (walkway.B.X - walkway.A.X) / length;
            var dirZ = (walkway.B.Z - walkway.A.Z) / length;
            var yawDegrees = (float)(Math.Atan2(dirX, dirZ) * 180.0 / Math.PI);

            var pieces = new List<WalkwayPiece>(count);
            for (var i = 0; i < count; i++)
            {
                var alongDistance = (i + 0.5f) * pieceLength;
                pieces.Add(new WalkwayPiece(
                    new GridPoint(
                        walkway.A.X + dirX * alongDistance,
                        walkway.A.Z + dirZ * alongDistance),
                    yawDegrees,
                    pieceLength / PieceModelSize));
            }

            return pieces;
        }
    }
}
