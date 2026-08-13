using System.Globalization;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: the Unity-side copy for the two bug-report confirmation toasts
    /// (docs/specs/ui/toast.md). Copy stays out of engine-free Core (rule #2),
    /// exactly like <see cref="ToastCopy"/>; these two lines are the debug
    /// tab's, so they live beside it rather than in the player-facing table.
    ///
    /// <para>ASCII only (#291) — the bundled DejaVu Sans is what renders
    /// them.</para>
    /// </summary>
    public static class BugReportCopy
    {
        private const string CopiedPrefix = "Bug report copied";
        private const string SavedPrefix = "Bug report saved:";
        private const string KilobyteSuffix = " KB";

        /// <summary>Bytes per kilobyte for the reported size (#161).</summary>
        public const int BytesPerKilobyte = 1024;

        /// <summary>Decimal places the size is shown to.</summary>
        public const int SizeDecimals = 1;

        /// <summary>"Bug report copied (24.6 KB)" — the size is the useful part:
        /// it is how you tell at a glance that a real snapshot went to the
        /// clipboard rather than an empty string.</summary>
        public static string Copied(int characterCount)
        {
            return CopiedPrefix + " (" + Size(characterCount) + ")";
        }

        /// <summary>"Bug report saved: bugreport-20260813-143000.txt" — names the
        /// file so it can be found on the device.</summary>
        public static string Saved(string fileName)
        {
            return SavedPrefix + " " + fileName;
        }

        private static string Size(int characterCount)
        {
            var kilobytes = characterCount / (double)BytesPerKilobyte;
            return kilobytes.ToString(
                "F" + SizeDecimals.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture) + KilobyteSuffix;
        }
    }
}
