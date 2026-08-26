using System;
using System.Collections.Generic;

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

        /// <summary>
        /// The Unity command-line switch that requests the debug variant
        /// (#734). `pr-build.yml` and `rc-build.yml` pass it through
        /// <c>game-ci/unity-builder</c>'s <c>customParameters</c> input, which
        /// is the only channel that actually reaches the Unity process inside
        /// the builder's Docker container — see
        /// <see cref="BuildCommandLineFlag"/>.
        /// </summary>
        public const string CommandLineFlag = "-doggiehoodDebugBuild";

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
        /// <c>DOGGIEHOOD_DEBUG_BUILD</c> environment variable. This is the
        /// local/editor channel; CI must use the two-channel overload,
        /// because the variable never crosses into the build container.
        /// </summary>
        public static bool IsDebugBuildRequested(string envValue)
        {
            return BuildEnvironmentFlag.IsEnabled(envValue);
        }

        /// <summary>
        /// True when either channel requests the debug variant: the
        /// <c>DOGGIEHOOD_DEBUG_BUILD</c> environment variable (local and
        /// editor builds) or the <see cref="CommandLineFlag"/> switch on
        /// Unity's command line (CI, where the environment variable is
        /// stranded outside game-ci's Docker container — #734, the same
        /// defect #731 found on the emulator flag).
        /// </summary>
        public static bool IsDebugBuildRequested(string envValue, IReadOnlyList<string> commandLineArgs)
        {
            return BuildEnvironmentFlag.IsEnabled(envValue)
                   || BuildCommandLineFlag.IsEnabled(commandLineArgs, CommandLineFlag);
        }
    }
}
