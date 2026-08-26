using System.Collections.Generic;
using Doggiehood.Core.Diagnostics;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: the in-memory tail of the Unity log, kept so a bug report can say
    /// what the console said just before things went sideways.
    ///
    /// <para>A fixed-size <b>ring buffer</b> of
    /// <see cref="Capacity"/> = <see cref="DiagnosticNumbers.LogTailSize"/>
    /// entries — it holds the tail and drops the rest, so it can never grow
    /// without limit on a long session. It writes <b>nothing to disk</b> until a
    /// report is generated, and it never leaves the device.</para>
    ///
    /// <para>Installed by <see cref="WorldBootstrap"/> as the very first thing it
    /// does, so it catches the startup errors that are hardest to reproduce on a
    /// tablet. Thin wiring only: it maps <c>UnityEngine.LogType</c> onto the
    /// engine-free <see cref="DiagnosticLogSeverity"/> and stores plain Core
    /// <see cref="DiagnosticLogEntry"/> data — the rendering decisions all live
    /// in <see cref="DiagnosticReport"/>.</para>
    /// </summary>
    public sealed class DiagnosticLogBuffer : MonoBehaviour
    {
        /// <summary>How many lines are kept. One constant with the rendered
        /// <c>LOG</c> section's cap (#161), so the buffer and the report can
        /// never disagree about the tail length.</summary>
        public const int Capacity = DiagnosticNumbers.LogTailSize;

        private readonly Queue<DiagnosticLogEntry> entries = new Queue<DiagnosticLogEntry>();
        private bool capturing;

        /// <summary>Adds the buffer to <paramref name="host"/> (or returns the one
        /// already there) and starts capturing <b>immediately</b>. Idempotent, so
        /// a second bootstrap pass cannot end up double-recording every line.
        ///
        /// <para>Capture is armed here rather than left to <c>OnEnable</c> on
        /// purpose: "installed early enough to catch startup errors" has to mean
        /// the log pipe is hooked the instant this returns, not whenever the
        /// engine next runs a lifecycle callback — and a plain MonoBehaviour gets
        /// no lifecycle callbacks at all outside play mode, so an editor-hosted
        /// buffer would silently record nothing.</para></summary>
        public static DiagnosticLogBuffer Install(GameObject host)
        {
            if (host == null)
            {
                return null;
            }

            var existing = host.GetComponent<DiagnosticLogBuffer>();
            var buffer = existing != null ? existing : host.AddComponent<DiagnosticLogBuffer>();
            buffer.StartCapturing();
            return buffer;
        }

        /// <summary>The buffered tail, oldest first — the order the report emits
        /// it in (newest last).</summary>
        public IReadOnlyList<DiagnosticLogEntry> Entries
        {
            get { return new List<DiagnosticLogEntry>(entries); }
        }

        /// <summary>Records one line, evicting the oldest once the buffer is
        /// full. Public so the wiring can be exercised without going through the
        /// engine's log pump.</summary>
        public void Record(LogType type, string message, string stackTrace)
        {
            entries.Enqueue(new DiagnosticLogEntry(SeverityOf(type), message, stackTrace));
            while (entries.Count > Capacity)
            {
                entries.Dequeue();
            }
        }

        /// <summary>Maps the engine's log type onto the engine-free Core
        /// severity, so nothing downstream of here names a UnityEngine type.</summary>
        public static DiagnosticLogSeverity SeverityOf(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return DiagnosticLogSeverity.Warning;
                case LogType.Error:
                case LogType.Assert:
                    return DiagnosticLogSeverity.Error;
                case LogType.Exception:
                    return DiagnosticLogSeverity.Exception;
                default:
                    return DiagnosticLogSeverity.Info;
            }
        }

        /// <summary>Whether this buffer is currently hooked to the log pipe.</summary>
        public bool IsCapturing => capturing;

        /// <summary>Hooks the log pipe. Safe to call twice — a doubled
        /// subscription would record every line twice.</summary>
        public void StartCapturing()
        {
            if (capturing)
            {
                return;
            }

            Application.logMessageReceived += HandleLog;
            capturing = true;
        }

        /// <summary>Unhooks the log pipe. Called when the component is disabled
        /// or destroyed, and callable directly for a host that is torn down
        /// somewhere those callbacks do not run (the editor outside play
        /// mode).</summary>
        public void StopCapturing()
        {
            if (!capturing)
            {
                return;
            }

            Application.logMessageReceived -= HandleLog;
            capturing = false;
        }

        private void OnEnable()
        {
            StartCapturing();
        }

        private void OnDisable()
        {
            StopCapturing();
        }

        private void OnDestroy()
        {
            StopCapturing();
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            Record(type, condition, stackTrace);
        }
    }
}
