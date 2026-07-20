using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    public class MoveInSystemTests
    {
        [Test]
        public void PityCounter_IncrementsOnFailureAndResetsOnSuccess()
        {
            // #54: base 5%, +5% per completed quest without a move-in, reset
            // to base on success.
            var system = new MoveInSystem();
            var vacant = new VacantHouses(new[] { 1 });
            var activeDogs = Array.Empty<Dog>();

            Assert.That(system.CurrentMoveInChance, Is.EqualTo(MoveInNumbers.BaseMoveInChance).Within(1e-9));

            for (var i = 1; i <= 3; i++)
            {
                var result = system.OnQuestCompleted(vacant, activeDogs, new SequenceRandom(0.99));

                Assert.That(result, Is.Empty, $"quest {i} should not have triggered a move-in");
                Assert.That(system.QuestsSinceLastMoveIn, Is.EqualTo(i));
                Assert.That(system.CurrentMoveInChance,
                    Is.EqualTo(MoveInNumbers.BaseMoveInChance + i * MoveInNumbers.MoveInChanceIncrementPerQuest).Within(1e-9));
            }

            var household = system.OnQuestCompleted(vacant, activeDogs, new SequenceRandom(0.0));

            Assert.That(household, Is.Not.Empty);
            Assert.That(system.QuestsSinceLastMoveIn, Is.EqualTo(0));
            Assert.That(system.CurrentMoveInChance, Is.EqualTo(MoveInNumbers.BaseMoveInChance).Within(1e-9));
        }

        [Test]
        public void PityCounter_DoesNotAdvance_WhenNoVacantHouseExists()
        {
            // #54: "the counter only advances while at least one vacant
            // house exists."
            var system = new MoveInSystem();
            var noVacancy = new VacantHouses();

            for (var i = 0; i < 5; i++)
            {
                var result = system.OnQuestCompleted(noVacancy, Array.Empty<Dog>(), new SequenceRandom(0.0));
                Assert.That(result, Is.Empty);
            }

            Assert.That(system.QuestsSinceLastMoveIn, Is.EqualTo(0));
            Assert.That(system.CurrentMoveInChance, Is.EqualTo(MoveInNumbers.BaseMoveInChance).Within(1e-9));
        }

        [Test]
        public void SuccessfulRoll_OccupiesExactlyOneVacantHouse()
        {
            var system = new MoveInSystem();
            var vacant = new VacantHouses(new[] { 11, 22, 33 });

            var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), new SequenceRandom(0.0));

            Assert.That(household, Is.Not.Empty);
            Assert.That(vacant.Ids.Count, Is.EqualTo(2), "exactly one vacant house should have been taken");
            var occupiedHouseId = household[0].HouseId;
            Assert.That(new[] { 11, 22, 33 }, Does.Contain(occupiedHouseId));
            Assert.That(vacant.Ids, Does.Not.Contain(occupiedHouseId));
            Assert.That(household, Has.All.Property("HouseId").EqualTo(occupiedHouseId));
        }

        [Test]
        public void HouseholdComposition_FollowsThe70_25_5Weights()
        {
            var counts = new Dictionary<string, int> { { "single", 0 }, { "parentPuppy", 0 }, { "threeDog", 0 } };
            const int trials = 4000;

            for (var seed = 0; seed < trials; seed++)
            {
                var system = new MoveInSystem();
                var vacant = new VacantHouses(new[] { 1 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));

                var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), rng);

                if (household.Count == 1)
                {
                    counts["single"]++;
                }
                else if (household.Count == 2 && household.Any(d => d.IsPuppy))
                {
                    counts["parentPuppy"]++;
                }
                else if (household.Count == 3)
                {
                    counts["threeDog"]++;
                }
                else
                {
                    Assert.Fail($"Unexpected household shape: {household.Count} dogs");
                }
            }

            Assert.That(counts["single"] / (double)trials, Is.EqualTo(0.70).Within(0.03));
            Assert.That(counts["parentPuppy"] / (double)trials, Is.EqualTo(0.25).Within(0.03));
            Assert.That(counts["threeDog"] / (double)trials, Is.EqualTo(0.05).Within(0.02));
        }

        [Test]
        public void ParentAndPuppyHousehold_SharesOneBreed()
        {
            var checkedAny = false;

            for (var seed = 0; seed < 2000; seed++)
            {
                var system = new MoveInSystem();
                var vacant = new VacantHouses(new[] { 1 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));

                var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), rng);

                if (household.Count == 2 && household.Any(d => d.IsPuppy))
                {
                    Assert.That(household[0].Breed, Is.EqualTo(household[1].Breed), $"seed {seed}");
                    checkedAny = true;
                }
            }

            Assert.That(checkedAny, Is.True, "no parent+puppy household was ever generated across 2000 trials");
        }

        [Test]
        public void EachDog_GetsAnIndependentlyAssignedPersonality()
        {
            var seenPersonalities = new HashSet<Personality>();

            for (var seed = 0; seed < 600; seed++)
            {
                var system = new MoveInSystem();
                var vacant = new VacantHouses(new[] { 1 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));

                var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), rng);
                foreach (var dog in household)
                {
                    seenPersonalities.Add(dog.Personality);
                }
            }

            Assert.That(seenPersonalities, Is.EquivalentTo(Enum.GetValues(typeof(Personality)).Cast<Personality>()));
        }

        [Test]
        public void MultiDogHouseholds_NeverHaveDuplicateNamesWithinTheHousehold()
        {
            for (var seed = 0; seed < 2000; seed++)
            {
                var system = new MoveInSystem();
                var vacant = new VacantHouses(new[] { 1 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));

                var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), rng);

                Assert.That(household.Select(d => d.Name), Is.Unique, $"seed {seed}");
            }
        }

        [Test]
        public void NonEasterEggNames_AreDrawnFromTheGeneralPoolAndAvoidActiveDogs()
        {
            var system = new MoveInSystem(Array.Empty<string>(), Array.Empty<Breed>());

            for (var seed = 0; seed < 300; seed++)
            {
                var activeDogs = DogRoster.CreateStartingDogs();
                var vacant = new VacantHouses(new[] { 1 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));

                var household = system.OnQuestCompleted(vacant, activeDogs, rng);

                foreach (var dog in household)
                {
                    Assert.That(NamePool.Names, Does.Contain(dog.Name), $"seed {seed}");
                    Assert.That(activeDogs.Select(d => d.Name), Does.Not.Contain(dog.Name), $"seed {seed}");
                }
            }
        }

        [Test]
        public void FirstTwoNonEasterEggHouseholds_IntroduceTheReservedBreedsInRandomOrder()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var system = new MoveInSystem();
                var activeDogs = new List<Dog>();
                var nonEggBreedsInOrder = new List<Breed>();
                var houseId = 1;

                while (nonEggBreedsInOrder.Count < 2 && houseId < 50)
                {
                    var vacant = new VacantHouses(new[] { houseId });
                    var rng = new ForcedFirstSampleRandom(0.0, new Random(seed * 1000 + houseId));
                    var household = system.OnQuestCompleted(vacant, activeDogs, rng);
                    var head = household[0];

                    if (!EasterEggDogs.IsReserved(head.Name))
                    {
                        nonEggBreedsInOrder.Add(head.Breed);
                    }

                    activeDogs.AddRange(household);
                    houseId++;
                }

                Assert.That(nonEggBreedsInOrder[0], Is.EqualTo(Breed.FrenchBulldog).Or.EqualTo(Breed.Puggle), $"seed {seed}");
                var expectedSecond = nonEggBreedsInOrder[0] == Breed.FrenchBulldog ? Breed.Puggle : Breed.FrenchBulldog;
                Assert.That(nonEggBreedsInOrder[1], Is.EqualTo(expectedSecond), $"seed {seed}");
            }
        }

        [Test]
        public void OnceReservedBreedsAreExhausted_HeadBreedsUseCountWeightedSelection()
        {
            var breedsSeen = new HashSet<Breed>();
            var activeDogs = new List<Dog>();

            for (var seed = 0; seed < 300; seed++)
            {
                // No easter eggs, no remaining reserved breeds: purely weighted.
                var system = new MoveInSystem(Array.Empty<string>(), Array.Empty<Breed>());
                var vacant = new VacantHouses(new[] { seed + 1000 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));

                var household = system.OnQuestCompleted(vacant, activeDogs, rng);
                breedsSeen.Add(household[0].Breed);
            }

            Assert.That(breedsSeen.Count, Is.GreaterThan(2),
                "weighted selection should draw from more than just the two reserved breeds");
        }

        [Test]
        public void EasterEggRoll_ReplacesHeadNameAndBreed_WithAFixedEntry()
        {
            var system = new MoveInSystem();
            var vacant = new VacantHouses(new[] { 1 });

            var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), new SequenceRandom(0.0));

            var head = household[0];
            Assert.That(EasterEggDogs.IsReserved(head.Name), Is.True);
            var appearance = EasterEggDogs.Resolve(head.Name);
            Assert.That(head.Breed, Is.EqualTo(appearance.Breed));
            Assert.That(head.Coat, Is.EqualTo(appearance.Coat));
            Assert.That(system.RemainingEasterEggNames, Does.Not.Contain(head.Name));
        }

        [Test]
        public void UsedEasterEggName_NeverReappears_AcrossASaveLoadRoundTrip()
        {
            var system = new MoveInSystem();
            var vacant = new VacantHouses(new[] { 1 });
            var household = system.OnQuestCompleted(vacant, Array.Empty<Dog>(), new SequenceRandom(0.0));
            var usedName = household[0].Name;

            // The remaining-names/remaining-breeds snapshot is the
            // save-friendly boundary (#54): a real save file persists these
            // lists verbatim and reconstructs a MoveInSystem from them.
            var reloaded = new MoveInSystem(system.RemainingEasterEggNames, system.RemainingReservedBreeds);

            for (var seed = 0; seed < 500; seed++)
            {
                var vacant2 = new VacantHouses(new[] { 2 });
                var rng = new ForcedFirstSampleRandom(0.0, new Random(seed));
                var again = reloaded.OnQuestCompleted(vacant2, Array.Empty<Dog>(), rng);

                Assert.That(again[0].Name, Is.Not.EqualTo(usedName), $"seed {seed}");
            }
        }

        [Test]
        public void OnceAllEasterEggNamesAreUsed_TheEggRollNeverTriggersAgain()
        {
            var system = new MoveInSystem(Array.Empty<string>(), new[] { Breed.FrenchBulldog, Breed.Puggle });

            var household = system.OnQuestCompleted(new VacantHouses(new[] { 1 }), Array.Empty<Dog>(), new SequenceRandom(0.0));

            Assert.That(EasterEggDogs.IsReserved(household[0].Name), Is.False);
        }

        [Test]
        public void NewlyMovedInDog_IsImmediatelyEligibleForTheDailyQuestRotation()
        {
            // #54: "New dogs join the daily quest rotation immediately."
            var state = GameState.CreateNew();
            var system = new MoveInSystem();
            var vacant = new VacantHouses(new[] { 999 });

            var household = system.OnQuestCompleted(vacant, state.Dogs, new SequenceRandom(0.0));
            Assert.That(household, Is.Not.Empty);

            foreach (var dog in household)
            {
                state.AddDog(dog);
            }

            var newDog = household[0];
            Assert.That(state.Dogs, Does.Contain(newDog));
            Assert.That(newDog.HasActiveQuest, Is.False, "a freshly moved-in dog starts quest-free");

            var everGotAQuest = false;
            for (var day = 0; day < 40 && !everGotAQuest; day++)
            {
                state.Quests.StartNewDay(new Random(day));
                everGotAQuest = newDog.HasActiveQuest;
            }

            Assert.That(everGotAQuest, Is.True, "new dog was never included in any daily rotation across 40 days");
        }

        [Test]
        public void CompositionWeights_SumToOneHundred()
        {
            // Guards the named constants against a future tuning typo.
            Assert.That(
                MoveInNumbers.SingleWeight + MoveInNumbers.ParentAndPuppyWeight + MoveInNumbers.ThreeDogWeight,
                Is.EqualTo(100));
        }
    }
}
