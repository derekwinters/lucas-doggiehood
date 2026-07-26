namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The world orientation that makes a flat, camera-facing world marker —
    /// a speech bubble (#148), the map-expansion lock icon (#178), and any
    /// future marker meant to read head-on toward the player — face the
    /// camera at any rotation (#266). The rig is orthographic with a fixed
    /// pitch and never rolls, so "face the camera" is fully determined by the
    /// live camera yaw (<see cref="CameraController.Yaw"/>): pitch and roll
    /// are constant and only the yaw tracks the camera. This is the single
    /// shared seam every camera-facing marker resolves its orientation
    /// through, rather than each marker re-deriving the billboard math.
    ///
    /// Before the free continuous rotation of #203, the yaw was the fixed 45°
    /// starting angle (<see cref="CameraController.DefaultYaw"/>); markers
    /// pinned to that constant skewed or went edge-on as the camera rotated
    /// away, which this fixes by sourcing the live yaw. The Unity layer applies
    /// the resolved angles to the marker every frame.
    /// </summary>
    public static class CameraFacing
    {
        public const float PitchDegrees = CameraRigConfig.PitchDegrees;
        public const float RollDegrees = 0f;

        /// <summary>Resolves the orientation a marker must take to face the
        /// camera at the given live yaw: the fixed rig pitch, that yaw, and
        /// zero roll.</summary>
        public static MarkerFacing Resolve(float cameraYaw)
        {
            return new MarkerFacing(PitchDegrees, cameraYaw, RollDegrees);
        }
    }

    /// <summary>The Euler angles (in degrees) a camera-facing world marker
    /// applies to read head-on toward the camera (#266).</summary>
    public readonly struct MarkerFacing
    {
        public MarkerFacing(float pitchDegrees, float yawDegrees, float rollDegrees)
        {
            PitchDegrees = pitchDegrees;
            YawDegrees = yawDegrees;
            RollDegrees = rollDegrees;
        }

        public float PitchDegrees { get; }
        public float YawDegrees { get; }
        public float RollDegrees { get; }
    }
}
