using System.Linq;
using System.Reflection;
using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class CameraRigConfigTests
    {
        [Test]
        public void DocumentedFixedCameraConstants()
        {
            // #21/#203: pitch and projection stay fixed. Yaw is no longer a
            // config constant here - it is mutable state on CameraController.
            Assert.That(CameraRigConfig.PitchDegrees, Is.EqualTo(45f));
            Assert.That(CameraRigConfig.Orthographic, Is.True);
            Assert.That(CameraRigConfig.RigDistance, Is.GreaterThan(0f));
        }

        [Test]
        public void PitchProjectionAndDistance_StayFixedImmutableConsts()
        {
            // Guard (#203): pitch, orthographic projection and rig distance
            // remain compile-time constants with no writable code path. Only
            // yaw became mutable, and it moved off this type entirely - there
            // must be no YawDegrees constant left to accidentally rely on.
            var type = typeof(CameraRigConfig);

            Assert.That(type.IsAbstract && type.IsSealed, Is.True, "CameraRigConfig must be a static class");

            var constFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => f.Name)
                .ToList();
            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.PitchDegrees)));
            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.Orthographic)));
            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.RigDistance)));
            Assert.That(constFields, Does.Not.Contain("YawDegrees"),
                "Yaw is no longer a fixed config constant (#203)");

            var writableFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => !f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.Name)
                .ToList();
            Assert.That(writableFields, Is.Empty, "No mutable fields allowed");

            var settableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .ToList();
            Assert.That(settableProperties, Is.Empty, "No settable properties allowed");
        }

        [Test]
        public void Rotation_IsNowPossible_ViaCameraController()
        {
            // Companion to the guard above (#203): the deliberate immovability
            // of #21 is reopened for yaw - the camera CAN now rotate freely.
            var controller = CameraController.ForStartingNeighborhood();
            var before = controller.Yaw;

            controller.Rotate(37f);

            Assert.That(controller.Yaw, Is.EqualTo(before + 37f));
        }
    }
}
