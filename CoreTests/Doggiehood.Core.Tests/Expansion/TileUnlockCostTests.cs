using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #295: the per-tile unlock cost is a single named, swappable function
    /// returning a flat amount today (defaulting to the 100-coin base already
    /// used for the onboarding "expand the map" step). It takes the current
    /// placed-tile count so a future per-tile-count scaling (Derek's "+10 per
    /// existing tile") is a one-place change — but for now the count is ignored
    /// and the cost stays flat.
    /// </summary>
    public class TileUnlockCostTests
    {
        [Test]
        public void Cost_DefaultsToTheFlatBaseCost()
        {
            Assert.That(TileUnlock.Cost(placedTileCount: 1), Is.EqualTo(TileUnlockNumbers.BaseCost));
        }

        [Test]
        public void Cost_IsFlat_IndependentOfPlacedTileCount()
        {
            Assert.That(TileUnlock.Cost(placedTileCount: 1),
                Is.EqualTo(TileUnlock.Cost(placedTileCount: 50)),
                "cost is flat today — the tile count must not change it yet");
        }

        [Test]
        public void BaseCost_MatchesTheOnboardingExpandStepValue()
        {
            Assert.That(TileUnlockNumbers.BaseCost, Is.EqualTo(100));
        }
    }
}
