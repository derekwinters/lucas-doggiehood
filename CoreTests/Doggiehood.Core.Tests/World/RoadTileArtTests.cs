using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #508: the City Kit Roads mesh each junction/terminus tile type renders
    /// at its centre, and the yaw that rotates that mesh's authored orientation
    /// onto the tile's declared <see cref="TileCatalog"/> edges. The authored
    /// (0-yaw) orientation of each staged piece was read from the kit OBJ
    /// vertices: <c>road-intersection-path</c> omits its SOUTH arm (roads N/E/W
    /// == <see cref="TileType.TeeNorth"/>); <c>road-bend</c> connects its NORTH
    /// and WEST edges (== <see cref="TileType.TurnNW"/>); <c>road-end-round</c>
    /// exits its EAST edge (== <see cref="TileType.CulDeSacEast"/>);
    /// <c>road-crossroad-path</c> is the symmetric 4-way. A wrong entry here
    /// renders a Tee/turn/cul-de-sac rotated off its neighbours, so the mapping
    /// is locked with a test (rule #6).
    /// </summary>
    public class RoadTileArtTests
    {
        [Test]
        public void FourWay_IsTheCrossroadPath_WithBakedCrosswalks()
        {
            Assert.That(RoadTileArt.TryGetCenterPiece(TileType.FourWay, out var piece), Is.True);
            Assert.That(piece.ResourceKey, Is.EqualTo(RoadTileArt.CrossroadPathKey));
            Assert.That(piece.YawDegrees, Is.EqualTo(0f));
            Assert.That(piece.HasBakedCrosswalks, Is.True);
        }

        [TestCase(TileType.TeeNorth, 0f)]
        [TestCase(TileType.TeeEast, 90f)]
        [TestCase(TileType.TeeSouth, 180f)]
        [TestCase(TileType.TeeWest, 270f)]
        public void Tees_AreTheIntersectionPath_YawedFromTheAuthoredTeeNorth_WithBakedCrosswalks(
            TileType type, float expectedYaw)
        {
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out var piece), Is.True);
            Assert.That(piece.ResourceKey, Is.EqualTo(RoadTileArt.IntersectionPathKey));
            Assert.That(piece.YawDegrees, Is.EqualTo(expectedYaw));
            Assert.That(piece.HasBakedCrosswalks, Is.True, "the 3-way mesh bakes its own crosswalk stripes");
        }

        [TestCase(TileType.TurnNW, 0f)]
        [TestCase(TileType.TurnNE, 90f)]
        [TestCase(TileType.TurnSE, 180f)]
        [TestCase(TileType.TurnSW, 270f)]
        public void Turns_AreTheBend_YawedFromTheAuthoredTurnNW_WithNoCrosswalks(
            TileType type, float expectedYaw)
        {
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out var piece), Is.True);
            Assert.That(piece.ResourceKey, Is.EqualTo(RoadTileArt.BendKey));
            Assert.That(piece.YawDegrees, Is.EqualTo(expectedYaw));
            Assert.That(piece.HasBakedCrosswalks, Is.False, "a turn has no crossing, so no crosswalk stripes");
        }

        [TestCase(TileType.CulDeSacEast, 0f)]
        [TestCase(TileType.CulDeSacSouth, 90f)]
        [TestCase(TileType.CulDeSacWest, 180f)]
        [TestCase(TileType.CulDeSacNorth, 270f)]
        public void CulDeSacs_AreTheRoundEnd_YawedFromTheAuthoredCulDeSacEast_WithNoCrosswalks(
            TileType type, float expectedYaw)
        {
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out var piece), Is.True);
            Assert.That(piece.ResourceKey, Is.EqualTo(RoadTileArt.EndRoundKey));
            Assert.That(piece.YawDegrees, Is.EqualTo(expectedYaw));
            Assert.That(piece.HasBakedCrosswalks, Is.False);
        }

        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        [TestCase(TileType.OpposingTurnsNS)]
        [TestCase(TileType.OpposingTurnsEW)]
        public void StraightAndOpposingTurns_HaveNoDedicatedCentrePiece(TileType type)
        {
            // A straight tile is just tiled road-straight arms; OpposingTurns is
            // deferred (#508 follow-up: compose two bends). Neither resolves a
            // single centre mesh, so the kit path keeps tiling straight arms.
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out _), Is.False);
        }
    }
}
