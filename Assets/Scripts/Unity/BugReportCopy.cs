using System.Globalization;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: the Unity-side copy for the two bug-report confirmations. Copy stays
    /// out of engine-free Core (rule #2), and stays <b>ASCII-only</b> because the
    /// bundled DejaVu Sans is what actually ships (#291).
    ///
    /// <para>Each line is one short sentence that has to fit the toast pill's
    /// one-line budget, like every other approved toast line
    /// (docs/specs/ui/toast.md) — an EditMode guard measures them.</para>
    /// </summary>
    public static class BugReportCopy
    {
        private const string CopiedPrefix = "Bug report copied (";
        private const string CopiedSuffix = ")";
        /// <summary>The saved line is deliberately terser than the copied one:
        /// with a full timestamped filename after it, "Bug report saved: …"
        /// measured 981px against the pill's 934px one-line budget, and the pill
        /// is not widened for copy (#578/#675). The filename itself starts with
        /// "bugreport", so the shorter lead-in loses nothing.</summary>
        private const string SavedPrefix = "Saved: ";

        /// <summary>Bytes per kilobyte, so the size readout is a named
        /// conversion rather than an inline 1024 (#161).</summary>
        private const float CharactersPerKilobyte = 1024f;

        /// <summary>Decimal places on the size readout — enough to tell a 12 KB
        /// snapshot from a 120 KB one at a glance.</summary>
        private const string SizeFormat = "F1";

        private const string KilobyteUnit = " KB";

        /// <summary>"Bug report copied (12.3 KB)" — the confirmation that the
        /// clipboard now holds the whole snapshot, and how big it is.</summary>
        public static string Copied(int characters)
        {
            var kilobytes = characters / CharactersPerKilobyte;
            return CopiedPrefix
                + kilobytes.ToString(SizeFormat, CultureInfo.InvariantCulture)
                + KilobyteUnit
                + CopiedSuffix;
        }

        /// <summary>"Saved: bugreport-20260826-180411.txt" — names the file so
        /// the player knows which one to go and fetch.</summary>
        public static string Saved(string fileName)
        {
            return SavedPrefix + (fileName ?? string.Empty);
        }
    }
}
