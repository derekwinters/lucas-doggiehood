using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #436: a live move-in must be reflected on screen the instant Core reports
    /// it. <see cref="QuestDirector"/> subscribes to
    /// <see cref="QuestManager.MoveInOccurred"/> and (a) spawns a
    /// <see cref="DogView"/> for each new resident via the shared
    /// <see cref="DogSpawner.SpawnDog"/> — bound to the filled house, tappable
    /// and wandering — without touching or duplicating any existing DogView, and
    /// (b) drops the filled house's vacancy greyscale (#58) by rebuilding it
    /// through <see cref="HouseUpgradeDirector.RefreshHouse"/>, which preserves
    /// the house's rolled variant/tint (the #407 destroy-and-rebuild).
    /// </summary>
    public class MoveInReflectionTests
    {
        // The move-in pity counter (MoveInNumbers) starts at a 5% base and rises
        // 5% per completion without a move-in, so it reaches 100% by this many
        // completions — a vacant house is then guaranteed filled. The loop is
        // deterministic in that it always terminates with a move-in; only WHICH
        // completion triggers it (and the household drawn) is random.
        private static readonly int MaxCompletionsToGuaranteeMoveIn =
            (int)System.Math.Ceiling(1.0 / MoveInNumbers.MoveInChanceIncrementPerQuest) + 1;

        private const int StartingDogCount = 8;

        private GameState state;
        private GameObject worldRoot;
        private int vacantHouseId;
        private HouseVariant vacantHouseVariant;
        private QuestDirector questDirector;
        private HouseUpgradeDirector upgradeDirector;

        [SetUp]
        public void SetUp()
        {
            WorldBuilder.ForcePrimitiveFallback = false;

            // A game with exactly one vacant house — a freshly built (never
            // occupied) zone lot — so a completed quest's move-in roll has
            // somewhere to land, unlike the always-occupied starting houses.
            state = GameState.CreateNew();
            state.Wallet.Deposit(100_000);
            Assert.That(state.TryUnlockNextZone(), Is.True);
            vacantHouseId = state.UnlockedZones[0].Lots[0].HouseId;
            Assert.That(state.TryBuildHouse(vacantHouseId), Is.True);
            Assert.That(state.Houses.Single(h => h.Id == vacantHouseId).IsVacant, Is.True,
                "the freshly built zone house starts vacant");
            vacantHouseVariant = HouseVariantAssignment.ForHouse(vacantHouseId);

            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);

            // Mirror WorldBootstrap's wiring order (#436): the upgrade
            // re-renderer exists first so the QuestDirector can reuse it to drop
            // the vacancy tint on a move-in.
            upgradeDirector = new GameObject("upgrade-director").AddComponent<HouseUpgradeDirector>();
            upgradeDirector.transform.SetParent(worldRoot.transform);
            upgradeDirector.Init(state, worldRoot.transform, _ => { });

            questDirector = new GameObject("quest-director").AddComponent<QuestDirector>();
            questDirector.transform.SetParent(worldRoot.transform);
            questDirector.Init(state, worldRoot.transform, upgradeDirector);
        }

        [TearDown]
        public void Cleanup()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            foreach (var presenter in Object.FindObjectsByType<ConversationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }

            if (worldRoot != null)
            {
                Object.DestroyImmediate(worldRoot);
            }
        }

        /// <summary>Completes quests through the live QuestManager — firing
        /// MoveInOccurred into the wired QuestDirector — until the one vacant
        /// house fills. Returns the newly moved-in household.</summary>
        private IReadOnlyList<Dog> TriggerMoveIn()
        {
            var dog = state.Dogs.First(d => d.HouseId != vacantHouseId && !d.HasActiveQuest);
            for (var i = 0; i < MaxCompletionsToGuaranteeMoveIn; i++)
            {
                var before = state.Dogs.Count;
                var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(i));
                Assert.That(state.Quests.Accept(quest), Is.True);
                Assert.That(state.Quests.SprayHouse(quest.TargetHouseId.Value), Is.True);
                if (state.Dogs.Count > before)
                {
                    return state.Dogs.Skip(before).ToList();
                }
            }

            Assert.Fail("no move-in occurred within the guaranteed completion bound");
            return null;
        }

        private HouseView HouseViewFor(int houseId)
        {
            return Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .SingleOrDefault(h => h.HouseId == houseId);
        }

        [Test]
        public void OnMoveIn_SpawnsATappableDogViewForEachNewResident_BoundToTheFilledHouse_NoDuplicates()
        {
            var viewsBefore = Object.FindObjectsByType<DogView>(FindObjectsSortMode.None);
            Assert.That(viewsBefore.Length, Is.EqualTo(StartingDogCount),
                "the starting dogs are shown before any move-in");
            var namesBefore = viewsBefore.Select(v => v.Dog.Name).ToList();

            var household = TriggerMoveIn();
            Assert.That(household, Is.Not.Empty);

            var viewsAfter = Object.FindObjectsByType<DogView>(FindObjectsSortMode.None);
            Assert.That(viewsAfter.Length, Is.EqualTo(viewsBefore.Length + household.Count),
                "exactly one new DogView per moved-in dog — existing views untouched, none duplicated");
            Assert.That(viewsAfter.Select(v => v.Dog.Name).Distinct().Count(), Is.EqualTo(viewsAfter.Length),
                "no dog is shown by two DogViews");

            foreach (var name in namesBefore)
            {
                Assert.That(viewsAfter.Any(v => v.Dog.Name == name), Is.True,
                    $"{name}'s pre-existing DogView must survive the move-in");
            }

            foreach (var dog in household)
            {
                var view = viewsAfter.SingleOrDefault(v => v.Dog.Name == dog.Name);
                Assert.That(view, Is.Not.Null, $"{dog.Name} moved in but got no DogView");
                Assert.That(view.Dog.HouseId, Is.EqualTo(vacantHouseId),
                    "the new dog's view is bound to the filled house");
                Assert.That(view.GetComponentsInChildren<Collider>(), Is.Not.Empty,
                    "the new dog is tappable (has a tap collider), like every spawned dog");
            }
        }

        [Test]
        public void SpawnDog_ProducesTheSameWiringAsTheBulkSpawnDogsLoop()
        {
            // Refactor guard (#436): the extracted single-dog entry point must
            // place and wire a dog identically to the bulk build-time loop, so
            // the live move-in path and build path never diverge.
            var bulkState = GameState.CreateNew();
            var bulkRoot = WorldBuilder.Build(bulkState);
            DogSpawner.SpawnDogs(bulkState, bulkRoot.transform);
            var bulkDog = bulkState.Dogs[0]; // first dog => index 0 at its house
            var bulkView = bulkRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == bulkDog.Name);
            var bulkPosition = bulkView.transform.position;
            var bulkHasCollider = bulkView.GetComponentsInChildren<Collider>().Any();

            var singleState = GameState.CreateNew();
            var singleRoot = WorldBuilder.Build(singleState);
            var singleDog = singleState.Dogs[0];

            try
            {
                DogSpawner.SpawnDog(singleState, singleRoot.transform, singleDog, 0);
                var singleView = singleRoot.GetComponentsInChildren<DogView>()
                    .Single(v => v.Dog.Name == singleDog.Name);

                Assert.That(singleView.transform.position, Is.EqualTo(bulkPosition),
                    "SpawnDog anchors/staggers the dog to the same spot as the bulk loop");
                Assert.That(singleView.transform.parent, Is.EqualTo(singleRoot.transform),
                    "SpawnDog parents the dog under the world root, like the bulk loop");
                Assert.That(singleView.Dog.HouseId, Is.EqualTo(singleDog.HouseId),
                    "SpawnDog binds the dog to its own house");
                Assert.That(singleView.GetComponentsInChildren<Collider>().Any(), Is.EqualTo(bulkHasCollider),
                    "SpawnDog gives the dog the same tap-collider wiring as the bulk loop");
            }
            finally
            {
                Object.DestroyImmediate(bulkRoot);
                Object.DestroyImmediate(singleRoot);
            }
        }

        [Test]
        public void OnMoveIn_RefreshesTheFilledHouse_SoItNoLongerCarriesTheVacancyGreyscale()
        {
            var vacantColor = CoreColors.FromHex(Palette.VacantHouseTintHex);

            var whileVacant = HouseViewFor(vacantHouseId).transform.Find("Model");
            Assert.That(whileVacant, Is.Not.Null, "the vacant house renders a model");
            Assert.That(whileVacant.GetComponentsInChildren<Renderer>()
                .All(r => ColorApproximately(r.sharedMaterial.color, vacantColor)), Is.True,
                "sanity: the house shows the vacancy greyscale while vacant");

            TriggerMoveIn();

            var rebuilt = HouseViewFor(vacantHouseId);
            Assert.That(rebuilt, Is.Not.Null, "exactly one rebuilt HouseView remains for the filled house");
            var model = rebuilt.transform.Find("Model");
            Assert.That(model, Is.Not.Null);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                Assert.That(ColorApproximately(renderer.sharedMaterial.color, vacantColor), Is.False,
                    $"{renderer.name} must drop the vacancy greyscale once the house is occupied");
            }
        }

        [Test]
        public void OnMoveIn_TheRebuiltHouseKeepsItsRolledVariantTint()
        {
            var expectedTint = CoreColors.FromHex(Palette.HouseTintHex(vacantHouseVariant.TintIndex));

            TriggerMoveIn();

            var rebuilt = HouseViewFor(vacantHouseId);
            var model = rebuilt.transform.Find("Model");
            Assert.That(model, Is.Not.Null);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.sharedMaterial.color;
                Assert.That(color.r, Is.EqualTo(expectedTint.r).Within(0.01f),
                    $"{renderer.name} R keeps the rolled variant tint after move-in");
                Assert.That(color.g, Is.EqualTo(expectedTint.g).Within(0.01f),
                    $"{renderer.name} G keeps the rolled variant tint after move-in");
                Assert.That(color.b, Is.EqualTo(expectedTint.b).Within(0.01f),
                    $"{renderer.name} B keeps the rolled variant tint after move-in");
            }
        }

        private static bool ColorApproximately(Color a, Color b)
        {
            const float Tolerance = 0.01f;
            return Mathf.Abs(a.r - b.r) < Tolerance
                && Mathf.Abs(a.g - b.g) < Tolerance
                && Mathf.Abs(a.b - b.b) < Tolerance;
        }
    }
}
