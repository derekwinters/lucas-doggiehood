namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The angled top-down camera (#21) — SimCity/Animal Crossing spirit:
    /// shows house facades and roofs, keeps dogs easy to tap. Pitch and the
    /// orthographic projection are fixed constants here. Yaw is no longer
    /// fixed (#203): it is free, mutable rotation state on CameraController,
    /// driven by a two-finger twist gesture.
    /// </summary>
    public static class CameraRigConfig
    {
        public const float PitchDegrees = 45f;
        public const bool Orthographic = true;

        /// <summary>Distance the camera sits back along its view direction —
        /// cosmetic for an orthographic camera, but keeps near/far planes sane.</summary>
        public const float RigDistance = 60f;
    }
}
