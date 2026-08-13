using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: where a bug report lands on the device. A timestamped plain-text
    /// file under <c>persistentDataPath/bug-reports/</c> — the app's own
    /// sandbox, which needs no storage permission and no manifest entry.
    ///
    /// <para>This is deliberately a small, separate seam rather than inline
    /// file code in the Settings panel: sharing a saved report to the Android
    /// share sheet (#695) reuses exactly this path, so the file naming and
    /// location are defined in one place for both destinations.</para>
    ///
    /// <para><b>Local only.</b> Writing is the whole of it — nothing here
    /// uploads, syncs, or transmits (docs/specs/product-scope.md).</para>
    /// </summary>
    public static class BugReportFile
    {
        /// <summary>Folder under <c>persistentDataPath</c> reports collect in.</summary>
        public const string DirectoryName = "bug-reports";

        /// <summary>Filename lead-in, so reports sort together.</summary>
        public const string FileNamePrefix = "bugreport-";

        /// <summary>Plain text — a bug report is meant to be pasted and read.</summary>
        public const string FileNameExtension = ".txt";

        /// <summary>UTC timestamp shape in the filename: sortable, ASCII-safe,
        /// second-resolution so two reports never collide in practice.</summary>
        public const string TimestampFormat = "yyyyMMdd-HHmmss";

        /// <summary>The folder reports are written to.</summary>
        public static string DirectoryPath
        {
            get { return Path.Combine(Application.persistentDataPath, DirectoryName); }
        }

        /// <summary>The filename a report captured at
        /// <paramref name="timestampUtc"/> gets.</summary>
        public static string FileNameFor(DateTime timestampUtc)
        {
            return FileNamePrefix
                + timestampUtc.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture)
                + FileNameExtension;
        }

        /// <summary>Writes <paramref name="report"/> to a timestamped file,
        /// creating the folder if needed, and returns the full path — which the
        /// caller shows in its toast, and which #695 hands to the share
        /// sheet.</summary>
        public static string Write(string report, DateTime timestampUtc)
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, FileNameFor(timestampUtc));
            File.WriteAllText(path, report ?? string.Empty);
            return path;
        }
    }
}
