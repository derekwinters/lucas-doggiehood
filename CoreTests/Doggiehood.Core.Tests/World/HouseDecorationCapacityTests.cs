using System.Linq;
using Doggiehood.Core.Decorations;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #59: a house's yard holds at most as many decorations as its level
    /// (1/2/3/4). GameState.TryAddDecoration is the capacity-respecting
    /// placement path; the raw AddDecoration (save-load / grandfathered
    /// data) stays uncapped so nothing already placed is ever removed.
    /// </summary>
    public class HouseDecorationCapacityTests
    {
        private static Decoration BedFor(int houseId, int slot)
        {
            return new Decoration("bed", houseId, YardPlacement.PositionFor(houseId, slot));
        }

        [Test]
        public void DecorationCapacity_EqualsTheHouseLevel()
        {
            var state = GameState.CreateNew();
            var house = state.Houses.First();

            Assert.That(state.DecorationCapacityForHouse(house.Id), Is.EqualTo(1));

            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2);
            state.TryUpgradeHouse(house.Id);

            Assert.That(state.DecorationCapacityForHouse(house.Id), Is.EqualTo(2));
        }

        [Test]
        public void TryAddDecoration_FillsUpToCapacity_ThenRejectsFurtherPlacements()
        {
            // A level-1 house has one slot: the first placement succeeds,
            // the second is rejected and never lands.
            var state = GameState.CreateNew();
            var house = state.Houses.First();

            Assert.That(state.TryAddDecoration(BedFor(house.Id, 0)), Is.True);
            Assert.That(state.DecorationCountForHouse(house.Id), Is.EqualTo(1));

            Assert.That(state.TryAddDecoration(BedFor(house.Id, 1)), Is.False);
            Assert.That(state.DecorationCountForHouse(house.Id), Is.EqualTo(1));
        }

        [Test]
        public void TryAddDecoration_AllowsMorePlacements_AfterAnUpgradeRaisesCapacity()
        {
            var state = GameState.CreateNew();
            var house = state.Houses.First();
            state.TryAddDecoration(BedFor(house.Id, 0));
            Assert.That(state.TryAddDecoration(BedFor(house.Id, 1)), Is.False, "sanity: full at level 1");

            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2);
            state.TryUpgradeHouse(house.Id);

            Assert.That(state.TryAddDecoration(BedFor(house.Id, 1)), Is.True);
            Assert.That(state.DecorationCountForHouse(house.Id), Is.EqualTo(2));
        }

        [Test]
        public void TryAddDecoration_ForAnUnknownHouse_IsRejected()
        {
            var state = GameState.CreateNew();

            Assert.That(state.TryAddDecoration(BedFor(9999, 0)), Is.False);
            Assert.That(state.DecorationCountForHouse(9999), Is.EqualTo(0));
        }

        [Test]
        public void Grandfathering_DecorationsAlreadyOverCapacity_AreNeverRemoved()
        {
            // #59 (Derek, 2026-07-14): the MVP flow auto-placed decorations
            // with no cap. When the cap ships, a level-1 yard that already
            // holds three decorations keeps all three — the cap only blocks
            // NEW placements, it never removes what's there.
            var state = GameState.CreateNew();
            var house = state.Houses.First();
            state.AddDecoration(BedFor(house.Id, 0));
            state.AddDecoration(BedFor(house.Id, 1));
            state.AddDecoration(BedFor(house.Id, 2));
            Assert.That(state.DecorationCountForHouse(house.Id), Is.EqualTo(3),
                "sanity: three grandfathered decorations on a level-1 house");

            var placed = state.TryAddDecoration(BedFor(house.Id, 3));

            Assert.That(placed, Is.False, "a new placement past the over-full cap is blocked");
            Assert.That(state.DecorationCountForHouse(house.Id), Is.EqualTo(3),
                "nothing already placed is ever removed");
        }
    }
}
