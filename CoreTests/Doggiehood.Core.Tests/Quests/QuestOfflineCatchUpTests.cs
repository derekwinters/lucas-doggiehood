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
    /// #743: time spent away from the game counts. Every refresh interval that
    /// elapsed while the app was closed pays out exactly once — none skipped
    /// because the game was not running, none paid twice — which deliberately
    /// reverses #704's "away 1 hour or 4 days is one top-up" rule.
    ///
    /// <para>The catch-up is bounded structurally, not by a max-offline
    /// constant: one pacing window's worth of refreshes accrues exactly the
    /// target, so away 5 hours, 5 days or 5 months all land on the same full
    /// board. And it is advanced in ONE step — <c>intervals × perRefresh</c>,
    /// floored once — never a loop, so a four-month absence costs the same as a
    /// four-hour one.</para>
    /// </summary>
    public class QuestOfflineCatchUpTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        private const int DogsForTargetFive = 8;
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

        /// <summary>An empty board whose refresh clock started at
        /// <see cref="T0"/> — the shape a player leaves behind when they close
        /// the app with slots open.</summary>
        private static GameState WaitingSince(int dogCount, DateTime startedUtc)
        {
            var state = StateWithDogs(dogCount);
            state.RecordQuestRefreshTimerStart(startedUtc);
            return state;
        }

        private static TimeSpan Intervals(int count)
        {
            return TimeSpan.FromTicks(EconomyNumbers.RefreshInterval.Ticks * count);
        }

        private static int Target(GameState state)
        {
            return new QuestPacingPolicy().TargetActiveCount(state);
        }

        [Test]
        public void ElapsedRefreshIntervals_CountsWholeIntervalsSinceTheClockStarted()
        {
            var policy = new QuestPacingPolicy();
            var state = WaitingSince(DogsForTargetFive, T0);

            Assert.That(policy.ElapsedRefreshIntervals(T0, state), Is.EqualTo(0));
            Assert.That(policy.ElapsedRefreshIntervals(T0 + Intervals(1) - TimeSpan.FromTicks(1), state), Is.EqualTo(0));
            Assert.That(policy.ElapsedRefreshIntervals(T0 + Intervals(1), state), Is.EqualTo(1));
            Assert.That(policy.ElapsedRefreshIntervals(T0 + Intervals(4), state), Is.EqualTo(4));

            var noClock = StateWithDogs(DogsForTargetFive);
            Assert.That(policy.ElapsedRefreshIntervals(T0, noClock), Is.EqualTo(0),
                "no clock is running, so nothing has elapsed");
        }

        [Test]
        public void NegativeElapsedTime_ClampsToZeroIntervals()
        {
            // Guard 1: a device clock moved backwards (or a hand-edited save)
            // puts nowUtc before the persisted start. A naive elapsed/interval
            // would go negative and SUBTRACT from the accumulator.
            var policy = new QuestPacingPolicy();
            var state = WaitingSince(DogsForTargetFive, T0 + TimeSpan.FromDays(2));

            Assert.That(policy.ElapsedRefreshIntervals(T0, state), Is.EqualTo(0));

            Assert.That(state.Quests.TickPacing(T0, new Random(1)), Is.False, "nothing to do");
            Assert.That(state.Quests.ActiveQuests, Is.Empty, "no quests appear");
            Assert.That(state.QuestPacingAccumulator, Is.EqualTo(0d), "and nothing is subtracted");
        }

        [Test]
        public void AdvancingNIntervalsInOneStep_EqualsNSeparateSingleSteps()
        {
            // The proof that the O(1) shortcut is sound: the carried fraction
            // telescopes, so floor-once over N intervals delivers exactly what N
            // separate floored steps deliver, for any starting fraction.
            var policy = new QuestPacingPolicy();

            foreach (var dogCount in new[] { DogsForTargetFive, 18, DogsForTargetTwelve })
            {
                var state = StateWithDogs(dogCount);

                foreach (var startingFraction in new[] { 0d, 0.1d, 0.25d, 0.5d, 0.7d, 0.75d, 0.99d })
                {
                    foreach (var intervals in new[] { 1, 2, 3, 4, 7, 16, 97 })
                    {
                        var stepwiseAccumulator = startingFraction;
                        var stepwiseTotal = 0;
                        for (var i = 0; i < intervals; i++)
                        {
                            stepwiseTotal += policy.AdvanceAccumulator(
                                stepwiseAccumulator, state, out stepwiseAccumulator);
                        }

                        var oneStepTotal = policy.AdvanceAccumulator(
                            startingFraction, intervals, state, out var oneStepAccumulator);

                        Assert.That(oneStepTotal, Is.EqualTo(stepwiseTotal),
                            $"{dogCount} dogs, from {startingFraction}, {intervals} intervals: same total");
                        Assert.That(oneStepAccumulator, Is.EqualTo(stepwiseAccumulator).Within(1e-9),
                            $"{dogCount} dogs, from {startingFraction}, {intervals} intervals: same carried fraction");
                        Assert.That(oneStepAccumulator, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
                    }
                }
            }
        }

        [Test]
        public void AwayLessThanOneInterval_PaysOutNothing()
        {
            var state = WaitingSince(DogsForTargetFive, T0);

            state.Quests.TickPacing(T0 + Intervals(1) - TimeSpan.FromMinutes(1), new Random(2));

            Assert.That(state.Quests.ActiveQuests, Is.Empty, "a partial interval is not yet due");
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.EqualTo(T0), "and the clock is left alone");
        }

        [Test]
        public void AwayAFullPacingWindow_FillsTheBoardExactlyToTarget()
        {
            // The headline reversal: an empty board left for one pacing window
            // comes back full, because every refresh due in that window paid out.
            var state = WaitingSince(DogsForTargetFive, T0);

            state.Quests.TickPacing(T0 + TimeSpan.FromHours(EconomyNumbers.PacingWindowHours), new Random(3));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(Target(state)),
                "one window away = every refresh in it = a full board");
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Null, "a full board stops the clock");
        }

        [Test]
        public void AwayFiveHoursOrFiveMonths_BothLandOnAFullBoard()
        {
            // The ceiling is structural, not a max-offline constant: the accrual
            // rate reaches the cap on its own after one pacing window, and the
            // per-batch caps hold from there.
            var fiveHours = WaitingSince(DogsForTargetFive, T0);
            var fiveMonths = WaitingSince(DogsForTargetFive, T0);

            fiveHours.Quests.TickPacing(T0 + TimeSpan.FromHours(5), new Random(4));
            fiveMonths.Quests.TickPacing(T0 + TimeSpan.FromDays(150), new Random(4));

            Assert.That(fiveHours.Quests.ActiveQuests.Count(), Is.EqualTo(Target(fiveHours)));
            Assert.That(fiveMonths.Quests.ActiveQuests.Count(), Is.EqualTo(Target(fiveMonths)),
                "no amount of extra absence adds anything beyond a full board");
        }

        [Test]
        public void APartialIntervalIsCarried_NotSwallowedAndNotDoublePaid()
        {
            // The clock re-anchors to the last fully-consumed boundary, not to
            // nowUtc. Returning half-way through an interval must not throw that
            // half away — the next payout is due half an interval later, not a
            // whole one.
            var state = WaitingSince(DogsForTargetTwelve, T0);
            var halfAnInterval = TimeSpan.FromTicks(EconomyNumbers.RefreshInterval.Ticks / 2);

            state.Quests.TickPacing(T0 + Intervals(1) + halfAnInterval, new Random(5));
            var afterFirst = state.Quests.ActiveQuests.Count();

            Assert.That(afterFirst, Is.GreaterThan(0), "precondition: the first interval paid out");
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.EqualTo(T0 + Intervals(1)),
                "the clock re-anchors to the boundary it consumed, keeping the half-interval");

            state.Quests.TickPacing(T0 + Intervals(2), new Random(6));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(afterFirst),
                "the carried half-interval means the second payout is due on schedule");
        }

        [Test]
        public void AnAbsurdlyLongAbsence_ComputesWithoutOverflowOrANonFiniteRate()
        {
            // Guard 3: a save left for centuries yields an enormous interval
            // count. The one-step advance means no loop, but the count itself
            // must not overflow or produce a non-finite accumulator. (A loop
            // implementation would not finish this test.)
            var state = WaitingSince(DogsForTargetFive, new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.DoesNotThrow(() => state.Quests.TickPacing(DateTime.MaxValue, new Random(7)));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(Target(state)),
                "the board is simply full");
            Assert.That(double.IsNaN(state.QuestPacingAccumulator), Is.False);
            Assert.That(double.IsInfinity(state.QuestPacingAccumulator), Is.False);
            Assert.That(state.QuestPacingAccumulator, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
        }

        [Test]
        public void CatchingUp_NeverExceedsTheTarget_NorDoubleBooksADog()
        {
            // #310's caps are untouched: catching up fills the board UP TO
            // TargetActiveCount and stops, and no dog ends up holding two.
            var state = WaitingSince(DogsForTargetTwelve, T0);

            state.Quests.TickPacing(T0 + TimeSpan.FromDays(30), new Random(8));

            var active = state.Quests.ActiveQuests.ToList();
            Assert.That(active.Count, Is.EqualTo(Target(state)), "exactly the target, never above");
            Assert.That(active.Select(q => q.DogName).Distinct().Count(), Is.EqualTo(active.Count),
                "no dog is double-booked");
            Assert.That(active.Any(q => q.Type == QuestType.LostItem || q.Type == QuestType.PestControl), Is.True,
                "#310's always-one-free-quest invariant survives a catch-up");
        }

        [Test]
        public void AnAbsenceDeliversTheSameBoard_AsStayingOpenThroughIt()
        {
            // Every elapsed interval pays out EXACTLY once: closing the app for
            // a pacing window and sitting in it for a pacing window land in the
            // same place — neither a skipped interval nor a double-paid one.
            var stayedOpen = WaitingSince(DogsForTargetTwelve, T0);
            var wasAway = WaitingSince(DogsForTargetTwelve, T0);

            for (var i = 1; i <= EconomyNumbers.RefreshesPerPacingWindow; i++)
            {
                stayedOpen.Quests.TickPacing(T0 + Intervals(i), new Random(9));
            }

            var reloaded = SaveCodec.Load(SaveCodec.Save(wasAway));
            reloaded.Quests.TickPacing(T0 + Intervals(EconomyNumbers.RefreshesPerPacingWindow), new Random(9));

            Assert.That(reloaded.Quests.ActiveQuests.Count(),
                Is.EqualTo(stayedOpen.Quests.ActiveQuests.Count()),
                "away for the window == open through the window");
            Assert.That(reloaded.QuestRefreshTimerStartedUtc,
                Is.EqualTo(stayedOpen.QuestRefreshTimerStartedUtc),
                "and the clock ends up in the same place");
        }

        [Test]
        public void ARelaunchMidIntervalStillAddsNothing_TheWaitIsNotShortened()
        {
            // The other half of the durability invariant, unchanged by #743:
            // relaunching is never a shortcut.
            var state = WaitingSince(DogsForTargetTwelve, T0);
            var halfAnInterval = TimeSpan.FromTicks(EconomyNumbers.RefreshInterval.Ticks / 2);

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));
            reloaded.Quests.TickPacing(T0 + halfAnInterval, new Random(10));

            Assert.That(reloaded.Quests.ActiveQuests, Is.Empty, "the relaunch does not shorten the interval");
        }

        [Test]
        public void ForceRefresh_LeavesAConsistentClock_AfterACatchUp()
        {
            // #457's debug "Refresh quests now" runs one top-up and re-anchors
            // the wait to that instant, so it can neither leave a stale clock
            // that fires again immediately nor bank a second catch-up.
            var state = WaitingSince(DogsForTargetTwelve, T0);
            var forcedAt = T0 + TimeSpan.FromDays(2);

            state.Quests.ForceRefresh(forcedAt, new Random(11));

            Assert.That(state.QuestRefreshTimerStartedUtc, Is.EqualTo(forcedAt),
                "the wait restarts from the forced refresh");
            var afterForce = state.Quests.ActiveQuests.Count();

            state.Quests.TickPacing(forcedAt + Intervals(1) - TimeSpan.FromMinutes(1), new Random(12));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(afterForce),
                "and no banked interval fires a moment later");
        }

        [Test]
        public void ANonDivisorInterval_RoundsTheFillMomentLater_NeverTheTotalHigher()
        {
            // Granularity may round the fill moment later, never the total
            // higher: a 3h interval in a 4h window has boundaries at 3h and 6h,
            // so the board is still short when the window closes and fills at
            // 6h instead — but never holds more than the target. Pinned rather
            // than special-cased: pro-rating a partial interval would pay quests
            // out at a moment no refresh actually fires, and the shipping
            // interval divides the window evenly anyway.
            const int threeHours = 180;
            TuningConfig.Active.RefreshIntervalMinutes = threeHours;

            var state = WaitingSince(DogsForTargetFive, T0);
            var target = Target(state);

            state.Quests.TickPacing(T0 + TimeSpan.FromHours(EconomyNumbers.PacingWindowHours), new Random(13));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.LessThan(target),
                "only one 3h boundary has passed when the 4h window closes");

            state.Quests.TickPacing(T0 + TimeSpan.FromHours(6), new Random(14));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                "the second boundary fills it — exactly the target, never above");
        }
    }
}
