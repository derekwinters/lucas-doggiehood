using System;
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
