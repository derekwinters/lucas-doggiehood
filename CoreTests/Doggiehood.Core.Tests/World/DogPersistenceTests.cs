using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #704: every dog that has moved in is durable. Before this, only the
    /// four starting houses' 8 roster dogs ever existed — a moved-in dog was
    /// session-only, so a relaunch rebuilt the same 8 dogs while the house
    /// the household filled stayed persisted as occupied. That house then had
    /// no residents AND could never receive another move-in (occupancy only
    /// considers vacant houses), so it was permanently dead.
    /// </summary>
    public class DogPersistenceTests
    {
        /// <summary>A game with a frontier house built and a household moved
        /// into it — the exact state <c>HouseOccupancy.ApplyMoveIn</c> leaves
        /// behind (house occupied, its dog(s) on the live roster).</summary>
        private static GameState WithMovedInHousehold(params Dog[] household)
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked(HouseBuildNumbers.BaseCost);
            Assert.That(state.TryBuildHouse(FrontierTestWorld.FirstLotId), Is.True,
                "precondition: the frontier house builds");

            state.Houses.First(h => h.Id == FrontierTestWorld.FirstLotId).MarkOccupied();
            foreach (var dog in household)
            {
                state.AddDog(dog);
            }

            return state;
        }

        private static Dog Newcomer()
        {
            return new Dog("Biscuit", Breed.Puggle, Personality.Athletic,
                FrontierTestWorld.FirstLotId, isPuppy: true, CoatColor.Dark);
        }

        [Test]
        public void MovedInDog_RoundTripsThroughSaveCodec_WithItsWholeRecord()
        {
            var state = WithMovedInHousehold(Newcomer());

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            var dog = reloaded.Dogs.FirstOrDefault(d => d.Name == "Biscuit");
            Assert.That(dog, Is.Not.Null, "a moved-in dog survives the relaunch");
            Assert.That(dog.Breed, Is.EqualTo(Breed.Puggle), "breed survives");
            Assert.That(dog.Personality, Is.EqualTo(Personality.Athletic), "personality survives");
            Assert.That(dog.HouseId, Is.EqualTo(FrontierTestWorld.FirstLotId), "house survives");
            Assert.That(dog.IsPuppy, Is.True, "the puppy flag survives");
            Assert.That(dog.Coat, Is.EqualTo(CoatColor.Dark), "the coat survives");
        }

        [Test]
        public void MovedInHousehold_RoundTrips_LeavingItsHouseOccupied()
        {
            var state = WithMovedInHousehold(
                Newcomer(),
                new Dog("Crumb", Breed.Puggle, Personality.Shy,
                    FrontierTestWorld.FirstLotId, isPuppy: true));

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            var house = reloaded.Houses.First(h => h.Id == FrontierTestWorld.FirstLotId);
            Assert.That(house.IsVacant, Is.False, "the filled house stays occupied");
            Assert.That(reloaded.Dogs.Count(d => d.HouseId == house.Id), Is.EqualTo(2),
                "both members of the household come back");
        }

        [Test]
        public void StartingRoster_IsNeverDuplicated_ByARoundTrip()
        {
            var state = WithMovedInHousehold(Newcomer());
            var before = state.Dogs.Count;

            var once = SaveCodec.Load(SaveCodec.Save(state));
            var twice = SaveCodec.Load(SaveCodec.Save(once));

            Assert.That(once.Dogs.Count, Is.EqualTo(before), "one round-trip keeps the roster size");
            Assert.That(twice.Dogs.Count, Is.EqualTo(before), "and so does a second");
            Assert.That(twice.Dogs.Select(d => d.Name).Distinct().Count(), Is.EqualTo(before),
                "no dog is duplicated");
        }

        [Test]
        public void PopulationGrowth_SurvivesRelaunch_SoTheNeighborhoodIsNotPinnedAtEight()
        {
            var state = WithMovedInHousehold(Newcomer());
            var starting = GameState.CreateNew().Dogs.Count;

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.Dogs.Count, Is.GreaterThan(starting),
                "the neighborhood keeps the population it grew to");
        }

        // --- legacy saves written before #704 (no dog= lines at all) ---

        /// <summary>The exact bytes a pre-#704 build wrote: everything today's
        /// codec emits except the new dog= lines.</summary>
        private static string WithoutDogLines(string saved)
        {
            return string.Join("\n", saved.Split('\n').Where(line => !line.StartsWith("dog=")));
        }

        [Test]
        public void LegacySave_WithAnOccupiedHouseButNoResidents_ReVacatesIt()
        {
            var legacy = WithoutDogLines(SaveCodec.Save(WithMovedInHousehold(Newcomer())));

            var reloaded = SaveCodec.Load(legacy);

            var house = reloaded.Houses.First(h => h.Id == FrontierTestWorld.FirstLotId);
            Assert.That(reloaded.Dogs.Any(d => d.HouseId == house.Id), Is.False,
                "precondition: the legacy save carries no resident for that house");
            Assert.That(house.IsVacant, Is.True,
                "an occupied-but-empty house is re-vacated so it can receive a move-in again");
        }

        [Test]
        public void LegacySave_ReVacatedHouse_IsEligibleForAMoveInAgain()
        {
            var legacy = WithoutDogLines(SaveCodec.Save(WithMovedInHousehold(Newcomer())));

            var reloaded = SaveCodec.Load(legacy);

            Assert.That(reloaded.Houses.Where(h => h.IsVacant).Select(h => h.Id),
                Does.Contain(FrontierTestWorld.FirstLotId),
                "the repaired house is offered to the move-in system again");
        }

        [Test]
        public void Load_NeverVacatesAHouseThatStillHasResidents()
        {
            var state = WithMovedInHousehold(Newcomer());

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.Houses.Where(h => !h.IsVacant).Select(h => h.Id),
                Is.EquivalentTo(state.Houses.Where(h => !h.IsVacant).Select(h => h.Id)),
                "the repair only touches houses with nobody living in them");
        }
    }
}
