using System.Linq;
using Doggiehood.Core.Expansion;
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
    /// #668: the onboarding "build a new house" step — the LAST reward-chain step
    /// and the only one that had no world-space "this one" cue — now carries the
    /// same red ground-ring highlight the "fix up a home" step got in #571, on
    /// the foundation slab of the lot it wants tapped. It bites harder here than
    /// on the upgrade step because the build flow opens a centered confirmation
    /// dialog (#406), which is exactly when the bottom coach bar is suppressed
    /// (#506) — so the ring is deliberately NOT coach-bar-suppressed.
    ///
    /// <para>The decision is engine-free Core
    /// (<see cref="OnboardingHouseHighlight.TargetHouseId"/> → the easternmost
    /// buildable empty lot); these cover the thin Unity apply-seam: the director
    /// resolving an <see cref="EmptyLotView"/> foundation as a target, the
    /// attach/teardown lifecycle, the collider-free non-interference with the
    /// lot's own tap, and the ring taking its diameter from the shared #669
    /// <see cref="TargetRingGeometry"/> rule rather than its own numbers.</para>
    /// </summary>
    public class OnboardingBuildLotHighlightTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject worldRoot;
        private GameObject canvasHost;
        private GameState state;
        private ConfirmationDialog dialog;
        private ExpansionDirector expansion;
        private OnboardingHouseHighlightDirector director;

        [SetUp]
        public void BuildWorldWaitingOnTheBuildStep()
        {
            // Shared process-global gates a prior test may have left dirty (#544/#546).
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            state = ReachBuildStep();
            worldRoot = WorldBuilder.Build(state);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var dialogHost = new GameObject("dialog");
            dialogHost.transform.SetParent(canvasHost.transform, false);
            dialog = dialogHost.AddComponent<ConfirmationDialog>();
            dialog.Init();

            var expansionHost = new GameObject("expansion-director-host");
            expansionHost.transform.SetParent(worldRoot.transform);
            expansion = expansionHost.AddComponent<ExpansionDirector>();
            expansion.Init(state, worldRoot.transform, dialog);

            var host = new GameObject("highlight-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<OnboardingHouseHighlightDirector>();
            director.Init(state, worldRoot.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(worldRoot);
            Object.DestroyImmediate(canvasHost);
        }

        /// <summary>A game walked to the final <see cref="OnboardingRewardStep.BuildHouse"/>
        /// step: first quest granted, target house upgraded, the scripted first
        /// frontier tile unlocked — with enough left to afford the build.</summary>
        private static GameState ReachBuildStep()
        {
            var built = GameState.CreateNew();
            built.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());

            var house = built.Houses[0].Id;
            built.GrantOnboardingCompletionReward(house); // -> UpgradeHouse
            built.TryUpgradeHouse(house);                 // -> ExpandMap
            built.Wallet.Deposit(TileUnlock.Cost(built.Map.Tiles.Count));
            built.TryUnlockTile(FrontierEditModeWorld.FirstTile); // -> BuildHouse
            built.Wallet.Deposit(HouseBuildNumbers.Cost(built.PlayerBuiltHouseCount));

            Assert.That(built.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse),
                "precondition: the chain is waiting on BuildHouse");
            return built;
        }

        private static OnboardingHouseHighlightView[] Highlights()
        {
            return Object.FindObjectsByType<OnboardingHouseHighlightView>(FindObjectsSortMode.None);
        }

        /// <summary>The lot the Core rule picks — the easternmost buildable empty
        /// lot. Asserted non-null so a setup regression fails loudly rather than
        /// silently making every test vacuous.</summary>
        private int TargetLotId()
        {
            var target = OnboardingHouseHighlight.TargetHouseId(state);
            Assert.That(target, Is.Not.Null, "precondition: Core resolves a build target lot");
            return target.Value;
        }

        [Test]
        public void BuildStep_AttachesTheRingToTheTargetLotFoundation_AndToNoOtherLot()
        {
            var target = TargetLotId();

            director.Refresh();

            var highlights = Highlights();
            Assert.That(highlights.Length, Is.EqualTo(1), "only one lot is highlighted");
            Assert.That(highlights[0].HouseId, Is.EqualTo(target),
                "and it is exactly the lot the east-lot rule resolves");

            var otherLots = worldRoot.GetComponentsInChildren<EmptyLotView>()
                .Select(v => v.HouseId)
                .Where(id => id != target)
                .ToList();
            Assert.That(otherLots, Is.Not.Empty,
                "precondition: the unlocked tile has more than one buildable lot, so 'no other lot' is meaningful");
            foreach (var id in otherLots)
            {
                Assert.That(highlights.Any(h => h.HouseId == id), Is.False,
                    "no non-target empty lot carries a highlight");
            }

            // The ring sits on the target lot's foundation slab, not somewhere else.
            var foundation = worldRoot.GetComponentsInChildren<EmptyLotView>().Single(v => v.HouseId == target);
            var ring = highlights[0].GetComponentInChildren<Renderer>();
            Assert.That(ring.bounds.center.x, Is.EqualTo(foundation.transform.position.x).Within(0.01f),
                "the ring is concentric with the target foundation");
            Assert.That(ring.bounds.center.z, Is.EqualTo(foundation.transform.position.z).Within(0.01f));
        }

        [Test]
        public void BuildStep_TearsTheRingDown_WhenTheHouseIsBuilt()
        {
            var target = TargetLotId();
            director.Refresh();
            Assert.That(Highlights().Single().HouseId, Is.EqualTo(target), "precondition: the ring is up");

            Assert.That(state.TryBuildHouse(target), Is.True, "the target lot builds");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.Done));

            director.Refresh();

            Assert.That(Highlights(), Is.Empty,
                "the ring is torn down the instant the chain advances past BuildHouse");
        }

        [Test]
        public void BuildStep_NeverSpawnsARing_ForASaveLoadedAlreadyPastTheStep()
        {
            // A returning player whose chain is past BuildHouse gets nothing, even
            // though buildable empty lots are still all over the map. Modelled as a
            // cold start: tear down this test's director and its ring, restore the
            // later step, then stand a FRESH director up — so the assertion is
            // "never spawned", not "spawned then cleaned up".
            Object.DestroyImmediate(director.gameObject);
            foreach (var ring in Highlights())
            {
                Object.DestroyImmediate(ring.gameObject);
            }

            state.RestoreRewardChainStep(OnboardingRewardStep.Done);

            var host = new GameObject("reloaded-highlight-director-host");
            host.transform.SetParent(worldRoot.transform);
            var reloaded = host.AddComponent<OnboardingHouseHighlightDirector>();
            reloaded.Init(state, worldRoot.transform);

            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>(), Is.Not.Empty,
                "precondition: buildable empty lots remain");
            Assert.That(Highlights(), Is.Empty, "but no ring is ever spawned once past the build step");
        }

        [Test]
        public void BuildStep_RingIsColliderFree_AndDoesNotInterceptTheLotTap()
        {
            var target = TargetLotId();
            director.Refresh();

            var highlight = Highlights().Single();
            Assert.That(highlight.GetComponentsInChildren<Collider>(), Is.Empty,
                "the ring carries no collider, so it can never swallow the lot's tap");

            // And the lot's own tap still routes to the build confirmation dialog.
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().Single(v => v.HouseId == target);
            lotView.OnTapped();

            Assert.That(lotView.TapCount, Is.EqualTo(1), "the highlighted lot is still tappable");
            Assert.That(dialog.IsOpen, Is.True, "and its tap still opens the build confirmation dialog");
        }

        [Test]
        public void BuildStep_RingStaysVisible_WhileTheBuildConfirmationDialogIsOpen()
        {
            // #506 suppresses the bottom coach bar while a centered panel is open,
            // and the build flow ALWAYS opens one (#406) — so this ring must not
            // inherit that suppression, or the step loses its only cue at the exact
            // moment the player is deciding.
            var target = TargetLotId();
            director.Refresh();
            Assert.That(Highlights().Single().HouseId, Is.EqualTo(target), "precondition: the ring is up");

            worldRoot.GetComponentsInChildren<EmptyLotView>().Single(v => v.HouseId == target).OnTapped();
            Assert.That(dialog.IsOpen, Is.True, "precondition: a centered confirmation dialog is open");

            director.Refresh();

            var highlight = Highlights().SingleOrDefault();
            Assert.That(highlight, Is.Not.Null, "the ring persists while the build dialog is open");
            Assert.That(highlight.HouseId, Is.EqualTo(target));
        }

        [Test]
        public void BuildStep_RingTakesItsDiameterFromTheSharedCoreSizingRule()
        {
            // #669: one containment rule for every target ring. The foundation
            // ring must agree with the upgrade-house ring for an equal footprint,
            // rather than growing its own multiplier.
            var target = TargetLotId();
            director.Refresh();

            var foundation = worldRoot.GetComponentsInChildren<EmptyLotView>()
                .Single(v => v.HouseId == target)
                .GetComponent<Renderer>();
            var ring = Highlights().Single().GetComponentInChildren<Renderer>();

            var expected = TargetRingGeometry.OuterDiameter(
                foundation.bounds.size.x, foundation.bounds.size.z);
            Assert.That(ring.transform.lossyScale.x, Is.EqualTo(expected).Within(0.01f));
            Assert.That(ring.transform.lossyScale.z, Is.EqualTo(expected).Within(0.01f));

            // The whole foundation, corners included, sits inside the ring's hole.
            var innerRadius = 0.5f * TargetRingGeometry.InnerDiameter(ring.bounds.size.x);
            var cornerReach = 0.5f * Mathf.Sqrt(
                (foundation.bounds.size.x * foundation.bounds.size.x)
                + (foundation.bounds.size.z * foundation.bounds.size.z));
            Assert.That(innerRadius, Is.GreaterThan(cornerReach),
                "the foundation slab is fully contained by the ring's hole, with a gap");
        }
    }
}
