using System.Linq;
using Doggiehood.Core.Ui;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Ui
{
    /// <summary>
    /// #716: which Debug row belongs to which Debug sub-tab. The approved
    /// wireframe (docs/specs/ui/settings.md) splits today's five rows plus
    /// #692's two incoming bug-report rows across three groups — General,
    /// Visuals &amp; Tools, Reports — so no group exceeds the pane's computed
    /// capacity. The roster is engine-free data so the "none dropped, none
    /// duplicated, none overflowing" rules hold for rows whose Unity half has
    /// not been built yet.
    /// </summary>
    public class DebugSubTabRosterTests
    {
        // Today's approved pane box (docs/specs/ui/settings.md), used to check
        // the roster against the real capacity rather than a typed number.
        private static SettingsDebugPaneMetrics ApprovedPane()
        {
            return new SettingsDebugPaneMetrics(
                panelHeightPx: 820f,
                bodyTopInsetPx: 152f,
                panelPaddingPx: 48f,
                paneInsetPx: 48f,
                rowHeightPx: 96f,
                rowGapPx: 20f,
                subTabHeightPx: 72f,
                subTabBarGapPx: 20f);
        }

        [Test]
        public void ThereAreThreeSubTabs_InTheApprovedOrder()
        {
            Assert.That(DebugSubTabRoster.Order,
                Is.EqualTo(new[] { DebugSubTab.General, DebugSubTab.VisualsAndTools, DebugSubTab.Reports }));
        }

        [Test]
        public void EachSubTab_CarriesItsApprovedLabel()
        {
            Assert.That(DebugSubTabRoster.LabelOf(DebugSubTab.General), Is.EqualTo("General"));
            Assert.That(DebugSubTabRoster.LabelOf(DebugSubTab.VisualsAndTools), Is.EqualTo("Visuals & Tools"));
            Assert.That(DebugSubTabRoster.LabelOf(DebugSubTab.Reports), Is.EqualTo("Reports"));
        }

        [Test]
        public void TheGroups_AreTheApprovedSplitOfTheSevenRows()
        {
            Assert.That(DebugSubTabRoster.RowsIn(DebugSubTab.General), Is.EqualTo(new[]
            {
                DebugSubTabRoster.ShowBackyardFencesRow,
                DebugSubTabRoster.AddCoinsRow,
                DebugSubTabRoster.RefreshQuestsRow,
            }));

            Assert.That(DebugSubTabRoster.RowsIn(DebugSubTab.VisualsAndTools), Is.EqualTo(new[]
            {
                DebugSubTabRoster.ShowDebugElementColorsRow,
                DebugSubTabRoster.TuneBalanceRow,
            }));

            Assert.That(DebugSubTabRoster.RowsIn(DebugSubTab.Reports), Is.EqualTo(new[]
            {
                DebugSubTabRoster.CopyBugReportRow,
                DebugSubTabRoster.SaveBugReportRow,
            }));
        }

        [Test]
        public void EveryRow_IsReachableFromExactlyOneSubTab_NoneDroppedNoneDuplicated()
        {
            var placed = DebugSubTabRoster.Order
                .SelectMany(tab => DebugSubTabRoster.RowsIn(tab))
                .ToList();

            Assert.That(placed, Is.EquivalentTo(DebugSubTabRoster.AllRows),
                "every known Debug row lands in the sub-tabs — none dropped");
            Assert.That(placed.Distinct().Count(), Is.EqualTo(placed.Count),
                "no row is listed under two sub-tabs — none duplicated");
        }

        [Test]
        public void GroupOf_AnswersWithTheSubTabThatOwnsTheRow()
        {
            Assert.That(DebugSubTabRoster.GroupOf(DebugSubTabRoster.AddCoinsRow),
                Is.EqualTo(DebugSubTab.General));
            Assert.That(DebugSubTabRoster.GroupOf(DebugSubTabRoster.TuneBalanceRow),
                Is.EqualTo(DebugSubTab.VisualsAndTools));
            Assert.That(DebugSubTabRoster.GroupOf(DebugSubTabRoster.SaveBugReportRow),
                Is.EqualTo(DebugSubTab.Reports));
        }

        [Test]
        public void GroupOf_RejectsARowNoSubTabClaims()
        {
            Assert.That(() => DebugSubTabRoster.GroupOf("not-a-debug-row"),
                Throws.ArgumentException,
                "an unplaced row must be a loud failure, not a silently invisible button");
        }

        [Test]
        public void NoSubTab_HoldsMoreRowsThanThePaneCanShow()
        {
            // The #716 invariant, enforced on the roster itself: a group that
            // grows past the computed capacity fails here, before anything is
            // ever rendered off the panel.
            var metrics = ApprovedPane();

            foreach (var tab in DebugSubTabRoster.Order)
            {
                Assert.That(metrics.Fits(DebugSubTabRoster.RowsIn(tab).Count), Is.True,
                    $"the {DebugSubTabRoster.LabelOf(tab)} sub-tab holds more rows than the Debug pane can show");
            }
        }

        [Test]
        public void TheSevenRows_DoNotFitInASingleFlatList_WhichIsWhySubTabsExist()
        {
            Assert.That(ApprovedPane().Fits(DebugSubTabRoster.AllRows.Count), Is.False,
                "seven rows in one list is exactly the overflow #716 was filed for");
        }

        [Test]
        public void TheBugReportRows_AreMarkedPending_BecauseTheirUnityHalfLandsWith692()
        {
            Assert.That(DebugSubTabRoster.PendingRows, Is.EquivalentTo(new[]
            {
                DebugSubTabRoster.CopyBugReportRow,
                DebugSubTabRoster.SaveBugReportRow,
            }));

            foreach (var row in DebugSubTabRoster.PendingRows)
            {
                Assert.That(DebugSubTabRoster.AllRows, Contains.Item(row),
                    "a pending row still has a home in the roster — that is the point");
            }
        }

        [Test]
        public void TheBuiltRows_AreEveryRowThatIsNotPending()
        {
            Assert.That(DebugSubTabRoster.BuiltRows,
                Is.EqualTo(DebugSubTabRoster.AllRows.Except(DebugSubTabRoster.PendingRows).ToArray()),
                "the Unity layer builds exactly the non-pending rows today");
        }
    }
}
