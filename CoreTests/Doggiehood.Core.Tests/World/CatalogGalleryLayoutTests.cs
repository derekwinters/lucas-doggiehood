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
        private const float Scale = HousePlacement.KitScale;
        private const float Spacing = 16f;

        [Test]
        public void Compute_ReturnsOneEntryPerCatalogModel_InCatalogOrder()
        {
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

            Assert.That(entries.Count, Is.EqualTo(HouseModelCatalog.Models.Count));
            Assert.That(entries.Select(e => e.Model.ModelName),
                Is.EqualTo(HouseModelCatalog.Models.Select(m => m.ModelName)));
        }

        [Test]
        public void Compute_PlacesEntriesInARowAlongX_SpacedApart()
        {
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

            for (var i = 0; i < entries.Count; i++)
            {
                Assert.That(entries[i].Position.X, Is.EqualTo(i * Spacing).Within(0.0001f));
                Assert.That(entries[i].Position.Z, Is.EqualTo(0f).Within(0.0001f));
            }
        }

        [Test]
        public void Compute_AppliesTheSameFixedScaleToEveryModel_NoPerModelNormalization()
        {
            // Same rule WorldBuilder.BuildHouseModel applies since #145:
            // ONE fixed uniform scale for every City Kit house model — the
            // scale passed in, verbatim. No per-model footprint math may
            // creep back in (the old 8m/MaxFootprint rule made every model
            // a different size, so doors read at different scales).
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

            foreach (var entry in entries)
            {
                Assert.That(entry.UniformScale, Is.EqualTo(Scale).Within(0.0001f),
                    entry.Model.ModelName);
            }
        }

        [Test]
        public void Compute_DoorPosition_ComesFromFrontDoorWorldPosition_TheGamePathApi()
        {
            // Same-API guardrail: the annotated door must be exactly what
            // HouseModel.FrontDoorWorldPosition returns for the entry's
            // placement — no duplicated math allowed to drift.
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

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
        public void Compute_AtGalleryYawZero_TheDoorIsTheScaledAuthoredLocalPoint()
        {
            // Gallery convention: yaw 0, so container axes == model axes
            // and the door marker lands exactly at the entry position plus
            // the scaled authored 2D local door point. (This is the very
            // relationship Derek's gallery pass 1 measurements rely on —
            // Inspector local position / uniform scale = model-local door.)
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

            foreach (var entry in entries)
            {
                Assert.That(entry.YawDegrees, Is.EqualTo(0f), entry.Model.ModelName + " yaw");
                Assert.That(entry.DoorPosition.X,
                    Is.EqualTo(entry.Position.X + entry.UniformScale * entry.Model.FrontDoorLocalX)
                        .Within(0.0001f),
                    entry.Model.ModelName + " door X");
                Assert.That(entry.DoorPosition.Z,
                    Is.EqualTo(entry.Position.Z + entry.UniformScale * entry.Model.FrontDoorLocalZ)
                        .Within(0.0001f),
                    entry.Model.ModelName + " door Z");
            }
        }

        [Test]
        public void WalkwayEndBeyondFacade_MatchesTheRealInGameEndpointRule()
        {
            // #128: the in-game walkway ends on the sidewalk CENTERLINE,
            // which sits FrontSetback + SidewalkWidth / 2 = 3.75m beyond
            // the scaled front facade. Since gallery pass 1 the doors are
            // recessed, so this is an ENDPOINT rule, not a length: the
            // walkway's length varies per model with the door's recess
            // depth behind the facade, exactly as in the game.
            Assert.That(CatalogGalleryLayout.WalkwayEndBeyondFacade,
                Is.EqualTo(HousePlacement.FrontSetback + WorldDimensions.SidewalkWidth / 2f).Within(0.0001f));
        }

        [Test]
        public void Compute_WalkwayPlaceholder_RunsFromTheDoorToTheGameEndpointBeyondTheScaledFacade()
        {
            // The gallery walkway is a marker line from the door toward
            // where the sidewalk would be — straight out the front (world
            // -Z at yaw 0), ENDING WalkwayEndBeyondFacade past the scaled
            // front facade plane (the sidewalk-centerline endpoint the real
            // #128 walkway has; the gallery itself has no streets to attach
            // to). With recessed doors the run is longer than 3.75m by each
            // model's own recess depth.
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

            foreach (var entry in entries)
            {
                var scaledFacadeZ = entry.Position.Z - entry.UniformScale * entry.Model.FootprintZ / 2f;

                Assert.That(entry.WalkwayStart, Is.EqualTo(entry.DoorPosition),
                    entry.Model.ModelName + " walkway start");
                Assert.That(entry.WalkwayEnd.X, Is.EqualTo(entry.DoorPosition.X).Within(0.0001f),
                    entry.Model.ModelName + " walkway end X");
                Assert.That(entry.WalkwayEnd.Z,
                    Is.EqualTo(scaledFacadeZ - CatalogGalleryLayout.WalkwayEndBeyondFacade).Within(0.0001f),
                    entry.Model.ModelName + " walkway end Z");
            }
        }

        [Test]
        public void Compute_FencePlaceholder_IsTheScaledFootprintRectangle_WithTheDoorStrictlyInsideIt()
        {
            // #129 fences don't exist yet; the placeholder outlines the
            // scaled footprint so Derek can judge the authored numbers
            // against the rendered model. Since gallery pass 1 the doors
            // are recessed (porches), so the marker sits strictly INSIDE
            // the footprint rectangle, no longer on its front edge.
            var entries = CatalogGalleryLayout.Compute(Scale, Spacing);

            foreach (var entry in entries)
            {
                var halfX = entry.UniformScale * entry.Model.FootprintX / 2f;
                var halfZ = entry.UniformScale * entry.Model.FootprintZ / 2f;

                Assert.That(entry.FenceMin.X, Is.EqualTo(entry.Position.X - halfX).Within(0.0001f));
                Assert.That(entry.FenceMin.Z, Is.EqualTo(entry.Position.Z - halfZ).Within(0.0001f));
                Assert.That(entry.FenceMax.X, Is.EqualTo(entry.Position.X + halfX).Within(0.0001f));
                Assert.That(entry.FenceMax.Z, Is.EqualTo(entry.Position.Z + halfZ).Within(0.0001f));

                Assert.That(entry.DoorPosition.X,
                    Is.GreaterThan(entry.FenceMin.X).And.LessThan(entry.FenceMax.X),
                    entry.Model.ModelName + " door strictly within the footprint (X)");
                Assert.That(entry.DoorPosition.Z,
                    Is.GreaterThan(entry.FenceMin.Z).And.LessThan(entry.FenceMax.Z),
                    entry.Model.ModelName + " door strictly within the footprint (Z)");
            }
        }

        [Test]
        public void Compute_RejectsNonPositiveScaleOrSpacing()
        {
            Assert.That(() => CatalogGalleryLayout.Compute(0f, Spacing), Throws.ArgumentException);
            Assert.That(() => CatalogGalleryLayout.Compute(Scale, 0f), Throws.ArgumentException);
        }
    }
}
