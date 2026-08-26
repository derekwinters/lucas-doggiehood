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
        public void ShouldRefresh_IsFalse_WhenNoWaitIsRunning()
        {
            // #704: the clock starts when the board drops below target, not at
            // "never rotated". A game with nothing waiting refreshes nothing.
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();

            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Null, "precondition: no wait is running");
            Assert.That(policy.ShouldRefresh(DateTime.UtcNow, state), Is.False);
        }

        [Test]
        public void ShouldRefresh_IsTrue_WhenIntervalHasElapsed()
        {
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();
            var start = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);
            state.RecordQuestRefreshTimerStart(start);

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
            state.RecordQuestRefreshTimerStart(start);

            var justUnder = start + EconomyNumbers.RefreshInterval - TimeSpan.FromMinutes(1);
            Assert.That(policy.ShouldRefresh(justUnder, state), Is.False);
        }

        [Test]
        public void IsBoardBelowTarget_IsWhatStartsAndStopsTheWait()
        {
            // #704: a full board is waiting for nothing, so no clock runs
            // against it — that is what stopped relaunches from banking
            // refreshes the player never waited for.
            var policy = new QuestPacingPolicy();
            var state = GameState.CreateNew();
            Assert.That(policy.IsBoardBelowTarget(state), Is.True, "an empty board is short");

            var rng = new Random(1);
            while (state.Quests.ActiveQuests.Count() < policy.TargetActiveCount(state))
            {
                state.Quests.GiveQuestTo(
                    state.Dogs.First(d => !d.HasActiveQuest), QuestType.LostItem, rng);
            }

            Assert.That(policy.IsBoardBelowTarget(state), Is.False, "a board at target is not");
        }

        [Test]
        public void RefreshInterval_IsTheTunedMinuteCadence()
        {
            // #543 replaced the 8h all-or-nothing batch with a recurring
            // trickle; #743 moved that cadence off whole hours, so the span is
            // built from the minutes value (QuestPacingWindowTests pins the
            // shipping 15-minute number itself).
            Assert.That(EconomyNumbers.RefreshInterval,
                Is.EqualTo(TimeSpan.FromMinutes(EconomyNumbers.RefreshIntervalMinutes)));
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
        public void PerRefreshRate_IsTargetOverThePacingWindow()
        {
            // #624/#743: perRefresh = target × interval / window — a sixteenth
            // of the target every 15 minutes over the 4h window, i.e. the same
            // 1.5 / 3.0 / 1.25 per HOUR as before, delivered in four slices.
            // Representative targets fall out of the population-scaled clamp
            // with the raised floor of 5: 8 dogs -> 5 (floor), 12 -> 5 (floor),
            // 18 -> 6, 100 -> 12 (ceiling).
            var policy = new QuestPacingPolicy();

            Assert.That(policy.PerRefreshRate(StateWithDogs(18)), Is.EqualTo(0.375).Within(1e-9), "target 6 -> 1.5/hr");
            Assert.That(policy.PerRefreshRate(StateWithDogs(100)), Is.EqualTo(0.75).Within(1e-9), "target 12 -> 3.0/hr");
            Assert.That(policy.PerRefreshRate(StateWithDogs(8)), Is.EqualTo(0.3125).Within(1e-9), "target 5 (floor) -> 1.25/hr");
            Assert.That(policy.PerRefreshRate(StateWithDogs(12)), Is.EqualTo(0.3125).Within(1e-9), "target 5 (floor) -> 1.25/hr");
        }

        [Test]
        public void AdvanceAccumulator_CarriesTheFractionalRemainder_AcrossBoundaries()
        {
            // #624/#743: a 1.25/hr target (target 5 floor over the 4h window) at
            // the 15-minute cadence adds 0.3125 per refresh. Most refreshes add
            // nothing; the carried fraction tips a whole quest in on every
            // fourth one and then, once the leftover has built up, on every
            // third — never a fractional quest, and exactly 5 over the window.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(8); // target 5 (floor) -> 0.3125 per refresh

            var acc = 0.0;
            var addedPerRefresh = new System.Collections.Generic.List<int>();
            for (var refresh = 0; refresh < EconomyNumbers.RefreshesPerPacingWindow; refresh++)
            {
                addedPerRefresh.Add(policy.AdvanceAccumulator(acc, state, out acc));
            }

            Assert.That(addedPerRefresh,
                Is.EqualTo(new[] { 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1 }),
                "the leftover fraction is carried forward, never dropped and never doubled");
            Assert.That(addedPerRefresh.Sum(), Is.EqualTo(5), "exactly the target over one pacing window");
        }

        [Test]
        public void AdvanceAccumulator_FractionalRate_NeverAddsAFractionalOrBursts()
        {
            // #624/#743: a 1.5/hr target (target 6) fills to target over the 4h
            // window via error diffusion — never a fractional quest and never a
            // burst above ceil(perRefresh) in a single refresh.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(18); // target 6 -> 0.375 per refresh

            var acc = 0.0;
            var addedPerRefresh = new System.Collections.Generic.List<int>();
            for (var refresh = 0; refresh < EconomyNumbers.RefreshesPerPacingWindow; refresh++)
            {
                addedPerRefresh.Add(policy.AdvanceAccumulator(acc, state, out acc));
            }

            Assert.That(addedPerRefresh,
                Has.All.LessThanOrEqualTo((int)Math.Ceiling(policy.PerRefreshRate(state))),
                "never a fractional or flood add");
            Assert.That(addedPerRefresh.Sum(), Is.EqualTo(6), "fills exactly to target over the pacing window");
        }

        [Test]
        public void AdvanceAccumulator_LongRunRate_ConvergesToTargetOverThePacingWindow()
        {
            // #624: over the long run the whole quests added equal the fractional
            // per-refresh rate times the refresh count — no drift, no lost
            // fraction, whatever the granularity.
            var policy = new QuestPacingPolicy();

            foreach (var dogCount in new[] { 8, 12, 18, 100 })
            {
                var state = StateWithDogs(dogCount);
                var rate = policy.PerRefreshRate(state);
                const int refreshes = 6000;

                var acc = 0.0;
                var total = 0;
                for (var refresh = 0; refresh < refreshes; refresh++)
                {
                    total += policy.AdvanceAccumulator(acc, state, out acc);
                }

                // Long-run total is within one whole quest of rate*refreshes.
                Assert.That(total, Is.EqualTo((int)Math.Round(rate * refreshes)).Within(1),
                    $"long-run rate for {dogCount} dogs converges to target/4 per window");
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
