using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            Assert.That(state.Map.HasTileAt(new TileCoordinate(0, 0)), Is.True);
            Assert.That(state.Map.GetTileAt(new TileCoordinate(0, 0)), Is.EqualTo(TileType.FourWay));
            Assert.That(state.UnlockedTiles, Is.Empty);
        }

        [Test]
        public void TryUnlockTile_Fails_WhenTheWalletCannotAffordTheCost()
        {
            // #295: a fresh GameState starts with 0 coins, well below the
            // flat per-tile 100-coin cost.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            var unlocked = state.TryUnlockTile(FrontierTestWorld.FirstTile);

            Assert.That(unlocked, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.UnlockedTiles, Is.Empty);
            Assert.That(state.Map.HasTileAt(new TileCoordinate(0, 1)), Is.False);
        }

        [Test]
        public void TryUnlockTile_Succeeds_DeductsCostAndPlacesTheTile_WhenAffordable()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));

            var unlocked = state.TryUnlockTile(FrontierTestWorld.FirstTile);

            Assert.That(unlocked, Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.UnlockedTiles.Count, Is.EqualTo(1));
            Assert.That(state.Map.GetTileAt(new TileCoordinate(0, 1)), Is.EqualTo(TileType.CulDeSacSouth));
        }

        [Test]
        public void TryUnlockTile_FreshlyUnlockedTile_HasZeroHouses_AllLotsBuildable()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));

            state.TryUnlockTile(FrontierTestWorld.FirstTile);

            var lots = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile);
            Assert.That(lots, Is.Not.Empty);
            Assert.That(state.Houses.Count, Is.EqualTo(4));
            foreach (var lot in lots)
            {
                Assert.That(state.IsLotBuildable(lot.HouseId), Is.True);
            }
        }

        [Test]
        public void LotsForUnlockedTile_NonOriginFourWay_YieldsFourBuildableQuadrantLots()
        {
            // #607: a non-origin FourWay is a full intersection — all four
            // quadrants border a straight roaded edge square-on, so it carries
            // one buildable lot per quadrant, the same treatment every other
            // lotted tile gets. (It previously came in with zero lots.)
            var fourWayCoordinate = new TileCoordinate(1, 0);
            var state = GameState.CreateNew();
            // Adjacent to the origin FourWay (roads agree on the shared edge),
            // placed straight onto the map via the restore path — no target map
            // or wallet needed to exercise the lot generation.
            state.RestoreUnlockedTile(fourWayCoordinate, TileType.FourWay);

            var lots = state.LotsForUnlockedTile(fourWayCoordinate);

            Assert.That(lots.Count, Is.EqualTo(4));
            var center = TileGeometry.CenterOf(fourWayCoordinate);
            float d = NeighborhoodLayout.LotDistanceFromCenter;
            var expected = new System.Collections.Generic.Dictionary<Quadrant, GridPoint>
            {
                { Quadrant.NorthEast, new GridPoint(center.X + d, center.Z + d) },
                { Quadrant.NorthWest, new GridPoint(center.X - d, center.Z + d) },
                { Quadrant.SouthEast, new GridPoint(center.X + d, center.Z - d) },
                { Quadrant.SouthWest, new GridPoint(center.X - d, center.Z - d) },
            };
            CollectionAssert.AreEquivalent(expected.Keys, lots.Select(lot => lot.Quadrant));
            foreach (var lot in lots)
            {
                Assert.That(lot.HouseId, Is.EqualTo(FrontierHouseId.For(fourWayCoordinate, lot.Quadrant)),
                    "each lot carries the stable position-derived frontier id");
                Assert.That(lot.Position.X, Is.EqualTo(expected[lot.Quadrant].X));
                Assert.That(lot.Position.Z, Is.EqualTo(expected[lot.Quadrant].Z));
                Assert.That(state.IsLotBuildable(lot.HouseId), Is.True,
                    "a freshly unlocked FourWay's lots are all buildable");
            }
        }

        [Test]
        public void LotsForUnlockedTile_OriginFourWay_YieldsNoCatalogLots()
        {
            // #607: the origin FourWay's four lots live in NeighborhoodLayout
            // (ids 1-4, seeded at CreateNew). LotsForUnlockedTile must NOT also
            // emit catalog lots for the origin coordinate — that would collide
            // with / double-count the seeded origin houses.
            var state = GameState.CreateNew();

            Assert.That(state.LotsForUnlockedTile(new TileCoordinate(0, 0)), Is.Empty);
        }

        [Test]
        public void OriginFourWay_HouseLots_AreUnchanged_ByTheFrontierFourWayFix()
        {
            // #607 regression guard: the seeded origin lots keep their exact
            // NeighborhoodLayout ids/quadrants/positions (byte-identical before
            // and after the FourWay lot fix).
            var lots = NeighborhoodLayout.HouseLots;
            float d = NeighborhoodLayout.LotDistanceFromCenter;

            Assert.That(lots.Count, Is.EqualTo(4));
            Assert.That(lots.Select(lot => lot.HouseId), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            void AssertLot(int id, Quadrant quadrant, float x, float z)
            {
                var lot = lots.Single(candidate => candidate.HouseId == id);
                Assert.That(lot.Quadrant, Is.EqualTo(quadrant));
                Assert.That(lot.Position.X, Is.EqualTo(x));
                Assert.That(lot.Position.Z, Is.EqualTo(z));
            }

            AssertLot(1, Quadrant.NorthEast, d, d);
            AssertLot(2, Quadrant.NorthWest, -d, d);
            AssertLot(3, Quadrant.SouthEast, d, -d);
            AssertLot(4, Quadrant.SouthWest, -d, -d);
        }

        [Test]
        public void TryUnlockTile_Fails_WhenTheCoordinateIsAlreadyPlaced()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(1_000_000);

            state.TryUnlockTile(FrontierTestWorld.FirstTile);
            var coinsAfterFirstUnlock = state.Wallet.Coins;
            var secondAttempt = state.TryUnlockTile(FrontierTestWorld.FirstTile);

            Assert.That(secondAttempt, Is.False, "a placed coordinate is no longer on the frontier");
            Assert.That(state.UnlockedTiles.Count, Is.EqualTo(1));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsAfterFirstUnlock));
        }

        [Test]
        public void TryBuildHouse_Succeeds_DeductsTheFlatCost_AndAddsALevelOneVacantHouse_OnAnEmptyLotInAnUnlockedZone()
        {
            // #57/#540: the first tile's unlock cost + HouseBuildNumbers' base
            // to build on one of its lots — read from the live pricing seams so
            // a rebalance (#674) can't strand this setup.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count)
                + HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
            state.TryUnlockTile(FrontierTestWorld.FirstTile);
            var lot = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile)[0];

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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count)
                + HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
            state.TryUnlockTile(FrontierTestWorld.FirstTile);
            var lot = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile)[0];
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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(50); // affordable, but no zone unlocked yet
            var lockedLot = FrontierTestWorld.FirstTileLots()[0];

            var built = state.TryBuildHouse(lockedLot.HouseId);

            Assert.That(built, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(50));
            Assert.That(state.Houses.Count, Is.EqualTo(4));
        }

        [Test]
        public void TryBuildHouse_Fails_WhenTheBalanceIsInsufficient()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            state.TryUnlockTile(FrontierTestWorld.FirstTile); // spends the whole unlock cost; wallet is now 0
            var lot = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile)[0];

            var built = state.TryBuildHouse(lot.HouseId);

            Assert.That(built, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.Houses.Count, Is.EqualTo(4));
        }

        [Test]
        public void GetHouseLot_ResolvesAZoneLot_AfterItsZoneIsUnlocked()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            state.TryUnlockTile(FrontierTestWorld.FirstTile);
            var expectedLot = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile)[0];

            var lot = state.GetHouseLot(expectedLot.HouseId);

            Assert.That(lot.Quadrant, Is.EqualTo(expectedLot.Quadrant));
            Assert.That(lot.Position.X, Is.EqualTo(expectedLot.Position.X));
            Assert.That(lot.Position.Z, Is.EqualTo(expectedLot.Position.Z));
        }

        [Test]
        public void GetHouseLot_ResolvesAStartingLayoutLot()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            var lot = state.GetHouseLot(1);

            Assert.That(lot.Quadrant, Is.EqualTo(NeighborhoodLayout.GetHouseLot(1).Quadrant));
        }

        [Test]
        public void GetHouseLot_Throws_ForAnUnknownHouseId()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2);
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
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2);

            var upgraded = state.TryUpgradeHouse(9999);

            Assert.That(upgraded, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2));
            Assert.That(state.Houses, Has.All.Property("Level").EqualTo(House.InitialLevel));
        }

        [Test]
        public void TryUpgradeHouse_ClimbsTwoToThreeToFour_DebitingTheDoublingCosts()
        {
            // #59: sequential upgrades charge 200 then 400 (the named
            // doubling constants) as the house climbs to the level-4 cap.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2
                + Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel3
                + Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel4);
            var house = state.Houses.First();

            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(2));
            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(3));
            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel));
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
        }

        [Test]
        public void TryUpgradeHouse_AtMaxLevel_IsRejectedWithNoStateChange()
        {
            // #59: upgrading past level 4 is rejected — no deduction, level
            // unchanged. The wallet is left flush with coins to prove the
            // rejection is the cap, not affordability.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2
                + Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel3
                + Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel4);
            var house = state.Houses.First();
            state.TryUpgradeHouse(house.Id);
            state.TryUpgradeHouse(house.Id);
            state.TryUpgradeHouse(house.Id);
            Assert.That(house.Level, Is.EqualTo(Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel), "sanity: at the cap");
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel4);
            var coinsAtCap = state.Wallet.Coins;

            var upgraded = state.TryUpgradeHouse(house.Id);

            Assert.That(upgraded, Is.False);
            Assert.That(house.Level, Is.EqualTo(Doggiehood.Core.Expansion.HouseUpgradeNumbers.MaxLevel));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsAtCap));
        }

        [Test]
        public void TryUpgradeHouse_InsufficientBalance_IsRejected_LevelAndBalanceUnchanged()
        {
            // #59: an unaffordable upgrade leaves both the level and the
            // balance untouched (Wallet.TrySpend never deducts on a
            // rejected spend).
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2 - 1);
            var house = state.Houses.First();
            var coinsBefore = state.Wallet.Coins;

            var upgraded = state.TryUpgradeHouse(house.Id);

            Assert.That(upgraded, Is.False);
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore));
        }

        [Test]
        public void WalkNetwork_ForANewGame_IsConfinedToTheStartingIntersection()
        {
            // #398: before any zone is unlocked the live network is just the
            // starting FourWay — every sidewalk node sits within it (the
            // shared north edge is at TileSize/2 = 30).
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            Assert.That(state.WalkNetwork.Nodes, Has.All.Property("Z").LessThanOrEqualTo(WorldDimensions.TileSize / 2f + 0.001f));
        }

        [Test]
        public void WalkNetwork_SpansTheNewlyUnlockedTile_AfterTryUnlockTileSucceeds()
        {
            // #398: the whole point of the fix — once the cul-de-sac north of
            // the start is unlocked, the live network must extend onto it so
            // dogs can wander there.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));

            Assert.That(state.WalkNetwork.Nodes.Any(n => n.Z > WorldDimensions.TileSize / 2f + 0.001f), Is.False,
                "network already spanned the zone before unlocking");

            Assert.That(state.TryUnlockTile(FrontierTestWorld.FirstTile), Is.True);

            Assert.That(state.WalkNetwork.Nodes.Any(n => n.Z > WorldDimensions.TileSize / 2f + 0.001f), Is.True,
                "network did not grow onto the unlocked cul-de-sac tile");
            Assert.That(state.WalkNetwork.IsFullyConnected(), Is.True);
        }

        [Test]
        public void WalkNetwork_IsUnchanged_WhenTryUnlockTileFails()
        {
            // A failed unlock (can't afford it) changes nothing — the cached
            // live network is the very same instance afterwards.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var before = state.WalkNetwork;

            Assert.That(state.TryUnlockTile(FrontierTestWorld.FirstTile), Is.False);

            Assert.That(state.WalkNetwork, Is.SameAs(before));
        }

        [Test]
        public void WalkNetwork_Rebuilds_AfterTryBuildHouseSucceeds()
        {
            // #398: a successful build invalidates the cached live network so
            // the next read reflects the new house. (Since #430 a zone house
            // DOES gain a front walkway — the dedicated coverage lives in
            // ZoneHouseWalkwayTests; here the contract is just that the rebuild
            // happens, stays fully connected, and never throws on the lot.)
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(1000);
            state.TryUnlockTile(FrontierTestWorld.FirstTile);
            var lot = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile)[0];
            var before = state.WalkNetwork;

            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);

            Assert.That(state.WalkNetwork, Is.Not.SameAs(before), "the network was not rebuilt after a successful build");
            Assert.That(state.WalkNetwork.IsFullyConnected(), Is.True);
        }
    }
}
