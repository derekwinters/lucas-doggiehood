using System.Collections.Generic;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// The starting cast (#63, docs/specs/dogs/roster-names.md): 8 dogs
    /// across the 4 houses — parent+puppy, parent+puppy, single, three.
    /// </summary>
    public static class DogRoster
    {
        public static IReadOnlyList<Dog> CreateStartingDogs()
        {
            return new[]
            {
                new Dog("Zeus", Breed.GermanShepherd, Personality.Brave, 1, false),
                new Dog("Nala", Breed.GermanShepherd, Personality.Excited, 1, true),
                new Dog("Bailey", Breed.GoldenRetriever, Personality.AdventurousExploring, 2, false),
                new Dog("Sunny", Breed.GoldenRetriever, Personality.Excited, 2, true),
                new Dog("Pepper", Breed.Chihuahua, Personality.Grumpy, 3, false),
                new Dog("Duke", Breed.Labrador, Personality.Brave, 4, false),
                new Dog("Scout", Breed.Beagle, Personality.AdventurousExploring, 4, false),
                new Dog("Waffles", Breed.Frenchton, Personality.Shy, 4, false),
            };
        }
    }
}
