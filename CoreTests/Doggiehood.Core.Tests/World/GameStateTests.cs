using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class GameStateTests
    {
        [Test]
        public void CreateNew_ContainsExactlyFourHouses()
        {
            Assert.That(GameState.CreateNew().Houses.Count, Is.EqualTo(4));
        }

        [Test]
        public void CreateNew_StartingHousesAreAlreadyOccupied()
        {
            // #58: the 4 starting houses already have dogs living in them
            // (#63) — they must never report vacant.
            Assert.That(GameState.CreateNew().Houses, Has.All.Property("IsVacant").False);
        }

        [Test]
        public void HandleQuestCompleted_IsANoOp_WhenNoHouseIsVacant()
        {
            // #58/#54: GameState is wired to the move-in system, but with
            // every starting house occupied there is nothing to fill —
            // the pity counter must not advance and the roster must not
            // change, regardless of the roll.
            var state = GameState.CreateNew();
            var dogCountBefore = state.Dogs.Count;

            var moved = state.HandleQuestCompleted(new Random());

            Assert.That(moved, Is.Empty);
            Assert.That(state.Dogs.Count, Is.EqualTo(dogCountBefore));
            Assert.That(state.Houses, Has.All.Property("IsVacant").False);
        }

        [Test]
        public void AddDog_ExtendsTheLiveRoster()
        {
            // #54: a moved-in dog joins the live roster immediately.
            var state = GameState.CreateNew();
            var newDog = new Dog("Buddy", Breed.Beagle, Personality.Excited, houseId: 1, isPuppy: false);

            state.AddDog(newDog);

            Assert.That(state.Dogs.Count, Is.EqualTo(9));
            Assert.That(state.Dogs, Does.Contain(newDog));
        }

        [Test]
        public void Houses_HaveUniqueIds()
        {
            Assert.That(GameState.CreateNew().Houses.Select(h => h.Id), Is.Unique);
        }

        [Test]
        public void Houses_CoverAllFourQuadrants()
        {
            var quadrants = GameState.CreateNew().Houses.Select(h => h.Quadrant).ToList();

            Assert.That(quadrants, Is.Unique);
            Assert.That(quadrants.Count, Is.EqualTo(4));
        }

        [Test]
        public void Houses_MatchTheNeighborhoodLayoutLots()
        {
            foreach (var house in GameState.CreateNew().Houses)
            {
                var lot = NeighborhoodLayout.GetHouseLot(house.Id);
                Assert.That(house.Quadrant, Is.EqualTo(lot.Quadrant));
            }
        }

        [Test]
        public void CreateNew_MapIsSeededWithOnlyTheStartingFourWayIntersection()
        {
            var state = GameState.CreateNew();

            Assert.That(state.Map.HasTileAt(new TileCoordinate(0, 0)), Is.True);
            Assert.That(state.Map.GetTileAt(new TileCoordinate(0, 0)), Is.EqualTo(TileType.FourWay));
            Assert.That(state.UnlockedZones, Is.Empty);
        }

        [Test]
        public void TryUnlockNextZone_Fails_WhenTheWalletCannotAffordTheCost()
        {
            // #56: a fresh GameState starts with 0 coins, well below the
            // first zone's 100-coin cost.
            var state = GameState.CreateNew();

            var unlocked = state.TryUnlockNextZone();

            Assert.That(unlocked, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.UnlockedZones, Is.Empty);
            Assert.That(state.Map.HasTileAt(new TileCoordinate(0, 1)), Is.False);
        }

        [Test]
        public void TryUnlockNextZone_Succeeds_DeductsCostAndPlacesTheZonesTiles_WhenAffordable()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);

            var unlocked = state.TryUnlockNextZone();

            Assert.That(unlocked, Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.UnlockedZones.Count, Is.EqualTo(1));
            Assert.That(state.Map.GetTileAt(new TileCoordinate(0, 1)), Is.EqualTo(TileType.TurnSW));
            Assert.That(state.Map.GetTileAt(new TileCoordinate(-1, 1)), Is.EqualTo(TileType.CulDeSacEast));
        }

        [Test]
        public void TryUnlockNextZone_FreshlyUnlockedZone_HasZeroHouses_AllLotsBuildable()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);

            state.TryUnlockNextZone();

            var newZone = state.UnlockedZones[0];
            Assert.That(newZone.Lots, Is.Not.Empty);
            Assert.That(state.Houses.Count, Is.EqualTo(4));
            foreach (var lot in newZone.Lots)
            {
                Assert.That(state.IsLotBuildable(lot.HouseId), Is.True);
            }
        }

        [Test]
        public void TryUnlockNextZone_Fails_WhenNoMoreZonesAreAuthored()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1_000_000);

            state.TryUnlockNextZone();
            var coinsAfterFirstUnlock = state.Wallet.Coins;
            var secondAttempt = state.TryUnlockNextZone();

            Assert.That(secondAttempt, Is.False);
            Assert.That(state.UnlockedZones.Count, Is.EqualTo(1));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsAfterFirstUnlock));
        }
    }
}
