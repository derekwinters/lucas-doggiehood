using System.IO;
using System.Text.RegularExpressions;
using Doggiehood.Core.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #695: the hand-authored Android plug-in the share sheet needs — a
    /// <c>FileProvider</c> declaration and the <c>file_paths.xml</c> resource it
    /// points at.
    ///
    /// <para>Per docs/engineering/unity-serialization.md, hand-authored Unity/
    /// Android configuration is asserted at the <b>text</b> level: Unity and the
    /// Gradle manifest merger ignore what they do not understand, silently, at
    /// build time, and a wrong authority string does not fail anything until a
    /// device refuses the file. These assertions pin the exact values so a bad
    /// edit fails the suite instead of shipping.</para>
    ///
    /// <para>The plug-in is an <b>Android Library Project</b>
    /// (<c>*.androidlib</c>), not a custom main manifest. That distinction is the
    /// point of <see cref="Invariant_ThePluginManifestOnlyAdds_ItNeverReplacesUnitysOwn"/>:
    /// a library manifest is <i>merged into</i> Unity's, so it cannot drop the
    /// activity the game launches through — and Unity does not support Android
    /// resources outside a library project, which is where
    /// <c>file_paths.xml</c> has to live.</para>
    /// </summary>
    public class BugReportShareProviderTests
    {
        private const string PluginRoot = "Assets/Plugins/Android/doggiehood-share.androidlib";
        private const string ManifestPath = PluginRoot + "/src/main/AndroidManifest.xml";
        private const string FilePathsPath = PluginRoot + "/src/main/res/xml/file_paths.xml";
        private const string BuildGradlePath = PluginRoot + "/build.gradle";

        private static string Read(string path)
        {
            Assert.That(File.Exists(path), Is.True, path + " must exist");
            return File.ReadAllText(path);
        }

        // ---------------------------------------------------------------
        // The provider declaration
        // ---------------------------------------------------------------

        [Test]
        public void TheManifest_DeclaresTheFileProvider_WithTheExactAttributesAndroidRequires()
        {
            var manifest = Read(ManifestPath);

            Assert.That(manifest, Does.Contain(
                "android:name=\"" + AndroidShareTarget.FileProviderClassName + "\""),
                "the provider class, pinned so a rename cannot ship silently");
            Assert.That(manifest, Does.Contain("android:exported=\"false\""),
                "a FileProvider is never exported");
            Assert.That(manifest, Does.Contain("android:grantUriPermissions=\"true\""),
                "without this the receiving app cannot read the file it was handed");
        }

        [Test]
        public void TheManifestAuthority_IsDerivedFromTheApplicationId_NotHardCoded()
        {
            var manifest = Read(ManifestPath);

            Assert.That(manifest, Does.Contain(
                "android:authorities=\"" + FileProviderAuthority.ManifestAuthority + "\""),
                "the manifest defers to Gradle's ${applicationId} placeholder");
            Assert.That(manifest, Does.Not.Contain("com.derekwinters.doggiehood"),
                "no build's application id is baked in — the side-by-side .debug build " +
                "(#80/#734) must get its own authority for free");
        }

        [Test]
        public void TheProvider_PointsAtTheFilePathsResource()
        {
            var manifest = Read(ManifestPath);

            Assert.That(manifest, Does.Contain(
                "android:name=\"" + AndroidShareTarget.FileProviderPathsMetaDataName + "\""));
            Assert.That(manifest, Does.Contain("android:resource=\"@xml/file_paths\""));
        }

        [Test]
        public void TheRuntimeAuthority_IsTheSameRuleTheManifestUses()
        {
            // The one string that fails only on device: both ends must derive it
            // the same way, from the live application id.
            Assert.That(AndroidShareTarget.Authority,
                Is.EqualTo(FileProviderAuthority.For(Application.identifier)));
            Assert.That(FileProviderAuthority.ManifestAuthority,
                Is.EqualTo(FileProviderAuthority.ApplicationIdPlaceholder + FileProviderAuthority.Suffix));
        }

        // ---------------------------------------------------------------
        // The paths the provider is allowed to serve
        // ---------------------------------------------------------------

        [Test]
        public void FilePaths_GrantsExactlyTheBugReportsFolder_AndNothingElse()
        {
            var paths = Read(FilePathsPath);
            var folder = BugReportFile.FolderName + "/";

            Assert.That(paths, Does.Contain("<paths"));
            Assert.That(paths, Does.Contain("path=\"" + folder + "\""),
                "the provider serves the bug-reports folder, not the whole sandbox");
            Assert.That(paths, Does.Not.Contain("path=\".\""),
                "never grant the storage root");
            Assert.That(paths, Does.Not.Contain("path=\"\""));
        }

        [Test]
        public void FilePaths_CoversBothPlacesUnityCanPutPersistentDataPath()
        {
            // Application.persistentDataPath is the app's external files dir by
            // default and its internal files dir when Write Permission is
            // Internal; only one of these two roots is ever the live one, and
            // which is a Player Setting rather than something this code picks.
            var paths = Read(FilePathsPath);

            Assert.That(paths, Does.Contain("<external-files-path"));
            Assert.That(paths, Does.Contain("<files-path"));
        }

        // ---------------------------------------------------------------
        // It is a library plug-in, not a replacement for Unity's manifest
        // ---------------------------------------------------------------

        [Test]
        public void ThePlugin_IsAnAndroidLibraryProject_WithItsOwnNamespace()
        {
            var gradle = Read(BuildGradlePath);

            Assert.That(gradle, Does.Contain("apply plugin: 'com.android.library'"),
                "a library module, so its manifest merges into Unity's");
            Assert.That(Regex.IsMatch(gradle, "namespace\\s+\"[a-z0-9_.]+\""), Is.True,
                "AGP 8+ takes the namespace from build.gradle, not the manifest");
        }

        [Test]
        public void Invariant_ThePluginManifestOnlyAdds_ItNeverReplacesUnitysOwn()
        {
            // A custom Assets/Plugins/Android/AndroidManifest.xml *overrides*
            // Unity's generated manifest wholesale — get one element wrong and
            // the game stops launching, on device, where no test can see it. A
            // library manifest can only add. This asserts we stayed on the side
            // that cannot break the app.
            Assert.That(File.Exists("Assets/Plugins/Android/AndroidManifest.xml"), Is.False,
                "no custom main manifest — the FileProvider is added by a library plug-in");

            var manifest = Read(ManifestPath);
            Assert.That(manifest, Does.Not.Contain("<activity"),
                "a library manifest declares no activity");
            Assert.That(manifest, Does.Not.Contain("android.intent.category.LAUNCHER"),
                "and never claims the launcher");
            Assert.That(manifest, Does.Not.Contain("package="),
                "AGP 8+ rejects a package attribute; the namespace lives in build.gradle");
        }

        // ---------------------------------------------------------------
        // Committed .meta files (docs/engineering/unity-serialization.md)
        // ---------------------------------------------------------------

        [Test]
        public void EveryPluginFileAndFolder_HasACommittedMetaWithAGuid()
        {
            foreach (var path in new[]
                     {
                         "Assets/Plugins",
                         "Assets/Plugins/Android",
                         PluginRoot,
                         PluginRoot + "/src",
                         PluginRoot + "/src/main",
                         PluginRoot + "/src/main/res",
                         PluginRoot + "/src/main/res/xml",
                         BuildGradlePath,
                         ManifestPath,
                         FilePathsPath,
                     })
            {
                var meta = path + ".meta";
                Assert.That(File.Exists(meta), Is.True, meta + " must be committed");
                Assert.That(Regex.IsMatch(File.ReadAllText(meta), "guid: [0-9a-f]{32}"), Is.True,
                    meta + " must carry a pinned guid");
            }
        }
    }
}
