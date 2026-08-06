using System;
using System.Linq;
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
            return LostItemQuestAt(itemName, new GridPoint(3f, -4f));
        }

        private static Quest LostItemQuestAt(string itemName, GridPoint position)
        {
            return new Quest(
                1, QuestType.LostItem, "Zeus", itemName,
                Array.Empty<string>(), position, null, null);
        }

        /// <summary>#580: a hidden-item point sitting squarely on a raised
        /// sidewalk band (the kit's curb+sidewalk band, which reads in-game as
        /// "the road" since the kit paves it) — the midpoint of a real Sidewalk
        /// edge in the state's own walk network, so the production
        /// <see cref="WalkNetwork.SurfaceHeightAt"/> lookup resolves it to the
        /// raised surface by construction.</summary>
        private static GridPoint OnRaisedBand(GameState state)
        {
            var band = state.WalkNetwork.Edges.First(e => e.Kind == WalkEdgeKind.Sidewalk);
            return new GridPoint((band.A.X + band.B.X) / 2f, (band.A.Z + band.B.Z) / 2f);
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

        // ---- #521/#535: the red "finder glow" on the lost item -----------
        // #535: Derek's revised design is a flat red GROUND RING only. The item
        // keeps its own mesh, size and colour — no engulfing halo, no size
        // pulse, no orbiting sparkle (in playtest those read as the item, and
        // the lost puppy, ballooning into "a big red ball").

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
        public void FinderGlow_GroundRingIsTheRedPaletteColour()
        {
            // Derek's decision: the glow is RED, sourced from the named
            // Palette.LostItemGlowHex constant (#521/#161).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var ring = Glow(view).Find(GroundRingName);
            Assert.That(ring, Is.Not.Null);

            var expected = CoreColors.FromHex(Palette.LostItemGlowHex);
            var actual = ring.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
        }

        [Test]
        public void FinderGlow_GroundRingIsAHollowAnnulus_NotAFilledDisc()
        {
            // #602: the finder glow is a ring OUTLINE, not a filled disc — its
            // mesh must have a genuine hole in the middle (no geometry inside
            // the inner radius) so the item and the ground it rests on stay
            // uncovered. This replaces the old disc-scale assertion (a solid
            // Cylinder primitive can't be hollow, so the shape is a generated
            // annulus mesh now).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var ring = Glow(view).Find(GroundRingName);

            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null, "the ring renders from a generated annulus mesh");

            var minRadius = float.MaxValue;
            var maxRadius = 0f;
            foreach (var v in mesh.vertices)
            {
                var r = Mathf.Sqrt((v.x * v.x) + (v.z * v.z));
                minRadius = Mathf.Min(minRadius, r);
                maxRadius = Mathf.Max(maxRadius, r);
                Assert.That(Mathf.Abs(v.y), Is.LessThan(0.001f),
                    "the ring is flat — every vertex lies in the XZ plane");
            }

            Assert.That(minRadius, Is.GreaterThan(0.001f),
                "a genuine hole in the middle — no geometry at the center, so it reads as a ring, not a disc");

            var expectedFraction = LostItemGlow.GroundRingInnerScale / LostItemGlow.GroundRingScale;
            Assert.That(minRadius / maxRadius, Is.EqualTo(expectedFraction).Within(0.02f),
                "the hole is sized to the named inner/outer ratio (#161/#602)");
        }

        [Test]
        public void FinderGlow_GroundRingOuterEdge_StillMatchesTheNamedScaleConstant()
        {
            // #602 regression: opening a hole must NOT shrink the ring's
            // footprint — its OUTER edge still spans LostItemGlow.GroundRingScale
            // (the pre-#602 disc's diameter), so the ring frames the same area
            // the disc used to cover. Named constants, not inline literals
            // (#161).
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var ring = Glow(view).Find(GroundRingName);

            var bounds = ring.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size.x, Is.EqualTo(LostItemGlow.GroundRingScale).Within(0.02f),
                "the ring's outer diameter is unchanged from the pre-#602 disc");
            Assert.That(bounds.size.z, Is.EqualTo(LostItemGlow.GroundRingScale).Within(0.02f));
        }

        [Test]
        public void FinderGlow_IsAGroundRingOnly_WithNoHaloOrSparkle()
        {
            // #535: the revised design is the flat ground ring ONLY — the
            // engulfing halo and the orbiting sparkle are gone, so nothing
            // balloons over or circles the item.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);
            var glow = Glow(view);

            Assert.That(glow.Find(GroundRingName), Is.Not.Null, "the glow keeps its ground ring");
            Assert.That(glow.Find(HaloName), Is.Null, "the engulfing halo is dropped (#535)");
            Assert.That(glow.Find(SparkleName), Is.Null, "the orbiting sparkle is dropped (#535)");
        }

        [Test]
        public void FinderGlow_LeavesTheItemAtItsNormalSizeAndColour()
        {
            // #535 core fix: the item itself is untouched — it renders at its
            // own graybox scale and its own material colour, NOT scaled up or
            // recoloured the finder-glow red.
            var view = LostItemView.Spawn(state, LostItemQuest("ball"), parent.transform);

            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one * LostItemView.SphereScale),
                "the lost item keeps its own size — the glow never scales it");

            var itemColour = view.GetComponent<Renderer>().sharedMaterial.color;
            var glowRed = CoreColors.FromHex(Palette.LostItemGlowHex);
            Assert.That(
                Mathf.Abs(itemColour.r - glowRed.r) > 0.05f
                || Mathf.Abs(itemColour.g - glowRed.g) > 0.05f
                || Mathf.Abs(itemColour.b - glowRed.b) > 0.05f,
                Is.True,
                "the item is not recoloured the finder-glow red — its own colour is preserved");
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
            // zone — a tap well outside the item but within the glow's ground
            // ring must NOT register, so the glow stays purely decorative.
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

                // A point over the glow's ground ring but well outside the small
                // item and its 32px padding: if the glow renderers were counted,
                // this would be inside the padded zone and collect; excluded, it
                // must miss.
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

        // ---- #580: the finder ring must track the surface the item rests on -
        // On grass (RoadSurfaceHeight = 0) the flat ring read fine, but the
        // raised paved band sits SidewalkSurfaceHeight above that plane, so a
        // ring fixed at the ground plane rendered buried under the band's mesh.
        // The ring's Y — and ONLY its Y — now follows the item's actual surface.

        private const float RingHeightTolerance = 0.001f;

        [Test]
        public void FinderGlow_OnARaisedBand_LiftsTheGroundRingToThatSurface_NotTheFlatGroundPlane()
        {
            // The item sits on the raised paved band, so the ring must float
            // just above THAT surface (SidewalkSurfaceHeight + GroundRingHeight),
            // not at the flat pre-fix ground-plane height where the raised road
            // mesh occluded it.
            var view = LostItemView.Spawn(state, LostItemQuestAt("ball", OnRaisedBand(state)), parent.transform);
            var ring = Glow(view).Find(GroundRingName);

            var expected = WorldDimensions.SidewalkSurfaceHeight + LostItemGlow.GroundRingHeight;
            Assert.That(ring.position.y, Is.EqualTo(expected).Within(RingHeightTolerance),
                "the ring floats just above the raised band the item rests on");
            Assert.That(ring.position.y,
                Is.GreaterThan(WorldDimensions.RoadSurfaceHeight + LostItemGlow.GroundRingHeight + RingHeightTolerance),
                "and clearly above the flat pre-fix ground-plane placement, so it is no longer occluded");
        }

        [Test]
        public void FinderGlow_OnGrass_KeepsTheGroundRingAtTheFlatSurface_Unchanged()
        {
            // #549 regression guard: away from any raised band, the ring keeps
            // its original flat placement (RoadSurfaceHeight + GroundRingHeight).
            // The fix only lifts the ring where the surface is actually raised.
            var view = LostItemView.Spawn(state, LostItemQuestAt("ball", new GridPoint(500f, 500f)), parent.transform);
            var ring = Glow(view).Find(GroundRingName);

            var expected = WorldDimensions.RoadSurfaceHeight + LostItemGlow.GroundRingHeight;
            Assert.That(ring.position.y, Is.EqualTo(expected).Within(RingHeightTolerance),
                "on-grass ring placement is unchanged from #549");
        }

        [Test]
        public void FinderGlow_RingScaleAndColour_AreUnchangedAcrossSurfaces()
        {
            // #580 is positioning-ONLY: only the ring's Y changes with the
            // surface. Its scale (GroundRingScale/GroundRingThickness) and
            // colour (Palette.LostItemGlowHex) must be identical on a raised
            // band and on grass (#535/#549 must not regress).
            var bandRing = Glow(LostItemView.Spawn(
                state, LostItemQuestAt("ball", OnRaisedBand(state)), parent.transform)).Find(GroundRingName);
            var grassRing = Glow(LostItemView.Spawn(
                state, LostItemQuestAt("ball", new GridPoint(500f, 500f)), parent.transform)).Find(GroundRingName);

            Assert.That(bandRing.localScale, Is.EqualTo(grassRing.localScale),
                "the ring is the same size on every surface");
            Assert.That(bandRing.localScale.x, Is.EqualTo(LostItemGlow.GroundRingScale).Within(0.0001f));
            Assert.That(bandRing.localScale.y, Is.EqualTo(LostItemGlow.GroundRingThickness).Within(0.0001f));
            Assert.That(bandRing.localScale.z, Is.EqualTo(LostItemGlow.GroundRingScale).Within(0.0001f));

            var expected = CoreColors.FromHex(Palette.LostItemGlowHex);
            var bandColour = bandRing.GetComponent<Renderer>().sharedMaterial.color;
            var grassColour = grassRing.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(bandColour.r, Is.EqualTo(grassColour.r).Within(0.001f));
            Assert.That(bandColour.g, Is.EqualTo(grassColour.g).Within(0.001f));
            Assert.That(bandColour.b, Is.EqualTo(grassColour.b).Within(0.001f));
            Assert.That(bandColour.r, Is.EqualTo(expected.r).Within(0.01f));
            Assert.That(bandColour.g, Is.EqualTo(expected.g).Within(0.01f));
            Assert.That(bandColour.b, Is.EqualTo(expected.b).Within(0.01f));
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
