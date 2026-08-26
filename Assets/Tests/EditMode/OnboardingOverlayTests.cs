using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #207: the onboarding coach prompt reported as "no interactions, never
    /// dismisses". The Core <see cref="OnboardingSequence"/> is already
    /// correct (see CoreTests), so these guard the Unity wiring: the overlay
    /// advances from the real <see cref="CameraRig"/> pan/zoom and
    /// <see cref="ConversationPresenter"/> open signals, stops drawing once
    /// the sequence is Done, survives a null CameraRig (the WorldBootstrap
    /// case that used to silently skip wiring), and lays its bar out against
    /// the approved wireframe constants (docs/specs/ui/onboarding-overlay.md).
    /// </summary>
    public class OnboardingOverlayTests
    {
        private GameState state;
        private Doggiehood.Core.Dogs.Dog targetDog;
        private GameObject overlayHost;
        private GameObject presenterHost;
        private GameObject rigHost;
        private OnboardingOverlay overlay;
        private ConversationPresenter presenter;
        private CameraRig rig;

        [SetUp]
        public void CreateFixture()
        {
            state = GameState.CreateNew();
            // #543: quests trickle in hourly, so drive a full pacing window of
            // boundaries to fill the neighborhood and have an active quest to
            // target here.
            for (var refresh = 0; refresh < Doggiehood.Core.Economy.EconomyNumbers.RefreshesPerPacingWindow; refresh++)
            {
                state.Quests.StartNewDay(new System.Random(1 + refresh));
            }

            targetDog = state.Dogs.First(d => d.HasActiveQuest);

            presenterHost = new GameObject("presenter-host");
            presenter = presenterHost.AddComponent<ConversationPresenter>();
            presenter.State = state;

            rigHost = new GameObject("rig-host", typeof(Camera));
            rig = rigHost.AddComponent<CameraRig>();
            rig.ApplyConfiguration();

            overlayHost = new GameObject("overlay-host");
            overlay = overlayHost.AddComponent<OnboardingOverlay>();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(overlayHost);
            Object.DestroyImmediate(presenterHost);
            Object.DestroyImmediate(rigHost);
        }

        [Test]
        public void Update_AdvancesFromRealPanZoomAndConversationOpen_ThroughToDone()
        {
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Pan));
            Assert.That(overlay.ShouldDraw, Is.True);

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Zoom));

            rig.HandlePinch(60f, 1000f);
            overlay.Poll();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble));

            Assert.That(presenter.TryOpen(targetDog), Is.True);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest));

            // The overlay observes that the target dog's quest is gone.
            targetDog.ClearQuest();
            overlay.Poll();

            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(state.OnboardingComplete, Is.True);
        }

        [Test]
        public void OnGUI_KeepsDrawing_AfterTheFirstQuest_ForTheRewardChain()
        {
            // #371 supersedes the old #207 "auto-dismiss at Done" behavior: the
            // single coach bar (onboarding-overlay.md, #374) does NOT dismiss when
            // the first quest completes — it hands off to the reward-chain steps.
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.ShouldDraw, Is.True, "the coach prompt draws while onboarding runs");

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            rig.HandlePinch(60f, 1000f);
            overlay.Poll();
            presenter.TryOpen(targetDog);
            targetDog.ClearQuest();
            overlay.Poll();

            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(overlay.ShouldDraw, Is.True,
                "the coach stays up for the reward chain rather than dismissing at the first quest");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
        }

        [Test]
        public void Update_WithNullCameraRig_StillAdvancesToDone()
        {
            // #207: WorldBootstrap's FindFirstObjectByType<CameraRig>() can
            // return null; it used to silently skip wiring the overlay. With
            // no camera to pan/zoom, those steps can't be performed, so the
            // sequence must not deadlock on them — it still advances through
            // the conversation + quest-completion steps to Done.
            overlay.Init(state, null, presenter);

            Assert.DoesNotThrow(() => overlay.Poll());
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble),
                "with no rig, the pan/zoom steps are satisfied so onboarding can still progress");

            presenter.TryOpen(targetDog);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest));

            targetDog.ClearQuest();
            overlay.Poll();

            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            // #371: reaching Done now hands off to the reward chain rather than
            // dismissing, so the coach stays up for the upgrade step.
            Assert.That(overlay.ShouldDraw, Is.True);
            Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.UpgradeHousePrompt));
        }

        [Test]
        public void Poll_SelfHealsTapBubble_WhenTheConversationWasOpenedEarly()
        {
            // #329: the player taps the target dog's bubble during Pan/Zoom —
            // an early open fired through the presenter. The overlay must
            // remember it (not skip pan/zoom) and, once the flow reaches
            // TapBubble, self-heal rather than strand with "nothing to tap".
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Pan));

            Assert.That(presenter.TryOpen(targetDog), Is.True);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Pan),
                "an early open doesn't skip the pan/zoom teaching steps");

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            rig.HandlePinch(60f, 1000f);
            overlay.Poll();

            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest),
                "TapBubble self-heals because the bubble was already opened early");

            targetDog.ClearQuest();
            overlay.Poll();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done),
                "onboarding still reaches Done rather than stranding");
            Assert.That(state.OnboardingComplete, Is.True);
        }

        [Test]
        public void SuppressesBubbleForTargetDog_UntilTheTapBubbleStep()
        {
            // #329: the overlay gates the target dog's speech bubble to the
            // TapBubble step — DogView consults this so the bubble is first
            // tappable at step 3. Non-target dogs are never gated.
            overlay.Init(state, rig, presenter);

            var otherDog = state.Dogs.First(d => d != targetDog);
            Assert.That(overlay.SuppressesBubbleFor(targetDog), Is.True, "Pan: target dog gated");
            Assert.That(overlay.SuppressesBubbleFor(otherDog), Is.False, "non-target never gated");

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            Assert.That(overlay.SuppressesBubbleFor(targetDog), Is.True, "Zoom: still gated");

            rig.HandlePinch(60f, 1000f);
            overlay.Poll();
            Assert.That(overlay.SuppressesBubbleFor(targetDog), Is.False,
                "TapBubble: the bubble becomes tappable for the first time");
        }

        [Test]
        public void ConversationDeclineStaysReachable_DuringOnboarding()
        {
            // #329 regression guard (#185): no onboarding state may leave the
            // conversation dialog stuck open — the "Not now" decline always
            // dismisses it. The real reported symptom was a pinned coach bar,
            // not an un-closable dialog; this pins that invariant down.
            overlay.Init(state, rig, presenter);

            Assert.That(presenter.TryOpen(targetDog), Is.True);
            Assert.That(presenter.Current, Is.Not.Null, "the conversation opened");

            presenter.DeclineCurrent();
            Assert.That(presenter.Current, Is.Null, "the decline dismisses the dialog");
        }

        [Test]
        public void CompletedSave_SuppressesTheOverlayEntirely()
        {
            // #44/#207: a save with OnboardingComplete already true must never
            // show the coach prompt (ShouldRun is the gate WorldBootstrap uses).
            state.MarkOnboardingComplete();

            Assert.That(OnboardingSequence.ShouldRun(state), Is.False);
        }

        [Test]
        public void CoachRect_IsBottomCenter_AtTheContentSizedWireframeSize()
        {
            // docs/specs/ui/onboarding-overlay.md (#176/#451): CoachWidthPx 900,
            // content-sized height 88 + 48 + 16 = 152, CoachBottomMarginPx 56,
            // anchored bottom-center at the 1920x1200 reference.
            var rect = OnboardingOverlay.ComputeCoachRect(1920f, 1200f);

            Assert.That(rect.width, Is.EqualTo(900f).Within(0.01f));
            Assert.That(rect.height, Is.EqualTo(152f).Within(0.01f));
            Assert.That(rect.x, Is.EqualTo((1920f - 900f) / 2f).Within(0.01f), "centered horizontally");
            Assert.That(rect.y, Is.EqualTo(1200f - 152f - 56f).Within(0.01f), "bottom margin above the screen edge");
        }

        [Test]
        public void CoachBar_AppliesTheSharedCandyCottageBaseline()
        {
            // #297 restyle: the graybox GUI.Box becomes the Candy Cottage coach
            // bar, styled to the shared baseline (shared-components.md #65) —
            // thick ink outline, hard straight-down shadow, full pill radius.
            Assert.That(OnboardingOverlay.OutlineThicknessPx, Is.EqualTo(6f), "shared OutlineThicknessPx");
            Assert.That(OnboardingOverlay.ShadowOffsetPx, Is.EqualTo(8f), "shared ShadowOffsetPx");
            Assert.That(OnboardingOverlay.PillRadiusPx, Is.EqualTo(999f), "shared PillRadiusPx (full pill)");
        }

        [Test]
        public void CoachBar_LayoutConstants_MatchTheMockup()
        {
            // #297 / mockups/onboarding-overlay.html: padding 0 34px, gap 22px,
            // a 52px leaf paw badge (5px ink ring). No inline geometry literals
            // (#161). The trailing step dots are gone (#451).
            Assert.That(OnboardingOverlay.CoachPadXPx, Is.EqualTo(34f), "coach content x-padding");
            Assert.That(OnboardingOverlay.CoachGapPx, Is.EqualTo(22f), "gap between regions");
            Assert.That(OnboardingOverlay.PawDiameterPx, Is.EqualTo(52f), "leaf paw badge diameter");
            Assert.That(OnboardingOverlay.PawOutlineThicknessPx, Is.EqualTo(5f), "paw badge ink ring");
        }

        [Test]
        public void PhaseTitleTab_LayoutConstants_MatchTheSpecTable()
        {
            // #451 / onboarding-overlay.md "Phase-title region" table. No inline
            // geometry literals (#161).
            Assert.That(OnboardingOverlay.PhaseTitleLeftInsetPx, Is.EqualTo(34f), "tab inset from bar's left edge");
            Assert.That(OnboardingOverlay.PhaseTitleOffsetPx, Is.EqualTo(28f), "overlap above the bar's top edge");
            Assert.That(OnboardingOverlay.PhaseTitlePaddingXPx, Is.EqualTo(30f), "tab horizontal label inset");
            Assert.That(OnboardingOverlay.PhaseTitlePaddingYPx, Is.EqualTo(8f), "tab vertical label inset");
            Assert.That(OnboardingOverlay.PhaseTitleFontPx, Is.EqualTo(26), "tab label size");
            Assert.That(OnboardingOverlay.PhaseTitleContentTopPaddingPx, Is.EqualTo(48f), "content-row top padding");
            Assert.That(OnboardingOverlay.PhaseTitleContentBottomPaddingPx, Is.EqualTo(16f), "content-row bottom padding");
        }

        [Test]
        public void CoachBar_Colors_MatchTheFixedCandyCottagePalette()
        {
            // shared-components.md palette: Cream #FFF3D9, Ink #2E2A26, Leaf #58C06A.
            AssertHex(OnboardingOverlay.CreamColor, 0xFF, 0xF3, 0xD9, "Cream fill");
            AssertHex(OnboardingOverlay.InkColor, 0x2E, 0x2A, 0x26, "Ink outline/shadow/text");
            AssertHex(OnboardingOverlay.LeafColor, 0x58, 0xC0, 0x6A, "Leaf paw badge");
        }

        [Test]
        public void LabelFont_IsTheBundledDejaVuSans_NotAnEditorOnlyBuiltin()
        {
            // #291: the runtime-drawn message text must use the bundled font,
            // never an editor-only built-in (which renders invisible on device).
            Assert.That(OnboardingOverlay.LabelFontResource, Is.EqualTo("DejaVuSans"));
        }

        [Test]
        public void MessageText_ShowsCurrentOnboardingStepText_WithDogSubstitution()
        {
            // The coach message is the current OnboardingSequence step's text;
            // the tap-bubble step substitutes the live target dog's name.
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Pan));
            Assert.That(overlay.MessageText, Does.Contain("look around the neighborhood"),
                "step 1 shows the pan guidance");

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            rig.HandlePinch(60f, 1000f);
            overlay.Poll();

            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble));
            Assert.That(overlay.MessageText, Does.Contain(targetDog.Name),
                "step 3 substitutes the live target dog's name");
            Assert.That(overlay.MessageText, Does.Contain("speech bubble"));
        }

        [Test]
        public void CoachBar_ReShowsForTheRewardChainSteps_ThenDismissesWhenTheChainCompletes()
        {
            // #371: the one standard coach bar (onboarding-overlay.md, #374) does
            // not dismiss when the first quest finishes — it re-shows the
            // reward-chain prompts (upgrade -> expand -> build), advancing as each
            // real action completes, and dismisses for good only at chain end.
            overlay.Init(state, rig, presenter);

            DriveFirstQuestSequenceToDone();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done));

            // Completing the first quest granted reward-chain step 1, so the chain
            // now waits on UpgradeHouse and the coach re-shows that prompt.
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
            Assert.That(overlay.ShouldDraw, Is.True, "coach stays up for the upgrade step");
            Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.UpgradeHousePrompt));

            // #469: the Upgrade step is scoped to the first-quest dog's own
            // house, so upgrade THAT house (targetDog.HouseId) — not a hardcoded
            // Houses[0], which the #543 trickle no longer guarantees is the
            // onboarding dog's house.
            Assert.That(state.TryUpgradeHouse(targetDog.HouseId), Is.True);
            Assert.That(overlay.ShouldDraw, Is.True, "coach stays up for the expand step");
            Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.ExpandMapPrompt));

            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
            Assert.That(overlay.ShouldDraw, Is.True, "coach stays up for the build step");
            Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.BuildHousePrompt));

            var lot = state.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile)[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);
            Assert.That(state.RewardChain.IsComplete, Is.True);
            Assert.That(overlay.ShouldDraw, Is.False,
                "the coach dismisses for good once the chain completes at build");
        }

        [Test]
        public void StepDots_AreFullyRemoved_NoDotMembersRemain()
        {
            // #451: the trailing step-dots region is DROPPED, not hidden — the
            // dot count froze at 4/4 through the reward chain and was misleading.
            // This regression guard fails if any dot member is reintroduced.
            var type = typeof(OnboardingOverlay);
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance;
            foreach (var name in new[]
            {
                "StepDotCount", "FilledDotCount", "DrawStepDots",
                "DotDiameterPx", "DotOutlineThicknessPx", "DotGapPx",
            })
            {
                Assert.That(type.GetMember(name, flags), Is.Empty,
                    name + " must be gone (#451 removed the step dots)");
            }
        }

        [Test]
        public void DesiredContentWidth_ReservesNoDotsColumn()
        {
            // #451: the content-width calc is left/right padding + paw badge +
            // gap + message, with NO trailing dots column reserved.
            const float messageWidth = 400f;
            var expected = 2f * OnboardingOverlay.CoachPadXPx + OnboardingOverlay.PawDiameterPx
                + OnboardingOverlay.CoachGapPx + messageWidth;
            Assert.That(OnboardingOverlay.DesiredContentWidthPx(messageWidth),
                Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void PhaseTitleTab_IsTopLeft_InsetAndOverlappingAboveTheTopEdge_LabeledFromCore()
        {
            // #451: the tab is inset PhaseTitleLeftInsetPx from the bar's left
            // edge, overlaps PhaseTitleOffsetPx above its top edge, and is sized
            // to the label plus horizontal/vertical paddings. Its label is the
            // current phase from the Core OnboardingCoach lookup.
            var coach = OnboardingOverlay.ComputeCoachRect(1920f, 1200f);
            const float labelWidth = 120f;
            var tab = OnboardingOverlay.ComputePhaseTitleRect(coach, labelWidth, 1f);

            Assert.That(tab.x, Is.EqualTo(coach.x + OnboardingOverlay.PhaseTitleLeftInsetPx).Within(0.01f),
                "inset from the bar's left edge");
            Assert.That(tab.y, Is.EqualTo(coach.y - OnboardingOverlay.PhaseTitleOffsetPx).Within(0.01f),
                "offset above the bar's top edge");
            Assert.That(tab.yMax, Is.GreaterThan(coach.y),
                "the tab overlaps down onto the bar's top edge");
            Assert.That(tab.width, Is.EqualTo(labelWidth + 2f * OnboardingOverlay.PhaseTitlePaddingXPx).Within(0.01f));
            Assert.That(tab.height, Is.EqualTo(OnboardingOverlay.PhaseTitleFontPx + 2f * OnboardingOverlay.PhaseTitlePaddingYPx).Within(0.01f));

            overlay.Init(state, rig, presenter);
            Assert.That(overlay.PhaseTitleText, Is.EqualTo(OnboardingCoach.PhaseTitle(
                overlay.CurrentStep, state.RewardChain.CurrentStep)),
                "the tab label is the Core phase-title lookup");
        }

        [Test]
        public void PhaseTitleText_SwapsOncePerPhase_NotPerStep()
        {
            // #451: "Learn the ropes" through every tutorial step, then one title
            // per reward-chain phase — the tab names the PHASE, not the step.
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.PhaseTitleText, Is.EqualTo(OnboardingCoach.LearnTheRopesTitle),
                "Pan: tutorial phase");

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            Assert.That(overlay.PhaseTitleText, Is.EqualTo(OnboardingCoach.LearnTheRopesTitle),
                "Zoom: same tutorial phase title");

            DriveFirstQuestSequenceToDone();
            Assert.That(overlay.PhaseTitleText, Is.EqualTo(OnboardingCoach.FixUpAHomeTitle),
                "upgrade phase");

            // #469: upgrade the first-quest dog's own house (the only one the
            // Upgrade step allows), not a hardcoded Houses[0].
            state.TryUpgradeHouse(targetDog.HouseId);
            Assert.That(overlay.PhaseTitleText, Is.EqualTo(OnboardingCoach.GrowTheNeighborhoodTitle),
                "expand phase");

            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);
            Assert.That(overlay.PhaseTitleText, Is.EqualTo(OnboardingCoach.BuildHouseTitle),
                "build phase");
        }

        [Test]
        public void CoachBar_SingleLineHeight_IsContentSized_AndGrowsUpward()
        {
            // #451: single-line height = CoachHeightPx + top + bottom padding
            // (88 + 48 + 16 = 152), bottom edge fixed at CoachBottomMarginPx so
            // the bar grows upward.
            var rect = OnboardingOverlay.ComputeCoachRect(1920f, 1200f);
            Assert.That(rect.height, Is.EqualTo(152f).Within(0.01f));
            Assert.That(rect.yMax, Is.EqualTo(1200f - 56f).Within(0.01f),
                "bottom edge fixed at CoachBottomMarginPx; the bar grows upward");
        }

        [Test]
        public void CoachBar_GrowToFit_UsesCoachWidthAsMinimum_AndWrapsAtTheMaxWidth()
        {
            // #369/#374 fold-in (Locked #374 constants): CoachWidthPx (900) is now
            // a MINIMUM; the bar grows to fit a wider message up to CoachMaxWidthPx
            // (1500), beyond which the message wraps. No inline literals (#161).
            Assert.That(OnboardingOverlay.CoachWidthPx, Is.EqualTo(900f), "minimum width");
            Assert.That(OnboardingOverlay.CoachMaxWidthPx, Is.EqualTo(1500f), "max width before wrap");

            Assert.That(OnboardingOverlay.ComputeCoachWidthPx(400f), Is.EqualTo(900f),
                "narrow content clamps up to the minimum");
            Assert.That(OnboardingOverlay.ComputeCoachWidthPx(1100f), Is.EqualTo(1100f),
                "mid content grows to fit");
            Assert.That(OnboardingOverlay.ComputeCoachWidthPx(2000f), Is.EqualTo(1500f),
                "over-max content clamps to the max");

            Assert.That(OnboardingOverlay.NeedsWrap(1100f), Is.False);
            Assert.That(OnboardingOverlay.NeedsWrap(2000f), Is.True);
        }

        [Test]
        public void CoachBar_GrowsInHeight_WhenTheMessageWraps()
        {
            var single = OnboardingOverlay.ComputeCoachRect(1920f, 1200f, 1100f);
            Assert.That(single.width, Is.EqualTo(1100f).Within(0.01f), "grown to fit the content");
            Assert.That(single.height, Is.EqualTo(152f).Within(0.01f),
                "one line: content-sized base height (88 + 48 + 16)");

            var wrapped = OnboardingOverlay.ComputeCoachRect(1920f, 1200f, 2000f);
            Assert.That(wrapped.width, Is.EqualTo(1500f).Within(0.01f), "clamped to the max width");
            Assert.That(wrapped.height, Is.GreaterThan(152f),
                "wrapping to a second line grows the bar in height instead of overflowing");
        }

        [Test]
        public void CoachBar_TwoLineWrap_GrowsTallerThan152_AndTheMessageTopClearsTheTab()
        {
            // #451 two-line-wrap case (the worst case: the tutorial step-1 message
            // wraps at CoachWidthPx). The content-sized bar grows taller than the
            // 152px single-line case, and the content row's top still clears the
            // phase-title tab's bottom edge — the clearance that drove the 48/16
            // paddings (#435 revision history).
            var single = OnboardingOverlay.ComputeCoachRect(1920f, 1200f, 900f);
            var wrapped = OnboardingOverlay.ComputeCoachRect(1920f, 1200f, 2000f);

            Assert.That(single.height, Is.EqualTo(152f).Within(0.01f), "single line: 152px");
            Assert.That(wrapped.height, Is.GreaterThan(152f), "the wrapped message grows the bar taller");
            Assert.That(wrapped.yMax, Is.EqualTo(single.yMax).Within(0.01f),
                "both anchored at the same bottom edge, growing upward");

            var tab = OnboardingOverlay.ComputePhaseTitleRect(wrapped, 120f, 1f);
            var contentTop = wrapped.y + OnboardingOverlay.PhaseTitleContentTopPaddingPx;
            Assert.That(contentTop, Is.GreaterThanOrEqualTo(tab.yMax),
                "the wrapped message's top edge clears the tab's bottom edge");
        }

        [Test]
        public void ShouldDraw_IsSuppressed_WhileTheHouseProfileIsOpen_DuringTheUpgradeStep()
        {
            // #506 (Option 1): during the Upgrade step the coach bar overlaps the
            // house profile's footer Upgrade button. The overlay observes an
            // "is a centered panel open" predicate wired from WorldBootstrap and
            // suppresses the bar while a centered modal panel is open, restoring it
            // on close (with the same step prompt — closing is not a step advance).
            var canvasHost = BuildCanvasHost();
            var houseHost = new GameObject("house-profile");
            houseHost.transform.SetParent(canvasHost.transform, false);
            var houseProfile = houseHost.AddComponent<HouseProfileOverlay>();
            houseProfile.Init();

            try
            {
                overlay.Init(state, rig, presenter, () => houseProfile.IsOpen);
                DriveFirstQuestSequenceToDone();
                Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
                Assert.That(overlay.ShouldDraw, Is.True,
                    "with no panel open the coach bar shows for the Upgrade step");

                houseProfile.Open(
                    new House(2, Quadrant.NorthWest, isVacant: false, level: 1),
                    new List<Dog> { new Dog("Biscuit", Breed.FrenchBulldog, Personality.Brave, 2, false) });
                Assert.That(houseProfile.IsOpen, Is.True, "the centered house profile is open");
                Assert.That(overlay.ShouldDraw, Is.False,
                    "the open house profile suppresses the coach bar so the Upgrade button is unobstructed");

                houseProfile.Close();
                Assert.That(overlay.ShouldDraw, Is.True,
                    "closing the profile brings the coach bar back");
                Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.UpgradeHousePrompt),
                    "the same Upgrade-step prompt as before — the panel did not advance the step");
            }
            finally
            {
                Object.DestroyImmediate(canvasHost);
            }
        }

        [Test]
        public void ShouldDraw_IsSuppressed_WhileTheDogProfileIsOpen_ProvingItGeneralizesBeyondHouses()
        {
            // #506: Option 1 is meant to generalize to every centered modal panel,
            // not special-case houses — the DogProfileOverlay (also center-anchored)
            // suppresses the coach bar the same way.
            var canvasHost = BuildCanvasHost();
            var dogHost = new GameObject("dog-profile");
            dogHost.transform.SetParent(canvasHost.transform, false);
            var dogProfile = dogHost.AddComponent<DogProfileOverlay>();
            dogProfile.Init();

            try
            {
                overlay.Init(state, rig, presenter, () => dogProfile.IsOpen);
                DriveFirstQuestSequenceToDone();
                Assert.That(overlay.ShouldDraw, Is.True, "coach bar shows for the Upgrade step");

                dogProfile.Open(new Dog("Bailey", Breed.GoldenRetriever, Personality.Brave, 2, false));
                Assert.That(dogProfile.IsOpen, Is.True);
                Assert.That(overlay.ShouldDraw, Is.False,
                    "an open dog profile suppresses the coach bar too — the fix is not house-specific");

                dogProfile.Close();
                Assert.That(overlay.ShouldDraw, Is.True, "closing the dog profile restores the coach bar");
            }
            finally
            {
                Object.DestroyImmediate(canvasHost);
            }
        }

        /// <summary>Builds a configured <see cref="UiCanvas"/> host for the centered
        /// profile overlays, importing the bundled label font first so a fresh CI
        /// Library resolves it before the overlays build (mirrors the profile
        /// overlay tests' setup, docs/engineering/unity-serialization.md §4).</summary>
        private static GameObject BuildCanvasHost()
        {
            AssetDatabase.ImportAsset("Assets/UI/Fonts/Resources/DejaVuSans.ttf",
                ImportAssetOptions.ForceSynchronousImport);
            var canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            return canvasHost;
        }

        /// <summary>Drives the first-quest sequence (pan -> zoom -> tap bubble ->
        /// complete) all the way to Done, which grants reward-chain step 1 and
        /// leaves the chain waiting on UpgradeHouse.</summary>
        private void DriveFirstQuestSequenceToDone()
        {
            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            rig.HandlePinch(60f, 1000f);
            overlay.Poll();
            presenter.TryOpen(targetDog);
            targetDog.ClearQuest();
            overlay.Poll();
        }

        [Test]
        public void GestureCoach_LayoutConstants_MatchTheApprovedWireframe()
        {
            // #330 gesture-arrow coach constants (docs/specs/ui/onboarding-overlay.md).
            // No inline geometry literals (#161).
            Assert.That(OnboardingOverlay.GestureCenterYPx, Is.EqualTo(480f), "vertical anchor");
            Assert.That(OnboardingOverlay.ArrowLengthPx, Is.EqualTo(200f), "arrow shaft + head");
            Assert.That(OnboardingOverlay.ArrowThicknessPx, Is.EqualTo(22f), "shaft width");
            Assert.That(OnboardingOverlay.ArrowHeadSizePx, Is.EqualTo(56f), "arrowhead span");
            Assert.That(OnboardingOverlay.ArrowOutlineThicknessPx, Is.EqualTo(6f), "ink outline");
            Assert.That(OnboardingOverlay.PanTravelPx, Is.EqualTo(260f), "pan sweep distance");
            Assert.That(OnboardingOverlay.ZoomNearOffsetPx, Is.EqualTo(70f), "zoom arrows closest");
            Assert.That(OnboardingOverlay.ZoomFarOffsetPx, Is.EqualTo(220f), "zoom arrows farthest");
            Assert.That(OnboardingOverlay.ArrowFillOpacity, Is.EqualTo(0.92f).Within(0.0001f), "fill opacity");
        }

        [Test]
        public void GestureFill_IsGold_DistinctFromTheCoachBarChrome()
        {
            // Decision 1 in the approved proposal: Gold fill, not Cream/Leaf, so
            // the gesture cue reads as an action prompt rather than decoration.
            AssertHex(OnboardingOverlay.GestureFillColor, 0xFF, 0xC2, 0x3C, "Gold gesture fill");
        }

        [Test]
        public void ComputePanArrowCenter_SweepsAlongEachAxis_FromTheGestureAnchor()
        {
            // At the 1920x1200 reference the scale is 1: anchor is screen-center-x
            // (960) at GestureCenterYPx (480); the arrow center sweeps +/- half of
            // PanTravelPx (260 -> +/-130) along its axis over the beat's progress.
            const float w = 1920f, h = 1200f;
            const float anchorX = w / 2f;      // 960
            const float anchorY = 480f;        // GestureCenterYPx at scale 1
            const float half = 260f / 2f;      // 130

            var lr0 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.LeftToRight, 0f);
            Assert.That(lr0.x, Is.EqualTo(anchorX - half).Within(0.01f));
            Assert.That(lr0.y, Is.EqualTo(anchorY).Within(0.01f));
            var lr1 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.LeftToRight, 1f);
            Assert.That(lr1.x, Is.EqualTo(anchorX + half).Within(0.01f));

            var rl0 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.RightToLeft, 0f);
            Assert.That(rl0.x, Is.EqualTo(anchorX + half).Within(0.01f), "right-to-left starts on the right");
            var rl1 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.RightToLeft, 1f);
            Assert.That(rl1.x, Is.EqualTo(anchorX - half).Within(0.01f));

            var ud0 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.UpToDown, 0f);
            Assert.That(ud0.x, Is.EqualTo(anchorX).Within(0.01f), "vertical beats keep x on the anchor");
            Assert.That(ud0.y, Is.EqualTo(anchorY - half).Within(0.01f), "up-to-down starts above");
            var ud1 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.UpToDown, 1f);
            Assert.That(ud1.y, Is.EqualTo(anchorY + half).Within(0.01f));

            var du0 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.DownToUp, 0f);
            Assert.That(du0.y, Is.EqualTo(anchorY + half).Within(0.01f), "down-to-up starts below");
            var du1 = OnboardingOverlay.ComputePanArrowCenter(w, h, GestureBeat.DownToUp, 1f);
            Assert.That(du1.y, Is.EqualTo(anchorY - half).Within(0.01f));
        }

        [Test]
        public void ComputeZoomArrowOffset_SpreadsOutOnZoomIn_AndClosesOnZoomOut()
        {
            const float h = 1200f; // scale 1 at the reference height
            Assert.That(OnboardingOverlay.ComputeZoomArrowOffsetPx(h, GestureBeat.ZoomIn, 0f),
                Is.EqualTo(70f).Within(0.01f), "zoom-in starts near the anchor");
            Assert.That(OnboardingOverlay.ComputeZoomArrowOffsetPx(h, GestureBeat.ZoomIn, 1f),
                Is.EqualTo(220f).Within(0.01f), "zoom-in spreads far apart");
            Assert.That(OnboardingOverlay.ComputeZoomArrowOffsetPx(h, GestureBeat.ZoomOut, 0f),
                Is.EqualTo(220f).Within(0.01f), "zoom-out starts far apart");
            Assert.That(OnboardingOverlay.ComputeZoomArrowOffsetPx(h, GestureBeat.ZoomOut, 1f),
                Is.EqualTo(70f).Within(0.01f), "zoom-out closes toward the anchor");
        }

        [Test]
        public void ComputeZoomArrowCenters_AreSymmetricAboutTheAnchor()
        {
            const float w = 1920f, h = 1200f;
            const float anchorX = w / 2f;
            const float anchorY = 480f;
            var pair = OnboardingOverlay.ComputeZoomArrowCenters(w, h, GestureBeat.ZoomIn, 1f);
            Assert.That(pair.Left.x, Is.EqualTo(anchorX - 220f).Within(0.01f), "left arrow at -far");
            Assert.That(pair.Right.x, Is.EqualTo(anchorX + 220f).Within(0.01f), "right arrow at +far");
            Assert.That(pair.Left.y, Is.EqualTo(anchorY).Within(0.01f));
            Assert.That(pair.Right.y, Is.EqualTo(anchorY).Within(0.01f));
        }

        [Test]
        public void ComputeArrowTip_IsHalfAShaftLengthFromCenter_AlongThePerDirectionAxis()
        {
            // #615: the procedural chevron head (and its ComputeChevronArmEnd /
            // ChevronHalfAngleDeg helpers) is gone — the arrow is now the tinted
            // Kenney sprite. ComputeArrowTip survives as the per-direction axis
            // geometry helper (mirroring ComputePanArrowCenter): the tip sits half
            // a shaft-length (ArrowLengthPx) from the center along the arrow's
            // rotated axis. Right (0 deg) points +x; left (180 deg) points -x.
            const float scale = 1f;
            const float half = 200f / 2f; // ArrowLengthPx / 2 at scale 1
            var center = new Vector2(600f, 480f);

            var right = OnboardingOverlay.ComputeArrowTip(center, 0f, scale);
            Assert.That(right.x, Is.EqualTo(center.x + half).Within(0.01f),
                "the canonical 0-degree arrow's tip is half a shaft-length to the right");
            Assert.That(right.y, Is.EqualTo(center.y).Within(0.01f));

            var left = OnboardingOverlay.ComputeArrowTip(center, 180f, scale);
            Assert.That(left.x, Is.EqualTo(center.x - half).Within(0.01f),
                "a 180-degree rotation flips the tip to the left of center");
            Assert.That(left.y, Is.EqualTo(center.y).Within(0.01f));
        }

        [Test]
        public void ChevronHead_HelpersAndConstants_AreFullyRemoved_ForTheKenneySpriteSwap()
        {
            // #615: the procedural shaft+chevron draw is replaced by the imported
            // Kenney arrow sprite, so its head-only helpers/constant are DROPPED,
            // not hidden. This regression guard fails if any are reintroduced.
            var type = typeof(OnboardingOverlay);
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance;
            foreach (var name in new[]
            {
                "ComputeChevronArmEnd", "DrawChevronArm", "DrawArrowStroke", "ChevronHalfAngleDeg",
            })
            {
                Assert.That(type.GetMember(name, flags), Is.Empty,
                    name + " must be gone (#615 swapped in the Kenney arrow sprite)");
            }
        }

        [Test]
        public void GestureClock_ResetsToZero_OnThePanToZoomStepChange()
        {
            // #468 secondary: gestureElapsed only reset when ShouldDrawGesture went
            // false — but it stays true across the Pan -> Zoom handoff (both are
            // gesture-eligible), so the Zoom beat loop used to start wherever Pan's
            // clock left off instead of at its first beat. The fix resets the clock
            // on ANY CurrentStep change. StepGestureClock is public so the reset is
            // observable without a running player loop.
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Pan));

            // Accumulate some Pan gesture time.
            overlay.StepGestureClock(0.5f);
            Assert.That(overlay.StepGestureClock(0.5f), Is.GreaterThan(0f),
                "the clock accumulates while the Pan arrows show");

            // A real pan advances Pan -> Zoom; the arrows keep showing across it.
            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Zoom));
            Assert.That(overlay.ShouldDrawGesture, Is.True,
                "the Zoom arrows show, so the old ShouldDrawGesture-false reset never fires");

            // The clock restarts at the Zoom step's first beat, not Pan's leftover.
            Assert.That(overlay.StepGestureClock(0f), Is.EqualTo(0f),
                "gestureElapsed resets to 0 on the Pan -> Zoom step change");
        }

        [Test]
        public void ShouldDrawGesture_OnlyDuringPanAndZoom_AndHidesTheInstantTheRealActionRegisters()
        {
            // #330: the arrow coach is scoped to the two movement steps and hides
            // the instant AdvanceCameraSteps registers the real pan/zoom — the same
            // real-action gate that advances the coach bar.
            overlay.Init(state, rig, presenter);
            Assert.That(overlay.ShouldDrawGesture, Is.True, "Pan: pan arrows show");

            rig.HandleDrag(120f, 0f, 1000f);
            overlay.Poll();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Zoom));
            Assert.That(overlay.ShouldDrawGesture, Is.True, "Zoom: zoom arrows show");

            rig.HandlePinch(60f, 1000f);
            overlay.Poll();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble));
            Assert.That(overlay.ShouldDrawGesture, Is.False,
                "the arrows vanish the instant the real zoom advances past the movement steps");
        }

        private static void AssertHex(Color color, byte r, byte g, byte b, string what)
        {
            var c32 = (Color32)color;
            Assert.That(c32.r, Is.EqualTo(r), what + " red channel");
            Assert.That(c32.g, Is.EqualTo(g), what + " green channel");
            Assert.That(c32.b, Is.EqualTo(b), what + " blue channel");
        }
    }
}
