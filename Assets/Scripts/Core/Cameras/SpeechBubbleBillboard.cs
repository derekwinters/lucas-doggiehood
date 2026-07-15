namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The one world orientation that makes a speech bubble face the camera
    /// (#148). Because the rig is orthographic and no code path ever
    /// rotates it (see <see cref="CameraRigConfig"/>), "billboard toward
    /// the camera" does not depend on the camera's or the dog's position —
    /// it is simply the rig's fixed pitch/yaw with zero roll. The Unity
    /// layer applies these angles to the bubble every frame so the dog's
    /// own facing never leaks into the bubble.
    /// </summary>
    public static class SpeechBubbleBillboard
    {
        public const float PitchDegrees = CameraRigConfig.PitchDegrees;
        public const float YawDegrees = CameraRigConfig.YawDegrees;
        public const float RollDegrees = 0f;
    }
}
