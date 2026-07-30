using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #299 "assign once, persist": a zone-built house (id >= 5) rolls its
    /// <see cref="HouseVariant"/> (ladder + tint) once at build and keeps it
    /// unchanged across app relaunches (SaveCodec round-trip) and across its
    /// L1->L4 upgrades. The variant is deterministic
    /// (<see cref="HouseVariantAssignment"/>), and its ladder id / tint INDEX
    /// are stored in the save so they survive an RNG or palette retune.
    /// </summary>
    public class HouseVariantPersistenceTests
    {
        private static GameState UnlockedGameWithFunds(int extraCoins)
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost + HouseBuildNumbers.Cost + extraCoins);
            Assert.That(state.TryUnlockNextZone(), Is.True, "precondition: the first zone unlocks");
            return state;
        }

        [Test]
        public void BuiltZoneHouse_GetsItsDeterministicVariant()
        {
            var state = UnlockedGameWithFunds(0);
            var lotId = ZoneCatalog.FirstZone.Lots.First().HouseId;

            Assert.That(state.TryBuildHouse(lotId), Is.True, "precondition: the house builds");

            var house = state.Houses.First(h => h.Id == lotId);
            var expected = HouseVariantAssignment.ForHouse(lotId);
            Assert.That(house.Variant.HasValue, Is.True, "a zone house carries a rolled variant");
            Assert.That(house.Variant.Value.LadderId, Is.EqualTo(expected.LadderId));
            Assert.That(house.Variant.Value.TintIndex, Is.EqualTo(expected.TintIndex));
        }

        [Test]
        public void StarterHouse_HasNoRolledVariant()
        {
            var state = GameState.CreateNew();
            foreach (var house in state.Houses)
            {
                Assert.That(house.Variant.HasValue, Is.False, $"starter house {house.Id} keeps its fixed ladder");
            }
        }

        [Test]
        public void ZoneHouseVariant_RoundTripsThroughSaveCodec_Unchanged()
        {
            var state = UnlockedGameWithFunds(0);
            var lotId = ZoneCatalog.FirstZone.Lots.First().HouseId;
            state.TryBuildHouse(lotId);
            var built = state.Houses.First(h => h.Id == lotId).Variant.Value;

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            var house = reloaded.Houses.FirstOrDefault(h => h.Id == lotId);
            Assert.That(house, Is.Not.Null, "the built zone house survives the save round-trip");
            Assert.That(house.Variant.HasValue, Is.True);
            Assert.That(house.Variant.Value.LadderId, Is.EqualTo(built.LadderId), "ladder survives save/load");
            Assert.That(house.Variant.Value.TintIndex, Is.EqualTo(built.TintIndex), "tint index survives save/load");
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel), "level survives save/load");
            Assert.That(house.IsVacant, Is.True, "a freshly built house is vacant, and that survives save/load");
        }

        [Test]
        public void ZoneHouseVariant_IsUnchangedAcrossL1ToL4Upgrades()
        {
            var state = UnlockedGameWithFunds(
                HouseUpgradeNumbers.CostToReach(2)
                + HouseUpgradeNumbers.CostToReach(3)
                + HouseUpgradeNumbers.CostToReach(4));
            var lotId = ZoneCatalog.FirstZone.Lots.First().HouseId;
            state.TryBuildHouse(lotId);
            var atLevel1 = state.Houses.First(h => h.Id == lotId).Variant.Value;

            Assert.That(state.TryUpgradeHouse(lotId), Is.True, "L1->L2");
            Assert.That(state.TryUpgradeHouse(lotId), Is.True, "L2->L3");
            Assert.That(state.TryUpgradeHouse(lotId), Is.True, "L3->L4");

            var house = state.Houses.First(h => h.Id == lotId);
            Assert.That(house.Level, Is.EqualTo(HouseUpgradeNumbers.MaxLevel), "reached L4");
            Assert.That(house.Variant.Value.LadderId, Is.EqualTo(atLevel1.LadderId), "ladder unchanged by upgrades");
            Assert.That(house.Variant.Value.TintIndex, Is.EqualTo(atLevel1.TintIndex), "tint unchanged by upgrades");

            // ...and the upgraded house + its variant still round-trip.
            var reloaded = SaveCodec.Load(SaveCodec.Save(state));
            var reloadedHouse = reloaded.Houses.First(h => h.Id == lotId);
            Assert.That(reloadedHouse.Level, Is.EqualTo(HouseUpgradeNumbers.MaxLevel));
            Assert.That(reloadedHouse.Variant.Value.LadderId, Is.EqualTo(atLevel1.LadderId));
            Assert.That(reloadedHouse.Variant.Value.TintIndex, Is.EqualTo(atLevel1.TintIndex));
        }

        [Test]
        public void FreshGame_WithNoZoneHouses_RoundTripsWithNoBuiltHouses()
        {
            var reloaded = SaveCodec.Load(SaveCodec.Save(GameState.CreateNew()));

            // Only the 4 starters, none carrying a rolled variant.
            Assert.That(reloaded.Houses.Count, Is.EqualTo(GameState.CreateNew().Houses.Count));
            Assert.That(reloaded.Houses.All(h => !h.Variant.HasValue), Is.True);
        }
    }
}
