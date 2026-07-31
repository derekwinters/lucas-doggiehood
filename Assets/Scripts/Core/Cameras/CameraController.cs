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

        /// <summary>Breathing room (meters) added around the map's tile
        /// coverage when deriving the pan bounds (#20, #373), so the player
        /// can pan a little past the outermost tiles rather than being pinned
        /// exactly to their edges.</summary>
        private const float BoundsMargin = 12f;

        public WorldBounds Bounds { get; private set; }
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
            // The initial pan bounds derive from the same live-map path an
            // unlock later grows them by (#373): the starting map is just the
            // seeded FourWay intersection at the origin.
            var startingMap = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            return new CameraController(BoundsForMap(startingMap), NeighborhoodLayout.Intersection, DefaultZoom);
        }

        /// <summary>
        /// Recomputes the pan <see cref="Bounds"/> from the live tile
        /// <paramref name="map"/> (#373): after a zone unlock extends the map
        /// (e.g. the #360 north cul-de-sac), the bounds grow to cover the new
        /// tiles so <see cref="Pan"/>/<see cref="FocusOn"/> can reach them.
        /// The current <see cref="Position"/> is re-clamped into the new
        /// bounds so it never sits outside them.
        /// </summary>
        public void RecomputeBoundsFromMap(TileMap map)
        {
            Bounds = BoundsForMap(map);
            Position = new GridPoint(Bounds.ClampX(Position.X), Bounds.ClampZ(Position.Z));
        }

        private static WorldBounds BoundsForMap(TileMap map)
        {
            var extent = MapExtent.Covering(map);
            return new WorldBounds(
                extent.MinX - BoundsMargin, extent.MaxX + BoundsMargin,
                extent.MinZ - BoundsMargin, extent.MaxZ + BoundsMargin);
        }

        public void Pan(float deltaX, float deltaZ)
        {
            Position = new GridPoint(
                Bounds.ClampX(Position.X + deltaX),
                Bounds.ClampZ(Position.Z + deltaZ));
        }

        /// <summary>Moves the camera to an absolute target point, clamped to
        /// the world bounds (#165). Unlike <see cref="Pan"/> (a relative
        /// delta), this recentres on a place — the dog profile's Home button
        /// flies the camera to that dog's house.</summary>
        public void FocusOn(GridPoint target)
        {
            Position = new GridPoint(Bounds.ClampX(target.X), Bounds.ClampZ(target.Z));
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
