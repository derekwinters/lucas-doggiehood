using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #343: the actionable "unlock the next zone" offer resolved off
    /// <see cref="GameState"/> — the next zone's number, its
    /// <see cref="ZoneUnlock.CostForZoneNumber"/> cost, and whether the
    /// wallet can afford it right now. This is the single Core source the
    /// tap-to-unlock confirmation dialog reads for the cost it shows on
    /// Yes and for gating (a grey/unaffordable lock's tap is a no-op).
    /// Resolves to null once every authored zone is unlocked (nothing left
    /// to offer), mirroring <see cref="ExpansionIndicator.Resolve"/>.
    /// </summary>
    public class ZoneUnlockOfferTests
    {
        [Test]
        public void Resolve_OnAFreshGame_OffersTheFirstZone_AtItsBaseCost_Unaffordable()
        {
            var state = GameState.CreateNew();

            var offer = ZoneUnlockOffer.Resolve(state);

            Assert.That(offer, Is.Not.Null);
            Assert.That(offer.Value.ZoneNumber, Is.EqualTo(1));
            Assert.That(offer.Value.Cost, Is.EqualTo(ZoneUnlockNumbers.BaseCost));
            Assert.That(offer.Value.IsAffordable, Is.False,
                "a fresh wallet can't afford the first zone");
        }

        [Test]
        public void Resolve_BecomesAffordable_OnceTheWalletCoversTheCost()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost - 1);
            Assert.That(ZoneUnlockOffer.Resolve(state).Value.IsAffordable, Is.False);

            state.Wallet.Deposit(1);

            Assert.That(ZoneUnlockOffer.Resolve(state).Value.IsAffordable, Is.True);
        }

        [Test]
        public void Resolve_ReturnsNull_WhenEveryAuthoredZoneIsAlreadyUnlocked()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone(); // unlocks the only authored zone so far

            Assert.That(ZoneUnlockOffer.Resolve(state), Is.Null);
        }
    }
}
