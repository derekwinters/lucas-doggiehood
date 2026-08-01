using System.Linq;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
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
            state.Quests.StartNewDay(new System.Random(1));
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
        public void CoachRect_IsBottomCenter_AtTheWireframeSize()
        {
            // docs/specs/ui/onboarding-overlay.md (#176): CoachWidthPx 900,
            // CoachHeightPx 88, CoachBottomMarginPx 56, anchored bottom-center
            // at the 1920x1200 reference.
            var rect = OnboardingOverlay.ComputeCoachRect(1920f, 1200f);

            Assert.That(rect.width, Is.EqualTo(900f).Within(0.01f));
            Assert.That(rect.height, Is.EqualTo(88f).Within(0.01f));
            Assert.That(rect.x, Is.EqualTo((1920f - 900f) / 2f).Within(0.01f), "centered horizontally");
            Assert.That(rect.y, Is.EqualTo(1200f - 88f - 56f).Within(0.01f), "bottom margin above the screen edge");
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
            // a 52px leaf paw badge (5px ink ring), and 16px dots (4px ink ring)
            // spaced 12px apart. No inline geometry literals (#161).
            Assert.That(OnboardingOverlay.CoachPadXPx, Is.EqualTo(34f), "coach content x-padding");
            Assert.That(OnboardingOverlay.CoachGapPx, Is.EqualTo(22f), "gap between regions");
            Assert.That(OnboardingOverlay.PawDiameterPx, Is.EqualTo(52f), "leaf paw badge diameter");
            Assert.That(OnboardingOverlay.PawOutlineThicknessPx, Is.EqualTo(5f), "paw badge ink ring");
            Assert.That(OnboardingOverlay.DotDiameterPx, Is.EqualTo(16f), "step dot diameter");
            Assert.That(OnboardingOverlay.DotOutlineThicknessPx, Is.EqualTo(4f), "step dot ink ring");
            Assert.That(OnboardingOverlay.DotGapPx, Is.EqualTo(12f), "gap between step dots");
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
        public void FilledDotCount_TracksTheCurrentStep()
        {
            // The trailing dots row fills up to and including the current step:
            // one dot per guided step out of StepDotCount.
            Assert.That(OnboardingOverlay.FilledDotCount(OnboardingStep.Pan), Is.EqualTo(1));
            Assert.That(OnboardingOverlay.FilledDotCount(OnboardingStep.Zoom), Is.EqualTo(2));
            Assert.That(OnboardingOverlay.FilledDotCount(OnboardingStep.TapBubble), Is.EqualTo(3));
            Assert.That(OnboardingOverlay.FilledDotCount(OnboardingStep.CompleteQuest), Is.EqualTo(4));
            Assert.That(OnboardingOverlay.FilledDotCount(OnboardingStep.Done), Is.EqualTo(4));
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

            Assert.That(state.TryUpgradeHouse(state.Houses[0].Id), Is.True);
            Assert.That(overlay.ShouldDraw, Is.True, "coach stays up for the expand step");
            Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.ExpandMapPrompt));

            Assert.That(state.TryUnlockNextZone(), Is.True);
            Assert.That(overlay.ShouldDraw, Is.True, "coach stays up for the build step");
            Assert.That(overlay.MessageText, Is.EqualTo(OnboardingCoach.BuildHousePrompt));

            var lot = state.UnlockedZones[0].Lots[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);
            Assert.That(state.RewardChain.IsComplete, Is.True);
            Assert.That(overlay.ShouldDraw, Is.False,
                "the coach dismisses for good once the chain completes at build");
        }

        [Test]
        public void StepDots_StayFrozenAtFourOfFour_DuringTheRewardChainSteps()
        {
            // #371 / onboarding-overlay.md: StepDotCount stays 4 and the dots
            // track only the first-launch four steps — during the reward chain
            // they stay filled at 4/4 (not advancing).
            Assert.That(OnboardingOverlay.StepDotCount, Is.EqualTo(4));

            overlay.Init(state, rig, presenter);
            DriveFirstQuestSequenceToDone();
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done));

            Assert.That(OnboardingOverlay.FilledDotCount(overlay.CurrentStep), Is.EqualTo(4),
                "dots are full at the first reward-chain step");
            state.TryUpgradeHouse(state.Houses[0].Id);
            Assert.That(OnboardingOverlay.FilledDotCount(overlay.CurrentStep), Is.EqualTo(4),
                "dots do not advance further through the reward chain");
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
            Assert.That(single.height, Is.EqualTo(88f).Within(0.01f), "one line: base height");

            var wrapped = OnboardingOverlay.ComputeCoachRect(1920f, 1200f, 2000f);
            Assert.That(wrapped.width, Is.EqualTo(1500f).Within(0.01f), "clamped to the max width");
            Assert.That(wrapped.height, Is.GreaterThan(88f),
                "wrapping to a second line grows the bar in height instead of overflowing");
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
