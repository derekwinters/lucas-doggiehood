using System;
using System.IO;
using System.Linq;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #692: the Settings Debug pane's two <b>Reports</b> rows — <b>Copy bug
    /// report</b> and <b>Save bug report</b>. Both are ordinary Debug action
    /// rows (docs/specs/ui/settings.md), so they are gated behind the same 10-tap
    /// unlock as every other Debug affordance and sized by the same named
    /// constants; what is new is where the rendered snapshot goes — the
    /// clipboard, or a timestamped file under <c>persistentDataPath</c>. No
    /// network, ever.
    /// </summary>
    public class BugReportRowTests
    {
        private const string TestVersion = "0.16.0-abc1234";
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject panelHost;
        private SettingsPanel panel;
        private GameState state;
        private bool forceFencesAtStart;
        private bool showDebugColorsAtStart;
        private string clipboardAtStart;

        [SetUp]
        public void CreatePanel()
        {
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            forceFencesAtStart = WorldBuilder.ForceFencesVisible;
            WorldBuilder.ForceFencesVisible = false;
            showDebugColorsAtStart = WorldBuilder.ShowDebugElementColors;
            WorldBuilder.ShowDebugElementColors = false;
            clipboardAtStart = ReadClipboard();

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
            RestoreClipboard(clipboardAtStart);
            UnityEngine.Object.DestroyImmediate(canvasHost);
        }

        // ---------------------------------------------------------------
        // The rows exist, on the Reports sub-tab, at the approved sizes
        // ---------------------------------------------------------------

        [Test]
        public void BothBugReportRows_AreBuiltOnTheReportsSubTab()
        {
            foreach (var rowKey in new[]
                     {
                         DebugSubTabRoster.CopyBugReportRow,
                         DebugSubTabRoster.SaveBugReportRow,
                     })
            {
                var row = panel.DebugRowRect(rowKey);

                Assert.That(row, Is.Not.Null, rowKey + " is built by the Unity layer now (#692)");
                Assert.That(row.parent, Is.EqualTo(panel.SubTabGroupRect(DebugSubTab.Reports)),
                    rowKey + " sits under the Reports sub-tab the roster assigns it");
                Assert.That(panel.BuiltDebugRowKeys, Contains.Item(rowKey));
            }
        }

        [Test]
        public void BothRows_AreTheApprovedRowAndActionPillSize()
        {
            foreach (var row in new[] { panel.CopyBugReportRowRect, panel.SaveBugReportRowRect })
            {
                Assert.That(row.sizeDelta.y, Is.EqualTo(SettingsPanel.DebugRowHeightPx));
            }

            foreach (var action in new[] { panel.CopyBugReportButtonRect, panel.SaveBugReportButtonRect })
            {
                Assert.That(action.sizeDelta,
                    Is.EqualTo(new Vector2(SettingsPanel.DebugActionWidthPx, SettingsPanel.DebugActionHeightPx)),
                    "sized exactly like Add coins / Refresh quests now (#161 — named constants)");
            }
        }

        [Test]
        public void BothPillLabels_AreAsciiOnly_BecauseOfTheBundledFont()
        {
            // #291: DejaVu Sans is what ships; a non-ASCII glyph renders as a box.
            foreach (var label in panelHost.GetComponentsInChildren<Text>(true)
                         .Select(text => text.text)
                         .Where(text => text != null && text.Contains("bug report")))
            {
                Assert.That(label.All(character => character < 128), Is.True,
                    "ASCII only: " + label);
            }

            Assert.That(panel.CopyBugReportButtonRect.GetComponentInChildren<Text>(true).text
                .All(character => character < 128), Is.True);
            Assert.That(panel.SaveBugReportButtonRect.GetComponentInChildren<Text>(true).text
                .All(character => character < 128), Is.True);
        }

        [Test]
        public void BothRows_AreGatedBehindTheTenTapDebugUnlock()
        {
            panel.Open();

            Assert.That(panel.CopyBugReportRowRect.gameObject.activeInHierarchy, Is.False,
                "an un-unlocked Debug tab shows no bug-report row");
            Assert.That(panel.SaveBugReportRowRect.gameObject.activeInHierarchy, Is.False);

            ShowReportsSubTab();

            Assert.That(panel.CopyBugReportRowRect.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.SaveBugReportRowRect.gameObject.activeInHierarchy, Is.True);
        }

        // ---------------------------------------------------------------
        // Copy bug report
        // ---------------------------------------------------------------

        [Test]
        public void TappingCopy_CopiesANonEmptyReport_WithEverySectionInIt()
        {
            ShowReportsSubTab();

            panel.CopyBugReportButtonRect.GetComponent<Button>().onClick.Invoke();
            var copied = panel.LastCopiedBugReport;

            Assert.That(copied, Is.Not.Null.And.Not.Empty);
            foreach (var section in DiagnosticReport.SectionNames)
            {
                Assert.That(copied, Does.Contain(DiagnosticReport.HeaderFor(section)),
                    "the copy carries the whole snapshot, " + section + " included");
            }
        }

        [Test]
        public void TappingCopy_PutsThatReportOnTheSystemClipboard()
        {
            // The CI editor runs -nographics and has no system clipboard to
            // round-trip through, so probe first and report inconclusive there
            // rather than weakening the assertion for the editors that do.
            if (!SystemClipboardRoundTrips())
            {
                Assert.Ignore("this editor has no system clipboard to read back (-nographics)");
            }

            ShowReportsSubTab();

            panel.CopyBugReportButtonRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(panel.LastCopiedBugReport));
            Assert.That(GUIUtility.systemCopyBuffer, Is.Not.Empty);
        }

        [Test]
        public void TappingCopy_RaisesAToastNamingTheReportsSize()
        {
            var toasts = new System.Collections.Generic.List<string>();
            panel.ToastRequested = message => toasts.Add(message);
            ShowReportsSubTab();

            panel.CopyBugReportButtonRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(toasts.Count, Is.EqualTo(1), "one confirmation, not a stream");
            Assert.That(toasts[0], Is.EqualTo(BugReportCopy.Copied(panel.LastCopiedBugReport.Length)));
        }

        // ---------------------------------------------------------------
        // Save bug report
        // ---------------------------------------------------------------

        [Test]
        public void TappingSave_WritesATimestampedFileUnderTheBugReportsFolder()
        {
            ShowReportsSubTab();

            panel.SaveBugReportButtonRect.GetComponent<Button>().onClick.Invoke();
            var path = panel.LastSavedBugReportPath;

            try
            {
                Assert.That(path, Is.Not.Null.And.Not.Empty);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(BugReportFile.DirectoryPath));
                Assert.That(Path.GetFileName(path), Does.StartWith(BugReportFile.FileNamePrefix));
                Assert.That(Path.GetExtension(path), Is.EqualTo(BugReportFile.FileExtension));
                Assert.That(File.Exists(path), Is.True, "the report is on disk");
                Assert.That(File.ReadAllText(path), Does.Contain(DiagnosticReport.HeaderFor(DiagnosticReport.SaveSection)));
            }
            finally
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void TappingSave_RaisesAToastNamingTheFile()
        {
            var toasts = new System.Collections.Generic.List<string>();
            panel.ToastRequested = message => toasts.Add(message);
            ShowReportsSubTab();

            panel.SaveBugReportButtonRect.GetComponent<Button>().onClick.Invoke();
            var path = panel.LastSavedBugReportPath;

            try
            {
                Assert.That(toasts.Count, Is.EqualTo(1));
                Assert.That(toasts[0], Is.EqualTo(BugReportCopy.Saved(Path.GetFileName(path))));
                Assert.That(toasts[0], Does.Contain(Path.GetFileName(path)),
                    "the toast tells the player which file to go and fetch");
            }
            finally
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void TheFileName_IsTimestampedToTheSecond_SoTwoReportsNeverCollide()
        {
            var first = BugReportFile.FileNameFor(new DateTime(2026, 8, 26, 18, 4, 11, DateTimeKind.Utc));
            var second = BugReportFile.FileNameFor(new DateTime(2026, 8, 26, 18, 4, 12, DateTimeKind.Utc));

            Assert.That(first, Is.EqualTo("bugreport-20260826-180411.txt"));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        // ---------------------------------------------------------------
        // The payload the rows deliver
        // ---------------------------------------------------------------

        [Test]
        public void TheReport_DescribesTheDeviceItWasTakenOn()
        {
            var report = BugReportBuilder.Build(
                state, new DebugToggleRegistry(), null, null, DateTime.UtcNow);

            Assert.That(report, Does.Contain(Application.version));
            Assert.That(report, Does.Contain(SystemInfo.deviceModel));
            Assert.That(report, Does.Contain(SystemInfo.operatingSystem));
            Assert.That(report, Does.Contain(Application.platform.ToString()));
        }

        [Test]
        public void TheReport_CarriesTheBufferedLogTail()
        {
            var host = new GameObject("log-buffer-host");
            try
            {
                var buffer = DiagnosticLogBuffer.Install(host);
                buffer.Record(LogType.Warning, "the truck did a weird thing", string.Empty);

                var report = BugReportBuilder.Build(
                    state, new DebugToggleRegistry(), buffer, null, DateTime.UtcNow);

                Assert.That(report, Does.Contain("the truck did a weird thing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        /// <summary>Reads the clipboard, tolerating an editor that has none —
        /// a copy test must not fail in setup on a headless runner.</summary>
        private static string ReadClipboard()
        {
            try
            {
                return GUIUtility.systemCopyBuffer;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Puts back whatever was on the clipboard before the test, so
        /// running the suite locally does not leave a whole bug report on it.</summary>
        private static void RestoreClipboard(string value)
        {
            if (value == null)
            {
                return;
            }

            try
            {
                GUIUtility.systemCopyBuffer = value;
            }
            catch (Exception)
            {
                // No clipboard here; nothing to put back.
            }
        }

        /// <summary>Whether this editor actually has a readable system
        /// clipboard — it does not under <c>-nographics</c>.</summary>
        private static bool SystemClipboardRoundTrips()
        {
            const string probe = "doggiehood-clipboard-probe";
            try
            {
                GUIUtility.systemCopyBuffer = probe;
                return GUIUtility.systemCopyBuffer == probe;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ShowReportsSubTab()
        {
            panel.Open();
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                panel.TapVersion(i * 0.2);
            }

            panel.DebugTabRect.GetComponent<Button>().onClick.Invoke();
            panel.SubTabRect(DebugSubTab.Reports).GetComponent<Button>().onClick.Invoke();
        }
    }
}
