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
                DebugSubTabRoster.ShareBugReportRow,
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
        public void EveryRow_DoesNotFitInASingleFlatList_WhichIsWhySubTabsExist()
        {
            Assert.That(ApprovedPane().Fits(DebugSubTabRoster.AllRows.Count), Is.False,
                "the whole roster in one list is exactly the overflow #716 was filed for");
        }

        [Test]
        public void Invariant_TheReportsSubTab_IsNowFull_SoAFourthRowNeedsAWireframeDecision()
        {
            // #695's Share bug report row is the Reports group's third and last:
            // the pane shows exactly ApprovedPane().RowCapacity rows. This is
            // asserted rather than left as a comment because the next reporting
            // affordance must NOT be squeezed in here — per
            // docs/specs/ui/settings.md, a full group's fix is a new sub-tab,
            // which is a wireframe decision (CLAUDE.md rule #8), not a code one.
            var metrics = ApprovedPane();

            Assert.That(DebugSubTabRoster.RowsIn(DebugSubTab.Reports).Count,
                Is.EqualTo(metrics.RowCapacity),
                "Reports is at capacity");
            Assert.That(metrics.Fits(DebugSubTabRoster.RowsIn(DebugSubTab.Reports).Count + 1), Is.False,
                "a fourth Reports row would hang off the Debug pane");
        }

        [Test]
        public void TheBugReportRows_AreBuiltRowsNow_BecauseTheirUnityHalfLandedWith692And695()
        {
            // #716 placed the first two and marked them pending; #692 built them
            // and #695 added the third built alongside its Unity half, so nothing
            // is pending any more.
            Assert.That(DebugSubTabRoster.PendingRows, Is.Empty,
                "every placed Debug row now has a Unity half");
            Assert.That(DebugSubTabRoster.IsPending(DebugSubTabRoster.CopyBugReportRow), Is.False);
            Assert.That(DebugSubTabRoster.IsPending(DebugSubTabRoster.SaveBugReportRow), Is.False);
            Assert.That(DebugSubTabRoster.IsPending(DebugSubTabRoster.ShareBugReportRow), Is.False);

            Assert.That(DebugSubTabRoster.BuiltRows, Contains.Item(DebugSubTabRoster.CopyBugReportRow));
            Assert.That(DebugSubTabRoster.BuiltRows, Contains.Item(DebugSubTabRoster.SaveBugReportRow));
            Assert.That(DebugSubTabRoster.BuiltRows, Contains.Item(DebugSubTabRoster.ShareBugReportRow));
        }

        [Test]
        public void TheBuiltRows_AreEveryRowThatIsNotPending()
        {
            Assert.That(DebugSubTabRoster.BuiltRows,
                Is.EqualTo(DebugSubTabRoster.AllRows.Except(DebugSubTabRoster.PendingRows).ToArray()),
                "the Unity layer builds exactly the non-pending rows today");
            Assert.That(DebugSubTabRoster.BuiltRows, Is.EqualTo(DebugSubTabRoster.AllRows.ToArray()),
                "and with #692 landed, that is every row the wireframe places");
        }
    }
}
