using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Guards the Android static splash image (#136 follow-up): the cover
    /// art is packed into the APK as a plain drawable and shown by the OS
    /// the moment the app opens — a different mechanism from the engine
    /// splash screen, whose background never rendered in headless CI builds
    /// (evidence on #136) and which stays parked.
    ///
    /// Serialization-level assertions, per docs/engineering/unity-serialization.md.
    /// </summary>
    public class AndroidStaticSplashTests
    {
        private const string SplashAssetPath = "Assets/Art/Splash/cover-art.png";
        private const string SplashGuid = "de77c4a46f2441dfa4bdebeccd7eaa25";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void CoverArtTexture_ExistsAtItsPinnedPathAndGuid()
        {
            AssetDatabase.ImportAsset(SplashAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashAssetPath);
            Assert.That(texture, Is.Not.Null,
                $"the cover art texture is missing or unimportable at {SplashAssetPath}");

            Assert.That(AssetDatabase.AssetPathToGUID(SplashAssetPath), Is.EqualTo(SplashGuid),
                "the cover art's GUID changed — ProjectSettings' androidSplashScreen reference would break");
        }

        [Test]
        public void AndroidStaticSplash_ReferencesTheCoverArtTexture()
        {
            var settingsYaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            // The static splash takes the Texture2D main asset (fileID
            // 2800000), unlike the engine splash background which needed the
            // Sprite sub-asset. \s+ tolerates Unity re-wrapping on resave.
            Assert.That(settingsYaml, Does.Match(
                @"androidSplashScreen: \{fileID: 2800000,\s+guid: " + SplashGuid + @",\s+type: 3\}"),
                "androidSplashScreen does not reference the cover art — the APK would launch without the static splash");
            Assert.That(settingsYaml, Does.Match(@"AndroidSplashScreenScale: 2\b"),
                "the static splash is not set to ScaleToFill (2) — the art would letterbox instead of filling the screen");
        }
    }
}
