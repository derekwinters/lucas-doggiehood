using Doggiehood.Core.Onboarding;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    /// <summary>
    /// #371: the pure guidance decision behind the single onboarding coach bar
    /// (docs/specs/ui/onboarding-overlay.md, "Standard onboarding coverage
    /// (#374)"). The one coach surface covers both the first-quest sequence and
    /// the follow-on reward chain (upgrade -> expand -> build); this Core logic
    /// decides whether it should still show and which reward-step prompt to
    /// display, so the thin Unity overlay only renders what it returns.
    /// </summary>
    public class OnboardingCoachTests
    {
        [Test]
        public void PromptForRewardStep_ReturnsTheApprovedCopy_ForTheThreeChainSteps()
        {
            // Copy is the accepted #374 default from the onboarding-overlay spec's
            // step table — not invented here.
            Assert.That(OnboardingCoach.PromptForRewardStep(OnboardingRewardStep.UpgradeHouse),
                Is.EqualTo("Tap a house, then Upgrade to make it even nicer!"));
            Assert.That(OnboardingCoach.PromptForRewardStep(OnboardingRewardStep.ExpandMap),
                Is.EqualTo("Tap the glowing lock to open up a new street!"));
            Assert.That(OnboardingCoach.PromptForRewardStep(OnboardingRewardStep.BuildHouse),
                Is.EqualTo("Tap the empty lot to build a new house!"));
        }

        [Test]
        public void PromptForRewardStep_IsEmpty_ForFirstQuestAndDone()
        {
            // FirstQuest is owned by the first-quest sequence's own four prompts,
            // and Done shows nothing — neither is a reward-chain guidance surface.
            Assert.That(OnboardingCoach.PromptForRewardStep(OnboardingRewardStep.FirstQuest),
                Is.Empty);
            Assert.That(OnboardingCoach.PromptForRewardStep(OnboardingRewardStep.Done),
                Is.Empty);
        }

        [Test]
        public void ShouldShow_StaysTrue_WhileTheFirstQuestSequenceRuns()
        {
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.Pan, OnboardingRewardStep.FirstQuest),
                Is.True);
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.CompleteQuest, OnboardingRewardStep.FirstQuest),
                Is.True);
        }

        [Test]
        public void ShouldShow_StaysTrue_AfterTheSequenceIsDone_UntilTheRewardChainCompletes()
        {
            // The dismissal gate is the reward chain, not the first-quest
            // sequence alone: once the sequence reaches Done the coach must keep
            // showing through upgrade -> expand -> build.
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse),
                Is.True);
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.ExpandMap),
                Is.True);
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.BuildHouse),
                Is.True);
        }

        [Test]
        public void ShouldShow_IsFalse_OnlyOnceBothTheSequenceAndTheRewardChainAreDone()
        {
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.Done),
                Is.False, "dismisses for good once the chain completes at build");
        }
    }
}
