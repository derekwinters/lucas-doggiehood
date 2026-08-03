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
        public void PostChain_RunsTheRecurringRotation()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            state.TryUpgradeHouse(state.Houses[0].Id);
            state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile);
            state.TryBuildHouse(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstLotId);
            Assert.That(state.RewardChain.IsComplete, Is.True);

            // #543: the build step released the recurring rotation, which now
            // trickles quests in hourly rather than a 2-4 batch. Drive a full
            // pacing window of hourly launches (LastRotationUtc starts null, so
            // the first fires immediately, each subsequent one an hour later) and
            // confirm the rotation is active and fills up to the population
            // target.
            var target = new QuestPacingPolicy().TargetActiveCount(state);
            for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
            {
                state.Quests.EnsureQuestsForLaunch(
                    NowUtc + TimeSpan.FromHours(hour), new Random(hour));
            }

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                "the post-chain recurring rotation trickles up to the target");
        }
    }
}
