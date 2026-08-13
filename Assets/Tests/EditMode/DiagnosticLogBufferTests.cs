using System;
using System.IO;
using System.Linq;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #692: the Unity-layer log ring buffer that feeds a bug report's final
    /// <c>LOG</c> section. It must be genuinely bounded — a diagnostic aid that
    /// grows without limit is a memory leak — it must write nothing to disk
    /// until a report is generated, and it must be installed early enough to
    /// catch startup errors.
    /// </summary>
    public class DiagnosticLogBufferTests
    {
        private GameObject host;
        private DiagnosticLogBuffer buffer;

        [SetUp]
        public void CreateBuffer()
        {
            // Built INACTIVE so OnEnable never subscribes to the engine's log
            // pipe — these tests drive Record directly, and a live subscription
            // would fold the test runner's own chatter into the assertions.
            host = new GameObject("diagnostic-log-buffer");
            host.SetActive(false);
            buffer = host.AddComponent<DiagnosticLogBuffer>();
        }

        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void Capacity_IsTheSharedCoreTailSize()
        {
            Assert.That(DiagnosticLogBuffer.Capacity, Is.EqualTo(DiagnosticNumbers.LogTailSize),
                "the buffer and the report's LOG section are bounded by the same number");
        }

        [Test]
        public void Record_KeepsEntriesOldestFirst()
        {
            buffer.Record(DiagnosticLogSeverity.Log, "first", string.Empty);
            buffer.Record(DiagnosticLogSeverity.Warning, "second", string.Empty);

            var entries = buffer.Entries;

            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Message, Is.EqualTo("first"));
            Assert.That(entries[1].Message, Is.EqualTo("second"));
            Assert.That(entries[1].Severity, Is.EqualTo(DiagnosticLogSeverity.Warning));
        }

        [Test]
        public void Record_IsBoundedAndDropsTheOldestLinesFirst()
        {
            for (var i = 0; i < DiagnosticLogBuffer.Capacity * 2; i++)
            {
                buffer.Record(DiagnosticLogSeverity.Log, "line-" + i, string.Empty);
            }

            var entries = buffer.Entries;

            Assert.That(entries.Count, Is.EqualTo(DiagnosticLogBuffer.Capacity),
                "a ring buffer never grows without limit");
            Assert.That(entries.First().Message,
                Is.EqualTo("line-" + DiagnosticLogBuffer.Capacity));
            Assert.That(entries.Last().Message,
                Is.EqualTo("line-" + (DiagnosticLogBuffer.Capacity * 2 - 1)),
                "newest last, matching the report's LOG ordering");
        }

        [Test]
        public void Record_PreservesAnExceptionsStackTrace()
        {
            buffer.Record(DiagnosticLogSeverity.Exception, "boom", "at Foo()");

            Assert.That(buffer.Entries.Single().StackTrace, Is.EqualTo("at Foo()"));
        }

        [Test]
        public void SeverityFor_MapsEveryEngineLogTypeOntoCoreData()
        {
            Assert.That(DiagnosticLogBuffer.SeverityFor(LogType.Log), Is.EqualTo(DiagnosticLogSeverity.Log));
            Assert.That(DiagnosticLogBuffer.SeverityFor(LogType.Warning), Is.EqualTo(DiagnosticLogSeverity.Warning));
            Assert.That(DiagnosticLogBuffer.SeverityFor(LogType.Assert), Is.EqualTo(DiagnosticLogSeverity.Assert));
            Assert.That(DiagnosticLogBuffer.SeverityFor(LogType.Error), Is.EqualTo(DiagnosticLogSeverity.Error));
            Assert.That(DiagnosticLogBuffer.SeverityFor(LogType.Exception), Is.EqualTo(DiagnosticLogSeverity.Exception));
        }

        [Test]
        public void Install_IsIdempotentAndCapturesFromTheMomentItIsInstalled()
        {
            var liveHost = new GameObject("live-log-host");
            try
            {
                var installed = DiagnosticLogBuffer.Install(liveHost);
                Assert.That(DiagnosticLogBuffer.Install(liveHost), Is.SameAs(installed),
                    "installing twice must not stack two buffers on one host");

                var marker = "diagnostic-log-buffer-marker-" + Guid.NewGuid();
                Debug.Log(marker);

                Assert.That(installed.Entries.Any(entry => entry.Message == marker), Is.True,
                    "it hooks the log pipe on install, not later");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(liveHost);
            }
        }

        [Test]
        public void WorldBootstrap_InstallsTheBufferBeforeAnyOtherStartupWork()
        {
            // "Early enough to capture startup errors" is an ordering rule, so
            // it is asserted as one: the install has to come before the very
            // first thing Awake() does, which is loading the save.
            var source = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "Unity", "WorldBootstrap.cs"));

            var awake = source.IndexOf("private void Awake()", StringComparison.Ordinal);
            Assert.That(awake, Is.GreaterThanOrEqualTo(0), "WorldBootstrap.Awake() is the startup entry point");

            var install = source.IndexOf(
                nameof(DiagnosticLogBuffer) + "." + nameof(DiagnosticLogBuffer.Install), awake, StringComparison.Ordinal);
            var loadSave = source.IndexOf("SaveStore.LoadOrCreate(", awake, StringComparison.Ordinal);

            Assert.That(install, Is.GreaterThan(awake), "the buffer is installed inside Awake()");
            Assert.That(install, Is.LessThan(loadSave),
                "it is installed before any other startup work, so it catches startup errors");
        }
    }
}
