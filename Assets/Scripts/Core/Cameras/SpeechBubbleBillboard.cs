namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The world orientation that makes a speech bubble face the camera
    /// (#148). Because the rig is orthographic, "billboard toward the camera"
    /// is simply the rig's pitch/yaw with zero roll, independent of the
    /// camera's or the dog's position. The yaw here is the fixed starting
    /// yaw (<see cref="CameraController.DefaultYaw"/>); keeping bubbles facing
    /// the camera under the free twist rotation added in #203 is part of the
    /// visibility work deferred with #181. The Unity layer applies these
    /// angles to the bubble every frame so the dog's own facing never leaks in.
    /// </summary>
    public static class SpeechBubbleBillboard
    {
        public const float PitchDegrees = CameraRigConfig.PitchDegrees;
        public const float YawDegrees = CameraController.DefaultYaw;
        public const float RollDegrees = 0f;
    }
}
