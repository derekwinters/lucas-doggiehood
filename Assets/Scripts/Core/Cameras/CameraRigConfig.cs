namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The fixed isometric/angled top-down camera (#21) — SimCity/Animal
    /// Crossing spirit: shows house facades and roofs, keeps dogs easy to
    /// tap. Deliberately a static class of constants: there must be no code
    /// path that rotates the camera or switches to a free orbit view.
    /// </summary>
    public static class CameraRigConfig
    {
        public const float PitchDegrees = 45f;
        public const float YawDegrees = 45f;
        public const bool Orthographic = true;

        /// <summary>Distance the camera sits back along its view direction —
        /// cosmetic for an orthographic camera, but keeps near/far planes sane.</summary>
        public const float RigDistance = 60f;
    }
}
