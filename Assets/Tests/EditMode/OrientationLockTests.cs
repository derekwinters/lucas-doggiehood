using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards that the app ships locked to landscape (#22, #256) at the
    /// serialization level (per docs/engineering/unity-serialization.md):
    /// PlayerSettings deserializes once at editor startup, so regexing the
    /// committed ProjectSettings YAML is the order-independent guard.
    ///
    /// "Locked to landscape" in this project (#22) means: auto-rotate between
    /// the two LANDSCAPE orientations, never portrait. That is
    /// defaultScreenOrientation = AutoRotation (4) WITH portrait disallowed
    /// and both landscape orientations allowed — NOT a single fixed
    /// LandscapeLeft, which would stop the tablet working when held the other
    /// way. The scalar alone locks nothing; the allowedAutorotate* flags do
    /// the landscape restriction, so all five values are pinned here. This
    /// matches SceneContractTests.Orientation_IsLockedToLandscape, which
    /// asserts the same contract through the PlayerSettings API.
    /// </summary>
    public class OrientationLockTests
    {
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        // Unity's UIOrientation enum, verified against real project files
        // (see docs/engineering/unity-serialization.md): Portrait = 0,
        // PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3,
        // AutoRotation = 4.
        private const int AutoRotation = 4;

        private static int ReadSetting(string yaml, string key)
        {
            var match = Regex.Match(yaml, @"^\s*" + Regex.Escape(key) + @":\s*(\d+)\s*$",
                RegexOptions.Multiline);
            Assert.That(match.Success, Is.True,
                $"no {key} entry in ProjectSettings — orientation would fall back to Unity's default");
            return int.Parse(match.Groups[1].Value);
        }

        [Test]
        public void Orientation_IsLockedToLandscapeOnly_AutoRotatingBetweenLandscapes()
        {
            var yaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            // AutoRotation is what lets both landscape orientations work; the
            // flags below restrict that rotation to landscape only (#22).
            Assert.That(ReadSetting(yaml, "defaultScreenOrientation"), Is.EqualTo(AutoRotation),
                "defaultScreenOrientation must be AutoRotation (4); the landscape lock comes from the allowedAutorotate flags, not a fixed single orientation (#22)");

            // Portrait is never allowed.
            Assert.That(ReadSetting(yaml, "allowedAutorotateToPortrait"), Is.EqualTo(0),
                "portrait must be disallowed (#22)");
            Assert.That(ReadSetting(yaml, "allowedAutorotateToPortraitUpsideDown"), Is.EqualTo(0),
                "upside-down portrait must be disallowed (#22)");

            // Both landscape orientations are allowed, so the tablet works
            // held either way.
            Assert.That(ReadSetting(yaml, "allowedAutorotateToLandscapeLeft"), Is.EqualTo(1),
                "landscape-left must be allowed (#22)");
            Assert.That(ReadSetting(yaml, "allowedAutorotateToLandscapeRight"), Is.EqualTo(1),
                "landscape-right must be allowed (#22)");
        }
    }
}
