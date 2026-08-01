using Doggiehood.Core.Art;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    /// <summary>
    /// #464: the house -> model-resource-name resolution used by
    /// WorldBuilder.BuildHouse is now a single Core.Art function
    /// (<see cref="HouseModelResolver"/>) so the world and the house-profile
    /// render-to-texture snapshot resolve a house's CURRENT model from ONE
    /// source. These lock its output to exactly what WorldBuilder's inline
    /// branch produced: starter ids (1-4) walk their fixed
    /// <see cref="HouseLevelModelTable"/> ladder at the house's level; zone ids
    /// (>= 5) walk their rolled variant's ladder; an unresolvable house (a zone
    /// house with no rolled variant) yields null (the graybox fallback).
    /// </summary>
    public class HouseModelResolverTests
    {
        [Test]
        public void StarterHouse_ResolvesItsLadderMeshAtTheGivenLevel()
        {
            // A starter id (1-4) resolves HouseLevelModelTable.ForHouseLevel at
            // the house's current level — identical to WorldBuilder's non-zone
            // branch (HouseModelResourcePath(house.Id, house.Level)).
            for (var level = HouseLevelModelTable.MinLevel; level <= 4; level++)
            {
                Assert.That(
                    HouseModelResolver.ResolveModelName(1, level, null),
                    Is.EqualTo(HouseLevelModelTable.ForHouseLevel(1, level)),
                    $"house 1 at level {level}");
            }

            Assert.That(HouseModelResolver.ResolveModelName(1, 1, null), Is.EqualTo("building-type-r"));
            Assert.That(HouseModelResolver.ResolveModelName(1, 2, null), Is.EqualTo("building-type-c"));
            Assert.That(HouseModelResolver.ResolveModelName(3, 1, null), Is.EqualTo("building-type-k"));
        }

        [Test]
        public void ZoneHouse_ResolvesItsRolledVariantLadderMeshAtTheGivenLevel()
        {
            // A zone id (>= 5) resolves through its stored variant's ladder,
            // exactly like WorldBuilder's zone branch
            // (HouseLevelModelTable.ForHouseLevel(house.Variant.Value.LadderId, level)).
            var zoneId = HouseVariantAssignment.FirstZoneHouseId;
            var variant = HouseVariantAssignment.ForHouse(zoneId);

            for (var level = HouseLevelModelTable.MinLevel; level <= 4; level++)
            {
                Assert.That(
                    HouseModelResolver.ResolveModelName(zoneId, level, variant),
                    Is.EqualTo(HouseLevelModelTable.ForHouseLevel(variant.LadderId, level)),
                    $"zone house {zoneId} at level {level}");
            }
        }

        [Test]
        public void ZoneHouse_WithNoRolledVariant_ResolvesNull_MatchingTheGrayboxFallback()
        {
            // WorldBuilder resolves null (graybox) for a zone house whose
            // Variant is unset — the resolver mirrors that.
            Assert.That(
                HouseModelResolver.ResolveModelName(HouseVariantAssignment.FirstZoneHouseId, 1, null),
                Is.Null);
        }
    }
}
