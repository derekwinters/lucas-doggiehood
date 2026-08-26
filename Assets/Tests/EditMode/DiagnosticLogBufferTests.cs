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
    /// section. It hooks <c>Application.logMessageReceived</c> the moment it is
    /// installed, keeps the most recent
    /// <see cref="DiagnosticNumbers.LogTailSize"/> lines and nothing more — never
    /// growing without limit — and writes nothing to disk until a report is
    /// generated.
    ///
    /// <para>The fixture's shared buffer is installed and then deliberately
    /// <b>unhooked</b>: the record-mechanics tests drive <c>Record</c> directly,
    /// and an ambient engine log landing in the buffer mid-test would make them
    /// flaky. The three capture tests each install their own live buffer, so what
    /// they assert about the log pipe is exact.</para>
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
            buffer.StopCapturing();
        }

        [TearDown]
        public void Cleanup()
        {
            // Outside play mode nothing calls OnDisable/OnDestroy, so unhook the
            // log pipe explicitly rather than leaving a dead subscriber behind.
            buffer.StopCapturing();
            UnityEngine.Object.DestroyImmediate(host);
        }

        // ---------------------------------------------------------------
        // Ring-buffer mechanics
        // ---------------------------------------------------------------

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
            WithLiveBuffer(live => Assert.That(live.Entries, Is.Empty));
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

        // ---------------------------------------------------------------
        // Capturing the real log pipe
        // ---------------------------------------------------------------

        [Test]
        public void Install_ArmsCaptureImmediately_NotAtSomeLaterLifecycleCallback()
        {
            // A plain MonoBehaviour gets no OnEnable at all outside play mode, so
            // a buffer that waited for one would silently record nothing — and in
            // the player it would still only start whenever the engine got round
            // to the callback, which is not what "installed early enough to catch
            // startup errors" means.
            WithLiveBuffer(live =>
            {
                Assert.That(live.IsCapturing, Is.True, "installed means hooked, right now");

                DiagnosticLogBuffer.Install(live.gameObject);

                Assert.That(live.IsCapturing, Is.True,
                    "a second install must neither double-subscribe nor unhook");
            });
        }

        [Test]
        public void ARealUnityLogMessage_IsCaptured_ThroughTheSubscribedHandler()
        {
            // The buffer is only worth having if it catches what the engine logs
            // — including the startup errors that are hardest to reproduce.
            var marker = "doggiehood-log-marker-" + Guid.NewGuid();

            WithLiveBuffer(live =>
            {
                Debug.Log(marker);

                Assert.That(live.Entries.Any(entry => entry.Message.Contains(marker)), Is.True,
                    "the buffer is hooked to Application.logMessageReceived from the moment " +
                    "Install() returns");
            });
        }

        [Test]
        public void StopCapturing_UnhooksTheLogPipe()
        {
            var marker = "doggiehood-log-marker-" + Guid.NewGuid();

            WithLiveBuffer(live =>
            {
                live.StopCapturing();
                var recordedBefore = live.Entries.Count;

                Debug.Log(marker);

                Assert.That(live.IsCapturing, Is.False);
                Assert.That(live.Entries.Count, Is.EqualTo(recordedBefore),
                    "an unhooked buffer records nothing");
                Assert.That(live.Entries.Any(entry => entry.Message.Contains(marker)), Is.False);
            });
        }

        // ---------------------------------------------------------------
        // Installed early enough to matter
        // ---------------------------------------------------------------

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

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        /// <summary>Runs <paramref name="body"/> against a freshly installed,
        /// still-hooked buffer on its own host, then unhooks and destroys it —
        /// so a capture assertion is never contaminated by a line logged before
        /// the test began.</summary>
        private static void WithLiveBuffer(Action<DiagnosticLogBuffer> body)
        {
            var liveHost = new GameObject("live-log-buffer-host");
            DiagnosticLogBuffer live = null;
            try
            {
                live = DiagnosticLogBuffer.Install(liveHost);
                body(live);
            }
            finally
            {
                if (live != null)
                {
                    live.StopCapturing();
                }

                UnityEngine.Object.DestroyImmediate(liveHost);
            }
        }
    }
}
