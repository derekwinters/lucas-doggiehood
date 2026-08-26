using Doggiehood.Core.Ui;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Ui
{
    /// <summary>
    /// #716: the Settings Debug pane's row capacity, computed instead of guessed.
    ///
    /// <para>The flat Debug list ran out of room at exactly five rows, and its
    /// fifth only fit by spending the Settings panel's incidental bottom margin —
    /// invisible until a sixth row had nowhere on screen to go. The approved
    /// wireframe (docs/specs/ui/settings.md) splits the pane into sub-tabs and
    /// derives each group's capacity <b>strictly inside the Debug pane's own
    /// box</b>, so the number stays correct if the panel or row metrics ever
    /// change rather than being hand-typed.</para>
    /// </summary>
    public class SettingsDebugPaneMetricsTests
    {
        // The approved wireframe values (docs/specs/ui/settings.md), named here so
        // the fixture reads as the spec it locks rather than as bare numbers.
        private const float SettingsPanelHeightPx = 820f;
        private const float SettingsPanelPaddingPx = 48f;
        private const float BodyTopInsetPx = 152f;   // panel padding + title band
        private const float PaneInsetPx = 48f;       // Debug pane inset inside the content pane
        private const float DebugRowHeightPx = 96f;
        private const float DebugRowGapPx = 20f;
        private const float DebugSubTabHeightPx = 72f;
        private const float DebugSubTabGapPx = 16f;
        private const float DebugSubTabBarGapPx = 20f;

        private const int ApprovedRowCapacity = 3;

        private static SettingsDebugPaneMetrics Approved()
        {
            return new SettingsDebugPaneMetrics(
                panelHeightPx: SettingsPanelHeightPx,
                bodyTopInsetPx: BodyTopInsetPx,
                panelPaddingPx: SettingsPanelPaddingPx,
                paneInsetPx: PaneInsetPx,
                rowHeightPx: DebugRowHeightPx,
                rowGapPx: DebugRowGapPx,
                subTabHeightPx: DebugSubTabHeightPx,
                subTabBarGapPx: DebugSubTabBarGapPx);
        }

        [Test]
        public void PaneHeight_IsThePanelMinusItsTitleBandAndInsets()
        {
            // 820 - 152 (title band + top padding) - 48 (bottom padding) - 2*48 = 524
            Assert.That(Approved().PaneHeightPx, Is.EqualTo(524f));
        }

        [Test]
        public void TheSubTabBarBlock_IsTheBarPlusItsGapAboveTheFirstRow()
        {
            Assert.That(Approved().SubTabBarBlockHeightPx,
                Is.EqualTo(DebugSubTabHeightPx + DebugSubTabBarGapPx),
                "the bar takes its own height plus one row-gap off the top of the pane");
        }

        [Test]
        public void RowCapacity_IsThreeForTodaysApprovedConstants()
        {
            Assert.That(Approved().RowCapacity, Is.EqualTo(ApprovedRowCapacity),
                "524px of pane, less the 92px sub-tab block, holds three 96px rows at a 20px gap");
        }

        [Test]
        public void TheLastRowThatFits_EndsInsideThePaneBox_AndTheNextOneDoesNot()
        {
            var metrics = Approved();

            Assert.That(metrics.RowBottomEdgeFromPaneTopPx(metrics.RowCapacity - 1),
                Is.LessThanOrEqualTo(metrics.PaneHeightPx),
                "the last row that fits ends inside the Debug pane's own box");
            Assert.That(metrics.RowBottomEdgeFromPaneTopPx(metrics.RowCapacity),
                Is.GreaterThan(metrics.PaneHeightPx),
                "one row past capacity hangs off the pane — the failure #716 exists to catch");
        }

        [Test]
        public void Fits_AcceptsCapacityAndRejectsOneMore()
        {
            var metrics = Approved();

            Assert.That(metrics.Fits(metrics.RowCapacity), Is.True);
            Assert.That(metrics.Fits(metrics.RowCapacity + 1), Is.False,
                "a group seeded with one row too many must fail, not merely look tight");
        }

        [Test]
        public void EveryRowThatFits_AlsoEndsInsideTheSettingsPanelItself()
        {
            // The invariant is stated against SettingsPanelHeightPx: a row that
            // does not render inside the panel is unreachable, not just tight.
            var metrics = Approved();

            for (var i = 0; i < metrics.RowCapacity; i++)
            {
                Assert.That(BodyTopInsetPx + PaneInsetPx + metrics.RowBottomEdgeFromPaneTopPx(i),
                    Is.LessThanOrEqualTo(SettingsPanelHeightPx),
                    $"row {i} must render fully inside the Settings panel");
            }
        }

        [Test]
        public void Capacity_IsDerived_NotHardCoded_SoATallerPanelHoldsMore()
        {
            var taller = new SettingsDebugPaneMetrics(
                panelHeightPx: SettingsPanelHeightPx + DebugRowHeightPx + DebugRowGapPx,
                bodyTopInsetPx: BodyTopInsetPx,
                panelPaddingPx: SettingsPanelPaddingPx,
                paneInsetPx: PaneInsetPx,
                rowHeightPx: DebugRowHeightPx,
                rowGapPx: DebugRowGapPx,
                subTabHeightPx: DebugSubTabHeightPx,
                subTabBarGapPx: DebugSubTabBarGapPx);

            Assert.That(taller.RowCapacity, Is.EqualTo(ApprovedRowCapacity + 1),
                "capacity tracks the constants; it is computed, never a typed 3");
        }

        [Test]
        public void Capacity_ShrinksWhenTheSubTabBarGrows()
        {
            var fatBar = new SettingsDebugPaneMetrics(
                panelHeightPx: SettingsPanelHeightPx,
                bodyTopInsetPx: BodyTopInsetPx,
                panelPaddingPx: SettingsPanelPaddingPx,
                paneInsetPx: PaneInsetPx,
                rowHeightPx: DebugRowHeightPx,
                rowGapPx: DebugRowGapPx,
                subTabHeightPx: DebugSubTabHeightPx + DebugRowHeightPx + DebugRowGapPx,
                subTabBarGapPx: DebugSubTabBarGapPx);

            Assert.That(fatBar.RowCapacity, Is.EqualTo(ApprovedRowCapacity - 1),
                "the bar is paid for out of the row budget, not out of the panel's margin");
        }

        [Test]
        public void APaneWithNoRoomForASingleRow_HasZeroCapacity()
        {
            var tiny = new SettingsDebugPaneMetrics(
                panelHeightPx: BodyTopInsetPx + SettingsPanelPaddingPx + 2f * PaneInsetPx
                    + DebugSubTabHeightPx + DebugSubTabBarGapPx,
                bodyTopInsetPx: BodyTopInsetPx,
                panelPaddingPx: SettingsPanelPaddingPx,
                paneInsetPx: PaneInsetPx,
                rowHeightPx: DebugRowHeightPx,
                rowGapPx: DebugRowGapPx,
                subTabHeightPx: DebugSubTabHeightPx,
                subTabBarGapPx: DebugSubTabBarGapPx);

            Assert.That(tiny.RowCapacity, Is.EqualTo(0));
            Assert.That(tiny.Fits(1), Is.False);
        }

    }
}
