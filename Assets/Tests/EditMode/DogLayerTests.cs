using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class DogLayerTests
    {
        private GameObject worldRoot;
        private GameState state;

        [SetUp]
        public void BuildWorldWithDogs()
        {
            state = GameState.CreateNew();
            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var presenter in Object.FindObjectsByType<ConversationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }

            Object.DestroyImmediate(worldRoot);
        }

        [Test]
        public void SpawnsAllEightRosterDogs_OnSidewalks_NeverOnARoad()
        {
            // #8/#63/#106: every roster dog is in the world, standing on a
            // sidewalk (outside both roads' pavement + grass verge band),
            // never on the road itself.
            var views = worldRoot.GetComponentsInChildren<DogView>();

            Assert.That(views.Length, Is.EqualTo(8));
            Assert.That(views.Select(v => v.Dog.Name),
                Is.EquivalentTo(new[] { "Zeus", "Nala", "Bailey", "Sunny", "Pepper", "Duke", "Scout", "Waffles" }));

            var roadAndVergeHalfWidth = NeighborhoodLayout.StreetWidth / 2f + WorldDimensions.GrassVergeWidth;
            foreach (var view in views)
            {
                var p = view.transform.position;
                Assert.That(Mathf.Abs(p.x) > roadAndVergeHalfWidth && Mathf.Abs(p.z) > roadAndVergeHalfWidth, Is.True,
                    $"{view.Dog.Name} spawned on the road or its verge at {p}");
            }
        }

        [Test]
        public void SpeechBubble_ShowsExactlyWhenTheDogHasAnActiveQuest()
        {
            // #10: bubble bound to HasActiveQuest.
            var view = worldRoot.GetComponentsInChildren<DogView>()[0];
            var bubble = view.transform.Find(DogView.BubbleName).gameObject;

            Assert.That(bubble.activeSelf, Is.False, "no quest -> no bubble");

            view.Dog.GiveQuest();
            view.RefreshBubble();
            Assert.That(bubble.activeSelf, Is.True, "quest -> bubble");

            view.Dog.ClearQuest();
            view.RefreshBubble();
            Assert.That(bubble.activeSelf, Is.False, "resolved -> bubble gone");
        }

        [Test]
        public void TappingADogWithAQuest_OpensItsConversation()
        {
            // #11: tap -> conversation for that dog's quest.
            var presenterHost = new GameObject("presenter", typeof(ConversationPresenter));
            var presenter = presenterHost.GetComponent<ConversationPresenter>();
            var view = worldRoot.GetComponentsInChildren<DogView>()
                .Single(v => v.Dog.Name == "Pepper");

            view.OnTapped();
            Assert.That(presenter.IsOpen, Is.False, "no quest -> tap is a no-op");

            view.Dog.GiveQuest();
            view.OnTapped();

            Assert.That(presenter.IsOpen, Is.True);
            Assert.That(presenter.Current.Lines.Any(l => l.Contains("Pepper")), Is.True,
                "conversation is scoped to the tapped dog");
        }

        [Test]
        public void WindowDog_RendersAtItsHouseWindowAnchor()
        {
            // #9: InsideAtWindow renders at the window anchor, WindowWatch pose.
            var dog = new Dog("Windowy", Breed.Beagle, Personality.Shy, 2, false);
            dog.PlaceInsideAtWindow();

            var house = Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .Single(h => h.HouseId == 2);
            var go = new GameObject("window-dog");
            go.transform.SetParent(worldRoot.transform);

            var view = go.AddComponent<DogView>();
            view.Init(dog, house.WindowAnchor);

            Assert.That(dog.State, Is.EqualTo(DogState.WindowWatch));
            Assert.That(Vector3.Distance(go.transform.position, house.WindowAnchor.position), Is.LessThan(0.001f));
        }

        [Test]
        public void EachPoseState_ProducesADistinctPose()
        {
            // #66: the four states map to distinguishable poses on the rig.
            var idleDog = new Dog("Posey", Breed.Beagle, Personality.Brave, 1, false);
            var go = new GameObject("pose-dog");
            go.transform.SetParent(worldRoot.transform);
            var view = go.AddComponent<DogView>();
            view.Init(idleDog, null);
            var body = go.transform.Find("Body");

            var idle = body.localRotation;

            idleDog.TryRest(comfortDecorationSelected: true);
            view.ApplyPose(null);
            var rest = body.localRotation;

            idleDog.PlaceOnStreet();
            idleDog.TrySit(buyQuestAccepted: true, isAtHome: true);
            view.ApplyPose(null);
            var sit = body.localRotation;

            Assert.That(Quaternion.Angle(idle, rest), Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(idle, sit), Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(rest, sit), Is.GreaterThan(1f));
            // WindowWatch is distinguished by rendering at the window anchor
            // (covered above) rather than by body rotation.
        }

        [Test]
        public void CubePetsAssetPresent_BodyUsesImportedModelInsteadOfPrimitives()
        {
            // #119: the shared Kenney Cube Pets placeholder (CC0) replaces
            // the graybox capsule+sphere rig for every roster dog once the
            // model is importable via Resources.Load. This is a pure
            // Unity-layer visual swap — Core's Breed data model is
            // untouched, and every breed still renders the same shared
            // model until breed-distinct modeling (#35) lands.
            var dog = new Dog("Modely", Breed.Beagle, Personality.Brave, 1, false);
            var go = new GameObject("model-dog");
            go.transform.SetParent(worldRoot.transform);
            var view = go.AddComponent<DogView>();
            view.Init(dog, null);

            var body = go.transform.Find("Body");
            Assert.That(body, Is.Not.Null, "DogView must still expose a child named 'Body'");

            var head = go.transform.Find("Head");
            Assert.That(head, Is.Null,
                "Cube Pets model already includes a head; no separate Head sibling should be created");

            var meshFilter = body.GetComponentInChildren<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null,
                "Body should be (or contain) the imported Cube Pets mesh, not a primitive capsule");
            Assert.That(meshFilter.sharedMesh, Is.Not.Null);

            var renderer = body.GetComponentInChildren<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.color, Is.EqualTo(BreedCoats.ForDog(dog)),
                "the imported model's material should be tinted with the dog's breed coat color");
        }

        [Test]
        public void OnlyDogsAndHouses_AreInteractable()
        {
            // #37: no other interactable character exists in the world.
            // Houses stay tappable scenery (#20); dogs are the only
            // interactable characters.
            var interactables = worldRoot.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IInteractable>()
                .ToList();

            Assert.That(interactables, Is.Not.Empty);
            Assert.That(interactables.All(i => i is DogView || i is HouseView), Is.True,
                "unexpected interactable kind found");
        }
    }
}
