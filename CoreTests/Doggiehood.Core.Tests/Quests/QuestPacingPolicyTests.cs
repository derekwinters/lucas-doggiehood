using System;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #310: the pacing seam that owns both quest-pacing decisions — the
    /// refresh cadence (<see cref="QuestPacingPolicy.ShouldRefresh"/>) and the
    /// aggregate cap (<see cref="QuestPacingPolicy.TargetActiveCount"/>) — so
    /// they can evolve without touching the quest engine. Every tuning value
    /// is a named <see cref="EconomyNumbers"/> constant (#161), and the cadence
    /// is UTC-only (no device-local clock) to remove timezone spoofing.
    /// </summary>
    public class QuestPacingPolicyTests
    {
        private static GameState StateWithDogs(int dogCount)
        {
            var state = GameState.CreateNew();
            // CreateNew seeds the 8 roster dogs; top up (or note the floor
            // already covers < 8) so Dogs.Count reaches the requested size.
            for (var i = state.Dogs.Count; i < dogCount; i++)
            {
                state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
            }

            return state;
        }

        [Test]
        public void ShouldRefresh_IsTrue_WhenNeverRotated()
        {
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();

            Assert.That(state.LastRotationUtc, Is.Null, "precondition: no rotation recorded yet");
            Assert.That(policy.ShouldRefresh(DateTime.UtcNow, state), Is.True);
        }

        [Test]
        public void ShouldRefresh_IsTrue_WhenIntervalHasElapsed()
        {
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();
            var start = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);
            state.RecordRotationUtc(start);

            var atInterval = start + EconomyNumbers.RefreshInterval;
            Assert.That(policy.ShouldRefresh(atInterval, state), Is.True, "exactly at the interval refreshes");
            Assert.That(policy.ShouldRefresh(atInterval + TimeSpan.FromHours(1), state), Is.True);
        }

        [Test]
        public void ShouldRefresh_IsFalse_WhenUnderTheInterval()
        {
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();
            var start = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);
            state.RecordRotationUtc(start);

            var justUnder = start + EconomyNumbers.RefreshInterval - TimeSpan.FromMinutes(1);
            Assert.That(policy.ShouldRefresh(justUnder, state), Is.False);
        }

        [Test]
        public void RefreshInterval_IsEightHours()
        {
            // The approved cadence (#310, review session 2026-07-28): every 8h,
            // superseding the earlier once-per-day proposal.
            Assert.That(EconomyNumbers.RefreshInterval, Is.EqualTo(TimeSpan.FromHours(8)));
        }

        [Test]
        public void TargetActiveCount_IsPopulationScaled_ClampedToFloorAndCeiling()
        {
            var policy = new QuestPacingPolicy();

            // clamp(round(dogCount / 3), 3, 12).
            Assert.That(policy.TargetActiveCount(StateWithDogs(8)), Is.EqualTo(3), "floor: 8 dogs");
            Assert.That(policy.TargetActiveCount(StateWithDogs(18)), Is.EqualTo(6), "mid: 18 dogs");
            Assert.That(policy.TargetActiveCount(StateWithDogs(100)), Is.EqualTo(12), "ceiling: 100 dogs");
        }

        [Test]
        public void TargetActiveCount_UsesNamedEconomyConstants()
        {
            // #161: divisor, floor, ceiling are all named, not inline literals.
            Assert.That(EconomyNumbers.TargetActiveDivisor, Is.EqualTo(3));
            Assert.That(EconomyNumbers.TargetActiveFloor, Is.EqualTo(3));
            Assert.That(EconomyNumbers.TargetActiveCeiling, Is.EqualTo(12));
        }

        [Test]
        public void EligibleSubjectPool_DelegatesToThePopulationGate_OverTheLiveCatalog()
        {
            // #317: the pacing seam owns pool selection — it feeds the live
            // ItemCatalog and the neighborhood's population through the pure
            // QuestCostTiers gate, so difficulty scaling changes in one place.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(12);

            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Gift, state),
                Is.EqualTo(QuestCostTiers.EligibleNames(
                    ItemCatalog.Items, ItemEligibility.Gift, state.Dogs.Count)));
            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Decoration, state),
                Is.EqualTo(QuestCostTiers.EligibleNames(
                    ItemCatalog.Items, ItemEligibility.Decoration, state.Dogs.Count)));
        }

        [Test]
        public void EligibleSubjectPool_AtTheStartingPopulation_MatchesTodaysGiftAndDecorationPools()
        {
            // #317 checklist: early game is unchanged — every current
            // purchasable entry sits in the starter band (30-50 coins), so the
            // gated pool at the starting population equals the full tagged pool.
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();

            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Gift, state),
                Is.EquivalentTo(ItemCatalog.NamesEligibleFor(ItemEligibility.Gift)));
            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Decoration, state),
                Is.EquivalentTo(ItemCatalog.NamesEligibleFor(ItemEligibility.Decoration)));
        }
    }
}
