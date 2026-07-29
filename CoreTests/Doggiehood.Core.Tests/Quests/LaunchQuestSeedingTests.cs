using System;
using System.Linq;
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
            state.GrantOnboardingCompletionReward(); // advance past the first step
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            state.Quests.EnsureQuestsForLaunch(NowUtc, new Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0),
                "the rotation stays suppressed while the guided chain is in progress");
        }

        [Test]
        public void PostChain_RunsTheRecurringRotation()
        {
            var state = GameState.CreateNew();
            state.GrantOnboardingCompletionReward();
            state.TryUpgradeHouse(state.Houses[0].Id);
            state.TryUnlockNextZone();
            state.TryBuildHouse(state.UnlockedZones[0].Lots[0].HouseId);
            Assert.That(state.RewardChain.IsComplete, Is.True);

            // The build step already released the first rotation; a later launch
            // under the 8h boundary is a no-op, and past it refreshes again.
            var activeAfterRelease = state.Quests.ActiveQuests.Count();
            Assert.That(activeAfterRelease, Is.InRange(2, 4));

            state.Quests.EnsureQuestsForLaunch(NowUtc, new Random(1));
            // #310 boundary: LastRotationUtc is still null right after the build
            // release, so this launch tops up toward the target once more.
            Assert.That(state.Quests.ActiveQuests.Count(), Is.InRange(2, 12));
        }
    }
}
