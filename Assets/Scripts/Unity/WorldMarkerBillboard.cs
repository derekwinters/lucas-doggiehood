using Doggiehood.Core.Cameras;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Unity apply-seam for camera-facing world markers (#266): rotates a
    /// marker transform to the orientation Core's <see cref="CameraFacing"/>
    /// resolves from the live camera yaw, so speech bubbles and the
    /// map-expansion lock icon stay head-on at every rotation instead of being
    /// pinned to the pre-#203 fixed 45° yaw. Falls back to
    /// <see cref="CameraController.DefaultYaw"/> when no <see cref="CameraRig"/>
    /// is in the scene (isolated tests, or before the rig exists), reproducing
    /// the original fixed facing. Pure rotation — no other transform state is
    /// touched, so colliders and tap routing are unaffected.
    /// </summary>
    internal static class WorldMarkerBillboard
    {
        public static void Face(Transform marker, CameraRig rig)
        {
            var yaw = rig != null ? rig.Controller.Yaw : CameraController.DefaultYaw;
            var facing = CameraFacing.Resolve(yaw);
            marker.rotation = Quaternion.Euler(
                facing.PitchDegrees, facing.YawDegrees, facing.RollDegrees);
        }
    }
}
