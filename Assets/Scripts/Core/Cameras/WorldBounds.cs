namespace Doggiehood.Core.Cameras
{
    /// <summary>Axis-aligned pannable area on the ground plane (#20).</summary>
    public readonly struct WorldBounds
    {
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public WorldBounds(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float ClampX(float x)
        {
            return x < MinX ? MinX : (x > MaxX ? MaxX : x);
        }

        public float ClampZ(float z)
        {
            return z < MinZ ? MinZ : (z > MaxZ ? MaxZ : z);
        }
    }
}
