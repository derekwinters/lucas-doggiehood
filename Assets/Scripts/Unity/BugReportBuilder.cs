using System;
using System.Collections.Generic;
using System.Globalization;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #692: the thin Unity half of the bug report — it collects the facts only
    /// the engine and the live scene know (build/device/screen/uptime, the
    /// buffered log tail, where each dog is standing right now) and hands them to
    /// the engine-free <see cref="DiagnosticReport"/>, which owns every rendering
    /// decision.
    ///
    /// <para>No decision logic lives here: this class gathers and forwards. That
    /// is what keeps the whole payload unit-testable in <c>CoreTests</c> against a
    /// hand-built <see cref="GameState"/>, with no engine at all.</para>
    /// </summary>
    public static class BugReportBuilder
    {
        /// <summary>Report timestamp format — ISO-8601 UTC, sortable and
        /// unambiguous across devices.</summary>
        public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        /// <summary>Build flavor as it reads in the report.</summary>
        public const string DevelopmentFlavor = "development";

        public const string ReleaseFlavor = "release";

        /// <summary>Renders the whole snapshot for the live game.</summary>
        public static string Build(
            GameState state,
            DebugToggleRegistry toggles,
            DiagnosticLogBuffer log,
            Transform worldRoot,
            DateTime nowUtc)
        {
            return DiagnosticReport.Render(
                state,
                TuningConfig.Active,
                toggles,
                Environment(nowUtc),
                log != null ? log.Entries : new List<DiagnosticLogEntry>(),
                DogWorldPositions(worldRoot));
        }

        /// <summary>The build/device facts, as engine-free Core data.</summary>
        public static DiagnosticEnvironment Environment(DateTime nowUtc)
        {
            return new DiagnosticEnvironment(
                appVersion: Application.version,
                buildFlavor: Flavor(),
                platform: Application.platform.ToString(),
                deviceModel: SystemInfo.deviceModel,
                operatingSystem: SystemInfo.operatingSystem,
                screenWidth: Screen.width,
                screenHeight: Screen.height,
                screenDpi: Screen.dpi,
                timestamp: nowUtc.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture),
                sessionUptimeSeconds: Time.realtimeSinceStartupAsDouble);
        }

        /// <summary>Which APK this is — the flavor plus the application id, so a
        /// report from the side-by-side <c>.debug</c> build is distinguishable
        /// from one off the store build.</summary>
        private static string Flavor()
        {
            var flavor = Debug.isDebugBuild ? DevelopmentFlavor : ReleaseFlavor;
            return flavor + " " + Application.identifier;
        }

        /// <summary>Where each dog's view is standing at this instant, keyed by
        /// dog name. Only the scene knows this, which is exactly why Core takes
        /// it as data; a null root (or a dog with no view) simply reports no
        /// position rather than failing the snapshot.</summary>
        private static IReadOnlyDictionary<string, GridPoint> DogWorldPositions(Transform worldRoot)
        {
            var positions = new Dictionary<string, GridPoint>();
            if (worldRoot == null)
            {
                return positions;
            }

            foreach (var view in worldRoot.GetComponentsInChildren<DogView>(true))
            {
                if (view.Dog == null || view.Dog.Name == null)
                {
                    continue;
                }

                var position = view.transform.position;
                positions[view.Dog.Name] = new GridPoint(position.x, position.z);
            }

            return positions;
        }
    }
}
