using System;

namespace Doggiehood.Core.Versioning
{
    /// <summary>
    /// The shared truthy/falsy convention for the boolean environment
    /// variables that select a build variant in CI —
    /// <c>DOGGIEHOOD_DEBUG_BUILD</c> (#80) and
    /// <c>DOGGIEHOOD_EMULATOR_BUILD</c> (#648). Only "1" and "true"
    /// (case-insensitive, surrounding whitespace ignored) enable a flag, so
    /// an unset or accidentally-empty variable can never silently turn a
    /// variant on.
    /// </summary>
    public static class BuildEnvironmentFlag
    {
        public static bool IsEnabled(string envValue)
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
