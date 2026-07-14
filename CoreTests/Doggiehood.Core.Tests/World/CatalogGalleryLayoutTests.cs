using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// The #126 debug-gallery layout: one row entry per catalog model,
    /// annotated (door marker, walkway placeholder, fence placeholder)
    /// purely from the same Core APIs the game path uses — the Editor
    /// builder renders these numbers verbatim, so the gallery can never
    /// drift from what the game would do.
    /// </summary>
    public class CatalogGalleryLayoutTests
    {
        private const float TargetFootprint = 8f;
        private const float Spacing = 12f;

        [Test]
        public void Compute_ReturnsOneEntryPerCatalogModel_InCatalogOrder()
        {
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            Assert.That(entries.Count, Is.EqualTo(HouseModelCatalog.Models.Count));
            Assert.That(entries.Select(e => e.Model.ModelName),
                Is.EqualTo(HouseModelCatalog.Models.Select(m => m.ModelName)));
        }

        [Test]
        public void Compute_PlacesEntriesInARowAlongX_SpacedApart()
        {
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            for (var i = 0; i < entries.Count; i++)
            {
                Assert.That(entries[i].Position.X, Is.EqualTo(i * Spacing).Within(0.0001f));
                Assert.That(entries[i].Position.Z, Is.EqualTo(0f).Within(0.0001f));
            }
        }

        [Test]
        public void Compute_UsesTheGameScalingRule_TargetFootprintOverMaxFootprint()
        {
            // Same rule WorldBuilder.BuildHouseModel applies:
            // uniform scale = HouseTargetFootprint / model.MaxFootprint.
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            foreach (var entry in entries)
            {
                Assert.That(entry.UniformScale,
                    Is.EqualTo(TargetFootprint / entry.Model.MaxFootprint).Within(0.0001f),
                    entry.Model.ModelName);
            }
        }

        [Test]
        public void Compute_DoorPosition_ComesFromFrontDoorWorldPosition_TheGamePathApi()
        {
            // Same-API guardrail: the annotated door must be exactly what
            // HouseModel.FrontDoorWorldPosition returns for the entry's
            // placement — no duplicated math allowed to drift.
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            foreach (var entry in entries)
            {
                var expected = entry.Model.FrontDoorWorldPosition(
                    entry.Position, entry.YawDegrees, entry.UniformScale);

                Assert.That(entry.DoorPosition.X, Is.EqualTo(expected.X).Within(0.0001f),
                    entry.Model.ModelName + " door X");
                Assert.That(entry.DoorPosition.Z, Is.EqualTo(expected.Z).Within(0.0001f),
                    entry.Model.ModelName + " door Z");
            }
        }

        [Test]
        public void Compute_FrontsFaceMinusZ_SoTheDoorSitsOnTheScaledFrontFacadePlane()
        {
            // Gallery convention: yaw 0, so the model-local front facade
            // plane (z = -FootprintZ / 2) lands at
            // entry.Z - scale * FootprintZ / 2 in world space — every door
            // marker must sit exactly on it.
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            foreach (var entry in entries)
            {
                Assert.That(entry.YawDegrees, Is.EqualTo(0f), entry.Model.ModelName + " yaw");
                Assert.That(entry.DoorPosition.Z,
                    Is.EqualTo(entry.Position.Z - entry.UniformScale * entry.Model.FootprintZ / 2f)
                        .Within(0.0001f),
                    entry.Model.ModelName + " door facade plane");
            }
        }

        [Test]
        public void Compute_WalkwayPlaceholder_RunsFromTheDoorStraightOutTheFront()
        {
            // #128 walkways don't exist yet; the placeholder is a marker
            // line from the door toward where the sidewalk would be —
            // straight out the front (world -Z at yaw 0) by WalkwayLength.
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            foreach (var entry in entries)
            {
                Assert.That(entry.WalkwayStart, Is.EqualTo(entry.DoorPosition),
                    entry.Model.ModelName + " walkway start");
                Assert.That(entry.WalkwayEnd.X, Is.EqualTo(entry.DoorPosition.X).Within(0.0001f),
                    entry.Model.ModelName + " walkway end X");
                Assert.That(entry.WalkwayEnd.Z,
                    Is.EqualTo(entry.DoorPosition.Z - CatalogGalleryLayout.WalkwayLength).Within(0.0001f),
                    entry.Model.ModelName + " walkway end Z");
            }
        }

        [Test]
        public void Compute_FencePlaceholder_IsTheScaledFootprintRectangle_WithTheDoorOnItsFrontEdge()
        {
            // #129 fences don't exist yet; the placeholder outlines the
            // scaled footprint so Derek can judge the authored numbers
            // against the rendered model. The door must sit on the rect's
            // front (min-Z) edge, within its X extent.
            var entries = CatalogGalleryLayout.Compute(TargetFootprint, Spacing);

            foreach (var entry in entries)
            {
                var halfX = entry.UniformScale * entry.Model.FootprintX / 2f;
                var halfZ = entry.UniformScale * entry.Model.FootprintZ / 2f;

                Assert.That(entry.FenceMin.X, Is.EqualTo(entry.Position.X - halfX).Within(0.0001f));
                Assert.That(entry.FenceMin.Z, Is.EqualTo(entry.Position.Z - halfZ).Within(0.0001f));
                Assert.That(entry.FenceMax.X, Is.EqualTo(entry.Position.X + halfX).Within(0.0001f));
                Assert.That(entry.FenceMax.Z, Is.EqualTo(entry.Position.Z + halfZ).Within(0.0001f));

                Assert.That(entry.DoorPosition.Z, Is.EqualTo(entry.FenceMin.Z).Within(0.0001f),
                    entry.Model.ModelName + " door on the front fence edge");
                Assert.That(entry.DoorPosition.X,
                    Is.InRange(entry.FenceMin.X - 0.0001f, entry.FenceMax.X + 0.0001f),
                    entry.Model.ModelName + " door within the facade extent");
            }
        }

        [Test]
        public void Compute_RejectsNonPositiveTargetFootprintOrSpacing()
        {
            Assert.That(() => CatalogGalleryLayout.Compute(0f, Spacing), Throws.ArgumentException);
            Assert.That(() => CatalogGalleryLayout.Compute(TargetFootprint, 0f), Throws.ArgumentException);
        }
    }
}
