namespace Doggiehood.Core.World
{
    /// <summary>
    /// One annotated row of the #126 debug catalog gallery: where a
    /// <see cref="HouseModel"/> sits in the gallery, how it is scaled and
    /// yawed, and the annotation geometry (door marker, walkway
    /// placeholder, fence placeholder) — all derived from the same Core
    /// APIs the game path uses so the Editor rendering can never drift
    /// from reality. Walkways (#128) and fences (#129) don't exist yet;
    /// their placeholders here are authoring aids only.
    /// </summary>
    public sealed class CatalogGalleryEntry
    {
        public HouseModel Model { get; }

        /// <summary>Ground-plane position the model is placed at.</summary>
        public GridPoint Position { get; }

        /// <summary>World yaw (Unity convention). 0 in the gallery: the
        /// model's local -Z front facade faces world -Z (the viewer).</summary>
        public float YawDegrees { get; }

        /// <summary>Uniform scale, by the same rule WorldBuilder applies:
        /// target footprint / <see cref="HouseModel.MaxFootprint"/>.</summary>
        public float UniformScale { get; }

        /// <summary>Door marker position — exactly
        /// <see cref="HouseModel.FrontDoorWorldPosition"/> for this entry's
        /// placement.</summary>
        public GridPoint DoorPosition { get; }

        /// <summary>Walkway placeholder: starts at the door...</summary>
        public GridPoint WalkwayStart { get; }

        /// <summary>...and runs straight out the front toward where the
        /// sidewalk would be.</summary>
        public GridPoint WalkwayEnd { get; }

        /// <summary>Fence placeholder: min corner of the scaled footprint
        /// rectangle.</summary>
        public GridPoint FenceMin { get; }

        /// <summary>Fence placeholder: max corner of the scaled footprint
        /// rectangle.</summary>
        public GridPoint FenceMax { get; }

        public CatalogGalleryEntry(HouseModel model, GridPoint position, float yawDegrees,
            float uniformScale, GridPoint doorPosition, GridPoint walkwayStart, GridPoint walkwayEnd,
            GridPoint fenceMin, GridPoint fenceMax)
        {
            Model = model;
            Position = position;
            YawDegrees = yawDegrees;
            UniformScale = uniformScale;
            DoorPosition = doorPosition;
            WalkwayStart = walkwayStart;
            WalkwayEnd = walkwayEnd;
            FenceMin = fenceMin;
            FenceMax = fenceMax;
        }
    }
}
