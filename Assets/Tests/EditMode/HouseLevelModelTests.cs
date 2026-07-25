using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #59: WorldBuilder renders the mesh for a house's current level from
    /// HouseLevelModelTable. A level-1 house shows its anchored as-built
    /// mesh; upgrading swaps in the next rung. The L2-L4 upgrade meshes have
    /// no authored HouseModelCatalog geometry yet, so they render via the
    /// graybox fallback until a later art pass — and leveling never resizes
    /// or moves the lot.
    /// </summary>
    public class HouseLevelModelTests
    {
        private GameObject container;

        [SetUp]
        public void SetUp()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            container = new GameObject("HouseLevelModelTestContainer");
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

        [Test]
        public void HouseModelResourcePath_ResolvesTheLevelMesh_FromHouseLevelModelTable()
        {
            var lot = NeighborhoodLayout.HouseLots.First();

            for (var level = HouseLevelModelTable.MinLevel; level <= Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel; level++)
            {
                Assert.That(WorldBuilder.HouseModelResourcePath(lot.HouseId, level),
                    Is.EqualTo(HouseLevelModelTable.ForHouseLevel(lot.HouseId, level)),
                    $"house {lot.HouseId} level {level}");
            }
        }

        [Test]
        public void BuildHouse_AtAnUnauthoredUpgradeLevel_FallsBackToGraybox()
        {
            // House 1 at level 2 resolves to building-type-c, whose
            // footprint/door geometry isn't authored in HouseModelCatalog —
            // WorldBuilder must render the graybox "Walls" fallback rather
            // than an un-anchored kit mesh.
            var lot = NeighborhoodLayout.HouseLots.First();
            const int unauthoredLevel = 2;
            Assert.That(HouseModelCatalog.HasModel(HouseLevelModelTable.ForHouseLevel(lot.HouseId, unauthoredLevel)),
                Is.False, "sanity: the level-2 mesh has no catalog entry yet");
            var house = new House(lot.HouseId, lot.Quadrant, isVacant: false, level: unauthoredLevel);

            var houseRoot = WorldBuilder.BuildHouse(container.transform, house);

            var childNames = houseRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
            Assert.That(childNames, Does.Contain("Walls"), "an un-authored upgrade level must fall back to graybox");
            Assert.That(childNames, Has.No.Member("Model"), "an un-authored upgrade level must not render a kit mesh");
        }

        [Test]
        public void BuildHouse_AtLevelOne_RendersTheAnchoredKitMesh()
        {
            // Level 1 resolves to the anchored as-built mesh (building-type-r
            // for house 1), which has a catalog entry and staged kit model —
            // so the kit path renders a "Model" child, exactly as today.
            var lot = NeighborhoodLayout.HouseLots.First();
            var house = new House(lot.HouseId, lot.Quadrant, isVacant: false, level: HouseLevelModelTable.MinLevel);

            var houseRoot = WorldBuilder.BuildHouse(container.transform, house);

            var childNames = houseRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
            Assert.That(childNames, Does.Contain("Model"), "a level-1 house renders its anchored kit mesh");
            Assert.That(childNames, Has.No.Member("Walls"), "a level-1 house should not use the graybox fallback");
        }

        [Test]
        public void LevelingAHouse_NeverMovesOrResizesItsLot()
        {
            // The house root sits at the same front-setback position at every
            // level — leveling swaps the mesh only, it never resizes the lot.
            var lot = NeighborhoodLayout.HouseLots.First();
            var expected = HousePlacement.Position(lot, WorldBuilder.HouseKitScale);

            foreach (var level in new[] { HouseLevelModelTable.MinLevel, 2 })
            {
                var house = new House(lot.HouseId, lot.Quadrant, isVacant: false, level: level);
                var houseRoot = WorldBuilder.BuildHouse(container.transform, house);

                Assert.That(houseRoot.transform.position.x, Is.EqualTo(expected.X).Within(0.001f), $"level {level} X");
                Assert.That(houseRoot.transform.position.z, Is.EqualTo(expected.Z).Within(0.001f), $"level {level} Z");

                Object.DestroyImmediate(houseRoot);
            }
        }
    }
}
