using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Tests.World;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    /// <summary>
    /// #571: the onboarding "fix up a home" (upgrade a house) reward-chain step
    /// marks the target house with the existing red ground-ring highlight (#535)
    /// so it's obvious which house to tap and upgrade. These cover the
    /// Unity-independent decision — "should the highlight show, and on which
    /// house?" — mirroring <see cref="LostItemGlow.ShouldShow"/>'s shape, so the
    /// Unity view stays a thin apply-seam. No new Core state: the predicate reads
    /// only <see cref="OnboardingRewardChain.CurrentStep"/> and the already-stored
    /// <see cref="GameState.OnboardingUpgradeTargetHouseId"/> (#469).
    /// </summary>
    public class OnboardingHouseHighlightTests
    {
        [Test]
        public void ShouldShow_TrueOnlyDuringTheUpgradeStepWithARecordedTarget()
        {
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.UpgradeHouse, 5), Is.True,
                "the highlight is active while the chain waits on UpgradeHouse and a target house is recorded");
        }

        [Test]
        public void ShouldShow_FalseDuringTheUpgradeStepWithNoRecordedTarget()
        {
            // A legacy save mid-chain on UpgradeHouse carries no target (#469):
            // with nothing to point at, no highlight shows.
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.UpgradeHouse, null), Is.False,
                "no recorded target house means nothing to highlight");
        }

        [Test]
        public void ShouldShow_FalseForEveryOtherStep_IncludingOncePastTheUpgradeStep()
        {
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.FirstQuest, 5), Is.False,
                "before the upgrade step there is no highlight");
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.ExpandMap, 5), Is.False,
                "the highlight clears the instant the chain advances past UpgradeHouse");
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.BuildHouse, 5), Is.False);
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.Done, 5), Is.False,
                "a returning player past onboarding never gets the highlight");
        }

        [Test]
        public void TargetHouseId_IsExactlyTheStoredTarget_DuringTheUpgradeStep_NeverAnotherHouse()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;
            var other = state.Houses[1].Id;

            // Step 1 completes: records the target house and advances to UpgradeHouse.
            state.GrantOnboardingCompletionReward(target);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.EqualTo(target),
                "the highlight targets exactly the recorded first-quest dog's house");
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Not.EqualTo(other),
                "never another house, even if another is upgrade-eligible for unrelated reasons");
        }

        [Test]
        public void TargetHouseId_IsNullBeforeTheUpgradeStep()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.FirstQuest));
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Null,
                "no highlight before the upgrade step is reached");
        }

        [Test]
        public void TargetHouseId_IsNullOncePastTheUpgradeStep_EvenThoughTheTargetIdIsStillStored()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;

            state.GrantOnboardingCompletionReward(target);
            state.TryUpgradeHouse(target); // advances past UpgradeHouse -> ExpandMap

            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.ExpandMap));
            Assert.That(state.OnboardingUpgradeTargetHouseId, Is.EqualTo(target),
                "the stored target id persists past the step");
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Null,
                "but the highlight clears the moment the chain advances past UpgradeHouse");
        }
    }
}
