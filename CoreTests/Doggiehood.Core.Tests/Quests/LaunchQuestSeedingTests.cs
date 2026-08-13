using System;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #316: the single launch-time quest-seeding decision the thin Unity
    /// bootstrap defers to. Pre-chain (still on the first guided step) seeds
    /// the one tutorial quest; mid-chain (the guided upgrade/expand/build
    /// steps) stays suppressed; post-chain runs the #310 recurring refresh.
    /// </summary>
    public class LaunchQuestSeedingTests
    {
        private static readonly DateTime NowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void PreChain_FirstStep_SeedsExactlyTheOneTutorialQuest()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());

                state.Quests.EnsureQuestsForLaunch(NowUtc, new Random(seed));

                var active = state.Quests.ActiveQuests.ToList();
                Assert.That(active.Count, Is.EqualTo(1), $"seed {seed}: one tutorial quest");
                Assert.That(active[0].Type, Is.EqualTo(QuestType.LostItem), $"seed {seed}");
            }
        }

        [Test]
        public void MidChain_GuidedSteps_SeedNothing()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            state.GrantOnboardingCompletionReward(state.Houses[0].Id); // advance past the first step
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            state.Quests.EnsureQuestsForLaunch(NowUtc, new Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0),
                "the rotation stays suppressed while the guided chain is in progress");
        }

        [Test]
        public void PostChain_ImmediatelySeedsTheInitialBatch_ToTargetOnDistinctDogs()
        {
            // #579: the moment the reward chain completes at the build step, the
            // rotation is RELEASED with an immediate seed to the population
            // target — not left empty to trickle up "over the following hours".
            // No hour elapses and no further EnsureQuestsForLaunch call happens.
            for (var seed = 0; seed < 10; seed++)
            {
                var state = GameState.CreateNew();
                state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
                state.GrantOnboardingCompletionReward(state.Houses[0].Id);
                state.TryUpgradeHouse(state.Houses[0].Id);
                state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile);
                state.TryBuildHouse(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstLotId);
                Assert.That(state.RewardChain.IsComplete, Is.True, $"seed {seed}");

                var target = new QuestPacingPolicy().TargetActiveCount(state);
                var active = state.Quests.ActiveQuests.ToList();
                Assert.That(active.Count, Is.EqualTo(target),
                    $"seed {seed}: release seeds exactly the population target, not an empty board");
                Assert.That(active.Select(q => q.DogName).Distinct().Count(),
                    Is.EqualTo(active.Count), $"seed {seed}: every seeded quest is on a distinct dog");
            }
        }

        [Test]
        public void PostChain_NextHourlyLaunch_AddsNothingFurther_WithACleanHandoff()
        {
            // #579: after the immediate release seed hits the target, the next
            // hourly EnsureQuestsForLaunch (the recurring #543 trickle) adds
            // nothing — headroom is already met — and records a clean
            // LastRotationUtc handoff into the recurring rotation. No double-seed.
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            state.TryUpgradeHouse(state.Houses[0].Id);
            state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile);
            state.TryBuildHouse(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstLotId);
            var target = new QuestPacingPolicy().TargetActiveCount(state);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                "release already seeded the board to target");
            Assert.That(state.LastRotationUtc, Is.Null,
                "the release itself does not stamp the rotation clock");

            // Drive a full pacing window of subsequent hourly launches; the
            // recurring rotation continues from the seeded state without ever
            // exceeding the target (no double-seeding).
            for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
            {
                state.Quests.EnsureQuestsForLaunch(
                    NowUtc + TimeSpan.FromHours(hour), new Random(hour));
                Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                    $"hour {hour}: recurring rotation never exceeds the already-met target");
            }

            // #704: a board sitting at target is waiting for nothing, so the
            // recurring pacing tick starts no clock and stamps no rotation —
            // where it used to record a boundary that added nothing. The clock
            // starts the moment the player completes something and opens a slot.
            Assert.That(state.QuestRefreshTimerStartedUtc, Is.Null,
                "no wait runs against a full board");
            Assert.That(state.LastRotationUtc, Is.Null,
                "and no top-up was needed, so nothing stamped the rotation clock");
        }
    }
}
