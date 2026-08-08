using System;

namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The angled top-down camera (#21) — SimCity/Animal Crossing spirit:
    /// shows house facades and roofs, keeps dogs easy to tap. Pitch and the
    /// orthographic projection are fixed constants here. Yaw is no longer
    /// fixed (#203): it is free, mutable rotation state on CameraController,
    /// driven by a two-finger twist gesture.
    ///
    /// #679: the rig's placement in depth is no longer fixed either. Because
    /// the ground is pitched away from the view axis, the visible ground is a
    /// slab of view depth <see cref="GroundDepthReach"/> either side of the
    /// focus point, and that slab grows with the zoom. A pinned set-back
    /// therefore pushed the near half of the frame behind the camera once the
    /// zoom passed ~60m — the blank band along the bottom of the screen that
    /// four coverage-and-colour fixes (#536 → #558 → #570 → #611) could not
    /// touch, because nothing was under-covered: the ground was being clipped.
    /// The set-back and both clip planes are derived from the live zoom here
    /// instead, so the guarantee is owned by code rather than by whatever the
    /// Main scene happens to serialize.
    /// </summary>
    public static class CameraRigConfig
    {
        public const float PitchDegrees = 45f;
        public const bool Orthographic = true;

        /// <summary>The clearance (meters) held between the camera and the
        /// world it is looking at (#679). It is the distance from the lens to
        /// the <b>nearest visible ground</b> — held constant at every zoom by
        /// <see cref="RigDistanceFor"/> — and the same margin
        /// <see cref="FarClipFor"/> leaves past the far edge of the visible
        /// ground. Until #679 this was the camera's whole set-back, pinned
        /// regardless of zoom, which is what let the ground fall behind the
        /// near clip plane.</summary>
        public const float RigDistance = 60f;

        /// <summary>The camera's near clip plane (meters), owned here rather
        /// than left to the scene's serialized value (#679). Kept small: the
        /// clearance that actually matters is <see cref="RigDistance"/>, which
        /// holds the nearest visible ground far in front of this plane.</summary>
        public const float NearClipPlane = 0.3f;

        private static readonly float TangentOfPitch =
            (float)Math.Tan(PitchDegrees * Math.PI / 180.0);

        /// <summary>Half the view depth of the visible ground slab at
        /// <paramref name="zoom"/> (#679). A point at the top or bottom edge of
        /// the frame lies <c>zoom / sin(pitch)</c> metres away across the
        /// ground, of which <c>cos(pitch)</c> projects onto the view axis — so
        /// the ground the viewport covers spans <c>zoom / tan(pitch)</c> of
        /// view depth either side of the focus point. At the fixed 45° pitch
        /// that is simply the zoom itself.</summary>
        public static float GroundDepthReach(float zoom) => zoom / TangentOfPitch;

        /// <summary>How far back along its view direction the camera sits from
        /// its focus point at <paramref name="zoom"/> (#679): far enough that
        /// the near edge of the visible ground slab stays a constant
        /// <see cref="RigDistance"/> in front of the lens, at every zoom and so
        /// for every map size.</summary>
        public static float RigDistanceFor(float zoom) => RigDistance + GroundDepthReach(zoom);

        /// <summary>The camera's far clip plane (meters) at
        /// <paramref name="zoom"/> (#679): past the far edge of the visible
        /// ground slab, with the same <see cref="RigDistance"/> clearance the
        /// near edge gets. Derived rather than serialized, so growing the map
        /// (and with it <c>CameraController.MaxZoom</c>) can never push the
        /// distant edge of the ground through it.</summary>
        public static float FarClipFor(float zoom)
            => RigDistanceFor(zoom) + GroundDepthReach(zoom) + RigDistance;
    }
}
