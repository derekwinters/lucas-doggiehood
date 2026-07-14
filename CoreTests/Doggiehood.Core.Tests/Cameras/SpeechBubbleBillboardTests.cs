using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class SpeechBubbleBillboardTests
    {
        [Test]
        public void FacingMatchesTheFixedCameraRigAngles_WithZeroRoll()
        {
            // #148 follow-up: the speech bubble must always face the
            // camera. The rig is orthographic and, by contract, no code
            // path ever rotates it (CameraRigConfig), so "face the camera"
            // reduces to one world orientation: the rig's own fixed
            // pitch/yaw with zero roll — independent of where the camera or
            // the dog stands. This class is the single Core source of that
            // orientation for the Unity layer to apply.
            Assert.That(SpeechBubbleBillboard.PitchDegrees,
                Is.EqualTo(CameraRigConfig.PitchDegrees),
                "billboard pitch must track the fixed rig pitch");
            Assert.That(SpeechBubbleBillboard.YawDegrees,
                Is.EqualTo(CameraRigConfig.YawDegrees),
                "billboard yaw must track the fixed rig yaw");
            Assert.That(SpeechBubbleBillboard.RollDegrees, Is.EqualTo(0f),
                "a billboard never rolls — the bubble stays upright in view");
        }
    }
}
