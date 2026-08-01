using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #437: the move-in pity counter (accumulated move-in chance /
    /// quests-since-last-move-in) and the consumed easter-egg/reserved-breed
    /// reserve survive an app relaunch. Before this, both reset every session
    /// — odds silently fell back to the 5% base and a used easter-egg name
    /// could reappear. Persisted through <see cref="SaveCodec"/> following the
    /// established restore-without-re-firing pattern
    /// (<see cref="GameState.RestoreRewardChainStep"/>).
    /// </summary>
    public class MoveInPersistenceTests
    {
        // A fresh GameState's only houses are the 4 always-occupied starters,
        // so a move-in roll has nowhere to land. Building a vacant lot in an
        // unlocked zone gives the pity counter a real target to advance
        // against, exactly like MoveInReflectionTests does.
        private static GameState StateWithOneVacantHouse()
        {
            var state = Doggiehood.Core.Tests.World.FrontierTestWorld.WithFirstTileUnlocked(100_000);
            var lotId = Doggiehood.Core.Tests.World.FrontierTestWorld.FirstLotId;
            Assert.That(state.TryBuildHouse(lotId), Is.True, "precondition: a vacant lot is built");
            Assert.That(state.Houses.Single(h => h.Id == lotId).IsVacant, Is.True,
                "precondition: the freshly built zone house starts vacant");
            return state;
        }

        [Test]
        public void GameState_ExposesCurrentMoveInState_ForSaving()
        {
            var state = GameState.CreateNew();

            // A fresh game mirrors a default MoveInSystem: 0 quests, the full
            // easter-egg reserve, the full reserved-breed pair.
            Assert.That(state.MoveInQuestsSinceLastMoveIn, Is.EqualTo(0));
            Assert.That(state.MoveInRemainingEasterEggNames,
                Is.EquivalentTo(EasterEggDogs.ReservedNames));
            Assert.That(state.MoveInRemainingReservedBreeds,
                Is.EquivalentTo(new[] { Breed.FrenchBulldog, Breed.Puggle }));
        }

        [Test]
        public void RestoreMoveInState_RebuildsCounterAndReserves_WithoutRollingDiceOrFiringAMoveIn()
        {
            var state = GameState.CreateNew();
            var dogCountBefore = state.Dogs.Count;

            state.RestoreMoveInState(
                4,
                new[] { "Rex", "Stella" },
                new[] { Breed.Puggle });

            Assert.That(state.MoveInQuestsSinceLastMoveIn, Is.EqualTo(4),
                "the persisted pity counter is restored");
            Assert.That(state.MoveInRemainingEasterEggNames,
                Is.EquivalentTo(new[] { "Rex", "Stella" }),
                "the persisted remaining easter-egg reserve is restored");
            Assert.That(state.MoveInRemainingReservedBreeds,
                Is.EquivalentTo(new[] { Breed.Puggle }),
                "the persisted remaining reserved breeds are restored");
            Assert.That(state.Dogs.Count, Is.EqualTo(dogCountBefore),
                "restoring move-in state must not fire a move-in (no dogs added)");
        }

        [Test]
        public void PityCounter_RoundTripsThroughSaveCodec()
        {
            var state = StateWithOneVacantHouse();

            // Drive a few completions that all fail their move-in roll (rng
            // always above CurrentMoveInChance), so the pity counter climbs.
            const int failedRolls = 3;
            for (var i = 0; i < failedRolls; i++)
            {
                var household = state.HandleQuestCompleted(new SequenceRandom(0.99));
                Assert.That(household, Is.Empty, $"roll {i} should fail its move-in");
            }

            Assert.That(state.MoveInQuestsSinceLastMoveIn, Is.EqualTo(failedRolls),
                "precondition: the counter climbed with each failed roll");
            var chanceBefore = MoveInNumbers.BaseMoveInChance
                + failedRolls * MoveInNumbers.MoveInChanceIncrementPerQuest;

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.MoveInQuestsSinceLastMoveIn, Is.EqualTo(failedRolls),
                "the accumulated pity counter survives the save round-trip");
            var reloadedChance = MoveInNumbers.BaseMoveInChance
                + reloaded.MoveInQuestsSinceLastMoveIn * MoveInNumbers.MoveInChanceIncrementPerQuest;
            Assert.That(reloadedChance, Is.EqualTo(chanceBefore).Within(1e-9),
                "the accumulated move-in chance is preserved");
        }

        [Test]
        public void ConsumedEasterEggAndReservedBreed_StayConsumed_AcrossASaveLoadRoundTrip()
        {
            var state = StateWithOneVacantHouse();

            // Force a successful move-in whose head is an easter-egg dog:
            // SequenceRandom(0.0) rolls below every threshold, so the move-in
            // fires and the easter-egg branch (and a reserved breed) is taken.
            var household = state.HandleQuestCompleted(new SequenceRandom(0.0));
            Assert.That(household, Is.Not.Empty, "precondition: a move-in fired");
            var consumedEggName = household[0].Name;
            Assert.That(EasterEggDogs.IsReserved(consumedEggName), Is.True,
                "precondition: the head is an easter-egg dog");
            Assert.That(state.MoveInRemainingEasterEggNames, Does.Not.Contain(consumedEggName),
                "precondition: the used egg is gone from the live reserve");
            var remainingBreedsBefore = state.MoveInRemainingReservedBreeds.ToList();

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.MoveInRemainingEasterEggNames, Does.Not.Contain(consumedEggName),
                "a consumed easter-egg name stays consumed after reload");
            Assert.That(reloaded.MoveInRemainingReservedBreeds,
                Is.EquivalentTo(remainingBreedsBefore),
                "the consumed reserved breed stays consumed after reload");
        }

        [Test]
        public void FreshGame_MoveInState_RoundTripsAsDefault()
        {
            var reloaded = SaveCodec.Load(SaveCodec.Save(GameState.CreateNew()));

            Assert.That(reloaded.MoveInQuestsSinceLastMoveIn, Is.EqualTo(0));
            Assert.That(reloaded.MoveInRemainingEasterEggNames,
                Is.EquivalentTo(EasterEggDogs.ReservedNames));
            Assert.That(reloaded.MoveInRemainingReservedBreeds,
                Is.EquivalentTo(new[] { Breed.FrenchBulldog, Breed.Puggle }));
        }

        [Test]
        public void LegacySave_WithNoMoveInLine_LoadsAsFreshMoveInState()
        {
            // A pre-#437 save carries no moveIn field — it must load as a fresh
            // move-in system (base chance, 0 quests, full reserves), never throw.
            var reloaded = SaveCodec.Load("version=1\ncoins=250\nonboarded=1\n");

            Assert.That(reloaded.MoveInQuestsSinceLastMoveIn, Is.EqualTo(0));
            Assert.That(reloaded.MoveInRemainingEasterEggNames,
                Is.EquivalentTo(EasterEggDogs.ReservedNames));
            Assert.That(reloaded.MoveInRemainingReservedBreeds,
                Is.EquivalentTo(new[] { Breed.FrenchBulldog, Breed.Puggle }));
        }
    }
}
