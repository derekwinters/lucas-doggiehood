using System;
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
    /// #692: the engine-free bug-report renderer. These tests pin the two
    /// invariants the spec carries (docs/engineering/diagnostic-report.md):
    /// a report never silently omits a system, and generating one never
    /// mutates anything.
    /// </summary>
    public class DiagnosticReportSectionTests
    {
        internal static DiagnosticEnvironment SampleEnvironment()
        {
            return new DiagnosticEnvironment(
                appVersion: "0.15.0",
                buildFlavor: "release",
                platform: "Android",
                deviceModel: "Google Pixel Tablet",
                operatingSystem: "Android 14 API-34",
                screenWidth: 2560,
                screenHeight: 1600,
                screenDpi: 276f,
                timestamp: "2026-08-13T14:30:00Z",
                sessionUptimeSeconds: 412.5);
        }

        internal static string Render(
            GameState state,
            TuningConfig tuning = null,
            DebugToggleRegistry toggles = null,
            IReadOnlyList<DiagnosticLogEntry> log = null,
            IReadOnlyDictionary<string, GridPoint> dogPositions = null)
        {
            return DiagnosticReport.Render(
                state,
                tuning ?? new TuningConfig(),
                toggles ?? new DebugToggleRegistry(),
                SampleEnvironment(),
                log ?? Array.Empty<DiagnosticLogEntry>(),
                dogPositions);
        }

        [Test]
        public void EverySectionHeader_IsEmittedForADefaultGameState()
        {
            var report = Render(GameState.CreateNew());

            foreach (var section in DiagnosticReport.SectionNames)
            {
                var header = DiagnosticReport.HeaderFor(section);
                Assert.That(report, Does.Contain(header),
                    "a diagnostic report never silently omits a system: " + header);
            }
        }

        [Test]
        public void SectionHeaders_AppearExactlyOnceAndInTheDeclaredOrder()
        {
            var report = Render(GameState.CreateNew());
            var lines = report.Split('\n');

            var headersInReport = lines
                .Where(line => line.StartsWith(DiagnosticReport.HeaderFence, StringComparison.Ordinal))
                .ToList();

            var expected = DiagnosticReport.SectionNames
                .Select(DiagnosticReport.HeaderFor)
                .ToList();

            Assert.That(headersInReport, Is.EqualTo(expected));
        }

        [Test]
        public void LogIsTheFinalSection_SoATruncatedReportIsRecognizable()
        {
            var report = Render(GameState.CreateNew());

            Assert.That(DiagnosticReport.SectionNames.Last(), Is.EqualTo(DiagnosticReport.LogSection));
            Assert.That(report.IndexOf(DiagnosticReport.HeaderFor(DiagnosticReport.LogSection), StringComparison.Ordinal),
                Is.GreaterThan(report.IndexOf(DiagnosticReport.HeaderFor(DiagnosticReport.OnboardingSection), StringComparison.Ordinal)));
        }

        [Test]
        public void EmptySections_ReadAsNoneRatherThanBeingSkipped()
        {
            // A brand-new game has unlocked no tile, placed no item, registered
            // no debug toggle and captured no log line — every one of those
            // must say so out loud.
            var report = Render(GameState.CreateNew());

            foreach (var listLabel in new[]
                     {
                         DiagnosticReport.UnlockedTilesLabel,
                         DiagnosticReport.GreenSpacesLabel,
                         DiagnosticReport.FrontierLabel,
                         DiagnosticReport.UnbuiltLotVariantsLabel,
                         DiagnosticReport.PlacedItemsLabel,
                         DiagnosticReport.DecorationsLabel,
                     })
            {
                Assert.That(BodyOf(report, listLabel), Is.EqualTo(DiagnosticReport.EmptyMarker),
                    listLabel + " must read as (none), not vanish");
            }

            Assert.That(SectionBody(report, DiagnosticReport.DebugSection).Trim(),
                Is.EqualTo(DiagnosticReport.EmptyMarker));
            Assert.That(SectionBody(report, DiagnosticReport.LogSection).Trim(),
                Is.EqualTo(DiagnosticReport.EmptyMarker));
        }

        /// <summary>The first line under a <c>label:</c> line, trimmed.</summary>
        private static string BodyOf(string report, string label)
        {
            var lines = report.Split('\n');
            for (var i = 0; i < lines.Length - 1; i++)
            {
                if (lines[i].Trim() == label + ":")
                {
                    return lines[i + 1].Trim();
                }
            }

            return null;
        }

        /// <summary>Everything between one section header and the next.</summary>
        internal static string SectionBody(string report, string section)
        {
            var lines = report.Split('\n');
            var body = new List<string>();
            var inside = false;
            foreach (var line in lines)
            {
                if (line.StartsWith(DiagnosticReport.HeaderFence, StringComparison.Ordinal))
                {
                    if (inside)
                    {
                        break;
                    }

                    inside = line == DiagnosticReport.HeaderFor(section);
                    continue;
                }

                if (inside)
                {
                    body.Add(line);
                }
            }

            return string.Join("\n", body);
        }
    }
}
