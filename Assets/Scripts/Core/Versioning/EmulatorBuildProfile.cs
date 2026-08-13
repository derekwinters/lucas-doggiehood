using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Versioning
{
    /// <summary>
    /// The Unity-independent half of the emulator-targeted build variant
    /// (#648): the <c>DOGGIEHOOD_EMULATOR_BUILD</c> truthy convention and the
    /// <c>.emulator</c> Android applicationId suffix. The suffix lets the
    /// emulator APK sit side-by-side with the device build (and with the
    /// <c>.debug</c> build, see <see cref="ApplicationIdSuffix"/>) rather
    /// than replacing it.
    /// </summary>
    public static class EmulatorBuildProfile
    {
        public const string Suffix = ".emulator";

        /// <summary>
        /// The Unity command-line switch that requests the emulator variant
        /// (#731). The release workflows pass it through
        /// <c>game-ci/unity-builder</c>'s <c>customParameters</c> input, which
        /// is the only channel that actually reaches the Unity process inside
        /// the builder's Docker container — see
        /// <see cref="BuildCommandLineFlag"/>.
        /// </summary>
        public const string CommandLineFlag = "-doggiehoodEmulatorBuild";

        public static string Apply(string baseApplicationId, bool isEmulatorBuild)
        {
            if (string.IsNullOrWhiteSpace(baseApplicationId))
            {
                throw new ArgumentException("Base application id is required.", nameof(baseApplicationId));
            }

            if (!isEmulatorBuild || baseApplicationId.EndsWith(Suffix, StringComparison.Ordinal))
            {
                return baseApplicationId;
            }

            return baseApplicationId + Suffix;
        }

        /// <summary>
        /// Parses the truthy/falsy convention used by the
        /// <c>DOGGIEHOOD_EMULATOR_BUILD</c> environment variable. This is the
        /// local/editor channel; CI must use the two-channel overload,
        /// because the variable never crosses into the build container.
        /// </summary>
        public static bool IsEmulatorBuildRequested(string envValue)
        {
            return BuildEnvironmentFlag.IsEnabled(envValue);
        }

        /// <summary>
        /// True when either channel requests the emulator variant: the
        /// <c>DOGGIEHOOD_EMULATOR_BUILD</c> environment variable (local and
        /// editor builds) or the <see cref="CommandLineFlag"/> switch on
        /// Unity's command line (CI, where the environment variable is
        /// stranded outside game-ci's Docker container — #731).
        /// </summary>
        public static bool IsEmulatorBuildRequested(string envValue, IReadOnlyList<string> commandLineArgs)
        {
            return BuildEnvironmentFlag.IsEnabled(envValue)
                   || BuildCommandLineFlag.IsEnabled(commandLineArgs, CommandLineFlag);
        }
    }
}
