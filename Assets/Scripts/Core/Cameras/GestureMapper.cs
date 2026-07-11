using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// Maps raw screen-space gestures (pixels) onto camera operations (#20).
    /// The camera yaw is fixed (CameraRigConfig), so screen axes project
    /// onto fixed diagonal ground directions. Content follows the finger:
    /// dragging right moves the camera left along its view-right axis.
    /// </summary>
    public static class GestureMapper
    {
        /// <summary>World-space pan delta for a drag of (dx, dy) pixels
        /// (screen-up positive) at the given zoom and screen height.</summary>
        public static GridPoint DragToPan(float dragXPixels, float dragYPixels, float zoom, float screenHeightPixels)
        {
            var metersPerPixel = MetersPerPixel(zoom, screenHeightPixels);
            var yaw = CameraRigConfig.YawDegrees * (float)Math.PI / 180f;
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
