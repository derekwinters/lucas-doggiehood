using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards the input wiring (#107): CameraRig polls the legacy Input
    /// Manager (Input.touchCount, touch phases, mouse fallbacks). The
    /// hand-authored ProjectSettings.asset never set activeInputHandler,
    /// and with the key absent Unity 6 does not reliably enable the legacy
    /// backend — the new Input System package isn't installed, so every
    /// Input.* call was silently dead in the editor and on device. The
    /// legacy Input class itself ships in Unity's always-present
    /// InputLegacyModule (there is no com.unity.modules.input manifest
    /// module — CI proved that on this PR's first round).
    ///
    /// Serialization-level assertion, per docs/engineering/unity-serialization.md.
    /// </summary>
    public class InputHandlingTests
    {
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void ProjectSettings_PinTheLegacyInputManagerHandler()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            Assert.That(settingsYaml, Does.Match(@"activeInputHandler: 0\b"),
                "activeInputHandler is not pinned to 0 (legacy Input Manager) — CameraRig\'s Input polling would be disabled (#107)");
        }
    }
}
