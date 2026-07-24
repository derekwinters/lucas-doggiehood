using Doggiehood.Core.World;

namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// Camera navigation decisions (#20): pan clamped to the neighborhood
    /// bounds, zoom clamped between min/max. Zoom is the orthographic
    /// half-height in meters (smaller = closer). The Unity layer feeds
    /// gesture deltas in and applies Position/Zoom to the actual camera.
    /// </summary>
    public sealed class CameraController
    {
        public const float MinZoom = 6f;
        public const float MaxZoom = 30f;
        public const float DefaultZoom = 18f;

        /// <summary>Starting yaw (#203): the old fixed isometric angle,
        /// now the initial value of free, mutable rotation.</summary>
        public const float DefaultYaw = 45f;

        private const float BoundsMargin = 12f;

        public WorldBounds Bounds { get; }
        public GridPoint Position { get; private set; }
        public float Zoom { get; private set; }

        /// <summary>Camera yaw in degrees (#203). Free continuous rotation —
        /// never clamped or snapped, unlike Position (bounds) or Zoom (min/max).</summary>
        public float Yaw { get; private set; }

        public CameraController(WorldBounds bounds, GridPoint startPosition, float startZoom)
        {
            Bounds = bounds;
            Position = new GridPoint(bounds.ClampX(startPosition.X), bounds.ClampZ(startPosition.Z));
            Zoom = ClampZoom(startZoom);
            Yaw = DefaultYaw;
        }

        public static CameraController ForStartingNeighborhood()
        {
            var extent = NeighborhoodLayout.LotDistanceFromCenter + BoundsMargin;
            var bounds = new WorldBounds(-extent, extent, -extent, extent);
            return new CameraController(bounds, NeighborhoodLayout.Intersection, DefaultZoom);
        }

        public void Pan(float deltaX, float deltaZ)
        {
            Position = new GridPoint(
                Bounds.ClampX(Position.X + deltaX),
                Bounds.ClampZ(Position.Z + deltaZ));
        }

        public void ZoomBy(float delta)
        {
            Zoom = ClampZoom(Zoom + delta);
        }

        /// <summary>Rotates the camera yaw by the delta (#203). Unclamped:
        /// rotation is free and continuous, with no snapping to fixed angles.</summary>
        public void Rotate(float deltaDegrees)
        {
            Yaw += deltaDegrees;
        }

        private static float ClampZoom(float zoom)
        {
            return zoom < MinZoom ? MinZoom : (zoom > MaxZoom ? MaxZoom : zoom);
        }
    }
}
