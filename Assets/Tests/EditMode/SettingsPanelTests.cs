using System;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
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
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject panelHost;
        private SettingsPanel panel;
        private GameState state;
        private bool forceFencesAtStart;
        private bool showDebugColorsAtStart;

        [SetUp]
        public void CreatePanel()
        {
            // #291: the panel binds its bundled UI font via Resources.Load in
            // Build(); force-import it so a fresh CI Library resolves it before
            // the panel is constructed (docs/engineering/unity-serialization.md §4).
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            forceFencesAtStart = WorldBuilder.ForceFencesVisible;
            WorldBuilder.ForceFencesVisible = false;
            showDebugColorsAtStart = WorldBuilder.ShowDebugElementColors;
            WorldBuilder.ShowDebugElementColors = false;

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
            WorldBuilder.ShowDebugElementColors = showDebugColorsAtStart;
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

        // --- #467: the tab-to-tab vertical gap and the tab-label stretch ---

        [Test]
        public void DebugTab_SitsOneTabAndGapBelowTheAboutTab()
        {
            Assert.That(panel.DebugTabRect.anchoredPosition.y,
                Is.EqualTo(panel.AboutTabRect.anchoredPosition.y
                    - (SettingsPanel.TabHeightPx + SettingsPanel.TabGapPx)),
                "the Debug tab pill stacks exactly one tab-height-and-gap below About (#467)");
        }

        [Test]
        public void SidebarTabLabels_AreStretchedToTheirTab_NotTheZeroSizeDefault()
        {
            foreach (var tab in new[] { panel.AboutTabRect, panel.DebugTabRect })
            {
                var label = tab.Find("Label") as RectTransform;
                Assert.That(label, Is.Not.Null, tab.name + " has no Label child");
                Assert.That(label.anchorMin, Is.EqualTo(Vector2.zero),
                    tab.name + " label must stretch to the full tab, not the un-stretched (0,0) default (#467)");
                Assert.That(label.anchorMax, Is.EqualTo(Vector2.one),
                    tab.name + " label must stretch to the full tab, not the un-stretched (0,0) default (#467)");
            }
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

        // --- #328: About-pane copy (tagline reads "Designed by Lucas"; no credit line) ---

        [Test]
        public void AboutPane_TaglineReadsDesignedByLucas()
        {
            var tagline = FindAboutLabel("Tagline");

            Assert.That(tagline, Is.Not.Null, "the About pane still shows a tagline line");
            Assert.That(tagline.text, Is.EqualTo("Designed by Lucas"),
                "the About-pane tagline reads the #328 copy");
        }

        [Test]
        public void AboutPane_HasNoCreditLine()
        {
            Assert.That(FindAboutLabel("Credits"), Is.Null,
                "the 'Made by Derek & Lucas' credit line was dropped (#328)");

            foreach (var label in panel.AboutPaneRect.GetComponentsInChildren<Text>(true))
            {
                Assert.That(label.text, Does.Not.Contain("Made by"),
                    "no About-pane label carries the removed 'Made by …' credit copy (#328)");
            }
        }

        private Text FindAboutLabel(string name)
        {
            foreach (var label in panel.AboutPaneRect.GetComponentsInChildren<Text>(true))
            {
                if (label.gameObject.name == name)
                {
                    return label;
                }
            }

            return null;
        }

        // --- #291: labels use a bundled font, not an Editor-only built-in lookup ---

        [Test]
        public void Labels_UseTheBundledFont_NotAnEditorOnlyBuiltinLookup()
        {
            var font = panel.VersionLabel.font;

            Assert.That(font, Is.Not.Null,
                "the version label has no font — it would draw nothing in the Android build (#291)");
            Assert.That(font.name, Does.Contain("DejaVu"),
                "labels must use the bundled DejaVu font; Resources.GetBuiltinResource " +
                "(Arial/LegacyRuntime) is Editor-only and gets stripped from the build (#291)");
            Assert.That(font.name, Does.Not.Contain("Arial"));
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
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

        // --- #457: Debug-tab "Refresh quests now" action ---

        [Test]
        public void DebugPane_RendersARefreshQuestsRow_BelowTheAddCoinsRow()
        {
            Assert.That(panel.RefreshQuestsRowRect, Is.Not.Null,
                "the Debug pane lists a Refresh quests now action row (#457)");
            Assert.That(panel.RefreshQuestsRowRect.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugRowHeightPx),
                "the action row reuses the approved debug-row height — no new constant (#218)");
            Assert.That(panel.RefreshQuestsRowRect.anchoredPosition.y,
                Is.EqualTo(-2f * (SettingsPanel.DebugRowHeightPx + SettingsPanel.DebugRowGapPx)),
                "it stacks two rows-and-gaps below the fence toggle, inventing no new layout");
        }

        [Test]
        public void RefreshQuestsButton_IsPresent_SizedFromTheExistingActionConstants()
        {
            Assert.That(panel.RefreshQuestsButtonRect, Is.Not.Null);
            Assert.That(panel.RefreshQuestsButtonRect.sizeDelta.x, Is.EqualTo(SettingsPanel.DebugActionWidthPx));
            Assert.That(panel.RefreshQuestsButtonRect.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugActionHeightPx));
        }

        [Test]
        public void RefreshQuestsRow_IsGatedBehindTheDebugUnlock_LikeTheOtherRows()
        {
            // The row lives in the same Debug pane as Add coins, so it is
            // unreachable until the Debug tab is unlocked and selected.
            Assert.That(panel.RefreshQuestsRowRect.parent, Is.EqualTo(panel.AddCoinsRowRect.parent),
                "the refresh row shares the Debug pane, so it is gated identically");
            Assert.That(panel.RefreshQuestsRowRect.gameObject.activeInHierarchy, Is.False,
                "it stays hidden until the Debug tab is unlocked, like the existing rows");
        }

        [Test]
        public void RefreshQuests_ForcesAQuestRotation_ViaTheCoreSeam()
        {
            // #543/#624: quests trickle in hourly (target/4 per hour), so top the
            // roster up to a 1.5/hr population (18 dogs -> target 6) to make a
            // single forced tick add a whole quest deterministically.
            for (var i = state.Dogs.Count; i < 18; i++)
            {
                state.AddDog(new Doggiehood.Core.Dogs.Dog(
                    $"extra-{i}", Doggiehood.Core.Dogs.Breed.GermanShepherd,
                    Doggiehood.Core.Dogs.Personality.Brave, 1, false));
            }

            Assert.That(state.LastRotationUtc, Is.Null, "precondition: no rotation has happened yet");
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0), "precondition: no active quests yet");

            var before = System.DateTime.UtcNow;
            panel.RefreshQuests();
            var after = System.DateTime.UtcNow;

            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(0),
                "tapping the action forces a new-quest top-up through QuestManager.ForceRefresh (#457)");
            Assert.That(state.LastRotationUtc, Is.Not.Null,
                "the forced refresh records its instant, restarting the hourly window");
            Assert.That(state.LastRotationUtc.Value, Is.InRange(before, after),
                "it passes DateTime.UtcNow to the Core seam exactly once");
        }

        // --- #611: Debug-tab "Show debug element colors" toggle ---

        [Test]
        public void DebugPane_RendersADebugColorsRow_BelowTheRefreshQuestsRow()
        {
            // #611: the fourth Debug-tab row, stacked one row-and-gap below the
            // Refresh quests row using the existing DebugRowHeightPx/DebugRowGapPx
            // constants — no invented layout values.
            Assert.That(panel.DebugColorsRowRect, Is.Not.Null,
                "the Debug pane lists a Show debug element colors toggle row (#611)");
            Assert.That(panel.DebugColorsRowRect.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugRowHeightPx),
                "the row reuses the approved debug-row height — no new constant");
            Assert.That(panel.DebugColorsRowRect.anchoredPosition.y,
                Is.EqualTo(-3f * (SettingsPanel.DebugRowHeightPx + SettingsPanel.DebugRowGapPx)),
                "it stacks three rows-and-gaps below the fence toggle, inventing no new layout");
        }

        [Test]
        public void DebugColorsToggle_IsRegistered_InTheDebugToggleRegistry()
        {
            Assert.That(panel.Toggles.Contains(SettingsPanel.DebugColorsToggleKey), Is.True,
                "the toggle plugs into the shared Core DebugToggleRegistry (#219), like the fence switch");
        }

        [Test]
        public void DebugColorsToggleTrack_IsTheWireframeSize()
        {
            Assert.That(panel.DebugColorsToggleRect.sizeDelta.x, Is.EqualTo(SettingsPanel.ToggleTrackWidthPx));
            Assert.That(panel.DebugColorsToggleRect.sizeDelta.y, Is.EqualTo(SettingsPanel.ToggleTrackHeightPx));
        }

        [Test]
        public void DebugColorsToggle_DrivesWorldBuilderShowDebugElementColors()
        {
            UnlockDebug();

            Assert.That(WorldBuilder.ShowDebugElementColors, Is.False);

            panel.ToggleDebugColors();
            Assert.That(panel.DebugColorsToggleOn, Is.True);
            Assert.That(WorldBuilder.ShowDebugElementColors, Is.True,
                "flipping the toggle drives the WorldBuilder.ShowDebugElementColors seam (#611)");

            panel.ToggleDebugColors();
            Assert.That(panel.DebugColorsToggleOn, Is.False);
            Assert.That(WorldBuilder.ShowDebugElementColors, Is.False);
        }

        [Test]
        public void DebugColorsToggle_RequestsARefresh_SoTheColorSwapIsLive()
        {
            UnlockDebug();
            var refreshes = 0;
            panel.DebugColorsRefresh = () => refreshes++;

            panel.ToggleDebugColors();

            Assert.That(refreshes, Is.EqualTo(1),
                "the live build must repaint the ground and reconfigure the camera so the colours actually swap");
        }

        [Test]
        public void DebugColorsRow_IsGatedBehindTheDebugUnlock_LikeTheOtherRows()
        {
            Assert.That(panel.DebugColorsRowRect.parent, Is.EqualTo(panel.RefreshQuestsRowRect.parent),
                "the debug-colors row shares the Debug pane, so it is gated identically");
            Assert.That(panel.DebugColorsRowRect.gameObject.activeInHierarchy, Is.False,
                "it stays hidden until the Debug tab is unlocked, like the existing rows");
        }

        // --- #298: Candy Cottage chrome restyle (shared-components.md via CandyChromeUgui) ---

        [Test]
        public void Panel_HasCandyCottageChrome_FillOutlineRadiusAndHardShadow()
        {
            var image = panel.PanelRect.GetComponent<Image>();

            AssertHex(image.color, 0xFF, 0xFD, 0xF7, "panel fill (#FFFDF7)");
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.sprite.border,
                Is.EqualTo(new Vector4(CandyChromeUgui.PanelRadiusPx, CandyChromeUgui.PanelRadiusPx,
                    CandyChromeUgui.PanelRadiusPx, CandyChromeUgui.PanelRadiusPx)),
                "the panel corner radius is the shared PanelRadiusPx = 40");

            AssertInkOutline(panel.PanelRect.gameObject);
            AssertHardShadow(panel.PanelRect.gameObject);
        }

        [Test]
        public void CloseButton_HasCandyCottageChrome_OutlineAndHardShadow()
        {
            AssertInkOutline(panel.CloseButtonRect.gameObject);
            AssertHardShadow(panel.CloseButtonRect.gameObject);
            AssertHex(panel.CloseButtonRect.GetComponent<Image>().color, 0xFF, 0xF3, 0xD9,
                "the close affordance is a cream pill");
        }

        [Test]
        public void SidebarTabs_ArePillStyled_WithRoleTintsAndInkOutline()
        {
            // Built default: About is the active tab (Coral), Debug inactive (Cream).
            AssertHex(panel.AboutTabRect.GetComponent<Image>().color, 0xFF, 0x7A, 0x5C,
                "the active tab takes the primary Coral role tint");
            AssertHex(panel.DebugTabRect.GetComponent<Image>().color, 0xFF, 0xF3, 0xD9,
                "an inactive tab takes the neutral Cream role tint");

            AssertInkOutline(panel.AboutTabRect.gameObject);
            AssertInkOutline(panel.DebugTabRect.gameObject);
        }

        [Test]
        public void AddCoinsAction_IsAGoldPill_WithOutlineAndHardShadow()
        {
            AssertHex(panel.AddCoinsButtonRect.GetComponent<Image>().color, 0xFF, 0xC2, 0x3C,
                "the Add coins action is a Gold #FFC23C pill (#286/#298)");
            AssertInkOutline(panel.AddCoinsButtonRect.gameObject);
            AssertHardShadow(panel.AddCoinsButtonRect.gameObject);
        }

        [Test]
        public void RefreshQuestsAction_IsAGoldPill_WithOutlineAndHardShadow()
        {
            AssertHex(panel.RefreshQuestsButtonRect.GetComponent<Image>().color, 0xFF, 0xC2, 0x3C,
                "the Refresh quests action is a Gold #FFC23C pill, styled like Add coins (#457/#298)");
            AssertInkOutline(panel.RefreshQuestsButtonRect.gameObject);
            AssertHardShadow(panel.RefreshQuestsButtonRect.gameObject);
        }

        [Test]
        public void FenceSwitch_FollowsCandyCottageSwitchStyling_OutlineNoShadow()
        {
            // Track + knob carry the Ink outline but no drop-shadow (wireframe switch).
            AssertInkOutline(panel.FenceToggleRect.gameObject);
            AssertInkOutline(panel.FenceKnobRect.gameObject);
            Assert.That(PureShadowOf(panel.FenceToggleRect.gameObject), Is.Null,
                "the switch track has no drop-shadow, matching the mockup toggle");

            AssertHex(panel.FenceToggleRect.GetComponent<Image>().color, 0xD8, 0xD2, 0xC6,
                "the switch reads Disabled grey when off (default)");

            UnlockDebug();
            panel.ToggleFence();
            AssertHex(panel.FenceToggleRect.GetComponent<Image>().color, 0x58, 0xC0, 0x6A,
                "the switch reads Leaf green when on");
        }

        [Test]
        public void AllChromeImages_UseTheDefaultUiMaterial_DeviceSafeNoStrippedShader()
        {
            // #291 by construction: no chrome Image assigns a custom material, so
            // each renders through the always-included UI/Default material.
            foreach (var image in panel.PanelRect.GetComponentsInChildren<Image>(true))
            {
                Assert.That(image.material, Is.EqualTo(image.defaultMaterial),
                    image.name + " must render through the always-included UI/Default material (#291)");
            }

            Assert.That(panel.ScrimRect.GetComponent<Image>().material,
                Is.EqualTo(panel.ScrimRect.GetComponent<Image>().defaultMaterial));
        }

        private static void AssertInkOutline(GameObject go)
        {
            // #616: the outline is a constant-width Ink contour band (an inflated
            // rounded-sprite Image drawn behind the fill), NOT the offset-copy
            // Outline mesh effect that produced the uneven corners.
            Assert.That(go.GetComponent<Outline>(), Is.Null,
                go.name + " still uses the offset-copy Outline mesh effect (#616)");

            var ink = CandyChromeUgui.OutlineInk(go);
            Assert.That(ink, Is.Not.Null, go.name + " has no Ink contour-band underlay");
            AssertHex(ink.color, 0x2E, 0x2A, 0x26, go.name + " outline");
            Assert.That(ink.raycastTarget, Is.False, go.name + " outline must not intercept taps");
            Assert.That(ink.material, Is.EqualTo(ink.defaultMaterial),
                go.name + " outline must render through the default UI material (device-safe)");

            var fillRt = go.GetComponent<RectTransform>();
            var inkRt = ink.rectTransform;
            Assert.That(inkRt.GetSiblingIndex(), Is.LessThan(fillRt.GetSiblingIndex()),
                go.name + " outline band must render behind the fill");
            var w = CandyChromeUgui.OutlineThicknessPx;
            Assert.That(fillRt.offsetMin.x - inkRt.offsetMin.x, Is.EqualTo(w).Within(0.01f),
                go.name + " outline band width is not the shared OutlineThicknessPx = 6");
            Assert.That(fillRt.offsetMin.y - inkRt.offsetMin.y, Is.EqualTo(w).Within(0.01f));
            Assert.That(inkRt.offsetMax.x - fillRt.offsetMax.x, Is.EqualTo(w).Within(0.01f));
            Assert.That(inkRt.offsetMax.y - fillRt.offsetMax.y, Is.EqualTo(w).Within(0.01f));
        }

        private static void AssertHardShadow(GameObject go)
        {
            var shadow = PureShadowOf(go);
            Assert.That(shadow, Is.Not.Null, go.name + " has no hard drop-shadow");
            AssertHex(shadow.effectColor, 0x2E, 0x2A, 0x26, go.name + " shadow");
            Assert.That(shadow.effectDistance, Is.EqualTo(new Vector2(0f, -CandyChromeUgui.ShadowOffsetPx)),
                go.name + " shadow is not a single hard offset at the shared ShadowOffsetPx = 8 (no blur)");
        }

        private static void AssertHex(Color color, byte r, byte g, byte b, string what)
        {
            var c32 = (Color32)color;
            Assert.That(c32.r, Is.EqualTo(r), what + " red channel");
            Assert.That(c32.g, Is.EqualTo(g), what + " green channel");
            Assert.That(c32.b, Is.EqualTo(b), what + " blue channel");
        }

        private static Shadow PureShadowOf(GameObject go)
        {
            foreach (var shadow in go.GetComponents<Shadow>())
            {
                if (shadow.GetType() == typeof(Shadow))
                {
                    return shadow;
                }
            }

            return null;
        }

        private void UnlockDebug()
        {
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 0.2);
            }
        }

        // ---------------------------------------------------------------
        // #622/#656: the "Tune balance…" Debug-tab entry row. It ships in
        // EVERY build (development, release-candidate and release alike) and
        // is gated by the 10-tap Debug unlock alone — the same gate as the
        // rest of the Debug tab, with no build-configuration gate on top
        // (docs/specs/ui/debug-tuning-menu.md, docs/specs/ui/settings.md).
        // ---------------------------------------------------------------

        [Test]
        public void TuneBalanceRow_IsTheFifthDebugRowAtTheSharedRowMetrics()
        {
            UnlockDebug();

            Assert.That(panel.TuneBalanceRowRect, Is.Not.Null);
            Assert.That(panel.TuneBalanceRowRect.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugRowHeightPx));
            Assert.That(panel.TuneBalanceButtonRect.sizeDelta,
                Is.EqualTo(new Vector2(SettingsPanel.DebugActionWidthPx, SettingsPanel.DebugActionHeightPx)),
                "the entry row is a Debug action row like Add coins / Refresh quests now");

            // Stacked one row below the #611 debug-colors switch (order 4).
            Assert.That(panel.TuneBalanceRowRect.anchoredPosition.y,
                Is.LessThan(panel.DebugColorsRowRect.anchoredPosition.y));
        }

        [Test]
        public void TuneBalanceRow_RaisesTheOpenRequest()
        {
            UnlockDebug();
            var opened = 0;
            panel.TuneBalanceRequested = () => opened++;

            panel.TuneBalanceButtonRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(opened, Is.EqualTo(1));
        }

        [Test]
        public void TuneBalanceRow_IsBuiltWithNoDevBuildFlagInvolved()
        {
            // #656: the panel is built by the one and only Init(state, version)
            // — the same call every other Debug row goes through — and the row
            // is there. Nothing about the build configuration is consulted.
            Assert.That(panel.TuneBalanceRowRect, Is.Not.Null);
            Assert.That(panel.TuneBalanceButtonRect, Is.Not.Null);
            Assert.That(
                panelHost.GetComponentsInChildren<Text>(true).Any(t => t.text.StartsWith("Tune balance")),
                Is.True);
        }

        [Test]
        public void SettingsPanel_ExposesNoDevBuildGateOnItsApi()
        {
            // #656 retires the injected dev-build flag entirely: one Init
            // overload, and no DevBuild* member left implying a rule the
            // project no longer has.
            var type = typeof(SettingsPanel);

            Assert.That(
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Count(m => m.Name == nameof(SettingsPanel.Init)),
                Is.EqualTo(1),
                "the Init(state, version, devBuild) overload is retired");

            Assert.That(
                type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Select(m => m.Name)
                    .Where(n => n.IndexOf("DevBuild", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray(),
                Is.Empty,
                "no dev-build member survives on the Settings panel");
        }

        [Test]
        public void TuneBalanceRow_IsUnreachableUntilTheTenTapUnlock()
        {
            // #656: with the dev-build gate gone, the 10-tap unlock is the ONLY
            // gate on the tuning menu in a shipping build, so it has to
            // genuinely hold. Open the panel and prove nothing an untrained tap
            // can reach leads to the row.
            panel.Open();

            Assert.That(panel.DebugTabVisible, Is.False, "the Debug tab starts locked");
            Assert.That(panel.TuneBalanceRowRect.gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.TuneBalanceRowRect.parent, Is.EqualTo(panel.DebugColorsRowRect.parent),
                "it shares the Debug pane, so it is gated identically to the other rows");

            // Nothing live in the open panel is the entry row, and nothing live
            // is the Debug tab that would reveal it.
            var live = panelHost.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(live, Does.Not.Contain(panel.TuneBalanceButtonRect.GetComponent<Button>()),
                "the entry pill must not be tappable before the unlock");
            Assert.That(live.Any(b => b.gameObject == panel.DebugTabRect.gameObject), Is.False,
                "the Debug tab is hidden too, so the gesture is the only way in");

            // One tap short of the gesture still gets you nowhere.
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock - 1; i++)
            {
                panel.TapVersion(i * 0.2);
            }

            Assert.That(panel.DebugTabVisible, Is.False);
            Assert.That(panel.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void TuneBalanceRow_BecomesReachableOnlyAfterTheUnlockRevealsTheDebugTab()
        {
            panel.Open();
            UnlockDebug();

            // Revealed, but still not on screen until the Debug tab is selected.
            Assert.That(panel.DebugTabVisible, Is.True);
            Assert.That(panel.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.False);

            panel.DebugTabRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(panel.TuneBalanceRowRect.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void OpeningTheTuningMenu_LeavesSettingsOpenOnTheDebugTab()
        {
            // The tuning panel layers OVER Settings; it does not replace it
            // (docs/specs/ui/debug-tuning-menu.md, "layer, don't replace").
            UnlockDebug();
            panel.Open();
            panel.TuneBalanceRequested = () => { };

            panel.TuneBalanceButtonRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(panel.IsOpen, Is.True);
            Assert.That(panel.DebugTabVisible, Is.True);
        }
    }
}
