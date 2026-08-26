using System.Linq;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #716: the Settings <b>Debug</b> pane's sub-tab bar. The pane used to be one
    /// flat list that fit exactly five rows — and it had five, so the next one
    /// hung off the bottom of the panel over the dimmed background. The approved
    /// wireframe (docs/specs/ui/settings.md) splits the rows into named groups
    /// behind a sub-tab bar and derives each group's capacity from the pane's own
    /// box.
    ///
    /// <para>These tests assert the built bar against the wireframe's named
    /// constants, that only the active group's rows are on screen, and the
    /// invariant that gave the bug its teeth: <b>every Debug row renders fully
    /// inside the Settings panel</b> — measured off the real RectTransform chain,
    /// not assumed.</para>
    /// </summary>
    public class SettingsDebugSubTabTests
    {
        private const string TestVersion = "0.16.0-abc1234";
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        /// <summary>The approved capacity for today's constants (#716). Locked
        /// here as the wireframe's answer; the panel must <i>compute</i> it.</summary>
        private const int ApprovedRowCapacity = 3;

        private GameObject canvasHost;
        private GameObject panelHost;
        private SettingsPanel panel;
        private GameState state;
        private bool forceFencesAtStart;
        private bool showDebugColorsAtStart;

        [SetUp]
        public void CreatePanel()
        {
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

        // ---------------------------------------------------------------
        // The four new constants, and where each was reused from
        // ---------------------------------------------------------------

        [Test]
        public void SubTabConstants_MatchTheApprovedWireframe()
        {
            Assert.That(SettingsPanel.DebugSubTabHeightPx, Is.EqualTo(72f));
            Assert.That(SettingsPanel.DebugSubTabGapPx, Is.EqualTo(16f));
            Assert.That(SettingsPanel.DebugSubTabRadiusPx, Is.EqualTo(24f));
            Assert.That(SettingsPanel.DebugSubTabBarGapPx, Is.EqualTo(20f));
        }

        [Test]
        public void SubTabConstants_ReuseThePanesExistingMetrics_NotInventedNumbers()
        {
            Assert.That(SettingsPanel.DebugSubTabHeightPx, Is.EqualTo(SettingsPanel.DebugActionHeightPx),
                "the sub-tab pill is the pill height the pane already uses");
            Assert.That(SettingsPanel.DebugSubTabGapPx, Is.EqualTo(SettingsPanel.TabGapPx),
                "the sub-tab gap is the outer tab gap, one level in");
            Assert.That(SettingsPanel.DebugSubTabRadiusPx, Is.EqualTo(SettingsPanel.TabRadiusPx),
                "the sub-tab pill is the outer tab shape, one level in");
            Assert.That(SettingsPanel.DebugSubTabBarGapPx, Is.EqualTo(SettingsPanel.DebugRowGapPx),
                "the bar sits exactly one row-gap above the first row");
        }

        // ---------------------------------------------------------------
        // Capacity is computed from the panel's own constants
        // ---------------------------------------------------------------

        [Test]
        public void RowCapacity_IsComputedFromThePanelsConstants_NotHardCoded()
        {
            Assert.That(SettingsPanel.DebugSubTabRowCapacity, Is.EqualTo(ApprovedRowCapacity),
                "today's constants allow three rows per sub-tab (#716)");
            Assert.That(SettingsPanel.DebugSubTabRowCapacity,
                Is.EqualTo(SettingsPanel.DebugPaneMetrics.RowCapacity),
                "the panel defers to the engine-free Core calculation, it does not keep its own copy");
        }

        [Test]
        public void DebugPaneMetrics_DescribeTheRealPaneBox()
        {
            var metrics = SettingsPanel.DebugPaneMetrics;

            Assert.That(metrics.PanelHeightPx, Is.EqualTo(SettingsPanel.SettingsPanelHeightPx));
            Assert.That(metrics.RowHeightPx, Is.EqualTo(SettingsPanel.DebugRowHeightPx));
            Assert.That(metrics.RowGapPx, Is.EqualTo(SettingsPanel.DebugRowGapPx));
            Assert.That(metrics.SubTabHeightPx, Is.EqualTo(SettingsPanel.DebugSubTabHeightPx));
            Assert.That(metrics.SubTabBarGapPx, Is.EqualTo(SettingsPanel.DebugSubTabBarGapPx));
            Assert.That(metrics.PaneHeightPx, Is.EqualTo(HeightOf(panel.DebugPaneRect, HeightOf(panel.ContentPaneRect, SettingsPanel.SettingsPanelHeightPx))).Within(0.01f),
                "the computed pane box is the pane the panel actually builds");
        }

        // ---------------------------------------------------------------
        // The bar itself
        // ---------------------------------------------------------------

        [Test]
        public void DebugPane_RendersASubTabBar_AtTheTopOfThePane()
        {
            var bar = panel.DebugSubTabBarRect;

            Assert.That(bar, Is.Not.Null, "the Debug pane has a sub-tab bar (#716)");
            Assert.That(bar.parent, Is.EqualTo(panel.DebugPaneRect),
                "the bar lives inside the Debug pane's own padding box");
            Assert.That(bar.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugSubTabHeightPx));
            Assert.That(TopInParent(bar), Is.EqualTo(0f).Within(0.01f),
                "the bar is the first thing in the pane");
        }

        [Test]
        public void TheBar_CarriesTheThreeApprovedSubTabs_InOrder()
        {
            var labels = DebugSubTabRoster.Order
                .Select(tab => panel.SubTabRect(tab).GetComponentInChildren<Text>(true).text)
                .ToArray();

            Assert.That(labels, Is.EqualTo(new[] { "General", "Visuals & Tools", "Reports" }));
        }

        [Test]
        public void SubTabPills_AreTheApprovedHeightAndCornerRadius()
        {
            foreach (var tab in DebugSubTabRoster.Order)
            {
                var pill = panel.SubTabRect(tab);
                var image = pill.GetComponent<Image>();

                Assert.That(pill.sizeDelta.y, Is.EqualTo(0f).Within(0.01f),
                    DebugSubTabRoster.LabelOf(tab) + " stretches to the bar's height");
                Assert.That(pill.anchorMin.y, Is.EqualTo(0f));
                Assert.That(pill.anchorMax.y, Is.EqualTo(1f));
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.sprite.border,
                    Is.EqualTo(new Vector4(SettingsPanel.DebugSubTabRadiusPx, SettingsPanel.DebugSubTabRadiusPx,
                        SettingsPanel.DebugSubTabRadiusPx, SettingsPanel.DebugSubTabRadiusPx)),
                    DebugSubTabRoster.LabelOf(tab) + " uses DebugSubTabRadiusPx corners");
            }
        }

        [Test]
        public void AdjacentSubTabPills_AreSeparatedByExactlyTheSubTabGap()
        {
            var order = DebugSubTabRoster.Order;

            for (var i = 0; i < order.Count - 1; i++)
            {
                var left = panel.SubTabRect(order[i]);
                var right = panel.SubTabRect(order[i + 1]);

                Assert.That(left.anchorMax.x, Is.EqualTo(right.anchorMin.x).Within(0.0001f),
                    "the pills partition the bar contiguously");
                Assert.That(-left.offsetMax.x + right.offsetMin.x,
                    Is.EqualTo(SettingsPanel.DebugSubTabGapPx).Within(0.01f),
                    "neighbouring pills sit exactly DebugSubTabGapPx apart");
            }

            Assert.That(panel.SubTabRect(order[0]).offsetMin.x, Is.EqualTo(0f).Within(0.01f),
                "the first pill starts flush with the bar");
            Assert.That(panel.SubTabRect(order[order.Count - 1]).offsetMax.x, Is.EqualTo(0f).Within(0.01f),
                "the last pill ends flush with the bar");
        }

        [Test]
        public void EachSubTabsRowList_StartsOneBarGapBelowTheBar()
        {
            foreach (var tab in DebugSubTabRoster.Order)
            {
                var group = panel.SubTabGroupRect(tab);

                Assert.That(group, Is.Not.Null,
                    DebugSubTabRoster.LabelOf(tab) + " has a row container to build into");
                Assert.That(group.parent, Is.EqualTo(panel.DebugPaneRect));
                Assert.That(TopInParent(group),
                    Is.EqualTo(SettingsPanel.DebugSubTabHeightPx + SettingsPanel.DebugSubTabBarGapPx).Within(0.01f),
                    "the row list starts DebugSubTabBarGapPx below the bar");
            }
        }

        // ---------------------------------------------------------------
        // Selecting a sub-tab
        // ---------------------------------------------------------------

        [Test]
        public void TheDebugPane_OpensOnTheGeneralSubTab()
        {
            Assert.That(panel.ActiveDebugSubTab, Is.EqualTo(DebugSubTab.General));
        }

        [Test]
        public void SelectingASubTab_ShowsOnlyThatGroupsRows()
        {
            ShowDebugTab();

            foreach (var selected in DebugSubTabRoster.Order)
            {
                panel.SelectDebugSubTab(selected);

                Assert.That(panel.ActiveDebugSubTab, Is.EqualTo(selected));

                foreach (var tab in DebugSubTabRoster.Order)
                {
                    var expectVisible = tab == selected;

                    foreach (var rowKey in DebugSubTabRoster.RowsIn(tab))
                    {
                        var row = panel.DebugRowRect(rowKey);
                        if (row == null)
                        {
                            continue; // placed by #716, built by #692
                        }

                        Assert.That(row.gameObject.activeInHierarchy, Is.EqualTo(expectVisible),
                            rowKey + " visibility must follow the active sub-tab (" +
                            DebugSubTabRoster.LabelOf(selected) + ")");
                    }
                }
            }
        }

        [Test]
        public void RowsOnAnInactiveSubTab_AreNotInteractable()
        {
            ShowDebugTab();
            panel.SelectDebugSubTab(DebugSubTab.General);

            var liveButtons = panelHost.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy)
                .ToArray();

            foreach (var rowKey in DebugSubTabRoster.RowsIn(DebugSubTab.VisualsAndTools))
            {
                var row = panel.DebugRowRect(rowKey);
                if (row == null)
                {
                    continue;
                }

                foreach (var button in row.GetComponentsInChildren<Button>(true))
                {
                    Assert.That(liveButtons, Has.No.Member(button),
                        rowKey + " must not be tappable while another sub-tab is selected");
                }
            }
        }

        [Test]
        public void TheActiveSubTabPill_TakesTheActiveRoleTint_LikeTheOuterTabs()
        {
            ShowDebugTab();
            panel.SelectDebugSubTab(DebugSubTab.VisualsAndTools);

            AssertHex(panel.SubTabRect(DebugSubTab.VisualsAndTools).GetComponent<Image>().color,
                0xFF, 0x7A, 0x5C, "the active sub-tab takes the Coral role tint");
            AssertHex(panel.SubTabRect(DebugSubTab.General).GetComponent<Image>().color,
                0xFF, 0xF3, 0xD9, "an inactive sub-tab takes the neutral Cream role tint");
        }

        [Test]
        public void TappingASubTabPill_SwitchesTheGroup()
        {
            ShowDebugTab();

            panel.SubTabRect(DebugSubTab.Reports).GetComponent<Button>().onClick.Invoke();

            Assert.That(panel.ActiveDebugSubTab, Is.EqualTo(DebugSubTab.Reports));
        }

        [Test]
        public void TheSubTabBar_IsStillGatedBehindTheTenTapUnlock()
        {
            panel.Open();

            Assert.That(panel.DebugSubTabBarRect.gameObject.activeInHierarchy, Is.False,
                "the bar lives in the Debug pane, so the unlock gates it like every row");
        }

        // ---------------------------------------------------------------
        // Grouping: none dropped, none duplicated
        // ---------------------------------------------------------------

        [Test]
        public void EveryBuiltDebugRow_LivesUnderExactlyOneSubTabsRowList()
        {
            foreach (var rowKey in DebugSubTabRoster.BuiltRows)
            {
                var row = panel.DebugRowRect(rowKey);

                Assert.That(row, Is.Not.Null, rowKey + " is built by the Unity layer");
                Assert.That(row.parent, Is.EqualTo(panel.SubTabGroupRect(DebugSubTabRoster.GroupOf(rowKey))),
                    rowKey + " must sit under the sub-tab the roster assigns it");
            }
        }

        [Test]
        public void ThePanelBuildsEveryNonPendingRow_AndNoOthers()
        {
            Assert.That(panel.BuiltDebugRowKeys, Is.EquivalentTo(DebugSubTabRoster.BuiltRows),
                "every row the roster says is built, is built — none dropped, none extra");
            Assert.That(panel.BuiltDebugRowKeys.Distinct().Count(),
                Is.EqualTo(panel.BuiltDebugRowKeys.Count),
                "no row is built twice");
        }

        [Test]
        public void TheReportsSubTab_HasAnEmptyRowListReadyFor692()
        {
            // #716 delivers the structure; #692 builds Copy/Save bug report into it.
            Assert.That(panel.SubTabGroupRect(DebugSubTab.Reports), Is.Not.Null);
            Assert.That(DebugSubTabRoster.RowsIn(DebugSubTab.Reports).Count,
                Is.LessThanOrEqualTo(SettingsPanel.DebugSubTabRowCapacity),
                "the incoming bug-report rows already fit the group they were placed in");
        }

        // ---------------------------------------------------------------
        // The invariant (#716): every Debug row renders inside the panel
        // ---------------------------------------------------------------

        [Test]
        public void Invariant_EveryBuiltDebugRow_RendersFullyInsideTheSettingsPanel()
        {
            var paneBottomFromPanelTop =
                TopFromPanel(panel.DebugPaneRect)
                + HeightOf(panel.DebugPaneRect, HeightOf(panel.ContentPaneRect, SettingsPanel.SettingsPanelHeightPx));

            foreach (var rowKey in DebugSubTabRoster.BuiltRows)
            {
                var row = panel.DebugRowRect(rowKey);
                var bottom = TopFromPanel(row) + row.sizeDelta.y;

                Assert.That(bottom, Is.LessThanOrEqualTo(paneBottomFromPanelTop + 0.01f),
                    rowKey + " must end inside the Debug pane's own box — the panel's " +
                    "incidental bottom margin is not capacity (#716)");
                Assert.That(bottom, Is.LessThanOrEqualTo(SettingsPanel.SettingsPanelHeightPx + 0.01f),
                    rowKey + " must render fully inside the Settings panel; a row that " +
                    "does not fit is unreachable, not merely tight");
            }
        }

        [Test]
        public void Invariant_NoSubTabHoldsMoreRowsThanThePaneCanShow()
        {
            foreach (var tab in DebugSubTabRoster.Order)
            {
                Assert.That(DebugSubTabRoster.RowsIn(tab).Count,
                    Is.LessThanOrEqualTo(SettingsPanel.DebugSubTabRowCapacity),
                    DebugSubTabRoster.LabelOf(tab) + " holds more rows than the Debug pane can show");
            }
        }

        [Test]
        public void Invariant_ASubTabSeededWithOneRowTooMany_FallsOffThePane()
        {
            // The failing case, asserted rather than trusted: a group holding
            // DebugSubTabRowCapacity + 1 rows puts its last row past the pane.
            var metrics = SettingsPanel.DebugPaneMetrics;
            var oneTooMany = SettingsPanel.DebugSubTabRowCapacity + 1;

            Assert.That(metrics.Fits(oneTooMany), Is.False);
            Assert.That(metrics.RowBottomEdgeFromPaneTopPx(oneTooMany - 1),
                Is.GreaterThan(metrics.PaneHeightPx),
                "one row past capacity hangs off the Debug pane — the suite must fail, " +
                "not the button float over the scrim");
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        private void ShowDebugTab()
        {
            panel.Open();
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 0.2);
            }

            panel.DebugTabRect.GetComponent<Button>().onClick.Invoke();
        }

        /// <summary>Distance from a rect's parent's top edge to the rect's own top
        /// edge, read off the real RectTransform (top-anchored rows use
        /// anchoredPosition; vertically stretched containers use offsetMax).</summary>
        private static float TopInParent(RectTransform rect)
        {
            if (Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            {
                return -rect.anchoredPosition.y;
            }

            return -rect.offsetMax.y;
        }

        /// <summary>Distance from the Settings panel's top edge down to
        /// <paramref name="rect"/>'s top edge, walking the real hierarchy.</summary>
        private float TopFromPanel(RectTransform rect)
        {
            var top = 0f;
            var node = rect;

            while (node != null && node != panel.PanelRect)
            {
                top += TopInParent(node);
                node = node.parent as RectTransform;
            }

            Assert.That(node, Is.EqualTo(panel.PanelRect),
                "the rect must hang off the Settings panel");
            return top;
        }

        /// <summary>A rect's own height given its parent's height (stretched rects
        /// are the parent less their insets; fixed rects carry it in sizeDelta).</summary>
        private static float HeightOf(RectTransform rect, float parentHeightPx)
        {
            if (Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            {
                return rect.sizeDelta.y;
            }

            return parentHeightPx + rect.offsetMax.y - rect.offsetMin.y;
        }

        private static void AssertHex(Color color, byte r, byte g, byte b, string because)
        {
            var actual = (Color32)color;
            Assert.That(new[] { actual.r, actual.g, actual.b }, Is.EqualTo(new[] { r, g, b }), because);
        }
    }
}
