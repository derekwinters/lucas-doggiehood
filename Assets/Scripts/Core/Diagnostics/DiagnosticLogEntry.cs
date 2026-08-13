namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #692: one captured log line — severity, message and (for an exception)
    /// its stack trace — as plain Core data. The Unity layer's ring buffer
    /// records these; <see cref="DiagnosticReport"/> renders the tail of them
    /// into the report's final <c>LOG</c> section.
    /// </summary>
    public readonly struct DiagnosticLogEntry
    {
        public DiagnosticLogEntry(DiagnosticLogSeverity severity, string message, string stackTrace = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }

        /// <summary>How loud the line was.</summary>
        public DiagnosticLogSeverity Severity { get; }

        /// <summary>The logged message text (never null).</summary>
        public string Message { get; }

        /// <summary>The captured stack trace, or empty when there was none.
        /// Preserved verbatim so an exception in the report is as actionable as
        /// it was in the console.</summary>
        public string StackTrace { get; }
    }
}
