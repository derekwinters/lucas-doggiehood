using System;
using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #704: now that quests survive a relaunch, the scene has to show the ones
    /// that were already accepted when the app closed. Bug swarms and yard
    /// decorations already re-derive from Core at Init; a hidden lost item did
    /// not — it was only ever spawned at the moment of acceptance, so a
    /// reloaded find-it quest had nothing in the world to find.
    /// </summary>
    public class RestoredQuestViewTests
    {
        private GameObject worldRoot;
        private GameState state;
        private QuestDirector director;

        [SetUp]
        public void BuildWorld()
        {
            RoadCrossingGate.Shared.Clear();
            state = GameState.CreateNew();
            state.MarkOnboardingComplete();
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldRoot != null)
            {
                Object.DestroyImmediate(worldRoot);
            }
        }

        /// <summary>Builds the scene the way WorldBootstrap does, from whatever
        /// <see cref="state"/> currently holds — i.e. "relaunch into this
        /// save".</summary>
        private void Relaunch()
        {
            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);
            var host = new GameObject("quest-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<QuestDirector>();
            director.Init(state, worldRoot.transform);
        }

        [Test]
        public void AnAcceptedLostItemQuest_HasItsHiddenItemInTheWorldAfterARelaunch()
        {
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(1));
            Assert.That(state.Quests.Accept(quest), Is.True, "precondition: the search is underway");
            state = SaveCodec.Load(SaveCodec.Save(state));

            Relaunch();

            var restored = state.Quests.ActiveQuests.Single();
            var items = Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None);
            Assert.That(items.Any(view => view.Quest == restored), Is.True,
                "the item the player is hunting for is back in the world");
        }

        [Test]
        public void AnUnacceptedLostItemQuest_PutsNothingInTheWorld()
        {
            // The item appears when the search starts, not when the quest is
            // merely offered — a relaunch must not leak the hiding place.
            state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(2));
            state = SaveCodec.Load(SaveCodec.Save(state));

            Relaunch();

            Assert.That(Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void RefreshingLostItems_IsIdempotent()
        {
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(3));
            state.Quests.Accept(quest);
            Relaunch();
            var spawned = Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None).Length;

            director.RefreshLostItems();

            Assert.That(Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None).Length,
                Is.EqualTo(spawned), "re-syncing never doubles an item already in the world");
        }

        [Test]
        public void AnAcceptedPestControlQuest_HasItsBugsBackAfterARelaunch()
        {
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.PestControl, new Random(4));
            state.Quests.Accept(quest);
            state = SaveCodec.Load(SaveCodec.Save(state));

            Relaunch();

            var swarms = Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None);
            Assert.That(swarms.Any(s => s.HouseId == quest.TargetHouseId.Value), Is.True,
                "the bugged house still shows its swarm");
        }
    }
}
