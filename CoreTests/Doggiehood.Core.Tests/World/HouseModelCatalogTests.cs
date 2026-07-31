using System.Linq;
using Doggiehood.Core.Art;
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
        public void ForHouse_UnresolvableIdThrows()
        {
            // Since #299/#414 every id >= HouseVariantAssignment.FirstZoneHouseId
            // is a valid zone house that deterministically rolls a ladder, so a
            // large id like 999 now RESOLVES rather than throwing. ForHouse only
            // throws for an id with no assignment at all: a non-zone id outside
            // the four starters (no HouseStyleTable style, not a zone house).
            Assert.That(HouseVariantAssignment.IsZoneHouse(0), Is.False);
            Assert.That(() => HouseModelCatalog.ForHouse(0), Throws.ArgumentException);
            Assert.That(() => HouseModelCatalog.ForHouse(-1), Throws.ArgumentException);
        }

        [Test]
        public void ForModel_UnknownNameThrows()
        {
            Assert.That(() => HouseModelCatalog.ForModel("building-type-zzz"), Throws.ArgumentException);
        }

        [Test]
        public void HasModel_IsTrueOnlyForCatalogedMeshes()
        {
            // #59: WorldBuilder uses this non-throwing check to steer a
            // level-resolved mesh with no catalog entry to the graybox
            // fallback instead of crashing on ForModel's ArgumentException.
            // The starter-house ladder meshes now ALL carry a catalog entry
            // (v0.6 de-graybox pass — every level renders its chosen kit
            // mesh), so the fallback is reached only for genuinely-unknown
            // meshes (e.g. an expansion house with no ladder).
            Assert.That(HouseModelCatalog.HasModel("building-type-r"), Is.True);
            Assert.That(HouseModelCatalog.HasModel("building-type-c"), Is.True, "L2 upgrade mesh now has a catalog entry");
            Assert.That(HouseModelCatalog.HasModel("building-type-zzz"), Is.False);
        }

        [Test]
        public void EveryLevelOfEveryStarterHouse_ResolvesToARealCatalogEntry()
        {
            // "No graybox at any level" as an enforced invariant (Derek
            // 2026-07-25: the chosen kit mesh must render at every level, not
            // a graybox placeholder). Every level 1..MaxLevel of every starter
            // house's HouseLevelModelTable ladder must name a mesh that has a
            // full HouseModelCatalog entry, so WorldBuilder never falls back
            // to the graybox for a starter house at any level.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(HouseLevelModelTable.HasHouse(lot.HouseId), Is.True,
                    $"house {lot.HouseId} has no level ladder");

                for (var level = HouseLevelModelTable.MinLevel;
                     level <= Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel;
                     level++)
                {
                    var mesh = HouseLevelModelTable.ForHouseLevel(lot.HouseId, level);
                    Assert.That(HouseModelCatalog.HasModel(mesh), Is.True,
                        $"house {lot.HouseId} level {level} mesh '{mesh}' has no catalog entry");
                }
            }
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
        public void Models_RecordTheMeasuredFootprints_ForTheLevel1Starters_R_H_Q()
        {
            // Level-1 starter meshes for houses 1/2/4 (Derek's 2026-07-25
            // call resolving #122). Footprints measured from the kit FBX
            // bounding-box extent / 100 — the same model-local convention
            // the b/g/k/m entries use. House 3 keeps building-type-k, whose
            // footprint is already covered above.
            AssertEntry("building-type-r", 1.028f, 1.020f);
            AssertEntry("building-type-h", 1.300f, 0.916f);
            AssertEntry("building-type-q", 1.240f, 0.8856f);
        }

        [Test]
        public void Models_DoorLocalPoints_ForTheLevel1Starters_R_H_Q_ArePlaceholders()
        {
            // PLACEHOLDER door anchors for the new r/h/q starters, NOT
            // authored measurements: centered on X and a quarter of the way
            // toward the street (z = -FootprintZ/4). They are provisional
            // pending a Derek gallery authoring pass (same mechanism that
            // produced the #126 pass-1 door data for b/g/k/m). Chosen to sit
            // strictly inside the footprint so the within-footprint
            // guardrail holds until the real anchors land.
            AssertDoor("building-type-r", 0f, -0.2550f);
            AssertDoor("building-type-h", 0f, -0.2290f);
            AssertDoor("building-type-q", 0f, -0.2214f);
        }

        [Test]
        public void Models_RecordTheMeasuredFootprints_ForTheLadderMeshes()
        {
            // The 10 non-L1 upgrade meshes of the #59 ladders (v0.6
            // de-graybox pass, Derek 2026-07-25: every level renders its
            // chosen kit mesh, no graybox). Footprints measured from each kit
            // FBX bounding-box extent / 100 — the same model-local convention
            // b/g/k/m and the r/h/q L1 starters use.
            AssertEntry("building-type-c", 1.2864f, 1.0281f);
            AssertEntry("building-type-s", 1.4060f, 1.0864f);
            AssertEntry("building-type-f", 1.4280f, 1.4059f);
            AssertEntry("building-type-i", 1.2864f, 1.0280f);
            AssertEntry("building-type-l", 1.0336f, 1.0200f);
            AssertEntry("building-type-j", 1.3700f, 0.9160f);
            AssertEntry("building-type-d", 1.7564f, 1.0280f);
            AssertEntry("building-type-e", 1.3000f, 1.0280f);
            AssertEntry("building-type-u", 1.4280f, 1.0869f);
            AssertEntry("building-type-n", 1.7843f, 1.3779f);
        }

        [Test]
        public void Models_DoorLocalPoints_ForTheLadderMeshes_ArePlaceholders()
        {
            // PLACEHOLDER door anchors for the 10 non-L1 ladder meshes, NOT
            // authored measurements: centered on X and a quarter of the way
            // toward the street (z = -FootprintZ/4) — the same provisional
            // pattern as the r/h/q L1 starters, pending a Derek gallery
            // authoring pass (the mechanism that produced the #126 pass-1 door
            // data for b/g/k/m). They sit strictly inside the footprint so the
            // within-footprint guardrail holds until the real anchors land.
            AssertDoor("building-type-c", 0f, -0.2570f);
            AssertDoor("building-type-s", 0f, -0.2716f);
            AssertDoor("building-type-f", 0f, -0.3515f);
            AssertDoor("building-type-i", 0f, -0.2570f);
            AssertDoor("building-type-l", 0f, -0.2550f);
            AssertDoor("building-type-j", 0f, -0.2290f);
            AssertDoor("building-type-d", 0f, -0.2570f);
            AssertDoor("building-type-e", 0f, -0.2570f);
            AssertDoor("building-type-u", 0f, -0.2717f);
            AssertDoor("building-type-n", 0f, -0.3445f);
        }

        [Test]
        public void Models_RecordTheMeasuredFootprints_ForTheFifthLadderMeshes()
        {
            // The 5th-house-ladder meshes staged for #299 (#348 asset step,
            // Derek-approved option B — agent-computed footprints). Footprints
            // measured from each kit FBX bounding-box extent / 100 — the same
            // model-local convention b/g/k/m and the r/h/q L1 starters use.
            // building-type-m already carries its own row (asserted above).
            AssertEntry("building-type-o", 1.2700f, 1.0280f);
            AssertEntry("building-type-p", 1.2400f, 0.9900f);
            AssertEntry("building-type-a", 1.3000f, 1.0281f);
        }

        [Test]
        public void Models_DoorLocalPoints_ForTheFifthLadderMeshes_ArePlaceholders()
        {
            // PLACEHOLDER door anchors for the 5th-ladder meshes, NOT authored
            // measurements: centered on X and a quarter of the way toward the
            // street (z = -FootprintZ/4) — the same provisional pattern as the
            // r/h/q L1 starters and the L2-L4 ladder meshes, pending a Derek
            // gallery authoring pass (the mechanism that produced the #126
            // pass-1 door data for b/g/k/m). They sit strictly inside the
            // footprint so the within-footprint guardrail holds.
            AssertDoor("building-type-o", 0f, -0.2570f);
            AssertDoor("building-type-p", 0f, -0.2475f);
            AssertDoor("building-type-a", 0f, -0.2570f);
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
        public void ForHouse_UsesTheLevel1MeshOfEachUpgradePath()
        {
            // The houseId -> model assignment lives on
            // Doggiehood.Core.Art.HouseStyleTable (#64) as the single
            // source of truth; HouseModelCatalog.ForHouse delegates to it
            // rather than keeping its own duplicate assignment list. Derek's
            // 2026-07-25 call (resolving the #122 placeholder) set these to
            // the Level-1 (as-built) mesh of each house's #59 upgrade path.
            Assert.That(HouseModelCatalog.ForHouse(1).ModelName, Is.EqualTo("building-type-r"));
            Assert.That(HouseModelCatalog.ForHouse(2).ModelName, Is.EqualTo("building-type-h"));
            Assert.That(HouseModelCatalog.ForHouse(3).ModelName, Is.EqualTo("building-type-k"));
            Assert.That(HouseModelCatalog.ForHouse(4).ModelName, Is.EqualTo("building-type-q"));
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

        [Test]
        public void ForHouse_ZoneLotResolvesViaHouseVariantAssignment_InsteadOfThrowing()
        {
            // #414: a zone lot (id >= 5) has no HouseStyleTable style, so the
            // old ForHouse -> HouseStyleTable.ForHouse path threw
            // ArgumentException for it. The chokepoint fix branches zone ids
            // through the #299 rolled ladder instead
            // (HouseVariantAssignment -> HouseLevelModelTable level 1 ->
            // ForModel), so ForHouse resolves a real catalog entry.
            const int zoneId = HouseVariantAssignment.FirstZoneHouseId; // 5
            Assert.That(HouseVariantAssignment.IsZoneHouse(zoneId), Is.True);

            HouseModel resolved = null;
            Assert.That(() => resolved = HouseModelCatalog.ForHouse(zoneId), Throws.Nothing,
                "zone house model resolution must not throw through HouseStyleTable");

            var variant = HouseVariantAssignment.ForHouse(zoneId);
            var expected = HouseModelCatalog.ForModel(
                HouseLevelModelTable.ForHouseLevel(variant.LadderId, HouseLevelModelTable.MinLevel));
            Assert.That(resolved, Is.SameAs(expected),
                "zone house model must come from its rolled ladder's level-1 catalog entry");
        }

        [Test]
        public void ForHouse_ZoneHousePlacementFootprint_IsLevelInvariant()
        {
            // #414: the placement/footprint call sites (HousePlacement.
            // HouseFootprint, LotFence) have only a HouseLot — no house.Level —
            // and leveling a zone house never resizes or moves its lot (every
            // ladder mesh fits the shared lot envelope). So ForHouse resolves
            // the rolled ladder's LEVEL-1 mesh: the footprint it hands every
            // placement call site is fixed for the house's whole lifetime,
            // identical no matter which level (1..4) the house is at in-game.
            foreach (var zoneId in new[] { 5, 6, 7, 13, 42 })
            {
                var variant = HouseVariantAssignment.ForHouse(zoneId);
                var levelOne = HouseModelCatalog.ForModel(
                    HouseLevelModelTable.ForHouseLevel(variant.LadderId, HouseLevelModelTable.MinLevel));

                var resolved = HouseModelCatalog.ForHouse(zoneId);

                Assert.That(resolved.ModelName, Is.EqualTo(levelOne.ModelName),
                    $"zone house {zoneId} placement must resolve its ladder's level-1 mesh");
                Assert.That(resolved.FootprintX, Is.EqualTo(levelOne.FootprintX),
                    $"zone house {zoneId} placement footprint X must be the level-1 envelope");
                Assert.That(resolved.FootprintZ, Is.EqualTo(levelOne.FootprintZ),
                    $"zone house {zoneId} placement footprint Z must be the level-1 envelope");
            }
        }

        [Test]
        public void ForHouse_StarterIdsStillResolveViaHouseStyleTable_Unchanged()
        {
            // #414 regression guard: making ForHouse zone-aware must not change
            // how the four starter houses (ids 1-4) resolve — they keep going
            // through HouseStyleTable exactly as before the chokepoint branch.
            for (var houseId = 1; houseId <= 4; houseId++)
            {
                Assert.That(HouseVariantAssignment.IsZoneHouse(houseId), Is.False,
                    $"house {houseId} must be a starter, not a zone house");
                var expected = HouseModelCatalog.ForModel(HouseStyleTable.ForHouse(houseId).ModelName);
                Assert.That(HouseModelCatalog.ForHouse(houseId), Is.SameAs(expected),
                    $"starter house {houseId} must still resolve via HouseStyleTable");
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
