using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #128: layout math for rendering a front walkway as tiled City Kit
    /// Suburban path pieces (path-short, a 0.2 x 0.2 model-local square
    /// paver). Lives in Core so the numbers are dotnet-testable;
    /// WorldBuilder instantiates these pieces verbatim, the same pattern
    /// as CatalogGalleryLayout. Pieces are scaled x10 in width — the same
    /// derivation as WorldBuilder.RoadTileScale: the model dimension lands
    /// exactly on a WorldDimensions value, here SidewalkWidth (2m), which
    /// is also the walkway edge's declared Width. Along the walkway,
    /// pieces shrink (never stretch past a paver) to cover the door →
    /// sidewalk segment exactly, with no gap at the door and no overshoot
    /// onto the kit road tile's raised sidewalk band.
    /// </summary>
    public class WalkwayTilingTests
    {
        private static WalkEdge WalkwayFor(int houseId)
        {
            Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(houseId, out var walkway), Is.True);
            return walkway;
        }

        [Test]
        public void WidthScale_MakesAPieceExactlyOneSidewalkWide()
        {
            Assert.That(WalkwayTiling.WidthScale * WalkwayTiling.PieceModelSize,
                Is.EqualTo(WorldDimensions.SidewalkWidth).Within(0.0001f));
        }

        [Test]
        public void PiecesAlong_CoverTheWalkwayExactly_FromDoorToSidewalk_WithoutGapsOrOverlap()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var walkway = WalkwayFor(lot.HouseId);
                var pieces = WalkwayTiling.PiecesAlong(walkway);

                Assert.That(pieces, Is.Not.Empty, $"house {lot.HouseId} walkway has no pieces");

                // No piece may exceed a full paver's world size (pieces
                // shrink to fit, never stretch past the square).
                var nominal = WalkwayTiling.PieceModelSize * WalkwayTiling.WidthScale;
                foreach (var piece in pieces)
                {
                    Assert.That(piece.LengthScale * WalkwayTiling.PieceModelSize,
                        Is.LessThanOrEqualTo(nominal + 0.0001f),
                        $"house {lot.HouseId}: a piece is stretched past a full paver");
                }

                // Together the pieces span exactly the walkway's length.
                var totalLength = pieces.Sum(p => p.LengthScale * WalkwayTiling.PieceModelSize);
                Assert.That(totalLength, Is.EqualTo(walkway.Length).Within(0.001f),
                    $"house {lot.HouseId}: pieces must cover the walkway exactly");

                // Contiguous run from the door (A) to the sidewalk (B):
                // the first piece's near edge sits on A, the last piece's
                // far edge on B, and consecutive centers are spaced by one
                // piece length.
                var pieceLength = pieces[0].LengthScale * WalkwayTiling.PieceModelSize;
                AssertAtDistanceAlongWalkway(walkway, pieces[0].Position, pieceLength / 2f,
                    $"house {lot.HouseId}: first piece must start at the door");
                AssertAtDistanceAlongWalkway(walkway, pieces[pieces.Count - 1].Position,
                    walkway.Length - pieceLength / 2f,
                    $"house {lot.HouseId}: last piece must end at the sidewalk");

                for (var i = 0; i + 1 < pieces.Count; i++)
                {
                    var dx = pieces[i + 1].Position.X - pieces[i].Position.X;
                    var dz = pieces[i + 1].Position.Z - pieces[i].Position.Z;
                    Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.EqualTo(pieceLength).Within(0.001f),
                        $"house {lot.HouseId}: pieces {i} and {i + 1} are not contiguous");
                }
            }
        }

        [Test]
        public void PiecesAlong_YawPointsDownTheWalkway()
        {
            // A piece's local Z (its length axis) must run along the
            // walkway: the unit vector (sin yaw, cos yaw) — Unity's yaw
            // convention on the ground plane — equals the door → sidewalk
            // direction.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var walkway = WalkwayFor(lot.HouseId);
                var dirX = (walkway.B.X - walkway.A.X) / walkway.Length;
                var dirZ = (walkway.B.Z - walkway.A.Z) / walkway.Length;

                foreach (var piece in WalkwayTiling.PiecesAlong(walkway))
                {
                    var radians = piece.YawDegrees * Math.PI / 180.0;
                    Assert.That(Math.Sin(radians), Is.EqualTo(dirX).Within(0.001f),
                        $"house {lot.HouseId}: piece yaw X component");
                    Assert.That(Math.Cos(radians), Is.EqualTo(dirZ).Within(0.001f),
                        $"house {lot.HouseId}: piece yaw Z component");
                }
            }
        }

        private static void AssertAtDistanceAlongWalkway(WalkEdge walkway, GridPoint point, float expectedDistance,
            string message)
        {
            var dirX = (walkway.B.X - walkway.A.X) / walkway.Length;
            var dirZ = (walkway.B.Z - walkway.A.Z) / walkway.Length;

            var alongDistance = (point.X - walkway.A.X) * dirX + (point.Z - walkway.A.Z) * dirZ;
            Assert.That(alongDistance, Is.EqualTo(expectedDistance).Within(0.001f), message + " (along)");

            var offAxis = (point.X - walkway.A.X) * dirZ - (point.Z - walkway.A.Z) * dirX;
            Assert.That(offAxis, Is.EqualTo(0f).Within(0.001f), message + " (must lie on the walkway line)");
        }
    }
}
