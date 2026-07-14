using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class HouseModelCatalogTests
    {
        [Test]
        public void ForHouse_HasACatalogEntryForEveryHouseLot()
        {
            // #125 completeness guard: every house the lot mapping can
            // reference must resolve to a full catalog entry (footprint +
            // door data), so adding a house model is one FBX + one catalog
            // row and forgetting the row is impossible.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var model = HouseModelCatalog.ForHouse(lot.HouseId);

                Assert.That(model, Is.Not.Null, $"house {lot.HouseId} has no catalog entry");
                Assert.That(HouseModelCatalog.Models, Does.Contain(model),
                    $"house {lot.HouseId} maps to a model missing from the catalog");
            }
        }

        [Test]
        public void ForHouse_UnknownIdThrows()
        {
            Assert.That(() => HouseModelCatalog.ForHouse(999), Throws.ArgumentException);
        }

        [Test]
        public void ForModel_UnknownNameThrows()
        {
            Assert.That(() => HouseModelCatalog.ForModel("building-type-zzz"), Throws.ArgumentException);
        }

        [Test]
        public void Models_HaveUniqueNames_AndPositiveFootprintsWithDoorsOnTheFrontFacade()
        {
            Assert.That(HouseModelCatalog.Models, Is.Not.Empty);
            Assert.That(HouseModelCatalog.Models.Select(m => m.ModelName), Is.Unique);

            foreach (var model in HouseModelCatalog.Models)
            {
                Assert.That(model.FootprintX, Is.GreaterThan(0f), $"{model.ModelName} FootprintX");
                Assert.That(model.FootprintZ, Is.GreaterThan(0f), $"{model.ModelName} FootprintZ");

                // The door must sit somewhere on the front facade, not
                // beyond the model's own width.
                Assert.That(System.Math.Abs(model.FrontDoorOffset),
                    Is.LessThanOrEqualTo(model.FootprintX / 2f),
                    $"{model.ModelName} door offset falls outside the facade");
            }
        }

        [Test]
        public void Models_RecordTheAuthoredKitNumbers_ForTypesB_G_K_M()
        {
            // #125: authored data parsed from the Kenney City Kit Suburban
            // GLB geometry (model-local units, scale-independent).
            AssertEntry("building-type-b", 1.828f, 1.140f);
            AssertEntry("building-type-g", 1.450f, 1.178f);
            AssertEntry("building-type-k", 0.921f, 1.020f);
            AssertEntry("building-type-m", 1.428f, 1.428f);
        }

        [Test]
        public void Models_MaxFootprint_MatchesTheValuesWorldBuilderScaledBy()
        {
            // The pre-#125 WorldBuilder.HouseModels tuple recorded each
            // model's max horizontal footprint; the catalog's per-axis
            // footprints must reduce to the same numbers so house scaling
            // is unchanged by the move into Core.
            Assert.That(HouseModelCatalog.ForModel("building-type-b").MaxFootprint, Is.EqualTo(1.828f).Within(0.0001f));
            Assert.That(HouseModelCatalog.ForModel("building-type-g").MaxFootprint, Is.EqualTo(1.45f).Within(0.0001f));
            Assert.That(HouseModelCatalog.ForModel("building-type-k").MaxFootprint, Is.EqualTo(1.02f).Within(0.0001f));
            Assert.That(HouseModelCatalog.ForModel("building-type-m").MaxFootprint, Is.EqualTo(1.428f).Within(0.0001f));
        }

        [Test]
        public void Models_FirstPassDoorOffsets_AreCenteredOnTheFacade()
        {
            // First pass (#125): no door node exists in the fused kit
            // meshes, so the door is recorded horizontally centered on the
            // front facade for all four models. Refinement happens via the
            // #126 debug gallery with Derek.
            foreach (var model in HouseModelCatalog.Models)
            {
                Assert.That(model.FrontDoorOffset, Is.EqualTo(0f), model.ModelName);
            }
        }

        [Test]
        public void ForHouse_KeepsThePlaceholderModelPicks()
        {
            // The houseId -> model assignment moved here from
            // WorldBuilder.HouseModels (#122's placeholder picks, still
            // awaiting Derek and Lucas's re-pick in the Editor).
            Assert.That(HouseModelCatalog.ForHouse(1).ModelName, Is.EqualTo("building-type-b"));
            Assert.That(HouseModelCatalog.ForHouse(2).ModelName, Is.EqualTo("building-type-g"));
            Assert.That(HouseModelCatalog.ForHouse(3).ModelName, Is.EqualTo("building-type-k"));
            Assert.That(HouseModelCatalog.ForHouse(4).ModelName, Is.EqualTo("building-type-m"));
        }

        private static void AssertEntry(string modelName, float footprintX, float footprintZ)
        {
            var model = HouseModelCatalog.ForModel(modelName);
            Assert.That(model.FootprintX, Is.EqualTo(footprintX).Within(0.0001f), modelName + " FootprintX");
            Assert.That(model.FootprintZ, Is.EqualTo(footprintZ).Within(0.0001f), modelName + " FootprintZ");
        }
    }
}
