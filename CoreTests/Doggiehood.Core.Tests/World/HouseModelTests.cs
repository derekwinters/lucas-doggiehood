using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class HouseModelTests
    {
        // Door convention (gallery pass 1, 2026-07-14): Derek's measured
        // doors are NOT on the facade plane — porches recess them, and
        // building-type-b's door sits near its footprint center — so the
        // door is a full 2D model-local ground-plane point now, not a
        // scalar along the z = -FootprintZ / 2 facade. The front-facade
        // convention itself (kit models face model-local -Z) is unchanged.
        [Test]
        public void FrontDoorLocalPosition_IsTheAuthored2DModelLocalPoint()
        {
            var model = new HouseModel("test-house", 2f, 1.5f, 0.4f, -0.3f);

            Assert.That(model.FrontDoorLocalPosition.X, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(model.FrontDoorLocalPosition.Z, Is.EqualTo(-0.3f).Within(0.0001f));
        }

        [Test]
        public void FrontDoorWorldPosition_AtYawZero_ScalesAndTranslatesTheLocalDoor()
        {
            // Yaw 0 leaves the model front on world -Z: the door is the
            // local door point, uniformly scaled, moved to the lot.
            var model = new HouseModel("test-house", 2f, 2f, 0.5f, -1f);

            var door = model.FrontDoorWorldPosition(new GridPoint(10f, 20f), 0f, 3f);

            Assert.That(door.X, Is.EqualTo(11.5f).Within(0.0001f));
            Assert.That(door.Z, Is.EqualTo(17f).Within(0.0001f));
        }

        [Test]
        public void FrontDoorWorldPosition_AtYaw90_FrontFacesWest()
        {
            // Unity yaw convention (clockwise seen from above): 90 degrees
            // turns local +Z to world +X, so the -Z front faces world -X.
            // Local door (0.5, -1) -> rotated (-1, -0.5) -> scaled by 2 and
            // moved to the lot.
            var model = new HouseModel("test-house", 2f, 2f, 0.5f, -1f);

            var door = model.FrontDoorWorldPosition(new GridPoint(10f, 20f), 90f, 2f);

            Assert.That(door.X, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(door.Z, Is.EqualTo(19f).Within(0.0001f));
        }

        [Test]
        public void FrontDoorWorldPosition_AtYaw180_FrontFacesNorth()
        {
            // Local door (0.5, -1) -> rotated (-0.5, 1) at unit scale.
            var model = new HouseModel("test-house", 2f, 2f, 0.5f, -1f);

            var door = model.FrontDoorWorldPosition(new GridPoint(0f, 0f), 180f, 1f);

            Assert.That(door.X, Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(door.Z, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
