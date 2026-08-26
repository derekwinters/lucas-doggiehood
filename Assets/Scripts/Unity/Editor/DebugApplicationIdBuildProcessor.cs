using System;
using System.Collections.Generic;
using Doggiehood.Core.Versioning;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Doggiehood.Unity.Editor
{
    /// <summary>
    /// Applies the `.debug` Android application id suffix (#80) before a
    /// build when the build asks for it — either via
    /// <see cref="DebugBuildEnvironmentVariable"/> being truthy ("1" or
    /// "true", case-insensitive), which is the local/editor channel, or via
    /// <see cref="ApplicationIdSuffix.CommandLineFlag"/> on Unity's command
    /// line, which is the only channel that reaches the editor inside
    /// game-ci's build container (#734). So debug builds (pr-build.yml,
    /// rc-build.yml) can be installed side-by-side with a release build
    /// (release-please.yml, which asks through neither channel) on the same
    /// device. The permanent identifier is restored after the build so
    /// repeated local/CI builds don't compound the suffix.
    /// </summary>
    public class DebugApplicationIdBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string DebugBuildEnvironmentVariable = "DOGGIEHOOD_DEBUG_BUILD";

        /// <summary>Grep-able marker for the build log line written when the suffix applies.</summary>
        private const string AppliedSuffixLogPrefix = "[Doggiehood] Debug applicationId suffix applied:";

        private static string _originalApplicationId;
        private static bool _suffixApplied;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => ApplyIfRequested();

        public void OnPostprocessBuild(BuildReport report) => RestoreIfApplied();

        /// <summary>
        /// Appends the debug suffix to the Android application id if this
        /// build's real environment or command line requests it.
        /// </summary>
        public static void ApplyIfRequested()
        {
            ApplyIfRequested(
                Environment.GetEnvironmentVariable(DebugBuildEnvironmentVariable),
                Environment.GetCommandLineArgs());
        }

        /// <summary>
        /// Appends the debug suffix to the Android application id if either
        /// channel requests it — <see cref="DebugBuildEnvironmentVariable"/>
        /// being truthy, or <see cref="ApplicationIdSuffix.CommandLineFlag"/>
        /// appearing in Unity's command line.
        ///
        /// Both channels exist because neither covers both cases (#734, the
        /// same defect #731 found on the emulator flag). A local or editor
        /// build sets the environment variable; a CI build cannot, because
        /// <c>game-ci/unity-builder</c> runs Unity inside a Docker container
        /// and forwards only a fixed allowlist of variables, so a
        /// repo-specific one is stranded on the runner. CI therefore passes
        /// the switch through the builder's <c>customParameters</c> input,
        /// which is appended to the <c>unity-editor</c> command line.
        ///
        /// Both parameters are injected rather than read here so EditMode
        /// tests can drive either channel; the parameterless overload supplies
        /// the real ones. Public (rather than private) so those tests can call
        /// it without constructing a <see cref="BuildReport"/> — Unity's Bee
        /// build strips non-public members from an assembly's ref.dll, so
        /// `internal` isn't visible to the separate EditMode test assembly.
        /// </summary>
        public static void ApplyIfRequested(string envValue, IReadOnlyList<string> commandLineArgs)
        {
            var isDebugBuild = ApplicationIdSuffix.IsDebugBuildRequested(envValue, commandLineArgs);

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

            // Leaves a trace in the Unity build log, so a CI run's own output
            // says whether the suffix applied rather than leaving it to be
            // inferred from the finished APK (#734).
            Debug.Log($"{AppliedSuffixLogPrefix} {suffixed}.");
        }

        /// <summary>
        /// Restores the identifier captured by <see cref="ApplyIfRequested"/>,
        /// if a suffix was applied. A no-op otherwise.
        /// </summary>
        public static void RestoreIfApplied()
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
