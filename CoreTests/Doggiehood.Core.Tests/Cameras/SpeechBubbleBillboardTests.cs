using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class SpeechBubbleBillboardTests
    {
        [Test]
        public void FacingMatchesTheFixedCameraRigAngles_WithZeroRoll()
        {
            // #148 follow-up: the speech bubble must face the camera. The
            // rig is orthographic, so "face the camera" reduces to one world
            // orientation: the rig's pitch and the starting yaw with zero
            // roll. Pitch stays the fixed rig pitch; yaw is the fixed default
            // yaw (bubble-facing under the free rotation of #203 is deferred
            // with #181). This class is the single Core source of that
            // orientation for the Unity layer to apply.
            Assert.That(SpeechBubbleBillboard.PitchDegrees,
                Is.EqualTo(CameraRigConfig.PitchDegrees),
                "billboard pitch must track the fixed rig pitch");
            Assert.That(SpeechBubbleBillboard.YawDegrees,
                Is.EqualTo(CameraController.DefaultYaw),
                "billboard yaw must track the fixed starting yaw");
            Assert.That(SpeechBubbleBillboard.RollDegrees, Is.EqualTo(0f),
                "a billboard never rolls — the bubble stays upright in view");
        }
    }
}
