using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    /// <summary>
    /// #59: each house's level (1-4) resolves to a City Kit Suburban mesh,
    /// anchored on that house's Level-1 (as-built) mesh. Derek approved the
    /// L1->L4 ladders (2026-07-25) as the real data; as of the v0.6
    /// de-graybox pass every rung has a full HouseModelCatalog entry, so Unity
    /// renders each level's chosen kit mesh (only the non-L1 door anchors
    /// remain provisional placeholders pending a gallery authoring pass).
    /// </summary>
    public class HouseLevelModelTableTests
    {
        [Test]
        public void ForHouseLevel_ResolvesTheApprovedLadders()
        {
            AssertLadder(1, "building-type-r", "building-type-c", "building-type-s", "building-type-b");
            AssertLadder(2, "building-type-h", "building-type-i", "building-type-g", "building-type-f");
            AssertLadder(3, "building-type-k", "building-type-l", "building-type-j", "building-type-d");
            AssertLadder(4, "building-type-q", "building-type-e", "building-type-u", "building-type-n");
        }

        [Test]
        public void ForHouseLevel_ResolvesTheFifthZoneLadder()
        {
            // #299: zone-built houses (id >= 5) roll one of five ladders. The
            // 5th ladder is size-ordered L1->L4 o -> p -> a -> m (Derek's
            // approved draft order, 2026-07-28; m is the largest/top rung and
            // was already catalogued).
            Assert.That(HouseLevelModelTable.HasHouse(5), Is.True,
                "the 5th zone ladder exists");
            AssertLadder(5, "building-type-o", "building-type-p", "building-type-a", "building-type-m");
        }

        [Test]
        public void FifthLadder_HasExactlyFourDistinctRungs_LikeTheOthers()
        {
            // #299 completeness: the new ladder is 4 rungs like every starter
            // ladder — one past the cap is out of range, and no mesh repeats.
            var meshes = new List<string>();
            for (var level = HouseLevelModelTable.MinLevel; level <= HouseUpgradeNumbers.MaxLevel; level++)
            {
                meshes.Add(HouseLevelModelTable.ForHouseLevel(5, level));
            }

            Assert.That(meshes, Is.Unique, "the 5th ladder reuses a mesh across its levels");
            Assert.That(() => HouseLevelModelTable.ForHouseLevel(5, HouseUpgradeNumbers.MaxLevel + 1),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>(),
                "the 5th ladder must have exactly MaxLevel levels");
        }

        [Test]
        public void Level1_IsAnchoredOnTheHouseStyleTableMesh()
        {
            // The table is "anchored on L1": every house's level-1 entry must
            // equal the as-built mesh HouseStyleTable assigns that house, so
            // a level-1 house renders exactly as it does today.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(HouseLevelModelTable.ForHouseLevel(lot.HouseId, HouseLevelModelTable.MinLevel),
                    Is.EqualTo(HouseStyleTable.ForHouse(lot.HouseId).ModelName),
                    $"house {lot.HouseId} level-1 mesh must match its HouseStyleTable model");
            }
        }

        [Test]
        public void EveryHouseLot_ResolvesAllFourLevels()
        {
            // Completeness guard: every house the lot mapping can reference
            // resolves a mesh for each level MinLevel..MaxLevel, and one past
            // the cap is out of range — this also ties the ladder length to
            // HouseUpgradeNumbers.MaxLevel.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                for (var level = HouseLevelModelTable.MinLevel; level <= HouseUpgradeNumbers.MaxLevel; level++)
                {
                    Assert.That(HouseLevelModelTable.ForHouseLevel(lot.HouseId, level),
                        Is.Not.Null.And.Not.Empty, $"house {lot.HouseId} level {level}");
                }

                Assert.That(() => HouseLevelModelTable.ForHouseLevel(lot.HouseId, HouseUpgradeNumbers.MaxLevel + 1),
                    Throws.InstanceOf<System.ArgumentOutOfRangeException>(),
                    $"house {lot.HouseId} must have exactly MaxLevel levels");
            }
        }

        [Test]
        public void EachHouse_HasFourDistinctMeshesAcrossItsLevels()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var meshes = new List<string>();
                for (var level = HouseLevelModelTable.MinLevel; level <= HouseUpgradeNumbers.MaxLevel; level++)
                {
                    meshes.Add(HouseLevelModelTable.ForHouseLevel(lot.HouseId, level));
                }

                Assert.That(meshes, Is.Unique, $"house {lot.HouseId} reuses a mesh across its levels");
            }
        }

        [Test]
        public void HasHouse_IsTrueOnlyForTheStartingHouseLots()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(HouseLevelModelTable.HasHouse(lot.HouseId), Is.True);
            }

            Assert.That(HouseLevelModelTable.HasHouse(999), Is.False);
        }

        [Test]
        public void ForHouseLevel_UnknownHouseThrows()
        {
            Assert.That(() => HouseLevelModelTable.ForHouseLevel(999, HouseLevelModelTable.MinLevel),
                Throws.ArgumentException);
        }

        [Test]
        public void ForHouseLevel_LevelBelowMinimumThrows()
        {
            Assert.That(() => HouseLevelModelTable.ForHouseLevel(1, HouseLevelModelTable.MinLevel - 1),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        private static void AssertLadder(int houseId, params string[] expected)
        {
            for (var i = 0; i < expected.Length; i++)
            {
                var level = HouseLevelModelTable.MinLevel + i;
                Assert.That(HouseLevelModelTable.ForHouseLevel(houseId, level), Is.EqualTo(expected[i]),
                    $"house {houseId} level {level}");
            }
        }
    }
}
