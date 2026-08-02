using System;
using Doggiehood.Core.Art;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class LostItemViewTests
    {
        /// <summary>An axis-aligned bounding box has 8 corners; used when
        /// projecting the item's world bounds to screen space (#311/#335).</summary>
        private const int BoundsCornerCount = 8;

        /// <summary>DogView scales a puppy DOG to this (a bare literal in
        /// DogView.Init); the lost-item puppy must read as "slightly smaller"
        /// than a puppy dog per Derek's #335 direction.</summary>
        private const float PuppyDogScale = 0.55f;

        private GameState state;
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            state = GameState.CreateNew();
            parent = new GameObject("lost-item-parent");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }

        private static Quest LostItemQuest(string itemName)
        {
            return new Quest(
                1, QuestType.LostItem, "Zeus", itemName,
                Array.Empty<string>(), new GridPoint(3f, -4f), null, null);
        }

        [Test]
        public void SpawnForPuppy_UsesTheImportedDogModel_NotAPrimitiveSphere()
        {
            // #335: a lost-puppy quest must show the real shared Cube Pets dog
            // model (reused from DogView), not the generic graybox sphere every
            // other lost item still uses.
            var view = LostItemView.Spawn(state, LostItemQuest("puppy"), parent.transform);

            var body = view.transform.Find("Body");
            Assert.That(body, Is.Not.Null, "the puppy lost-item must instantiate a 'Body' child from the model");

            var meshFilter = body.GetComponentInChildren<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh.name.ToLowerInvariant(), Does.Not.Contain("sphere"),
                "the puppy must be the imported dog mesh, not a primitive sphere");

            Assert.That(body.GetComponentInChildren<MeshRenderer>(), Is.Not.Null,
                "the imported puppy model renders through a MeshRenderer");
        }

        [Test]
        public void SpawnForNonPuppySubject_StillUsesTheSphereFallback()
        {
            // Only "puppy" has a reusable model; toy (#332) / ball (#333) keep
            // the sphere placeholder until their assets land.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);

            Assert.That(view.transform.Find("Body"), Is.Null,
                "a non-puppy subject must not instantiate the imported model");

            var meshFilter = view.GetComponent<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh.name.ToLowerInvariant(), Does.Contain("sphere"),
                "non-puppy lost items keep the graybox sphere fallback");
        }

        [Test]
        public void SpawnForPuppy_CarriesAFittedTapCollider()
        {
            // #148/#335: the imported FBX ships no collider, so without a fitted
            // box TapRouter's Physics.Raycast passes straight through and taps
            // never register.
            var view = LostItemView.Spawn(state, LostItemQuest("puppy"), parent.transform);

            var collider = view.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null,
                "the puppy needs a fitted BoxCollider on the interactable root for raycast taps");
            Assert.That(collider.size, Is.Not.EqualTo(Vector3.zero),
                "the collider must be fitted to the model's bounds, not left empty");
        }

        [Test]
        public void SpawnForPuppy_UsesANamedScaleConstant_SmallerThanAPuppyDog()
        {
            // Derek's #335 direction: "the same dog model we use for puppies,
            // but make it slightly smaller."
            Assert.That(LostItemView.PuppyModelScale, Is.LessThan(PuppyDogScale),
                "the lost-item puppy must be slightly smaller than a puppy dog (0.55)");

            var view = LostItemView.Spawn(state, LostItemQuest("puppy"), parent.transform);
            var body = view.transform.Find("Body");

            Assert.That(body.localScale.x, Is.EqualTo(LostItemView.PuppyModelScale).Within(0.0001f));
            Assert.That(body.localScale.y, Is.EqualTo(LostItemView.PuppyModelScale).Within(0.0001f));
            Assert.That(body.localScale.z, Is.EqualTo(LostItemView.PuppyModelScale).Within(0.0001f));
        }

        [Test]
        public void SpawnForPuppy_PlacesItAtTheHiddenPosition_AndItStaysPut()
        {
            // #335: "have it remain in place" — no wander/movement is added.
            var quest = LostItemQuest("puppy");
            var view = LostItemView.Spawn(state, quest, parent.transform);

            Assert.That(view.transform.position.x,
                Is.EqualTo(quest.HiddenItemPosition.Value.X).Within(0.001f));
            Assert.That(view.transform.position.z,
                Is.EqualTo(quest.HiddenItemPosition.Value.Z).Within(0.001f));

            // A slow in-place look-around may rotate the puppy, but it must
            // never translate it away from the hidden spot.
            var before = view.transform.position;
            view.TickLookAround(1f);
            Assert.That(Vector3.Distance(view.transform.position, before), Is.LessThan(0.0001f),
                "the puppy stays put — look-around is rotation only, never movement");
        }

        [Test]
        public void PuppyLookAround_TurnsInPlaceOverTime()
        {
            // Optional #335 "consider": a slow in-place yaw so the puppy
            // appears to look around. TickLookAround is the test-drivable hook
            // (EditMode can't run the Play-mode Update loop), mirroring
            // DogView.TickAnimation.
            var view = LostItemView.Spawn(state, LostItemQuest("puppy"), parent.transform);
            var before = view.transform.rotation;

            view.TickLookAround(1f);

            Assert.That(Quaternion.Angle(view.transform.rotation, before), Is.GreaterThan(1f),
                "the puppy should slowly turn in place to look around");
        }

        [Test]
        public void TryHandleLostItemTap_StillHitsThePaddedZone_AfterTheMeshSwap()
        {
            // #311 forgiving tap zone must survive the mesh swap: a tap within
            // the puppy model's padded projected bounds still completes.
            var quest = LostItemQuest("puppy");
            var view = LostItemView.Spawn(state, quest, parent.transform);

            view.transform.position = new Vector3(500f, 0f, 500f);

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var bounds = CombinedRendererBounds(view.transform);
                cam.transform.position = bounds.center + new Vector3(0f, 6f, -6f);
                cam.transform.LookAt(bounds.center);
                Physics.SyncTransforms();

                var screenCenter = cam.WorldToScreenPoint(bounds.center);
                var handled = view.TryHandleLostItemTap(cam, new Vector2(screenCenter.x, screenCenter.y));

                Assert.That(handled, Is.True,
                    "a tap on the puppy's projected bounds must register via the #311 padded zone");
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        // ---- #521: the red "finder glow" on the lost item ----------------

        private const string GlowRootName = "FinderGlow";
        private const string HaloName = "Halo";
        private const string GroundRingName = "GroundRing";
        private const string SparkleName = "Sparkle";

        private static Transform Glow(LostItemView view)
        {
            return view.transform.Find(GlowRootName);
        }

        [Test]
        public void Spawn_AttachesTheFinderGlow_AsAChildOfTheLostItem()
        {
            // #521: a red finder glow is attached to the hidden item so it pops
            // on any surface. It lives as a child of the item view, so it
            // shares the view's lifecycle — collect/dismiss destroys the view
            // and the glow with it.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);

            var glow = Glow(view);
            Assert.That(glow, Is.Not.Null, "the finder glow must be a child of the lost item view");
            Assert.That(glow.GetComponentInChildren<Renderer>(), Is.Not.Null,
                "the glow must actually render something");
        }

        [Test]
        public void FinderGlow_IsAttachedForEverySubject_IncludingThePuppyModel()
        {
            // The glow gates on quest state (Core LostItemGlow.ShouldShow), not
            // on which model renders — a lost puppy gets the same finder glow as
            // a graybox ball.
            var puppyView = LostItemView.Spawn(state, LostItemQuest("puppy"), parent.transform);

            Assert.That(Glow(puppyView), Is.Not.Null,
                "the puppy lost item must also carry the finder glow");
        }

        [Test]
        public void FinderGlow_HaloIsTheRedPaletteColour()
        {
            // Derek's decision: the glow is RED, sourced from the named
            // Palette.LostItemGlowHex constant (#521/#161).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var halo = Glow(view).Find(HaloName);
            Assert.That(halo, Is.Not.Null);

            var expected = CoreColors.FromHex(Palette.LostItemGlowHex);
            var actual = halo.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
        }

        [Test]
        public void FinderGlow_HaloUsesTheNamedScaleConstant()
        {
            // #161: the halo size is the named Core constant, not an inline
            // literal. Before any pulse tick it sits at exactly HaloScale.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var halo = Glow(view).Find(HaloName);

            Assert.That(halo.localScale.x, Is.EqualTo(LostItemGlow.HaloScale).Within(0.0001f));
            Assert.That(halo.localScale.y, Is.EqualTo(LostItemGlow.HaloScale).Within(0.0001f));
            Assert.That(halo.localScale.z, Is.EqualTo(LostItemGlow.HaloScale).Within(0.0001f));
        }

        [Test]
        public void FinderGlow_HasAGroundContactRingAndASparkle()
        {
            // The effect is halo + ground contact ring + subtle sparkle (#521).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var glow = Glow(view);

            Assert.That(glow.Find(GroundRingName), Is.Not.Null, "the glow needs a ground contact ring");
            Assert.That(glow.Find(SparkleName), Is.Not.Null, "the glow needs a sparkle");
        }

        [Test]
        public void FinderGlow_IsNonInteractive_SoItNeverStealsTapToCollect()
        {
            // The glow is pure decoration: no colliders anywhere in its subtree
            // and no IInteractable, so TapRouter's raycast passes straight
            // through it to the item beneath (tap-to-collect stays intact).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var glow = Glow(view);

            Assert.That(glow.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty,
                "the finder glow must carry no colliders");
            Assert.That(glow.GetComponentsInChildren<IInteractable>(true), Is.Empty,
                "the finder glow must not be interactable");
        }

        [Test]
        public void FinderGlow_PulsesTheHalo_OverTime()
        {
            // The halo breathes via the deterministic Core pulse curve. Driving
            // TickGlow to the half-period peak scales the halo to
            // HaloScale x PulseScaleMax (EditMode can't run the Play Update).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var halo = Glow(view).Find(HaloName);

            view.TickGlow(LostItemGlow.PulsePeriodSeconds / 2f);

            var expected = LostItemGlow.HaloScale * LostItemGlow.PulseScaleMax;
            Assert.That(halo.localScale.x, Is.EqualTo(expected).Within(0.001f),
                "the halo peaks at HaloScale x PulseScaleMax half-way through the pulse");
        }

        [Test]
        public void FinderGlow_SparkleOrbits_OverTime()
        {
            // The sparkle drifts around the item on the Core orbit curve.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var sparkle = Glow(view).Find(SparkleName);
            var before = sparkle.localPosition;

            view.TickGlow(0.5f);

            Assert.That(Vector3.Distance(sparkle.localPosition, before), Is.GreaterThan(0.0001f),
                "the sparkle should move as it orbits the item");
        }

        [Test]
        public void FinderGlow_IsTornDownWithTheItem_OnCollectOrDismiss()
        {
            // The glow is a child of the item view, so when the item is
            // collected/dismissed (the view GameObject is destroyed) the glow
            // goes with it — it's never left lingering in the world.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var glow = Glow(view);
            Assert.That(glow, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(view.gameObject);

            Assert.That(glow == null, Is.True,
                "destroying the item view must remove its finder glow too");
        }

        [Test]
        public void FinderGlow_DoesNotInflateTheForgivingTapZone()
        {
            // The glow's child renderers must not expand the #311 padded tap
            // zone — a tap well outside the item but within the glow halo must
            // NOT register, so the glow stays purely decorative.
            var quest = LostItemQuest("ball");
            var view = LostItemView.Spawn(state, quest, parent.transform);
            view.transform.position = new Vector3(500f, 0f, 500f);

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var item = view.GetComponent<Renderer>().bounds;
                cam.transform.position = item.center + new Vector3(0f, 6f, -6f);
                cam.transform.LookAt(item.center);
                Physics.SyncTransforms();

                // A point inside the glow halo but well outside the small item
                // and its 32px padding: if the glow renderers were counted, this
                // would be inside the padded zone and collect; excluded, it must
                // miss.
                var inGlowOffItem = item.center + new Vector3(item.extents.x + 0.5f, 0f, 0f);
                var screen = cam.WorldToScreenPoint(inGlowOffItem);
                var handled = view.TryHandleLostItemTap(cam, new Vector2(screen.x, screen.y));

                Assert.That(handled, Is.False,
                    "a tap on the glow but off the item must not collect — the glow is decoration");
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(camGo);
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
    }
}
