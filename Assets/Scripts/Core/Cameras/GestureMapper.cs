using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// Maps raw screen-space gestures (pixels) onto camera operations (#20,
    /// #203). Pan direction depends on the live camera yaw passed in, since
    /// yaw is now free rotation: screen axes project onto the ground plane at
    /// the current orientation. Content follows the finger: dragging right
    /// moves the camera left along its view-right axis. A two-finger twist
    /// maps to a yaw-rotation delta.
    /// </summary>
    public static class GestureMapper
    {
        private const float DegreesToRadians = (float)Math.PI / 180f;

        /// <summary>Twist-to-rotation gain: degrees of camera yaw per degree
        /// of two-finger twist. 1:1 so the world tracks the fingers exactly.
        /// Sign is preserved, pinning the convention that a clockwise twist
        /// (positive delta) rotates the camera clockwise (positive delta).</summary>
        public const float TwistRotationSensitivity = 1f;

        /// <summary>World-space pan delta for a drag of (dx, dy) pixels
        /// (screen-up positive) at the given camera yaw, zoom and screen
        /// height. Pan direction follows the live yaw (#203).</summary>
        public static GridPoint DragToPan(float dragXPixels, float dragYPixels, float yawDegrees, float zoom, float screenHeightPixels)
        {
            var metersPerPixel = MetersPerPixel(zoom, screenHeightPixels);
            var yaw = yawDegrees * DegreesToRadians;
            var cos = (float)Math.Cos(yaw);
            var sin = (float)Math.Sin(yaw);

            // Ground-plane projections of the camera's right and forward axes.
            var rightX = cos;
            var rightZ = -sin;
            var forwardX = sin;
            var forwardZ = cos;

            return new GridPoint(
                -(dragXPixels * rightX + dragYPixels * forwardX) * metersPerPixel,
                -(dragXPixels * rightZ + dragYPixels * forwardZ) * metersPerPixel);
        }

        /// <summary>Zoom delta for a pinch: fingers moving apart (positive
        /// pixels) shrinks the orthographic size, i.e. zooms in.</summary>
        public static float PinchToZoom(float pinchDeltaPixels, float zoom, float screenHeightPixels)
        {
            return -pinchDeltaPixels * MetersPerPixel(zoom, screenHeightPixels);
        }

        /// <summary>Yaw-rotation delta (degrees) for a two-finger twist of the
        /// given degrees (#203). Clockwise twist -> clockwise camera rotation:
        /// the sign of the twist is preserved through the sensitivity gain.</summary>
        public static float TwistToRotation(float twistDeltaDegrees)
        {
            return twistDeltaDegrees * TwistRotationSensitivity;
        }

        private static float MetersPerPixel(float zoom, float screenHeightPixels)
        {
            if (screenHeightPixels <= 0f)
            {
                throw new ArgumentException("Screen height must be positive.", nameof(screenHeightPixels));
            }

            return 2f * zoom / screenHeightPixels;
        }
    }
}
