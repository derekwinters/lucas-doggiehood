using System.IO;
using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #453: the multi-lock map-expansion trigger. One lock per unlockable
    /// frontier coordinate; tapping an affordable lock raises the reusable
    /// ConfirmationDialog with that coordinate's cost, and confirming (Yes) calls
    /// GameState.TryUnlockTile for THAT coordinate — not the retired
    /// TryUnlockNextZone — then makes the tile's empty lots appear (wired to the
    /// #57 build path), reconciles the lock set, and saves. A grey/unaffordable
    /// lock never opens the dialog; No cancels without spending.
    /// </summary>
    public class ExpansionUnlockDirectorTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject worldRoot;
        private GameObject canvasHost;
        private GameState state;
        private ConfirmationDialog dialog;
        private ExpansionDirector buildDirector;
        private ExpansionUnlockDirector unlockDirector;
        private Sprite sprite;

        [SetUp]
        public void BuildScene()
        {
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);
            DeleteSaveIfPresent();

            // Past onboarding: the whole frontier is open, so there are several
            // simultaneous locks to tell apart (the #453 multi-lock case).
            state = FrontierEditModeWorld.WithTargetMap();
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count) + HouseBuildNumbers.BaseCost); // one unlock + one build

            worldRoot = new GameObject("world-root");

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var dialogHost = new GameObject("dialog");
            dialogHost.transform.SetParent(canvasHost.transform, false);
            dialog = dialogHost.AddComponent<ConfirmationDialog>();
            dialog.Init();

            buildDirector = new GameObject("build-director").AddComponent<ExpansionDirector>();
            buildDirector.Init(state, worldRoot.transform, dialog);

            unlockDirector = new GameObject("unlock-director").AddComponent<ExpansionUnlockDirector>();
            unlockDirector.Init(state, worldRoot.transform, dialog, buildDirector);

            // Inject a test sprite so the lock set exists even if the staged icon
            // Resource doesn't import in this EditMode run; this forces a Sync.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            unlockDirector.UseSprites(sprite, sprite);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(worldRoot);
            Object.DestroyImmediate(canvasHost);
            Object.DestroyImmediate(buildDirector.gameObject);
            Object.DestroyImmediate(unlockDirector.gameObject);
            DeleteSaveIfPresent();
        }

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, SaveStore.SaveFileName);

        private static void DeleteSaveIfPresent()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }

        private ExpansionIndicatorView LockFor(TileCoordinate coordinate)
        {
            return worldRoot.GetComponentsInChildren<ExpansionIndicatorView>()
                .Single(v => v.Coordinate.Equals(coordinate));
        }

        [Test]
        public void BuildsOneLockPerUnlockableFrontierCoordinate()
        {
            var lockCoordinates = worldRoot.GetComponentsInChildren<ExpansionIndicatorView>()
                .Select(v => v.Coordinate);

            Assert.That(lockCoordinates, Is.EquivalentTo(state.UnlockableFrontier()),
                "one lock per currently-unlockable frontier coordinate");
            Assert.That(worldRoot.GetComponentsInChildren<ExpansionIndicatorView>().Length,
                Is.GreaterThanOrEqualTo(2), "post-onboarding several frontier tiles are open at once");
        }

        [Test]
        public void TappingAnAffordableLock_OpensTheDialog_WithTheTileCostOnYes()
        {
            LockFor(FrontierEditModeWorld.FirstTile).OnTapped();

            Assert.That(dialog.IsOpen, Is.True, "an affordable lock tap raises the confirmation dialog");
            Assert.That(dialog.CostGroup.activeSelf, Is.True);
            Assert.That(dialog.CostAmountLabel.text,
                Is.EqualTo(TileUnlock.Cost(state.Map.Tiles.Count).ToString()));
        }

        [Test]
        public void ConfirmingYes_UnlocksThatMarkersCoordinate_ViaTryUnlockTile_MakesLotsAppear_AndSaves()
        {
            var coinsBefore = state.Wallet.Coins;
            var target = FrontierEditModeWorld.FirstTile;

            LockFor(target).OnTapped();
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Map.HasTileAt(target), Is.True, "the tapped marker's coordinate is placed");
            Assert.That(state.UnlockedTiles, Does.Contain(target));
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore - TileUnlock.Cost(1)),
                "the flat tile cost was spent");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Length, Is.GreaterThan(0),
                "the unlocked tile's empty lots appear in the scene");
            Assert.That(dialog.IsOpen, Is.False, "the dialog closes after confirming");
            Assert.That(File.Exists(SavePath), Is.True, "the unlock is persisted");
            Assert.That(worldRoot.GetComponentsInChildren<ExpansionIndicatorView>()
                .Any(v => v.Coordinate.Equals(target)), Is.False,
                "the unlocked coordinate's lock is destroyed");
        }

        [Test]
        public void TappingASpecificLock_UnlocksThatCoordinate_NotAnother()
        {
            // Two distinct frontier coordinates each carry their own lock; tapping
            // one unlocks exactly it. Only the first unlock is affordable, so pick
            // the east tile and confirm it's the one that lands.
            var target = new TileCoordinate(1, 0);
            var other = new TileCoordinate(-1, 0);
            Assume.That(state.UnlockableFrontier(), Does.Contain(target));
            Assume.That(state.UnlockableFrontier(), Does.Contain(other));

            LockFor(target).OnTapped();
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Map.HasTileAt(target), Is.True, "the tapped coordinate is placed");
            Assert.That(state.Map.HasTileAt(other), Is.False, "a different frontier coordinate is untouched");
        }

        [Test]
        public void TheNewEmptyLots_AreWiredToTheBuildPath()
        {
            LockFor(FrontierEditModeWorld.FirstTile).OnTapped();
            dialog.YesButton.onClick.Invoke();

            var lot = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lot.HouseId;

            lot.OnTapped();
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.True,
                "tapping a freshly-appeared lot then confirming builds a house — the new lots are wired");
        }

        [Test]
        public void ConfirmingYes_RendersTheTilesRoads_AndGrowsTheCameraPanBounds()
        {
            var cameraObject = new GameObject("camera", typeof(Camera));
            var rig = cameraObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
            var maxZBefore = rig.Controller.Bounds.MaxZ;

            LockFor(FrontierEditModeWorld.FirstTile).OnTapped();
            dialog.YesButton.onClick.Invoke();

            var tileRoad = worldRoot.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name.StartsWith(WorldBuilder.ZoneRoadNamePrefix));
            Assert.That(tileRoad, Is.Not.Null, "the unlocked tile's roads render, not only lot markers");

            Assert.That(rig.Controller.Bounds.MaxZ, Is.GreaterThan(maxZBefore),
                "the pan bounds grow to reach the just-unlocked north tile");

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TappingAGreyUnaffordableLock_DoesNotOpenTheDialog()
        {
            state.Wallet.TrySpend(state.Wallet.Coins); // drain below the cost

            LockFor(FrontierEditModeWorld.FirstTile).OnTapped();

            Assert.That(dialog.IsOpen, Is.False, "a grey lock's tap is a no-op (never opens the dialog)");
        }

        [Test]
        public void TappingNo_DismissesWithoutUnlocking()
        {
            var target = FrontierEditModeWorld.FirstTile;
            LockFor(target).OnTapped();
            dialog.NoButton.onClick.Invoke();

            Assert.That(state.Map.HasTileAt(target), Is.False, "No never unlocks");
            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void ConfirmingAfterTheBalanceDropsBelowCost_IsASafeNoOp()
        {
            var target = FrontierEditModeWorld.FirstTile;
            LockFor(target).OnTapped();               // opened while affordable
            state.Wallet.TrySpend(state.Wallet.Coins); // spent elsewhere before Yes

            dialog.YesButton.onClick.Invoke();

            Assert.That(state.Map.HasTileAt(target), Is.False,
                "Core TryUnlockTile rejects the now-unaffordable unlock; the director makes no scene changes");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Length, Is.EqualTo(0),
                "no empty lots appear when the unlock failed");
        }
    }
}
