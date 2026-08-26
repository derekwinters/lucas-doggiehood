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
    /// #692: the two properties a bug-report snapshot is only trustworthy with —
    /// it is <b>read-only</b> (snapshotting a bug must not change the bug), and
    /// its <c>LOG</c> tail is bounded, ordered and complete.
    /// </summary>
    public class DiagnosticReportInvariantTests
    {
        private const string StackTraceLine = "at Doggiehood.Unity.HouseView.Wire () [0x00000]";

        private static string Render(GameState state, TuningConfig tuning, DebugToggleRegistry toggles,
            IReadOnlyList<DiagnosticLogEntry> log)
        {
            return DiagnosticReport.Render(
                state, tuning, toggles, DiagnosticReportSectionTests.Environment(), log);
        }

        [Test]
        public void Invariant_RenderingIsReadOnly_TwiceOverProducesTheSameBytes()
        {
            var state = GameState.CreateNew();
            var tuning = new TuningConfig();
            var toggles = new DebugToggleRegistry();
            toggles.Register("show-backyard-fences", true);
            var log = new List<DiagnosticLogEntry>
            {
                new DiagnosticLogEntry(DiagnosticLogSeverity.Warning, "something odd"),
            };

            var first = Render(state, tuning, toggles, log);
            var second = Render(state, tuning, toggles, log);

            Assert.That(second, Is.EqualTo(first),
                "a report is a snapshot, not an event — the same state renders the same bytes");
        }

        [Test]
        public void Invariant_RenderingLeavesTheGameStateTuningAndTogglesExactlyAsTheyWere()
        {
            var state = GameState.CreateNew();
            var tuning = new TuningConfig();
            var toggles = new DebugToggleRegistry();
            toggles.Register("show-backyard-fences", true);

            var saveBefore = SaveCodec.Save(state);
            var coinsBefore = state.Wallet.Coins;
            var payoutBefore = tuning.QuestPayout;
            var questsBefore = state.Quests.ActiveQuests.Count();
            var dogsBefore = state.Dogs.Count;

            Render(state, tuning, toggles, new List<DiagnosticLogEntry>());

            Assert.That(SaveCodec.Save(state), Is.EqualTo(saveBefore),
                "snapshotting a bug must not change the bug");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore));
            Assert.That(tuning.QuestPayout, Is.EqualTo(payoutBefore));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(questsBefore));
            Assert.That(state.Dogs.Count, Is.EqualTo(dogsBefore));
            Assert.That(toggles.IsOn("show-backyard-fences"), Is.True);
        }

        [Test]
        public void TheLogSection_KeepsAtMostTheTailSize_NewestLast()
        {
            var overflow = DiagnosticNumbers.LogTailSize + 25;
            var log = new List<DiagnosticLogEntry>();
            for (var i = 0; i < overflow; i++)
            {
                log.Add(new DiagnosticLogEntry(DiagnosticLogSeverity.Info, "line-" + i));
            }

            var body = DiagnosticReportSectionTests.BodyOf(
                Render(GameState.CreateNew(), new TuningConfig(), new DebugToggleRegistry(), log), "LOG");
            var lines = body.Split('\n').Where(line => line.Contains("line-")).ToArray();

            Assert.That(lines.Length, Is.EqualTo(DiagnosticNumbers.LogTailSize),
                "the tail is capped so a report can never balloon");
            Assert.That(lines.First(), Does.Contain("line-" + (overflow - DiagnosticNumbers.LogTailSize)),
                "the oldest lines are the ones dropped");
            Assert.That(lines.Last(), Does.Contain("line-" + (overflow - 1)),
                "newest last — the interesting lines sit at the end of the report");
        }

        [Test]
        public void TheLogSection_CarriesSeverityAndPreservesAnExceptionsStackTrace()
        {
            var log = new List<DiagnosticLogEntry>
            {
                new DiagnosticLogEntry(DiagnosticLogSeverity.Exception, "NullReferenceException", StackTraceLine),
            };

            var body = DiagnosticReportSectionTests.BodyOf(
                Render(GameState.CreateNew(), new TuningConfig(), new DebugToggleRegistry(), log), "LOG");

            Assert.That(body, Does.Contain("[" + DiagnosticLogSeverity.Exception + "]"));
            Assert.That(body, Does.Contain("NullReferenceException"));
            Assert.That(body, Does.Contain(StackTraceLine),
                "an exception in the report must be as actionable as it was in the console");
        }

        [Test]
        public void TheLogSection_IsTheLastThingInTheReport()
        {
            var log = new List<DiagnosticLogEntry>
            {
                new DiagnosticLogEntry(DiagnosticLogSeverity.Error, "the very last line"),
            };

            var report = Render(GameState.CreateNew(), new TuningConfig(), new DebugToggleRegistry(), log);

            Assert.That(report.TrimEnd('\n'), Does.EndWith("the very last line"),
                "a truncated report has to be recognizable as truncated (#695 leans on this)");
        }
    }
}
