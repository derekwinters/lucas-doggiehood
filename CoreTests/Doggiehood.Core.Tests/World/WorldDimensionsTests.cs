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
            // The grass verge was removed (0m) by Derek's decision
            // (2026-07-13, in conversation, superseding the original #106
            // 1.5m verge): with the City Kit Roads tiles (#121/#122) the
            // sidewalk abuts the road directly, putting Core's sidewalk
            // centerline at 4m — exactly on the kit tile's modeled raised
            // curb+sidewalk band (3-5m from the road centerline at tile
            // scale 10).
            Assert.That(WorldDimensions.GrassVergeWidth, Is.EqualTo(0f));
            Assert.That(WorldDimensions.SidewalkWidth, Is.EqualTo(2f));
            Assert.That(WorldDimensions.CrosswalkWidth, Is.EqualTo(3f));
            Assert.That(WorldDimensions.CulDeSacBulbRadius, Is.EqualTo(9f));
            Assert.That(WorldDimensions.OpposingTurnArchRadius, Is.EqualTo(15f));
        }
    }
}
