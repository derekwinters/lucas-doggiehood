using System;
using System.Linq;
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
        public void RefreshInterval_IsOneHour()
        {
            // #543: quests now trickle in hourly instead of an 8h batch — the
            // cadence is every hour, and the per-hour amount is derived from the
            // target spread over PacingWindowHours (see PerHourRate).
            Assert.That(EconomyNumbers.RefreshInterval, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(EconomyNumbers.RefreshIntervalHours, Is.EqualTo(1));
        }

        [Test]
        public void PacingWindowHours_IsANamedConstant_EqualToSix()
        {
            // #543 confirmation #1: the window the target is spread over is a
            // named, tunable EconomyNumbers constant (#161), not a magic 6.
            Assert.That(EconomyNumbers.PacingWindowHours, Is.EqualTo(6));
        }

        [Test]
        public void PerHourRate_IsTargetOverThePacingWindow()
        {
            // #543: perHour = target / 6.0. Representative targets fall out of
            // the population-scaled clamp: 8 dogs -> 3, 12 -> 4, 18 -> 6,
            // 100 -> 12 (ceiling).
            var policy = new QuestPacingPolicy();

            Assert.That(policy.PerHourRate(StateWithDogs(18)), Is.EqualTo(1.0).Within(1e-9), "target 6 -> 1.0/hr");
            Assert.That(policy.PerHourRate(StateWithDogs(100)), Is.EqualTo(2.0).Within(1e-9), "target 12 -> 2.0/hr");
            Assert.That(policy.PerHourRate(StateWithDogs(8)), Is.EqualTo(0.5).Within(1e-9), "target 3 -> 0.5/hr");
            Assert.That(policy.PerHourRate(StateWithDogs(12)), Is.EqualTo(4.0 / 6.0).Within(1e-9), "target 4 -> ~0.667/hr");
        }

        [Test]
        public void AdvanceAccumulator_CarriesTheFractionalRemainder_AcrossBoundaries()
        {
            // #543: a 0.5/hr target adds exactly 1 whole quest every other hour,
            // never a fractional quest — the leftover 0.5 is carried forward.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(8); // target 3 -> 0.5/hr

            var acc = 0.0;
            var addedPerHour = new System.Collections.Generic.List<int>();
            for (var hour = 0; hour < 6; hour++)
            {
                addedPerHour.Add(policy.AdvanceAccumulator(acc, state, out acc));
            }

            Assert.That(addedPerHour, Is.EqualTo(new[] { 0, 1, 0, 1, 0, 1 }),
                "0.5/hr trickles 1 quest every other hourly boundary");
        }

        [Test]
        public void AdvanceAccumulator_TwoThirdsRate_AddsRoughlyTwoQuestsEveryThreeHours()
        {
            // #543: a 0.667/hr target (target 4) adds roughly 2 whole quests per
            // 3 hours via error diffusion — never a fractional quest and never a
            // burst above ceil(0.667)=1 in a single hour. (Because target/6 is
            // not exactly representable in double, an individual 3-hour window is
            // "roughly 2", within one of 2; the long-run rate is exact — see
            // AdvanceAccumulator_LongRunRate_ConvergesToTargetOverSix.)
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(12); // target 4 -> 0.6667/hr

            var acc = 0.0;
            var addedPerHour = new System.Collections.Generic.List<int>();
            for (var hour = 0; hour < 3; hour++)
            {
                addedPerHour.Add(policy.AdvanceAccumulator(acc, state, out acc));
            }

            Assert.That(addedPerHour, Has.All.LessThanOrEqualTo(1), "never a fractional or burst add");
            Assert.That(addedPerHour.Sum(), Is.EqualTo(2).Within(1), "roughly 2 whole quests over 3 hours");
        }

        [Test]
        public void AdvanceAccumulator_LongRunRate_ConvergesToTargetOverSix()
        {
            // #543: over the long run the whole quests added per hour equal the
            // fractional rate target/6 — no drift, no lost fraction.
            var policy = new QuestPacingPolicy();

            foreach (var dogCount in new[] { 8, 12, 18, 100 })
            {
                var state = StateWithDogs(dogCount);
                var rate = policy.PerHourRate(state);
                const int hours = 6000;

                var acc = 0.0;
                var total = 0;
                for (var hour = 0; hour < hours; hour++)
                {
                    total += policy.AdvanceAccumulator(acc, state, out acc);
                }

                // Long-run total is within one whole quest of rate*hours.
                Assert.That(total, Is.EqualTo((int)Math.Round(rate * hours)).Within(1),
                    $"long-run rate for {dogCount} dogs converges to target/6");
            }
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
            // #317 checklist: early game is unchanged — every current STARTER
            // purchasable entry is offered at the starting population. #318's
            // 100-coin fence is the one Premium entry, so it is gated OUT here
            // (starting population is below the premium gate) — otherwise the
            // gated pool equals the full tagged pool.
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();

            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Gift, state),
                Is.EquivalentTo(ItemCatalog.NamesEligibleFor(ItemEligibility.Gift)
                    .Where(n => n != ItemCatalog.FenceItemName)));
            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Decoration, state),
                Is.EquivalentTo(ItemCatalog.NamesEligibleFor(ItemEligibility.Decoration)));
        }

        [Test]
        public void EligibleSubjectPool_GatesTheFence_ToThePremiumPopulation()
        {
            // #318: the 100-coin fence sits in the Premium tier, so it only
            // enters the Gift subject pool once the neighborhood reaches the
            // premium population gate (10 dogs) — its automatic later-game gate,
            // falling straight out of #317 with no bespoke threshold.
            var policy = new QuestPacingPolicy();

            var belowGate = StateWithDogs(QuestCostTiers.PremiumPopulationGate - 1);
            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Gift, belowGate),
                Does.Not.Contain(ItemCatalog.FenceItemName));

            var atGate = StateWithDogs(QuestCostTiers.PremiumPopulationGate);
            Assert.That(policy.EligibleSubjectPool(ItemEligibility.Gift, atGate),
                Contains.Item(ItemCatalog.FenceItemName));
        }
    }
}
