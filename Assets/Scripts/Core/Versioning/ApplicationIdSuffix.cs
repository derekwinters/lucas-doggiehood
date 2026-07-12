using System;

namespace Doggiehood.Core.Versioning
{
    /// <summary>
    /// Composes the Android application id used for debug builds. The
    /// permanent id (<c>com.derekwinters.doggiehood</c>, see #80) is used
    /// unchanged for release builds; debug builds append <c>.debug</c> so
    /// a debug build can be installed side-by-side with a release build on
    /// the same device.
    /// </summary>
    public static class ApplicationIdSuffix
    {
        public const string Debug = ".debug";

        public static string Apply(string baseApplicationId, bool isDebugBuild)
        {
            if (string.IsNullOrWhiteSpace(baseApplicationId))
            {
                throw new ArgumentException("Base application id is required.", nameof(baseApplicationId));
            }

            if (!isDebugBuild || baseApplicationId.EndsWith(Debug, StringComparison.Ordinal))
            {
                return baseApplicationId;
            }

            return baseApplicationId + Debug;
        }

        /// <summary>
        /// Parses the truthy/falsy convention used by the
        /// <c>DOGGIEHOOD_DEBUG_BUILD</c> CI environment variable.
        /// </summary>
        public static bool IsDebugBuildRequested(string envValue)
        {
            if (string.IsNullOrWhiteSpace(envValue))
            {
                return false;
            }

            var trimmed = envValue.Trim();
            return trimmed == "1" || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
