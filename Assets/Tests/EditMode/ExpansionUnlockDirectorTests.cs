using System.IO;
using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #343: the player-facing map-expansion trigger (Derek's Option A). Tapping
    /// the affordable lock indicator raises the reusable ConfirmationDialog with
    /// the next zone's cost; confirming (Yes) calls GameState.TryUnlockNextZone,
    /// then makes the zone's empty lots appear (wired to the #57 build path),
    /// refreshes the indicator, and saves — mirroring how ExpansionDirector
    /// wires EmptyLotView → TryBuildHouse. A grey/unaffordable lock never opens
    /// the dialog; No/scrim cancels without spending.
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
        private ExpansionIndicatorView indicator;

        [SetUp]
        public void BuildScene()
        {
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);
            DeleteSaveIfPresent();

            state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost + HouseBuildNumbers.Cost); // unlock + one build

            worldRoot = new GameObject("world-root");

            // The lock indicator, built manually (no dependency on the staged
            // icon resource) — same host shape WorldBuilder gives it.
            var indicatorHost = new GameObject("ExpansionIndicator");
            indicatorHost.transform.SetParent(worldRoot.transform);
            indicatorHost.AddComponent<SpriteRenderer>();
            indicator = indicatorHost.AddComponent<ExpansionIndicatorView>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            indicator.Init(state, sprite, sprite);

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

        [Test]
        public void TappingTheAffordableLock_OpensTheDialog_WithTheNextZoneCostOnYes()
        {
            indicator.OnTapped();

            Assert.That(dialog.IsOpen, Is.True, "an affordable lock tap raises the confirmation dialog");
            Assert.That(dialog.CostGroup.activeSelf, Is.True);
            Assert.That(dialog.CostAmountLabel.text, Is.EqualTo(ZoneUnlockNumbers.BaseCost.ToString()));
        }

        [Test]
        public void ConfirmingYes_UnlocksTheZone_SpendsTheCoins_MakesLotsAppear_AndSaves()
        {
            var coinsBefore = state.Wallet.Coins;

            indicator.OnTapped();
            dialog.YesButton.onClick.Invoke();

            Assert.That(state.UnlockedZones.Count, Is.EqualTo(1), "the next zone is unlocked");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore - ZoneUnlockNumbers.BaseCost),
                "the unlock cost was spent");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Length, Is.GreaterThan(0),
                "the unlocked zone's empty lots appear in the scene");
            Assert.That(dialog.IsOpen, Is.False, "the dialog closes after confirming");
            Assert.That(File.Exists(SavePath), Is.True, "the unlock is persisted");
            Assert.That(indicator.GetComponent<SpriteRenderer>().enabled, Is.False,
                "with the only authored zone unlocked, the lock indicator hides itself");
        }

        [Test]
        public void TheNewEmptyLots_AreWiredToTheBuildPath()
        {
            indicator.OnTapped();
            dialog.YesButton.onClick.Invoke();

            var lot = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
            var houseId = lot.HouseId;

            lot.OnTapped(); // 50 coins remain — the build should succeed

            Assert.That(state.Houses.Any(h => h.Id == houseId), Is.True,
                "tapping a freshly-appeared lot builds a house — the new lots are wired");
        }

        [Test]
        public void ConfirmingYes_RendersTheZonesRoads_AndGrowsTheCameraPanBounds()
        {
            // #373: the two gaps together — a confirmed unlock renders the new
            // zone's road surfaces (derived from GameState.Map, not just lot
            // markers) and grows the live camera rig's pan bounds north so the
            // player can pan over to the just-revealed zone.
            var cameraObject = new GameObject("camera", typeof(Camera));
            var rig = cameraObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
            var maxZBefore = rig.Controller.Bounds.MaxZ;

            indicator.OnTapped();
            dialog.YesButton.onClick.Invoke();

            var zoneRoad = worldRoot.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name.StartsWith(WorldBuilder.ZoneRoadNamePrefix));
            Assert.That(zoneRoad, Is.Not.Null, "the unlocked zone's roads render, not only lot markers");

            var northLotZ = ZoneCatalog.FirstZone.Lots.Max(lot => lot.Position.Z);
            Assert.That(rig.Controller.Bounds.MaxZ, Is.GreaterThan(maxZBefore),
                "the pan bounds grow north on unlock");
            Assert.That(rig.Controller.Bounds.MaxZ, Is.GreaterThanOrEqualTo(northLotZ),
                "the grown bounds reach the new zone's northernmost lot");

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TappingAGreyUnaffordableLock_DoesNotOpenTheDialog()
        {
            state.Wallet.TrySpend(state.Wallet.Coins); // drain below the cost

            indicator.OnTapped();

            Assert.That(dialog.IsOpen, Is.False, "a grey lock's tap is a no-op (never opens the dialog)");
        }

        [Test]
        public void TappingNo_DismissesWithoutUnlocking()
        {
            indicator.OnTapped();
            dialog.NoButton.onClick.Invoke();

            Assert.That(state.UnlockedZones.Count, Is.EqualTo(0), "No never unlocks");
            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void ConfirmingAfterTheBalanceDropsBelowCost_IsASafeNoOp()
        {
            indicator.OnTapped();               // opened while affordable
            state.Wallet.TrySpend(state.Wallet.Coins); // spent elsewhere before Yes

            dialog.YesButton.onClick.Invoke();

            Assert.That(state.UnlockedZones.Count, Is.EqualTo(0),
                "Core TryUnlockNextZone rejects the now-unaffordable unlock; the director makes no scene changes");
            Assert.That(worldRoot.GetComponentsInChildren<EmptyLotView>().Length, Is.EqualTo(0),
                "no empty lots appear when the unlock failed");
        }
    }
}
