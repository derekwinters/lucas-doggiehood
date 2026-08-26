using System;

namespace Doggiehood.Core.Ui
{
    /// <summary>
    /// #716: the engine-free geometry of the Settings <b>Debug</b> pane — how many
    /// action/toggle rows one Debug sub-tab can hold, derived from the approved
    /// wireframe's own constants (docs/specs/ui/settings.md) rather than counted
    /// by eye.
    ///
    /// <para>The flat Debug list ran out of room at exactly five rows, and its
    /// fifth only fit by spending the Settings panel's incidental bottom margin —
    /// harmless-looking until a sixth row had nowhere on screen to go. Adding the
    /// sub-tab bar makes that worse, not better, if capacity is still guessed:
    /// the bar costs <c>DebugSubTabHeightPx + DebugSubTabBarGapPx</c> off the top
    /// of the pane before any row starts. So capacity is computed here, strictly
    /// inside the Debug pane's <b>own</b> box, and asserted by tests in both
    /// assemblies — a group holding one row too many fails the suite instead of
    /// floating a button over the scrim.</para>
    ///
    /// <para>Pure arithmetic, no engine types (rule #2). The Unity
    /// <c>SettingsPanel</c> feeds it the panel's named layout constants; nothing
    /// here knows what a RectTransform is.</para>
    /// </summary>
    public readonly struct SettingsDebugPaneMetrics
    {
        /// <summary>Row counts below this are never valid — a pane that cannot
        /// show a single row has no capacity at all.</summary>
        private const int NoRows = 0;

        /// <param name="panelHeightPx">The Settings panel's own height
        /// (<c>SettingsPanelHeightPx</c>) — the outer bound the invariant is
        /// stated against.</param>
        /// <param name="bodyTopInsetPx">Panel padding + title band above the
        /// content body.</param>
        /// <param name="panelPaddingPx">The panel inset below the content
        /// pane.</param>
        /// <param name="paneInsetPx">The Debug pane's own inset inside the
        /// content pane, applied at the top and the bottom.</param>
        /// <param name="rowHeightPx">One Debug row (<c>DebugRowHeightPx</c>).</param>
        /// <param name="rowGapPx">Gap between stacked rows (<c>DebugRowGapPx</c>).</param>
        /// <param name="subTabHeightPx">The sub-tab bar's pill height
        /// (<c>DebugSubTabHeightPx</c>).</param>
        /// <param name="subTabBarGapPx">Gap between the bar and the first row
        /// (<c>DebugSubTabBarGapPx</c>).</param>
        public SettingsDebugPaneMetrics(
            float panelHeightPx,
            float bodyTopInsetPx,
            float panelPaddingPx,
            float paneInsetPx,
            float rowHeightPx,
            float rowGapPx,
            float subTabHeightPx,
            float subTabBarGapPx)
        {
            PanelHeightPx = panelHeightPx;
            BodyTopInsetPx = bodyTopInsetPx;
            PanelPaddingPx = panelPaddingPx;
            PaneInsetPx = paneInsetPx;
            RowHeightPx = rowHeightPx;
            RowGapPx = rowGapPx;
            SubTabHeightPx = subTabHeightPx;
            SubTabBarGapPx = subTabBarGapPx;
        }

        public float PanelHeightPx { get; }

        public float BodyTopInsetPx { get; }

        public float PanelPaddingPx { get; }

        public float PaneInsetPx { get; }

        public float RowHeightPx { get; }

        public float RowGapPx { get; }

        public float SubTabHeightPx { get; }

        public float SubTabBarGapPx { get; }

        /// <summary>The Debug pane's usable height: the panel less its title band,
        /// its bottom padding, and the pane's own top+bottom insets.</summary>
        public float PaneHeightPx =>
            PanelHeightPx - BodyTopInsetPx - PanelPaddingPx - (PaneInsetPx + PaneInsetPx);

        /// <summary>What the sub-tab bar costs before the first row can start:
        /// its pill height plus the one row-gap that separates it from the
        /// list.</summary>
        public float SubTabBarBlockHeightPx => SubTabHeightPx + SubTabBarGapPx;

        /// <summary>The vertical budget left for rows once the bar is paid
        /// for.</summary>
        public float RowsAvailableHeightPx => PaneHeightPx - SubTabBarBlockHeightPx;

        /// <summary>How many rows one sub-tab may hold, computed strictly inside
        /// the pane's own box — the panel's incidental bottom margin is
        /// deliberately not spent (that slack is what hid #716 for five rows).</summary>
        public int RowCapacity
        {
            get
            {
                var available = RowsAvailableHeightPx;
                if (available < RowHeightPx)
                {
                    return NoRows;
                }

                // n rows occupy n*height + (n-1)*gap, i.e. n*(height+gap) - gap.
                return (int)Math.Floor((available + RowGapPx) / (RowHeightPx + RowGapPx));
            }
        }

        /// <summary>The bottom edge of the row at <paramref name="rowIndex"/>
        /// (0-based within its sub-tab), measured down from the Debug pane's
        /// top.</summary>
        public float RowBottomEdgeFromPaneTopPx(int rowIndex)
        {
            if (rowIndex < NoRows)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }

            return SubTabBarBlockHeightPx
                + rowIndex * (RowHeightPx + RowGapPx)
                + RowHeightPx;
        }

        /// <summary>Whether a sub-tab holding <paramref name="rowCount"/> rows
        /// renders entirely inside the pane.</summary>
        public bool Fits(int rowCount)
        {
            return rowCount >= NoRows && rowCount <= RowCapacity;
        }
    }
}
