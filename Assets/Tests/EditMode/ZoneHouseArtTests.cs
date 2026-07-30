using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #299: a zone-built house (id >= 5) no longer falls through to the
    /// graybox box. WorldBuilder.BuildHouse resolves it through its rolled
    /// <see cref="HouseVariant"/> — the variant's ladder mesh at the house's
    /// current level from <see cref="HouseLevelModelTable"/> — and applies the
    /// generated palette tint (<see cref="Palette.HouseTintHex"/>) as a
    /// material color-multiply, exactly like the vacancy tint. A zone house
    /// with no rolled variant (constructed directly, or an unknown expansion
    /// id) still falls back to graybox.
    /// </summary>
    public class ZoneHouseArtTests
    {
        private const int ZoneHouseId = 5;
        private GameObject container;

        [SetUp]
        public void SetUp()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            container = new GameObject("ZoneHouseArtTestContainer");
        }

        [TearDown]
        public void TearDown()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            if (container != null)
            {
                Object.DestroyImmediate(container);
            }
        }

        private static House ZoneHouse(int level, bool isVacant)
        {
            return new House(ZoneHouseId, Quadrant.NorthEast, isVacant, level,
                HouseVariantAssignment.ForHouse(ZoneHouseId));
        }

        private static HouseLot ZoneLot()
        {
            return new HouseLot(ZoneHouseId, Quadrant.NorthEast, new GridPoint(1f, 1f));
        }

        [Test]
        public void BuildHouse_ForAZoneHouse_RendersItsKitModel_NotGraybox()
        {
            var houseRoot = WorldBuilder.BuildHouse(container.transform, ZoneHouse(HouseLevelModelTable.MinLevel, isVacant: false), ZoneLot());

            var childNames = houseRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
            Assert.That(childNames, Does.Contain("Model"), "a zone house renders its rolled kit mesh");
            Assert.That(childNames, Has.No.Member("Walls"), "a zone house must not use the graybox fallback");
        }

        [Test]
        public void BuildHouse_ForAZoneHouse_AtEveryLevel_RendersItsLaddersMesh()
        {
            var variant = HouseVariantAssignment.ForHouse(ZoneHouseId);

            for (var level = HouseLevelModelTable.MinLevel;
                 level <= Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel;
                 level++)
            {
                var houseRoot = WorldBuilder.BuildHouse(container.transform, ZoneHouse(level, isVacant: false), ZoneLot());

                var expectedMesh = HouseLevelModelTable.ForHouseLevel(variant.LadderId, level);
                var sourceMeshes = Resources.Load<GameObject>(expectedMesh)
                    .GetComponentsInChildren<MeshFilter>().Select(mf => mf.sharedMesh)
                    .Where(m => m != null).ToList();
                var model = houseRoot.transform.Find("Model");
                Assert.That(model, Is.Not.Null, $"level {level} renders a kit mesh");
                var houseMeshes = model.GetComponentsInChildren<MeshFilter>()
                    .Select(mf => mf.sharedMesh).Where(m => m != null).ToList();
                Assert.That(houseMeshes, Is.Not.Empty, $"level {level} renders no mesh");
                Assert.That(houseMeshes, Is.SubsetOf(sourceMeshes),
                    $"level {level} renders meshes from ladder {variant.LadderId}'s mesh {expectedMesh}");

                Object.DestroyImmediate(houseRoot);
            }
        }

        [Test]
        public void BuildHouse_ForAnOccupiedZoneHouse_AppliesItsPaletteTintToEveryRenderer()
        {
            var variant = HouseVariantAssignment.ForHouse(ZoneHouseId);
            var expected = CoreColors.FromHex(Palette.HouseTintHex(variant.TintIndex));

            var houseRoot = WorldBuilder.BuildHouse(container.transform, ZoneHouse(HouseLevelModelTable.MinLevel, isVacant: false), ZoneLot());

            var model = houseRoot.transform.Find("Model");
            Assert.That(model, Is.Not.Null);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.sharedMaterial.color;
                Assert.That(color.r, Is.EqualTo(expected.r).Within(0.01f), $"{renderer.name} R");
                Assert.That(color.g, Is.EqualTo(expected.g).Within(0.01f), $"{renderer.name} G");
                Assert.That(color.b, Is.EqualTo(expected.b).Within(0.01f), $"{renderer.name} B");
            }
        }

        [Test]
        public void BuildHouse_ForAVacantZoneHouse_TheVacancyGreyStillWins()
        {
            var expected = CoreColors.FromHex(Palette.VacantHouseTintHex);

            var houseRoot = WorldBuilder.BuildHouse(container.transform, ZoneHouse(HouseLevelModelTable.MinLevel, isVacant: true), ZoneLot());

            var model = houseRoot.transform.Find("Model");
            Assert.That(model, Is.Not.Null);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.sharedMaterial.color;
                Assert.That(color.r, Is.EqualTo(expected.r).Within(0.01f), $"{renderer.name} R stays vacancy grey");
                Assert.That(color.g, Is.EqualTo(expected.g).Within(0.01f), $"{renderer.name} G stays vacancy grey");
                Assert.That(color.b, Is.EqualTo(expected.b).Within(0.01f), $"{renderer.name} B stays vacancy grey");
            }
        }

        [Test]
        public void BuildHouse_ForAZoneHouseWithNoVariant_FallsBackToGraybox()
        {
            // A zone house id with no rolled variant (constructed directly)
            // can't resolve a ladder, so it must still render the graybox box.
            var house = new House(ZoneHouseId, Quadrant.NorthEast, isVacant: false);

            var houseRoot = WorldBuilder.BuildHouse(container.transform, house, ZoneLot());

            var childNames = houseRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
            Assert.That(childNames, Does.Contain("Walls"), "a variant-less zone house falls back to graybox");
            Assert.That(childNames, Has.No.Member("Model"));
        }
    }
}
