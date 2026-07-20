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
    }
}
