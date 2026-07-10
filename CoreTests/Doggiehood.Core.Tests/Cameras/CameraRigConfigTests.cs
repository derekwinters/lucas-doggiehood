using System.Linq;
using System.Reflection;
using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class CameraRigConfigTests
    {
        [Test]
        public void DocumentedIsometricAngleConstants()
        {
            // #21: fixed isometric/angled top-down view. The EditMode suite
            // asserts the scene camera matches these same constants.
            Assert.That(CameraRigConfig.PitchDegrees, Is.EqualTo(45f));
            Assert.That(CameraRigConfig.YawDegrees, Is.EqualTo(45f));
            Assert.That(CameraRigConfig.Orthographic, Is.True);
        }

        [Test]
        public void AngleIsFixed_NoCodePathCanChangeIt()
        {
            // Guard (#21): the config must stay a static class of constants —
            // no public writable state or methods that could enable a free
            // orbit/rotation camera.
            var type = typeof(CameraRigConfig);

            Assert.That(type.IsAbstract && type.IsSealed, Is.True, "CameraRigConfig must be a static class");

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
    }
}
