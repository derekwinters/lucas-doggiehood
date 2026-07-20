using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// Bridges MoveInSystem's abstract VacantHouses id set (#54) to real
    /// House objects (#58): vacancy must flip to occupied exactly when a
    /// household moves into that house, and never otherwise.
    /// </summary>
    public class HouseOccupancyTests
    {
        [Test]
        public void SuccessfulMoveIn_FlipsExactlyTheChosenHouse_AndNoOtherHouse()
        {
            var houses = new List<House>
            {
                new House(1, Quadrant.NorthEast), // vacant by default
                new House(2, Quadrant.NorthWest), // vacant by default
                new House(3, Quadrant.SouthEast, isVacant: false),
            };
            var moveIn = new MoveInSystem();

            var household = HouseOccupancy.ApplyMoveIn(houses, moveIn, Array.Empty<Dog>(), new SequenceRandom(0.0));

            Assert.That(household, Is.Not.Empty);
            var occupiedHouseId = household[0].HouseId;
            Assert.That(new[] { 1, 2 }, Does.Contain(occupiedHouseId));

            foreach (var house in houses)
            {
                var expectedVacant = house.Id != occupiedHouseId && house.Id != 3;
                Assert.That(house.IsVacant, Is.EqualTo(expectedVacant),
                    $"house {house.Id} vacancy changed unexpectedly");
            }
        }

        [Test]
        public void FailedRoll_LeavesEveryHousesVacancyUnchanged()
        {
            var houses = new List<House>
            {
                new House(1, Quadrant.NorthEast),
                new House(2, Quadrant.NorthWest, isVacant: false),
            };
            var moveIn = new MoveInSystem();

            var household = HouseOccupancy.ApplyMoveIn(houses, moveIn, Array.Empty<Dog>(), new SequenceRandom(0.99));

            Assert.That(household, Is.Empty);
            Assert.That(houses[0].IsVacant, Is.True);
            Assert.That(houses[1].IsVacant, Is.False);
        }

        [Test]
        public void NoVacantHouses_NeverFlipsAnything_RegardlessOfTheRoll()
        {
            var houses = new List<House> { new House(1, Quadrant.NorthEast, isVacant: false) };
            var moveIn = new MoveInSystem();

            var household = HouseOccupancy.ApplyMoveIn(houses, moveIn, Array.Empty<Dog>(), new SequenceRandom(0.0));

            Assert.That(household, Is.Empty);
            Assert.That(houses[0].IsVacant, Is.False);
        }
    }
}
