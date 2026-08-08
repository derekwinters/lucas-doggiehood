using Doggiehood.Core.Art;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Debugging;
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

        /// <summary>Void backstop clear colour (#558): the same grass green the
        /// ground plane is painted (<see cref="Palette.GrassHex"/>), so any area
        /// beyond the mesh edge reads as continuous grass rather than the default
        /// blue seam. Mirrors <see cref="PortraitCamera"/>'s SolidColor pattern.
        /// #611: when the Debug tab's diagnostic toggle
        /// (<see cref="WorldBuilder.ShowDebugElementColors"/>) is on, this becomes
        /// the loud <see cref="DebugElementColors.BackstopDebugHex"/> instead — the
        /// same Core colour decision the ground plane uses — so the backstop is
        /// visually distinct from the ground and the "border" element is
        /// identifiable.</summary>
        private static Color BackstopColor()
            => CoreColors.FromHex(
                DebugElementColors.BackstopHex(WorldBuilder.ShowDebugElementColors));

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

            // #558: clear to grass, not the default blue, as the void backstop.
            // The ground mesh now tracks the map footprint plus a modest margin
            // (CameraController.GroundExtentForMap) rather than the camera's
            // ballooning max-zoom reach, so anything the mesh doesn't cover at an
            // extreme pan+zoom-out lands on this grass-green clear colour and
            // reads as continuous grass instead of a seam. Mirrors PortraitCamera.
            cachedCamera.clearFlags = CameraClearFlags.SolidColor;
            cachedCamera.backgroundColor = BackstopColor();

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

        /// <summary>Routes a tap to the world (#20), unless UI absorbs it
        /// first (#422). <paramref name="pointerId"/> is the touch fingerId on
        /// the touch path, or null for the mouse — threaded through so the
        /// pointer-over-UI check uses the correct
        /// <c>IsPointerOverGameObject</c> overload.</summary>
        public void HandleTap(Vector2 screenPosition, int? pointerId = null)
        {
            TapRouter.RouteTap(cachedCamera, screenPosition, pointerId);
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

            // #679: the set-back and the far clip are derived from the zoom, not
            // fixed. A camera pitched down at a flat ground plane sees a slab of
            // view depth 2 x GroundDepthReach(zoom) thick, so a constant 60m
            // set-back put the foreground behind the camera the moment the zoom
            // passed 60 — the near clip plane discarded it and the grass clear
            // colour showed through as the growing bottom-of-screen band that
            // #536/#558/#570/#611 kept chasing as a coverage problem. The
            // serialized far clip (300) had the mirror-image failure at the top
            // of the frame. Both now track the zoom, so the whole frame renders
            // at every zoom the map-scaled MaxZoom (#510/#524) allows.
            cachedCamera.nearClipPlane = CameraRigConfig.NearClipPlane;
            cachedCamera.farClipPlane = CameraRigConfig.FarClipFor(Controller.Zoom);

            var target = new Vector3(Controller.Position.X, 0f, Controller.Position.Z);
            transform.position = target - transform.forward * CameraRigConfig.RigDistanceFor(Controller.Zoom);
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

        /// <summary>#568: clears the shared <see cref="ModalInputGate"/>'s
        /// this-frame close latch at end of frame. Unity guarantees every
        /// <c>LateUpdate</c> runs only after all <c>Update</c>s (including the
        /// EventSystem's UI dispatch and this rig's own tap routing) have
        /// completed for the frame — so a modal that a tap dismissed this frame
        /// keeps blocking that same tap's world raycast, but the latch is always
        /// clear before the next frame's unrelated tap is checked.</summary>
        private void LateUpdate()
        {
            ModalInputGate.Shared.EndFrame();
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
                        // #422: thread the fingerId so the pointer-over-UI guard
                        // uses IsPointerOverGameObject(fingerId) for this touch.
                        HandleTap(touch.position, touch.fingerId);
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
