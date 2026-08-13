using System.Collections.Generic;
using Doggiehood.Core.Diagnostics;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: a fixed-size ring buffer of the most recent log lines, feeding the
    /// bug report's final <c>LOG</c> section. Installed by
    /// <see cref="WorldBootstrap"/> as the very first thing it does, so it
    /// catches startup errors — the ones that are hardest to reproduce and
    /// impossible to read off a tablet otherwise.
    ///
    /// <para><b>Ring buffer only.</b> Nothing is written to disk until a report
    /// is actually generated, and the buffer is bounded at
    /// <see cref="Capacity"/> entries so a chatty session can never grow it
    /// without limit. Translating the engine's <see cref="LogType"/> into Core's
    /// <see cref="DiagnosticLogSeverity"/> here is what keeps the report
    /// renderer engine-free.</para>
    /// </summary>
    public sealed class DiagnosticLogBuffer : MonoBehaviour
    {
        /// <summary>How many lines are retained — the same bound the report's
        /// <c>LOG</c> section renders, so the buffer never holds more than a
        /// report can carry.</summary>
        public const int Capacity = DiagnosticNumbers.LogTailSize;

        private readonly Queue<DiagnosticLogEntry> entries = new Queue<DiagnosticLogEntry>(Capacity);

        /// <summary>Adds the buffer to <paramref name="host"/> (idempotent —
        /// an already-installed buffer is returned as-is) and returns it. It
        /// begins capturing the moment it is enabled.</summary>
        public static DiagnosticLogBuffer Install(GameObject host)
        {
            var existing = host.GetComponent<DiagnosticLogBuffer>();
            return existing != null ? existing : host.AddComponent<DiagnosticLogBuffer>();
        }

        /// <summary>The retained lines, oldest first — the order the report's
        /// <c>LOG</c> section emits them (newest last).</summary>
        public IReadOnlyList<DiagnosticLogEntry> Entries
        {
            get { return new List<DiagnosticLogEntry>(entries); }
        }

        /// <summary>Translates the engine's log type onto Core's engine-free
        /// severity, one for one.</summary>
        public static DiagnosticLogSeverity SeverityFor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return DiagnosticLogSeverity.Warning;
                case LogType.Assert:
                    return DiagnosticLogSeverity.Assert;
                case LogType.Error:
                    return DiagnosticLogSeverity.Error;
                case LogType.Exception:
                    return DiagnosticLogSeverity.Exception;
                default:
                    return DiagnosticLogSeverity.Log;
            }
        }

        /// <summary>Records one line, dropping the oldest once
        /// <see cref="Capacity"/> is reached. Public so the ring behavior is
        /// directly testable without driving the engine's log pipe.</summary>
        public void Record(DiagnosticLogSeverity severity, string message, string stackTrace)
        {
            entries.Enqueue(new DiagnosticLogEntry(severity, message, stackTrace));
            while (entries.Count > Capacity)
            {
                entries.Dequeue();
            }
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        private void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            Record(SeverityFor(type), message, stackTrace);
        }
    }
}
