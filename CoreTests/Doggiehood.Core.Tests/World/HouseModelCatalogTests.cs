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
        public void Models_HaveUniqueNames_AndPositiveFootprints()
        {
            Assert.That(HouseModelCatalog.Models, Is.Not.Empty);
            Assert.That(HouseModelCatalog.Models.Select(m => m.ModelName), Is.Unique);

            foreach (var model in HouseModelCatalog.Models)
            {
                Assert.That(model.FootprintX, Is.GreaterThan(0f), $"{model.ModelName} FootprintX");
                Assert.That(model.FootprintZ, Is.GreaterThan(0f), $"{model.ModelName} FootprintZ");
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
        public void Models_DoorLocalPoints_AreDereksGalleryPass1Measurements()
        {
            // Derek's gallery pass 1 (2026-07-14): each DoorMarker moved
            // onto the visible door of the rendered mesh, Inspector local
            // position read in the entry container (gallery yaw 0, so
            // container axes == model axes), divided back by the entry's
            // then-current uniform scale (8 / the model's max footprint,
            // the pre-#145 normalization rule) and rounded to 4 decimals.
            AssertDoor("building-type-b", -0.2612f, 0.0446f);
            AssertDoor("building-type-g", 0.0769f, -0.3382f);
            AssertDoor("building-type-k", 0.1900f, -0.3672f);
            AssertDoor("building-type-m", -0.0464f, -0.6105f);
        }

        [Test]
        public void Models_DoorLocalPoints_LieStrictlyWithinTheFootprint()
        {
            // Guardrail (replacing the pre-gallery facade-plane rule): a
            // real kit door is recessed somewhere INSIDE the model's
            // footprint rectangle — never on or beyond its bounds. Catches
            // sign flips, axis swaps, and scaled-vs-local mixups in future
            // authoring passes.
            foreach (var model in HouseModelCatalog.Models)
            {
                Assert.That(System.Math.Abs(model.FrontDoorLocalX),
                    Is.LessThan(model.FootprintX / 2f),
                    $"{model.ModelName} door X outside the footprint");
                Assert.That(System.Math.Abs(model.FrontDoorLocalZ),
                    Is.LessThan(model.FootprintZ / 2f),
                    $"{model.ModelName} door Z outside the footprint");
            }
        }

        [Test]
        public void ForHouse_KeepsThePlaceholderModelPicks()
        {
            // The houseId -> model assignment (#122's placeholder picks,
            // still awaiting Derek and Lucas's re-pick in the Editor) now
            // lives on Doggiehood.Core.Art.HouseStyleTable (#64) as the
            // single source of truth; HouseModelCatalog.ForHouse delegates
            // to it rather than keeping its own duplicate assignment list.
            Assert.That(HouseModelCatalog.ForHouse(1).ModelName, Is.EqualTo("building-type-b"));
            Assert.That(HouseModelCatalog.ForHouse(2).ModelName, Is.EqualTo("building-type-g"));
            Assert.That(HouseModelCatalog.ForHouse(3).ModelName, Is.EqualTo("building-type-k"));
            Assert.That(HouseModelCatalog.ForHouse(4).ModelName, Is.EqualTo("building-type-m"));
        }

        [Test]
        public void ForHouse_DelegatesToHouseStyleTable_ForTheModelAssignment()
        {
            // #64: one source of truth for houseId -> model. Every house
            // style's ModelName must resolve to the same catalog entry
            // HouseModelCatalog.ForHouse returns for that house.
            foreach (var style in Doggiehood.Core.Art.HouseStyleTable.Styles)
            {
                Assert.That(HouseModelCatalog.ForHouse(style.StyleId).ModelName,
                    Is.EqualTo(style.ModelName));
            }
        }

        private static void AssertEntry(string modelName, float footprintX, float footprintZ)
        {
            var model = HouseModelCatalog.ForModel(modelName);
            Assert.That(model.FootprintX, Is.EqualTo(footprintX).Within(0.0001f), modelName + " FootprintX");
            Assert.That(model.FootprintZ, Is.EqualTo(footprintZ).Within(0.0001f), modelName + " FootprintZ");
        }

        private static void AssertDoor(string modelName, float localX, float localZ)
        {
            var model = HouseModelCatalog.ForModel(modelName);
            Assert.That(model.FrontDoorLocalX, Is.EqualTo(localX).Within(0.00001f), modelName + " door local X");
            Assert.That(model.FrontDoorLocalZ, Is.EqualTo(localZ).Within(0.00001f), modelName + " door local Z");
        }
    }
}
