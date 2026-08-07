using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #508: the City Kit Roads mesh each junction/terminus tile type renders
    /// at its centre, and the yaw that rotates that mesh's authored orientation
    /// onto the tile's declared <see cref="TileCatalog"/> edges. The authored
    /// (0-yaw) orientation of each staged piece: <c>road-intersection-path</c>
    /// omits its SOUTH arm (roads N/E/W == <see cref="TileType.TeeNorth"/>);
    /// <c>road-bend</c>'s raw kit source connects NORTH and WEST, but Unity's
    /// FBX import mirrors the X axis (W↔E), so the <em>imported</em> bend
    /// connects NORTH and EAST (== <see cref="TileType.TurnNE"/>) at 0-yaw (#515);
    /// <c>road-crossroad-path</c> is the symmetric 4-way. The cul-de-sac's
    /// <c>road-end-round</c> open road exits +X in the raw kit source but WEST
    /// once that same FBX import mirrors the X axis, so
    /// <see cref="TileType.CulDeSacEast"/> takes a half-turn to bring the open
    /// road back to its declared EAST edge (#514). These are pure
    /// data-table pins; the empirical mesh-geometry check that would catch a
    /// wrong reading lives in the EditMode <c>WorldKitArtTests</c> (it needs
    /// <c>UnityEngine.Mesh</c>). A wrong entry here renders a Tee/turn/cul-de-sac
    /// rotated off its neighbours, so the mapping is locked with a test
    /// (rule #6).
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

        [TestCase(TileType.TurnNE, 0f)]
        [TestCase(TileType.TurnSE, 90f)]
        [TestCase(TileType.TurnSW, 180f)]
        [TestCase(TileType.TurnNW, 270f)]
        public void Turns_AreTheBend_YawedSoBothArmsMeetTheCatalogEdges_WithNoCrosswalks(
            TileType type, float expectedYaw)
        {
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out var piece), Is.True);
            Assert.That(piece.ResourceKey, Is.EqualTo(RoadTileArt.BendKey));
            Assert.That(piece.YawDegrees, Is.EqualTo(expectedYaw));
            Assert.That(piece.HasBakedCrosswalks, Is.False, "a turn has no crossing, so no crosswalk stripes");
        }

        [TestCase(TileType.CulDeSacEast, 180f)]
        [TestCase(TileType.CulDeSacSouth, 270f)]
        [TestCase(TileType.CulDeSacWest, 0f)]
        [TestCase(TileType.CulDeSacNorth, 90f)]
        public void CulDeSacs_AreTheRoundEnd_YawedSoTheOpenRoadMeetsTheCatalogEdge_WithNoCrosswalks(
            TileType type, float expectedYaw)
        {
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out var piece), Is.True);
            Assert.That(piece.ResourceKey, Is.EqualTo(RoadTileArt.EndRoundKey));
            Assert.That(piece.YawDegrees, Is.EqualTo(expectedYaw));
            Assert.That(piece.HasBakedCrosswalks, Is.False);
        }

        [TestCase(TileType.StraightNS)]
        [TestCase(TileType.StraightEW)]
        public void Straights_HaveNoDedicatedCentrePiece(TileType type)
        {
            // A straight tile is just tiled road-straight arms — it resolves no
            // single centre mesh, so the kit path keeps tiling straight arms.
            Assert.That(RoadTileArt.TryGetCenterPiece(type, out _), Is.False);
        }
    }
}
