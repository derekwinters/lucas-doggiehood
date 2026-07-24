using Doggiehood.Core.Cameras;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Thin adapter between input gestures and the Core CameraController
    /// (#20, #21, #203). All decisions (pan clamping, gesture math, the fixed
    /// pitch/projection, free yaw rotation) live in Core; this component polls
    /// input, forwards deltas, and copies the resulting state onto the actual
    /// camera. The two-finger twist that drives rotation is assembled here
    /// from per-frame touch angles, since Unity's touch API has no built-in
    /// twist gesture.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private const float TapMaxDragPixels = 12f;

        private Camera cachedCamera;
        private Vector3 lastPointerPosition;
        private float accumulatedDragPixels;
        private float lastPinchDistance;
        private float lastTwistAngle;

        public CameraController Controller { get; } = CameraController.ForStartingNeighborhood();

        private void Awake()
        {
            ApplyConfiguration();
        }

        /// <summary>Applies the fixed projection and current controller state
        /// (position, zoom, yaw). Idempotent; tests call it directly.</summary>
        public void ApplyConfiguration()
        {
            cachedCamera = GetComponent<Camera>();
            cachedCamera.orthographic = CameraRigConfig.Orthographic;
            ApplyControllerState();
        }

        public void HandleDrag(float dragXPixels, float dragYPixels, float screenHeightPixels)
        {
            var pan = GestureMapper.DragToPan(dragXPixels, dragYPixels, Controller.Yaw, Controller.Zoom, screenHeightPixels);
            Controller.Pan(pan.X, pan.Z);
            ApplyControllerState();
        }

        public void HandlePinch(float pinchDeltaPixels, float screenHeightPixels)
        {
            Controller.ZoomBy(GestureMapper.PinchToZoom(pinchDeltaPixels, Controller.Zoom, screenHeightPixels));
            ApplyControllerState();
        }

        public void HandleTwist(float twistDeltaDegrees)
        {
            Controller.Rotate(GestureMapper.TwistToRotation(twistDeltaDegrees));
            ApplyControllerState();
        }

        public void HandleTap(Vector2 screenPosition)
        {
            TapRouter.RouteTap(cachedCamera, screenPosition);
        }

        /// <summary>Input-independent core of two-finger polling (#203). Given
        /// this frame's two touch positions and whether both touches are
        /// continuing (neither just began), emits the pinch-zoom and
        /// twist-rotation for the change since the previous sample, then
        /// records the new baseline. Public so EditMode tests can drive it
        /// without simulating Unity's touch input.</summary>
        public void ProcessTwoFingerSample(Vector2 first, Vector2 second, bool bothContinuing, float screenHeightPixels)
        {
            var span = second - first;
            var distance = span.magnitude;
            var angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;

            if (bothContinuing && lastPinchDistance > 0f)
            {
                HandlePinch(distance - lastPinchDistance, screenHeightPixels);

                // Mathf.DeltaAngle gives the counter-clockwise angle change;
                // negate so a clockwise finger twist is a positive twist delta.
                // GestureMapper.TwistToRotation then inverts that into the camera
                // yaw so the scene follows the fingers (see its docs).
                HandleTwist(-Mathf.DeltaAngle(lastTwistAngle, angle));
            }

            lastPinchDistance = distance;
            lastTwistAngle = angle;
            accumulatedDragPixels = float.MaxValue; // a two-finger gesture is never a tap
        }

        private void ApplyControllerState()
        {
            transform.rotation = Quaternion.Euler(CameraRigConfig.PitchDegrees, Controller.Yaw, 0f);
            cachedCamera.orthographicSize = Controller.Zoom;
            var target = new Vector3(Controller.Position.X, 0f, Controller.Position.Z);
            transform.position = target - transform.forward * CameraRigConfig.RigDistance;
        }

        private void Update()
        {
            if (Input.touchCount >= 2)
            {
                PollPinch();
            }
            else if (Input.touchCount == 1)
            {
                PollTouchDrag(Input.GetTouch(0));
            }
            else
            {
                PollMouse();
            }
        }

        private void PollPinch()
        {
            var a = Input.GetTouch(0);
            var b = Input.GetTouch(1);
            var bothContinuing = a.phase != TouchPhase.Began && b.phase != TouchPhase.Began;

            ProcessTwoFingerSample(a.position, b.position, bothContinuing, Screen.height);
        }

        private void PollTouchDrag(Touch touch)
        {
            lastPinchDistance = 0f;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    accumulatedDragPixels = 0f;
                    break;
                case TouchPhase.Moved:
                    accumulatedDragPixels += touch.deltaPosition.magnitude;
                    HandleDrag(touch.deltaPosition.x, touch.deltaPosition.y, Screen.height);
                    break;
                case TouchPhase.Ended:
                    if (accumulatedDragPixels <= TapMaxDragPixels)
                    {
                        HandleTap(touch.position);
                    }
                    break;
            }
        }

        private void PollMouse()
        {
            lastPinchDistance = 0f;

            if (Input.GetMouseButtonDown(0))
            {
                lastPointerPosition = Input.mousePosition;
                accumulatedDragPixels = 0f;
            }
            else if (Input.GetMouseButton(0))
            {
                var delta = Input.mousePosition - lastPointerPosition;
                accumulatedDragPixels += delta.magnitude;
                if (delta != Vector3.zero)
                {
                    HandleDrag(delta.x, delta.y, Screen.height);
                    lastPointerPosition = Input.mousePosition;
                }
            }
            else if (Input.GetMouseButtonUp(0) && accumulatedDragPixels <= TapMaxDragPixels)
            {
                HandleTap(Input.mousePosition);
            }

            var scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                // Desktop convenience: scroll ~ pinch.
                HandlePinch(scroll * 50f, Screen.height);
            }
        }
    }
}
