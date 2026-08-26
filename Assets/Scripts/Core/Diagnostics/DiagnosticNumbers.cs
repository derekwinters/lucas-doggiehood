namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #692/#161: the named numbers the diagnostic-report payload is sized and
    /// formatted by — never bare literals in a method body.
    /// </summary>
    public static class DiagnosticNumbers
    {
        /// <summary>How many of the most recent log lines a report carries (the
        /// issue's <c>DiagnosticLogTailSize</c>). It bounds both the Unity-layer
        /// ring buffer and the rendered <c>LOG</c> section, so the buffer can
        /// never grow without limit and the report can never balloon.</summary>
        public const int LogTailSize = 200;

        /// <summary>The report format's own version, emitted in the
        /// <c>REPORT</c> header, so an old report is recognizable as an old
        /// format rather than a broken one.</summary>
        public const int ReportSchemaVersion = 1;

        /// <summary>Decimal places world/lot coordinates are printed to — enough
        /// to place a dog precisely without dumping float noise into the
        /// report.</summary>
        public const int CoordinateDecimals = 2;

        /// <summary>Decimal places the session uptime is printed to.</summary>
        public const int UptimeDecimals = 1;
    }
}
