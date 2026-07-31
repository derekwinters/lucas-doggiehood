using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The axis-aligned world-space rectangle (meters) covering every tile
    /// placed in a <see cref="TileMap"/> (#373): each tile spans
    /// <see cref="WorldDimensions.TileSize"/> centred on
    /// <see cref="TileGeometry.CenterOf"/>, so the extent runs a half-tile
    /// beyond the outermost tile centres on each axis. This is the single
    /// map-derived basis the ground plane grows to cover and the camera pan
    /// bounds recompute from when a zone is unlocked, so a new zone is neither
    /// floating over void nor out of pan reach.
    /// </summary>
    public readonly struct MapExtent
    {
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public MapExtent(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) / 2f;
        public float CenterZ => (MinZ + MaxZ) / 2f;

        /// <summary>The extent covering every tile currently placed in
        /// <paramref name="map"/> — always at least the single seeded
        /// origin tile, so it is never empty.</summary>
        public static MapExtent Covering(TileMap map)
        {
            float half = WorldDimensions.TileSize / 2f;
            bool seeded = false;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;

            foreach (KeyValuePair<TileCoordinate, TileType> tile in map.Tiles)
            {
                var center = TileGeometry.CenterOf(tile.Key);
                float tileMinX = center.X - half;
                float tileMaxX = center.X + half;
                float tileMinZ = center.Z - half;
                float tileMaxZ = center.Z + half;

                if (!seeded)
                {
                    minX = tileMinX;
                    maxX = tileMaxX;
                    minZ = tileMinZ;
                    maxZ = tileMaxZ;
                    seeded = true;
                    continue;
                }

                if (tileMinX < minX) minX = tileMinX;
                if (tileMaxX > maxX) maxX = tileMaxX;
                if (tileMinZ < minZ) minZ = tileMinZ;
                if (tileMaxZ > maxZ) maxZ = tileMaxZ;
            }

            return new MapExtent(minX, maxX, minZ, maxZ);
        }
    }
}
