using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class GestureMapperTests
    {
        // At zoom 10 (ortho half-height) on a 1000px-tall screen, the world
        // spans 20m over 1000px -> 0.02 m/px. Pan direction now depends on the
        // live camera yaw passed in (#203); the historical cases use yaw 45deg,
        // where screen axes map diagonally onto the ground.
        private const float Zoom = 10f;
        private const float ScreenHeight = 1000f;
        private const float MetersPerPixel = 0.02f;
        private const float Diag = 0.70710678f; // sin/cos 45
        private const float IsoYaw = 45f;
        private const float QuarterYaw = 90f;

        [Test]
        public void DragRight_AtIsoYaw_PansTheCameraLeftAlongTheViewRightAxis()
        {
            var pan = GestureMapper.DragToPan(100f, 0f, IsoYaw, Zoom, ScreenHeight);

            Assert.That(pan.X, Is.EqualTo(-100f * Diag * MetersPerPixel).Within(0.0001f));
            Assert.That(pan.Z, Is.EqualTo(100f * Diag * MetersPerPixel).Within(0.0001f));
        }

        [Test]
        public void DragUp_AtIsoYaw_PansTheCameraBackAlongTheViewForwardAxis()
        {
            var pan = GestureMapper.DragToPan(0f, 100f, IsoYaw, Zoom, ScreenHeight);

            Assert.That(pan.X, Is.EqualTo(-100f * Diag * MetersPerPixel).Within(0.0001f));
            Assert.That(pan.Z, Is.EqualTo(-100f * Diag * MetersPerPixel).Within(0.0001f));
        }

        [Test]
        public void DragRight_PanDirection_FollowsARotatedCamera()
        {
            // #203: at yaw 90deg the view-right axis has rotated a quarter turn,
            // so the same rightward drag produces a different world pan than the
            // yaw 45deg case above. cos90=0, sin90=1 => rightX=0, rightZ=-1.
            var pan = GestureMapper.DragToPan(100f, 0f, QuarterYaw, Zoom, ScreenHeight);

            Assert.That(pan.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(pan.Z, Is.EqualTo(100f * MetersPerPixel).Within(0.0001f));

            // And it genuinely differs from the yaw 45deg result.
            var isoPan = GestureMapper.DragToPan(100f, 0f, IsoYaw, Zoom, ScreenHeight);
            Assert.That(pan.X, Is.Not.EqualTo(isoPan.X).Within(0.0001f));
        }

        [Test]
        public void DragDistance_ScalesWithZoomLevel()
        {
            var zoomedOut = GestureMapper.DragToPan(100f, 0f, IsoYaw, 30f, ScreenHeight);
            var zoomedIn = GestureMapper.DragToPan(100f, 0f, IsoYaw, 6f, ScreenHeight);

            // The same finger movement covers more world when zoomed out.
            Assert.That(System.Math.Abs(zoomedOut.X), Is.GreaterThan(System.Math.Abs(zoomedIn.X)));
        }

        [Test]
        public void PinchApart_ZoomsIn()
        {
            var zoomDelta = GestureMapper.PinchToZoom(100f, Zoom, ScreenHeight);

            Assert.That(zoomDelta, Is.EqualTo(-100f * MetersPerPixel).Within(0.0001f));
        }

        [Test]
        public void ZeroOrNegativeScreenHeight_Throws()
        {
            Assert.That(() => GestureMapper.DragToPan(1f, 1f, IsoYaw, Zoom, 0f), Throws.ArgumentException);
            Assert.That(() => GestureMapper.PinchToZoom(1f, Zoom, -5f), Throws.ArgumentException);
        }

        [Test]
        public void TwistToRotation_ClockwiseTwist_ProducesClockwiseRotation()
        {
            // #203 sign convention: a clockwise two-finger twist (positive delta)
            // rotates the camera clockwise (positive yaw delta). Same-sign.
            var rotation = GestureMapper.TwistToRotation(12f);

            Assert.That(rotation, Is.GreaterThan(0f));
            Assert.That(rotation, Is.EqualTo(12f * GestureMapper.TwistRotationSensitivity).Within(0.0001f));
        }

        [Test]
        public void TwistToRotation_CounterClockwiseTwist_ProducesCounterClockwiseRotation()
        {
            var rotation = GestureMapper.TwistToRotation(-12f);

            Assert.That(rotation, Is.LessThan(0f));
            Assert.That(rotation, Is.EqualTo(-12f * GestureMapper.TwistRotationSensitivity).Within(0.0001f));
        }
    }
}
