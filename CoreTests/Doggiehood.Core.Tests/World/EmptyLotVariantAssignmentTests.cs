using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #434/#453: a frontier lot's <see cref="HouseVariant"/> is rolled and
    /// PERSISTED when its tile unlocks (its lots first appear), not lazily at
    /// build time (#299's earlier timing). Building later reads that pre-assigned
    /// variant rather than re-rolling; a save with no persisted assignment still
    /// resolves the same variant through the deterministic
    /// <see cref="HouseVariantAssignment.ForHouse"/> fallback (the roll is a pure
    /// function of the house id, so the fallback is bit-identical).
    /// </summary>
    public class EmptyLotVariantAssignmentTests
    {
        [Test]
        public void UnlockingATile_AssignsAndPersistsAVariant_ForEveryLot()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked();

            foreach (var lot in state.LotsForUnlockedTile(FrontierTestWorld.FirstTile))
            {
                Assert.That(state.AssignedLotVariants.ContainsKey(lot.HouseId), Is.True,
                    $"lot {lot.HouseId} gets a variant assigned at unlock");
                var expected = HouseVariantAssignment.ForHouse(lot.HouseId);
                Assert.That(state.AssignedLotVariants[lot.HouseId].LadderId, Is.EqualTo(expected.LadderId));
                Assert.That(state.AssignedLotVariants[lot.HouseId].TintIndex, Is.EqualTo(expected.TintIndex));
            }
        }

        [Test]
        public void UnbuiltLotVariants_RoundTripThroughSaveCodec()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked();

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            foreach (var lot in state.LotsForUnlockedTile(FrontierTestWorld.FirstTile))
            {
                Assert.That(reloaded.AssignedLotVariants.ContainsKey(lot.HouseId), Is.True,
                    $"lot {lot.HouseId}'s assigned variant survives the save round-trip");
                var expected = state.AssignedLotVariants[lot.HouseId];
                Assert.That(reloaded.AssignedLotVariants[lot.HouseId].LadderId, Is.EqualTo(expected.LadderId));
                Assert.That(reloaded.AssignedLotVariants[lot.HouseId].TintIndex, Is.EqualTo(expected.TintIndex));
            }
        }

        [Test]
        public void TryBuildHouse_UsesThePersistedAssignedVariant_RatherThanReRolling()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked(HouseBuildNumbers.BaseCost);
            var lotId = FrontierTestWorld.FirstLotId;

            // Pin an assignment deliberately DIFFERENT from the deterministic
            // roll, so a build that read the persisted value can be told apart
            // from one that re-rolled.
            var rolled = HouseVariantAssignment.ForHouse(lotId);
            var pinned = new HouseVariant(
                rolled.LadderId % HouseVariantAssignment.LadderCount + 1,
                (rolled.TintIndex + 1) % HouseVariantAssignment.TintCount);
            Assert.That(pinned.LadderId, Is.Not.EqualTo(rolled.LadderId), "the pinned variant differs from the roll");
            state.RestoreAssignedLotVariant(lotId, pinned);

            Assert.That(state.TryBuildHouse(lotId), Is.True, "the house builds");

            var house = state.Houses.First(h => h.Id == lotId);
            Assert.That(house.Variant.Value.LadderId, Is.EqualTo(pinned.LadderId),
                "the built house carries the persisted assignment, not a fresh roll");
            Assert.That(house.Variant.Value.TintIndex, Is.EqualTo(pinned.TintIndex));
        }

        [Test]
        public void Save_WithUnlockedTileButNoLotVariantLines_BuildsViaDeterministicFallback()
        {
            // A save carrying tile= but no lotvariant= lines (e.g. an unbuilt lot
            // whose variant line was dropped) still resolves the same variant.
            var reloaded = SaveCodec.Load(
                "version=1\ncoins=" + HouseBuildNumbers.BaseCost + "\nonboarded=1\ntile=0|1|CulDeSacSouth\n");
            var lotId = FrontierTestWorld.FirstLotId;

            Assert.That(reloaded.AssignedLotVariants.ContainsKey(lotId), Is.False,
                "this save has no persisted lot-variant assignment");

            Assert.That(reloaded.TryBuildHouse(lotId), Is.True, "it still builds");
            var house = reloaded.Houses.First(h => h.Id == lotId);
            var expected = HouseVariantAssignment.ForHouse(lotId);
            Assert.That(house.Variant.Value.LadderId, Is.EqualTo(expected.LadderId),
                "the fallback roll is bit-identical to what unlock would have assigned");
            Assert.That(house.Variant.Value.TintIndex, Is.EqualTo(expected.TintIndex));
        }

        [Test]
        public void BuiltLot_IsPersistedAsAHouseLine_NotADuplicateLotVariantLine()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked(HouseBuildNumbers.BaseCost);
            var lotId = FrontierTestWorld.FirstLotId;
            Assert.That(state.TryBuildHouse(lotId), Is.True);

            var saved = SaveCodec.Save(state);

            Assert.That(saved, Does.Contain("house=" + lotId + "|"),
                "a built lot persists as a house line");
            Assert.That(saved, Does.Not.Contain("lotvariant=" + lotId + "|"),
                "a built lot must not ALSO emit an unbuilt-lot variant line");
        }
    }
}
