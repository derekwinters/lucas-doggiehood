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
        private const string IconGuid = "98dccd106d954b90b1d9c604ed43c329";
        private const string AdaptiveBackgroundAssetPath = "Assets/Art/Icon/app-icon-background.png";
        private const string AdaptiveBackgroundGuid = "95336c4f55af42bc934e304cddd1d017";
        private const string AdaptiveForegroundAssetPath = "Assets/Art/Icon/app-icon-foreground.png";
        private const string AdaptiveForegroundGuid = "edb4eaefd10543289c324d86831fb595";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        // Deliberately NOT asserted via PlayerSettings.GetIconsForTargetGroup's
        // texture references: PlayerSettings deserializes at editor startup,
        // and on a fresh Library rebuild (CI) the icon texture — referenced by
        // nothing else — is imported lazily afterwards, so the in-memory
        // reference resolves null for that whole session even though the
        // serialized wiring is correct. Asserting at the serialization level
        // is order-independent and guards the same contract.

        [Test]
        public void IconTexture_ExistsAtItsPinnedPathAndGuid()
        {
            AssertTextureAtPinnedPathAndGuid(IconAssetPath, IconGuid, "m_BuildTargetIcons");
        }

        [TestCase(AdaptiveBackgroundAssetPath, AdaptiveBackgroundGuid)]
        [TestCase(AdaptiveForegroundAssetPath, AdaptiveForegroundGuid)]
        public void AdaptiveIconLayer_ExistsAtItsPinnedPathAndGuid(string assetPath, string guid)
        {
            AssertTextureAtPinnedPathAndGuid(assetPath, guid, "m_BuildTargetPlatformIcons");
        }

        private static void AssertTextureAtPinnedPathAndGuid(
            string assetPath, string guid, string referencingSettingsKey)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Assert.That(iconTexture, Is.Not.Null,
                $"the app icon texture is missing or unimportable at {assetPath}");

            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(guid),
                $"the icon's GUID changed — ProjectSettings' {referencingSettingsKey} reference would break");
        }

        [Test]
        public void DefaultIcon_IsConfigured_SoTheUnityLogoNeverShips()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            var iconsBlock = System.Text.RegularExpressions.Regex.Match(
                settingsYaml,
                @"m_BuildTargetIcons:\s*\n(?:.*\n)*?(?=  \w|\w)");
            Assert.That(iconsBlock.Success, Is.True,
                "no m_BuildTargetIcons block in ProjectSettings — the build would ship the Unity logo");
            Assert.That(iconsBlock.Value, Does.Contain(IconGuid),
                "the default app icon slot does not reference the Doggiehood icon texture's GUID");
        }

        [Test]
        public void AndroidAdaptiveIcon_IsConfigured_WithBackgroundAndForegroundLayers()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            var platformIconsBlock = System.Text.RegularExpressions.Regex.Match(
                settingsYaml,
                @"m_BuildTargetPlatformIcons:\s*\n(?:.*\n)*?(?=  \w|\w)");
            Assert.That(platformIconsBlock.Success, Is.True,
                "no m_BuildTargetPlatformIcons block in ProjectSettings — Android would fall back to the square legacy icon");

            Assert.That(platformIconsBlock.Value, Does.Contain("m_BuildTarget: Android"),
                "m_BuildTargetPlatformIcons has no Android entry — the adaptive icon is not wired");
            Assert.That(platformIconsBlock.Value, Does.Contain(AdaptiveBackgroundGuid),
                "the Android adaptive icon does not reference the background layer texture's GUID");
            Assert.That(platformIconsBlock.Value, Does.Contain(AdaptiveForegroundGuid),
                "the Android adaptive icon does not reference the foreground layer texture's GUID");
        }
    }
}
