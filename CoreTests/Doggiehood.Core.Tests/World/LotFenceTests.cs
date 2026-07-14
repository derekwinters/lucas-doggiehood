using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #129: per-lot fence boundary geometry — a rectangle around each
    /// house lot with a gate gap exactly where the lot's front walkway
    /// (#128) crosses it. Fencing is a per-lot on/off flag on
    /// <see cref="HouseLot"/> (all four starting lots fenced today) so a
    /// later design pass can make fences a buyable decoration or
    /// house-level upgrade without reshaping this geometry.
    /// </summary>
    public class LotFenceTests
    {
        [Test]
        public void HouseLots_AllHaveFencesEnabledByDefault()
        {
            // Derek's #129 request: all four starting lots render fenced so
            // the Editor check shows the feature everywhere. The flag (not
            // the constant-on state) is the mechanism a later buyable-fence
            // decision would flip.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(lot.HasFence, Is.True,
                    $"lot {lot.HouseId} should be fenced by default");
            }
        }

        [Test]
        public void HasFence_CanBeDisabledPerLot()
        {
            var lot = new HouseLot(1, Quadrant.NorthEast, new GridPoint(14f, 14f), hasFence: false);
            Assert.That(lot.HasFence, Is.False);
        }
    }
}
