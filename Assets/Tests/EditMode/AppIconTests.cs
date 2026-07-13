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
            AssetDatabase.ImportAsset(IconAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            Assert.That(iconTexture, Is.Not.Null,
                $"the app icon texture is missing or unimportable at {IconAssetPath}");

            Assert.That(AssetDatabase.AssetPathToGUID(IconAssetPath), Is.EqualTo(IconGuid),
                "the icon's GUID changed — ProjectSettings' m_BuildTargetIcons reference would break");
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
    }
}
