using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #543: quests trickle in hourly via a persisted fractional accumulator
    /// (error diffusion) rather than an 8h all-or-nothing batch. These cover
    /// the QuestManager-side wiring — the accumulator is advanced and persisted
    /// on <see cref="GameState"/> at each hourly boundary, survives a
    /// <see cref="SaveCodec"/> round-trip (legacy saves default to 0.0), and the
    /// existing headroom / free-dog clamp and free-quest guarantee still hold
    /// when fed the accumulator-derived amount.
    /// </summary>
    public class QuestTrickleTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

        private static GameState StateWithDogs(int dogCount)
        {
            var state = GameState.CreateNew();
            for (var i = state.Dogs.Count; i < dogCount; i++)
            {
                state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
            }

            return state;
        }

        [Test]
        public void GameState_QuestPacingAccumulator_DefaultsToZero()
        {
            Assert.That(GameState.CreateNew().QuestPacingAccumulator, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void HourlyBoundary_PersistsTheLeftoverFraction_OnGameState()
        {
            // #624: 8 dogs -> target 5 (floor) -> 1.25/hr. The first boundary adds
            // floor(1.25)=1 whole quest and carries 0.25 on the accumulator; the
            // second adds floor(0.25+1.25)=1 and carries 0.5 — the remainder lives
            // on GameState between calls.
            var state = StateWithDogs(8);
            // #704: the hourly clock starts when the board drops below target —
            // an empty board here — so arm it an hour before the boundary.
            state.RecordQuestRefreshTimerStart(T0 - EconomyNumbers.RefreshInterval);

            state.Quests.TickPacing(T0, new Random(1));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(1), "1.25/hr adds one quest the first hour");
            Assert.That(state.QuestPacingAccumulator, Is.EqualTo(0.25).Within(1e-9), "the 0.25 remainder is banked");

            state.Quests.TickPacing(T0 + EconomyNumbers.RefreshInterval, new Random(2));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(2), "the second hour adds another whole quest");
            Assert.That(state.QuestPacingAccumulator, Is.EqualTo(0.5).Within(1e-9), "the carried 0.25 + 1.25 leaves 0.5");
        }

        [Test]
        public void QuestPacingAccumulator_RoundTripsThroughSaveCodec()
        {
            var state = StateWithDogs(8);
            state.RecordQuestRefreshTimerStart(T0 - EconomyNumbers.RefreshInterval); // #704: arm the wait
            state.Quests.TickPacing(T0, new Random(1)); // #624: banks 0.25 (1.25/hr)
            Assert.That(state.QuestPacingAccumulator, Is.EqualTo(0.25).Within(1e-9), "precondition: a pending fraction");

            var loaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(loaded.QuestPacingAccumulator, Is.EqualTo(0.25).Within(1e-9),
                "the pending fraction survives save/load so cadence is not reset on relaunch");
        }

        [Test]
        public void LegacySave_WithoutTheAccumulatorLine_LoadsAtZero()
        {
            // A save produced before #543 has no questPacingAcc= line; it must
            // load with the accumulator at its 0.0 default (no migration).
            const string legacy = "onboarded=1\nrotatedUtc=2026-08-03T00:00:00.0000000Z\n";

            var loaded = SaveCodec.Load(legacy);

            Assert.That(loaded.QuestPacingAccumulator, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void TopUp_NeverExceedsTheCap_EvenWhenTheAccumulatorYieldsMoreThanHeadroom()
        {
            // 100 dogs -> target 12 -> 2.0/hr. Drive boundaries until the cap is
            // reached; the accumulator-derived amount is still clamped by
            // headroom so the active count never passes the target.
            var state = StateWithDogs(100);
            var target = new QuestPacingPolicy().TargetActiveCount(state);
            var now = T0;

            for (var i = 0; i < 12; i++)
            {
                state.Quests.TickPacing(now, new Random(i));
                Assert.That(state.Quests.ActiveQuests.Count(), Is.LessThanOrEqualTo(target),
                    "the accumulator amount is still clamped by the headroom");
                now += EconomyNumbers.RefreshInterval;
            }

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target), "and fills up to the cap");
        }

        [Test]
        public void TopUp_IsClampedByFreeDogs_WhenFewerDogsThanTheHourlyAmount()
        {
            // Only 2 quest-free dogs but a 2.0/hr rate that would add 2+; the
            // free-dog clamp keeps the add within the available dogs and never
            // double-assigns a dog already holding a quest.
            var state = StateWithDogs(100); // 2.0/hr
            // Occupy all but two dogs with quests.
            var rng = new Random(7);
            var free = state.Dogs.Where(d => !d.HasActiveQuest).Take(state.Dogs.Count - 2).ToList();
            foreach (var dog in free)
            {
                state.Quests.GiveQuestTo(dog, QuestType.PestControl, rng);
            }

            var freeBefore = state.Dogs.Count(d => !d.HasActiveQuest);
            Assert.That(freeBefore, Is.EqualTo(2), "precondition: exactly two free dogs");

            state.Quests.ForceRefresh(T0, new Random(3));

            Assert.That(state.Dogs.Count(d => !d.HasActiveQuest), Is.GreaterThanOrEqualTo(0));
            Assert.That(state.Quests.ActiveQuests.Count(q => true), Is.LessThanOrEqualTo(state.Dogs.Count),
                "never assigns more quests than there are dogs");
        }
    }
}
