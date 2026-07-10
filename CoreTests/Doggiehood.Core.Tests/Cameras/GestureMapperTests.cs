using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class GestureMapperTests
    {
        // At zoom 10 (ortho half-height) on a 1000px-tall screen, the world
        // spans 20m over 1000px -> 0.02 m/px. Yaw is fixed at 45 degrees
        // (CameraRigConfig), so screen axes map diagonally onto the ground.
        private const float Zoom = 10f;
        private const float ScreenHeight = 1000f;
        private const float Diag = 0.70710678f; // sin/cos 45

        [Test]
        public void DragRight_PansTheCameraLeftAlongTheViewRightAxis()
        {
            var pan = GestureMapper.DragToPan(100f, 0f, Zoom, ScreenHeight);

            Assert.That(pan.X, Is.EqualTo(-100f * Diag * 0.02f).Within(0.0001f));
            Assert.That(pan.Z, Is.EqualTo(100f * Diag * 0.02f).Within(0.0001f));
        }

        [Test]
        public void DragUp_PansTheCameraBackAlongTheViewForwardAxis()
        {
            var pan = GestureMapper.DragToPan(0f, 100f, Zoom, ScreenHeight);

            Assert.That(pan.X, Is.EqualTo(-100f * Diag * 0.02f).Within(0.0001f));
            Assert.That(pan.Z, Is.EqualTo(-100f * Diag * 0.02f).Within(0.0001f));
        }

        [Test]
        public void DragDistance_ScalesWithZoomLevel()
        {
            var zoomedOut = GestureMapper.DragToPan(100f, 0f, 30f, ScreenHeight);
            var zoomedIn = GestureMapper.DragToPan(100f, 0f, 6f, ScreenHeight);

            // The same finger movement covers more world when zoomed out.
            Assert.That(System.Math.Abs(zoomedOut.X), Is.GreaterThan(System.Math.Abs(zoomedIn.X)));
        }

        [Test]
        public void PinchApart_ZoomsIn()
        {
            var zoomDelta = GestureMapper.PinchToZoom(100f, Zoom, ScreenHeight);

            Assert.That(zoomDelta, Is.EqualTo(-100f * 0.02f).Within(0.0001f));
        }

        [Test]
        public void ZeroOrNegativeScreenHeight_Throws()
        {
            Assert.That(() => GestureMapper.DragToPan(1f, 1f, Zoom, 0f), Throws.ArgumentException);
            Assert.That(() => GestureMapper.PinchToZoom(1f, Zoom, -5f), Throws.ArgumentException);
        }
    }
}
