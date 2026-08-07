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
        public void PacingWindowHours_IsANamedConstant_EqualToFour()
        {
            // #624 confirmation: the window the target is spread over is a
            // named, tunable EconomyNumbers constant (#161), not a magic 4.
            // #624 shortened it 6h -> 4h to lift the early quest rate.
            Assert.That(EconomyNumbers.PacingWindowHours, Is.EqualTo(4));
        }

        [Test]
        public void PerHourRate_IsTargetOverThePacingWindow()
        {
            // #624: perHour = target / 4.0. Representative targets fall out of
            // the population-scaled clamp with the raised floor of 5:
            // 8 dogs -> 5 (floor), 12 -> 5 (floor), 18 -> 6, 100 -> 12 (ceiling).
            var policy = new QuestPacingPolicy();

            Assert.That(policy.PerHourRate(StateWithDogs(18)), Is.EqualTo(1.5).Within(1e-9), "target 6 -> 1.5/hr");
            Assert.That(policy.PerHourRate(StateWithDogs(100)), Is.EqualTo(3.0).Within(1e-9), "target 12 -> 3.0/hr");
            Assert.That(policy.PerHourRate(StateWithDogs(8)), Is.EqualTo(1.25).Within(1e-9), "target 5 (floor) -> 1.25/hr");
            Assert.That(policy.PerHourRate(StateWithDogs(12)), Is.EqualTo(1.25).Within(1e-9), "target 5 (floor) -> 1.25/hr");
        }

        [Test]
        public void AdvanceAccumulator_CarriesTheFractionalRemainder_AcrossBoundaries()
        {
            // #624: a 1.25/hr target (target 5 floor over the 4h window) adds a
            // whole quest each hour and, on the fourth hour, the carried
            // 0.25 + 0.25 + 0.25 remainder tips a second whole quest in — never a
            // fractional quest, the leftover fraction is carried forward.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(8); // target 5 (floor) -> 1.25/hr

            var acc = 0.0;
            var addedPerHour = new System.Collections.Generic.List<int>();
            for (var hour = 0; hour < 4; hour++)
            {
                addedPerHour.Add(policy.AdvanceAccumulator(acc, state, out acc));
            }

            Assert.That(addedPerHour, Is.EqualTo(new[] { 1, 1, 1, 2 }),
                "1.25/hr trickles a quest each hour, the carried fraction adding a second on the fourth");
        }

        [Test]
        public void AdvanceAccumulator_FractionalRate_NeverAddsAFractionalOrBursts()
        {
            // #624: a 1.5/hr target (target 6) fills to target over the 4h window
            // via error diffusion — never a fractional quest and never a burst
            // above ceil(1.5)=2 in a single hour.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(18); // target 6 -> 1.5/hr

            var acc = 0.0;
            var addedPerHour = new System.Collections.Generic.List<int>();
            for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
            {
                addedPerHour.Add(policy.AdvanceAccumulator(acc, state, out acc));
            }

            Assert.That(addedPerHour, Has.All.LessThanOrEqualTo(2), "never a fractional or flood add");
            Assert.That(addedPerHour.Sum(), Is.EqualTo(6), "fills exactly to target over the pacing window");
        }

        [Test]
        public void AdvanceAccumulator_LongRunRate_ConvergesToTargetOverThePacingWindow()
        {
            // #624: over the long run the whole quests added per hour equal the
            // fractional rate target/4 — no drift, no lost fraction.
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
                    $"long-run rate for {dogCount} dogs converges to target/4");
            }
        }

        [Test]
        public void TargetActiveCount_IsPopulationScaled_ClampedToFloorAndCeiling()
        {
            var policy = new QuestPacingPolicy();

            // #624: clamp(round(dogCount / 3), 5, 12) — floor raised 3 -> 5.
            Assert.That(policy.TargetActiveCount(StateWithDogs(8)), Is.EqualTo(5), "floor: 8 dogs");
            Assert.That(policy.TargetActiveCount(StateWithDogs(18)), Is.EqualTo(6), "mid: 18 dogs");
            Assert.That(policy.TargetActiveCount(StateWithDogs(100)), Is.EqualTo(12), "ceiling: 100 dogs");
        }

        [Test]
        public void TargetActiveCount_ForAStartingNeighborhood_IsTheRaisedFloorOfFive()
        {
            // #624 checklist: a starting neighborhood sits below the divisor's
            // reach, so its target is the floor — raised 3 -> 5 here.
            var policy = new QuestPacingPolicy();
            var starting = GameState.CreateNew();

            Assert.That(policy.TargetActiveCount(starting), Is.EqualTo(5));
        }

        [Test]
        public void TargetActiveCount_UsesNamedEconomyConstants()
        {
            // #161: divisor, floor, ceiling are all named, not inline literals.
            Assert.That(EconomyNumbers.TargetActiveDivisor, Is.EqualTo(3));
            Assert.That(EconomyNumbers.TargetActiveFloor, Is.EqualTo(5));
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
