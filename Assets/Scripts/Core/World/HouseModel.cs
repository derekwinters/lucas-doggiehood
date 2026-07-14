using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Authored data for one Kenney City Kit Suburban house model (#125).
    /// The kit FBX files are single fused meshes with no queryable "door"
    /// node, so the semantic positions live here as model-local,
    /// scale-independent numbers that everything downstream (door world
    /// position, walkway start, fence gate gap, dog walks-to-door) derives
    /// from.
    ///
    /// Front-facade convention: the kit models face model-local -Z — the
    /// fact WorldBuilder.HouseModelYawOffsetDegrees (180) already encodes
    /// from Derek's Editor screenshot evidence. The front facade is
    /// therefore the local plane z = -FootprintZ / 2, and
    /// <see cref="FrontDoorOffset"/> runs along local +X on that plane
    /// (0 = horizontally centered).
    /// </summary>
    public sealed class HouseModel
    {
        /// <summary>Resources load key of the model, e.g. "building-type-b".</summary>
        public string ModelName { get; }

        /// <summary>Model-local footprint size along local X (units).</summary>
        public float FootprintX { get; }

        /// <summary>Model-local footprint size along local Z (units).</summary>
        public float FootprintZ { get; }

        /// <summary>Door position along the front facade (local +X,
        /// 0 = centered), in model-local units.</summary>
        public float FrontDoorOffset { get; }

        public HouseModel(string modelName, float footprintX, float footprintZ, float frontDoorOffset)
        {
            ModelName = modelName;
            FootprintX = footprintX;
            FootprintZ = footprintZ;
            FrontDoorOffset = frontDoorOffset;
        }

        /// <summary>The larger horizontal extent — what uniform scaling
        /// targets (WorldBuilder scales so this lands on its 8m
        /// HouseTargetFootprint).</summary>
        public float MaxFootprint
        {
            get { return Math.Max(FootprintX, FootprintZ); }
        }

        /// <summary>The door point in model-local ground-plane coordinates:
        /// on the -Z front facade, <see cref="FrontDoorOffset"/> along +X.</summary>
        public GridPoint FrontDoorLocalPosition
        {
            get { return new GridPoint(FrontDoorOffset, -FootprintZ / 2f); }
        }

        /// <summary>
        /// Where the front door sits on the world ground plane for a house
        /// placed at <paramref name="lotPosition"/> with the given world yaw
        /// and uniform scale — pure geometry, no engine. Yaw follows Unity's
        /// convention (degrees, clockwise seen from above, 0 = local axes
        /// aligned with world axes), so the value WorldBuilder computes for
        /// the model transform can be passed straight through.
        /// </summary>
        public GridPoint FrontDoorWorldPosition(GridPoint lotPosition, float yawDegrees, float uniformScale)
        {
            var local = FrontDoorLocalPosition;
            var radians = yawDegrees * Math.PI / 180.0;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);

            // Unity yaw rotates +Z toward +X (clockwise from above).
            var rotatedX = local.X * cos + local.Z * sin;
            var rotatedZ = -local.X * sin + local.Z * cos;

            return new GridPoint(
                lotPosition.X + uniformScale * rotatedX,
                lotPosition.Z + uniformScale * rotatedZ);
        }
    }
}
