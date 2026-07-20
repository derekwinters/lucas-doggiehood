using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// New houses start empty; dogs move in gradually over time (#54,
    /// docs/specs/expansion.md#move-in-system). Pure Core logic: operates on
    /// an abstract VacantHouses set plus the current dog roster, with no
    /// dependency on the #109/#56 tile-grid/zone geometry.
    ///
    /// Trigger: a shared, neighborhood-wide pity counter rolled once per
    /// completed quest (base 5%, +5% per quest without a move-in, reset to
    /// base on success). On success, one vacant house is chosen uniformly
    /// at random and a new household moves in:
    /// - Household shape is 70% single / 25% parent+puppy / 5% three-dog.
    ///   A parent+puppy pair shares one breed; a three-dog household gives
    ///   each dog its own independently-rolled breed (matching the starting
    ///   three-dog house's precedent of three different breeds).
    /// - The household head has a 5% chance of being an easter-egg dog
    ///   (fixed name+breed+coat, breed roll skipped entirely); once used, an
    ///   easter-egg name is permanently removed from the reserve.
    /// - Otherwise the head's breed is the reserved French Bulldog/Puggle
    ///   pair (first two such heads, in random order) and afterwards
    ///   count-weighted (BreedWeighting) by the neighborhood's current mix.
    /// - Every dog gets an independently-rolled personality and a name from
    ///   the general pool with no duplicates among currently-active dogs.
    ///
    /// New dogs join the daily quest rotation immediately: they're ordinary
    /// Dog instances with no active quest, so QuestManager.StartNewDay picks
    /// them up like any other quest-free dog once added to the live roster
    /// (GameState.AddDog).
    /// </summary>
    public sealed class MoveInSystem
    {
        private static readonly IReadOnlyList<Breed> DefaultReservedBreeds = new[] { Breed.FrenchBulldog, Breed.Puggle };

        private readonly List<string> remainingEasterEggNames;
        private readonly List<Breed> remainingReservedBreeds;

        public MoveInSystem()
            : this(EasterEggDogs.ReservedNames, DefaultReservedBreeds)
        {
        }

        /// <summary>Reconstructs a system from persisted state (#54: "used
        /// entries never reappear, including across save/load") — the
        /// remaining-names/remaining-breeds lists are the save-friendly
        /// boundary a real save file would store verbatim.</summary>
        public MoveInSystem(IEnumerable<string> remainingEasterEggNames, IEnumerable<Breed> remainingReservedBreeds)
        {
            this.remainingEasterEggNames = new List<string>(remainingEasterEggNames);
            this.remainingReservedBreeds = new List<Breed>(remainingReservedBreeds);
        }

        public int QuestsSinceLastMoveIn { get; private set; }

        public IReadOnlyList<string> RemainingEasterEggNames
        {
            get { return remainingEasterEggNames; }
        }

        public IReadOnlyList<Breed> RemainingReservedBreeds
        {
            get { return remainingReservedBreeds; }
        }

        public double CurrentMoveInChance
        {
            get { return MoveInNumbers.BaseMoveInChance + QuestsSinceLastMoveIn * MoveInNumbers.MoveInChanceIncrementPerQuest; }
        }

        /// <summary>Call once per completed quest. Returns the newly
        /// moved-in household (empty if no move-in happened this time).</summary>
        public IReadOnlyList<Dog> OnQuestCompleted(VacantHouses vacantHouses, IReadOnlyList<Dog> activeDogs, Random rng)
        {
            if (vacantHouses == null)
            {
                throw new ArgumentNullException(nameof(vacantHouses));
            }

            if (!vacantHouses.HasAny)
            {
                return Array.Empty<Dog>();
            }

            if (rng.NextDouble() >= CurrentMoveInChance)
            {
                QuestsSinceLastMoveIn++;
                return Array.Empty<Dog>();
            }

            QuestsSinceLastMoveIn = 0;
            var houseId = vacantHouses.TakeRandom(rng);
            return CreateHousehold(houseId, activeDogs, rng);
        }

        private List<Dog> CreateHousehold(int houseId, IReadOnlyList<Dog> activeDogs, Random rng)
        {
            var namesInUse = new HashSet<string>(activeDogs.Select(d => d.Name));
            var composition = PickComposition(rng);

            var head = CreateHeadDog(houseId, activeDogs, namesInUse, rng);
            namesInUse.Add(head.Name);
            var household = new List<Dog> { head };

            switch (composition)
            {
                case HouseholdComposition.ParentAndPuppy:
                    var puppy = CreateFollowerDog(houseId, head.Breed, isPuppy: true, namesInUse, rng);
                    namesInUse.Add(puppy.Name);
                    household.Add(puppy);
                    break;

                case HouseholdComposition.ThreeDog:
                    for (var i = 0; i < 2; i++)
                    {
                        var combinedActive = activeDogs.Concat(household).ToList();
                        var breed = BreedWeighting.PickWeighted(combinedActive, rng);
                        var extra = CreateFollowerDog(houseId, breed, isPuppy: false, namesInUse, rng);
                        namesInUse.Add(extra.Name);
                        household.Add(extra);
                    }

                    break;
            }

            return household;
        }

        private Dog CreateHeadDog(int houseId, IReadOnlyList<Dog> activeDogs, ISet<string> namesInUse, Random rng)
        {
            if (remainingEasterEggNames.Count > 0 && rng.NextDouble() < MoveInNumbers.EasterEggChance)
            {
                var eggIndex = rng.Next(remainingEasterEggNames.Count);
                var eggName = remainingEasterEggNames[eggIndex];
                remainingEasterEggNames.RemoveAt(eggIndex);

                var appearance = EasterEggDogs.Resolve(eggName);
                var personality = PickPersonality(rng);
                return new Dog(eggName, appearance.Breed, personality, houseId, isPuppy: false, coat: appearance.Coat);
            }

            var breed = PickHeadBreed(activeDogs, rng);
            var name = NamePool.PickName(rng, namesInUse);
            var headPersonality = PickPersonality(rng);
            return new Dog(name, breed, headPersonality, houseId, isPuppy: false);
        }

        private Breed PickHeadBreed(IReadOnlyList<Dog> activeDogs, Random rng)
        {
            if (remainingReservedBreeds.Count > 0)
            {
                var index = rng.Next(remainingReservedBreeds.Count);
                var breed = remainingReservedBreeds[index];
                remainingReservedBreeds.RemoveAt(index);
                return breed;
            }

            return BreedWeighting.PickWeighted(activeDogs, rng);
        }

        private static Dog CreateFollowerDog(int houseId, Breed breed, bool isPuppy, ISet<string> namesInUse, Random rng)
        {
            var name = NamePool.PickName(rng, namesInUse);
            var personality = PickPersonality(rng);
            return new Dog(name, breed, personality, houseId, isPuppy);
        }

        private static HouseholdComposition PickComposition(Random rng)
        {
            var totalWeight = MoveInNumbers.SingleWeight + MoveInNumbers.ParentAndPuppyWeight + MoveInNumbers.ThreeDogWeight;
            var roll = rng.Next(totalWeight);

            if (roll < MoveInNumbers.SingleWeight)
            {
                return HouseholdComposition.Single;
            }

            if (roll < MoveInNumbers.SingleWeight + MoveInNumbers.ParentAndPuppyWeight)
            {
                return HouseholdComposition.ParentAndPuppy;
            }

            return HouseholdComposition.ThreeDog;
        }

        private static Personality PickPersonality(Random rng)
        {
            var values = (Personality[])Enum.GetValues(typeof(Personality));
            return values[rng.Next(values.Length)];
        }
    }
}
