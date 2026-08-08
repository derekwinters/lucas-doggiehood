using Doggiehood.Core.Interaction;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Interaction
{
    /// <summary>
    /// #670 (absorbing #667): a house tap resolves to exactly ONE outcome.
    /// Before this, <c>HouseView.Tapped</c> fanned out to two independent
    /// subscribers that both fired on every tap — QuestDirector's spray and
    /// WorldBootstrap's open-profile — with nothing arbitrating between them,
    /// so tapping a house with bugs on it sprayed the house <em>and</em> opened
    /// its profile panel over the top. Derek's call (2026-08-07) is "whole
    /// house": while a house has bugs, tapping anywhere on it sprays, and its
    /// profile is unreachable until it's clear. That keeps
    /// <c>docs/specs/quests/quest-content.md</c>'s "the house itself is the tap
    /// target… no aiming" rule true as written.
    /// </summary>
    public class HouseTapArbiterTests
    {
        [Test]
        public void AHouseWithAPendingSpray_Sprays()
        {
            Assert.That(HouseTapArbiter.Resolve(hasPendingSpray: true),
                Is.EqualTo(HouseTapOutcome.Spray));
        }

        [Test]
        public void AHouseWithoutAPendingSpray_OpensItsProfile()
        {
            Assert.That(HouseTapArbiter.Resolve(hasPendingSpray: false),
                Is.EqualTo(HouseTapOutcome.OpenProfile));
        }

        [Test]
        public void TheOutcomeIsSingleValued_SoOneTapCanNeverDoBoth()
        {
            // The whole point of the arbiter is that "spray" and "open profile"
            // are mutually exclusive by construction, not by two handlers
            // politely agreeing. A single-valued result makes a fan-out
            // impossible to express.
            foreach (var hasPendingSpray in new[] { true, false })
            {
                var outcome = HouseTapArbiter.Resolve(hasPendingSpray);
                Assert.That(outcome == HouseTapOutcome.Spray || outcome == HouseTapOutcome.OpenProfile);
            }
        }

        [Test]
        public void IsAwaitingSpray_TracksTheHousesPestQuestLifecycle()
        {
            // The predicate the Unity layer feeds the arbiter, so "does this
            // house have bugs right now?" is a Core question, not a scene scan.
            var state = GameState.CreateNew();
            var buggedDog = state.Dogs[4]; // Pepper, house 3
            var pest = state.Quests.GiveQuestTo(buggedDog, QuestType.PestControl, new System.Random(5));

            Assert.That(state.Quests.IsAwaitingSpray(buggedDog.HouseId), Is.False,
                "a given-but-unaccepted pest quest has not put bugs on the house yet");

            state.Quests.Accept(pest);
            Assert.That(state.Quests.IsAwaitingSpray(buggedDog.HouseId), Is.True);
            Assert.That(state.Quests.IsAwaitingSpray(buggedDog.HouseId + 1), Is.False,
                "a neighbouring house is unaffected");

            state.Quests.SprayHouse(buggedDog.HouseId);
            Assert.That(state.Quests.IsAwaitingSpray(buggedDog.HouseId), Is.False,
                "spraying the last bug restores profile access");
        }

        [Test]
        public void TheArbiterFollowsLiveQuestState_SprayThenProfile()
        {
            // End to end over the real quest state: the same house answers
            // "spray" while bugged and "profile" once clear, from one predicate.
            var state = GameState.CreateNew();
            var buggedDog = state.Dogs[4];
            var pest = state.Quests.GiveQuestTo(buggedDog, QuestType.PestControl, new System.Random(5));
            state.Quests.Accept(pest);

            Assert.That(HouseTapArbiter.Resolve(state.Quests.IsAwaitingSpray(buggedDog.HouseId)),
                Is.EqualTo(HouseTapOutcome.Spray));

            state.Quests.SprayHouse(buggedDog.HouseId);

            Assert.That(HouseTapArbiter.Resolve(state.Quests.IsAwaitingSpray(buggedDog.HouseId)),
                Is.EqualTo(HouseTapOutcome.OpenProfile));
        }
    }
}
