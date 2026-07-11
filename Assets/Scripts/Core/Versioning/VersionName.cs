using System;

namespace Doggiehood.Core.Versioning
{
    /// <summary>
    /// Composes human-readable version names for builds. The base version is
    /// the contents of the repo-root VERSION file (owned by release-please);
    /// debug builds append the short commit SHA so every build is uniquely
    /// identifiable (see #75).
    /// </summary>
    public static class VersionName
    {
        public static string ForDebugBuild(string baseVersion, string shortSha)
        {
            if (string.IsNullOrWhiteSpace(baseVersion))
            {
                throw new ArgumentException("Base version is required.", nameof(baseVersion));
            }

            if (string.IsNullOrWhiteSpace(shortSha))
            {
                throw new ArgumentException("Short commit SHA is required.", nameof(shortSha));
            }

            return baseVersion.Trim() + "-" + shortSha.Trim();
        }
    }
}
