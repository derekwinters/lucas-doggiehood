using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Versioning
{
    /// <summary>
    /// The command-line half of the build-variant flag convention (#731):
    /// reads a boolean switch such as <c>-doggiehoodEmulatorBuild</c> out of
    /// Unity's own argument vector.
    ///
    /// It exists because an environment variable cannot reach Unity in CI.
    /// <c>game-ci/unity-builder</c> does not run the editor in the workflow
    /// step's process — it runs it inside a Docker container, forwarding only
    /// a fixed allowlist of variables (<c>UNITY_*</c>, <c>BUILD_*</c>,
    /// <c>ANDROID_*</c>, <c>CUSTOM_PARAMETERS</c>, <c>GITHUB_*</c>,
    /// <c>RUNNER_*</c>). A repo-specific variable set with the step's
    /// <c>env:</c> is therefore visible on the runner and never inside the
    /// container, so <c>Environment.GetEnvironmentVariable</c> returns null in
    /// the Unity process. The builder's <c>customParameters</c> input, by
    /// contrast, is forwarded as <c>CUSTOM_PARAMETERS</c> and appended
    /// verbatim to the <c>unity-editor</c> command line — which is what
    /// <see cref="Environment.GetCommandLineArgs"/> then hands back.
    ///
    /// A bare switch means "on". An explicit following value is parsed with
    /// the same truthy convention as the environment channel
    /// (<see cref="BuildEnvironmentFlag"/>), so <c>-flag false</c> and
    /// <c>FLAG=false</c> agree rather than contradicting each other.
    /// </summary>
    public static class BuildCommandLineFlag
    {
        /// <summary>Unity switches start with a dash; a following token that does is the next switch, not this one's value.</summary>
        private const char SwitchPrefix = '-';

        public static bool IsEnabled(IReadOnlyList<string> commandLineArgs, string flagName)
        {
            if (commandLineArgs == null || string.IsNullOrWhiteSpace(flagName))
            {
                return false;
            }

            var flag = flagName.Trim();
            for (var i = 0; i < commandLineArgs.Count; i++)
            {
                var argument = commandLineArgs[i];
                if (argument == null || !argument.Trim().Equals(flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return BuildEnvironmentFlag.IsEnabled(ValueAfter(commandLineArgs, i));
            }

            return false;
        }

        /// <summary>
        /// The explicit value following the flag, or the truthy literal that
        /// makes a bare switch mean "on".
        /// </summary>
        private static string ValueAfter(IReadOnlyList<string> commandLineArgs, int flagIndex)
        {
            if (flagIndex + 1 >= commandLineArgs.Count)
            {
                return BuildEnvironmentFlag.EnabledValue;
            }

            var next = commandLineArgs[flagIndex + 1];
            if (string.IsNullOrWhiteSpace(next) || next.Trim()[0] == SwitchPrefix)
            {
                return BuildEnvironmentFlag.EnabledValue;
            }

            return next;
        }
    }
}
