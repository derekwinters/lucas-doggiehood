using System;
using System.Collections.Generic;
using System.Globalization;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Tuning;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The balance tuning menu (#622): a centered, scrollable panel of grouped
    /// sliders over the Core <see cref="TuningConfig"/>, so
    /// pacing/economy/expansion/move-in can be dialed in <b>live on-device</b>
    /// instead of guessing numbers and rebuilding. Reached from the
    /// "Tune balance…" row in the Settings Debug tab
    /// (<see cref="SettingsPanel"/>), it layers as a modal <em>over</em> the
    /// still-open Settings panel and closes back to it — it never replaces it.
    ///
    /// <para><b>Thin view, no balance logic.</b> Every slider's label, unit,
    /// group and min/max/step comes from the engine-free
    /// <see cref="TuningCatalog"/>, and every value is read from / written
    /// straight into <see cref="TuningConfig.Active"/>, whose Core seams
    /// (<c>EconomyNumbers</c>, <c>TileUnlockNumbers</c>, …) re-read it on every
    /// access (#620) — so a drag changes the running game immediately. There is
    /// no hard-coded slider list here that could drift from Core: add a field to
    /// <see cref="TuningConfig"/> and it shows up, or the Core catalog test
    /// fails.</para>
    ///
    /// <para><b>Part of the debug menu, gated by the 10-tap unlock (#656).</b>
    /// Built unconditionally through <see cref="Create"/> in <em>every</em>
    /// build — development, release-candidate and the shipping release alike —
    /// because it lives inside the existing Debug tab, whose 10-tap unlock
    /// gesture (#219) is its sole gate. That is deliberate and temporary: the
    /// debug menu stays in the game while the balance is still being dialed in,
    /// and the whole thing (tab, gesture, this panel) comes out in one later
    /// pass (docs/specs/ui/debug-tuning-menu.md).</para>
    ///
    /// <para><b>Style is deliberately NOT Candy Cottage.</b> This is the one
    /// screen exempt from the shared chrome (#298/#465): a flat panel, a thin
    /// <see cref="TuningPanelOutlinePx"/> outline (not the shared 6px band), no
    /// hard drop-shadows, plain track/knob sliders and tabular value readouts.
    /// The exemption is recorded in the approved wireframe so a later pass
    /// doesn't "fix" it toward the game's art direction. Every layout number is
    /// that wireframe's named constant (#161), asserted by EditMode tests.</para>
    /// </summary>
    public sealed class TuningMenuOverlay : MonoBehaviour
    {
        // --- Layout constants from the approved #621 wireframe ---
        public const float TuningPanelWidthPx = 1200f;
        public const float TuningPanelHeightPx = 920f;
        public const float TuningPanelPaddingPx = 32f;
        public const float TuningPanelRadiusPx = 16f;
        public const float TuningPanelOutlinePx = 2f;
        public const float HeaderHeightPx = 88f;
        public const int HeaderTitleFontPx = 40;
        public const float HeaderGapPx = 24f;
        public const float CloseButtonSizePx = 56f;
        public const float ResetAllButtonWidthPx = 220f;
        public const float ResetButtonHeightPx = 56f;
        public const float ScrollBodyPaddingRightPx = 24f;
        public const float ScrollbarWidthPx = 12f;
        public const float GroupGapPx = 28f;
        public const float GroupHeaderHeightPx = 64f;
        public const int GroupHeaderFontPx = 32;
        public const float GroupResetButtonWidthPx = 150f;
        public const float ControlRowHeightPx = 96f;
        public const float ControlRowGapPx = 12f;
        public const float ControlRowPaddingXPx = 24f;
        public const int ControlLabelFontPx = 28;
        public const int ControlValueFontPx = 30;
        public const float SliderTrackHeightPx = 12f;
        public const float SliderTrackRadiusPx = 6f;
        public const float SliderKnobPx = 40f;
        public const float SliderLabelValueGapPx = 8f;

        /// <summary>The "Tune balance…" Debug-tab entry row's height. The
        /// wireframe pins it to the Settings debug-row metric rather than a
        /// second, independently-drifting number.</summary>
        public const float EntryRowHeightPx = SettingsPanel.DebugRowHeightPx;

        // --- Graybox geometry read off the mockup CSS (#161: no inline literals) ---
        private const float HeaderButtonGapPx = 20f;      // mockup .thead gap
        private const float ButtonRadiusPx = 8f;          // mockup .btn border-radius
        private const float ButtonOutlinePx = 1f;         // mockup .btn border-width
        private const float ControlRowRadiusPx = 8f;      // mockup .crow border-radius
        private const float ControlRowOutlinePx = 1f;     // mockup .crow border-width
        private const float GroupHeaderRuleHeightPx = 2f; // mockup .ghead border-bottom
        private const int ButtonFontPx = 26;              // mockup .btn font-size
        private const int GroupResetFontPx = 22;          // mockup .ghead .greset font-size
        private const int CloseGlyphFontPx = 30;          // mockup .thead .xclose font-size

        /// <summary>Text leading factor, matching the rest of the runtime UGUI
        /// (see <see cref="SettingsPanel"/>'s row labels).</summary>
        private const float LineLeadingFactor = 1.3f;

        /// <summary>The label/value line above each slider band.</summary>
        private const float LabelLineHeightPx = ControlValueFontPx * LineLeadingFactor;

        /// <summary>Vertical inset that centers a row's (label line + gap +
        /// slider band) stack inside <see cref="ControlRowHeightPx"/> — derived,
        /// never an invented magic number.</summary>
        private const float ControlRowInsetYPx =
            (ControlRowHeightPx - LabelLineHeightPx - SliderLabelValueGapPx - SliderKnobPx) / 2f;

        // --- Display strings ---
        private const string HeaderTitleText = "Balance Tuning · DEV";
        private const string ResetAllText = "Reset all";
        private const string GroupResetText = "Reset";
        private const string CloseGlyphText = "✕";

        /// <summary>Live value readout: up to two decimals, invariant culture
        /// so a device locale can never render "1,5" into a dev readout.</summary>
        private const string DecimalValueFormat = "0.##";
        private const string ValueUnitSeparator = " ";

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build (runtime UGUI cannot use the Editor-only
        /// built-in font). Same asset SettingsPanel uses.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        // --- Utilitarian dev-tool palette (mockups/debug-tuning-menu.html) ---
        // Deliberately NOT the CandyChromeUgui palette: this screen is the one
        // documented exemption from the shared art direction.
        private static readonly Color ScrimColor = new Color(30f / 255f, 28f / 255f, 25f / 255f, 0.40f); // .scrim2
        private static readonly Color PanelColor = new Color32(0xFA, 0xFA, 0xF7, 0xFF);        // --dev-panel
        private static readonly Color LineColor = new Color32(0xB9, 0xB4, 0xA8, 0xFF);         // --dev-line
        private static readonly Color LineStrongColor = new Color32(0x6D, 0x67, 0x5C, 0xFF);   // --dev-line-strong
        private static readonly Color HeaderColor = new Color32(0x33, 0x30, 0x2B, 0xFF);       // --dev-head
        private static readonly Color HeaderTextColor = new Color32(0xF4, 0xF1, 0xEA, 0xFF);   // --dev-head-tx
        private static readonly Color RowColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);          // --dev-row
        private static readonly Color TrackColor = new Color32(0xD9, 0xD4, 0xC7, 0xFF);        // --dev-track
        private static readonly Color FillColor = new Color32(0x5B, 0x7D, 0xB1, 0xFF);         // --dev-fill
        private static readonly Color KnobColor = new Color32(0x33, 0x30, 0x2B, 0xFF);         // --dev-knob
        private static readonly Color ButtonColor = new Color32(0xE7, 0xE2, 0xD6, 0xFF);       // --dev-btn
        private static readonly Color TextColor = new Color32(0x33, 0x30, 0x2B, 0xFF);         // --dev-tx
        private static readonly Color ResetAllColor = new Color32(0xC6, 0x55, 0x3C, 0xFF);     // .thead .resetall
        private static readonly Color ResetAllOutlineColor = new Color32(0x8F, 0x3A, 0x27, 0xFF);
        private static readonly Color ResetAllTextColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color CloseColor = new Color32(0x4A, 0x45, 0x3E, 0xFF);        // .thead .xclose
        private static readonly Color CloseOutlineColor = new Color32(0x2B, 0x28, 0x23, 0xFF);
        private static readonly Color TransparentColor = new Color(0f, 0f, 0f, 0f);

        private readonly List<ControlRow> rows = new List<ControlRow>();
        private readonly List<GroupSection> groups = new List<GroupSection>();

        private GameObject content;
        private RectTransform scrimRect;
        private RectTransform panelRect;
        private RectTransform panelOutlineRect;
        private RectTransform headerRect;
        private RectTransform closeButtonRect;
        private RectTransform resetAllButtonRect;
        private ScrollRect scroll;
        private RectTransform scrollContentRect;
        private Scrollbar verticalScrollbar;

        /// <summary>Guards the slider -> config write path while the panel is
        /// redrawing itself <em>from</em> the config, so a refresh can never
        /// write a value back and disturb live state (#622 checklist: opening
        /// and closing must not change anything but the tuned values).</summary>
        private bool redrawing;

        // --- Test/serialization surface (every assertion goes through these) ---
        public RectTransform ScrimRect => scrimRect;
        public RectTransform PanelRect => panelRect;
        public RectTransform PanelOutlineRect => panelOutlineRect;
        public RectTransform HeaderRect => headerRect;
        public RectTransform CloseButtonRect => closeButtonRect;
        public RectTransform ResetAllButtonRect => resetAllButtonRect;
        public ScrollRect Scroll => scroll;
        public RectTransform ScrollContentRect => scrollContentRect;
        public Scrollbar VerticalScrollbar => verticalScrollbar;

        /// <summary>Every control row, in the panel's top-to-bottom order.</summary>
        public IReadOnlyList<ControlRow> Rows => rows;

        /// <summary>The four groups, in the wireframe's display order.</summary>
        public IReadOnlyList<GroupSection> Groups => groups;

        /// <summary>Whether the panel is currently shown.</summary>
        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>
        /// Builds the overlay under <paramref name="parent"/> (expected to be
        /// the shared <see cref="UiCanvas"/>), unconditionally: #656 makes this
        /// part of the existing debug menu, so it is built in <b>every</b>
        /// build and the Settings Debug tab's 10-tap unlock (#219) is its only
        /// gate. It starts closed, so nothing is on screen until that unlock
        /// has been performed and the "Tune balance…" row tapped.
        /// </summary>
        public static TuningMenuOverlay Create(Transform parent)
        {
            var host = new GameObject("TuningMenuOverlay");
            host.transform.SetParent(parent, false);
            var overlay = host.AddComponent<TuningMenuOverlay>();
            overlay.Init();
            return overlay;
        }

        /// <summary>Builds the panel hierarchy under this GameObject. Starts
        /// closed; a fresh panel is built each launch, so nothing persists.</summary>
        public void Init()
        {
            Build();
            Redraw();
            content.SetActive(false);
        }

        /// <summary>Shows the panel, re-seeding every slider from the live
        /// <see cref="TuningConfig.Active"/> (values may have moved since it was
        /// last open) and registering as a modal so taps never leak to the
        /// Settings panel or the world beneath (#544).</summary>
        public void Open()
        {
            if (content == null)
            {
                return;
            }

            content.SetActive(true);
            Redraw();
            ModalInputGate.Shared.Register(this);
        }

        /// <summary>Hides the panel and releases the modal gate. Tuned values
        /// stay exactly as they were — closing is not a reset.</summary>
        public void Close()
        {
            if (content != null)
            {
                content.SetActive(false);
            }

            ModalInputGate.Shared.Unregister(this);
        }

        /// <summary>Global "Reset all": re-seeds the whole config from a fresh
        /// <see cref="TuningConfig"/> (the #620 seam) and snaps every slider
        /// back.</summary>
        public void ResetAll()
        {
            TuningConfig.ResetToDefaults();
            Redraw();
        }

        /// <summary>Per-group "Reset": restores just <paramref name="group"/>'s
        /// fields to their shipping defaults, leaving the other groups' live
        /// overrides in place, then snaps that group's sliders back.</summary>
        public void ResetGroup(TuningGroup group)
        {
            TuningConfig.ResetGroupToDefaults(group);
            Redraw();
        }

        /// <summary>Re-reads <see cref="TuningConfig.Active"/> into every slider
        /// and value readout. Writes back to the config are suppressed while it
        /// runs, so a redraw is strictly a read.</summary>
        public void Redraw()
        {
            redrawing = true;
            try
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var value = row.Field.Read(TuningConfig.Active);
                    row.Slider.value = (float)value;
                    row.ValueText.text = FormatValue(row.Field, value);
                }
            }
            finally
            {
                redrawing = false;
            }
        }

        /// <summary>Formats a live value as the wireframe's readout: the number
        /// (whole for an <c>int</c> tunable) plus its declared unit.</summary>
        public static string FormatValue(TuningField field, double value)
        {
            var number = field.IsInteger
                ? ((int)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
                : value.ToString(DecimalValueFormat, CultureInfo.InvariantCulture);

            return string.IsNullOrEmpty(field.Unit) ? number : number + ValueUnitSeparator + field.Unit;
        }

        private void OnSliderChanged(ControlRow row, float value)
        {
            if (redrawing)
            {
                return;
            }

            // Core clamps/snaps to the field's declared range on the way in, so
            // a drag can never push a balance value out of bounds.
            row.Field.Write(TuningConfig.Active, value);
            row.ValueText.text = FormatValue(row.Field, row.Field.Read(TuningConfig.Active));
        }

        // ---------------------------------------------------------------
        // Building (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void Build()
        {
            content = new GameObject("TuningContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            Stretch(contentRect);

            var scrim = CreateImage("Scrim", contentRect, ScrimColor);
            scrimRect = scrim.rectTransform;
            Stretch(scrimRect);
            scrim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            BuildPanel(contentRect);
        }

        private void BuildPanel(RectTransform parent)
        {
            var panel = CreateImage("Panel", parent, PanelColor);
            panelRect = panel.rectTransform;
            Center(panelRect, TuningPanelWidthPx, TuningPanelHeightPx);
            Round(panel, TuningPanelRadiusPx);
            panelOutlineRect = DevOutline(panel.gameObject, TuningPanelRadiusPx, TuningPanelOutlinePx, LineStrongColor);

            BuildHeader(panelRect);
            BuildScrollBody(panelRect);
        }

        private void BuildHeader(RectTransform parent)
        {
            var header = CreateImage("Header", parent, HeaderColor);
            headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, HeaderHeightPx);
            headerRect.anchoredPosition = Vector2.zero;

            var title = CreateLabel("Title", headerRect, HeaderTitleText, HeaderTitleFontPx,
                TextAnchor.MiddleLeft, HeaderTextColor);
            Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(TuningPanelPaddingPx, 0f);

            closeButtonRect = BuildHeaderButton(
                "Close", CloseGlyphText, CloseGlyphFontPx, CloseButtonSizePx, CloseColor,
                CloseOutlineColor, HeaderTextColor, TuningPanelPaddingPx, Close);

            resetAllButtonRect = BuildHeaderButton(
                "ResetAll", ResetAllText, ButtonFontPx, ResetAllButtonWidthPx, ResetAllColor,
                ResetAllOutlineColor, ResetAllTextColor,
                TuningPanelPaddingPx + CloseButtonSizePx + HeaderButtonGapPx, ResetAll);
        }

        private RectTransform BuildHeaderButton(
            string name, string label, int fontPx, float widthPx, Color fill, Color outline,
            Color textColor, float rightInsetPx, UnityEngine.Events.UnityAction onClick)
        {
            var image = CreateImage(name, headerRect, fill);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(widthPx, ResetButtonHeightPx);
            rect.anchoredPosition = new Vector2(-rightInsetPx, 0f);
            Round(image, ButtonRadiusPx);
            DevOutline(image.gameObject, ButtonRadiusPx, ButtonOutlinePx, outline);

            var text = CreateLabel("Label", rect, label, fontPx, TextAnchor.MiddleCenter, textColor);
            Stretch(text.rectTransform);

            image.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            return rect;
        }

        private void BuildScrollBody(RectTransform parent)
        {
            var body = CreateRect("ScrollBody", parent);
            Stretch(body);
            body.offsetMin = new Vector2(TuningPanelPaddingPx, TuningPanelPaddingPx);
            body.offsetMax = new Vector2(-TuningPanelPaddingPx, -(HeaderHeightPx + HeaderGapPx));

            var viewport = CreateRect("Viewport", body);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-(ScrollbarWidthPx + ScrollBodyPaddingRightPx), 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            scrollContentRect = CreateRect("Content", viewport);
            scrollContentRect.anchorMin = new Vector2(0f, 1f);
            scrollContentRect.anchorMax = new Vector2(1f, 1f);
            scrollContentRect.pivot = new Vector2(0.5f, 1f);
            scrollContentRect.anchoredPosition = Vector2.zero;
            scrollContentRect.sizeDelta = new Vector2(0f, BuildGroups(scrollContentRect));

            verticalScrollbar = BuildScrollbar(body);

            scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            scroll.content = scrollContentRect;
            scroll.verticalScrollbar = verticalScrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static Scrollbar BuildScrollbar(RectTransform parent)
        {
            var track = CreateImage("Scrollbar", parent, TransparentColor);
            var rect = track.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ScrollbarWidthPx, 0f);
            rect.anchoredPosition = Vector2.zero;

            var slidingArea = CreateRect("SlidingArea", rect);
            Stretch(slidingArea);

            var handle = CreateImage("Handle", slidingArea, LineColor);
            Stretch(handle.rectTransform);
            Round(handle, SliderTrackRadiusPx);

            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            return scrollbar;
        }

        /// <summary>Stacks the four groups (each: a group header + its control
        /// rows) into the scroll content and returns the total content height,
        /// so the content rect is sized by what was actually built rather than
        /// by a guessed number.</summary>
        private float BuildGroups(RectTransform parent)
        {
            var y = 0f;
            var catalogGroups = TuningCatalog.Groups;

            for (var g = 0; g < catalogGroups.Count; g++)
            {
                if (g > 0)
                {
                    y += GroupGapPx;
                }

                var group = catalogGroups[g];
                var section = new GroupSection(group);
                BuildGroupHeader(parent, section, y);
                y += GroupHeaderHeightPx + ControlRowGapPx;

                var fields = TuningCatalog.FieldsIn(group);
                for (var f = 0; f < fields.Count; f++)
                {
                    if (f > 0)
                    {
                        y += ControlRowGapPx;
                    }

                    var row = BuildControlRow(parent, fields[f], y);
                    section.Add(row);
                    rows.Add(row);
                    y += ControlRowHeightPx;
                }

                groups.Add(section);
            }

            return y;
        }

        private void BuildGroupHeader(RectTransform parent, GroupSection section, float topOffset)
        {
            var header = CreateRect("Group-" + section.Group, parent);
            AnchorTop(header, topOffset, GroupHeaderHeightPx);

            var name = CreateLabel("Name", header, TuningCatalog.DisplayName(section.Group),
                GroupHeaderFontPx, TextAnchor.MiddleLeft, HeaderColor);
            Stretch(name.rectTransform);

            // The mockup's group rule: a hairline under the group header.
            var rule = CreateImage("Rule", header, LineStrongColor);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, GroupHeaderRuleHeightPx);
            rule.rectTransform.anchoredPosition = Vector2.zero;
            rule.raycastTarget = false;

            var button = CreateImage("Reset", header, ButtonColor);
            var buttonRect = button.rectTransform;
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.sizeDelta = new Vector2(GroupResetButtonWidthPx, ResetButtonHeightPx);
            buttonRect.anchoredPosition = Vector2.zero;
            Round(button, ButtonRadiusPx);
            DevOutline(button.gameObject, ButtonRadiusPx, ButtonOutlinePx, LineStrongColor);

            var buttonLabel = CreateLabel("Label", buttonRect, GroupResetText, GroupResetFontPx,
                TextAnchor.MiddleCenter, TextColor);
            Stretch(buttonLabel.rectTransform);

            var group = section.Group;
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => ResetGroup(group));

            section.Bind(header, buttonRect, name);
        }

        private ControlRow BuildControlRow(RectTransform parent, TuningField field, float topOffset)
        {
            var rowImage = CreateImage("Row-" + field.FieldName, parent, RowColor);
            var rowRect = rowImage.rectTransform;
            AnchorTop(rowRect, topOffset, ControlRowHeightPx);
            Round(rowImage, ControlRowRadiusPx);
            DevOutline(rowImage.gameObject, ControlRowRadiusPx, ControlRowOutlinePx, LineColor);

            var labelLine = CreateRect("LabelLine", rowRect);
            labelLine.anchorMin = new Vector2(0f, 1f);
            labelLine.anchorMax = new Vector2(1f, 1f);
            labelLine.pivot = new Vector2(0.5f, 1f);
            labelLine.sizeDelta = new Vector2(-ControlRowPaddingXPx * 2f, LabelLineHeightPx);
            labelLine.anchoredPosition = new Vector2(0f, -ControlRowInsetYPx);

            var labelText = CreateLabel("Label", labelLine, field.Label, ControlLabelFontPx,
                TextAnchor.MiddleLeft, TextColor);
            Stretch(labelText.rectTransform);

            var valueText = CreateLabel("Value", labelLine, string.Empty, ControlValueFontPx,
                TextAnchor.MiddleRight, FillColor);
            Stretch(valueText.rectTransform);

            var slider = BuildSlider(rowRect, field);
            var row = new ControlRow(field, rowRect, labelText, valueText, slider);
            slider.onValueChanged.AddListener(value => OnSliderChanged(row, value));
            return row;
        }

        private static Slider BuildSlider(RectTransform parent, TuningField field)
        {
            // A full-height transparent band so the whole strip is the drag
            // target, not just the 12px track.
            var band = CreateImage("Slider", parent, TransparentColor);
            var bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 0f);
            bandRect.anchorMax = new Vector2(1f, 0f);
            bandRect.pivot = new Vector2(0.5f, 0f);
            bandRect.sizeDelta = new Vector2(-ControlRowPaddingXPx * 2f, SliderKnobPx);
            bandRect.anchoredPosition = new Vector2(0f, ControlRowInsetYPx);

            var track = CreateImage("Track", bandRect, TrackColor);
            CenterBand(track.rectTransform, SliderTrackHeightPx, insetXPx: 0f);
            Round(track, SliderTrackRadiusPx);
            track.raycastTarget = false;

            // Fill and handle slide inside an area inset by half a knob, so the
            // knob's extremes stay flush with the track's ends.
            var fillArea = CreateRect("FillArea", bandRect);
            CenterBand(fillArea, SliderTrackHeightPx, SliderKnobPx);
            var fill = CreateImage("Fill", fillArea, FillColor);
            Stretch(fill.rectTransform);
            Round(fill, SliderTrackRadiusPx);
            fill.raycastTarget = false;

            var handleArea = CreateRect("HandleArea", bandRect);
            CenterBand(handleArea, SliderKnobPx, SliderKnobPx);
            var handle = CreateImage("Handle", handleArea, KnobColor);
            // Width is the knob size; the height comes from the handle area
            // (Slider stretches the handle across the area's cross-axis), which
            // is exactly SliderKnobPx tall.
            handle.rectTransform.sizeDelta = new Vector2(SliderKnobPx, 0f);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            Round(handle, SliderKnobPx / 2f);
            handle.raycastTarget = false;

            var slider = band.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = band;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = field.IsInteger;
            slider.minValue = (float)field.Min;
            slider.maxValue = (float)field.Max;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            return slider;
        }

        // --- small UGUI helpers ---

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

        private static Text CreateLabel(string name, RectTransform parent, string value, int fontSize,
            TextAnchor anchor, Color color)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.font = LabelFont();
            return text;
        }

        private static Font LabelFont()
        {
            if (labelFont == null)
            {
                labelFont = Resources.Load<Font>(LabelFontResource);
            }

            return labelFont;
        }

        /// <summary>Applies the procedural rounded-rect sprite at
        /// <paramref name="cornerRadiusPx"/>. Geometry only — none of the Candy
        /// Cottage outline/shadow chrome comes with it, which is the point on
        /// this screen.</summary>
        private static void Round(Image image, float cornerRadiusPx)
        {
            image.sprite = CandyChromeUgui.RoundedSprite(cornerRadiusPx);
            image.type = Image.Type.Sliced;
        }

        /// <summary>The utilitarian outline: a thin band of
        /// <paramref name="color"/> behind the fill, inflated by
        /// <paramref name="thicknessPx"/> on every side. Same constant-width
        /// contour construction as the shared chrome (#616) but at this screen's
        /// own thin width and dev palette — never the Ink band.</summary>
        private static RectTransform DevOutline(GameObject fill, float cornerRadiusPx, float thicknessPx, Color color)
        {
            var fillRect = fill.GetComponent<RectTransform>();
            var band = CreateImage(fill.name + "-Outline", (RectTransform)fillRect.parent, color);
            var bandRect = band.rectTransform;
            bandRect.SetSiblingIndex(fillRect.GetSiblingIndex());
            bandRect.anchorMin = fillRect.anchorMin;
            bandRect.anchorMax = fillRect.anchorMax;
            bandRect.pivot = fillRect.pivot;

            var inflate = new Vector2(thicknessPx, thicknessPx);
            bandRect.offsetMin = fillRect.offsetMin - inflate;
            bandRect.offsetMax = fillRect.offsetMax + inflate;

            band.sprite = CandyChromeUgui.RoundedSprite(cornerRadiusPx + thicknessPx);
            band.type = Image.Type.Sliced;
            band.raycastTarget = false;
            return bandRect;
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
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
        }

        /// <summary>A horizontally-stretched band of <paramref name="heightPx"/>
        /// centered in its parent, inset by <paramref name="insetXPx"/> total.</summary>
        private static void CenterBand(RectTransform rect, float heightPx, float insetXPx)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-insetXPx, heightPx);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>One tunable's row: the Core descriptor it renders, plus the
        /// widgets EditMode tests assert against.</summary>
        public sealed class ControlRow
        {
            public ControlRow(TuningField field, RectTransform rowRect, Text labelText, Text valueText, Slider slider)
            {
                Field = field;
                RowRect = rowRect;
                LabelText = labelText;
                ValueText = valueText;
                Slider = slider;
            }

            /// <summary>The Core descriptor this row renders — the row's label,
            /// unit, range and read/write accessors all come from it.</summary>
            public TuningField Field { get; }

            public RectTransform RowRect { get; }
            public Text LabelText { get; }
            public Text ValueText { get; }
            public Slider Slider { get; }
        }

        /// <summary>One group: its header (name + Reset) and the control rows
        /// under it.</summary>
        public sealed class GroupSection
        {
            private readonly List<ControlRow> sectionRows = new List<ControlRow>();

            public GroupSection(TuningGroup group)
            {
                Group = group;
            }

            public TuningGroup Group { get; }
            public RectTransform HeaderRect { get; private set; }
            public RectTransform ResetButtonRect { get; private set; }
            public Text NameLabel { get; private set; }
            public IReadOnlyList<ControlRow> Rows => sectionRows;

            internal void Bind(RectTransform headerRect, RectTransform resetButtonRect, Text nameLabel)
            {
                HeaderRect = headerRect;
                ResetButtonRect = resetButtonRect;
                NameLabel = nameLabel;
            }

            internal void Add(ControlRow row)
            {
                sectionRows.Add(row);
            }
        }
    }
}
