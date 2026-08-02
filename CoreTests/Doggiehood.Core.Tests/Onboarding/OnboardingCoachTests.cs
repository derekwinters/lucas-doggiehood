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

        [Test]
        public void ShouldShow_IsFalse_WhenACenteredPanelIsOpen_EvenMidRewardChain()
        {
            // #506: during the Upgrade step the player must open HouseProfileOverlay
            // (a centered modal) and tap its footer Upgrade button — but the
            // bottom-anchored coach bar overlaps that button. Option 1 (approved):
            // suppress the coach bar while a centered modal panel is open, even
            // though the bar would otherwise be showing mid-reward-chain.
            Assert.That(
                OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse, centeredPanelOpen: false),
                Is.True, "with no panel open the bar shows for the Upgrade step");
            Assert.That(
                OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse, centeredPanelOpen: true),
                Is.False, "a centered modal panel suppresses the coach bar outright");
        }

        [Test]
        public void ShouldShow_ReturnsToItsPriorValue_OnceThePanelCloses()
        {
            // Suppression is not a step advance: closing the panel restores whatever
            // the bar would otherwise be showing for the current step.
            Assert.That(
                OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse, centeredPanelOpen: true),
                Is.False, "hidden while the panel is open");
            Assert.That(
                OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse, centeredPanelOpen: false),
                Is.True, "the same step's bar returns once the panel closes");
        }

        [Test]
        public void ShouldShow_StaysHidden_WhenTheChainCompletesWhileAPanelIsOpen_ThenThePanelCloses()
        {
            // Suppression composes with legitimate dismissal: if the reward chain
            // completes to Done while a panel is open, the bar is hidden — and it
            // must STAY hidden when the panel later closes (dismissal doesn't get
            // "un-suppressed" back on).
            Assert.That(
                OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.Done, centeredPanelOpen: true),
                Is.False, "hidden: both the chain is complete and a panel is open");
            Assert.That(
                OnboardingCoach.ShouldShow(OnboardingStep.Done, OnboardingRewardStep.Done, centeredPanelOpen: false),
                Is.False, "stays dismissed once the chain is complete, panel or not");
        }

        [Test]
        public void ShouldShow_DefaultsToNoPanelOpen_PreservingTheTwoArgBehavior()
        {
            // The panel-open argument is optional; the two-arg form (no panel) keeps
            // the pre-#506 decision so existing call sites are unchanged.
            Assert.That(OnboardingCoach.ShouldShow(OnboardingStep.Pan, OnboardingRewardStep.FirstQuest),
                Is.EqualTo(OnboardingCoach.ShouldShow(OnboardingStep.Pan, OnboardingRewardStep.FirstQuest, centeredPanelOpen: false)));
        }

        [Test]
        public void PhaseTitle_IsLearnTheRopes_ForEveryFirstQuestStep_SwappingOncePerPhaseNotPerStep()
        {
            // #451 / onboarding-overlay.md "Phase-title region": the tab names the
            // current onboarding PHASE, not step — all four tutorial steps show the
            // one Tutorial-phase title. The reward-chain state is FirstQuest
            // throughout the first-quest sequence.
            foreach (var step in new[]
            {
                OnboardingStep.Pan,
                OnboardingStep.Zoom,
                OnboardingStep.TapBubble,
                OnboardingStep.CompleteQuest,
            })
            {
                Assert.That(OnboardingCoach.PhaseTitle(step, OnboardingRewardStep.FirstQuest),
                    Is.EqualTo("Learn the ropes"), step + " is still the Tutorial phase");
                Assert.That(OnboardingCoach.PhaseTitle(step, OnboardingRewardStep.FirstQuest),
                    Is.EqualTo(OnboardingCoach.LearnTheRopesTitle));
            }
        }

        [Test]
        public void PhaseTitle_NamesEachRewardChainPhase_AfterTheFirstQuestSequenceIsDone()
        {
            // Once the first-quest sequence is Done the reward-chain step decides
            // the phase title — one per phase, per the approved spec table.
            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse),
                Is.EqualTo("Fix up a home"));
            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.ExpandMap),
                Is.EqualTo("Grow the neighborhood"));
            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.BuildHouse),
                Is.EqualTo("Build a house"));

            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.UpgradeHouse),
                Is.EqualTo(OnboardingCoach.FixUpAHomeTitle));
            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.ExpandMap),
                Is.EqualTo(OnboardingCoach.GrowTheNeighborhoodTitle));
            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.BuildHouse),
                Is.EqualTo(OnboardingCoach.BuildHouseTitle));
        }

        [Test]
        public void PhaseTitle_IsEmpty_OnceEverythingIsDone()
        {
            // No phase to name once both state machines are Done (the coach bar
            // is dismissed then anyway).
            Assert.That(OnboardingCoach.PhaseTitle(OnboardingStep.Done, OnboardingRewardStep.Done),
                Is.Empty);
        }
    }
}
