using Doggiehood.Core.Cameras;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Thin adapter between input gestures and the Core CameraController
    /// (#20, #21). All decisions (clamping, gesture math, the fixed angle)
    /// live in Core; this component polls input, forwards deltas, and copies
    /// the resulting state onto the actual camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private const float TapMaxDragPixels = 12f;

        private Camera cachedCamera;
        private Vector3 lastPointerPosition;
        private float accumulatedDragPixels;
        private float lastPinchDistance;

        public CameraController Controller { get; } = CameraController.ForStartingNeighborhood();

        private void Awake()
        {
            ApplyConfiguration();
        }

        /// <summary>Applies the fixed isometric configuration and current
        /// controller state. Idempotent; tests call it directly.</summary>
        public void ApplyConfiguration()
        {
            cachedCamera = GetComponent<Camera>();
            cachedCamera.orthographic = CameraRigConfig.Orthographic;
            transform.rotation = Quaternion.Euler(CameraRigConfig.PitchDegrees, CameraRigConfig.YawDegrees, 0f);
            ApplyControllerState();
        }

        public void HandleDrag(float dragXPixels, float dragYPixels, float screenHeightPixels)
        {
            var pan = GestureMapper.DragToPan(dragXPixels, dragYPixels, Controller.Zoom, screenHeightPixels);
            Controller.Pan(pan.X, pan.Z);
            ApplyControllerState();
        }

        public void HandlePinch(float pinchDeltaPixels, float screenHeightPixels)
        {
            Controller.ZoomBy(GestureMapper.PinchToZoom(pinchDeltaPixels, Controller.Zoom, screenHeightPixels));
            ApplyControllerState();
        }

        public void HandleTap(Vector2 screenPosition)
        {
            TapRouter.RouteTap(cachedCamera, screenPosition);
        }

        private void ApplyControllerState()
        {
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
            var distance = Vector2.Distance(a.position, b.position);

            if (a.phase != TouchPhase.Began && b.phase != TouchPhase.Began && lastPinchDistance > 0f)
            {
                HandlePinch(distance - lastPinchDistance, Screen.height);
            }

            lastPinchDistance = distance;
            accumulatedDragPixels = float.MaxValue; // a pinch is never a tap
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
