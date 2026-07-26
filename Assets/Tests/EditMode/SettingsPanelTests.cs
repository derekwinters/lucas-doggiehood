using Doggiehood.Core.Debugging;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #219: the in-game Settings panel, built under the #256 CanvasScaler and
    /// asserted against the approved wireframe's named constants
    /// (docs/specs/ui/settings.md / mockups/settings.html, #161/#218). Covers
    /// the About tab + version display, the 10-tap Debug unlock, and the first
    /// debug toggle driving WorldBuilder.ForceFencesVisible.
    /// </summary>
    public class SettingsPanelTests
    {
        private const string TestVersion = "0.4.0-abc1234";

        private GameObject canvasHost;
        private GameObject panelHost;
        private SettingsPanel panel;
        private GameState state;
        private bool forceFencesAtStart;

        [SetUp]
        public void CreatePanel()
        {
            forceFencesAtStart = WorldBuilder.ForceFencesVisible;
            WorldBuilder.ForceFencesVisible = false;

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            state = GameState.CreateNew();

            panelHost = new GameObject("settings-panel");
            panelHost.transform.SetParent(canvasHost.transform, false);
            panel = panelHost.AddComponent<SettingsPanel>();
            panel.Init(state, TestVersion);
        }

        [TearDown]
        public void Cleanup()
        {
            WorldBuilder.ForceFencesVisible = forceFencesAtStart;
            Object.DestroyImmediate(canvasHost);
        }

        // --- wireframe constants are locked to the approved #218 values ---

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(SettingsPanel.SettingsPanelWidthPx, Is.EqualTo(1400f));
            Assert.That(SettingsPanel.SettingsPanelHeightPx, Is.EqualTo(820f));
            Assert.That(SettingsPanel.SettingsPanelPaddingPx, Is.EqualTo(48f));
            Assert.That(SettingsPanel.SidebarWidthPx, Is.EqualTo(360f));
            Assert.That(SettingsPanel.SidebarContentGapPx, Is.EqualTo(40f));
            Assert.That(SettingsPanel.TabHeightPx, Is.EqualTo(96f));
            Assert.That(SettingsPanel.TabGapPx, Is.EqualTo(16f));
            Assert.That(SettingsPanel.TabRadiusPx, Is.EqualTo(24f));
            Assert.That(SettingsPanel.VersionFontSizePx, Is.EqualTo(44));
            Assert.That(SettingsPanel.ToggleTrackWidthPx, Is.EqualTo(104f));
            Assert.That(SettingsPanel.ToggleTrackHeightPx, Is.EqualTo(56f));
            Assert.That(SettingsPanel.ToggleKnobPx, Is.EqualTo(44f));
            Assert.That(SettingsPanel.DebugRowHeightPx, Is.EqualTo(96f));
            Assert.That(SettingsPanel.CloseButtonSizePx, Is.EqualTo(72f));
        }

        // --- built UI is sized from those constants ---

        [Test]
        public void Panel_IsCenteredAtTheWireframeSize()
        {
            var rect = panel.PanelRect;

            Assert.That(rect.sizeDelta.x, Is.EqualTo(SettingsPanel.SettingsPanelWidthPx));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(SettingsPanel.SettingsPanelHeightPx));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the panel is centered over the scrim (SettingsPanelAnchor = Center)");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void Scrim_StretchesAcrossTheWholeCanvas()
        {
            Assert.That(panel.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(panel.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Sidebar_IsTheWireframeWidth()
        {
            Assert.That(panel.SidebarRect.sizeDelta.x, Is.EqualTo(SettingsPanel.SidebarWidthPx));
        }

        [Test]
        public void AboutTab_IsTheWireframeHeight()
        {
            Assert.That(panel.AboutTabRect.sizeDelta.y, Is.EqualTo(SettingsPanel.TabHeightPx));
        }

        [Test]
        public void CloseButton_IsTheWireframeSizeAndAnchoredTopRight()
        {
            Assert.That(panel.CloseButtonRect.sizeDelta.x, Is.EqualTo(SettingsPanel.CloseButtonSizePx));
            Assert.That(panel.CloseButtonRect.sizeDelta.y, Is.EqualTo(SettingsPanel.CloseButtonSizePx));
            Assert.That(panel.CloseButtonRect.anchorMin, Is.EqualTo(Vector2.one),
                "the close affordance sits at the panel's top-right corner");
            Assert.That(panel.CloseButtonRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        // --- About tab: version display ---

        [Test]
        public void AboutTab_ShowsTheBuildVersionString()
        {
            Assert.That(panel.VersionLabel.text, Is.EqualTo(TestVersion));
        }

        [Test]
        public void VersionLabel_UsesTheWireframeFontSize()
        {
            Assert.That(panel.VersionLabel.fontSize, Is.EqualTo(SettingsPanel.VersionFontSizePx));
        }

        // --- open / close ---

        [Test]
        public void Panel_StartsClosed()
        {
            Assert.That(panel.IsOpen, Is.False);
        }

        [Test]
        public void Open_ShowsThePanel_Close_HidesIt()
        {
            panel.Open();
            Assert.That(panel.IsOpen, Is.True);

            panel.Close();
            Assert.That(panel.IsOpen, Is.False);
        }

        // --- Debug unlock gesture ---

        [Test]
        public void DebugTab_IsHiddenUntilUnlocked()
        {
            Assert.That(panel.DebugTabVisible, Is.False);
        }

        [Test]
        public void TappingTheVersionTenTimesWithinTheWindow_RevealsTheDebugTab()
        {
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 0.5);
            }

            Assert.That(panel.DebugTabVisible, Is.True);
        }

        [Test]
        public void NineTaps_DoesNotRevealTheDebugTab()
        {
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock - 1; i++)
            {
                panel.TapVersion(i * 0.5);
            }

            Assert.That(panel.DebugTabVisible, Is.False);
        }

        [Test]
        public void TenTapsSpreadPastTheWindow_DoesNotRevealTheDebugTab()
        {
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 2.0);
            }

            Assert.That(panel.DebugTabVisible, Is.False);
        }

        // --- first debug toggle drives the fence seam ---

        [Test]
        public void FenceToggle_DrivesWorldBuilderForceFencesVisible()
        {
            UnlockDebug();

            Assert.That(WorldBuilder.ForceFencesVisible, Is.False);

            panel.ToggleFence();
            Assert.That(panel.FenceToggleOn, Is.True);
            Assert.That(WorldBuilder.ForceFencesVisible, Is.True,
                "flipping the toggle drives the existing WorldBuilder.ForceFencesVisible seam (#152)");

            panel.ToggleFence();
            Assert.That(panel.FenceToggleOn, Is.False);
            Assert.That(WorldBuilder.ForceFencesVisible, Is.False);
        }

        [Test]
        public void FenceToggle_RequestsAWorldRebuildSoFencesShowHideLive()
        {
            UnlockDebug();
            var rebuilds = 0;
            panel.WorldRebuild = () => rebuilds++;

            panel.ToggleFence();

            Assert.That(rebuilds, Is.EqualTo(1),
                "the live build must rebuild so the fences actually appear/disappear");
        }

        [Test]
        public void FenceToggleTrack_IsTheWireframeSize()
        {
            Assert.That(panel.FenceToggleRect.sizeDelta.x, Is.EqualTo(SettingsPanel.ToggleTrackWidthPx));
            Assert.That(panel.FenceToggleRect.sizeDelta.y, Is.EqualTo(SettingsPanel.ToggleTrackHeightPx));
        }

        // --- #286: Debug-tab "Add coins" action ---

        [Test]
        public void AddCoinsAmount_IsTheNamedConstant()
        {
            Assert.That(SettingsPanel.DebugAddCoinsAmount, Is.EqualTo(100));
        }

        [Test]
        public void DebugPane_RendersAnAddCoinsRow_BelowTheFenceRow()
        {
            Assert.That(panel.AddCoinsRowRect, Is.Not.Null,
                "the Debug pane lists an Add coins action row (#286)");
            Assert.That(panel.AddCoinsRowRect.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugRowHeightPx),
                "the action row reuses the approved debug-row height (#218)");
            Assert.That(panel.AddCoinsRowRect.anchoredPosition.y,
                Is.EqualTo(-(SettingsPanel.DebugRowHeightPx + SettingsPanel.DebugRowGapPx)),
                "it stacks one row-and-gap below the fence toggle, inventing no new layout");
        }

        [Test]
        public void AddCoinsButton_IsPresentInTheAddCoinsRow()
        {
            Assert.That(panel.AddCoinsButtonRect, Is.Not.Null);
            Assert.That(panel.AddCoinsButtonRect.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugActionHeightPx));
        }

        [Test]
        public void AddCoins_DepositsTheNamedConstantIntoTheLiveWallet()
        {
            var before = state.Wallet.Coins;

            panel.AddCoins();

            Assert.That(state.Wallet.Coins, Is.EqualTo(before + SettingsPanel.DebugAddCoinsAmount),
                "the action grants coins via the Core wallet's Deposit seam (#286)");
        }

        [Test]
        public void AddCoins_IsRepeatable_EachTapStacks()
        {
            panel.AddCoins();
            panel.AddCoins();

            Assert.That(state.Wallet.Coins,
                Is.EqualTo(2 * SettingsPanel.DebugAddCoinsAmount));
        }

        [Test]
        public void HudCurrencyChip_ReflectsTheBalanceAfterADebugDeposit()
        {
            var hudHost = new GameObject("hud");
            var hud = hudHost.AddComponent<HudOverlay>();
            hud.Init(state);
            var before = hud.Label;

            panel.AddCoins();

            Assert.That(hud.Label, Is.EqualTo(CurrencyChip.Label(state.Wallet.Coins)),
                "the HUD chip reads the live wallet, so it shows the debug payout immediately");
            Assert.That(hud.Label, Is.Not.EqualTo(before));

            Object.DestroyImmediate(hudHost);
        }

        private void UnlockDebug()
        {
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 0.2);
            }
        }
    }
}
