using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Art;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #571: the onboarding "fix up a home" (upgrade a house) step marks the
    /// target house with the existing red ground-ring highlight (#535) so it's
    /// obvious which house to tap and upgrade. The decision lives in Core
    /// (<see cref="OnboardingHouseHighlight"/>); these cover the thin Unity
    /// apply-seam — the flat, non-pulsing, collider-free ring reusing the
    /// finder-glow visual, its footprint sized off the target house's own
    /// renderer bounds (mirroring <see cref="BugSwarmView"/>), and the director's
    /// attach/teardown lifecycle keyed to exactly the recorded target house,
    /// independent of the #506 coach-bar panel suppression.
    ///
    /// <para>#669: the seam now takes its diameter straight from the engine-free
    /// <see cref="TargetRingGeometry"/> rule and centres the ring on the house's
    /// BOUNDS centre rather than its transform pivot, so the house sits fully
    /// inside the ring's hole with open ground to spare instead of covering the
    /// band on every diagonal.</para>
    /// </summary>
    public class OnboardingHouseHighlightDirectorTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject worldRoot;
        private GameState state;
        private OnboardingHouseHighlightDirector director;

        [SetUp]
        public void BuildWorldAndDirector()
        {
            // Shared process-global gates a prior test may have left dirty (#544/#546).
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();

            state = GameState.CreateNew();
            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);

            var host = new GameObject("highlight-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<OnboardingHouseHighlightDirector>();
            director.Init(state, worldRoot.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var overlay in Object.FindObjectsByType<HouseProfileOverlay>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(overlay.gameObject);
            }

            Object.DestroyImmediate(worldRoot);
        }

        /// <summary>Advances the reward chain to the UpgradeHouse step and records
        /// the first starter house as the target; returns that target id.</summary>
        private int ReachUpgradeStep()
        {
            var target = state.Houses[0].Id;
            state.GrantOnboardingCompletionReward(target);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse),
                "precondition: the chain is waiting on UpgradeHouse");
            return target;
        }

        private static OnboardingHouseHighlightView[] Highlights()
        {
            return Object.FindObjectsByType<OnboardingHouseHighlightView>(FindObjectsSortMode.None);
        }

        [Test]
        public void TargetHouse_SpawnsTheFinderGlowRing_Flat_NonPulsing_RedTranslucent_NoCollider()
        {
            var target = ReachUpgradeStep();

            director.Refresh();

            var highlight = Highlights().Single();
            Assert.That(highlight.HouseId, Is.EqualTo(target), "the ring is bound to the target house");

            // Exactly one ring renderer — no engulfing halo, no orbiting sparkle
            // (the #535 revision dropped both; the highlight reuses only the ring).
            var renderers = highlight.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.EqualTo(1),
                "a single ground ring only — no halo/sparkle satellites");

            var ring = renderers[0];

            // Flat: the ring is a thin disc on the ground, not a tall puck.
            Assert.That(ring.bounds.size.y, Is.LessThan(0.5f), "the ring is flat");
            Assert.That(ring.bounds.size.x, Is.GreaterThan(ring.bounds.size.y * 4f),
                "the ring reads as a wide pool of light, not a puck");

            // The established finder-glow red, translucent so it blends over the
            // surface rather than occluding it.
            var expected = CoreColors.FromHex(Palette.LostItemGlowHex);
            var color = ring.sharedMaterial.color;
            Assert.That(color.r, Is.EqualTo(expected.r).Within(0.02f));
            Assert.That(color.g, Is.EqualTo(expected.g).Within(0.02f));
            Assert.That(color.b, Is.EqualTo(expected.b).Within(0.02f));
            Assert.That(color.a, Is.GreaterThan(0f).And.LessThan(1f), "translucent, not a solid red disc");

            // Non-interactive: never intercepts the house's own tap.
            Assert.That(highlight.GetComponentsInChildren<Collider>(), Is.Empty,
                "the highlight carries no collider");

            // Non-pulsing: the view runs no per-frame animation (unlike the bug
            // swarm's bob/spin), so it can't balloon or throb like the #535
            // regression the ring replaced.
            Assert.That(
                typeof(OnboardingHouseHighlightView).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                Is.Null,
                "the highlight is static — no Update-driven pulse");
        }

        [Test]
        public void RingFootprint_IsDerivedFromTheHouseRendererBounds_NotAFixedLostItemScale()
        {
            // Two synthetic houses of deliberately different size: the ring's
            // footprint must track each house's own renderer bounds (mirroring
            // BugSwarmView's encapsulate-bounds sizing), so it reads correctly
            // under any house variant/level — never the fixed
            // LostItemGlow.GroundRingScale a lost item uses.
            var smallHouse = SyntheticHouse(1f, new Vector3(100f, 0f, 100f));
            var bigHouse = SyntheticHouse(8f, new Vector3(140f, 0f, 140f));

            var smallRing = OnboardingHouseHighlightView.Spawn(101, smallHouse.transform, worldRoot.transform)
                .GetComponentInChildren<Renderer>();
            var bigRing = OnboardingHouseHighlightView.Spawn(102, bigHouse.transform, worldRoot.transform)
                .GetComponentInChildren<Renderer>();

            var smallFootprint = smallRing.bounds.size.x;
            var bigFootprint = bigRing.bounds.size.x;

            // A bigger house yields a proportionally bigger ring — the same
            // house-to-ring ratio for both, so the footprint is truly derived
            // from the bounds rather than a size-independent constant.
            Assert.That(bigFootprint, Is.GreaterThan(smallFootprint),
                "a larger house gets a larger ring");
            var smallRatio = smallFootprint / 1f;
            var bigRatio = bigFootprint / 8f;
            Assert.That(bigRatio, Is.EqualTo(smallRatio).Within(0.05f),
                "the ring footprint scales linearly with the house's own renderer bounds");

            // And neither equals the fixed lost-item ring scale (2.2), proving the
            // lost-item constant was not reused verbatim for a house-sized target.
            Assert.That(smallFootprint, Is.Not.EqualTo(LostItemGlow.GroundRingScale).Within(0.01f));
            Assert.That(bigFootprint, Is.GreaterThan(LostItemGlow.GroundRingScale),
                "an 8-unit house's ring dwarfs the fixed lost-item scale");
        }

        [Test]
        public void Ring_IsAHollowAnnulus_NotAFilledDisc()
        {
            // #602: the onboarding house highlight is the same red ring OUTLINE
            // as the finder glow — its mesh has a genuine hole so it frames the
            // house without painting a disc over the ground inside it. Sharing
            // the annulus mesh keeps both highlights visually consistent.
            var house = SyntheticHouse(4f, new Vector3(120f, 0f, 120f));
            var mesh = OnboardingHouseHighlightView.Spawn(103, house.transform, worldRoot.transform)
                .GetComponentInChildren<MeshFilter>().sharedMesh;

            Assert.That(mesh, Is.Not.Null, "the highlight renders from a generated annulus mesh");

            var minRadius = float.MaxValue;
            var maxRadius = 0f;
            foreach (var v in mesh.vertices)
            {
                var r = Mathf.Sqrt((v.x * v.x) + (v.z * v.z));
                minRadius = Mathf.Min(minRadius, r);
                maxRadius = Mathf.Max(maxRadius, r);
            }

            Assert.That(minRadius, Is.GreaterThan(0.001f),
                "a genuine hole in the middle — a ring, not a disc");
            var expectedFraction = LostItemGlow.GroundRingInnerScale / LostItemGlow.GroundRingScale;
            Assert.That(minRadius / maxRadius, Is.EqualTo(expectedFraction).Within(0.02f),
                "the hole matches the shared finder-glow inner/outer ratio, so both highlights stay consistent");
        }

        [TestCase(6f, 6f, TestName = "a square house")]
        [TestCase(14f, 4f, TestName = "a strongly rectangular house")]
        [TestCase(4f, 14f, TestName = "a strongly rectangular house, other axis")]
        public void Ring_ContainsTheWholeHouse_CornersIncluded_WithAVisibleGapOfOpenGround(
            float sizeX, float sizeZ)
        {
            // #669: the ring used to be sized from the house's LONGEST SIDE
            // (1.15 · max(x, z)) and applied as a diameter, so a square house's
            // own corners (1.414·w) reached past the ring's OUTER edge — the
            // house covered the whole band on all four diagonals and it read as
            // a few red slivers. The band is hollow, so the edge that has to
            // clear the house is the ring's INNER one.
            var house = SyntheticHouse(new Vector3(sizeX, 5f, sizeZ), new Vector3(200f, 0f, 200f));
            var ring = OnboardingHouseHighlightView.Spawn(110, house.transform, worldRoot.transform)
                .GetComponentInChildren<Renderer>();

            var outerDiameter = ring.bounds.size.x;
            var innerRadius = 0.5f * TargetRingGeometry.InnerDiameter(outerDiameter);
            var houseCornerReach = 0.5f * Mathf.Sqrt((sizeX * sizeX) + (sizeZ * sizeZ));

            Assert.That(innerRadius, Is.GreaterThan(houseCornerReach),
                "the whole house, corners included, sits inside the ring's hole");
            Assert.That(
                innerRadius - houseCornerReach,
                Is.GreaterThan(houseCornerReach * TargetRingGeometry.FootprintGapFraction * 0.99f),
                "with the named gap of open ground between the house and the band");

            // ...and not absurdly oversized on the short axis: a shallow house
            // gets a smaller ring than a square house of its long side would.
            var longestSide = Mathf.Max(sizeX, sizeZ);
            Assert.That(outerDiameter,
                Is.LessThanOrEqualTo(TargetRingGeometry.OuterDiameter(longestSide, longestSide) + 0.01f),
                "the ring is sized from the footprint's diagonal, not from a square of its longest side");
        }

        [Test]
        public void Ring_TakesItsDiameterFromTheCoreSizingRule_NotItsOwnUnityNumbers()
        {
            // The sizing rule is engine-free Core so the build-step foundation
            // highlight (#668) reuses it rather than re-deriving its own
            // numbers; the Unity seam only applies it.
            var house = SyntheticHouse(new Vector3(9f, 6f, 5f), new Vector3(220f, 0f, 220f));
            var ring = OnboardingHouseHighlightView.Spawn(111, house.transform, worldRoot.transform)
                .GetComponentInChildren<Renderer>();

            Assert.That(ring.transform.lossyScale.x,
                Is.EqualTo(TargetRingGeometry.OuterDiameter(9f, 5f)).Within(0.01f));
            Assert.That(ring.transform.lossyScale.z,
                Is.EqualTo(TargetRingGeometry.OuterDiameter(9f, 5f)).Within(0.01f));
        }

        [Test]
        public void Ring_IsConcentricWithTheHouseMesh_NotItsTransformPivot()
        {
            // #669: the root was positioned from houseTransform.position while
            // the SIZE came from the renderer bounds, so a house variant whose
            // pivot is not at its bounds centre got an off-centre ring that
            // clipped harder on one side. Centre on the mesh.
            var offset = new Vector3(3.5f, 0f, -2.25f);
            var house = SyntheticHouse(new Vector3(8f, 5f, 6f), new Vector3(240f, 0f, 240f), offset);

            var ring = OnboardingHouseHighlightView.Spawn(112, house.transform, worldRoot.transform)
                .GetComponentInChildren<Renderer>();

            var meshCenter = house.GetComponentInChildren<Renderer>().bounds.center;
            Assert.That(ring.bounds.center.x, Is.EqualTo(meshCenter.x).Within(0.01f),
                "the ring is centred on the house's actual mesh, not its pivot");
            Assert.That(ring.bounds.center.z, Is.EqualTo(meshCenter.z).Within(0.01f));
        }

        [Test]
        public void NoLongestSideMultiplierConstant_SoASubDiagonalValueCannotComeBack()
        {
            // #669/#161: `FootprintMultiplier` is deleted rather than retuned —
            // any surviving "multiply the longest side" constant invites someone
            // to put a value below √2 back, which cannot contain the house no
            // matter how it is tuned.
            Assert.That(
                typeof(OnboardingHouseHighlightView).GetField(
                    "FootprintMultiplier",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                Is.Null,
                "the retired longest-side multiplier is gone, not retuned");
        }

        [Test]
        public void Highlight_AttachesToExactlyTheTargetHouse_AndTearsDownWhenTheChainAdvancesPastUpgrade()
        {
            var target = ReachUpgradeStep();

            director.Refresh();

            // Exactly one highlight, on the recorded target — never on any other house.
            var highlights = Highlights();
            Assert.That(highlights.Length, Is.EqualTo(1), "only one house is highlighted");
            Assert.That(highlights[0].HouseId, Is.EqualTo(target), "and it is exactly the recorded target");
            foreach (var house in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                if (house.HouseId != target)
                {
                    Assert.That(highlights.Any(h => h.HouseId == house.HouseId), Is.False,
                        "no non-target house carries a highlight");
                }
            }

            // Upgrading the target advances the chain past UpgradeHouse — the
            // 100-coin step-1 bonus funds the 100-coin upgrade.
            Assert.That(state.TryUpgradeHouse(target), Is.True, "the target house upgrades");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.ExpandMap));

            director.Refresh();

            Assert.That(Highlights(), Is.Empty,
                "the highlight is torn down the instant the chain advances past UpgradeHouse");
        }

        [Test]
        public void Highlight_IsCleared_ForAReloadWhoseChainIsAlreadyPastUpgrade()
        {
            // A returning player whose chain sits past UpgradeHouse must never see
            // the highlight, even though the target id persists (#469). Init's
            // first Refresh must spawn nothing.
            var target = state.Houses[0].Id;
            state.GrantOnboardingCompletionReward(target);
            state.TryUpgradeHouse(target); // -> ExpandMap, target id still stored

            director.Refresh();

            Assert.That(state.OnboardingUpgradeTargetHouseId, Is.EqualTo(target),
                "the target id still persists past the step");
            Assert.That(Highlights(), Is.Empty, "but no highlight shows once past UpgradeHouse");
        }

        [Test]
        public void Highlight_StaysVisible_WhileACenteredHouseProfilePanelIsOpen()
        {
            // #506 suppresses the bottom coach bar while a centered panel is open —
            // and that is the very gap this highlight fills. So the highlight must
            // NOT inherit that suppression: it stays on the target house even with
            // the house-profile panel open over it.
            var target = ReachUpgradeStep();
            director.Refresh();
            Assert.That(Highlights().Single().HouseId, Is.EqualTo(target), "precondition: highlight is up");

            var overlay = BuildOpenHouseProfile(target);
            Assert.That(overlay.IsOpen, Is.True, "precondition: a centered panel is open");

            director.Refresh();

            var highlight = Highlights().SingleOrDefault();
            Assert.That(highlight, Is.Not.Null, "the highlight persists while a centered panel is open");
            Assert.That(highlight.HouseId, Is.EqualTo(target));
        }

        /// <summary>A bare house transform whose single child cube renderer is
        /// <paramref name="size"/> units on each side, so its encapsulated
        /// renderer bounds have a known XZ extent.</summary>
        private GameObject SyntheticHouse(float size, Vector3 position)
        {
            return SyntheticHouse(Vector3.one * size, position, Vector3.zero);
        }

        /// <summary>A bare house transform whose single child cube renderer has
        /// the given non-uniform <paramref name="size"/>, so the ring can be
        /// checked against a strongly rectangular footprint as well as a square
        /// one.</summary>
        private GameObject SyntheticHouse(Vector3 size, Vector3 position)
        {
            return SyntheticHouse(size, position, Vector3.zero);
        }

        /// <summary>As above, with the mesh shifted <paramref name="meshOffset"/>
        /// from the house's transform pivot — the pivot-offset house variant the
        /// #669 concentricity check needs.</summary>
        private GameObject SyntheticHouse(Vector3 size, Vector3 position, Vector3 meshOffset)
        {
            var house = new GameObject("synthetic-house");
            house.transform.SetParent(worldRoot.transform);
            house.transform.position = position;

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(house.transform, worldPositionStays: false);
            mesh.transform.localPosition = meshOffset;
            mesh.transform.localScale = size;
            return house;
        }

        /// <summary>Opens the real centered house-profile overlay (#208) for the
        /// given house, following the established EditMode setup (canvas +
        /// bundled font), so the panel-independence assertion exercises a genuine
        /// open centered panel rather than a stand-in.</summary>
        private HouseProfileOverlay BuildOpenHouseProfile(int houseId)
        {
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            var canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.transform.SetParent(worldRoot.transform);
            canvasHost.AddComponent<UiCanvas>().Configure();

            var overlayHost = new GameObject("house-profile-overlay");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            var overlay = overlayHost.AddComponent<HouseProfileOverlay>();
            overlay.Init();

            var house = state.Houses.Single(h => h.Id == houseId);
            var residents = state.Dogs.Where(d => d.HouseId == houseId).ToList();
            overlay.Open(house, residents);
            return overlay;
        }
    }
}
