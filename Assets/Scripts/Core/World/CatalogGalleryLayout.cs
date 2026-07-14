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
        /// <summary>Where the walkway placeholder ENDS: this far beyond
        /// the scaled front facade plane, straight out the front. Reuses
        /// the real #128 endpoint — the in-game walkway ends on the
        /// sidewalk CENTERLINE, FrontSetback + SidewalkWidth / 2 past the
        /// facade — so the gallery preview matches the game even though
        /// the gallery has no streets to attach to. An endpoint rule
        /// rather than a length since gallery pass 1 (2026-07-14): the
        /// authored doors are recessed behind the facade, so each model's
        /// walkway run is longer than 3.75m by its own recess depth.</summary>
        public const float WalkwayEndBeyondFacade =
            HousePlacement.FrontSetback + WorldDimensions.SidewalkWidth / 2f;

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
                var scaledFacadeZ = position.Z - scale * model.FootprintZ / 2f;

                entries.Add(new CatalogGalleryEntry(
                    model,
                    position,
                    GalleryYawDegrees,
                    scale,
                    door,
                    walkwayStart: door,
                    walkwayEnd: new GridPoint(door.X, scaledFacadeZ - WalkwayEndBeyondFacade),
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
