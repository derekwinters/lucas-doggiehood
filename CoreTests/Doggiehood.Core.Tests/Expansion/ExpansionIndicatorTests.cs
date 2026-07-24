using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #178: the map-expansion lock indicator's full live state — where it
    /// hovers and whether it should tint affordable (gold) or not
    /// (grey/black) — resolved fresh off <see cref="GameState"/> every
    /// call (no caching), same pattern as the HUD currency chip reading
    /// Wallet.Coins live.
    /// </summary>
    public class ExpansionIndicatorTests
    {
        [Test]
        public void Resolve_OnAFreshGame_PointsAtTheFirstZonesEntrance_AndIsNotAffordable()
        {
            var state = GameState.CreateNew();

            var indicator = ExpansionIndicator.Resolve(state);

            Assert.That(indicator, Is.Not.Null);
            var expectedPosition = ExpansionIndicatorPlacement.Resolve(state.Map, ZoneCatalog.FirstZone);
            Assert.That(indicator.Value.Position.X, Is.EqualTo(expectedPosition.X));
            Assert.That(indicator.Value.Position.Z, Is.EqualTo(expectedPosition.Z));
            Assert.That(indicator.Value.IsAffordable, Is.False);
        }

        [Test]
        public void Resolve_BecomesAffordable_AsSoonAsTheWalletCoversTheNextZoneCost()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost - 1);
            Assert.That(ExpansionIndicator.Resolve(state).Value.IsAffordable, Is.False);

            state.Wallet.Deposit(1);

            Assert.That(ExpansionIndicator.Resolve(state).Value.IsAffordable, Is.True);
        }

        [Test]
        public void Resolve_ReturnsNull_WhenEveryAuthoredZoneIsAlreadyUnlocked()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone(); // unlocks the only authored zone so far

            var indicator = ExpansionIndicator.Resolve(state);

            Assert.That(indicator, Is.Null);
        }
    }
}
