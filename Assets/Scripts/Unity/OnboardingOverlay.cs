using Doggiehood.Core.Cameras;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// First-launch tutorial prompts (#44), layered over live gameplay as a
    /// small IMGUI banner — never a modal scene. Watches the real camera
    /// controller for pan/zoom, the presenter for the bubble tap, and the
    /// live quest for completion. All sequencing lives in Core.
    /// </summary>
    public sealed class OnboardingOverlay : MonoBehaviour
    {
        private OnboardingSequence sequence;
        private GameState state;
        private CameraRig rig;
        private GridPoint startPosition;
        private float startZoom;

        public void Init(GameState state, CameraRig rig, ConversationPresenter presenter)
        {
            this.state = state;
            this.rig = rig;
            sequence = new OnboardingSequence(state);
            startPosition = rig.Controller.Position;
            startZoom = rig.Controller.Zoom;

            presenter.Opened += dog => sequence.NotifyConversationOpened(dog);
        }

        private void Update()
        {
            if (sequence == null || sequence.CurrentStep == OnboardingStep.Done)
            {
                return;
            }

            if (!sequence.CurrentStep.Equals(OnboardingStep.Pan) && !sequence.CurrentStep.Equals(OnboardingStep.Zoom))
            {
                CheckQuestCompletion();
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

        private void OnGUI()
        {
            if (sequence == null || sequence.CurrentStep == OnboardingStep.Done)
            {
                return;
            }

            var prompt = PromptFor(sequence.CurrentStep);
            var width = Mathf.Min(560f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - width) / 2f, 16f, width, 44f), prompt);
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
    }
}
