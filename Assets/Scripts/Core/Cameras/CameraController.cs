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

        private const float BoundsMargin = 12f;

        public WorldBounds Bounds { get; }
        public GridPoint Position { get; private set; }
        public float Zoom { get; private set; }

        public CameraController(WorldBounds bounds, GridPoint startPosition, float startZoom)
        {
            Bounds = bounds;
            Position = new GridPoint(bounds.ClampX(startPosition.X), bounds.ClampZ(startPosition.Z));
            Zoom = ClampZoom(startZoom);
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

        private static float ClampZoom(float zoom)
        {
            return zoom < MinZoom ? MinZoom : (zoom > MaxZoom ? MaxZoom : zoom);
        }
    }
}
