using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #129: layout math for rendering LotFence runs as tiled City Kit
    /// Suburban fence pieces (fence.fbx: 0.475 long x 0.075 thick x 0.27
    /// high model-local, centered ground pivot, length along local +X —
    /// parsed from the kit GLB geometry, the same source as the #125 house
    /// footprints). Same Core-computes/Unity-instantiates pattern as
    /// WalkwayTiling: a whole number of pieces per run, compressed (never
    /// stretched past a full piece) to cover the run exactly, so fence
    /// ends land precisely on the lot corners and the gate-gap edges.
    /// </summary>
    public class FenceTilingTests
    {
        [Test]
        public void Scale_LandsTheFenceAtSuburbanFenceHeight()
        {
            // Decision (#129): uniform scale 5 — half the walkway/road
            // ground-tile scale (x10) — lands the 0.27 model height on
            // 1.35m, a believable suburban fence against the 8m houses.
            Assert.That(FenceTiling.Scale * FenceTiling.PieceModelHeight,
                Is.EqualTo(1.35f).Within(0.0001f));
        }

        [Test]
        public void PiecesAlong_CoverEachFenceRunExactly_WithoutGapsOrStretching()
        {
            var nominal = FenceTiling.PieceModelLength * FenceTiling.Scale;

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(lot);
                Assert.That(runs, Is.Not.Empty, $"lot {lot.HouseId} has no fence runs");

                foreach (var run in runs)
                {
                    var pieces = FenceTiling.PiecesAlong(run);
                    Assert.That(pieces, Is.Not.Empty, $"lot {lot.HouseId}: run has no pieces");

                    // Pieces compress to fit, never stretch past a piece.
                    foreach (var piece in pieces)
                    {
                        Assert.That(piece.LengthScale * FenceTiling.PieceModelLength,
                            Is.LessThanOrEqualTo(nominal + 0.0001f),
                            $"lot {lot.HouseId}: a fence piece is stretched past a full piece");
                    }

                    // Together the pieces span exactly the run's length.
                    var totalLength = pieces.Sum(p => p.LengthScale * FenceTiling.PieceModelLength);
                    Assert.That(totalLength, Is.EqualTo(run.Length).Within(0.001f),
                        $"lot {lot.HouseId}: pieces must cover the run exactly");

                    // Contiguous, end to end: first piece's near edge on A,
                    // last piece's far edge on B (the pieces have centered
                    // pivots), consecutive centers one piece length apart.
                    var pieceLength = pieces[0].LengthScale * FenceTiling.PieceModelLength;
                    AssertAtDistanceAlongRun(run, pieces[0].Position, pieceLength / 2f,
                        $"lot {lot.HouseId}: first piece must start at the run's A end");
                    AssertAtDistanceAlongRun(run, pieces[pieces.Count - 1].Position,
                        run.Length - pieceLength / 2f,
                        $"lot {lot.HouseId}: last piece must end at the run's B end");

                    for (var i = 0; i + 1 < pieces.Count; i++)
                    {
                        var dx = pieces[i + 1].Position.X - pieces[i].Position.X;
                        var dz = pieces[i + 1].Position.Z - pieces[i].Position.Z;
                        Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.EqualTo(pieceLength).Within(0.001f),
                            $"lot {lot.HouseId}: pieces {i} and {i + 1} are not contiguous");
                    }
                }
            }
        }

        [Test]
        public void PiecesAlong_YawPointsTheModelLengthAxisAlongTheRun()
        {
            // The fence model's length runs along model-local +X. Under
            // Unity's yaw convention (clockwise from above, +Z rotates
            // toward +X) local +X maps to world (cos yaw, -sin yaw) — that
            // must equal the run's A → B direction.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                foreach (var run in LotFence.RunsFor(lot))
                {
                    var dirX = (run.B.X - run.A.X) / run.Length;
                    var dirZ = (run.B.Z - run.A.Z) / run.Length;

                    foreach (var piece in FenceTiling.PiecesAlong(run))
                    {
                        var radians = piece.YawDegrees * Math.PI / 180.0;
                        Assert.That(Math.Cos(radians), Is.EqualTo(dirX).Within(0.001f),
                            $"lot {lot.HouseId}: piece yaw X component");
                        Assert.That(-Math.Sin(radians), Is.EqualTo(dirZ).Within(0.001f),
                            $"lot {lot.HouseId}: piece yaw Z component");
                    }
                }
            }
        }

        [Test]
        public void PiecesAlong_DegenerateRun_YieldsNoPieces()
        {
            var point = new GridPoint(1f, 2f);
            Assert.That(FenceTiling.PiecesAlong(new FenceRun(point, point)), Is.Empty);
        }

        private static void AssertAtDistanceAlongRun(FenceRun run, GridPoint point, float expectedDistance,
            string message)
        {
            var dirX = (run.B.X - run.A.X) / run.Length;
            var dirZ = (run.B.Z - run.A.Z) / run.Length;

            var alongDistance = (point.X - run.A.X) * dirX + (point.Z - run.A.Z) * dirZ;
            Assert.That(alongDistance, Is.EqualTo(expectedDistance).Within(0.001f), message + " (along)");

            var offAxis = (point.X - run.A.X) * dirZ - (point.Z - run.A.Z) * dirX;
            Assert.That(offAxis, Is.EqualTo(0f).Within(0.001f), message + " (must lie on the fence line)");
        }
    }
}
