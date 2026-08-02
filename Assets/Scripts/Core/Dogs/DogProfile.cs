namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Engine-free presentation model for the dog profile view (#165,
    /// docs/specs/ui/dog-profile.md): the four display fields (name, breed,
    /// age, personality) plus the Home-button target house, all read from a
    /// <see cref="Dog"/>'s Core data. The Unity overlay is thin wiring that
    /// renders these strings — no display logic lives in the MonoBehaviour.
    /// </summary>
    public readonly struct DogProfile
    {
        // The Age tile shows the dog's life stage. Numeric ages are not
        // authored anywhere in the roster; the only age-related Core datum is
        // the puppy flag, so the tile reads "Puppy" / "Adult".
        private const string PuppyAge = "Puppy";
        private const string AdultAge = "Adult";

        public string Name { get; }
        public string Breed { get; }
        public string Age { get; }
        public string Personality { get; }
        public int HouseId { get; }

        private DogProfile(string name, string breed, string age, string personality, int houseId)
        {
            Name = name;
            Breed = breed;
            Age = age;
            Personality = personality;
            HouseId = houseId;
        }

        public static DogProfile For(Dog dog)
        {
            return new DogProfile(
                dog.Name,
                BreedName(dog.Breed),
                dog.IsPuppy ? PuppyAge : AdultAge,
                PersonalityName(dog.Personality),
                dog.HouseId);
        }

        /// <summary>The recognizable breed name shown on the breed chip
        /// (docs/specs/dogs/behavior.md personality/breed list).</summary>
        private static string BreedName(Breed breed)
        {
            switch (breed)
            {
                case Dogs.Breed.GermanShepherd:
                    return "German Shepherd";
                case Dogs.Breed.GoldenRetriever:
                    return "Golden Retriever";
                case Dogs.Breed.Labrador:
                    return "Labrador";
                case Dogs.Breed.Beagle:
                    return "Beagle";
                case Dogs.Breed.Chihuahua:
                    return "Chihuahua";
                case Dogs.Breed.FrenchBulldog:
                    return "French Bulldog";
                case Dogs.Breed.Puggle:
                    return "Puggle";
                case Dogs.Breed.Frenchton:
                    return "Frenchton";
                default:
                    return breed.ToString();
            }
        }

        /// <summary>The documented personality trait name
        /// (docs/specs/dogs/behavior.md).</summary>
        private static string PersonalityName(Personality personality)
        {
            switch (personality)
            {
                case Dogs.Personality.Brave:
                    return "Brave";
                case Dogs.Personality.Adventurous:
                    return "Adventurous";
                case Dogs.Personality.Shy:
                    return "Shy";
                case Dogs.Personality.Excited:
                    return "Excited";
                case Dogs.Personality.Grumpy:
                    return "Grumpy";
                case Dogs.Personality.Athletic:
                    return "Athletic";
                default:
                    return personality.ToString();
            }
        }
    }
}
