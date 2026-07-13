using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards that the project ships with the Doggiehood app icon instead
    /// of silently falling back to the Unity default logo (#130).
    /// </summary>
    public class AppIconTests
    {
        [Test]
        public void DefaultIcon_IsConfigured_SoTheUnityLogoNeverShips()
        {
            // BuildTargetGroup.Unknown is the "default icon" slot that
            // applies to every platform without an explicit override.
            var icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);

            Assert.That(icons, Is.Not.Null.And.Not.Empty,
                "no default app icon is configured in PlayerSettings — the build would ship the Unity logo");
            Assert.That(icons.Any(icon => icon != null), Is.True,
                "the default app icon slot exists but its texture reference is broken (null)");
        }
    }
}
