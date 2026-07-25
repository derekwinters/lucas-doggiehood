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

        [Test]
        public void TryBuildHouse_Succeeds_DeductsTheFlatCost_AndAddsALevelOneVacantHouse_OnAnEmptyLotInAnUnlockedZone()
        {
            // #57: 100 to unlock the first zone + 50 (HouseBuildNumbers.Cost)
            // to build on one of its lots.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(150);
            state.TryUnlockNextZone();
            var lot = state.UnlockedZones[0].Lots[0];

            var built = state.TryBuildHouse(lot.HouseId);

            Assert.That(built, Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.Houses.Count, Is.EqualTo(5));

            var house = state.Houses.Single(h => h.Id == lot.HouseId);
            Assert.That(house.Quadrant, Is.EqualTo(lot.Quadrant));
            Assert.That(house.IsVacant, Is.True);
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel));
        }

        [Test]
        public void TryBuildHouse_Fails_WhenTheLotIsAlreadyOccupied()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(200);
            state.TryUnlockNextZone();
            var lot = state.UnlockedZones[0].Lots[0];
            state.TryBuildHouse(lot.HouseId);
            var coinsAfterFirstBuild = state.Wallet.Coins;

            var builtAgain = state.TryBuildHouse(lot.HouseId);

            Assert.That(builtAgain, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsAfterFirstBuild));
            Assert.That(state.Houses.Count(h => h.Id == lot.HouseId), Is.EqualTo(1));
        }

        [Test]
        public void TryBuildHouse_Fails_WhenTheZoneIsLocked()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(50); // affordable, but no zone unlocked yet
            var lockedLot = ZoneCatalog.FirstZone.Lots[0];

            var built = state.TryBuildHouse(lockedLot.HouseId);

            Assert.That(built, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(50));
            Assert.That(state.Houses.Count, Is.EqualTo(4));
        }

        [Test]
        public void TryBuildHouse_Fails_WhenTheBalanceIsInsufficient()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            state.TryUnlockNextZone(); // spends all 100; wallet is now 0
            var lot = state.UnlockedZones[0].Lots[0];

            var built = state.TryBuildHouse(lot.HouseId);

            Assert.That(built, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.Houses.Count, Is.EqualTo(4));
        }

        [Test]
        public void GetHouseLot_ResolvesAZoneLot_AfterItsZoneIsUnlocked()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            state.TryUnlockNextZone();
            var expectedLot = state.UnlockedZones[0].Lots[0];

            var lot = state.GetHouseLot(expectedLot.HouseId);

            Assert.That(lot.Quadrant, Is.EqualTo(expectedLot.Quadrant));
            Assert.That(lot.Position.X, Is.EqualTo(expectedLot.Position.X));
            Assert.That(lot.Position.Z, Is.EqualTo(expectedLot.Position.Z));
        }

        [Test]
        public void GetHouseLot_ResolvesAStartingLayoutLot()
        {
            var state = GameState.CreateNew();

            var lot = state.GetHouseLot(1);

            Assert.That(lot.Quadrant, Is.EqualTo(NeighborhoodLayout.GetHouseLot(1).Quadrant));
        }

        [Test]
        public void GetHouseLot_Throws_ForAnUnknownHouseId()
        {
            var state = GameState.CreateNew();

            Assert.Throws<ArgumentException>(() => state.GetHouseLot(-1));
        }

        [Test]
        public void TryUpgradeHouse_MovesLevelOneToTwo_DebitingTheNamedCost()
        {
            // #59: the first upgrade step charges HouseUpgradeNumbers'
            // named 100-coin constant and raises the house from level 1 to
            // level 2 — the same charge-then-mutate pattern as
            // TryBuildHouse. A starting house is level 1.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2);
            var house = state.Houses.First();
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel), "sanity: starts at level 1");

            var upgraded = state.TryUpgradeHouse(house.Id);

            Assert.That(upgraded, Is.True);
            Assert.That(house.Level, Is.EqualTo(2));
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
        }

        [Test]
        public void TryUpgradeHouse_UnknownHouse_IsRejectedWithNoStateChange()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2);

            var upgraded = state.TryUpgradeHouse(9999);

            Assert.That(upgraded, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(Expansion.HouseUpgradeNumbers.CostToLevel2));
            Assert.That(state.Houses, Has.All.Property("Level").EqualTo(House.InitialLevel));
        }

        [Test]
        public void TryUpgradeHouse_ClimbsTwoToThreeToFour_DebitingTheDoublingCosts()
        {
            // #59: sequential upgrades charge 200 then 400 (the named
            // doubling constants) as the house climbs to the level-4 cap.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2
                + Expansion.HouseUpgradeNumbers.CostToLevel3
                + Expansion.HouseUpgradeNumbers.CostToLevel4);
            var house = state.Houses.First();

            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(2));
            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(3));
            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(Expansion.HouseUpgradeNumbers.MaxLevel));
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
        }

        [Test]
        public void TryUpgradeHouse_AtMaxLevel_IsRejectedWithNoStateChange()
        {
            // #59: upgrading past level 4 is rejected — no deduction, level
            // unchanged. The wallet is left flush with coins to prove the
            // rejection is the cap, not affordability.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2
                + Expansion.HouseUpgradeNumbers.CostToLevel3
                + Expansion.HouseUpgradeNumbers.CostToLevel4);
            var house = state.Houses.First();
            state.TryUpgradeHouse(house.Id);
            state.TryUpgradeHouse(house.Id);
            state.TryUpgradeHouse(house.Id);
            Assert.That(house.Level, Is.EqualTo(Expansion.HouseUpgradeNumbers.MaxLevel), "sanity: at the cap");
            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel4);
            var coinsAtCap = state.Wallet.Coins;

            var upgraded = state.TryUpgradeHouse(house.Id);

            Assert.That(upgraded, Is.False);
            Assert.That(house.Level, Is.EqualTo(Expansion.HouseUpgradeNumbers.MaxLevel));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsAtCap));
        }

        [Test]
        public void TryUpgradeHouse_InsufficientBalance_IsRejected_LevelAndBalanceUnchanged()
        {
            // #59: an unaffordable upgrade leaves both the level and the
            // balance untouched (Wallet.TrySpend never deducts on a
            // rejected spend).
            var state = GameState.CreateNew();
            state.Wallet.Deposit(Expansion.HouseUpgradeNumbers.CostToLevel2 - 1);
            var house = state.Houses.First();
            var coinsBefore = state.Wallet.Coins;

            var upgraded = state.TryUpgradeHouse(house.Id);

            Assert.That(upgraded, Is.False);
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore));
        }
    }
}
