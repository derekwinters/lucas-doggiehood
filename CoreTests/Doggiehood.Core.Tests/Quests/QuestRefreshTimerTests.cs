using System;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #704: what starts the quest-refresh clock. It used to run against
    /// <see cref="GameState.LastRotationUtc"/> — always ticking, whether or not
    /// the board had room — and was only ever checked once, at app launch. It
    /// now starts the moment the board drops below the population-scaled
    /// target, is checked on a recurring basis while the app is open
    /// (<see cref="QuestManager.TickPacing"/>), and is persisted so waiting
    /// time is measured in elapsed time rather than in launches.
    /// </summary>
    public class QuestRefreshTimerTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);

        private static int Target(GameState state)
        {
            return new QuestPacingPolicy().TargetActiveCount(state);
        }

        /// <summary>A post-onboarding game whose board is full — the state the
        /// onboarding release leaves behind, and the steady state of play.</summary>
        private static GameState StateWithAFullBoard()
        {
            var state = GameState.CreateNew();
            state.MarkOnboardingComplete();
            state.RestoreRewardChainStep(Doggiehood.Core.Onboarding.OnboardingRewardStep.Done);
            var rng = new Random(1);
            while (state.Quests.ActiveQuests.Count() < Target(state))
            {
                var free = state.Dogs.First(d => !d.HasActiveQuest);
                state.Quests.GiveQuestTo(free, QuestType.PestControl, rng);
            }

            return state;
        }

        /// <summary>Completes one quest, opening a slot on the board.</summary>
        private static void CompleteOneQuest(GameState state)
        {
            var quest = state.Quests.ActiveQuests.First(q => q.Type == QuestType.PestControl);
            state.Quests.Accept(quest);
            Assert.That(state.Quests.SprayHouse(quest.TargetHouseId.Value), Is.True,
                "precondition: the quest completes");
        }

        [Test]
        public void NoTimerRuns_WhileTheBoardIsAtTarget()
        {
            var state = StateWithAFullBoard();

            state.Quests.TickPacing(T0, new Random(2));

            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Null,
                "a full board is not waiting on anything, so no clock runs");
            state.Quests.TickPacing(T0 + TimeSpan.FromDays(4), new Random(3));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(Target(state)),
                "and no amount of elapsed time pushes it past the cap");
        }

        [Test]
        public void TheTimerStarts_WhenTheBoardFirstDropsBelowTarget()
        {
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);

            state.Quests.TickPacing(T0, new Random(4));

            Assert.That(state.QuestRefreshTimerStartedUtc, Is.EqualTo(T0),
                "the clock starts the moment a slot opens");
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(Target(state) - 1),
                "starting the clock is not itself a refresh");
        }

        [Test]
        public void TheTopUp_FiresOneHourAfterTheTimerStarted_AndNotBefore()
        {
            // 8 dogs -> target 5 -> 1.25/hr, so an hour's worth of 15-minute
            // refreshes (#743) is what tips a whole quest in.
            var anHour = TimeSpan.FromHours(1);
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);
            var below = state.Quests.ActiveQuests.Count();
            state.Quests.TickPacing(T0, new Random(5));

            state.Quests.TickPacing(T0 + anHour - TimeSpan.FromMinutes(1), new Random(6));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(below), "not a minute early");

            state.Quests.TickPacing(T0 + anHour, new Random(7));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(below),
                "an hour after the slot opened, the trickle tops it up");
        }

        [Test]
        public void AwayFourDays_PaysOutMoreThanAwayOneInterval()
        {
            // #743 reverses this test's original ruling ("away 1 hour or 4 days
            // is one top-up"): elapsed time now decides how MANY intervals pay
            // out, not merely whether one does. Three open slots make the
            // difference visible — a single interval's trickle cannot refill
            // them, four days' worth can.
            var awayOneInterval = StateWithAFullBoard();
            var away4d = StateWithAFullBoard();
            foreach (var state in new[] { awayOneInterval, away4d })
            {
                CompleteOneQuest(state);
                CompleteOneQuest(state);
                CompleteOneQuest(state);
                state.Quests.TickPacing(T0, new Random(8));
            }

            awayOneInterval.Quests.TickPacing(T0 + EconomyNumbers.RefreshInterval, new Random(9));
            away4d.Quests.TickPacing(T0 + TimeSpan.FromDays(4), new Random(9));

            Assert.That(awayOneInterval.Quests.ActiveQuests.Count(),
                Is.LessThan(Target(awayOneInterval)),
                "precondition: one interval cannot refill three slots");
            Assert.That(away4d.Quests.ActiveQuests.Count(),
                Is.GreaterThan(awayOneInterval.Quests.ActiveQuests.Count()),
                "the time away counts — every interval due while closed pays out");
            Assert.That(away4d.Quests.ActiveQuests.Count(), Is.EqualTo(Target(away4d)),
                "and it stops at a full board");
        }

        [Test]
        public void TheTimerRestarts_WhenTheTopUpLeavesTheBoardStillBelowTarget()
        {
            // 8 dogs -> target 5 at 1.25/hr, so an hour's worth of trickle adds
            // a single quest and cannot refill a board with several slots open.
            var anHour = TimeSpan.FromHours(1);
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);
            CompleteOneQuest(state);
            CompleteOneQuest(state);
            state.Quests.TickPacing(T0, new Random(10));

            var firedAt = T0 + anHour;
            state.Quests.TickPacing(firedAt, new Random(11));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.LessThan(Target(state)),
                "precondition: an hour of trickle can't refill three slots");
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.EqualTo(firedAt),
                "the next wait is measured from the boundary this top-up consumed");
        }

        [Test]
        public void TheTimerClears_OnceTheBoardIsBackAtTarget()
        {
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);
            state.Quests.TickPacing(T0, new Random(12));
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Not.Null, "precondition: a clock is running");

            state.Quests.TickPacing(T0 + TimeSpan.FromHours(1), new Random(13));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(Target(state)),
                "precondition: the top-up refilled the single open slot");
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Null, "a full board stops the clock");
        }

        [Test]
        public void TheTimer_RoundTripsThroughSaveCodec()
        {
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);
            state.Quests.TickPacing(T0, new Random(14));

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.QuestRefreshTimerStartedUtc, Is.EqualTo(T0), "the wait is not restarted by a relaunch");
            Assert.That(reloaded.QuestRefreshTimerStartedUtc.Value.Kind, Is.EqualTo(DateTimeKind.Utc), "and stays UTC");
        }

        [Test]
        public void ARelaunchAddsNoQuestThatWaitingWouldNotHave()
        {
            // The other half of the durability invariant: relaunching is never a
            // shortcut. Half an hour into the wait, closing and reopening the
            // app must not produce a quest.
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);
            state.Quests.TickPacing(T0, new Random(15));
            var board = state.Quests.ActiveQuests.Count();

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));
            reloaded.Quests.TickPacing(T0 + TimeSpan.FromMinutes(30), new Random(16));

            Assert.That(reloaded.Quests.ActiveQuests.Count(), Is.EqualTo(board),
                "the relaunch does not shorten the hour");
        }

        [Test]
        public void LegacySave_WithNoTimerLine_SeedsTheTimerFromTheLastRotation()
        {
            // A pre-#704 save has a rotatedUtc= but no refresh timer, and its
            // board is empty (quests weren't persisted). Seeding the clock from
            // the last rotation means the player's time away still counts,
            // instead of the hour restarting at the upgrade.
            var state = GameState.CreateNew();
            state.MarkOnboardingComplete();
            state.RecordRotationUtc(T0);
            var legacy = string.Join("\n", SaveCodec.Save(state)
                .Split('\n')
                .Where(line => !line.StartsWith("questTimerUtc=")));

            var reloaded = SaveCodec.Load(legacy);

            Assert.That(reloaded.QuestRefreshTimerStartedUtc, Is.EqualTo(T0),
                "the old rotation stamp becomes the start of the wait");
            reloaded.Quests.TickPacing(T0 + TimeSpan.FromHours(1), new Random(17));
            Assert.That(reloaded.Quests.ActiveQuests.Count(), Is.GreaterThan(0),
                "so the first launch after the upgrade is not penalised an extra wait");
        }

        [Test]
        public void TickQuests_TopsUpDuringALongSession_WithoutARelaunch()
        {
            // #704 cause (c): the hourly boundary was only ever checked from
            // WorldBootstrap.Awake, so sitting in the game for hours produced no
            // new quests at all. This is the seam the app polls while open.
            var state = StateWithAFullBoard();
            CompleteOneQuest(state);
            var below = state.Quests.ActiveQuests.Count();

            state.Quests.TickQuests(T0, new Random(22));
            state.Quests.TickQuests(T0 + TimeSpan.FromHours(1), new Random(23));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(below),
                "an hour of play refills the slot, exactly as an hour away would");
        }

        [Test]
        public void TickQuests_StaysSuppressed_WhileTheOnboardingChainIsUnfinished()
        {
            // The recurring tick honours the same gate the launch path does:
            // the guided reward chain holds the rotation back until it completes.
            var state = GameState.CreateNew();
            Assert.That(state.RewardChain.IsComplete, Is.False, "precondition: mid-chain");

            state.Quests.TickQuests(T0, new Random(24));
            state.Quests.TickQuests(T0 + TimeSpan.FromDays(1), new Random(25));

            Assert.That(state.Quests.ActiveQuests, Is.Empty, "no rotation quests arrive during onboarding");
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Null, "and no clock is started behind its back");
        }

        [Test]
        public void TickPacing_ReportsWhetherItChangedAnything()
        {
            // The Unity layer polls this every few seconds; it saves only when
            // something actually moved, so the poll is not a disk write loop.
            var state = StateWithAFullBoard();

            Assert.That(state.Quests.TickPacing(T0, new Random(18)), Is.False,
                "a full board with no clock running changes nothing");

            CompleteOneQuest(state);
            Assert.That(state.Quests.TickPacing(T0, new Random(19)), Is.True, "starting the clock is a change");
            Assert.That(state.Quests.TickPacing(T0 + TimeSpan.FromMinutes(1), new Random(20)), Is.False,
                "waiting is not");
            Assert.That(state.Quests.TickPacing(T0 + EconomyNumbers.RefreshInterval, new Random(21)), Is.True,
                "and the boundary crossing is");
        }
    }
}
