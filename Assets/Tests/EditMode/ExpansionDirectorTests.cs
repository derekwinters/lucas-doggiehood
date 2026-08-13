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
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count)
                + HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));  // the live unlock + build prices
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);

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
            Assert.That(dialog.CostAmountLabel.text, Is.EqualTo(HouseBuildNumbers.BaseCost.ToString()));

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

            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore - HouseBuildNumbers.BaseCost));
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
        public void ConfirmingYes_DoesNotDuplicateTheAlreadyPlacedYardTrees()
        {
            // #434: an empty lot's yard trees are placed when the zone unlocks
            // (WorldBuilder.Build -> BuildEmptyLots), so building the house must
            // swap ONLY the foundation slab for the house mesh — the trees are
            // already there and must not be re-instantiated. Pick a lot whose
            // yard actually selects trees, then assert exactly one Yard - N
            // container survives the build.
            var treedLotView = worldRoot.GetComponentsInChildren<EmptyLotView>()
                .First(v => YardLandscaping.FrontTreesFor(state.GetHouseLot(v.HouseId))
                    .Concat(YardLandscaping.BackTreesFor(state.GetHouseLot(v.HouseId))).Any());
            var houseId = treedLotView.HouseId;

            var containerName = WorldBuilder.YardLandscapingNamePrefix + houseId;
            var before = worldRoot.transform.Cast<Transform>().Count(t => t.name == containerName);
            Assert.That(before, Is.EqualTo(1), "the empty lot already carries its trees from unlock");

            treedLotView.OnTapped();
            dialog.YesButton.onClick.Invoke();

            var after = worldRoot.transform.Cast<Transform>().Count(t => t.name == containerName);
            Assert.That(after, Is.EqualTo(1), "building the house must not duplicate the yard trees");
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
            Assert.That(state.Wallet.Coins, Is.EqualTo(0),
                "#690 defence in depth: a disabled Yes that is somehow invoked still spends nothing");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Select(v => v.HouseId),
                Does.Contain(houseId), "the marker survives a failed build");
            Assert.That(worldRoot.GetComponentsInChildren<HouseView>().Any(h => h.HouseId == houseId), Is.False);
        }

        [Test]
        public void TappingALotThePlayerCannotAfford_OpensTheDialog_WithYesGreyedOutAndUnpressable()
        {
            // #690: the reported bug — an unaffordable build used to offer a
            // normal, pressable Yes that silently did nothing.
            DrainWalletToJustBelowTheBuildCost();
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();

            lotView.OnTapped();

            Assert.That(dialog.IsOpen, Is.True,
                "the dialog still opens so the player can see the price");
            Assert.That(dialog.YesButton.interactable, Is.False, "Yes is not pressable");
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Disabled,
                "Yes greys out like an unaffordable quest pill");
        }

        [Test]
        public void TappingALotThePlayerCanAfford_OpensTheDialog_WithALiveLeafYes()
        {
            // Regression guard: an affordable build is completely unchanged.
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();

            lotView.OnTapped();

            Assert.That(dialog.YesButton.interactable, Is.True);
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Leaf,
                "an affordable build keeps the positive/leaf confirm");
        }

        [Test]
        public void TheUnaffordableDialog_StillShowsTheCostThePlayerIsShortOf()
        {
            DrainWalletToJustBelowTheBuildCost();
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();

            lotView.OnTapped();

            Assert.That(dialog.CostGroup.activeSelf, Is.True);
            Assert.That(dialog.CostAmountLabel.text,
                Is.EqualTo(HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount).ToString()));
        }

        [Test]
        public void TheUnaffordableDialog_IsNeverATrap_NoAndTheScrimStillDismiss()
        {
            // #329 guard: greying Yes must never grey the way out.
            DrainWalletToJustBelowTheBuildCost();
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();

            lotView.OnTapped();
            dialog.NoButton.onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.False, "No dismisses the unaffordable prompt");

            lotView.OnTapped();
            dialog.ScrimRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.False, "the scrim dismisses the unaffordable prompt");
        }

        [Test]
        public void InvokingConfirmWhileYesIsDisabled_BuildsNothing_AndLeavesTheWalletUntouched()
        {
            // #690 defence in depth: a disabled button must not merely LOOK
            // disabled — Core stays the sole authority on the spend.
            DrainWalletToJustBelowTheBuildCost();
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var coinsBefore = state.Wallet.Coins;

            lotView.OnTapped();
            Assert.That(dialog.YesButton.interactable, Is.False);
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.False, "no house is built");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore), "the wallet is untouched");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Select(v => v.HouseId),
                Does.Contain(houseId), "the foundation slab survives");
        }

        [Test]
        public void TheYesEnabledState_TracksCoresHouseBuildOfferIsAffordable_AtEveryBalance()
        {
            // #690 guard: the dialog's enabled state comes from Core's answer,
            // not a Unity-side wallet comparison. Sweeping the balance across the
            // exact boundary (cost - 1, cost, cost + 1) pins the two together.
            var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lotView.HouseId;
            var cost = HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount);

            foreach (var balance in new[] { 0, cost - 1, cost, cost + 1 })
            {
                SetWallet(balance);
                var expected = HouseBuildOffer.Resolve(state, houseId).Value.IsAffordable;

                lotView.OnTapped();

                Assert.That(dialog.YesButton.interactable, Is.EqualTo(expected),
                    "Yes must agree with HouseBuildOffer.IsAffordable at a balance of " + balance);
                AssertColor(dialog.YesButtonImage.color,
                    expected ? CandyChromeUgui.Leaf : CandyChromeUgui.Disabled,
                    "the Yes tint must follow Core's affordability at a balance of " + balance);

                dialog.Cancel();
            }
        }

        /// <summary>Leaves the wallet one coin short of the current build cost —
        /// the exact boundary the bug lives on.</summary>
        private void DrainWalletToJustBelowTheBuildCost()
        {
            SetWallet(HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount) - 1);
        }

        private void SetWallet(int coins)
        {
            state.Wallet.TrySpend(state.Wallet.Coins);
            state.Wallet.Deposit(coins);
        }

        private static void AssertColor(Color actual, Color expected, string what)
        {
            var a = (Color32)actual;
            var e = (Color32)expected;
            Assert.That(a.r, Is.EqualTo(e.r), what + " red");
            Assert.That(a.g, Is.EqualTo(e.g), what + " green");
            Assert.That(a.b, Is.EqualTo(e.b), what + " blue");
        }
    }
}
