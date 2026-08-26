using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: where a saved bug report lands on the device — a timestamped text
    /// file under <c>Application.persistentDataPath/bug-reports/</c>. Local disk
    /// only: no network call, no upload, no account (docs/specs/product-scope.md).
    ///
    /// <para>The file name carries the capture time to the second so two reports
    /// taken in one session never collide, and so the newest one is obvious in a
    /// file listing.</para>
    /// </summary>
    public static class BugReportFile
    {
        /// <summary>Folder the reports are written into, beside the save file.</summary>
        public const string FolderName = "bug-reports";

        /// <summary>Every report's file-name prefix, so a listing sorts them
        /// together.</summary>
        public const string FileNamePrefix = "bugreport-";

        /// <summary>Plain text — the report is meant to be pasted into an
        /// issue.</summary>
        public const string FileExtension = ".txt";

        /// <summary>Capture time, to the second, in the file name.</summary>
        public const string TimestampFormat = "yyyyMMdd-HHmmss";

        /// <summary>The folder saved reports live in on this device.</summary>
        public static string DirectoryPath
        {
            get { return Path.Combine(Application.persistentDataPath, FolderName); }
        }

        /// <summary>The file name a report captured at <paramref name="utc"/>
        /// gets.</summary>
        public static string FileNameFor(DateTime utc)
        {
            return FileNamePrefix
                + utc.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture)
                + FileExtension;
        }

        /// <summary>Writes <paramref name="report"/> to a timestamped file and
        /// returns its full path, creating the folder on first use.</summary>
        public static string Write(string report, DateTime utc)
        {
            var directory = DirectoryPath;
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, FileNameFor(utc));
            File.WriteAllText(path, report ?? string.Empty);
            return path;
        }
    }
}
