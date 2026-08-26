using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #743: the honest "time until the next quest" number, shipped here as
    /// Core arithmetic and painted later by #683 — this issue renders nothing.
    ///
    /// <para>Most 15-minute refreshes add <b>zero</b> quests at a small target
    /// (11 of 16 at the floor). A countdown pointing at the next <em>refresh</em>
    /// would therefore hit zero four times an hour while a quest appeared once —
    /// a readout that lies. From the persisted accumulator the honest answer is
    /// <c>ceil((1 − accumulator) / perRefresh)</c> refreshes, times the
    /// interval.</para>
    /// </summary>
    public class TimeUntilNextQuestTests
    {
        private const int DogsForTargetFive = 8;
        private const int DogsForTargetSeven = 21;
        private const int DogsForTargetTwelve = 100;

        [TearDown]
        public void RestoreDefaults()
        {
            TuningConfig.ResetToDefaults();
        }

        private static GameState StateWithDogs(int dogCount)
        {
            var state = GameState.CreateNew();
            for (var i = state.Dogs.Count; i < dogCount; i++)
            {
                state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
            }

            return state;
        }

        private static GameState StateWithAFullBoard()
        {
            var state = GameState.CreateNew();
            var rng = new Random(1);
            var policy = new QuestPacingPolicy();
            while (state.Quests.ActiveQuests.Count() < policy.TargetActiveCount(state))
            {
                state.Quests.GiveQuestTo(state.Dogs.First(d => !d.HasActiveQuest), QuestType.PestControl, rng);
            }

            return state;
        }

        [Test]
        public void OnAnEmptyBoard_ItIsTheWaitForTheFirstWholeQuest()
        {
            // The #743 delivery-shape table, read as a countdown: a busier
            // neighborhood accrues faster, so it waits less.
            var policy = new QuestPacingPolicy();

            Assert.That(policy.TimeUntilNextQuest(StateWithDogs(DogsForTargetFive)),
                Is.EqualTo(TimeSpan.FromMinutes(60)), "target 5: 0.3125 per refresh -> 4 refreshes");
            Assert.That(policy.TimeUntilNextQuest(StateWithDogs(DogsForTargetSeven)),
                Is.EqualTo(TimeSpan.FromMinutes(45)), "target 7: 0.4375 per refresh -> 3 refreshes");
            Assert.That(policy.TimeUntilNextQuest(StateWithDogs(DogsForTargetTwelve)),
                Is.EqualTo(TimeSpan.FromMinutes(30)), "target 12: 0.75 per refresh -> 2 refreshes");
        }

        [Test]
        public void ItShortensAsQuietRefreshesBankTheirFraction()
        {
            // A run of refreshes that deliver nothing is not a stalled
            // countdown: each one banks its fraction, so the answer shrinks.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(DogsForTargetFive);

            var answers = new System.Collections.Generic.List<TimeSpan>();
            var accumulator = 0d;
            for (var refresh = 0; refresh < 4; refresh++)
            {
                answers.Add(policy.TimeUntilNextQuest(state).Value);
                policy.AdvanceAccumulator(accumulator, state, out accumulator);
                state.RecordQuestPacingAccumulator(accumulator);
            }

            Assert.That(answers, Is.EqualTo(new[]
            {
                TimeSpan.FromMinutes(60),
                TimeSpan.FromMinutes(45),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(15),
            }), "three quiet refreshes count down to the fourth, which delivers");
        }

        [Test]
        public void AnAccumulatorJustUnderOne_IsOneRefreshAway_NeverZero()
        {
            // The next refresh will deliver, so the answer is one interval — not
            // zero, and never a fraction of an interval: a quest can only arrive
            // on a boundary.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(DogsForTargetFive);
            state.RecordQuestPacingAccumulator(0.999d);

            Assert.That(policy.TimeUntilNextQuest(state), Is.EqualTo(EconomyNumbers.RefreshInterval));
        }

        [Test]
        public void OnAFullBoard_ThereIsNoAnswer()
        {
            // No clock runs against a board at target (#704), so nothing is
            // pending and there is nothing honest to display.
            var policy = new QuestPacingPolicy();
            var state = StateWithAFullBoard();

            Assert.That(policy.IsBoardBelowTarget(state), Is.False, "precondition: the board is full");
            Assert.That(policy.TimeUntilNextQuest(state), Is.Null);
        }

        [Test]
        public void ItIsAlwaysAWholeNumberOfRefreshIntervals()
        {
            var policy = new QuestPacingPolicy();

            foreach (var dogCount in new[] { DogsForTargetFive, 18, DogsForTargetSeven, DogsForTargetTwelve })
            {
                var state = StateWithDogs(dogCount);
                foreach (var accumulator in new[] { 0d, 0.1d, 0.5d, 0.9d, 0.999d })
                {
                    state.RecordQuestPacingAccumulator(accumulator);
                    var answer = policy.TimeUntilNextQuest(state).Value;

                    Assert.That(answer.Ticks % EconomyNumbers.RefreshInterval.Ticks, Is.EqualTo(0L),
                        $"{dogCount} dogs at {accumulator}: a whole number of intervals");
                    Assert.That(answer, Is.GreaterThanOrEqualTo(EconomyNumbers.RefreshInterval),
                        "never zero — a quest only ever arrives on a boundary");
                }
            }
        }

        [Test]
        public void TheQuestManagerExposesIt_SoTheHudNeedsNoCoreLogicOfItsOwn()
        {
            // #683 paints this and nothing else; the arithmetic stays in Core.
            var state = StateWithDogs(DogsForTargetTwelve);

            Assert.That(state.Quests.TimeUntilNextQuest,
                Is.EqualTo(new QuestPacingPolicy().TimeUntilNextQuest(state)));
            Assert.That(StateWithAFullBoard().Quests.TimeUntilNextQuest, Is.Null);
        }
    }
}
