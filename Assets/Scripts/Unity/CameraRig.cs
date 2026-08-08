using Doggiehood.Core.Art;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Interaction;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Thin adapter between camera gestures and the Core CameraController
    /// (#20, #21, #203). All decisions (pan clamping, gesture math, the fixed
    /// pitch/projection, free yaw rotation) live in Core; this component
    /// forwards deltas and copies the resulting state onto the actual camera.
    ///
    /// #670: the rig no longer polls input. It registers with
    /// <see cref="InputAuthority"/> as an ordinary <see cref="InputTier.Camera"/>
    /// consumer — the lowest tier — and is offered a gesture only once modal UI,
    /// non-modal UI and the world have all declined it. That is what makes an
    /// open dialog block pan, pinch, twist and scroll: before, those four ran
    /// straight from this component's own raw polling and never asked the modal
    /// gate at all, so dragging a slider in the debug tuning menu panned the map
    /// underneath it. Raw input now enters only through <see cref="InputRouter"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private const float TapMaxDragPixels = 12f;

        /// <summary>Screen height to scale gestures by when Unity reports a
        /// non-positive one (headless/batch runs have no game view).
        /// <see cref="GestureMapper"/> requires a positive height, so without a
        /// fallback the camera would throw rather than merely be mis-scaled
        /// (#161 — named, not a bare literal).</summary>
        private const float FallbackScreenHeightPixels = 1080f;

        private static float ScreenHeightPixels
            => Screen.height > 0 ? Screen.height : FallbackScreenHeightPixels;

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
        private float accumulatedDragPixels;
        private IInputConsumer inputConsumer;

        public CameraController Controller { get; } = CameraController.ForStartingNeighborhood();

        private void Awake()
        {
            ApplyConfiguration();
        }

        private void OnDestroy()
        {
            InputAuthority.Shared.Unregister(inputConsumer);
        }

        /// <summary>Applies the fixed projection and current controller state
        /// (position, zoom, yaw), and registers the rig as the camera-tier input
        /// consumer. Idempotent; tests call it directly.</summary>
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

            EnsureInputWiring();
            ApplyControllerState();
        }

        /// <summary>#670: joins the input authority as the lowest-priority
        /// consumer, and makes sure the single raw-input entry point exists
        /// alongside the rig. Done here rather than only in <c>Awake</c> because
        /// EditMode tests build a rig without Unity's lifecycle callbacks;
        /// registration is idempotent, so calling it again is free.</summary>
        private void EnsureInputWiring()
        {
            if (GetComponent<InputRouter>() == null)
            {
                gameObject.AddComponent<InputRouter>();
            }

            inputConsumer ??= new DelegateInputConsumer(InputTier.Camera, OnGesture);
            InputAuthority.Shared.Register(inputConsumer);
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

        /// <summary>#670: the camera's whole input surface. Reached only for
        /// gestures the authority has already resolved to this consumer, so
        /// every "is a dialog open?" question has been answered before we get
        /// here — the rig itself no longer has (or needs) a gate check.
        ///
        /// A pan gesture's press and release are the same gesture as its travel,
        /// which is why the tap falls out of the release rather than needing its
        /// own path: under a threshold of travel, the release is a tap.</summary>
        private void OnGesture(InputGesture gesture, InputGesturePhase phase)
        {
            switch (gesture.Kind)
            {
                case InputGestureKind.Pan:
                    OnPan(gesture, phase);
                    break;
                case InputGestureKind.Pinch:
                    if (phase == InputGesturePhase.Changed)
                    {
                        HandlePinch(gesture.Scalar, ScreenHeightPixels);
                    }

                    break;
                case InputGestureKind.Twist:
                    if (phase == InputGesturePhase.Changed)
                    {
                        HandleTwist(gesture.Scalar);
                    }

                    break;
                case InputGestureKind.Scroll:
                    // Desktop convenience: scroll ~ pinch.
                    HandlePinch(gesture.Scalar, ScreenHeightPixels);
                    break;
            }
        }

        private void OnPan(InputGesture gesture, InputGesturePhase phase)
        {
            switch (phase)
            {
                case InputGesturePhase.Began:
                    accumulatedDragPixels = 0f;
                    break;
                case InputGesturePhase.Changed:
                    accumulatedDragPixels += new Vector2(gesture.DeltaX, gesture.DeltaY).magnitude;
                    HandleDrag(gesture.DeltaX, gesture.DeltaY, ScreenHeightPixels);
                    break;
                case InputGesturePhase.Ended:
                    if (accumulatedDragPixels <= TapMaxDragPixels)
                    {
                        // #422: thread the fingerId so the pointer-over-UI guard
                        // uses IsPointerOverGameObject(fingerId) for this touch;
                        // the mouse uses the parameterless overload (null).
                        HandleTap(
                            new Vector2(gesture.ScreenX, gesture.ScreenY),
                            gesture.PointerId == InputGesture.MousePointerId ? (int?)null : gesture.PointerId);
                    }

                    break;
                case InputGesturePhase.Cancelled:
                    // Superseded (a second finger arrived) — never a tap.
                    accumulatedDragPixels = float.MaxValue;
                    break;
            }
        }

        /// <summary>Copies the controller's state onto the real camera. The
        /// set-back and both clip planes are derived from the live zoom (#679),
        /// never pinned and never left to the scene: the visible ground is a
        /// slab whose view depth grows with the zoom, so a fixed set-back put
        /// the near half of the frame behind the camera past ~60m of zoom — the
        /// blank band along the bottom of the screen. Writing the planes here
        /// rather than reading the Main scene's serialized 0.3/300 is what makes
        /// the guarantee hold for every scene and every map size.</summary>
        private void ApplyControllerState()
        {
            transform.rotation = Quaternion.Euler(CameraRigConfig.PitchDegrees, Controller.Yaw, 0f);
            cachedCamera.orthographicSize = Controller.Zoom;
            var target = new Vector3(Controller.Position.X, 0f, Controller.Position.Z);
            transform.position = target - transform.forward * CameraRigConfig.RigDistanceFor(Controller.Zoom);
            cachedCamera.nearClipPlane = CameraRigConfig.NearClipPlane;
            cachedCamera.farClipPlane = CameraRigConfig.FarClipFor(Controller.Zoom);
        }
    }
}
