namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// The animated beat a <see cref="GestureCoach"/> arrow is playing.
    /// <see cref="Hidden"/> means no arrows are shown this frame.
    /// </summary>
    public enum GestureBeat
    {
        Hidden,
        LeftToRight,
        RightToLeft,
        UpToDown,
        DownToUp,
        ZoomIn,
        ZoomOut,
    }

    /// <summary>The active beat plus its 0-1 sweep progress.</summary>
    public readonly struct GestureBeatState
    {
        public GestureBeatState(GestureBeat beat, float progress)
        {
            Beat = beat;
            Progress = progress;
        }

        public GestureBeat Beat { get; }

        /// <summary>0 at the start of the sweep, 1 at its end; held at 1 through
        /// the pause before the next beat begins.</summary>
        public float Progress { get; }
    }

    /// <summary>
    /// #330: the engine-free gesture-beat sequencer behind the animated
    /// directional-arrow coach layered over the onboarding <b>Pan</b> and
    /// <b>Zoom</b> steps only (docs/specs/ui/onboarding-overlay.md,
    /// "Gesture-arrow coach"). It maps elapsed time + the current
    /// <see cref="OnboardingStep"/> to the beat that should play and its 0-1
    /// sweep progress; the thin <c>OnboardingOverlay</c> turns each beat +
    /// progress into arrow screen offsets and draws them. Every step other than
    /// Pan/Zoom is <see cref="GestureBeat.Hidden"/> — the arrows are scoped to
    /// the two movement steps and vanish the instant the real pan/zoom advances
    /// the sequence past them.
    /// </summary>
    public static class GestureCoach
    {
        // Approved wireframe timing constants (#330). No inline literals (#161).
        public const float BeatDurationSec = 1.1f;
        public const float BeatPauseSec = 0.5f;

        /// <summary>One beat's full slot: its sweep plus the hold that follows.</summary>
        public const float SlotDurationSec = BeatDurationSec + BeatPauseSec;

        private static readonly GestureBeat[] PanBeats =
        {
            GestureBeat.LeftToRight,
            GestureBeat.RightToLeft,
            GestureBeat.UpToDown,
            GestureBeat.DownToUp,
        };

        private static readonly GestureBeat[] ZoomBeats =
        {
            GestureBeat.ZoomIn,
            GestureBeat.ZoomOut,
        };

        /// <summary>The beat and 0-1 sweep progress for the given onboarding
        /// step at <paramref name="elapsedSeconds"/> since the step began. Pan
        /// cycles four directional beats, Zoom cycles zoom-in/zoom-out, and
        /// every other step is <see cref="GestureBeat.Hidden"/>.</summary>
        public static GestureBeatState BeatAt(OnboardingStep step, double elapsedSeconds)
        {
            GestureBeat[] beats;
            switch (step)
            {
                case OnboardingStep.Pan:
                    beats = PanBeats;
                    break;
                case OnboardingStep.Zoom:
                    beats = ZoomBeats;
                    break;
                default:
                    return new GestureBeatState(GestureBeat.Hidden, 0f);
            }

            var elapsed = elapsedSeconds < 0.0 ? 0.0 : elapsedSeconds;
            var cycleLength = beats.Length * SlotDurationSec;
            var cyclePos = elapsed % cycleLength;

            var slotIndex = (int)(cyclePos / SlotDurationSec);
            if (slotIndex >= beats.Length)
            {
                slotIndex = beats.Length - 1;
            }

            var withinSlot = cyclePos - slotIndex * SlotDurationSec;
            var progress = (float)(withinSlot / BeatDurationSec);
            if (progress > 1f)
            {
                progress = 1f;
            }

            return new GestureBeatState(beats[slotIndex], progress);
        }
    }
}
