using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class DogRosterTests
    {
        [Test]
        public void ExactlyEightStartingDogs_MatchingTheRosterTable()
        {
            // #63 / docs/specs/dogs/roster-names.md
            var dogs = DogRoster.CreateStartingDogs();

            Assert.That(dogs.Count, Is.EqualTo(8));

            AssertDog(dogs, "Zeus", Breed.GermanShepherd, Personality.Brave, 1, false);
            AssertDog(dogs, "Nala", Breed.GermanShepherd, Personality.Excited, 1, true);
            AssertDog(dogs, "Bailey", Breed.GoldenRetriever, Personality.AdventurousExploring, 2, false);
            AssertDog(dogs, "Sunny", Breed.GoldenRetriever, Personality.Excited, 2, true);
            AssertDog(dogs, "Pepper", Breed.Chihuahua, Personality.Grumpy, 3, false);
            AssertDog(dogs, "Duke", Breed.Labrador, Personality.Brave, 4, false);
            AssertDog(dogs, "Scout", Breed.Beagle, Personality.AdventurousExploring, 4, false);
            AssertDog(dogs, "Waffles", Breed.Frenchton, Personality.Shy, 4, false);
        }

        private static void AssertDog(System.Collections.Generic.IReadOnlyList<Dog> dogs,
            string name, Breed breed, Personality personality, int houseId, bool isPuppy)
        {
            var dog = dogs.SingleOrDefault(d => d.Name == name);
            Assert.That(dog, Is.Not.Null, $"missing {name}");
            Assert.That(dog.Breed, Is.EqualTo(breed), name);
            Assert.That(dog.Personality, Is.EqualTo(personality), name);
            Assert.That(dog.HouseId, Is.EqualTo(houseId), name);
            Assert.That(dog.IsPuppy, Is.EqualTo(isPuppy), name);
        }

        [Test]
        public void HousePopulations_MatchTheSpec()
        {
            // #34/#63: House1=2 (parent+puppy), House2=2, House3=1, House4=3.
            var byHouse = DogRoster.CreateStartingDogs()
                .GroupBy(d => d.HouseId)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.That(byHouse[1], Is.EqualTo(2));
            Assert.That(byHouse[2], Is.EqualTo(2));
            Assert.That(byHouse[3], Is.EqualTo(1));
            Assert.That(byHouse[4], Is.EqualTo(3));
        }

        [Test]
        public void OnlyNalaAndSunny_ArePuppies()
        {
            var puppies = DogRoster.CreateStartingDogs().Where(d => d.IsPuppy).Select(d => d.Name);

            Assert.That(puppies, Is.EquivalentTo(new[] { "Nala", "Sunny" }));
        }

        [Test]
        public void GameState_IncludesTheStartingDogs()
        {
            Assert.That(GameState.CreateNew().Dogs.Count, Is.EqualTo(8));
        }

        [Test]
        public void EveryDog_LivesInAnExistingHouse()
        {
            foreach (var dog in DogRoster.CreateStartingDogs())
            {
                Assert.That(() => NeighborhoodLayout.GetHouseLot(dog.HouseId), Throws.Nothing);
            }
        }
    }
}
