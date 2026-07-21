using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #57: tapping an empty lot's marker in an unlocked zone invokes
    /// GameState.TryBuildHouse and, on success, swaps the marker for the
    /// real house visual. Same Init/tap-simulation pattern as
    /// QuestDirectorTests (director.Init wires the real tap events; tests
    /// simulate a tap by calling the view's OnTapped directly).
    /// </summary>
    public class ExpansionDirectorTests
    {
        private GameObject worldRoot;
        private GameState state;
        private ExpansionDirector director;

        [SetUp]
        public void BuildWorldWithAnUnlockedZoneAndDirector()
        {
            state = GameState.CreateNew();
            state.Wallet.Deposit(150); // 100 to unlock the first zone + 50 to build a house
            state.TryUnlockNextZone();

            worldRoot = WorldBuilder.Build(state);

            var host = new GameObject("expansion-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<ExpansionDirector>();
            director.Init(state, worldRoot.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(worldRoot);
        }

        [Test]
        public void TappingAnEmptyLot_BuildsTheHouse_DeductsTheCost_AndSwapsTheMarkerForAHouseView()
        {
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var coinsBefore = state.Wallet.Coins;

            lotView.OnTapped();

            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore - HouseBuildNumbers.Cost));
            var house = state.Houses.SingleOrDefault(h => h.Id == houseId);
            Assert.That(house, Is.Not.Null);
            Assert.That(house.IsVacant, Is.True);
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel));

            var remainingMarkers = worldRoot.GetComponentsInChildren<EmptyLotView>();
            Assert.That(remainingMarkers.Select(v => v.HouseId).ToList(), Has.No.Member(houseId));

            var houseView = worldRoot.GetComponentsInChildren<HouseView>().SingleOrDefault(h => h.HouseId == houseId);
            Assert.That(houseView, Is.Not.Null, "the built house should get a real HouseView in the scene");
        }

        [Test]
        public void TappingAnEmptyLot_Fails_WhenTheBalanceIsInsufficient()
        {
            state.Wallet.TrySpend(state.Wallet.Coins); // drain the wallet to 0
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;

            lotView.OnTapped();

            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.False);
            var remainingMarkers = worldRoot.GetComponentsInChildren<EmptyLotView>();
            Assert.That(remainingMarkers.Select(v => v.HouseId), Does.Contain(houseId));
            Assert.That(worldRoot.GetComponentsInChildren<HouseView>().Any(h => h.HouseId == houseId), Is.False);
        }
    }
}
