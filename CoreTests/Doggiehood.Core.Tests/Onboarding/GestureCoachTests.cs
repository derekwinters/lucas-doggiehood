using Doggiehood.Core.Onboarding;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    /// <summary>
    /// #330: the pure gesture-beat sequencer behind the animated onboarding
    /// arrow coach (docs/specs/ui/onboarding-overlay.md, "Gesture-arrow coach").
    /// It maps elapsed time + the current <see cref="OnboardingStep"/> to the
    /// active <see cref="GestureBeat"/> and 0-1 progress within
    /// <see cref="GestureCoach.BeatDurationSec"/>/<see cref="GestureCoach.BeatPauseSec"/>.
    /// Engine-free; the thin Unity overlay turns each beat + progress into arrow
    /// screen offsets and draws them.
    /// </summary>
    public class GestureCoachTests
    {
        private const float Tol = 0.0001f;

        [Test]
        public void PanStep_CyclesTheFourDirectionalBeats_InOrder()
        {
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, 0.0).Beat,
                Is.EqualTo(GestureBeat.LeftToRight));
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, GestureCoach.SlotDurationSec).Beat,
                Is.EqualTo(GestureBeat.RightToLeft));
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, 2 * GestureCoach.SlotDurationSec).Beat,
                Is.EqualTo(GestureBeat.UpToDown));
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, 3 * GestureCoach.SlotDurationSec).Beat,
                Is.EqualTo(GestureBeat.DownToUp));
        }

        [Test]
        public void PanStep_LoopsBackToTheFirstBeat_AfterFourBeats()
        {
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, 4 * GestureCoach.SlotDurationSec).Beat,
                Is.EqualTo(GestureBeat.LeftToRight));
        }

        [Test]
        public void ProgressRampsZeroToOne_DuringTheSweep_ThenHoldsAtOne_DuringThePause()
        {
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, 0.0).Progress,
                Is.EqualTo(0f).Within(Tol), "start of sweep");
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, GestureCoach.BeatDurationSec / 2f).Progress,
                Is.EqualTo(0.5f).Within(Tol), "mid sweep");
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan, GestureCoach.BeatDurationSec).Progress,
                Is.EqualTo(1f).Within(Tol), "end of sweep");
            // During the hold/pause the arrow stays parked at its end position.
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan,
                    GestureCoach.BeatDurationSec + GestureCoach.BeatPauseSec / 2f).Progress,
                Is.EqualTo(1f).Within(Tol), "held during the pause");
            // ...and is still the same (first) beat until the slot elapses.
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Pan,
                    GestureCoach.BeatDurationSec + GestureCoach.BeatPauseSec / 2f).Beat,
                Is.EqualTo(GestureBeat.LeftToRight));
        }

        [Test]
        public void ZoomStep_CyclesZoomInThenZoomOut_AndLoops()
        {
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Zoom, 0.0).Beat,
                Is.EqualTo(GestureBeat.ZoomIn));
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Zoom, GestureCoach.SlotDurationSec).Beat,
                Is.EqualTo(GestureBeat.ZoomOut));
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Zoom, 2 * GestureCoach.SlotDurationSec).Beat,
                Is.EqualTo(GestureBeat.ZoomIn), "loops back after two beats");
        }

        [Test]
        public void ZoomStep_ReportsSweepProgress_LikeThePanStep()
        {
            Assert.That(GestureCoach.BeatAt(OnboardingStep.Zoom, GestureCoach.BeatDurationSec / 2f).Progress,
                Is.EqualTo(0.5f).Within(Tol));
        }

        [Test]
        public void EveryOtherStep_IsHidden_WithNoProgress()
        {
            foreach (var step in new[] { OnboardingStep.TapBubble, OnboardingStep.CompleteQuest, OnboardingStep.Done })
            {
                var state = GestureCoach.BeatAt(step, 3.3);
                Assert.That(state.Beat, Is.EqualTo(GestureBeat.Hidden), $"{step} shows no arrows");
                Assert.That(state.Progress, Is.EqualTo(0f).Within(Tol), $"{step} has no progress");
            }
        }

        [Test]
        public void TimingConstants_MatchTheApprovedWireframe()
        {
            // docs/specs/ui/onboarding-overlay.md gesture-coach constants (#330).
            Assert.That(GestureCoach.BeatDurationSec, Is.EqualTo(1.1f).Within(Tol));
            Assert.That(GestureCoach.BeatPauseSec, Is.EqualTo(0.5f).Within(Tol));
            Assert.That(GestureCoach.SlotDurationSec,
                Is.EqualTo(1.6f).Within(Tol), "one sweep plus its hold");
        }
    }
}
