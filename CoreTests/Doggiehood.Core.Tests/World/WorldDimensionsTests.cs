using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class WorldDimensionsTests
    {
        [Test]
        public void LockedStandardDimensions_MatchTheDesignedValues()
        {
            // #105: the 7 locked standard-world-dimension constants, in
            // meters, that the tile catalog (docs/specs/world/tile-catalog.md)
            // and future rendering (#106, #109) are built from.
            Assert.That(WorldDimensions.TileSize, Is.EqualTo(60f));
            Assert.That(WorldDimensions.RoadWidth, Is.EqualTo(6f));
            // 0.75m: Derek's midpoint request (2026-07-13, in conversation,
            // Editor review). At 0m verge (his earlier same-day decision,
            // superseding the original #106 1.5m) the dogs walked at 4m —
            // "a little too close to the road"; at the original 1.5m they
            // walked at 5.5m. The midpoint puts the sidewalk centerline at
            // RoadWidth/2 + 0.75 + SidewalkWidth/2 = 4.75m — still inside
            // the kit tile's modeled 3-5m pavement band, near its outer
            // edge. This is a logical setback for dog placement; the kit
            // tiles render no grass strip (visuals unchanged).
            Assert.That(WorldDimensions.GrassVergeWidth, Is.EqualTo(0.75f));
            Assert.That(WorldDimensions.SidewalkWidth, Is.EqualTo(2f));
            Assert.That(WorldDimensions.CrosswalkWidth, Is.EqualTo(3f));
            Assert.That(WorldDimensions.CulDeSacBulbRadius, Is.EqualTo(9f));
            Assert.That(WorldDimensions.OpposingTurnArchRadius, Is.EqualTo(15f));
        }
    }
}
