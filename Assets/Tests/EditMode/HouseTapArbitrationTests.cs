using System.Linq;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #670 (absorbing #667): one house tap, one outcome. <c>HouseView</c>'s tap
    /// event used to fan out to two independent subscribers that both fired —
    /// <see cref="QuestDirector"/>'s spray and WorldBootstrap's open-profile — so
    /// tapping a house with a bug swarm on it sprayed the house <em>and</em>
    /// opened its profile panel over the result.
    ///
    /// Derek's call (2026-08-07) is the "whole house" reading: while a house has
    /// bugs, tapping anywhere on it sprays, and the profile is unreachable for
    /// that house until it's clear. That keeps
    /// <c>docs/specs/quests/quest-content.md</c>'s "the house itself is the tap
    /// target… no aiming" rule literally true, and it is why the bug swarm
    /// itself still needs no collider and no tap zone.
    ///
    /// These assert on the profile-open outcome actually firing — the event
    /// WorldBootstrap opens the overlay from — not merely on a tap count.
    /// </summary>
    public class HouseTapArbitrationTests
    {
        private GameObject worldRoot;
        private GameState state;
        private QuestDirector director;
        private int profileOpens;

        [SetUp]
        public void BuildWorldAndWireHouses()
        {
            ModalInputGate.Shared.Clear();
            RoadCrossingGate.Shared.Clear();
            profileOpens = 0;

            state = GameState.CreateNew();
            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);

            var host = new GameObject("quest-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<QuestDirector>();
            director.Init(state, worldRoot.transform);

            // Exactly the production wiring: QuestDirector.WireHouses (run from
            // its Init) supplies the bugs predicate and takes the spray outcome;
            // WorldBootstrap takes the profile outcome.
            foreach (var view in worldRoot.GetComponentsInChildren<HouseView>())
            {
                view.ProfileRequested += () => profileOpens++;
            }
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var presenter in Object.FindObjectsByType<ConversationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }

            Object.DestroyImmediate(worldRoot);
            ModalInputGate.Shared.Clear();
        }

        /// <summary>Gives and accepts a pest-control quest, returning the
        /// bugged house's id.</summary>
        private int GiveBuggedHouseAQuest()
        {
            var dog = state.Dogs[4];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(state.Quests.Accept(quest), Is.True);
            director.OnQuestAccepted(quest);

            var houseId = dog.HouseId;
            Assert.That(state.Quests.IsAwaitingSpray(houseId), Is.True, "the house has bugs on it");
            return houseId;
        }

        private HouseView HouseWithId(int houseId)
            => worldRoot.GetComponentsInChildren<HouseView>().Single(v => v.HouseId == houseId);

        [Test]
        public void WithBugsOnTheHouse_ATapSpraysAndDoesNotOpenTheProfile()
        {
            var houseId = GiveBuggedHouseAQuest();

            HouseWithId(houseId).OnTapped();

            Assert.That(state.Quests.IsAwaitingSpray(houseId), Is.False, "the tap sprayed the house");
            Assert.That(profileOpens, Is.Zero,
                "the same tap must not also open the house profile — one tap, one outcome");
        }

        [Test]
        public void WithBugsOnTheHouse_TheSwarmDespawningMidPressStillDoesNotOpenTheProfile()
        {
            // R2 at the world seam: the swarm is pure feedback with no collider,
            // and it spins and bobs while the finger is down. Whether the cubes
            // are still there at release must not change what the tap does — the
            // house is the tap target, and it is the house's own state that
            // decides.
            var houseId = GiveBuggedHouseAQuest();
            var swarm = Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None)
                .SingleOrDefault(s => s.HouseId == houseId);
            Assert.That(swarm, Is.Not.Null, "a bugged house shows a swarm");

            Object.DestroyImmediate(swarm.gameObject);
            HouseWithId(houseId).OnTapped();

            Assert.That(state.Quests.IsAwaitingSpray(houseId), Is.False, "the tap still sprayed");
            Assert.That(profileOpens, Is.Zero, "and still did not open the profile");
        }

        [Test]
        public void WithNoBugQuest_ATapOpensTheProfileUnchanged()
        {
            // The regression guard: nothing about normal house taps changes.
            var clearHouse = worldRoot.GetComponentsInChildren<HouseView>().First();
            Assert.That(state.Quests.IsAwaitingSpray(clearHouse.HouseId), Is.False);

            clearHouse.OnTapped();

            Assert.That(profileOpens, Is.EqualTo(1), "a clear house opens its profile");
        }

        [Test]
        public void SprayingTheLastBug_RestoresProfileAccess()
        {
            // The profile is unreachable only while the house actually has bugs.
            var houseId = GiveBuggedHouseAQuest();
            var house = HouseWithId(houseId);

            house.OnTapped();
            Assert.That(profileOpens, Is.Zero);

            house.OnTapped();

            Assert.That(profileOpens, Is.EqualTo(1),
                "once the bugs are gone the very next tap opens the profile again");
        }
    }
}
