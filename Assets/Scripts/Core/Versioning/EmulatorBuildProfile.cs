using System;

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
        /// <c>DOGGIEHOOD_EMULATOR_BUILD</c> CI environment variable.
        /// </summary>
        public static bool IsEmulatorBuildRequested(string envValue)
        {
            return BuildEnvironmentFlag.IsEnabled(envValue);
        }
    }
}
