using System;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The in-game Settings panel (#219): a centered panel over a dim scrim
    /// with a sidebar tab rail and a content pane, opened from the HUD gear.
    /// The About tab shows the build version (also the 10-tap Debug unlock
    /// target); the Debug tab is hidden until unlocked and hosts on-device
    /// toggles — the first drives the existing
    /// <see cref="WorldBuilder.ForceFencesVisible"/> seam (#152).
    ///
    /// This is thin wiring: the unlock gesture and the toggle registry are
    /// engine-free <see cref="Doggiehood.Core.Debugging"/> Core logic, and all
    /// layout numbers are the approved wireframe's named constants
    /// (docs/specs/ui/settings.md, #161/#218), asserted by EditMode tests.
    /// Built under the #256 <see cref="UiCanvas"/> CanvasScaler so each px
    /// constant keeps a fixed on-screen meaning across tablet sizes.
    ///
    /// Its chrome is the shared "Candy Cottage" direction (#298/#65): the panel,
    /// tabs, close button, debug rows, add-coins action and switch are drawn by
    /// <see cref="CandyChromeUgui"/> — thick Ink outlines, flat hard
    /// drop-shadows, rounded/pill shapes and the shared palette
    /// (docs/specs/ui/shared-components.md) — with the wireframe layout unchanged.
    /// The chrome is procedural and device-safe (no raster art; only the
    /// always-included <c>UI/Default</c> shader plus the bundled font, #291).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        // --- Layout constants from the approved #218 wireframe ---
        public const float SettingsPanelWidthPx = 1400f;
        public const float SettingsPanelHeightPx = 820f;
        public const float SettingsPanelPaddingPx = 48f;
        public const float SidebarWidthPx = 360f;
        public const float SidebarContentGapPx = 40f;
        public const float TabHeightPx = 96f;
        public const float TabGapPx = 16f;
        public const float TabRadiusPx = 24f;
        public const int VersionFontSizePx = 44;
        public const float ToggleTrackWidthPx = 104f;
        public const float ToggleTrackHeightPx = 56f;
        public const float ToggleKnobPx = 44f;
        public const float DebugRowHeightPx = 96f;
        public const float CloseButtonSizePx = 72f;

        // --- #286: the Debug-tab "Add coins" action ---
        // Grant amount is a named constant (#161); the wireframe's single
        // gold action is "+100" (docs/specs/ui/settings.md mockup).
        public const int DebugAddCoinsAmount = 100;
        public const float DebugRowGapPx = 20f;       // mockup .drow margin-bottom
        public const float DebugActionWidthPx = 200f; // graybox width for the gold action pill
        public const float DebugActionHeightPx = 72f; // mockup .action height

        // Type sizes read off the mockup CSS (#161 — no inline literals).
        private const int TitleFontSizePx = 52;
        private const int AppNameFontSizePx = 64;
        private const int TaglineFontSizePx = 26;
        private const int VersionCaptionFontSizePx = 22;
        private const int TabFontSizePx = 36;
        private const int CloseGlyphFontSizePx = 38;
        private const int DebugRowLabelFontSizePx = 34;
        private const int DebugRowSubtitleFontSizePx = 20;
        private const int DebugActionFontSizePx = 30; // mockup .action font-size
        private const float KnobInsetPx = 6f;

        // --- Candy Cottage chrome corner radii (#298) ---
        // Panel/pill radii come from the shared baseline (CandyChromeUgui:
        // PanelRadiusPx / PillRadiusPx). These two are the wireframe mockup's own
        // corner radii, transcribed off mockups/settings.html (#161 — no invented
        // values): the content pane and each debug row. The sidebar tabs keep
        // their locked wireframe corner radius (TabRadiusPx = 24).
        private const float PaneRadiusPx = 28f;      // mockup .pane border-radius
        private const float DebugRowRadiusPx = 22f;  // mockup .drow border-radius

        // --- Display strings ---
        private const string TitleText = "Settings";
        private const string AppNameText = "Doggiehood";
        private const string TaglineText = "Designed by Lucas";
        private const string AboutTabText = "About";
        private const string DebugTabText = "Debug";
        private const string VersionCaptionText = "Version";
        private const string CloseGlyphText = "✕"; // ✕
        private const string FenceRowLabelText = "Show backyard fences";
        private const string FenceRowSubtitleText = "Drives WorldBuilder.ForceFencesVisible (#152)";
        private const string AddCoinsRowLabelText = "Add coins";
        private const string AddCoinsRowSubtitleText = "Grant coins to test expansion (#286)";
        // ASCII "+" — the bundled UI font (DejaVu Sans, #291) does not carry the
        // mockup's fullwidth plus (U+FF0B), so it would draw nothing in the build.
        private const string AddCoinsGlyph = "+";
        // #457: the Debug-tab "Refresh quests now" action — a one-shot pill styled
        // like Add coins. ASCII-only glyph for the same bundled-font reason.
        private const string RefreshQuestsRowLabelText = "Refresh quests now";
        private const string RefreshQuestsRowSubtitleText = "Force new-quest randomization, skip the 8h timer (#457)";
        private const string RefreshQuestsGlyph = "Go";

        /// <summary>#291: the bundled UI font, loaded from a Resources folder so
        /// it ships in the Android build. Runtime-built UGUI cannot rely on
        /// <c>Resources.GetBuiltinResource</c> (Editor-only, stripped from the
        /// player), which left every label invisible on device.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        /// <summary>Debug-toggle registry key for the first on-device toggle,
        /// the show/hide backyard fences switch (#152).</summary>
        public const string FenceToggleKey = "show-backyard-fences";

        // --- Palette (#298: the shared Candy Cottage palette, one source) ---
        // Every solid fill maps to a named CandyChromeUgui palette color
        // (docs/specs/ui/shared-components.md) — no hand-picked hex here.
        private static readonly Color PanelColor = CandyChromeUgui.Panel;
        private static readonly Color TabColor = CandyChromeUgui.Cream;         // inactive tab / close
        private static readonly Color TabActiveColor = CandyChromeUgui.Coral;   // active tab
        private static readonly Color RowColor = CandyChromeUgui.Panel;         // debug row backing
        private static readonly Color ToggleOnColor = CandyChromeUgui.Leaf;
        private static readonly Color ToggleOffColor = CandyChromeUgui.Disabled;
        private static readonly Color KnobColor = CandyChromeUgui.Panel;
        private static readonly Color InkColor = CandyChromeUgui.Ink;
        private static readonly Color ActionColor = CandyChromeUgui.Gold;       // gold add-coins action

        // The scrim (translucent dim) and the content pane's neutral stage tone
        // are not Candy Cottage component fills — the scrim is the shared 46%
        // ink dim (settings.md) and the stage is the mockup's --stage neutral.
        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.46f);
        private static readonly Color PaneColor = new Color32(0xE7, 0xDF, 0xCE, 0xFF); // mockup --stage

        private GameState state;
        private DebugUnlockGesture gesture;
        private DebugToggleRegistry toggles;

        private GameObject content;
        private RectTransform panelRect;
        private RectTransform scrimRect;
        private RectTransform sidebarRect;
        private RectTransform aboutTabRect;
        private RectTransform debugTabRect;
        private RectTransform closeButtonRect;
        private RectTransform aboutPaneRect;
        private RectTransform debugPaneRect;
        private RectTransform fenceToggleRect;
        private Image fenceToggleImage;
        private RectTransform fenceKnobRect;
        private RectTransform addCoinsRowRect;
        private RectTransform addCoinsButtonRect;
        private RectTransform refreshQuestsRowRect;
        private RectTransform refreshQuestsButtonRect;
        private Text versionLabel;

        /// <summary>Rebuild hook the bootstrap wires so a fence-toggle flip
        /// actually rebuilds the world (fences show/hide on a live build).</summary>
        public Action WorldRebuild { get; set; }

        public RectTransform PanelRect => panelRect;
        public RectTransform ScrimRect => scrimRect;
        public RectTransform SidebarRect => sidebarRect;
        public RectTransform AboutTabRect => aboutTabRect;
        public RectTransform DebugTabRect => debugTabRect;
        public RectTransform CloseButtonRect => closeButtonRect;
        public RectTransform FenceToggleRect => fenceToggleRect;
        public RectTransform FenceKnobRect => fenceKnobRect;
        public RectTransform AddCoinsRowRect => addCoinsRowRect;
        public RectTransform AddCoinsButtonRect => addCoinsButtonRect;
        public RectTransform RefreshQuestsRowRect => refreshQuestsRowRect;
        public RectTransform RefreshQuestsButtonRect => refreshQuestsButtonRect;
        public RectTransform AboutPaneRect => aboutPaneRect;
        public Text VersionLabel => versionLabel;

        /// <summary>Whether the panel is currently shown.</summary>
        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>Whether the Debug tab has been revealed (unlock gesture).</summary>
        public bool DebugTabVisible => debugTabRect != null && debugTabRect.gameObject.activeSelf;

        /// <summary>Live state of the fence debug toggle.</summary>
        public bool FenceToggleOn => toggles != null && toggles.IsOn(FenceToggleKey);

        /// <summary>The engine-free unlock gesture (fresh per session, #219).</summary>
        public DebugUnlockGesture Gesture => gesture;

        /// <summary>The engine-free debug-toggle registry (#219).</summary>
        public DebugToggleRegistry Toggles => toggles;

        /// <summary>
        /// Builds the panel hierarchy under this GameObject (expected to sit
        /// under a <see cref="UiCanvas"/>). <paramref name="state"/> is the
        /// live game state — the Debug tab's "Add coins" action (#286)
        /// deposits into its <see cref="GameState.Wallet"/>, mirroring how
        /// <see cref="HudOverlay"/> takes the state. <paramref name="version"/>
        /// is the build version string (the bootstrap passes
        /// <c>Application.version</c> — release-please owns the value, this
        /// only reads it). The panel starts closed and with the Debug tab
        /// hidden; both reset each session because a fresh panel is built on
        /// launch.
        /// </summary>
        public void Init(GameState state, string version)
        {
            this.state = state;
            gesture = new DebugUnlockGesture();
            toggles = new DebugToggleRegistry();
            toggles.Register(FenceToggleKey, WorldBuilder.ForceFencesVisible);
            toggles.Changed += OnToggleChanged;

            Build(version);
            content.SetActive(false);
        }

        /// <summary>Shows the panel.</summary>
        public void Open()
        {
            if (content != null)
            {
                content.SetActive(true);
            }
        }

        /// <summary>Hides the panel.</summary>
        public void Close()
        {
            if (content != null)
            {
                content.SetActive(false);
            }
        }

        /// <summary>
        /// Registers a tap on the version label at <paramref name="nowSeconds"/>
        /// (Unity feeds <c>Time.unscaledTimeAsDouble</c>). Ten taps within the
        /// gesture's rolling window reveal the Debug tab.
        /// </summary>
        public void TapVersion(double nowSeconds)
        {
            if (gesture.RegisterTap(nowSeconds) && !DebugTabVisible)
            {
                RevealDebugTab();
            }
        }

        /// <summary>Flips the fence debug toggle (drives the fence seam).</summary>
        public void ToggleFence()
        {
            toggles.Toggle(FenceToggleKey);
        }

        /// <summary>#286: the Debug-tab "Add coins" action — grants
        /// <see cref="DebugAddCoinsAmount"/> coins to the live wallet so
        /// neighborhood expansion can be tested without grinding quests. A
        /// plain action (not a persisted toggle), so it doesn't go through the
        /// bool-only <see cref="DebugToggleRegistry"/>; it deposits straight
        /// through the Core wallet seam. The HUD chip reads the wallet live,
        /// so the new balance shows immediately.</summary>
        public void AddCoins()
        {
            state?.Wallet.Deposit(DebugAddCoinsAmount);
        }

        /// <summary>#457: the Debug-tab "Refresh quests now" action — forces the
        /// new-quest randomization immediately, skipping the 8h refresh timer, so
        /// quest content can be tested without waiting. Thin wiring: the decision
        /// lives in Core (<see cref="Doggiehood.Core.Quests.QuestManager.ForceRefresh"/>),
        /// which runs the same top-up + timestamp-record as a natural rotation but
        /// without the cadence gate. Uses a fresh <see cref="System.Random"/> like
        /// the launch seeding bootstrap, since this entry point has no
        /// caller-supplied one.</summary>
        public void RefreshQuests()
        {
            state?.Quests.ForceRefresh(DateTime.UtcNow, new System.Random());
        }

        private void OnToggleChanged(string key, bool value)
        {
            if (key != FenceToggleKey)
            {
                return;
            }

            WorldBuilder.ForceFencesVisible = value;
            SyncFenceToggleVisual(value);
            WorldRebuild?.Invoke();
        }

        private void RevealDebugTab()
        {
            if (debugTabRect != null)
            {
                debugTabRect.gameObject.SetActive(true);
            }
        }

        private void SelectAbout()
        {
            SetActiveTab(showDebug: false);
        }

        private void SelectDebug()
        {
            SetActiveTab(showDebug: true);
        }

        private void SetActiveTab(bool showDebug)
        {
            if (aboutPaneRect != null)
            {
                aboutPaneRect.gameObject.SetActive(!showDebug);
            }

            if (debugPaneRect != null)
            {
                debugPaneRect.gameObject.SetActive(showDebug);
            }

            Paint(aboutTabRect, showDebug ? TabColor : TabActiveColor);
            Paint(debugTabRect, showDebug ? TabActiveColor : TabColor);
        }

        // ---------------------------------------------------------------
        // Building (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void Build(string version)
        {
            content = new GameObject("SettingsContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            Stretch(contentRect);

            scrimRect = CreateImage("Scrim", contentRect, ScrimColor).rectTransform;
            Stretch(scrimRect);
            var scrimButton = scrimRect.gameObject.AddComponent<Button>();
            scrimButton.onClick.AddListener(Close);

            BuildPanel(contentRect, version);
        }

        private void BuildPanel(RectTransform parent, string version)
        {
            var panelImage = CreateImage("Panel", parent, PanelColor);
            panelRect = panelImage.rectTransform;
            Center(panelRect, SettingsPanelWidthPx, SettingsPanelHeightPx);
            // Panel chrome: Panel fill + Ink outline + flat hard drop-shadow at
            // the shared PanelRadiusPx corner radius (#298, checklist item 1).
            CandyChromeUgui.ApplyRounded(panelImage, PanelColor, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            BuildCloseButton(panelRect);
            BuildTitle(panelRect);
            BuildSidebar(panelRect);
            BuildContentPane(panelRect, version);

            SelectAbout();
        }

        private void BuildCloseButton(RectTransform parent)
        {
            var closeImage = CreateImage("Close", parent, TabColor);
            closeButtonRect = closeImage.rectTransform;
            closeButtonRect.anchorMin = Vector2.one;
            closeButtonRect.anchorMax = Vector2.one;
            closeButtonRect.pivot = Vector2.one;
            closeButtonRect.sizeDelta = new Vector2(CloseButtonSizePx, CloseButtonSizePx);
            closeButtonRect.anchoredPosition = Vector2.zero;
            // Close chrome: a cream pill (a 72px square is a full circle) with the
            // Candy Cottage outline + hard shadow (#298, checklist item 4).
            CandyChromeUgui.ApplyPill(closeImage, TabColor, CloseButtonSizePx, withShadow: true);

            CreateLabel("Glyph", closeButtonRect, CloseGlyphText, CloseGlyphFontSizePx, TextAnchor.MiddleCenter);
            closeImage.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        }

        private void BuildTitle(RectTransform parent)
        {
            var title = CreateLabel("Title", parent, TitleText, TitleFontSizePx, TextAnchor.UpperLeft);
            var rect = title.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(-SettingsPanelPaddingPx * 2f, TitleFontSizePx * 2f);
            rect.anchoredPosition = new Vector2(SettingsPanelPaddingPx, -SettingsPanelPaddingPx);
        }

        private void BuildSidebar(RectTransform parent)
        {
            var sidebar = CreateRect("Sidebar", parent);
            sidebarRect = sidebar;
            sidebarRect.anchorMin = new Vector2(0f, 0f);
            sidebarRect.anchorMax = new Vector2(0f, 1f);
            sidebarRect.pivot = new Vector2(0f, 1f);
            sidebarRect.sizeDelta = new Vector2(SidebarWidthPx, -BodyTopInset() - SettingsPanelPaddingPx);
            sidebarRect.anchoredPosition = new Vector2(SettingsPanelPaddingPx, -BodyTopInset());

            aboutTabRect = BuildTab(sidebarRect, AboutTabText, order: 0, () => SelectAbout());
            debugTabRect = BuildTab(sidebarRect, DebugTabText, order: 1, () => SelectDebug());
            debugTabRect.gameObject.SetActive(false);
        }

        private RectTransform BuildTab(RectTransform sidebar, string label, int order, UnityEngine.Events.UnityAction onClick)
        {
            var tabImage = CreateImage("Tab-" + label, sidebar, TabColor);
            var rect = tabImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, TabHeightPx);
            rect.anchoredPosition = new Vector2(0f, -order * (TabHeightPx + TabGapPx));
            // Tab chrome: a PillButton-styled row — Ink outline + hard shadow at
            // the locked wireframe TabRadiusPx corner radius; the role tint
            // (active Coral / inactive Cream) is applied by SetActiveTab (#298,
            // checklist item 2).
            CandyChromeUgui.ApplyRounded(tabImage, TabColor, TabRadiusPx, withShadow: true);

            var text = CreateLabel("Label", rect, label, TabFontSizePx, TextAnchor.MiddleLeft);
            // #467: stretch the label to the full tab bounds BEFORE the left inset,
            // mirroring the file's Stretch() helper. Without this the rect keeps
            // Unity's un-stretched default anchors, so MiddleLeft renders pinned
            // toward a corner ("top-right justified") and the un-clipped label
            // overflows past its own pill into the neighbor tab ("touching").
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(TabRadiusPx, 0f);

            tabImage.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            return rect;
        }

        private void BuildContentPane(RectTransform parent, string version)
        {
            var pane = CreateImage("ContentPane", parent, PaneColor);
            var rect = pane.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(SettingsPanelPaddingPx + SidebarWidthPx + SidebarContentGapPx, SettingsPanelPaddingPx);
            rect.offsetMax = new Vector2(-SettingsPanelPaddingPx, -BodyTopInset());
            // Content pane: a rounded stage surface inside the panel — Ink outline,
            // no drop-shadow (it is an inset interior surface, per the mockup).
            CandyChromeUgui.ApplyRounded(pane, PaneColor, PaneRadiusPx, withShadow: false);

            BuildAboutPane(rect, version);
            BuildDebugPane(rect);
        }

        private void BuildAboutPane(RectTransform parent, string version)
        {
            aboutPaneRect = CreateRect("AboutPane", parent);
            Stretch(aboutPaneRect);
            aboutPaneRect.offsetMin = new Vector2(SettingsPanelPaddingPx, SettingsPanelPaddingPx);
            aboutPaneRect.offsetMax = new Vector2(-SettingsPanelPaddingPx, -SettingsPanelPaddingPx);

            var appName = CreateLabel("AppName", aboutPaneRect, AppNameText, AppNameFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(appName.rectTransform, 0f, AppNameFontSizePx * 1.2f);

            var tagline = CreateLabel("Tagline", aboutPaneRect, TaglineText, TaglineFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(tagline.rectTransform, AppNameFontSizePx * 1.3f, TaglineFontSizePx * 1.5f);

            var caption = CreateLabel("VersionCaption", aboutPaneRect, VersionCaptionText, VersionCaptionFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(caption.rectTransform, AppNameFontSizePx * 2.6f, VersionCaptionFontSizePx * 1.5f);

            versionLabel = CreateLabel("Version", aboutPaneRect, version, VersionFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(versionLabel.rectTransform, AppNameFontSizePx * 2.6f + VersionCaptionFontSizePx * 1.6f, VersionFontSizePx * 1.4f);
            versionLabel.gameObject.AddComponent<Button>().onClick.AddListener(
                () => TapVersion(Time.unscaledTimeAsDouble));
        }

        private void BuildDebugPane(RectTransform parent)
        {
            debugPaneRect = CreateRect("DebugPane", parent);
            Stretch(debugPaneRect);
            debugPaneRect.offsetMin = new Vector2(SettingsPanelPaddingPx, SettingsPanelPaddingPx);
            debugPaneRect.offsetMax = new Vector2(-SettingsPanelPaddingPx, -SettingsPanelPaddingPx);

            BuildFenceRow(debugPaneRect);
            BuildAddCoinsRow(debugPaneRect);
            BuildRefreshQuestsRow(debugPaneRect);
            debugPaneRect.gameObject.SetActive(false);
        }

        /// <summary>Creates one full-width debug row, stacked from the top of
        /// the Debug pane by <paramref name="order"/> (0 = fence toggle,
        /// 1 = add-coins, …), each separated by <see cref="DebugRowGapPx"/>.</summary>
        private static RectTransform CreateDebugRow(RectTransform parent, string name, int order)
        {
            var image = CreateImage(name, parent, RowColor);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, DebugRowHeightPx);
            rect.anchoredPosition = new Vector2(0f, -order * (DebugRowHeightPx + DebugRowGapPx));
            // Row chrome: a rounded Panel-fill card with the Ink outline + hard
            // shadow, matching the mockup .drow (#298).
            CandyChromeUgui.ApplyRounded(image, RowColor, DebugRowRadiusPx, withShadow: true);
            return rect;
        }

        private void BuildFenceRow(RectTransform parent)
        {
            var rect = CreateDebugRow(parent, "FenceRow", order: 0);

            var label = CreateLabel("Label", rect, FenceRowLabelText, DebugRowLabelFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(label.rectTransform, KnobInsetPx, DebugRowLabelFontSizePx * 1.3f);
            label.rectTransform.offsetMin = new Vector2(TabRadiusPx, label.rectTransform.offsetMin.y);

            var subtitle = CreateLabel("Subtitle", rect, FenceRowSubtitleText, DebugRowSubtitleFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(subtitle.rectTransform, DebugRowLabelFontSizePx * 1.4f, DebugRowSubtitleFontSizePx * 1.3f);
            subtitle.rectTransform.offsetMin = new Vector2(TabRadiusPx, subtitle.rectTransform.offsetMin.y);

            fenceToggleImage = CreateImage("Toggle", rect, ToggleOffColor);
            fenceToggleRect = fenceToggleImage.rectTransform;
            fenceToggleRect.anchorMin = new Vector2(1f, 0.5f);
            fenceToggleRect.anchorMax = new Vector2(1f, 0.5f);
            fenceToggleRect.pivot = new Vector2(1f, 0.5f);
            fenceToggleRect.sizeDelta = new Vector2(ToggleTrackWidthPx, ToggleTrackHeightPx);
            fenceToggleRect.anchoredPosition = new Vector2(-KnobInsetPx, 0f);
            // Switch track: a full pill with the Ink outline, no drop-shadow — the
            // Candy Cottage switch styling (#298, checklist item 3). The on/off
            // fill (Leaf/Disabled) is driven by SyncFenceToggleVisual.
            CandyChromeUgui.ApplyPill(fenceToggleImage, ToggleOffColor, ToggleTrackHeightPx, withShadow: false);

            var knobImage = CreateImage("Knob", fenceToggleRect, KnobColor);
            fenceKnobRect = knobImage.rectTransform;
            fenceKnobRect.sizeDelta = new Vector2(ToggleKnobPx, ToggleKnobPx);
            fenceKnobRect.anchorMin = new Vector2(0f, 0.5f);
            fenceKnobRect.anchorMax = new Vector2(0f, 0.5f);
            fenceKnobRect.pivot = new Vector2(0f, 0.5f);
            // Knob: a round Panel-fill cap with the Ink outline (no drop-shadow).
            CandyChromeUgui.ApplyPill(knobImage, KnobColor, ToggleKnobPx, withShadow: false);

            fenceToggleImage.gameObject.AddComponent<Button>().onClick.AddListener(ToggleFence);
            SyncFenceToggleVisual(FenceToggleOn);
        }

        private void BuildAddCoinsRow(RectTransform parent)
        {
            addCoinsRowRect = CreateDebugRow(parent, "AddCoinsRow", order: 1);

            var label = CreateLabel("Label", addCoinsRowRect, AddCoinsRowLabelText, DebugRowLabelFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(label.rectTransform, KnobInsetPx, DebugRowLabelFontSizePx * 1.3f);
            label.rectTransform.offsetMin = new Vector2(TabRadiusPx, label.rectTransform.offsetMin.y);

            var subtitle = CreateLabel("Subtitle", addCoinsRowRect, AddCoinsRowSubtitleText, DebugRowSubtitleFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(subtitle.rectTransform, DebugRowLabelFontSizePx * 1.4f, DebugRowSubtitleFontSizePx * 1.3f);
            subtitle.rectTransform.offsetMin = new Vector2(TabRadiusPx, subtitle.rectTransform.offsetMin.y);

            var actionImage = CreateImage("Action", addCoinsRowRect, ActionColor);
            addCoinsButtonRect = actionImage.rectTransform;
            addCoinsButtonRect.anchorMin = new Vector2(1f, 0.5f);
            addCoinsButtonRect.anchorMax = new Vector2(1f, 0.5f);
            addCoinsButtonRect.pivot = new Vector2(1f, 0.5f);
            addCoinsButtonRect.sizeDelta = new Vector2(DebugActionWidthPx, DebugActionHeightPx);
            addCoinsButtonRect.anchoredPosition = new Vector2(-KnobInsetPx, 0f);
            // Add-coins action: a Gold pill with the Ink outline + hard shadow
            // (#298, checklist item 3).
            CandyChromeUgui.ApplyPill(actionImage, ActionColor, DebugActionHeightPx, withShadow: true);

            // "+100" built from the named amount — no bare literal (#161).
            CreateLabel("Glyph", addCoinsButtonRect, AddCoinsGlyph + DebugAddCoinsAmount, DebugActionFontSizePx, TextAnchor.MiddleCenter);
            actionImage.gameObject.AddComponent<Button>().onClick.AddListener(AddCoins);
        }

        /// <summary>#457: the third Debug-tab row — a "Refresh quests now" action
        /// pill styled exactly like <see cref="BuildAddCoinsRow"/> (same
        /// <see cref="DebugActionWidthPx"/>/<see cref="DebugActionHeightPx"/> Gold
        /// pill), stacked one row below Add coins via the existing
        /// <see cref="DebugRowHeightPx"/>/<see cref="DebugRowGapPx"/> constants
        /// (#161 — no new named layout values). Wired to the Core forced-refresh
        /// seam so the 8h timer can be skipped for playtesting.</summary>
        private void BuildRefreshQuestsRow(RectTransform parent)
        {
            refreshQuestsRowRect = CreateDebugRow(parent, "RefreshQuestsRow", order: 2);

            var label = CreateLabel("Label", refreshQuestsRowRect, RefreshQuestsRowLabelText, DebugRowLabelFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(label.rectTransform, KnobInsetPx, DebugRowLabelFontSizePx * 1.3f);
            label.rectTransform.offsetMin = new Vector2(TabRadiusPx, label.rectTransform.offsetMin.y);

            var subtitle = CreateLabel("Subtitle", refreshQuestsRowRect, RefreshQuestsRowSubtitleText, DebugRowSubtitleFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(subtitle.rectTransform, DebugRowLabelFontSizePx * 1.4f, DebugRowSubtitleFontSizePx * 1.3f);
            subtitle.rectTransform.offsetMin = new Vector2(TabRadiusPx, subtitle.rectTransform.offsetMin.y);

            var actionImage = CreateImage("Action", refreshQuestsRowRect, ActionColor);
            refreshQuestsButtonRect = actionImage.rectTransform;
            refreshQuestsButtonRect.anchorMin = new Vector2(1f, 0.5f);
            refreshQuestsButtonRect.anchorMax = new Vector2(1f, 0.5f);
            refreshQuestsButtonRect.pivot = new Vector2(1f, 0.5f);
            refreshQuestsButtonRect.sizeDelta = new Vector2(DebugActionWidthPx, DebugActionHeightPx);
            refreshQuestsButtonRect.anchoredPosition = new Vector2(-KnobInsetPx, 0f);
            // Refresh action: a Gold pill with the Ink outline + hard shadow,
            // matching the Add coins action (#457/#298).
            CandyChromeUgui.ApplyPill(actionImage, ActionColor, DebugActionHeightPx, withShadow: true);

            CreateLabel("Glyph", refreshQuestsButtonRect, RefreshQuestsGlyph, DebugActionFontSizePx, TextAnchor.MiddleCenter);
            actionImage.gameObject.AddComponent<Button>().onClick.AddListener(RefreshQuests);
        }

        private void SyncFenceToggleVisual(bool on)
        {
            if (fenceToggleImage == null || fenceKnobRect == null)
            {
                return;
            }

            fenceToggleImage.color = on ? ToggleOnColor : ToggleOffColor;
            fenceKnobRect.anchorMin = new Vector2(on ? 1f : 0f, 0.5f);
            fenceKnobRect.anchorMax = fenceKnobRect.anchorMin;
            fenceKnobRect.pivot = new Vector2(on ? 1f : 0f, 0.5f);
            fenceKnobRect.anchoredPosition = new Vector2(on ? -KnobInsetPx : KnobInsetPx, 0f);
        }

        // --- small UGUI helpers ---

        private static float BodyTopInset()
        {
            // Title band + panel padding above the sidebar/content body.
            return SettingsPanelPaddingPx + TitleFontSizePx * 2f;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateLabel(string name, RectTransform parent, string value, int fontSize, TextAnchor anchor)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = InkColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = LabelFont();
            return text;
        }

        /// <summary>Loads (and caches) the bundled UI font from Resources so a
        /// build actually ships glyphs. See <see cref="LabelFontResource"/>.</summary>
        private static Font LabelFont()
        {
            if (labelFont == null)
            {
                labelFont = Resources.Load<Font>(LabelFontResource);
            }

            return labelFont;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void AnchorTop(RectTransform rect, float topOffset, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
        }

        private static void Paint(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }
    }
}
