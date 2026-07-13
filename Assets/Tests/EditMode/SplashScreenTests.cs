using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards that the app launches with the Doggiehood cover art as the
    /// splash screen background instead of the default Unity splash.
    ///
    /// Like <see cref="AppIconTests"/>, these assert at the serialization
    /// level: PlayerSettings deserializes at editor startup, so in-memory
    /// references to lazily-imported textures can resolve null on a fresh
    /// Library rebuild (CI) even when the wiring is correct.
    /// </summary>
    public class SplashScreenTests
    {
        private const string SplashAssetPath = "Assets/Art/Splash/cover-art.png";
        private const string SplashGuid = "de77c4a46f2441dfa4bdebeccd7eaa25";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        // The splash background is a Sprite reference, so ProjectSettings
        // points at the sprite sub-asset (fileID 21300000), NOT the
        // Texture2D main asset (2800000). Verified against real projects
        // (daggerfall-unity among others) — the same class of mistake as
        // the m_Kind: 5 icon bug, guarded here on purpose.
        private const long SpriteFileId = 21300000;

        [Test]
        public void SplashBackgroundTexture_ExistsAtItsPinnedPathAndGuid()
        {
            AppIconTests.AssertTextureAtPinnedPathAndGuid(
                SplashAssetPath, SplashGuid, "splashScreenBackgroundSourceLandscape/Portrait");
        }

        [Test]
        public void SplashBackgroundSprite_ImportsAtThePinnedFileId()
        {
            AssetDatabase.ImportAsset(SplashAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SplashAssetPath);
            Assert.That(sprite, Is.Not.Null,
                $"no Sprite sub-asset at {SplashAssetPath} — the texture must import with " +
                "textureType: 8 (Sprite) or the splash background reference resolves null");

            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out var guid, out long fileId),
                Is.True, "could not resolve the splash sprite's GUID and local file ID");
            Assert.That(guid, Is.EqualTo(SplashGuid),
                "the splash sprite's GUID changed — ProjectSettings' splashScreenBackgroundSource references would break");
            Assert.That(fileId, Is.EqualTo(SpriteFileId),
                "the splash sprite's local file ID drifted from 21300000 — the .meta's " +
                "internalIDToNameTable must pin it or ProjectSettings' references break on reimport");
        }

        [Test]
        public void SplashScreen_IsEnabled_WithTheUnityLogoHidden()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            Assert.That(settingsYaml, Does.Match(@"m_ShowUnitySplashScreen: 1\b"),
                "the splash screen is not enabled — the cover art would never show at launch");
            Assert.That(settingsYaml, Does.Match(@"m_ShowUnitySplashLogo: 0\b"),
                "the Unity logo is not hidden — the splash should show only the cover art");
        }

        [Test]
        public void SplashBackground_ReferencesTheCoverArtSprite_ForBothOrientations()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            // \s+ tolerates Unity re-wrapping the line on a future resave.
            Assert.That(settingsYaml, Does.Match(
                @"splashScreenBackgroundSourceLandscape: \{fileID: " + SpriteFileId
                + @",\s+guid: " + SplashGuid + @",\s+type: 3\}"),
                "the landscape splash background does not reference the cover art sprite");
            Assert.That(settingsYaml, Does.Match(
                @"splashScreenBackgroundSourcePortrait: \{fileID: " + SpriteFileId
                + @",\s+guid: " + SplashGuid + @",\s+type: 3\}"),
                "the portrait splash background does not reference the cover art sprite");
        }
    }
}
