using Doggiehood.Core.Dogs;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    /// <summary>
    /// #165 / docs/specs/ui/dog-profile.md: the dog-only profile view reads
    /// its four fields (name, breed, age, personality) plus the Home-button
    /// target (the dog's house) from the dog's Core data. DogProfile is the
    /// engine-free presentation model those fields come from; the Unity
    /// overlay is thin wiring on top of it.
    /// </summary>
    public class DogProfileTests
    {
        [Test]
        public void For_ReadsNamePersonalityDisplayAndHouse_FromTheDog()
        {
            var dog = new Dog("Bailey", Breed.GoldenRetriever, Personality.AdventurousExploring, 2, false);

            var profile = DogProfile.For(dog);

            Assert.That(profile.Name, Is.EqualTo("Bailey"));
            Assert.That(profile.Personality, Is.EqualTo("Adventurous/Exploring"));
            Assert.That(profile.HouseId, Is.EqualTo(2));
        }

        [Test]
        public void Age_IsPuppyOrAdult_DerivedFromTheIsPuppyFlag()
        {
            // The authored roster marks certain dogs as puppies; numeric ages
            // are not authored anywhere, so the Age tile reads the puppy flag.
            var puppy = new Dog("Nala", Breed.GermanShepherd, Personality.Excited, 1, true);
            var adult = new Dog("Zeus", Breed.GermanShepherd, Personality.Brave, 1, false);

            Assert.That(DogProfile.For(puppy).Age, Is.EqualTo("Puppy"));
            Assert.That(DogProfile.For(adult).Age, Is.EqualTo("Adult"));
        }

        [TestCase(Breed.GermanShepherd, "German Shepherd")]
        [TestCase(Breed.GoldenRetriever, "Golden Retriever")]
        [TestCase(Breed.Labrador, "Labrador")]
        [TestCase(Breed.Beagle, "Beagle")]
        [TestCase(Breed.Chihuahua, "Chihuahua")]
        [TestCase(Breed.FrenchBulldog, "French Bulldog")]
        [TestCase(Breed.Puggle, "Puggle")]
        [TestCase(Breed.Frenchton, "Frenchton")]
        public void Breed_UsesTheRecognizableBreedName(Breed breed, string expected)
        {
            var dog = new Dog("Rex", breed, Personality.Brave, 1, false);

            Assert.That(DogProfile.For(dog).Breed, Is.EqualTo(expected));
        }

        [TestCase(Personality.Brave, "Brave")]
        [TestCase(Personality.AdventurousExploring, "Adventurous/Exploring")]
        [TestCase(Personality.Shy, "Shy")]
        [TestCase(Personality.Excited, "Excited")]
        [TestCase(Personality.Grumpy, "Grumpy")]
        [TestCase(Personality.Athletic, "Athletic")]
        public void Personality_UsesTheDocumentedTraitName(Personality personality, string expected)
        {
            var dog = new Dog("Rex", Breed.Beagle, personality, 1, false);

            Assert.That(DogProfile.For(dog).Personality, Is.EqualTo(expected));
        }
    }
}
