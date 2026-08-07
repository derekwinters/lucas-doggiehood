using System.Linq;
using System.Reflection;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Tuning;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #622: the balance tuning overlay, asserted against the approved
    /// wireframe's named constants (docs/specs/ui/debug-tuning-menu.md /
    /// mockups/debug-tuning-menu.html, #161/#621). Per #656 it is part of the
    /// existing debug menu — present in every build and reached only through
    /// the 10-tap Debug unlock.
    ///
    /// The load-bearing property is that the panel is a pure <b>view</b> over
    /// Core: its slider set comes from <see cref="TuningCatalog"/> (never a
    /// hand-kept UI list that could drift from <see cref="TuningConfig"/>), a
    /// slider move writes straight into the active config so the Core seams
    /// read the new value live, and both reset scopes re-seed from the shipping
    /// defaults.
    /// </summary>
    public class TuningMenuOverlayTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private TuningMenuOverlay overlay;
        private TuningConfig configAtStart;

        [SetUp]
        public void CreateOverlay()
        {
            // #544: the modal-input gate is a process-global singleton; clear it
            // so a registration leaked by an earlier test can't make this
            // overlay's gate read as already blocking before it opens.
            ModalInputGate.Shared.Clear();

            // #291: labels bind a bundled UI font via Resources.Load; force-import
            // it so a fresh CI Library resolves it before the overlay is built
            // (docs/engineering/unity-serialization.md §4).
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            // TuningConfig.Active is process-global: snapshot it so a slider
            // moved here can never leak balance changes into another test.
            configAtStart = TuningConfig.Active;
            TuningConfig.Active = new TuningConfig();

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlay = TuningMenuOverlay.Create(canvasHost.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasHost);
            TuningConfig.Active = configAtStart;
            TuningConfig.ResetToDefaults();
            ModalInputGate.Shared.Clear();
        }

        private TuningMenuOverlay.ControlRow Row(string fieldName)
        {
            return overlay.Rows.Single(r => r.Field.FieldName == fieldName);
        }

        // ---------------------------------------------------------------
        // Checklist 1 — the slider set is built from Core, not hard-coded
        // ---------------------------------------------------------------

        [Test]
        public void SliderSet_IsBuiltFromTheCoreTuningCatalog()
        {
            CollectionAssert.AreEquivalent(
                TuningCatalog.Fields.Select(f => f.FieldName).ToList(),
                overlay.Rows.Select(r => r.Field.FieldName).ToList(),
                "the overlay must render exactly the Core catalog's tunables — no hand-kept UI list");
        }

        [Test]
        public void EveryRow_TakesItsLabelRangeAndValueFromCore()
        {
            foreach (var row in overlay.Rows)
            {
                var field = row.Field;

                Assert.That(row.LabelText.text, Is.EqualTo(field.Label), field.FieldName + ": label");
                Assert.That(row.Slider.minValue, Is.EqualTo((float)field.Min), field.FieldName + ": slider min");
                Assert.That(row.Slider.maxValue, Is.EqualTo((float)field.Max), field.FieldName + ": slider max");
                Assert.That(row.Slider.wholeNumbers, Is.EqualTo(field.IsInteger), field.FieldName + ": whole numbers");
                Assert.That((double)row.Slider.value,
                    Is.EqualTo(field.Read(TuningConfig.Active)).Within(1e-3),
                    field.FieldName + ": slider seeded from the active config");
                Assert.That(row.ValueText.text, Is.Not.Null.And.Not.Empty, field.FieldName + ": live value readout");
            }
        }

        [Test]
        public void Groups_AreTheWireframesFourGroupsHoldingTheirCatalogFields()
        {
            Assert.That(overlay.Groups.Select(g => g.Group).ToList(),
                Is.EqualTo(TuningCatalog.Groups.ToList()));

            foreach (var section in overlay.Groups)
            {
                Assert.That(section.NameLabel.text, Is.EqualTo(TuningCatalog.DisplayName(section.Group)));
                CollectionAssert.AreEqual(
                    TuningCatalog.FieldsIn(section.Group).Select(f => f.FieldName).ToList(),
                    section.Rows.Select(r => r.Field.FieldName).ToList(),
                    section.Group + ": group rows come from TuningCatalog.FieldsIn");
            }

            Assert.That(overlay.Groups.SelectMany(g => g.Rows).Count(), Is.EqualTo(overlay.Rows.Count));
        }

        [Test]
        public void ValueReadout_ShowsTheUnitDeclaredInCore()
        {
            var payout = Row(nameof(TuningConfig.QuestPayout));

            Assert.That(payout.Field.Unit, Is.EqualTo("coins"));
            Assert.That(payout.ValueText.text, Does.Contain("coins"));
            Assert.That(payout.ValueText.text, Does.Contain(TuningConfig.Active.QuestPayout.ToString()));
        }

        // ---------------------------------------------------------------
        // Checklist 2 — a slider move writes into the active TuningConfig
        // ---------------------------------------------------------------

        [Test]
        public void MovingASlider_WritesIntoTheActiveConfig_AndACoreSeamReadsIt()
        {
            var payout = Row(nameof(TuningConfig.QuestPayout));
            Assert.That(EconomyNumbers.QuestPayout, Is.EqualTo(new TuningConfig().QuestPayout),
                "precondition: the Core seam starts on the shipping default");

            payout.Slider.value = 44f;

            Assert.That(TuningConfig.Active.QuestPayout, Is.EqualTo(44));
            Assert.That(EconomyNumbers.QuestPayout, Is.EqualTo(44),
                "the Core seam re-reads TuningConfig.Active, so the change is live (#620)");
            Assert.That(payout.ValueText.text, Does.Contain("44"), "the live value readout follows the drag");
        }

        [Test]
        public void MovingASlider_WritesADecimalFieldAtItsDeclaredStep()
        {
            var markup = Row(nameof(TuningConfig.PaidQuestMarkup));

            markup.Slider.value = 2.5f;

            Assert.That(TuningConfig.Active.PaidQuestMarkup, Is.EqualTo(2.5d).Within(1e-9));
            Assert.That(EconomyNumbers.PaidQuestMarkup, Is.EqualTo(2.5d).Within(1e-9));
        }

        [Test]
        public void MovingASlider_ChangesAPricingSeamThatDependsOnIt()
        {
            // A second, non-economy seam so "live" is proven end to end and not
            // just for one field: tile-unlock pricing reads the same config.
            var baseCost = Row(nameof(TuningConfig.TileUnlockBaseCost));

            baseCost.Slider.value = 200f;

            Assert.That(TuningConfig.Active.TileUnlockBaseCost, Is.EqualTo(200));
            Assert.That(TileUnlockNumbers.BaseCost, Is.EqualTo(200));
        }

        [Test]
        public void ASliderPushedPastItsRange_ClampsToTheCoreDeclaredBounds()
        {
            var window = Row(nameof(TuningConfig.PacingWindowHours));

            window.Slider.value = (float)window.Field.Max + 100f;

            Assert.That((double)TuningConfig.Active.PacingWindowHours,
                Is.EqualTo(window.Field.Max).Within(1e-9));
        }

        // ---------------------------------------------------------------
        // Checklist 3 — both reset scopes restore the shipping defaults
        // ---------------------------------------------------------------

        [Test]
        public void ResetAll_RestoresEveryFieldAndEverySliderToShippingDefaults()
        {
            var defaults = new TuningConfig();
            foreach (var row in overlay.Rows)
            {
                row.Slider.value = (float)row.Field.Clamp(row.Field.Read(defaults) + row.Field.Step);
            }

            overlay.ResetAll();

            foreach (var row in overlay.Rows)
            {
                var expected = row.Field.Read(defaults);
                Assert.That(row.Field.Read(TuningConfig.Active), Is.EqualTo(expected).Within(1e-9),
                    row.Field.FieldName + ": config value restored");
                Assert.That((double)row.Slider.value, Is.EqualTo(expected).Within(1e-3),
                    row.Field.FieldName + ": slider redrawn at the default");
            }
        }

        [Test]
        public void ResetGroup_RestoresOnlyThatGroupsSliders()
        {
            var defaults = new TuningConfig();
            foreach (var row in overlay.Rows)
            {
                row.Slider.value = (float)row.Field.Clamp(row.Field.Read(defaults) + row.Field.Step);
            }

            overlay.ResetGroup(TuningGroup.Economy);

            foreach (var row in overlay.Rows)
            {
                var isEconomy = row.Field.Group == TuningGroup.Economy;
                var expected = isEconomy
                    ? row.Field.Read(defaults)
                    : row.Field.Clamp(row.Field.Read(defaults) + row.Field.Step);

                Assert.That(row.Field.Read(TuningConfig.Active), Is.EqualTo(expected).Within(1e-9),
                    row.Field.FieldName + ": per-group reset scope");
                Assert.That((double)row.Slider.value, Is.EqualTo(expected).Within(1e-3),
                    row.Field.FieldName + ": slider redrawn for its scope");
            }
        }

        [Test]
        public void EveryGroupHeader_CarriesItsOwnResetButton()
        {
            foreach (var section in overlay.Groups)
            {
                Assert.That(section.ResetButtonRect, Is.Not.Null, section.Group + ": per-group Reset button");
                Assert.That(section.ResetButtonRect.sizeDelta,
                    Is.EqualTo(new Vector2(TuningMenuOverlay.GroupResetButtonWidthPx, TuningMenuOverlay.ResetButtonHeightPx)));
            }
        }

        // ---------------------------------------------------------------
        // Checklist 4 — built in every build, gated by the 10-tap unlock alone
        // (#656; the Settings-side gate is asserted in SettingsPanelTests and
        // end-to-end in WorldBootstrapTuningMenuTests)
        // ---------------------------------------------------------------

        [Test]
        public void Create_BuildsThePanel_WithNoBuildConfigurationGate()
        {
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.PanelRect, Is.Not.Null);
            Assert.That(overlay.IsOpen, Is.False, "the overlay starts closed");

            // #656: the factory takes a parent and nothing else — there is no
            // dev-build flag left to inject.
            var create = typeof(TuningMenuOverlay).GetMethod(
                nameof(TuningMenuOverlay.Create), BindingFlags.Public | BindingFlags.Static);
            Assert.That(create, Is.Not.Null);
            Assert.That(create.GetParameters().Select(p => p.ParameterType).ToArray(),
                Is.EqualTo(new[] { typeof(Transform) }));
        }

        [Test]
        public void Create_AlwaysReturnsAnOverlay_NeverNull()
        {
            var second = TuningMenuOverlay.Create(canvasHost.transform);

            Assert.That(second, Is.Not.Null,
                "the overlay ships in every build — development, release-candidate and release");
            Object.DestroyImmediate(second.gameObject);
        }

        // ---------------------------------------------------------------
        // Checklist 5 — layout matches the approved wireframe
        // ---------------------------------------------------------------

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(TuningMenuOverlay.TuningPanelWidthPx, Is.EqualTo(1200f));
            Assert.That(TuningMenuOverlay.TuningPanelHeightPx, Is.EqualTo(920f));
            Assert.That(TuningMenuOverlay.TuningPanelPaddingPx, Is.EqualTo(32f));
            Assert.That(TuningMenuOverlay.TuningPanelRadiusPx, Is.EqualTo(16f));
            Assert.That(TuningMenuOverlay.TuningPanelOutlinePx, Is.EqualTo(2f));
            Assert.That(TuningMenuOverlay.HeaderHeightPx, Is.EqualTo(88f));
            Assert.That(TuningMenuOverlay.HeaderTitleFontPx, Is.EqualTo(40));
            Assert.That(TuningMenuOverlay.HeaderGapPx, Is.EqualTo(24f));
            Assert.That(TuningMenuOverlay.CloseButtonSizePx, Is.EqualTo(56f));
            Assert.That(TuningMenuOverlay.ResetAllButtonWidthPx, Is.EqualTo(220f));
            Assert.That(TuningMenuOverlay.ResetButtonHeightPx, Is.EqualTo(56f));
            Assert.That(TuningMenuOverlay.ScrollBodyPaddingRightPx, Is.EqualTo(24f));
            Assert.That(TuningMenuOverlay.ScrollbarWidthPx, Is.EqualTo(12f));
            Assert.That(TuningMenuOverlay.GroupGapPx, Is.EqualTo(28f));
            Assert.That(TuningMenuOverlay.GroupHeaderHeightPx, Is.EqualTo(64f));
            Assert.That(TuningMenuOverlay.GroupHeaderFontPx, Is.EqualTo(32));
            Assert.That(TuningMenuOverlay.GroupResetButtonWidthPx, Is.EqualTo(150f));
            Assert.That(TuningMenuOverlay.ControlRowHeightPx, Is.EqualTo(96f));
            Assert.That(TuningMenuOverlay.ControlRowGapPx, Is.EqualTo(12f));
            Assert.That(TuningMenuOverlay.ControlRowPaddingXPx, Is.EqualTo(24f));
            Assert.That(TuningMenuOverlay.ControlLabelFontPx, Is.EqualTo(28));
            Assert.That(TuningMenuOverlay.ControlValueFontPx, Is.EqualTo(30));
            Assert.That(TuningMenuOverlay.SliderTrackHeightPx, Is.EqualTo(12f));
            Assert.That(TuningMenuOverlay.SliderTrackRadiusPx, Is.EqualTo(6f));
            Assert.That(TuningMenuOverlay.SliderKnobPx, Is.EqualTo(40f));
            Assert.That(TuningMenuOverlay.SliderLabelValueGapPx, Is.EqualTo(8f));
            Assert.That(TuningMenuOverlay.EntryRowHeightPx, Is.EqualTo(96f));
            Assert.That(TuningMenuOverlay.EntryRowHeightPx, Is.EqualTo(SettingsPanel.DebugRowHeightPx),
                "the Debug-tab entry row reuses the Settings debug-row metric");
        }

        [Test]
        public void Panel_IsCenteredAtTheWireframeSize()
        {
            Assert.That(overlay.PanelRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(overlay.PanelRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(overlay.PanelRect.sizeDelta,
                Is.EqualTo(new Vector2(TuningMenuOverlay.TuningPanelWidthPx, TuningMenuOverlay.TuningPanelHeightPx)));
            Assert.That(overlay.PanelRect.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Scrim_CoversTheWholeScreenBehindThePanel()
        {
            Assert.That(overlay.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(overlay.ScrimRect.GetSiblingIndex(),
                Is.LessThan(overlay.PanelRect.GetSiblingIndex()),
                "the scrim draws behind the panel");
        }

        [Test]
        public void Header_IsPinnedAndCarriesResetAllAndClose()
        {
            Assert.That(overlay.HeaderRect.sizeDelta.y, Is.EqualTo(TuningMenuOverlay.HeaderHeightPx));
            Assert.That(overlay.HeaderRect.anchorMin.y, Is.EqualTo(1f));
            Assert.That(overlay.HeaderRect.anchorMax.y, Is.EqualTo(1f));

            Assert.That(overlay.CloseButtonRect.sizeDelta,
                Is.EqualTo(new Vector2(TuningMenuOverlay.CloseButtonSizePx, TuningMenuOverlay.CloseButtonSizePx)));
            Assert.That(overlay.ResetAllButtonRect.sizeDelta,
                Is.EqualTo(new Vector2(TuningMenuOverlay.ResetAllButtonWidthPx, TuningMenuOverlay.ResetButtonHeightPx)));

            // The header is NOT inside the scrolling body — it stays put.
            Assert.That(overlay.HeaderRect.IsChildOf(overlay.ScrollContentRect), Is.False);
        }

        [Test]
        public void Body_ScrollsVerticallyWithAContentTallerThanItsViewport()
        {
            Assert.That(overlay.Scroll.vertical, Is.True);
            Assert.That(overlay.Scroll.horizontal, Is.False);
            Assert.That(overlay.Scroll.content, Is.SameAs(overlay.ScrollContentRect));
            Assert.That(overlay.Scroll.viewport, Is.Not.Null);
            Assert.That(overlay.ScrollContentRect.sizeDelta.y,
                Is.GreaterThan(TuningMenuOverlay.TuningPanelHeightPx),
                "37 tunables overflow the fixed-height panel, so the body must scroll");
            Assert.That(overlay.VerticalScrollbar, Is.Not.Null);
            Assert.That(overlay.VerticalScrollbar.GetComponent<RectTransform>().sizeDelta.x,
                Is.EqualTo(TuningMenuOverlay.ScrollbarWidthPx));
        }

        [Test]
        public void ControlRows_AreTheWireframeSize()
        {
            foreach (var row in overlay.Rows)
            {
                Assert.That(row.RowRect.sizeDelta.y, Is.EqualTo(TuningMenuOverlay.ControlRowHeightPx),
                    row.Field.FieldName + ": control-row height");
                Assert.That(row.LabelText.fontSize, Is.EqualTo(TuningMenuOverlay.ControlLabelFontPx));
                Assert.That(row.ValueText.fontSize, Is.EqualTo(TuningMenuOverlay.ControlValueFontPx));
                // Slider stretches its handle across the slide area's cross
                // axis, so the knob's height is the area's (SliderKnobPx) and
                // only the width is carried on the handle itself.
                Assert.That(row.Slider.handleRect.sizeDelta,
                    Is.EqualTo(new Vector2(TuningMenuOverlay.SliderKnobPx, 0f)),
                    row.Field.FieldName + ": slider knob width");
                Assert.That(((RectTransform)row.Slider.handleRect.parent).sizeDelta.y,
                    Is.EqualTo(TuningMenuOverlay.SliderKnobPx),
                    row.Field.FieldName + ": slider knob slide-area height");
                Assert.That(row.Slider.fillRect, Is.Not.Null, row.Field.FieldName + ": slider fill");
                Assert.That(row.Slider.direction, Is.EqualTo(Slider.Direction.LeftToRight));
            }
        }

        [Test]
        public void Panel_KeepsItsUtilitarianChrome_NotCandyCottage()
        {
            // docs/specs/ui/debug-tuning-menu.md: this screen is deliberately
            // exempt from the shared Candy Cottage chrome — a thin outline
            // (2px, not the shared 6) and no hard drop-shadow.
            Assert.That(overlay.PanelOutlineRect, Is.Not.Null);
            Assert.That(overlay.PanelOutlineRect.offsetMin.x,
                Is.EqualTo(overlay.PanelRect.offsetMin.x - TuningMenuOverlay.TuningPanelOutlinePx).Within(1e-3));
            Assert.That(TuningMenuOverlay.TuningPanelOutlinePx,
                Is.LessThan(CandyChromeUgui.OutlineThicknessPx));
            Assert.That(overlay.PanelRect.GetComponent<Shadow>(), Is.Null,
                "no Candy Cottage hard drop-shadow on the dev tool");
        }

        // ---------------------------------------------------------------
        // Checklist 6 — open/close disturbs nothing but the tuned values
        // ---------------------------------------------------------------

        [Test]
        public void Open_RegistersWithTheSharedModalGate_Close_Unregisters()
        {
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False);

            overlay.Open();
            Assert.That(overlay.IsOpen, Is.True);
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.True,
                "the tuning panel is modal — taps must not leak to Settings or the world");

            overlay.Close();
            Assert.That(overlay.IsOpen, Is.False);
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False);
        }

        [Test]
        public void OpeningAndClosing_LeavesEveryTunedValueExactlyAsItWas()
        {
            var payout = Row(nameof(TuningConfig.QuestPayout));
            payout.Slider.value = 77f;
            var snapshot = TuningCatalog.Fields.ToDictionary(f => f.FieldName, f => f.Read(TuningConfig.Active));

            overlay.Open();
            overlay.Close();
            overlay.Open();
            overlay.Close();

            foreach (var field in TuningCatalog.Fields)
            {
                Assert.That(field.Read(TuningConfig.Active), Is.EqualTo(snapshot[field.FieldName]).Within(1e-9),
                    field.FieldName + ": open/close must not move a tuned value");
            }

            Assert.That(TuningConfig.Active.QuestPayout, Is.EqualTo(77),
                "an override survives closing and re-opening the panel");
        }

        [Test]
        public void Reopening_RedrawsFromTheLiveConfig()
        {
            // A value changed elsewhere (another debug affordance, a later
            // issue's seam) must show up when the panel is re-opened, because
            // the panel reads Core rather than caching its own copy.
            TuningConfig.Active.QuestPayout = 130;

            overlay.Open();

            Assert.That((double)Row(nameof(TuningConfig.QuestPayout)).Slider.value,
                Is.EqualTo(130d).Within(1e-3));
            Assert.That(Row(nameof(TuningConfig.QuestPayout)).ValueText.text, Does.Contain("130"));
        }

        [Test]
        public void ClosingViaTheScrim_DoesNotResetAnything()
        {
            var payout = Row(nameof(TuningConfig.QuestPayout));
            payout.Slider.value = 55f;

            overlay.Open();
            overlay.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(overlay.IsOpen, Is.False);
            Assert.That(TuningConfig.Active.QuestPayout, Is.EqualTo(55));
        }
    }
}
