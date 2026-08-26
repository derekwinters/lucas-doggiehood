using System;
using System.IO;
using System.Linq;
using Doggiehood.Core.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #692: the Unity-layer ring buffer behind the bug report's <c>LOG</c>
    /// section. It subscribes to <c>Application.logMessageReceived</c>, keeps the
    /// most recent <see cref="DiagnosticNumbers.LogTailSize"/> lines and nothing
    /// more — never growing without limit — and writes nothing to disk until a
    /// report is generated.
    /// </summary>
    public class DiagnosticLogBufferTests
    {
        private const string TestMessage = "something went sideways";
        private const string TestStackTrace = "at Doggiehood.Unity.HouseView.Wire () [0x00000]";

        private GameObject host;
        private DiagnosticLogBuffer buffer;

        [SetUp]
        public void CreateBuffer()
        {
            host = new GameObject("log-buffer-host");
            buffer = DiagnosticLogBuffer.Install(host);
        }

        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void Install_AddsExactlyOneBuffer_AndIsIdempotent()
        {
            var again = DiagnosticLogBuffer.Install(host);

            Assert.That(again, Is.SameAs(buffer), "installing twice reuses the one buffer");
            Assert.That(host.GetComponents<DiagnosticLogBuffer>().Length, Is.EqualTo(1));
        }

        [Test]
        public void AFreshBuffer_HasNoEntries()
        {
            Assert.That(buffer.Entries, Is.Empty);
        }

        [Test]
        public void RecordedLines_ArriveInOrder_NewestLast()
        {
            buffer.Record(LogType.Log, "first", string.Empty);
            buffer.Record(LogType.Warning, "second", string.Empty);

            var entries = buffer.Entries;

            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Message, Is.EqualTo("first"));
            Assert.That(entries[1].Message, Is.EqualTo("second"));
            Assert.That(entries[1].Severity, Is.EqualTo(DiagnosticLogSeverity.Warning));
        }

        [Test]
        public void AnExceptionLine_KeepsItsSeverityAndStackTrace()
        {
            buffer.Record(LogType.Exception, TestMessage, TestStackTrace);

            var entry = buffer.Entries.Single();

            Assert.That(entry.Severity, Is.EqualTo(DiagnosticLogSeverity.Exception));
            Assert.That(entry.Message, Is.EqualTo(TestMessage));
            Assert.That(entry.StackTrace, Is.EqualTo(TestStackTrace));
        }

        [Test]
        public void TheBuffer_IsBounded_AndNeverGrowsWithoutLimit()
        {
            var overflow = DiagnosticLogBuffer.Capacity + 50;
            for (var i = 0; i < overflow; i++)
            {
                buffer.Record(LogType.Log, "line-" + i, string.Empty);
            }

            var entries = buffer.Entries;

            Assert.That(entries.Count, Is.EqualTo(DiagnosticLogBuffer.Capacity),
                "a ring buffer, not a log file — it holds the tail and drops the rest");
            Assert.That(entries.First().Message, Is.EqualTo("line-" + (overflow - DiagnosticLogBuffer.Capacity)),
                "the oldest lines are the ones dropped");
            Assert.That(entries.Last().Message, Is.EqualTo("line-" + (overflow - 1)));
        }

        [Test]
        public void TheCapacity_IsTheCoreLogTailSize_NotASecondNumber()
        {
            Assert.That(DiagnosticLogBuffer.Capacity, Is.EqualTo(DiagnosticNumbers.LogTailSize),
                "the buffer and the rendered LOG section are sized by one constant (#161)");
        }

        [Test]
        public void WorldBootstrap_InstallsTheBufferBeforeAnyOtherStartupWork()
        {
            // "Early enough to catch startup errors" is an ordering rule, so it
            // is asserted as one: the install has to come before the first thing
            // Awake() does, which is loading the save.
            var source = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "Unity", "WorldBootstrap.cs"));

            var awake = source.IndexOf("private void Awake()", StringComparison.Ordinal);
            Assert.That(awake, Is.GreaterThanOrEqualTo(0), "Awake() is the startup entry point");

            var install = source.IndexOf(
                nameof(DiagnosticLogBuffer) + "." + nameof(DiagnosticLogBuffer.Install),
                awake, StringComparison.Ordinal);
            var loadSave = source.IndexOf("SaveStore.LoadOrCreate(", awake, StringComparison.Ordinal);

            Assert.That(install, Is.GreaterThan(awake), "the buffer is installed inside Awake()");
            Assert.That(install, Is.LessThan(loadSave),
                "and before any other startup work, so it catches the startup errors " +
                "that are hardest to reproduce on a tablet");
        }

        [Test]
        public void ARealUnityLogMessage_IsCaptured_ThroughTheSubscribedHandler()
        {
            // The buffer is only worth having if it catches what the engine logs
            // — including the startup errors that are hardest to reproduce.
            Debug.Log(TestMessage);

            Assert.That(buffer.Entries.Any(entry => entry.Message.Contains(TestMessage)), Is.True,
                "the buffer subscribes to Application.logMessageReceived");
        }
    }
}
