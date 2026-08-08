using Doggiehood.Core.Cameras;
using Doggiehood.Core.Interaction;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #670 (R1): the one and only place raw <c>UnityEngine.Input</c> is read.
    /// It translates touches, the mouse and the wheel into engine-free
    /// <see cref="InputGesture"/>s and hands every one of them to
    /// <see cref="InputAuthority"/>, which decides who receives it. This
    /// component makes no decision of its own — it never moves the camera, never
    /// hit-tests the world, never asks whether a dialog is open.
    ///
    /// It exists because "an open modal blocks input" used to be a property each
    /// consumer opted into. <see cref="CameraRig"/> polled input itself and
    /// drove pan, pinch, twist and scroll straight from that polling, so those
    /// four paths never asked the modal gate and dragging a tuning-menu slider
    /// panned the map underneath it. With polling centralised here, a new input
    /// path cannot be added without going through the authority — and
    /// <c>InputAuthorityGuardTests</c> fails the build if one tries.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        /// <summary>Desktop convenience: one mouse-wheel tick reads as this many
        /// pixels of pinch (#161 — no bare literals).</summary>
        private const float ScrollTickPixels = 50f;

        private Vector3 lastMousePosition;
        private bool mouseGestureInFlight;
        private int? touchGestureFingerId;
        private bool twoFingerGestureInFlight;
        private float lastPinchDistance;
        private float lastTwistAngle;

        private void Update()
        {
            if (Input.touchCount >= 2)
            {
                PollTwoFinger();
            }
            else if (Input.touchCount == 1)
            {
                PollTouch(Input.GetTouch(0));
            }
            else
            {
                PollMouse();
            }
        }

        /// <summary>#568: clears the shared <see cref="ModalInputGate"/>'s
        /// this-frame close latch at end of frame. Unity runs every
        /// <c>LateUpdate</c> only after all <c>Update</c>s (including the
        /// EventSystem's UI dispatch and this router's own gesture dispatch) have
        /// completed — so a modal that a tap dismissed this frame keeps blocking
        /// that same tap, but the latch is always clear before the next frame's
        /// unrelated gesture. Moved here from <see cref="CameraRig"/> with #670:
        /// it is an input-frame boundary, and the camera is now an ordinary
        /// consumer rather than the owner of the input loop.</summary>
        private void LateUpdate()
        {
            ModalInputGate.Shared.EndFrame();
        }

        /// <summary>Releases anything still in flight when the router goes away,
        /// so a torn-down scene can't leave an ownership latch stuck and dead-end
        /// the next scene's input.</summary>
        private void OnDisable()
        {
            CancelSinglePointerGestures();
            EndTwoFingerGesture();
        }

        /// <summary>Input-independent core of two-finger polling (#203), public
        /// so EditMode tests can drive it without simulating Unity touches.
        /// Given this frame's two touch positions and whether both touches are
        /// continuing, emits the pinch-zoom and twist-rotation deltas since the
        /// previous sample as authority gestures, then records the new baseline.
        /// </summary>
        public void SampleTwoFingerGesture(Vector2 first, Vector2 second, bool bothContinuing)
        {
            var span = second - first;
            var distance = span.magnitude;
            var angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;

            if (!twoFingerGestureInFlight)
            {
                // A second finger supersedes whatever the first one was doing:
                // cancel it rather than releasing it, so the abandoned press
                // can't resolve as a tap.
                CancelSinglePointerGestures();
                InputAuthority.Shared.Begin(InputGesture.Pinch(0f));
                twoFingerGestureInFlight = true;
            }

            if (bothContinuing && lastPinchDistance > 0f)
            {
                InputAuthority.Shared.Continue(InputGesture.Pinch(distance - lastPinchDistance));

                // Mathf.DeltaAngle gives the counter-clockwise angle change;
                // negate so a clockwise finger twist is a positive twist delta.
                InputAuthority.Shared.Continue(
                    InputGesture.Twist(-Mathf.DeltaAngle(lastTwistAngle, angle)));
            }

            lastPinchDistance = distance;
            lastTwistAngle = angle;
        }

        private void PollTwoFinger()
        {
            var a = Input.GetTouch(0);
            var b = Input.GetTouch(1);
            var bothContinuing = a.phase != TouchPhase.Began && b.phase != TouchPhase.Began;

            SampleTwoFingerGesture(a.position, b.position, bothContinuing);
        }

        private void PollTouch(Touch touch)
        {
            EndTwoFingerGesture();

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchGestureFingerId = touch.fingerId;
                    InputAuthority.Shared.Begin(
                        InputGesture.Pan(touch.fingerId, touch.position.x, touch.position.y));
                    break;
                case TouchPhase.Moved:
                    InputAuthority.Shared.Continue(InputGesture.Pan(
                        touch.fingerId, touch.position.x, touch.position.y,
                        touch.deltaPosition.x, touch.deltaPosition.y));
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    touchGestureFingerId = null;
                    InputAuthority.Shared.End(
                        InputGesture.Pan(touch.fingerId, touch.position.x, touch.position.y));
                    break;
            }
        }

        private void PollMouse()
        {
            EndTwoFingerGesture();

            if (Input.GetMouseButtonDown(0))
            {
                lastMousePosition = Input.mousePosition;
                mouseGestureInFlight = true;
                InputAuthority.Shared.Begin(InputGesture.Pan(
                    InputGesture.MousePointerId, lastMousePosition.x, lastMousePosition.y));
            }
            else if (Input.GetMouseButton(0))
            {
                var delta = Input.mousePosition - lastMousePosition;
                if (delta != Vector3.zero)
                {
                    lastMousePosition = Input.mousePosition;
                    InputAuthority.Shared.Continue(InputGesture.Pan(
                        InputGesture.MousePointerId,
                        lastMousePosition.x, lastMousePosition.y,
                        delta.x, delta.y));
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                mouseGestureInFlight = false;
                InputAuthority.Shared.End(InputGesture.Pan(
                    InputGesture.MousePointerId, lastMousePosition.x, lastMousePosition.y));
            }

            var scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                InputAuthority.Shared.Deliver(InputGesture.Scroll(scroll * ScrollTickPixels));
            }
        }

        private void EndTwoFingerGesture()
        {
            if (!twoFingerGestureInFlight)
            {
                return;
            }

            twoFingerGestureInFlight = false;
            lastPinchDistance = 0f;
            InputAuthority.Shared.End(InputGesture.Pinch(0f));
        }

        private void CancelSinglePointerGestures()
        {
            if (mouseGestureInFlight)
            {
                mouseGestureInFlight = false;
                InputAuthority.Shared.Cancel(InputGesture.MousePointerId);
            }

            if (touchGestureFingerId.HasValue)
            {
                InputAuthority.Shared.Cancel(touchGestureFingerId.Value);
                touchGestureFingerId = null;
            }
        }
    }
}
