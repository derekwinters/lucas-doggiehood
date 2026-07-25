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
    /// mesh; upgrading swaps in the next rung. As of the v0.6 de-graybox pass
    /// every rung of every starter ladder has a HouseModelCatalog entry, so
    /// all four levels render their chosen kit mesh (visibly growing) — the
    /// graybox fallback is reached only for a genuinely-unknown mesh (e.g. an
    /// expansion-built house with no ladder). Leveling never resizes or moves
    /// the lot.
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
        public void BuildHouse_ForAHouseWithNoLadder_FallsBackToGraybox()
        {
            // The graybox fallback still exists for a genuinely-unknown mesh:
            // an expansion-built house on a zone lot beyond the starting 4 has
            // no HouseLevelModelTable ladder, so WorldBuilder can't resolve a
            // kit mesh and must render the graybox "Walls" fallback rather
            // than an un-anchored kit mesh. (Starter houses now render a real
            // kit mesh at every level — covered below.)
            const int expansionHouseId = 999;
            Assert.That(HouseLevelModelTable.HasHouse(expansionHouseId), Is.False,
                "sanity: an expansion house id has no level ladder");
            var lot = new HouseLot(expansionHouseId, Quadrant.NorthEast, new GridPoint(1f, 1f));
            var house = new House(expansionHouseId, Quadrant.NorthEast, isVacant: false);

            var houseRoot = WorldBuilder.BuildHouse(container.transform, house, lot);

            var childNames = houseRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
            Assert.That(childNames, Does.Contain("Walls"), "a house with no ladder must fall back to graybox");
            Assert.That(childNames, Has.No.Member("Model"), "a house with no ladder must not render a kit mesh");
        }

        [Test]
        public void BuildHouse_AtEveryLevel_RendersTheChosenKitMesh()
        {
            // v0.6 de-graybox pass: every level of a starter house resolves to
            // a real HouseModelCatalog entry with a staged kit model, so the
            // kit path renders a "Model" child at levels 1..4 — never the
            // graybox "Walls" fallback.
            var lot = NeighborhoodLayout.HouseLots.First();

            for (var level = HouseLevelModelTable.MinLevel;
                 level <= Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel;
                 level++)
            {
                var house = new House(lot.HouseId, lot.Quadrant, isVacant: false, level: level);
                var houseRoot = WorldBuilder.BuildHouse(container.transform, house);

                var childNames = houseRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
                Assert.That(childNames, Does.Contain("Model"), $"level {level} renders its chosen kit mesh");
                Assert.That(childNames, Has.No.Member("Walls"), $"level {level} should not use the graybox fallback");

                Object.DestroyImmediate(houseRoot);
            }
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
