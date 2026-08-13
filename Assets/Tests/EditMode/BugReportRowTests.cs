using System;
using System.IO;
using System.Linq;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #692: the two Settings Debug-tab bug-report rows — **Copy bug report**
    /// and **Save bug report**. They are ordinary Debug action rows in the pane
    /// docs/specs/ui/settings.md already describes as having "room for more", so
    /// they reuse the approved row component's metrics and are gated by exactly
    /// one thing: the 10-tap Debug unlock.
    /// </summary>
    public class BugReportRowTests
    {
        private const string TestVersion = "0.15.0-abc1234";
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject panelHost;
        private SettingsPanel panel;
        private GameState state;

        [SetUp]
        public void CreatePanel()
        {
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

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
            UnityEngine.Object.DestroyImmediate(canvasHost);
        }

        private void UnlockDebug()
        {
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 0.2);
            }
        }

        // ---------------------------------------------------------------
        // The rows themselves
        // ---------------------------------------------------------------

        [Test]
        public void BugReportRows_AreDebugActionRowsAtTheSharedRowMetrics()
        {
            UnlockDebug();

            foreach (var row in new[] { panel.CopyBugReportRowRect, panel.SaveBugReportRowRect })
            {
                Assert.That(row, Is.Not.Null);
                Assert.That(row.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugRowHeightPx));
            }

            foreach (var pill in new[] { panel.CopyBugReportButtonRect, panel.SaveBugReportButtonRect })
            {
                Assert.That(pill.sizeDelta,
                    Is.EqualTo(new Vector2(SettingsPanel.DebugActionWidthPx, SettingsPanel.DebugActionHeightPx)),
                    "sized exactly like Add coins / Refresh quests now");
            }

            // Stacked below the existing rows, in the documented order.
            Assert.That(panel.CopyBugReportRowRect.anchoredPosition.y,
                Is.LessThan(panel.TuneBalanceRowRect.anchoredPosition.y));
            Assert.That(panel.SaveBugReportRowRect.anchoredPosition.y,
                Is.LessThan(panel.CopyBugReportRowRect.anchoredPosition.y));
            Assert.That(
                panel.CopyBugReportRowRect.anchoredPosition.y - panel.SaveBugReportRowRect.anchoredPosition.y,
                Is.EqualTo(SettingsPanel.DebugRowHeightPx + SettingsPanel.DebugRowGapPx),
                "the shared row pitch — no new named layout values (#161)");
        }

        [Test]
        public void BugReportPillLabels_AreAsciiOnly()
        {
            // #291: the bundled DejaVu Sans is not guaranteed to carry decorative
            // glyphs, so every pill label stays ASCII.
            UnlockDebug();

            foreach (var pill in new[] { panel.CopyBugReportButtonRect, panel.SaveBugReportButtonRect })
            {
                var glyph = pill.GetComponentsInChildren<Text>(true).Single().text;
                Assert.That(glyph, Is.Not.Empty);
                Assert.That(glyph.All(c => c < 128), Is.True, "ASCII-only pill label: " + glyph);
            }
        }

        [Test]
        public void BugReportRows_AreUnreachableUntilTheTenTapUnlock()
        {
            panel.Open();

            Assert.That(panel.DebugTabVisible, Is.False, "the Debug tab starts locked");
            Assert.That(panel.CopyBugReportButtonRect.gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.SaveBugReportButtonRect.gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.CopyBugReportRowRect.parent, Is.EqualTo(panel.TuneBalanceRowRect.parent),
                "they share the Debug pane, so they are gated identically to the other rows");

            var live = panelHost.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(live, Has.No.Member(panel.CopyBugReportButtonRect.GetComponent<Button>()));
            Assert.That(live, Has.No.Member(panel.SaveBugReportButtonRect.GetComponent<Button>()));

            UnlockDebug();
            panel.DebugTabRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(panel.CopyBugReportButtonRect.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.SaveBugReportButtonRect.gameObject.activeInHierarchy, Is.True);
        }

        // ---------------------------------------------------------------
        // Copy bug report
        // ---------------------------------------------------------------

        [Test]
        public void CopyBugReport_WritesANonEmptyReportToTheClipboardAndRaisesAToast()
        {
            UnlockDebug();
            var toasts = new System.Collections.Generic.List<string>();
            panel.ToastRequested = toasts.Add;
            panel.BugReportProvider = () => DiagnosticReport.HeaderFor(DiagnosticReport.ReportSection) + "\nbody";
            GUIUtility.systemCopyBuffer = string.Empty;

            panel.CopyBugReportButtonRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(GUIUtility.systemCopyBuffer, Is.Not.Empty);
            Assert.That(GUIUtility.systemCopyBuffer,
                Does.Contain(DiagnosticReport.HeaderFor(DiagnosticReport.ReportSection)));
            Assert.That(toasts.Count, Is.EqualTo(1));
            Assert.That(toasts[0], Does.Contain("KB"), "the toast names the report's size");
        }

        // ---------------------------------------------------------------
        // Save bug report
        // ---------------------------------------------------------------

        [Test]
        public void SaveBugReport_WritesATimestampedFileUnderBugReportsAndRaisesAToastNamingIt()
        {
            UnlockDebug();
            var toasts = new System.Collections.Generic.List<string>();
            panel.ToastRequested = toasts.Add;
            panel.BugReportProvider = () => "snapshot-body";

            var path = panel.SaveBugReport();

            try
            {
                Assert.That(File.Exists(path), Is.True, "the report lands on disk: " + path);
                Assert.That(File.ReadAllText(path), Is.EqualTo("snapshot-body"));
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(BugReportFile.DirectoryPath));
                Assert.That(BugReportFile.DirectoryPath,
                    Is.EqualTo(Path.Combine(Application.persistentDataPath, BugReportFile.DirectoryName)));

                var fileName = Path.GetFileName(path);
                Assert.That(fileName, Does.StartWith(BugReportFile.FileNamePrefix));
                Assert.That(fileName, Does.EndWith(BugReportFile.FileNameExtension));
                Assert.That(toasts.Count, Is.EqualTo(1));
                Assert.That(toasts[0], Does.Contain(fileName), "the toast names the file");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void BugReportFileName_IsTimestampedSoTwoReportsNeverCollide()
        {
            var first = BugReportFile.FileNameFor(new DateTime(2026, 8, 13, 14, 30, 0, DateTimeKind.Utc));
            var second = BugReportFile.FileNameFor(new DateTime(2026, 8, 13, 14, 30, 1, DateTimeKind.Utc));

            Assert.That(first, Is.EqualTo("bugreport-20260813-143000.txt"));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        // ---------------------------------------------------------------
        // The snapshot the rows actually capture
        // ---------------------------------------------------------------

        [Test]
        public void BuildBugReport_CapturesEverySectionFromTheLiveGame()
        {
            var report = BugReportBuilder.Build(
                state,
                panel.Toggles,
                logBuffer: null,
                worldRoot: null,
                nowUtc: new DateTime(2026, 8, 13, 14, 30, 0, DateTimeKind.Utc));

            foreach (var section in DiagnosticReport.SectionNames)
            {
                Assert.That(report, Does.Contain(DiagnosticReport.HeaderFor(section)));
            }

            Assert.That(report, Does.Contain("appVersion=" + Application.version));
        }

        [Test]
        public void BugReportEnvironment_DescribesThisDeviceAndNothingPersonal()
        {
            var environment = BugReportBuilder.CaptureEnvironment(
                new DateTime(2026, 8, 13, 14, 30, 0, DateTimeKind.Utc));

            Assert.That(environment.AppVersion, Is.EqualTo(Application.version));
            Assert.That(environment.Platform, Is.EqualTo(Application.platform.ToString()));
            Assert.That(environment.DeviceModel, Is.EqualTo(SystemInfo.deviceModel));
            Assert.That(environment.OperatingSystem, Is.EqualTo(SystemInfo.operatingSystem));
            Assert.That(environment.ScreenWidth, Is.EqualTo(Screen.width));
            Assert.That(environment.Timestamp, Is.EqualTo("2026-08-13T14:30:00Z"));

            // docs/specs/product-scope.md: no accounts, no network, no location —
            // there is no device identifier beyond model/OS to capture, and none
            // is captured.
            Assert.That(environment.DeviceModel, Is.Not.Null);
        }
    }
}
