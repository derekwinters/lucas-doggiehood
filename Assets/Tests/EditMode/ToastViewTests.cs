using Doggiehood.Core.Cameras;
using Doggiehood.Core.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #541: the non-modal completion toast view (docs/specs/ui/toast.md /
    /// mockups/toast.html, approved wireframe #562). It renders the single current
    /// slot of the shared <see cref="ToastQueue{T}"/> in the reserved top-left HUD
    /// lane and clears it — promoting the next waiting toast — after
    /// <see cref="ToastView.ToastAutoDismissSeconds"/> (3.5s) or the instant it is
    /// tapped. It never registers with <see cref="ModalInputGate"/> (non-modal),
    /// and the HUD reserves its lane per <see cref="HudOverlay"/>'s
    /// <c>HudToastLane*</c> constants. The animation is driven through
    /// <see cref="ToastView.Tick"/> so it is testable without a frame loop.
    /// </summary>
    public class ToastViewTests
    {
        private const float Eps = 0.001f;

        private GameObject host;
        private ToastQueue<ToastRequest> queue;
        private ToastView view;

        [SetUp]
        public void SetUp()
        {
            ModalInputGate.Shared.Clear();
            host = new GameObject("toast-view");
            queue = new ToastQueue<ToastRequest>();
            view = host.AddComponent<ToastView>();
            view.Init(queue);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        // Advances the view fully through one toast's lifecycle: finish slide-in,
        // run out the auto-dismiss hold, then finish slide-out (which clears the
        // current slot and promotes any next toast).
        private void AdvanceThroughAutoDismiss()
        {
            view.Tick(ToastView.ToastSlideInMs / 1000f + Eps);   // slide-in -> hold
            view.Tick(ToastView.ToastAutoDismissSeconds + Eps);  // hold -> slide-out begins
            view.Tick(ToastView.ToastSlideOutMs / 1000f + Eps);  // slide-out -> promote/clear
        }

        [Test]
        public void ConstantsMatchTheApprovedWireframe()
        {
            Assert.That(ToastView.ToastLaneTopMarginPx, Is.EqualTo(32f));
            Assert.That(ToastView.ToastLaneLeftMarginPx, Is.EqualTo(36f));
            Assert.That(ToastView.ToastHeightPx, Is.EqualTo(88f));
            Assert.That(ToastView.ToastMaxWidthPx, Is.EqualTo(640f));
            Assert.That(ToastView.ToastCoinDiameterPx, Is.EqualTo(60f));
            Assert.That(ToastView.ToastPaddingLeftPx, Is.EqualTo(14f));
            Assert.That(ToastView.ToastPaddingRightPx, Is.EqualTo(28f));
            Assert.That(ToastView.ToastIconGapPx, Is.EqualTo(16f));
            Assert.That(ToastView.ToastFontSizePx, Is.EqualTo(34));
            Assert.That(ToastView.ToastAutoDismissSeconds, Is.EqualTo(3.5f));
            Assert.That(ToastView.ToastSlideInMs, Is.EqualTo(220f));
            Assert.That(ToastView.ToastSlideOutMs, Is.EqualTo(180f));
            Assert.That(ToastQueue<ToastRequest>.SlotCount, Is.EqualTo(1));
        }

        [Test]
        public void FirstEnqueue_StartsShowingInTheLane()
        {
            queue.Enqueue(new ToastRequest("Quest complete! +10 coins"));

            Assert.That(view.IsShowing, Is.True);
            Assert.That(view.CurrentMessage, Is.EqualTo("Quest complete! +10 coins"));
        }

        [Test]
        public void AutoTimeout_ClearsTheCurrentToast_AndPromotesTheNext()
        {
            queue.Enqueue(new ToastRequest("first"));
            queue.Enqueue(new ToastRequest("second"));
            Assert.That(view.CurrentMessage, Is.EqualTo("first"), "one at a time — second waits");

            AdvanceThroughAutoDismiss();

            Assert.That(view.CurrentMessage, Is.EqualTo("second"),
                "after the auto-timeout the next queued toast is promoted");
            Assert.That(view.IsShowing, Is.True);
        }

        [Test]
        public void Tap_DismissesEarly_AndPromotesTheNext()
        {
            queue.Enqueue(new ToastRequest("first"));
            queue.Enqueue(new ToastRequest("second"));

            view.Tick(ToastView.ToastSlideInMs / 1000f + Eps);   // now holding
            view.Tap();                                          // begin exit immediately
            view.Tick(ToastView.ToastSlideOutMs / 1000f + Eps);  // slide-out -> promote

            Assert.That(view.CurrentMessage, Is.EqualTo("second"),
                "a tap dismisses early and promotes the next, same as the timeout");
        }

        [Test]
        public void DismissingTheOnlyToast_LeavesTheLaneIdle()
        {
            queue.Enqueue(new ToastRequest("only"));

            AdvanceThroughAutoDismiss();

            Assert.That(view.IsShowing, Is.False, "nothing waiting -> the lane goes idle");
            Assert.That(queue.HasCurrent, Is.False);
        }

        [Test]
        public void Toast_IsNonModal_NeverRegistersWithTheModalGate_ThroughItsWholeLifecycle()
        {
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False);

            queue.Enqueue(new ToastRequest("Quest complete! +10 coins"));
            Assert.That(view.IsShowing, Is.True);
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False,
                "a showing toast never blocks world/gameplay taps (non-modal, no scrim)");

            AdvanceThroughAutoDismiss();
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False,
                "and it leaves no modal registration behind after it clears");
        }

        [Test]
        public void Hud_ReservesTheTopLeftToastLane_PerTheWireframe()
        {
            Assert.That(HudOverlay.HudToastLaneAnchor, Is.EqualTo(HudCorner.TopLeft));
            Assert.That(HudOverlay.HudToastLaneTopMarginPx, Is.EqualTo(32f));
            Assert.That(HudOverlay.HudToastLaneLeftMarginPx, Is.EqualTo(36f));

            var lane = HudOverlay.ComputeToastLaneRect();
            Assert.That(lane.x, Is.EqualTo(ToastView.ToastLaneLeftMarginPx), "left inset");
            Assert.That(lane.y, Is.EqualTo(ToastView.ToastLaneTopMarginPx), "top inset");
            Assert.That(lane.width, Is.EqualTo(ToastView.ToastMaxWidthPx));
            Assert.That(lane.height, Is.EqualTo(ToastView.ToastHeightPx));

            // Mirrors the currency chip's reservation in the opposite corner.
            Assert.That(HudOverlay.HudChipAnchor, Is.EqualTo(HudCorner.TopRight));
        }
    }
}
