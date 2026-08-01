using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #57/#406: tapping an empty lot's marker in an unlocked zone raises the
    /// reusable <see cref="ConfirmationDialog"/> ("Build a house here?" + the
    /// 50-coin cost on Yes) instead of spending on the bare tap; only Yes calls
    /// GameState.TryBuildHouse and, on success, swaps the marker for the real
    /// house visual and saves. No / scrim cancels with no spend. Mirrors how
    /// <see cref="ExpansionUnlockDirector"/> wraps the zone-unlock spend in the
    /// same dialog — the two expansion spends now behave consistently.
    /// </summary>
    public class ExpansionDirectorTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";
        private const string ExpectedTitle = "Build a house here?";

        private GameObject worldRoot;
        private GameObject canvasHost;
        private GameState state;
        private ConfirmationDialog dialog;
        private ExpansionDirector director;

        [SetUp]
        public void BuildWorldWithAnUnlockedZoneAndDirector()
        {
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            state = GameState.CreateNew();
            state.Wallet.Deposit(150); // 100 to unlock the first zone + 50 to build a house
            state.TryUnlockNextZone();

            worldRoot = WorldBuilder.Build(state);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var dialogHost = new GameObject("dialog");
            dialogHost.transform.SetParent(canvasHost.transform, false);
            dialog = dialogHost.AddComponent<ConfirmationDialog>();
            dialog.Init();

            var host = new GameObject("expansion-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<ExpansionDirector>();
            director.Init(state, worldRoot.transform, dialog);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(worldRoot);
            Object.DestroyImmediate(canvasHost);
        }

        [Test]
        public void TappingAnEmptyLot_OpensTheDialog_WithTheBuildCost_AndDoesNotBuildYet()
        {
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var coinsBefore = state.Wallet.Coins;

            lotView.OnTapped();

            Assert.That(dialog.IsOpen, Is.True, "an empty-lot tap raises the confirmation dialog");
            Assert.That(dialog.TitleLabel.text, Is.EqualTo(ExpectedTitle));
            Assert.That(dialog.CostGroup.activeSelf, Is.True, "the build cost is shown on Yes");
            Assert.That(dialog.CostAmountLabel.text, Is.EqualTo(HouseBuildNumbers.Cost.ToString()));

            // Nothing is spent or built until Yes.
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore), "the tap alone spends nothing");
            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.False, "no house is built on the tap");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Select(v => v.HouseId),
                Does.Contain(houseId), "the lot marker is still present");
        }

        [Test]
        public void ConfirmingYes_BuildsTheHouse_DeductsTheCost_SwapsTheMarker_AndSaves()
        {
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var coinsBefore = state.Wallet.Coins;

            lotView.OnTapped();
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore - HouseBuildNumbers.Cost));
            var house = state.Houses.SingleOrDefault(h => h.Id == houseId);
            Assert.That(house, Is.Not.Null);
            Assert.That(house.IsVacant, Is.True);
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel));

            var remainingMarkers = worldRoot.GetComponentsInChildren<EmptyLotView>();
            Assert.That(remainingMarkers.Select(v => v.HouseId).ToList(), Has.No.Member(houseId));

            var houseView = worldRoot.GetComponentsInChildren<HouseView>().SingleOrDefault(h => h.HouseId == houseId);
            Assert.That(houseView, Is.Not.Null, "the built house should get a real HouseView in the scene");
            Assert.That(dialog.IsOpen, Is.False, "the dialog closes after confirming");
        }

        [Test]
        public void ConfirmingYes_AlsoRendersTheWalkwayYardAndFence_ForTheBuiltZoneLot()
        {
            // #405/#430: a mid-game zone-lot build used to render only the house
            // mesh. After BuildHouse succeeds, ExpansionDirector now calls the
            // single-lot walkway/yard/fence helpers against the LIVE walk network
            // (#430), so the zone house gets the same treatments a starting house
            // gets at world-build time. Yard trees render for any zone lot; the
            // fence renders here with ForceFencesVisible on (the default lot is
            // unfenced). The walkway now renders too — since #430 the zone lot has
            // a real front-walkway edge in GameState.WalkNetwork, so it is no
            // longer the pre-#430 no-op.
            var originalFences = WorldBuilder.ForceFencesVisible;
            WorldBuilder.ForceFencesVisible = true;
            try
            {
                var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
                var houseId = lotView.HouseId;

                lotView.OnTapped();
                dialog.YesButton.onClick.Invoke();

                Assert.That(state.WalkNetwork.TryGetFrontWalkway(houseId, out _), Is.True,
                    "#430: the built zone house joins the live walk network with a front walkway");
                Assert.That(worldRoot.transform.Find(WorldBuilder.WalkwayNamePrefix + houseId),
                    Is.Not.Null, "the built zone house gets its front walkway rendered");
                Assert.That(worldRoot.transform.Find(WorldBuilder.YardLandscapingNamePrefix + houseId),
                    Is.Not.Null, "the built zone house gets its yard trees rendered");
                Assert.That(worldRoot.transform.Find(WorldBuilder.FenceNamePrefix + houseId),
                    Is.Not.Null, "the built zone house gets its fence rendered (forced visible)");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = originalFences;
            }
        }

        [Test]
        public void TappingNo_DismissesWithoutBuilding()
        {
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var coinsBefore = state.Wallet.Coins;

            lotView.OnTapped();
            dialog.NoButton.onClick.Invoke();

            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore), "No spends nothing");
            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.False, "No never builds");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Select(v => v.HouseId),
                Does.Contain(houseId), "the lot marker survives a cancel");
        }

        [Test]
        public void TappingTheScrim_DismissesWithoutBuilding()
        {
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var coinsBefore = state.Wallet.Coins;

            lotView.OnTapped();
            dialog.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore), "the scrim tap spends nothing");
            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.False, "the scrim tap never builds");
        }

        [Test]
        public void ConfirmingYes_WhenTheBalanceIsInsufficient_IsASafeNoOp()
        {
            state.Wallet.TrySpend(state.Wallet.Coins); // drain the wallet to 0
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;

            lotView.OnTapped();
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.False,
                "Core TryBuildHouse rejects the unaffordable build; no house is added");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Select(v => v.HouseId),
                Does.Contain(houseId), "the marker survives a failed build");
            Assert.That(worldRoot.GetComponentsInChildren<HouseView>().Any(h => h.HouseId == houseId), Is.False);
        }
    }
}
