using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// First-launch tutorial prompts (#44), layered over live gameplay as a
    /// slim bottom-center coach bar — never a modal scene. Watches the real
    /// camera controller for pan/zoom, the presenter for the bubble tap, and
    /// the live quest for completion, then auto-dismisses. All sequencing
    /// lives in Core; layout follows the approved wireframe
    /// (docs/specs/ui/onboarding-overlay.md, #176).
    /// </summary>
    public sealed class OnboardingOverlay : MonoBehaviour
    {
        // Layout constants from the #176 wireframe, authored at the 1920x1200
        // reference (docs/specs/ui/onboarding-overlay.md). No inline geometry
        // literals (#161).
        private const float ReferenceHeightPx = 1200f;
        private const float CoachWidthPx = 900f;
        private const float CoachHeightPx = 88f;
        private const float CoachBottomMarginPx = 56f;
        private const int StepDotCount = 4;
        private const int MsgFontPx = 30;
        private const float PadPx = 34f;
        private const char FilledDotGlyph = '●';
        private const char EmptyDotGlyph = '○';

        private OnboardingSequence sequence;
        private GameState state;
        private CameraRig rig;
        private GridPoint startPosition;
        private float startZoom;

        /// <summary>The live sequence step; used by wiring tests to observe
        /// advancement. Done once onboarding is complete.</summary>
        public OnboardingStep CurrentStep
        {
            get { return sequence == null ? OnboardingStep.Done : sequence.CurrentStep; }
        }

        /// <summary>Whether the coach bar should render this frame — false
        /// before Init and once the sequence reaches Done (#207: the banner
        /// must auto-dismiss after the first quest completes).</summary>
        public bool ShouldDraw
        {
            get { return sequence != null && sequence.CurrentStep != OnboardingStep.Done; }
        }

        public void Init(GameState state, CameraRig rig, ConversationPresenter presenter)
        {
            this.state = state;
            this.rig = rig;
            sequence = new OnboardingSequence(state);

            if (rig != null)
            {
                startPosition = rig.Controller.Position;
                startZoom = rig.Controller.Zoom;
            }

            presenter.Opened += dog => sequence.NotifyConversationOpened(dog);
        }

        /// <summary>One poll of the onboarding wiring: advance the camera
        /// steps from the live rig, then check quest completion. Public so
        /// EditMode tests can drive it without a running player loop.</summary>
        public void Poll()
        {
            if (sequence == null || sequence.CurrentStep == OnboardingStep.Done)
            {
                return;
            }

            AdvanceCameraSteps();

            if (sequence.CurrentStep == OnboardingStep.CompleteQuest)
            {
                CheckQuestCompletion();
            }
        }

        private void Update()
        {
            Poll();
        }

        private void AdvanceCameraSteps()
        {
            if (sequence.CurrentStep != OnboardingStep.Pan
                && sequence.CurrentStep != OnboardingStep.Zoom)
            {
                return;
            }

            if (rig == null)
            {
                // No CameraRig was wired (WorldBootstrap can find none): there
                // is nothing to pan or zoom, so don't deadlock the sequence on
                // steps the player physically can't perform — satisfy them so
                // onboarding can still reach the tap/complete steps and dismiss.
                sequence.NotifyPanned();
                sequence.NotifyZoomed();
                return;
            }

            if (!rig.Controller.Position.Equals(startPosition))
            {
                sequence.NotifyPanned();
            }

            if (!Mathf.Approximately(rig.Controller.Zoom, startZoom))
            {
                sequence.NotifyZoomed();
            }
        }

        private void CheckQuestCompletion()
        {
            if (sequence.TargetDog == null || sequence.TargetDog.HasActiveQuest)
            {
                return;
            }

            sequence.NotifyTargetDogQuestResolved();
            if (sequence.CurrentStep == OnboardingStep.Done)
            {
                SaveStore.Save(state);
            }
        }

        /// <summary>Computes the coach bar rect for the given screen size:
        /// bottom-center, scaled from the 1920x1200 wireframe reference so
        /// each px constant keeps a fixed meaning across tablet sizes.</summary>
        public static Rect ComputeCoachRect(float screenWidth, float screenHeight)
        {
            var scale = screenHeight / ReferenceHeightPx;
            var width = CoachWidthPx * scale;
            var height = CoachHeightPx * scale;
            var x = (screenWidth - width) / 2f;
            var y = screenHeight - height - CoachBottomMarginPx * scale;
            return new Rect(x, y, width, height);
        }

        private void OnGUI()
        {
            if (!ShouldDraw)
            {
                return;
            }

            var scale = Screen.height / ReferenceHeightPx;
            var rect = ComputeCoachRect(Screen.width, Screen.height);

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.RoundToInt(MsgFontPx * scale),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(Mathf.RoundToInt(PadPx * scale), Mathf.RoundToInt(PadPx * scale), 0, 0),
            };

            GUI.Box(rect, $"{PromptFor(sequence.CurrentStep)}    {StepDots(sequence.CurrentStep)}", style);
        }

        private string PromptFor(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.Pan:
                    return "Welcome to Doggiehood! Drag to look around the neighborhood.";
                case OnboardingStep.Zoom:
                    return "Nice! Pinch (or scroll) to zoom in and out.";
                case OnboardingStep.TapBubble:
                    var name = sequence.TargetDog != null ? sequence.TargetDog.Name : "a dog";
                    return $"{name} has something to say — tap the speech bubble!";
                default:
                    return "Help them out to finish your first quest!";
            }
        }

        /// <summary>The trailing step-dots region (#176): StepDotCount dots,
        /// filled up to and including the current step.</summary>
        private static string StepDots(OnboardingStep step)
        {
            var current = StepIndex(step);
            var dots = new char[StepDotCount];
            for (var i = 0; i < StepDotCount; i++)
            {
                dots[i] = i <= current ? FilledDotGlyph : EmptyDotGlyph;
            }

            return new string(dots);
        }

        private static int StepIndex(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.Pan:
                    return 0;
                case OnboardingStep.Zoom:
                    return 1;
                case OnboardingStep.TapBubble:
                    return 2;
                default:
                    return 3;
            }
        }
    }
}
