using System;

namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #695: the Android <c>FileProvider</c> authority a shared bug report's
    /// <c>content://</c> URI is issued under.
    ///
    /// <para>This is the one string in the share path that fails <b>at runtime on
    /// the device</b> when it is wrong — a mismatch between the manifest's
    /// <c>android:authorities</c> and the authority the runtime asks
    /// <c>FileProvider.getUriForFile</c> for is invisible to the test suite; the
    /// share sheet simply refuses the file. So the rule is stated once, here, and
    /// both ends read it: the manifest through
    /// <see cref="ManifestAuthority"/>'s Gradle <c>${applicationId}</c>
    /// placeholder, and the runtime through <see cref="For"/> on the live
    /// application id.</para>
    ///
    /// <para>It is <b>derived</b>, never typed, for the same reason
    /// <see cref="Doggiehood.Core.Versioning.ApplicationIdSuffix"/> exists (#80):
    /// the side-by-side <c>.debug</c> build has a different application id, and
    /// two apps sharing one authority string cannot both be installed. Hard-coding
    /// <c>com.derekwinters.doggiehood.fileprovider</c> would break exactly the
    /// build Lucas is most likely to be running when he hits a bug (#734).</para>
    ///
    /// <para>Engine-free (rule #2): a string rule, unit-tested with no Unity
    /// install.</para>
    /// </summary>
    public static class FileProviderAuthority
    {
        /// <summary>Appended to the application id. Any suffix works as long as
        /// it is unique to this app; this one says what the provider is for.</summary>
        public const string Suffix = ".fileprovider";

        /// <summary>Gradle's manifest placeholder for the application id being
        /// built. The manifest cannot know whether it is being merged into the
        /// release build or the <c>.debug</c> one, so it defers to Gradle — the
        /// same derivation as <see cref="For"/>, one build step earlier.</summary>
        public const string ApplicationIdPlaceholder = "${applicationId}";

        /// <summary>What the plug-in manifest's <c>android:authorities</c> must
        /// read, verbatim.</summary>
        public static string ManifestAuthority
        {
            get { return ApplicationIdPlaceholder + Suffix; }
        }

        /// <summary>The authority for a given application id. Idempotent, so a
        /// caller that already holds an authority cannot double the suffix.</summary>
        public static string For(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                throw new ArgumentException(
                    "An application id is required to derive the FileProvider authority.",
                    nameof(applicationId));
            }

            if (applicationId.EndsWith(Suffix, StringComparison.Ordinal))
            {
                return applicationId;
            }

            return applicationId + Suffix;
        }
    }
}
