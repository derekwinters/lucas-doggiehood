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
            Assert.That(WorldDimensions.GrassVergeWidth, Is.EqualTo(1.5f));
            Assert.That(WorldDimensions.SidewalkWidth, Is.EqualTo(2f));
            Assert.That(WorldDimensions.CrosswalkWidth, Is.EqualTo(3f));
            Assert.That(WorldDimensions.CulDeSacBulbRadius, Is.EqualTo(9f));
            Assert.That(WorldDimensions.OpposingTurnArchRadius, Is.EqualTo(15f));
        }
    }
}
