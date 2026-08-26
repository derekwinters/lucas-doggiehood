using System;
using System.Collections.Generic;
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
    /// #695: the Settings Debug pane's third <b>Reports</b> row — <b>Share bug
    /// report</b>. It writes the same timestamped snapshot #692's <b>Save bug
    /// report</b> row produces and then hands <i>that file</i> to the Android
    /// share sheet, so a report can reach Derek without a USB cable.
    ///
    /// <para>The share sheet itself is JNI and cannot run in EditMode, so the
    /// Unity layer splits at <see cref="IBugReportShareTarget"/>: everything
    /// above the seam — writing the file, composing the summary line, wiring the
    /// row, raising the toast — is asserted here against a fake target that
    /// records what it was handed. Only the intent resolving on a real device is
    /// beyond this suite.</para>
    /// </summary>
    public class ShareBugReportRowTests
    {
        private const string TestVersion = "0.16.0-abc1234";
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        /// <summary>A fake share target: it records the call instead of opening a
        /// share sheet, which is exactly what makes the whole path above the seam
        /// testable without a device.</summary>
        private sealed class RecordingShareTarget : IBugReportShareTarget
        {
            public int Calls { get; private set; }

            public string FilePath { get; private set; }

            public string Summary { get; private set; }

            public void Share(string filePath, string summary)
            {
                Calls++;
                FilePath = filePath;
                Summary = summary;
            }
        }

        private GameObject canvasHost;
        private GameObject panelHost;
        private SettingsPanel panel;
        private GameState state;
        private RecordingShareTarget shareTarget;
        private readonly List<string> toasts = new List<string>();
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

            toasts.Clear();
            panel.ToastRequested = message => toasts.Add(message);
            shareTarget = new RecordingShareTarget();
        }

        [TearDown]
        public void Cleanup()
        {
            WorldBuilder.ForceFencesVisible = forceFencesAtStart;
            WorldBuilder.ShowDebugElementColors = showDebugColorsAtStart;
            DeleteWrittenReport();
            UnityEngine.Object.DestroyImmediate(canvasHost);
        }

        // ---------------------------------------------------------------
        // The row: where it lives, how big it is, what gates it
        // ---------------------------------------------------------------

        [Test]
        public void TheShareRow_IsBuiltOnTheReportsSubTab()
        {
            var row = panel.DebugRowRect(DebugSubTabRoster.ShareBugReportRow);

            Assert.That(row, Is.Not.Null, "the Share bug report row is built by the Unity layer (#695)");
            Assert.That(row.parent, Is.EqualTo(panel.SubTabGroupRect(DebugSubTab.Reports)),
                "it sits under the Reports sub-tab the roster assigns it");
            Assert.That(panel.BuiltDebugRowKeys, Contains.Item(DebugSubTabRoster.ShareBugReportRow));
        }

        [Test]
        public void TheShareRow_IsTheApprovedRowAndActionPillSize()
        {
            Assert.That(panel.ShareBugReportRowRect.sizeDelta.y,
                Is.EqualTo(SettingsPanel.DebugRowHeightPx));
            Assert.That(panel.ShareBugReportButtonRect.sizeDelta,
                Is.EqualTo(new Vector2(SettingsPanel.DebugActionWidthPx, SettingsPanel.DebugActionHeightPx)),
                "sized exactly like Copy / Save beside it (#161 — named constants)");
        }

        [Test]
        public void TheSharePillLabel_IsAsciiOnly_BecauseOfTheBundledFont()
        {
            // #291: DejaVu Sans is what ships; a non-ASCII glyph renders as a box.
            var glyph = panel.ShareBugReportButtonRect.GetComponentInChildren<Text>(true).text;

            Assert.That(glyph.All(character => character < 128), Is.True, "ASCII only: " + glyph);
        }

        [Test]
        public void TheShareRow_IsGatedBehindTheTenTapDebugUnlock()
        {
            panel.Open();

            Assert.That(panel.ShareBugReportRowRect.gameObject.activeInHierarchy, Is.False,
                "an un-unlocked Debug tab shows no share row");

            ShowReportsSubTab();

            Assert.That(panel.ShareBugReportRowRect.gameObject.activeInHierarchy, Is.True);
        }

        // ---------------------------------------------------------------
        // Tapping it: the file, and the exact path handed across the seam
        // ---------------------------------------------------------------

        [Test]
        public void TappingShare_WritesATimestampedReportFile_LikeTheSaveRowDoes()
        {
            panel.ShareTarget = shareTarget;
            ShowReportsSubTab();

            TapShare();
            var path = panel.LastSavedBugReportPath;

            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(Path.GetDirectoryName(path), Is.EqualTo(BugReportFile.DirectoryPath),
                "the share reuses #692's one file-writing path — there is no second one");
            Assert.That(Path.GetFileName(path), Does.StartWith(BugReportFile.FileNamePrefix));
            Assert.That(Path.GetExtension(path), Is.EqualTo(BugReportFile.FileExtension));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(path),
                Does.Contain(DiagnosticReport.HeaderFor(DiagnosticReport.SaveSection)),
                "the shared file is the whole snapshot, not a summary of it");
        }

        [Test]
        public void TappingShare_HandsThatExactFileToTheShareTarget()
        {
            panel.ShareTarget = shareTarget;
            ShowReportsSubTab();

            TapShare();

            Assert.That(shareTarget.Calls, Is.EqualTo(1), "one share, not a stream");
            Assert.That(shareTarget.FilePath, Is.EqualTo(panel.LastSavedBugReportPath),
                "the file that was just written is the file that gets shared");
            Assert.That(File.Exists(shareTarget.FilePath), Is.True,
                "the file exists by the time it is handed over");
        }

        // ---------------------------------------------------------------
        // The truncation invariant, from the row's side
        // ---------------------------------------------------------------

        [Test]
        public void TheSummaryHandedOver_NamesTheBuild_TheDevice_AndWhenItWasTaken()
        {
            panel.ShareTarget = shareTarget;
            ShowReportsSubTab();

            var before = UtcDateStamp();
            TapShare();
            var after = UtcDateStamp();

            Assert.That(shareTarget.Summary, Is.Not.Null.And.Not.Empty);
            Assert.That(shareTarget.Summary, Does.Contain(Application.version), "app version");
            Assert.That(shareTarget.Summary, Does.Contain(SystemInfo.deviceModel), "device model");
            Assert.That(
                shareTarget.Summary.Contains(before) || shareTarget.Summary.Contains(after),
                Is.True,
                "when it was taken (tolerating a UTC date rollover mid-test)");
        }

        [Test]
        public void Invariant_TheReportTravelsAsTheFile_NeverInlinedIntoTheSummary()
        {
            // The report is tens of kilobytes and a receiving app is free to
            // truncate a long text extra; a report that arrives with its LOG
            // section silently cut off is worse than one that never sent. So the
            // body carries the short line only — the report is the attachment.
            panel.ShareTarget = shareTarget;
            ShowReportsSubTab();

            TapShare();
            var report = File.ReadAllText(panel.LastSavedBugReportPath);

            Assert.That(shareTarget.Summary.Length,
                Is.LessThanOrEqualTo(BugReportSummary.MaxLength));
            Assert.That(shareTarget.Summary.Length, Is.LessThan(report.Length),
                "sanity: the summary is nothing like the report's size");
            Assert.That(shareTarget.Summary, Does.Not.Contain("\n"), "one line");

            foreach (var section in DiagnosticReport.SectionNames)
            {
                Assert.That(shareTarget.Summary,
                    Does.Not.Contain(DiagnosticReport.HeaderFor(section)),
                    "the summary must not carry the report body (" + section + ")");
            }
        }

        // ---------------------------------------------------------------
        // The toast
        // ---------------------------------------------------------------

        [Test]
        public void TappingShare_RaisesOneToastConfirmingTheShareWasLaunched()
        {
            panel.ShareTarget = shareTarget;
            ShowReportsSubTab();

            TapShare();

            Assert.That(toasts.Count, Is.EqualTo(1), "one confirmation, not a stream");
            Assert.That(toasts[0],
                Is.EqualTo(BugReportCopy.Sharing(Path.GetFileName(panel.LastSavedBugReportPath))));
            Assert.That(toasts[0], Does.Contain(Path.GetFileName(panel.LastSavedBugReportPath)),
                "the toast names the report that was handed to the share sheet");
        }

        // ---------------------------------------------------------------
        // Off Android there is no share sheet
        // ---------------------------------------------------------------

        [Test]
        public void TheDefaultShareTarget_IsNullOffAndroid_SoNothingTriesToOpenAShareSheet()
        {
            Assert.That(Application.platform, Is.Not.EqualTo(RuntimePlatform.Android),
                "sanity: EditMode is not Android");
            Assert.That(BugReportShareTargets.ForThisPlatform(), Is.Null);
            Assert.That(panel.ShareTarget, Is.Null,
                "the panel takes the platform's answer, and off Android there is none");
        }

        [Test]
        public void OffAndroid_TheRowStillWritesTheFile_ToastsWhereItLanded_AndNeverThrows()
        {
            ShowReportsSubTab();

            Assert.DoesNotThrow(TapShare, "the Editor must never look broken");

            var path = panel.LastSavedBugReportPath;
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(path), Is.True, "the report is still on disk");
            Assert.That(toasts, Is.EqualTo(new[] { BugReportCopy.Saved(Path.GetFileName(path)) }),
                "with no share sheet the row falls back to Save bug report's confirmation");
        }

        // ---------------------------------------------------------------
        // Capacity (#716): Reports is now full
        // ---------------------------------------------------------------

        [Test]
        public void Invariant_TheReportsSubTab_IsNowFull_AndStillFitsThePane()
        {
            Assert.That(DebugSubTabRoster.RowsIn(DebugSubTab.Reports).Count,
                Is.EqualTo(SettingsPanel.DebugSubTabRowCapacity),
                "Copy + Save + Share is exactly the pane's computed capacity (#716)");
            Assert.That(SettingsPanel.DebugPaneMetrics.Fits(
                    DebugSubTabRoster.RowsIn(DebugSubTab.Reports).Count + 1), Is.False,
                "a fourth Reports row needs a new sub-tab, which is a wireframe decision");
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        private void TapShare()
        {
            panel.ShareBugReportButtonRect.GetComponent<Button>().onClick.Invoke();
        }

        /// <summary>Today's UTC date — enough to prove the summary carries a
        /// capture time without racing the clock's seconds.</summary>
        private static string UtcDateStamp()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void DeleteWrittenReport()
        {
            var path = panel != null ? panel.LastSavedBugReportPath : null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
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
