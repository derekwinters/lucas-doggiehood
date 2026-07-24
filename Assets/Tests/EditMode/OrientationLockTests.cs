using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards that the app ships locked to landscape (#22, #256).
    /// Doggiehood is a landscape tablet game; `defaultScreenOrientation`
    /// must be Unity's fixed landscape value, not AutoRotation (which would
    /// let the neighbourhood flip to portrait and break the 1920x1200 UI
    /// reference). Asserted at the serialization level per
    /// docs/engineering/unity-serialization.md: PlayerSettings deserializes
    /// once at editor startup, so regexing the committed YAML is the
    /// order-independent guard, and pinning the exact enum int stops a bad
    /// edit shipping silently.
    /// </summary>
    public class OrientationLockTests
    {
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        // Unity's UIOrientation enum, verified against real project files
        // (see docs/engineering/unity-serialization.md): Portrait = 0,
        // PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3,
        // AutoRotation = 4. Locking to landscape means the fixed
        // LandscapeLeft value, NOT AutoRotation.
        private const int LandscapeLeft = 3;
        private const int AutoRotation = 4;

        [Test]
        public void DefaultScreenOrientation_IsLockedToLandscape_NotAutoRotation()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            var match = Regex.Match(settingsYaml, @"^\s*defaultScreenOrientation:\s*(\d+)\s*$",
                RegexOptions.Multiline);
            Assert.That(match.Success, Is.True,
                "no defaultScreenOrientation entry in ProjectSettings — orientation would fall back to Unity's default");

            var value = int.Parse(match.Groups[1].Value);
            Assert.That(value, Is.Not.EqualTo(AutoRotation),
                "defaultScreenOrientation is AutoRotation (4) — the app is not locked to landscape (#22)");
            Assert.That(value, Is.EqualTo(LandscapeLeft),
                "defaultScreenOrientation must be LandscapeLeft (3) to lock the tablet to landscape (#22, #256)");
        }
    }
}
