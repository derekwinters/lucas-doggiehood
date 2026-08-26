using System;
using Doggiehood.Core.Diagnostics;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Diagnostics
{
    /// <summary>
    /// #695: the one-line summary a shared bug report travels with.
    ///
    /// <para>The whole point of this line is what it is <b>not</b>: the report
    /// body. A bug report is tens of kilobytes and a receiving app is free to
    /// truncate a long text extra (SMS certainly will), so the report travels as
    /// a file attachment and this short line rides in the message body — enough
    /// that if the attachment is dropped entirely, the recipient still knows
    /// which build it came from and can ask for the file.</para>
    /// </summary>
    public class BugReportSummaryTests
    {
        private static DiagnosticEnvironment EnvironmentWith(
            string appVersion = "0.16.0-abc1234",
            string deviceModel = "Google Pixel Tablet",
            string timestamp = "2026-08-26T18:04:11Z")
        {
            return new DiagnosticEnvironment(
                appVersion: appVersion,
                buildFlavor: "development com.derekwinters.doggiehood.debug",
                platform: "Android",
                deviceModel: deviceModel,
                operatingSystem: "Android OS 15",
                screenWidth: 1920,
                screenHeight: 1200,
                screenDpi: 240f,
                timestamp: timestamp,
                sessionUptimeSeconds: 91.5);
        }

        [Test]
        public void TheSummary_NamesTheBuild_TheDevice_AndWhenItWasTaken()
        {
            var line = BugReportSummary.Line(EnvironmentWith());

            Assert.That(line, Does.Contain("0.16.0-abc1234"), "app version");
            Assert.That(line, Does.Contain("Google Pixel Tablet"), "device model");
            Assert.That(line, Does.Contain("2026-08-26T18:04:11Z"), "timestamp");
        }

        [Test]
        public void TheSummary_SaysWhatItIs_SoAStrayMessageIsRecognizable()
        {
            Assert.That(BugReportSummary.Line(EnvironmentWith()),
                Does.StartWith(BugReportSummary.Label));
        }

        [Test]
        public void TheSummary_IsAsciiOnly_LikeEveryOtherPieceOfShippedCopy()
        {
            // #291: the bundled DejaVu Sans is what ships, and the line is also
            // read by whatever app the player picks.
            foreach (var character in BugReportSummary.Line(EnvironmentWith()))
            {
                Assert.That((int)character, Is.LessThan(128), "ASCII only: " + character);
            }
        }

        // ---------------------------------------------------------------
        // The truncation invariant, from the summary's side
        // ---------------------------------------------------------------

        [Test]
        public void Invariant_TheSummaryIsOneShortLine_NeverTheReportBody()
        {
            var line = BugReportSummary.Line(EnvironmentWith());

            Assert.That(line, Does.Not.Contain("\n"), "one line, never a block of text");
            Assert.That(line, Does.Not.Contain("\r"));
            Assert.That(line.Length, Is.LessThanOrEqualTo(BugReportSummary.MaxLength));
        }

        [Test]
        public void AnAbsurdlyLongDeviceModel_IsTruncatedRatherThanBecomingABodyOfText()
        {
            var line = BugReportSummary.Line(EnvironmentWith(deviceModel: new string('M', 4000)));

            Assert.That(line.Length, Is.EqualTo(BugReportSummary.MaxLength),
                "the summary is capped: it is a label, not a payload");
            Assert.That(line, Does.StartWith(BugReportSummary.Label));
        }

        [Test]
        public void ANewlineInADeviceString_IsFlattened_SoTheLineStaysOneLine()
        {
            var line = BugReportSummary.Line(EnvironmentWith(deviceModel: "Weird\r\nTablet"));

            Assert.That(line, Does.Not.Contain("\n"));
            Assert.That(line, Does.Not.Contain("\r"));
            Assert.That(line, Does.Contain("Weird"));
            Assert.That(line, Does.Contain("Tablet"));
        }

        [Test]
        public void TheSummary_NeverCarriesAReportSectionHeader()
        {
            // The failing case, asserted rather than trusted: if the report body
            // were ever inlined into the summary it would drag its section
            // headers along with it.
            var line = BugReportSummary.Line(EnvironmentWith());

            foreach (var section in DiagnosticReport.SectionNames)
            {
                Assert.That(line, Does.Not.Contain(DiagnosticReport.HeaderFor(section)),
                    "the summary is not the report (" + section + ")");
            }
        }

        [Test]
        public void MissingFacts_StillProduceAUsableLine_RatherThanThrowing()
        {
            var line = BugReportSummary.Line(new DiagnosticEnvironment(
                null, null, null, null, null, 0, 0, 0f, null, 0.0));

            Assert.That(line, Does.StartWith(BugReportSummary.Label));
            Assert.That(line, Does.Not.Contain("\n"));
            Assert.That(line.Length, Is.LessThanOrEqualTo(BugReportSummary.MaxLength));
        }
    }
}
