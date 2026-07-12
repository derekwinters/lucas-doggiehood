using System;
using Doggiehood.Core.Versioning;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Doggiehood.Unity.Editor
{
    /// <summary>
    /// Applies the `.debug` Android application id suffix (#80) before a
    /// build when the <see cref="DebugBuildEnvironmentVariable"/> environment
    /// variable is set to a truthy value ("1" or "true", case-insensitive),
    /// so debug builds (pr-build.yml, rc-build.yml) can be installed
    /// side-by-side with a release build (release-build.yml, which never
    /// sets the variable) on the same device. The permanent identifier is
    /// restored after the build so repeated local/CI builds don't compound
    /// the suffix.
    /// </summary>
    public class DebugApplicationIdBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string DebugBuildEnvironmentVariable = "DOGGIEHOOD_DEBUG_BUILD";

        private static string _originalApplicationId;
        private static bool _suffixApplied;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => ApplyIfRequested();

        public void OnPostprocessBuild(BuildReport report) => RestoreIfApplied();

        /// <summary>
        /// Appends the debug suffix to the Android application id if
        /// <see cref="DebugBuildEnvironmentVariable"/> is truthy. Internal
        /// (rather than private) so EditMode tests can drive it directly
        /// without constructing a <see cref="BuildReport"/>.
        /// </summary>
        internal static void ApplyIfRequested()
        {
            var envValue = Environment.GetEnvironmentVariable(DebugBuildEnvironmentVariable);
            var isDebugBuild = ApplicationIdSuffix.IsDebugBuildRequested(envValue);

            var current = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var suffixed = ApplicationIdSuffix.Apply(current, isDebugBuild);

            if (suffixed == current)
            {
                _suffixApplied = false;
                return;
            }

            _originalApplicationId = current;
            _suffixApplied = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, suffixed);
        }

        /// <summary>
        /// Restores the identifier captured by <see cref="ApplyIfRequested"/>,
        /// if a suffix was applied. A no-op otherwise.
        /// </summary>
        internal static void RestoreIfApplied()
        {
            if (!_suffixApplied)
            {
                return;
            }

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, _originalApplicationId);
            _suffixApplied = false;
            _originalApplicationId = null;
        }
    }
}
