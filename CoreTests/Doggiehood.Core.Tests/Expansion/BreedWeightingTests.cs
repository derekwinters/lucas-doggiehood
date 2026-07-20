using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    public class BreedWeightingTests
    {
        private static Dog MakeDog(string name, Breed breed)
        {
            return new Dog(name, breed, Personality.Brave, houseId: 1, isPuppy: false);
        }

        [Test]
        public void EveryBreed_HasAPositiveChance_EvenWhenAnotherBreedDominates()
        {
            var dogs = Enumerable.Range(0, 20)
                .Select(i => MakeDog($"Dog{i}", Breed.Labrador))
                .ToList();

            var seen = new HashSet<Breed>();
            for (var seed = 0; seed < 800; seed++)
            {
                seen.Add(BreedWeighting.PickWeighted(dogs, new Random(seed)));
            }

            Assert.That(seen, Is.EquivalentTo(Enum.GetValues(typeof(Breed)).Cast<Breed>()));
        }

        [Test]
        public void LessCommonBreeds_AreChosenMoreOftenThanOverrepresentedOnes()
        {
            // #54: "each breed's weight is inversely proportional to how
            // many of that breed currently live in the neighborhood."
            var dogs = Enumerable.Range(0, 10)
                .Select(i => MakeDog($"Common{i}", Breed.Labrador))
                .ToList(); // Beagle stays at zero current count.

            var counts = new Dictionary<Breed, int>();
            for (var seed = 0; seed < 4000; seed++)
            {
                var picked = BreedWeighting.PickWeighted(dogs, new Random(seed));
                counts[picked] = counts.TryGetValue(picked, out var c) ? c + 1 : 1;
            }

            Assert.That(counts.GetValueOrDefault(Breed.Beagle), Is.GreaterThan(counts.GetValueOrDefault(Breed.Labrador)));
        }

        [Test]
        public void WeightsShift_AsTheNeighborhoodsDistributionChanges()
        {
            var before = new List<Dog> { MakeDog("A", Breed.Chihuahua) };
            var after = new List<Dog>(before);
            after.AddRange(Enumerable.Range(0, 15).Select(i => MakeDog($"Extra{i}", Breed.Chihuahua)));

            int CountChihuahuaPicks(IReadOnlyList<Dog> active)
            {
                var hits = 0;
                for (var seed = 0; seed < 2000; seed++)
                {
                    if (BreedWeighting.PickWeighted(active, new Random(seed)) == Breed.Chihuahua)
                    {
                        hits++;
                    }
                }

                return hits;
            }

            var beforeHits = CountChihuahuaPicks(before);
            var afterHits = CountChihuahuaPicks(after);

            Assert.That(afterHits, Is.LessThan(beforeHits),
                "Chihuahua's selection weight should drop as its current count grows");
        }
    }
}
