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
        public void TapRaycast_OnADogsBody_OpensItsConversation()
        {
            // #148 regression: the imported Cube Pets FBX carries no
            // collider (unlike the primitive fallback rig, whose capsule and
            // sphere brought their own), so TapRouter's Physics.Raycast
            // sailed straight through every dog and taps were dead in the
            // editor. A DogView must be physically hittable end-to-end:
            // camera ray at the body -> collider -> OnTapped -> conversation.
            var presenterHost = new GameObject("presenter", typeof(ConversationPresenter));
            var presenter = presenterHost.GetComponent<ConversationPresenter>();
            var view = worldRoot.GetComponentsInChildren<DogView>()
                .Single(v => v.Dog.Name == "Pepper");
            view.Dog.GiveQuest();
            view.RefreshBubble();

            // Isolate the dog so no house/fence can intercept the ray.
            view.transform.position = new Vector3(400f, 0f, 400f);

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var target = CombinedRendererBounds(view.transform.Find("Body")).center;
                cam.transform.position = target + new Vector3(0f, 6f, -6f);
                cam.transform.LookAt(target);
                Physics.SyncTransforms();

                var routed = TapRouter.RouteTap(cam, cam.WorldToScreenPoint(target));

                Assert.That(routed, Is.True,
                    "a raycast tap at the dog's body must hit a collider that routes to its DogView");
                Assert.That(presenter.IsOpen, Is.True,
                    "tapping a bubbled dog opens its conversation");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void TapRaycast_OnTheSpeechBubbleItself_RoutesToTheDog()
        {
            // #148 regression: DogView.Init used to destroy the bubble's
            // collider, so the speech bubble — the sole quest-discovery
            // surface (conversation-system.md) — was never clickable. A ray
            // at the bubble alone (horizontal, above the body) must route to
            // the owning dog via GetComponentInParent.
            var presenterHost = new GameObject("presenter", typeof(ConversationPresenter));
            var presenter = presenterHost.GetComponent<ConversationPresenter>();
            var view = worldRoot.GetComponentsInChildren<DogView>()
                .Single(v => v.Dog.Name == "Duke");
            view.Dog.GiveQuest();
            view.RefreshBubble();

            view.transform.position = new Vector3(400f, 0f, -400f);

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var bubble = view.transform.Find(DogView.BubbleName);
                var target = CombinedRendererBounds(bubble).center;
                cam.transform.position = target + new Vector3(0f, 0f, -8f);
                cam.transform.LookAt(target);
                Physics.SyncTransforms();

                var routed = TapRouter.RouteTap(cam, cam.WorldToScreenPoint(target));

                Assert.That(routed, Is.True,
                    "a raycast tap at the speech bubble must hit a collider that routes to the dog");
                Assert.That(presenter.IsOpen, Is.True,
                    "tapping the speech bubble opens the dog's conversation");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void SpeechBubble_ProjectsToAReadableTapTargetAtDefaultZoom()
        {
            // #148: Derek's editor check found bubbles rendering tiny — the
            // 0.5-unit graybox bubble was ~1% of screen height once the
            // world moved to the x7 kit scale (#150) with the camera showing
            // 2*DefaultZoom = 36 world units vertically. The bubble is the
            // sole quest-discovery surface, so through the real rig at
            // DefaultZoom on a 1080p-reference view it must project to at
            // least 40 px in both screen axes (minimum-tap-target
            // territory).
            var view = worldRoot.GetComponentsInChildren<DogView>()[0];
            view.Dog.GiveQuest();
            view.RefreshBubble();

            var rigObject = new GameObject("rig-under-test", typeof(Camera));
            var cam = rigObject.GetComponent<Camera>();
            var rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
            Assert.That(rig.Controller.Zoom,
                Is.EqualTo(Doggiehood.Core.Cameras.CameraController.DefaultZoom),
                "sanity: the rig starts at the default zoom");

            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var bounds = CombinedRendererBounds(view.transform.Find(DogView.BubbleName));
                var minX = float.MaxValue;
                var maxX = float.MinValue;
                var minY = float.MaxValue;
                var maxY = float.MinValue;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                    var screen = cam.WorldToScreenPoint(corner);
                    minX = Mathf.Min(minX, screen.x);
                    maxX = Mathf.Max(maxX, screen.x);
                    minY = Mathf.Min(minY, screen.y);
                    maxY = Mathf.Max(maxY, screen.y);
                }

                Assert.That(maxX - minX, Is.GreaterThanOrEqualTo(40f),
                    "speech bubble is too narrow on screen at default zoom to read or tap");
                Assert.That(maxY - minY, Is.GreaterThanOrEqualTo(40f),
                    "speech bubble is too short on screen at default zoom to read or tap");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(rigObject);
            }
        }

        private static Bounds CombinedRendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        [Test]
        public void SpeechBubble_HoversAFixedClearanceAboveTheDogsHead_ForAdultsAndPuppies()
        {
            // #148 follow-up: Derek wants the bubble clearly above the
            // dog's head. The hover height is derived from the dog's
            // measured renderer bounds — the bubble's bottom sits
            // BubbleClearanceAboveHead above the tallest body renderer for
            // adults and puppies alike (the old fixed 2.5*scale center
            // ignored the body entirely and left puppy bubbles overlapping
            // the dog). Measured via localPosition minus the bubble's own
            // half height so the assertion is independent of the billboard
            // rotation.
            foreach (var isPuppy in new[] { false, true })
            {
                var dog = new Dog(isPuppy ? "Pup" : "Grown", Breed.Beagle, Personality.Brave, 1, isPuppy);
                var go = new GameObject("hover-dog");
                go.transform.SetParent(worldRoot.transform);
                var view = go.AddComponent<DogView>();
                view.Init(dog, null);

                var bubble = go.transform.Find(DogView.BubbleName);
                var dogTop = float.MinValue;
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.transform.IsChildOf(bubble))
                    {
                        continue;
                    }

                    dogTop = Mathf.Max(dogTop, renderer.bounds.max.y);
                }

                var bubbleBottom = bubble.position.y - bubble.localScale.y / 2f;
                Assert.That(bubbleBottom - dogTop,
                    Is.EqualTo(DogView.BubbleClearanceAboveHead).Within(0.05f),
                    $"{(isPuppy ? "puppy" : "adult")} bubble must hover the fixed clearance above the head");

                Object.DestroyImmediate(go);
            }
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
