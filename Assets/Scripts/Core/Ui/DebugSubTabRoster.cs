using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Ui
{
    /// <summary>#716: the Debug pane's sub-tabs — the named groups the Debug rows
    /// are split across so no single list outgrows the pane
    /// (docs/specs/ui/settings.md).</summary>
    public enum DebugSubTab
    {
        General = 0,
        VisualsAndTools = 1,
        Reports = 2,
    }

    /// <summary>
    /// #716: which Debug row belongs to which Debug sub-tab, as engine-free data.
    ///
    /// <para>The Debug pane was a single flat list that fit exactly five rows,
    /// and the sixth had nowhere on screen to go. The approved wireframe groups
    /// the rows behind a sub-tab bar; this roster is that grouping, kept in Core
    /// so the rules that matter — every row reachable from exactly one sub-tab,
    /// and no sub-tab holding more rows than
    /// <see cref="SettingsDebugPaneMetrics.RowCapacity"/> allows — are asserted
    /// as arithmetic, including for rows whose Unity half has not been built
    /// yet.</para>
    ///
    /// <para><see cref="PendingRows"/> exists for exactly that case: a row the
    /// wireframe places before its buttons are built. #692's two bug-report rows
    /// were the original occupants; that issue built them, so nothing is pending
    /// today and <see cref="BuiltRows"/> is every row. The mechanism stays for
    /// the next row that lands structure-first.</para>
    ///
    /// <para>#695 added <see cref="ShareBugReportRow"/> alongside its Unity half,
    /// which takes <b>Reports</b> to its full three-row capacity.</para>
    /// </summary>
    public static class DebugSubTabRoster
    {
        // --- sub-tab labels (the pill copy) ---
        public const string GeneralLabel = "General";
        public const string VisualsAndToolsLabel = "Visuals & Tools";
        public const string ReportsLabel = "Reports";

        // --- row keys: stable ids, independent of the on-screen row copy ---
        public const string ShowBackyardFencesRow = "show-backyard-fences";
        public const string AddCoinsRow = "add-coins";
        public const string RefreshQuestsRow = "refresh-quests-now";
        public const string ShowDebugElementColorsRow = "show-debug-element-colors";
        public const string TuneBalanceRow = "tune-balance";
        public const string CopyBugReportRow = "copy-bug-report";
        public const string SaveBugReportRow = "save-bug-report";
        public const string ShareBugReportRow = "share-bug-report";

        private static readonly DebugSubTab[] TabOrder =
        {
            DebugSubTab.General,
            DebugSubTab.VisualsAndTools,
            DebugSubTab.Reports,
        };

        private static readonly string[] GeneralRows =
        {
            ShowBackyardFencesRow,
            AddCoinsRow,
            RefreshQuestsRow,
        };

        private static readonly string[] VisualsAndToolsRows =
        {
            ShowDebugElementColorsRow,
            TuneBalanceRow,
        };

        /// <summary>The Reports group, now <b>full</b>: three rows is exactly
        /// <see cref="SettingsDebugPaneMetrics.RowCapacity"/> for today's
        /// constants (#695). The next reporting affordance does not go here —
        /// per docs/specs/ui/settings.md a full group's fix is a new sub-tab,
        /// which is a wireframe decision, not a code one.</summary>
        private static readonly string[] ReportsRows =
        {
            CopyBugReportRow,
            SaveBugReportRow,
            ShareBugReportRow,
        };

        /// <summary>Rows placed by the wireframe whose Unity half has not landed
        /// yet. Empty since #692 built the two bug-report rows.</summary>
        private static readonly string[] Pending = new string[0];

        /// <summary>The sub-tabs, left to right along the bar.</summary>
        public static IReadOnlyList<DebugSubTab> Order => TabOrder;

        /// <summary>Every Debug row the wireframe places, in sub-tab order.</summary>
        public static IReadOnlyList<string> AllRows { get; } = BuildAllRows();

        /// <summary>Rows whose Unity half has not landed yet — empty today
        /// (#692 built the last two). A pending row still occupies its sub-tab
        /// here, so the capacity invariant accounts for it before it exists.</summary>
        public static IReadOnlyList<string> PendingRows => Pending;

        /// <summary>The rows the Unity layer builds today — every row that is not
        /// <see cref="PendingRows"/>.</summary>
        public static IReadOnlyList<string> BuiltRows { get; } = BuildBuiltRows();

        /// <summary>The pill copy for one sub-tab.</summary>
        public static string LabelOf(DebugSubTab tab)
        {
            switch (tab)
            {
                case DebugSubTab.General:
                    return GeneralLabel;
                case DebugSubTab.VisualsAndTools:
                    return VisualsAndToolsLabel;
                case DebugSubTab.Reports:
                    return ReportsLabel;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tab));
            }
        }

        /// <summary>The rows one sub-tab shows, top to bottom.</summary>
        public static IReadOnlyList<string> RowsIn(DebugSubTab tab)
        {
            switch (tab)
            {
                case DebugSubTab.General:
                    return GeneralRows;
                case DebugSubTab.VisualsAndTools:
                    return VisualsAndToolsRows;
                case DebugSubTab.Reports:
                    return ReportsRows;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tab));
            }
        }

        /// <summary>The sub-tab that owns <paramref name="rowKey"/>. Throws for a
        /// row no sub-tab claims — an unplaced row would be invisible, which is
        /// the failure #716 exists to make loud.</summary>
        public static DebugSubTab GroupOf(string rowKey)
        {
            foreach (var tab in TabOrder)
            {
                foreach (var row in RowsIn(tab))
                {
                    if (row == rowKey)
                    {
                        return tab;
                    }
                }
            }

            throw new ArgumentException(
                "No Debug sub-tab claims the row '" + rowKey + "'.", nameof(rowKey));
        }

        /// <summary>Whether <paramref name="rowKey"/> is built by the Unity layer
        /// today (as opposed to placed but still pending, #692).</summary>
        public static bool IsPending(string rowKey)
        {
            foreach (var row in Pending)
            {
                if (row == rowKey)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] BuildAllRows()
        {
            var rows = new List<string>();
            foreach (var tab in TabOrder)
            {
                rows.AddRange(RowsIn(tab));
            }

            return rows.ToArray();
        }

        private static string[] BuildBuiltRows()
        {
            var rows = new List<string>();
            foreach (var row in AllRows)
            {
                if (!IsPending(row))
                {
                    rows.Add(row);
                }
            }

            return rows.ToArray();
        }
    }
}
