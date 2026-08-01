using Doggiehood.Core.Tests.World;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #406: the actionable "build a house on this lot" offer resolved off
    /// <see cref="GameState"/> — the flat <see cref="HouseBuildNumbers.Cost"/>
    /// and whether the wallet can afford it right now. This is the single Core
    /// source the tap-to-build confirmation dialog reads for the cost it shows
    /// on Yes; the twin of the frontier tile-unlock offer for the build spend.
    /// Resolves to null when the lot isn't buildable (already has a house),
    /// so a non-buildable tap is a no-op that never opens the dialog.
    /// </summary>
    public class HouseBuildOfferTests
    {
        private static GameState WithUnlockedFirstZone()
        {
            // First frontier tile unlocked; the unlock spends the deposit, so
            // the wallet is 0 afterwards.
            return FrontierTestWorld.WithFirstTileUnlocked();
        }

        [Test]
        public void Resolve_ReturnsNull_ForALotThatAlreadyHasAHouse()
        {
            var state = WithUnlockedFirstZone();
            state.Wallet.Deposit(HouseBuildNumbers.Cost);
            var lotId = FrontierTestWorld.FirstLotId;
            state.TryBuildHouse(lotId); // the lot now carries a house

            Assert.That(state.IsLotBuildable(lotId), Is.False);
            Assert.That(HouseBuildOffer.Resolve(state, lotId), Is.Null,
                "a lot that already has a house offers no build");
        }

        [Test]
        public void Resolve_OnABuildableLot_ReportsTheFlatCost_Unaffordable_OnAnEmptyWallet()
        {
            var state = WithUnlockedFirstZone(); // wallet is 0 after the unlock
            var lotId = FrontierTestWorld.FirstLotId;

            var offer = HouseBuildOffer.Resolve(state, lotId);

            Assert.That(offer, Is.Not.Null);
            Assert.That(offer.Value.Cost, Is.EqualTo(HouseBuildNumbers.Cost));
            Assert.That(offer.Value.IsAffordable, Is.False,
                "an empty wallet can't afford the flat build cost");
        }

        [Test]
        public void Resolve_BecomesAffordable_OnceTheWalletCoversTheFlatCost()
        {
            var state = WithUnlockedFirstZone();
            var lotId = FrontierTestWorld.FirstLotId;

            state.Wallet.Deposit(HouseBuildNumbers.Cost - 1);
            Assert.That(HouseBuildOffer.Resolve(state, lotId).Value.IsAffordable, Is.False);

            state.Wallet.Deposit(1);
            var offer = HouseBuildOffer.Resolve(state, lotId);
            Assert.That(offer.Value.IsAffordable, Is.True);
            Assert.That(offer.Value.IsAffordable,
                Is.EqualTo(state.Wallet.CanAfford(HouseBuildNumbers.Cost)),
                "IsAffordable mirrors Wallet.CanAfford for the flat cost");
        }
    }
}
