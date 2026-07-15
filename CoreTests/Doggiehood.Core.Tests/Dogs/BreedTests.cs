using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class BreedTests
    {
        [Test]
        public void Breed_IsAnEnumOfRealBreedNames()
        {
            // #35: Dog data carries a Breed field drawn from an enum/table
            // of real, recognizable breed names (docs/specs/dogs/roster-names.md)
            // rather than a single generic/placeholder value.
            var values = Enum.GetValues(typeof(Breed)).Cast<Breed>().Select(b => b.ToString());

            Assert.That(values, Is.EquivalentTo(new[]
            {
                "GermanShepherd",
                "GoldenRetriever",
                "Labrador",
                "Beagle",
                "Chihuahua",
                "FrenchBulldog",
                "Puggle",
                "Frenchton",
            }));
        }

        [Test]
        public void Dog_ExposesTheBreedItWasConstructedWith()
        {
            var dog = new Dog("Zeus", Breed.GermanShepherd, Personality.Brave, 1, false);

            Assert.That(dog.Breed, Is.EqualTo(Breed.GermanShepherd));
        }
    }
}
