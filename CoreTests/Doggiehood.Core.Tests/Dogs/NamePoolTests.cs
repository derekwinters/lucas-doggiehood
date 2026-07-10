using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class NamePoolTests
    {
        [Test]
        public void PoolContainsExactlyTheDocumentedNames()
        {
            // #67 / docs/specs/dogs/roster-names.md. NOTE: the spec says
            // "66 names total" but actually enumerates 68 (33 classic + 19
            // food-themed + 16 softer). The enumerated list is implemented
            // verbatim; the count discrepancy is flagged on #67 for Derek
            // to resolve.
            Assert.That(NamePool.Names.Count, Is.EqualTo(68));
            Assert.That(NamePool.Names, Is.Unique);
            Assert.That(NamePool.Names, Does.Contain("Buddy"));
            Assert.That(NamePool.Names, Does.Contain("Biscuit"));
            Assert.That(NamePool.Names, Does.Contain("Bella"));
            Assert.That(NamePool.Names, Does.Not.Contain("Hank"));
            Assert.That(NamePool.Names, Does.Not.Contain("Stella"));
        }

        [Test]
        public void PickName_NeverReturnsANameInUse()
        {
            var inUse = new HashSet<string>(NamePool.Names.Take(60));
            var rng = new System.Random(42);

            for (var i = 0; i < 200; i++)
            {
                Assert.That(inUse, Does.Not.Contain(NamePool.PickName(rng, inUse)));
            }
        }

        [Test]
        public void PickName_IsDeterministicForASeed()
        {
            var inUse = new HashSet<string> { "Buddy", "Max" };

            var first = NamePool.PickName(new System.Random(7), inUse);
            var second = NamePool.PickName(new System.Random(7), inUse);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void PickName_ThrowsWhenEveryNameIsTaken()
        {
            var inUse = new HashSet<string>(NamePool.Names);

            Assert.That(() => NamePool.PickName(new System.Random(1), inUse),
                Throws.InvalidOperationException);
        }
    }

    public class EasterEggDogsTests
    {
        [Test]
        public void EachReservedName_AlwaysResolvesToItsFixedBreedAndCoat()
        {
            // #68: fixed pairings, never randomized.
            var expected = new (string Name, Breed Breed, CoatColor Coat)[]
            {
                ("Rex", Breed.GermanShepherd, CoatColor.Black),
                ("Arnie", Breed.GoldenRetriever, CoatColor.Light),
                ("Hank", Breed.GoldenRetriever, CoatColor.Dark),
                ("Stella", Breed.Chihuahua, CoatColor.Default),
                ("Muffin", Breed.Puggle, CoatColor.Default),
                ("Akon", Breed.Puggle, CoatColor.Default),
                ("Brody", Breed.GoldenRetriever, CoatColor.Default),
            };

            Assert.That(EasterEggDogs.ReservedNames.Count, Is.EqualTo(7));

            foreach (var (name, breed, coat) in expected)
            {
                // Repeated lookups with different seeds — the pairing is fixed.
                for (var seed = 0; seed < 25; seed++)
                {
                    var resolved = EasterEggDogs.Resolve(name);
                    Assert.That(resolved.Breed, Is.EqualTo(breed), name);
                    Assert.That(resolved.Coat, Is.EqualTo(coat), name);
                }
            }
        }

        [Test]
        public void ReservedNames_NeverAppearInTheGeneralPool()
        {
            foreach (var name in EasterEggDogs.ReservedNames)
            {
                Assert.That(NamePool.Names, Does.Not.Contain(name));
            }
        }

        [Test]
        public void UnknownName_IsNotAnEasterEgg()
        {
            Assert.That(EasterEggDogs.IsReserved("Buddy"), Is.False);
            Assert.That(() => EasterEggDogs.Resolve("Buddy"), Throws.ArgumentException);
        }
    }
}
