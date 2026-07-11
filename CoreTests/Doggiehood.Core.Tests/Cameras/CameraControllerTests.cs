using Doggiehood.Core.Cameras;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class CameraControllerTests
    {
        private static CameraController NewController()
        {
            return CameraController.ForStartingNeighborhood();
        }

        [Test]
        public void StartsCenteredOnTheIntersection()
        {
            var camera = NewController();

            Assert.That(camera.Position, Is.EqualTo(NeighborhoodLayout.Intersection));
        }

        [Test]
        public void Pan_MovesThePositionByTheDelta()
        {
            var camera = NewController();

            camera.Pan(3f, -2f);

            Assert.That(camera.Position.X, Is.EqualTo(3f));
            Assert.That(camera.Position.Z, Is.EqualTo(-2f));
        }

        [Test]
        public void Pan_IsClampedToTheWorldBounds()
        {
            var camera = NewController();

            camera.Pan(10000f, 10000f);
            Assert.That(camera.Position.X, Is.EqualTo(camera.Bounds.MaxX));
            Assert.That(camera.Position.Z, Is.EqualTo(camera.Bounds.MaxZ));

            camera.Pan(-20000f, -20000f);
            Assert.That(camera.Position.X, Is.EqualTo(camera.Bounds.MinX));
            Assert.That(camera.Position.Z, Is.EqualTo(camera.Bounds.MinZ));
        }

        [Test]
        public void WorldBounds_EncloseEveryHouseLot()
        {
            var camera = NewController();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(lot.Position.X, Is.InRange(camera.Bounds.MinX, camera.Bounds.MaxX));
                Assert.That(lot.Position.Z, Is.InRange(camera.Bounds.MinZ, camera.Bounds.MaxZ));
            }
        }

        [Test]
        public void ZoomBy_ChangesTheZoomLevel()
        {
            var camera = NewController();
            var before = camera.Zoom;

            camera.ZoomBy(-2f);

            Assert.That(camera.Zoom, Is.EqualTo(before - 2f));
        }

        [Test]
        public void ZoomBy_IsClampedBetweenMinAndMax()
        {
            var camera = NewController();

            camera.ZoomBy(-10000f);
            Assert.That(camera.Zoom, Is.EqualTo(CameraController.MinZoom));

            camera.ZoomBy(10000f);
            Assert.That(camera.Zoom, Is.EqualTo(CameraController.MaxZoom));
        }

        [Test]
        public void InitialZoom_IsWithinTheClampRange()
        {
            var camera = NewController();

            Assert.That(camera.Zoom, Is.InRange(CameraController.MinZoom, CameraController.MaxZoom));
        }
    }
}
