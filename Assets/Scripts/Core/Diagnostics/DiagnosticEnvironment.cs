namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #692: the device/build facts a bug report needs, captured as engine-free
    /// Core data so <see cref="DiagnosticReport"/> can describe the device
    /// <b>without making a single engine call</b>. The thin Unity layer fills
    /// this in from <c>Application</c>/<c>SystemInfo</c>/<c>Screen</c>; Core
    /// only formats it, which is exactly what makes the whole payload
    /// deterministic under test.
    ///
    /// <para>Nothing here identifies a person. The game has no accounts, no
    /// network and no location data (docs/specs/product-scope.md); the only
    /// device-identifying values are the model and OS version, which are what
    /// makes an Android-specific bug diagnosable in the first place.</para>
    /// </summary>
    public readonly struct DiagnosticEnvironment
    {
        public DiagnosticEnvironment(
            string appVersion,
            string buildFlavor,
            string platform,
            string deviceModel,
            string operatingSystem,
            int screenWidth,
            int screenHeight,
            float screenDpi,
            string timestamp,
            double sessionUptimeSeconds)
        {
            AppVersion = appVersion ?? string.Empty;
            BuildFlavor = buildFlavor ?? string.Empty;
            Platform = platform ?? string.Empty;
            DeviceModel = deviceModel ?? string.Empty;
            OperatingSystem = operatingSystem ?? string.Empty;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            ScreenDpi = screenDpi;
            Timestamp = timestamp ?? string.Empty;
            SessionUptimeSeconds = sessionUptimeSeconds;
        }

        /// <summary>Build version string (release-please owns the value).</summary>
        public string AppVersion { get; }

        /// <summary>"debug" / "release-candidate" / "release" — which flavor is
        /// running, so a report from a store build is distinguishable.</summary>
        public string BuildFlavor { get; }

        /// <summary>Runtime platform name (e.g. "Android", "OSXEditor").</summary>
        public string Platform { get; }

        /// <summary>Device model string.</summary>
        public string DeviceModel { get; }

        /// <summary>Operating-system version string.</summary>
        public string OperatingSystem { get; }

        /// <summary>Screen width in pixels.</summary>
        public int ScreenWidth { get; }

        /// <summary>Screen height in pixels.</summary>
        public int ScreenHeight { get; }

        /// <summary>Screen DPI.</summary>
        public float ScreenDpi { get; }

        /// <summary>When the report was taken, pre-formatted by the caller so
        /// Core needs no clock (and a test can pin it).</summary>
        public string Timestamp { get; }

        /// <summary>Seconds since this session started.</summary>
        public double SessionUptimeSeconds { get; }
    }
}
