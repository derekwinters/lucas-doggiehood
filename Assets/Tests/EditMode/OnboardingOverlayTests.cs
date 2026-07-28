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
        public void OnGUI_StopsDrawing_OnceTheSequenceReachesDone()
        {
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
            Assert.That(overlay.ShouldDraw, Is.False,
                "the coach prompt must auto-dismiss once the first quest completes");
        }

        [Test]
        public void Update_WithNullCameraRig_StillAdvancesAndDismisses()
        {
            // #207: WorldBootstrap's FindFirstObjectByType<CameraRig>() can
            // return null; it used to silently skip wiring the overlay. With
            // no camera to pan/zoom, those steps can't be performed, so the
            // sequence must not deadlock on them — it still advances through
            // the conversation + quest-completion steps and dismisses.
            overlay.Init(state, null, presenter);

            Assert.DoesNotThrow(() => overlay.Poll());
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble),
                "with no rig, the pan/zoom steps are satisfied so onboarding can still progress");

            presenter.TryOpen(targetDog);
            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest));

            targetDog.ClearQuest();
            overlay.Poll();

            Assert.That(overlay.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(overlay.ShouldDraw, Is.False);
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
    }
}
