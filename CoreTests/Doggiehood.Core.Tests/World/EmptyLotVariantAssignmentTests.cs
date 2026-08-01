using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #434: a zone lot's <see cref="HouseVariant"/> is rolled and PERSISTED
    /// when the zone unlocks (its lots first appear), not lazily at build time
    /// (#299's earlier timing). Building later reads that pre-assigned variant
    /// rather than re-rolling; a legacy save with no persisted assignment still
    /// resolves the same variant through the deterministic
    /// <see cref="HouseVariantAssignment.ForHouse"/> fallback (the roll is a
    /// pure function of the house id, so the fallback is bit-identical).
    /// </summary>
    public class EmptyLotVariantAssignmentTests
    {
        private static GameState UnlockedGame(int extraCoins = 0)
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost + extraCoins);
            Assert.That(state.TryUnlockNextZone(), Is.True, "precondition: the first zone unlocks");
            return state;
        }

        [Test]
        public void UnlockingAZone_AssignsAndPersistsAVariant_ForEveryLot()
        {
            var state = UnlockedGame();
            var zone = state.UnlockedZones[0];

            foreach (var lot in zone.Lots)
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
            var state = UnlockedGame();

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            foreach (var lot in state.UnlockedZones[0].Lots)
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
            var state = UnlockedGame(HouseBuildNumbers.Cost);
            var lotId = ZoneCatalog.FirstZone.Lots.First().HouseId;

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
        public void LegacySave_WithUnlockedZoneButNoLotVariantLines_BuildsViaDeterministicFallback()
        {
            // A pre-#434 save carries zones= but no lotvariant= lines.
            var reloaded = SaveCodec.Load(
                "version=1\ncoins=" + HouseBuildNumbers.Cost + "\nonboarded=1\nzones=1\n");
            var lotId = ZoneCatalog.FirstZone.Lots.First().HouseId;

            Assert.That(reloaded.AssignedLotVariants.ContainsKey(lotId), Is.False,
                "a legacy save has no persisted lot-variant assignment");

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
            var state = UnlockedGame(HouseBuildNumbers.Cost);
            var lotId = ZoneCatalog.FirstZone.Lots.First().HouseId;
            Assert.That(state.TryBuildHouse(lotId), Is.True);

            var saved = SaveCodec.Save(state);

            Assert.That(saved, Does.Contain("house=" + lotId + "|"),
                "a built lot persists as a house line");
            Assert.That(saved, Does.Not.Contain("lotvariant=" + lotId + "|"),
                "a built lot must not ALSO emit an unbuilt-lot variant line");
        }
    }
}
