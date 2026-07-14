using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards the input wiring (#107): CameraRig polls the legacy Input
    /// Manager (Input.touchCount, touch phases, mouse fallbacks), which
    /// needs the com.unity.modules.input built-in module in the package
    /// manifest and the legacy activeInputHandler player setting. Both were
    /// missing from the hand-authored files, leaving every Input.* call
    /// silently dead in the editor and on device.
    ///
    /// Serialization-level assertions, per docs/engineering/unity-serialization.md.
    /// </summary>
    public class InputHandlingTests
    {
        private const string ManifestPath = "Packages/manifest.json";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void Manifest_IncludesTheLegacyInputModule()
        {
            var manifestJson = System.IO.File.ReadAllText(ManifestPath);

            Assert.That(manifestJson, Does.Contain("\"com.unity.modules.input\""),
                "Packages/manifest.json is missing com.unity.modules.input — every legacy Input.* call in CameraRig goes dead (#107)");
        }

        [Test]
        public void ProjectSettings_PinTheLegacyInputManagerHandler()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            Assert.That(settingsYaml, Does.Match(@"activeInputHandler: 0\b"),
                "activeInputHandler is not pinned to 0 (legacy Input Manager) — CameraRig's Input polling would be disabled (#107)");
        }
    }
}
