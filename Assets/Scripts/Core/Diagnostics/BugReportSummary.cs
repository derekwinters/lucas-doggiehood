using System.Text;

namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #695: the one short line a shared bug report travels with — app version,
    /// device, and when it was taken.
    ///
    /// <para><b>Invariant — a shared bug report is never silently truncated.</b>
    /// The report itself travels as a <i>file attachment</i>; this line is the
    /// message body beside it. That split exists because a report is tens of
    /// kilobytes and a receiving app is free to truncate a long text extra (SMS
    /// certainly will) — a report that arrives with its <c>LOG</c> section
    /// quietly cut off is worse than one that never sent, because nobody can
    /// tell it happened. So the body carries only this: enough to identify the
    /// build if the attachment is dropped entirely, and never the report.</para>
    ///
    /// <para>Engine-free (rule #2) so the rule is unit-tested with no Unity
    /// install: the Unity layer hands over a <see cref="DiagnosticEnvironment"/>
    /// and this decides what the line says.</para>
    /// </summary>
    public static class BugReportSummary
    {
        /// <summary>What the line opens with, so a stray message in an inbox is
        /// recognizable for what it is. ASCII only (#291).</summary>
        public const string Label = "Doggiehood bug report";

        /// <summary>Between the facts — a pipe reads cleanly in a message body
        /// and in an email subject alike.</summary>
        public const string Separator = " | ";

        /// <summary>The hard cap that keeps this a label rather than a payload.
        /// Comfortably longer than any real device string, and short enough that
        /// no receiving app has a reason to trim it (#161 — a named number, not
        /// a literal in a method body).</summary>
        public const int MaxLength = 200;

        /// <summary>What a line break in a device string is replaced with, so
        /// the summary can never become a block of text.</summary>
        private const char LineBreakReplacement = ' ';

        /// <summary>"Doggiehood bug report | 0.16.0-abc1234 | Google Pixel Tablet
        /// | 2026-08-26T18:04:11Z" — one line, always under
        /// <see cref="MaxLength"/>, whatever the device calls itself.</summary>
        public static string Line(DiagnosticEnvironment environment)
        {
            var line = new StringBuilder()
                .Append(Label)
                .Append(Separator)
                .Append(environment.AppVersion)
                .Append(Separator)
                .Append(environment.DeviceModel)
                .Append(Separator)
                .Append(environment.Timestamp)
                .ToString();

            return Truncate(Flatten(line));
        }

        /// <summary>Collapses any line break into a space — a device model is
        /// whatever the manufacturer wrote, and one line means one line.</summary>
        private static string Flatten(string value)
        {
            return value.Replace('\r', LineBreakReplacement).Replace('\n', LineBreakReplacement);
        }

        /// <summary>Caps the line at <see cref="MaxLength"/>. Losing the tail of
        /// an absurd device string is fine; the report is the attachment.</summary>
        private static string Truncate(string value)
        {
            return value.Length <= MaxLength ? value : value.Substring(0, MaxLength);
        }
    }
}
