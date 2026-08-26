using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Diagnostics
{
    /// <summary>
    /// #692: the bug-report snapshot's <b>shape</b> — every <c>== SECTION ==</c>
    /// header, the order they arrive in, and the invariant that a system with
    /// nothing to report still says so out loud.
    /// </summary>
    public class DiagnosticReportSectionTests
    {
        private const string TestVersion = "0.16.0-abc1234";
        private const string TestFlavor = "debug";
        private const string TestPlatform = "Android";
        private const string TestDevice = "Samsung SM-X200";
        private const string TestOs = "Android 13 API-33";
        private const int TestScreenWidth = 1920;
        private const int TestScreenHeight = 1200;
        private const float TestScreenDpi = 224f;
        private const string TestTimestamp = "2026-08-26T18:04:11Z";
        private const double TestUptimeSeconds = 412.5d;

        internal static DiagnosticEnvironment Environment()
        {
            return new DiagnosticEnvironment(
                appVersion: TestVersion,
                buildFlavor: TestFlavor,
                platform: TestPlatform,
                deviceModel: TestDevice,
                operatingSystem: TestOs,
                screenWidth: TestScreenWidth,
                screenHeight: TestScreenHeight,
                screenDpi: TestScreenDpi,
                timestamp: TestTimestamp,
                sessionUptimeSeconds: TestUptimeSeconds);
        }

        internal static string Render(GameState state)
        {
            return DiagnosticReport.Render(
                state,
                new TuningConfig(),
                new DebugToggleRegistry(),
                Environment(),
                new List<DiagnosticLogEntry>());
        }

        /// <summary>The body of one section: every line after its header, up to
        /// the next header (or the end of the report).</summary>
        internal static string BodyOf(string report, string section)
        {
            var lines = report.Split('\n');
            var body = new List<string>();
            var inside = false;

            foreach (var line in lines)
            {
                if (line == DiagnosticReport.HeaderFor(section))
                {
                    inside = true;
                    continue;
                }

                if (inside && line.StartsWith(DiagnosticReport.HeaderFence))
                {
                    break;
                }

                if (inside)
                {
                    body.Add(line);
                }
            }

            Assert.That(inside, Is.True, "the report carries a " + section + " section at all");
            return string.Join("\n", body);
        }

        [Test]
        public void TheReport_CarriesTheTwelveApprovedSections()
        {
            Assert.That(DiagnosticReport.SectionNames, Is.EqualTo(new[]
            {
                "REPORT", "SAVE", "TUNING", "DEBUG", "ECONOMY", "MAP",
                "HOUSES", "DOGS", "QUESTS", "ONBOARDING", "ITEMS", "LOG",
            }));
        }

        [Test]
        public void Invariant_EverySectionHeaderIsEmitted_EvenWhenItsSystemHasNothingToSay()
        {
            var report = Render(GameState.CreateNew());

            foreach (var section in DiagnosticReport.SectionNames)
            {
                Assert.That(report, Does.Contain(DiagnosticReport.HeaderFor(section)),
                    section + " must be present on every report — a silently omitted " +
                    "system reads as 'we never captured it'");
            }
        }

        [Test]
        public void Invariant_AnEmptySection_SaysNoneRatherThanVanishing()
        {
            // A brand-new neighborhood has no placed items, no decorations, and
            // (in this render) no captured log lines.
            var report = Render(GameState.CreateNew());

            Assert.That(BodyOf(report, "LOG").Trim(), Is.EqualTo(DiagnosticReport.EmptyMarker));
            Assert.That(BodyOf(report, "ITEMS"), Does.Contain(DiagnosticReport.EmptyMarker));
        }

        [Test]
        public void TheSections_ArriveInTheApprovedOrder_WithLogLast()
        {
            var report = Render(GameState.CreateNew());

            var positions = DiagnosticReport.SectionNames
                .Select(section => report.IndexOf(DiagnosticReport.HeaderFor(section)))
                .ToList();

            Assert.That(positions, Is.Ordered.Ascending, "sections arrive in the declared order");
            Assert.That(DiagnosticReport.SectionNames.Last(), Is.EqualTo("LOG"),
                "LOG is last on purpose: it makes the end of the report identifiable, " +
                "so a truncated report is recognizable as truncated");
        }

        [Test]
        public void TheReportHeader_DescribesTheDeviceWithoutCoreMakingAnEngineCall()
        {
            var body = BodyOf(Render(GameState.CreateNew()), "REPORT");

            Assert.That(body, Does.Contain(TestVersion));
            Assert.That(body, Does.Contain(TestFlavor));
            Assert.That(body, Does.Contain(TestPlatform));
            Assert.That(body, Does.Contain(TestDevice));
            Assert.That(body, Does.Contain(TestOs));
            Assert.That(body, Does.Contain("1920x1200"));
            Assert.That(body, Does.Contain("224"));
            Assert.That(body, Does.Contain(TestTimestamp));
            Assert.That(body, Does.Contain("412.5"));
            Assert.That(body, Does.Contain(DiagnosticNumbers.ReportSchemaVersion.ToString()));
        }
    }
}
