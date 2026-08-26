using System;
using System.Collections.Generic;
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
    /// #743: <b>the pacing window is the authority; the refresh interval is only
    /// granularity.</b> The board reaches its active-quest target one pacing
    /// window after it drops below target, whatever the refresh interval — the
    /// interval decides only how granular the trickle is.
    ///
    /// <para>Dropping the interval from an hour to 15 minutes therefore changes
    /// nothing about the total or the fill time: the same board fills in the
    /// same four hours, in 16 small steps instead of 4 big ones, and a busy
    /// neighborhood sees its first quest sooner.</para>
    /// </summary>
    public class QuestPacingWindowTests
    {
        /// <summary>Dog counts that land on the representative targets through
        /// <c>clamp(round(dogs / 3), 5, 12)</c>: the floor, a mid value, and the
        /// ceiling.</summary>
        private const int DogsForTargetFive = 8;
        private const int DogsForTargetSeven = 21;
        private const int DogsForTargetTwelve = 100;

        private static readonly DateTime T0 = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

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

        /// <summary>Walks the pure accumulator one refresh at a time and reports
        /// the cumulative whole quests delivered after each step.</summary>
        private static List<int> CumulativePerRefresh(GameState state, int refreshes)
        {
            var policy = new QuestPacingPolicy();
            var cumulative = new List<int>();
            var accumulator = 0d;
            var total = 0;
            for (var i = 0; i < refreshes; i++)
            {
                total += policy.AdvanceAccumulator(accumulator, state, out accumulator);
                cumulative.Add(total);
            }

            return cumulative;
        }

        [Test]
        public void TheShippingCadence_IsFifteenMinutes()
        {
            Assert.That(EconomyNumbers.RefreshIntervalMinutes, Is.EqualTo(15));
            Assert.That(EconomyNumbers.RefreshInterval, Is.EqualTo(TimeSpan.FromMinutes(15)));
            Assert.That(EconomyNumbers.PacingWindowHours, Is.EqualTo(4), "the window is unchanged");
            Assert.That(EconomyNumbers.RefreshesPerPacingWindow, Is.EqualTo(16));
        }

        [Test]
        public void TheFirstQuestArrivesSooner_ForABusierNeighborhood()
        {
            // #743's delivery-shape table: the first whole quest lands at
            // 60 / 45 / 30 minutes for targets 5 / 7 / 12. Only the floor target
            // is unchanged from the hourly cadence — a busy board no longer
            // waits a full hour for its first arrival.
            var expected = new Dictionary<int, int>
            {
                { DogsForTargetFive, 60 },
                { DogsForTargetSeven, 45 },
                { DogsForTargetTwelve, 30 },
            };

            foreach (var pair in expected)
            {
                var state = StateWithDogs(pair.Key);
                var cumulative = CumulativePerRefresh(state, EconomyNumbers.RefreshesPerPacingWindow);
                var firstRefreshThatDelivers = cumulative.FindIndex(c => c > 0) + 1;

                Assert.That(firstRefreshThatDelivers * EconomyNumbers.RefreshIntervalMinutes,
                    Is.EqualTo(pair.Value),
                    $"target {new QuestPacingPolicy().TargetActiveCount(state)}: first quest at {pair.Value} min");
            }
        }

        [Test]
        public void OnePacingWindowDeliversExactlyTheTarget_AtEveryInterval()
        {
            // The invariant: 15 min / 1h / 2h all reach the target in exactly
            // four hours — only the number of batches differs. The carried
            // fraction lands back on zero on the final step for every target.
            foreach (var intervalMinutes in new[] { 15, 60, 120 })
            {
                TuningConfig.Active.RefreshIntervalMinutes = intervalMinutes;

                foreach (var dogCount in new[] { DogsForTargetFive, DogsForTargetSeven, DogsForTargetTwelve })
                {
                    var state = StateWithDogs(dogCount);
                    var target = new QuestPacingPolicy().TargetActiveCount(state);
                    var refreshes = EconomyNumbers.RefreshesPerPacingWindow;
                    var cumulative = CumulativePerRefresh(state, refreshes);

                    Assert.That(refreshes * intervalMinutes,
                        Is.EqualTo(EconomyNumbers.PacingWindowHours * EconomyNumbers.MinutesPerHour),
                        $"{intervalMinutes} min divides the 4h window evenly");
                    Assert.That(cumulative[refreshes - 1], Is.EqualTo(target),
                        $"{intervalMinutes} min interval, target {target}: exactly the target after one window");
                }
            }
        }

        [Test]
        public void TheBoardReachesTarget_OnePacingWindowAfterItDropsBelow_AtEveryInterval()
        {
            // The invariant itself, at board level rather than in the pure
            // accumulator: whatever the interval, one pacing window after the
            // clock starts the board is exactly at target — and not before.
            foreach (var intervalMinutes in new[] { 15, 60, 120 })
            {
                TuningConfig.Active.RefreshIntervalMinutes = intervalMinutes;

                var state = StateWithDogs(DogsForTargetFive);
                var target = new QuestPacingPolicy().TargetActiveCount(state);
                var window = TimeSpan.FromHours(EconomyNumbers.PacingWindowHours);
                state.RecordQuestRefreshTimerStart(T0);

                var justShort = state.Quests.ActiveQuests.Count();
                state.Quests.TickPacing(T0 + window - EconomyNumbers.RefreshInterval, new Random(1));
                Assert.That(state.Quests.ActiveQuests.Count(), Is.LessThan(target),
                    $"{intervalMinutes} min: one interval short of the window is still short of target");

                state.Quests.TickPacing(T0 + window, new Random(2));
                Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                    $"{intervalMinutes} min: the window closes on a full board");
                Assert.That(justShort, Is.LessThan(target), "precondition: the board started short");
            }
        }

        [Test]
        public void HourlyMilestones_AreIdenticalToTheOldHourlyCadence()
        {
            // #743's second table: read at each whole hour, the 15-minute
            // cadence delivers exactly what the hourly one did — 1·2·3·5 at the
            // floor, 1·3·5·7 at target 7, 3·6·9·12 at the ceiling.
            var expected = new Dictionary<int, int[]>
            {
                { DogsForTargetFive, new[] { 1, 2, 3, 5 } },
                { DogsForTargetSeven, new[] { 1, 3, 5, 7 } },
                { DogsForTargetTwelve, new[] { 3, 6, 9, 12 } },
            };

            foreach (var pair in expected)
            {
                var state = StateWithDogs(pair.Key);
                var cumulative = CumulativePerRefresh(state, EconomyNumbers.RefreshesPerPacingWindow);
                var refreshesPerHour = EconomyNumbers.MinutesPerHour / EconomyNumbers.RefreshIntervalMinutes;

                var atEachHour = Enumerable.Range(1, EconomyNumbers.PacingWindowHours)
                    .Select(hour => cumulative[hour * refreshesPerHour - 1])
                    .ToArray();

                Assert.That(atEachHour, Is.EqualTo(pair.Value),
                    $"{pair.Key} dogs: hourly milestones unchanged by the finer cadence");
            }
        }

    }
}
