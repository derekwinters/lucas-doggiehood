using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class CameraFacingTests
    {
        [Test]
        public void Resolve_TracksTheLiveCameraYaw_WithFixedPitchAndZeroRoll()
        {
            // #266: a world marker that must read head-on toward the player
            // faces the *live* camera yaw (CameraController.Yaw), not the old
            // fixed 45° starting yaw. The rig is orthographic, so "face the
            // camera" is the fixed rig pitch + the current yaw with zero roll
            // (the same decomposition the old fixed billboard used), just
            // sourced from live yaw. This is the single Core seam every
            // camera-facing marker resolves its orientation through.
            const float liveYaw = 120f;

            var facing = CameraFacing.Resolve(liveYaw);

            Assert.That(facing.YawDegrees, Is.EqualTo(liveYaw),
                "marker yaw must follow the live camera yaw");
            Assert.That(facing.PitchDegrees, Is.EqualTo(CameraRigConfig.PitchDegrees),
                "marker pitch stays the fixed orthographic rig pitch");
            Assert.That(facing.RollDegrees, Is.EqualTo(0f),
                "a camera-facing marker never rolls — it stays upright in view");
        }

        [Test]
        public void Resolve_AtTheDefaultYaw_ReproducesTheOriginalFixedFacing()
        {
            // Regression guard (#266): at the default starting yaw the marker
            // must resolve to exactly the pre-#203 fixed facing, so on-launch
            // appearance is unchanged.
            var facing = CameraFacing.Resolve(CameraController.DefaultYaw);

            Assert.That(facing.PitchDegrees, Is.EqualTo(CameraRigConfig.PitchDegrees));
            Assert.That(facing.YawDegrees, Is.EqualTo(CameraController.DefaultYaw));
            Assert.That(facing.RollDegrees, Is.EqualTo(0f));
        }
    }
}
