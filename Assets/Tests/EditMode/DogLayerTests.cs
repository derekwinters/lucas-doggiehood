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

            // The model is authored standing upright on a ground-level pivot,
            // so idle must not inherit the capsule rig's 90° pitch — that
            // pitch is what tipped dogs face-down and half-underground.
            Assert.That(Quaternion.Angle(body.localRotation, Quaternion.identity), Is.LessThan(1f),
                "imported model should stand upright at idle (no capsule-rig pitch)");
        }

        [Test]
        public void ImportedModel_CarriesAnimatorAndStartsOnIdleClip()
        {
            // Cube Pets animation wiring: with the imported model present,
            // Init must put an Animator on the Body and start the pack's
            // "idle" take through a PlayableGraph. Clip names may carry
            // importer decoration (e.g. "animal-dog|idle"), so we assert by
            // suffix, case-insensitively — mirroring DogView's own matching.
            var view = InitModelDog(out var go);

            var body = go.transform.Find("Body");
            Assert.That(body.GetComponent<Animator>(), Is.Not.Null,
                "imported model's Body must carry an Animator for the PlayableGraph output");

            Assert.That(view.CurrentAnimationClipName, Is.Not.Null,
                "with the Cube Pets asset importable, an animation clip should be playing");
            Assert.That(view.CurrentAnimationClipName.ToLowerInvariant(), Does.EndWith("idle"),
                "a standing dog plays the idle take");
        }

        [Test]
        public void TickAnimation_SwitchesBetweenWalkAndIdleWithMovement()
        {
            // While actively moving toward a wander target the walk take
            // plays; when the dog stops it returns to idle. TickAnimation is
            // the Update-driven hook, exposed so EditMode tests can drive
            // frames deterministically (no Play-mode loop here).
            var view = InitModelDog(out _);

            view.TickAnimation(0.1f, isMoving: true);
            Assert.That(view.CurrentAnimationClipName.ToLowerInvariant(), Does.EndWith("walk"),
                "moving -> walk take");

            view.TickAnimation(0.1f, isMoving: false);
            Assert.That(view.CurrentAnimationClipName.ToLowerInvariant(), Does.EndWith("idle"),
                "stopped -> back to idle take");
        }

        [Test]
        public void TickAnimation_LoopsClipTimeInsteadOfClampingAtTheEnd()
        {
            // Imported FBX takes default to non-looping (loop-time lives in
            // importer settings the repo doesn't own), so DogView loops
            // manually: after every tick the playable's local time must stay
            // inside [0, clip.length).
            var view = InitModelDog(out _);
            view.TickAnimation(0f, isMoving: true);

            var clips = Resources.LoadAll<AnimationClip>("animal-dog");
            var walk = clips.Single(c =>
                !c.name.ToLowerInvariant().StartsWith("__preview__") &&
                (c.name.ToLowerInvariant() == "walk" ||
                 c.name.ToLowerInvariant().EndsWith("|walk")));
            Assert.That(walk.length, Is.GreaterThan(0f), "sanity: walk take has duration");

            var bigStep = walk.length * 0.75f;
            for (var i = 0; i < 4; i++)
            {
                view.TickAnimation(bigStep, isMoving: true);
                Assert.That(view.CurrentAnimationTime, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(view.CurrentAnimationTime, Is.LessThan((double)walk.length),
                    "clip time must wrap, not clamp at the end of the non-looping take");
            }
        }

        private DogView InitModelDog(out GameObject go)
        {
            var dog = new Dog("Animator", Breed.Beagle, Personality.Brave, 1, false);
            go = new GameObject("anim-dog");
            go.transform.SetParent(worldRoot.transform);
            var view = go.AddComponent<DogView>();
            view.Init(dog, null);
            return view;
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
