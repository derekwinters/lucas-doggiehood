namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// The angled top-down camera (#21) — SimCity/Animal Crossing spirit:
    /// shows house facades and roofs, keeps dogs easy to tap. Pitch and the
    /// orthographic projection are fixed constants here. Yaw is no longer
    /// fixed (#203): it is free, mutable rotation state on CameraController.
    ///
    /// The rig's *depth* placement — how far back the camera sits and where
    /// its clip planes go — is derived per-zoom rather than fixed, because a
    /// pitched camera looking at a flat ground plane sees a slab of view depth,
    /// not a plane: see <see cref="GroundDepthReach"/>.
    /// </summary>
    public static class CameraRigConfig
    {
        public const float PitchDegrees = 45f;
        public const bool Orthographic = true;

        /// <summary>Clearance (meters) kept in front of the nearest visible
        /// ground and behind the farthest — the slack the camera's set-back and
        /// far clip plane are built out from (see <see cref="RigDistanceFor"/>
        /// and <see cref="FarClipFor"/>). Cosmetic for an orthographic camera,
        /// which frames the same picture at any distance, but it is what keeps
        /// the whole frame inside the clip range.</summary>
        public const float RigDistance = 60f;

        /// <summary>The camera's near clip plane. Fixed, because
        /// <see cref="RigDistanceFor"/> moves the camera instead of moving this
        /// — Unity rejects a non-positive near plane, so the geometry has to be
        /// pushed in front of it rather than the plane pulled behind the
        /// geometry.</summary>
        public const float NearClipPlane = 0.3f;

        /// <summary>
        /// How far, along the view axis, the top and bottom edges of the frame
        /// sit from the focus point on the ground.
        ///
        /// The ground is tilted <see cref="PitchDegrees"/> away from the view
        /// axis, so the viewport does not cover a constant-depth slice of it. A
        /// point at the edge of the frame is <c>zoom / sin(pitch)</c> meters
        /// away across the ground (<paramref name="zoom"/> being the
        /// orthographic half-height), of which <c>cos(pitch)</c> projects onto
        /// the view axis — leaving <c>zoom / tan(pitch)</c> of depth. The
        /// visible ground therefore spans a slab
        /// <c>2 * GroundDepthReach(zoom)</c> deep, centred on the focus point,
        /// and that whole slab has to fit between the clip planes.
        /// </summary>
        public static float GroundDepthReach(float zoom)
        {
            var pitchRadians = PitchDegrees * System.Math.PI / 180.0;
            return (float)(zoom / System.Math.Tan(pitchRadians));
        }

        /// <summary>
        /// How far back along its view direction the camera sits from the focus
        /// point, at the given <paramref name="zoom"/>.
        ///
        /// This grows with the zoom, and must (#679). It used to be the flat
        /// <see cref="RigDistance"/> at every zoom, which meant that once the
        /// zoom passed ~60 the near half of the visible ground slab — the
        /// foreground, along the bottom of the screen — fell to a *negative*
        /// view depth, behind the camera, and was clipped away. The camera's
        /// clear colour showed through instead, as a hard-edged band along the
        /// bottom of the frame that grew as the player zoomed further out.
        /// Because #510/#524 grow <see cref="CameraController.MaxZoom"/> with
        /// the map, one tile of expansion was already enough to trigger it.
        /// Setting the camera back by the slab's own reach keeps the nearest
        /// visible ground a constant <see cref="RigDistance"/> in front of the
        /// camera at every zoom level.
        /// </summary>
        public static float RigDistanceFor(float zoom)
        {
            return RigDistance + GroundDepthReach(zoom);
        }

        /// <summary>The far clip plane for the given <paramref name="zoom"/>:
        /// past the far edge of the visible ground slab, with the same
        /// <see cref="RigDistance"/> of clearance kept at the near edge. Fixed
        /// far planes have the mirror-image failure of the near-plane bug
        /// (#679) — the Main scene's serialized 300 clipped the *distant* edge
        /// of the frame, the top of the screen, once the zoom passed 240.</summary>
        public static float FarClipFor(float zoom)
        {
            return RigDistanceFor(zoom) + GroundDepthReach(zoom) + RigDistance;
        }
    }
}
