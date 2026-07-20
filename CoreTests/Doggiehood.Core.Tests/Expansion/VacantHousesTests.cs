using System.Collections.Generic;
using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    public class VacantHousesTests
    {
        [Test]
        public void NewInstance_HasNoVacantHouses()
        {
            var vacant = new VacantHouses();

            Assert.That(vacant.HasAny, Is.False);
            Assert.That(vacant.Ids, Is.Empty);
        }

        [Test]
        public void Add_MakesAHouseVacant()
        {
            var vacant = new VacantHouses();

            vacant.Add(5);

            Assert.That(vacant.HasAny, Is.True);
            Assert.That(vacant.Ids, Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void TakeRandom_RemovesTheChosenHouseFromTheVacantSet()
        {
            var vacant = new VacantHouses(new[] { 10, 20, 30 });

            var taken = vacant.TakeRandom(new System.Random(1));

            Assert.That(vacant.Ids, Does.Not.Contain(taken));
            Assert.That(vacant.Ids.Count, Is.EqualTo(2));
        }

        [Test]
        public void TakeRandom_PicksUniformlyAcrossManyTrials()
        {
            // #54: "a vacant house is selected uniformly at random."
            var counts = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };

            for (var seed = 0; seed < 3000; seed++)
            {
                var vacant = new VacantHouses(new[] { 1, 2, 3 });
                var taken = vacant.TakeRandom(new System.Random(seed));
                counts[taken]++;
            }

            foreach (var count in counts.Values)
            {
                Assert.That(count, Is.InRange(800, 1200), "expected roughly uniform selection across 3000 trials");
            }
        }

        [Test]
        public void TakeRandom_ThrowsWhenNoneAreVacant()
        {
            var vacant = new VacantHouses();

            Assert.That(() => vacant.TakeRandom(new System.Random(1)), Throws.InvalidOperationException);
        }
    }
}
