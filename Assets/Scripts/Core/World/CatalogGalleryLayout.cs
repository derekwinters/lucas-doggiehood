using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Layout math for the #126 editor-only debug gallery: every
    /// <see cref="HouseModelCatalog"/> model in a row along +X, each
    /// annotated with its door marker, walkway placeholder, and fence
    /// placeholder. Lives in Core so the numbers are dotnet-testable and
    /// come from the exact APIs the game path uses
    /// (<see cref="HouseModel.FrontDoorWorldPosition"/>, the
    /// target-footprint / MaxFootprint scaling rule) — the Editor builder
    /// renders these entries verbatim and adds no math of its own.
    /// </summary>
    public static class CatalogGalleryLayout
    {
        /// <summary>Length of the walkway placeholder line, from the door
        /// straight out the front. Since #128 real walkways exist, and
        /// this reuses their exact length — door (on the facade,
        /// FrontSetback beyond the sidewalk's outer edge) to the sidewalk
        /// CENTERLINE — so the gallery preview matches the game even
        /// though the gallery itself has no streets to attach to.</summary>
        public const float WalkwayLength = HousePlacement.FrontSetback + WorldDimensions.SidewalkWidth / 2f;

        /// <summary>Gallery yaw: 0 leaves the model-local -Z front facade
        /// facing world -Z, so a viewer south of the row sees every
        /// front door.</summary>
        public const float GalleryYawDegrees = 0f;

        public static IReadOnlyList<CatalogGalleryEntry> Compute(float targetFootprint, float spacing)
        {
            if (targetFootprint <= 0f)
            {
                throw new ArgumentException("Target footprint must be positive.", nameof(targetFootprint));
            }

            if (spacing <= 0f)
            {
                throw new ArgumentException("Spacing must be positive.", nameof(spacing));
            }

            var entries = new List<CatalogGalleryEntry>();
            for (var i = 0; i < HouseModelCatalog.Models.Count; i++)
            {
                var model = HouseModelCatalog.Models[i];
                var position = new GridPoint(i * spacing, 0f);
                var scale = targetFootprint / model.MaxFootprint;
                var door = model.FrontDoorWorldPosition(position, GalleryYawDegrees, scale);

                entries.Add(new CatalogGalleryEntry(
                    model,
                    position,
                    GalleryYawDegrees,
                    scale,
                    door,
                    walkwayStart: door,
                    walkwayEnd: new GridPoint(door.X, door.Z - WalkwayLength),
                    fenceMin: new GridPoint(
                        position.X - scale * model.FootprintX / 2f,
                        position.Z - scale * model.FootprintZ / 2f),
                    fenceMax: new GridPoint(
                        position.X + scale * model.FootprintX / 2f,
                        position.Z + scale * model.FootprintZ / 2f)));
            }

            return entries;
        }
    }
}
