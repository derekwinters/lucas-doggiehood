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
    /// #692: the thin Unity-side collector that hands
    /// <see cref="DiagnosticReport"/> the two things engine-free Core cannot
    /// know — what device this is, and where each dog's view is standing right
    /// now — and then lets Core do all the formatting.
    ///
    /// <para>No decision logic lives here: it reads
    /// <c>Application</c>/<c>SystemInfo</c>/<c>Screen</c> and the live
    /// <see cref="DogView"/> transforms into plain Core values and calls
    /// <see cref="DiagnosticReport.Render"/>. Nothing is transmitted; the
    /// returned string goes only where the player sends it
    /// (docs/specs/product-scope.md).</para>
    /// </summary>
    public static class BugReportBuilder
    {
        /// <summary>Report timestamp shape — ISO-8601 UTC, so a report from any
        /// device is comparable with any other.</summary>
        public const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

        /// <summary>Build flavor reported for a development build.</summary>
        public const string DevelopmentFlavor = "development";

        /// <summary>Build flavor reported for a shipping build.</summary>
        public const string ReleaseFlavor = "release";

        /// <summary>Captures the device/build facts as engine-free Core data.
        /// <paramref name="nowUtc"/> is passed in (never read from a clock here)
        /// so the capture is pinnable in a test.</summary>
        public static DiagnosticEnvironment CaptureEnvironment(DateTime nowUtc)
        {
            return new DiagnosticEnvironment(
                appVersion: Application.version,
                buildFlavor: Debug.isDebugBuild ? DevelopmentFlavor : ReleaseFlavor,
                platform: Application.platform.ToString(),
                deviceModel: SystemInfo.deviceModel,
                operatingSystem: SystemInfo.operatingSystem,
                screenWidth: Screen.width,
                screenHeight: Screen.height,
                screenDpi: Screen.dpi,
                timestamp: nowUtc.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture),
                sessionUptimeSeconds: Time.realtimeSinceStartupAsDouble);
        }

        /// <summary>Where each dog's view is standing, keyed by dog name — the
        /// one part of "what the game looks like right now" that lives in the
        /// scene rather than in <see cref="GameState"/>. Empty when there is no
        /// world root (the panel still reports every dog's Core location).</summary>
        public static IReadOnlyDictionary<string, GridPoint> CaptureDogPositions(Transform worldRoot)
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

        /// <summary>Renders the whole snapshot for the live game.
        /// <paramref name="logBuffer"/> and <paramref name="worldRoot"/> are
        /// optional — a report is still worth having without them, and the
        /// report says so rather than omitting the section.</summary>
        public static string Build(
            GameState state,
            DebugToggleRegistry toggles,
            DiagnosticLogBuffer logBuffer,
            Transform worldRoot,
            DateTime nowUtc)
        {
            return DiagnosticReport.Render(
                state,
                TuningConfig.Active,
                toggles,
                CaptureEnvironment(nowUtc),
                logBuffer != null ? logBuffer.Entries : Array.Empty<DiagnosticLogEntry>(),
                CaptureDogPositions(worldRoot));
        }
    }
}
