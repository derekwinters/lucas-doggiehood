using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards that the project ships with the Doggiehood app icon instead
    /// of silently falling back to the Unity default logo (#130).
    /// </summary>
    public class AppIconTests
    {
        private const string IconAssetPath = "Assets/Art/Icon/app-icon.png";

        [Test]
        public void DefaultIcon_IsConfigured_SoTheUnityLogoNeverShips()
        {
            // Nothing in any scene references the icon texture, so on a
            // fresh Library rebuild (CI) it may not have been imported yet
            // when this test runs, leaving PlayerSettings' reference
            // transiently null. Force the import first — this also pins
            // that the asset actually lives at the expected path.
            AssetDatabase.ImportAsset(IconAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            Assert.That(iconTexture, Is.Not.Null,
                $"the app icon texture is missing or unimportable at {IconAssetPath}");

            // BuildTargetGroup.Unknown is the "default icon" slot that
            // applies to every platform without an explicit override.
            var icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);

            Assert.That(icons, Is.Not.Null.And.Not.Empty,
                "no default app icon is configured in PlayerSettings — the build would ship the Unity logo");
            Assert.That(icons.Any(icon => icon == iconTexture), Is.True,
                "the default app icon slot does not reference the Doggiehood icon texture");
        }
    }
}
