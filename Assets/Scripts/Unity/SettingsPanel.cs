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
        // gold action is "＋100" (docs/specs/ui/settings.md mockup).
        public const int DebugAddCoinsAmount = 100;
        public const float DebugRowGapPx = 20f;       // mockup .drow margin-bottom
        public const float DebugActionWidthPx = 200f; // graybox width for the gold action pill
        public const float DebugActionHeightPx = 72f; // mockup .action height

        // Type sizes read off the mockup CSS (#161 — no inline literals).
        private const int TitleFontSizePx = 52;
        private const int AppNameFontSizePx = 64;
        private const int TaglineFontSizePx = 26;
        private const int CreditsFontSizePx = 24;
        private const int VersionCaptionFontSizePx = 22;
        private const int TabFontSizePx = 36;
        private const int CloseGlyphFontSizePx = 38;
        private const int DebugRowLabelFontSizePx = 34;
        private const int DebugRowSubtitleFontSizePx = 20;
        private const int DebugActionFontSizePx = 30; // mockup .action font-size
        private const float KnobInsetPx = 6f;

        // --- Display strings ---
        private const string TitleText = "Settings";
        private const string AppNameText = "Doggiehood";
        private const string TaglineText = "A neighborhood of dogs";
        private const string CreditsText = "Made by Derek & Lucas";
        private const string AboutTabText = "About";
        private const string DebugTabText = "Debug";
        private const string VersionCaptionText = "Version";
        private const string CloseGlyphText = "✕"; // ✕
        private const string FenceRowLabelText = "Show backyard fences";
        private const string FenceRowSubtitleText = "Drives WorldBuilder.ForceFencesVisible (#152)";
        private const string AddCoinsRowLabelText = "Add coins";
        private const string AddCoinsRowSubtitleText = "Grant coins to test expansion (#286)";
        private const string AddCoinsGlyph = "＋"; // fullwidth plus, per the mockup

        /// <summary>Debug-toggle registry key for the first on-device toggle,
        /// the show/hide backyard fences switch (#152).</summary>
        public const string FenceToggleKey = "show-backyard-fences";

        // --- Palette (graybox, restyled by the #173 shared chrome pass) ---
        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.46f);
        private static readonly Color PanelColor = new Color(1f, 0.99f, 0.97f, 1f);
        private static readonly Color PaneColor = new Color(0.906f, 0.875f, 0.808f, 1f);
        private static readonly Color TabColor = new Color(1f, 0.953f, 0.851f, 1f);
        private static readonly Color TabActiveColor = new Color(1f, 0.478f, 0.361f, 1f);
        private static readonly Color RowColor = new Color(1f, 0.99f, 0.97f, 1f);
        private static readonly Color ToggleOnColor = new Color(0.345f, 0.753f, 0.416f, 1f);
        private static readonly Color ToggleOffColor = new Color(0.847f, 0.824f, 0.776f, 1f);
        private static readonly Color KnobColor = new Color(1f, 0.99f, 0.97f, 1f);
        private static readonly Color InkColor = new Color(0.180f, 0.165f, 0.149f, 1f);
        // Gold action pill (#FFC23C, mockup --gold).
        private static readonly Color ActionColor = new Color(1f, 0.761f, 0.235f, 1f);

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
        public RectTransform AddCoinsRowRect => addCoinsRowRect;
        public RectTransform AddCoinsButtonRect => addCoinsButtonRect;
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

            var text = CreateLabel("Label", rect, label, TabFontSizePx, TextAnchor.MiddleLeft);
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

            var credits = CreateLabel("Credits", aboutPaneRect, CreditsText, CreditsFontSizePx, TextAnchor.UpperLeft);
            AnchorTop(credits.rectTransform, AppNameFontSizePx * 4.2f, CreditsFontSizePx * 1.5f);
        }

        private void BuildDebugPane(RectTransform parent)
        {
            debugPaneRect = CreateRect("DebugPane", parent);
            Stretch(debugPaneRect);
            debugPaneRect.offsetMin = new Vector2(SettingsPanelPaddingPx, SettingsPanelPaddingPx);
            debugPaneRect.offsetMax = new Vector2(-SettingsPanelPaddingPx, -SettingsPanelPaddingPx);

            BuildFenceRow(debugPaneRect);
            BuildAddCoinsRow(debugPaneRect);
            debugPaneRect.gameObject.SetActive(false);
        }

        /// <summary>Creates one full-width debug row, stacked from the top of
        /// the Debug pane by <paramref name="order"/> (0 = fence toggle,
        /// 1 = add-coins, …), each separated by <see cref="DebugRowGapPx"/>.</summary>
        private static RectTransform CreateDebugRow(RectTransform parent, string name, int order)
        {
            var rect = CreateImage(name, parent, RowColor).rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, DebugRowHeightPx);
            rect.anchoredPosition = new Vector2(0f, -order * (DebugRowHeightPx + DebugRowGapPx));
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

            fenceKnobRect = CreateImage("Knob", fenceToggleRect, KnobColor).rectTransform;
            fenceKnobRect.sizeDelta = new Vector2(ToggleKnobPx, ToggleKnobPx);
            fenceKnobRect.anchorMin = new Vector2(0f, 0.5f);
            fenceKnobRect.anchorMax = new Vector2(0f, 0.5f);
            fenceKnobRect.pivot = new Vector2(0f, 0.5f);

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

            // "＋100" built from the named amount — no bare literal (#161).
            CreateLabel("Glyph", addCoinsButtonRect, AddCoinsGlyph + DebugAddCoinsAmount, DebugActionFontSizePx, TextAnchor.MiddleCenter);
            actionImage.gameObject.AddComponent<Button>().onClick.AddListener(AddCoins);
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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = font;
            return text;
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
