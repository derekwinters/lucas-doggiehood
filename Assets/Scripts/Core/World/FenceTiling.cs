using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>One rendered fence piece of a lot fence (#129): where it
    /// sits (the model has a centered ground pivot), which way its length
    /// axis points — the fence model's length runs along model-local +X,
    /// so under Unity's yaw convention (degrees, clockwise from above)
    /// local +X lands on world (cos yaw, -sin yaw) — and how much its
    /// model-local length must be scaled. Height and thickness always
    /// scale by <see cref="FenceTiling.Scale"/>.</summary>
    public readonly struct FencePiece
    {
        public GridPoint Position { get; }
        public float YawDegrees { get; }
        public float LengthScale { get; }

        public FencePiece(GridPoint position, float yawDegrees, float lengthScale)
        {
            Position = position;
            YawDegrees = yawDegrees;
            LengthScale = lengthScale;
        }
    }

    /// <summary>
    /// Layout math for rendering a <see cref="FenceRun"/> as tiled City
    /// Kit Suburban fence pieces (fence.fbx: 0.475 x 0.075 model-local
    /// footprint, 0.27 high, centered ground pivot, length along local +X
    /// — parsed from the kit GLB geometry, the same source as the #125
    /// house footprints). Same Core-computes/Unity-instantiates pattern
    /// and same compress-to-fit rule as <see cref="WalkwayTiling"/>: a
    /// whole number of pieces per run, each shrunk to exactly
    /// length/count (never stretched past a full piece), so the fence
    /// ends land precisely on the lot corners and the gate-gap edges.
    /// </summary>
    public static class FenceTiling
    {
        /// <summary>Model-local length of one fence piece along its local
        /// +X axis.</summary>
        public const float PieceModelLength = 0.475f;

        /// <summary>Model-local height of one fence piece.</summary>
        public const float PieceModelHeight = 0.27f;

        /// <summary>
        /// Uniform scale for height and thickness (length compresses per
        /// piece on top of it). Decision (#129, 2026-07-14): 5 — half the
        /// walkway/road ground-tile scale (x10, which would make a 2.7m
        /// wall) — lands the 0.27 model height on 1.35m: a believable
        /// suburban fence against the 8m-footprint houses that keeps dogs
        /// and yards visible. Derek tunes it in the Editor check.
        /// </summary>
        public const float Scale = 5f;

        private const float Epsilon = 0.001f;

        /// <summary>The pieces tiling <paramref name="run"/> from its A
        /// end to its B end, in order.</summary>
        public static IReadOnlyList<FencePiece> PiecesAlong(FenceRun run)
        {
            var length = run.Length;
            if (length < Epsilon)
            {
                return Array.Empty<FencePiece>();
            }

            var nominalLength = PieceModelLength * Scale;

            // Enough whole pieces to cover the run, each compressed to
            // exactly length/count (the small tolerance keeps an exact
            // multiple from picking up a superfluous piece to float error).
            var count = Math.Max(1, (int)Math.Ceiling(length / nominalLength - 0.0001f));
            var pieceLength = length / count;

            var dirX = (run.B.X - run.A.X) / length;
            var dirZ = (run.B.Z - run.A.Z) / length;

            // Local +X maps to world (cos yaw, -sin yaw) under Unity's
            // clockwise-from-above yaw, so pointing it down the run means
            // yaw = atan2(-dirZ, dirX).
            var yawDegrees = (float)(Math.Atan2(-dirZ, dirX) * 180.0 / Math.PI);

            var pieces = new List<FencePiece>(count);
            for (var i = 0; i < count; i++)
            {
                var alongDistance = (i + 0.5f) * pieceLength;
                pieces.Add(new FencePiece(
                    new GridPoint(
                        run.A.X + dirX * alongDistance,
                        run.A.Z + dirZ * alongDistance),
                    yawDegrees,
                    pieceLength / PieceModelLength));
            }

            return pieces;
        }
    }
}
