using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #291: the runtime-built UGUI settings panel (#219) rendered as a magenta
    /// box with invisible text in the Android build — nothing serialized
    /// referenced the built-in <c>UI/Default</c> shader or a runtime font, so
    /// both were stripped. These are serialization-level guards (per
    /// docs/engineering/unity-serialization.md §4/§5): they assert the exact
    /// YAML text of the build-inclusion wiring, not live object references
    /// (which resolve lazily on a fresh CI Library and can't be trusted here).
    ///
    /// Built-in shader fileIDs live in <c>unity_builtin_extra</c> under the
    /// reserved GUID <c>0000000000000000f000000000000000</c>; the two relevant
    /// UGUI ones are verified against real Unity project files:
    /// <c>10753 = Sprites/Default</c>, <c>10770 = UI/Default</c>.
    /// </summary>
    public class UiBuildResourcesTests
    {
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const string BuiltinResourcesGuid = "0000000000000000f000000000000000";
        private const string UiDefaultShaderEntry =
            "{fileID: 10770, guid: 0000000000000000f000000000000000, type: 0}";
        private const string SpritesDefaultShaderEntry =
            "{fileID: 10753, guid: 0000000000000000f000000000000000, type: 0}";

        private const string FontAssetPath = "Assets/Art/UI/Fonts/Resources/DejaVuSans.ttf";
        private const string FontMetaPath = FontAssetPath + ".meta";
        private const string FontGuid = "e02e85fa80aa47bf8881400c475f94c5";

        // --- UI/Default shader must be retained in the build ---

        [Test]
        public void GraphicsSettings_AlwaysIncludes_TheUiDefaultShader()
        {
            var yaml = File.ReadAllText(GraphicsSettingsPath);

            Assert.That(yaml, Does.Contain("m_AlwaysIncludedShaders:"),
                "GraphicsSettings has no Always Included Shaders list");
            Assert.That(yaml, Does.Contain(UiDefaultShaderEntry),
                "UI/Default (fileID 10770) is not in Always Included Shaders — every runtime-built " +
                "UGUI Image/Text renders magenta in the Android build (#291)");
        }

        [Test]
        public void GraphicsSettings_StillIncludes_TheSpritesDefaultShader()
        {
            // Regression guard: adding UI/Default must not drop the shader the
            // project already retained.
            var yaml = File.ReadAllText(GraphicsSettingsPath);
            Assert.That(yaml, Does.Contain(SpritesDefaultShaderEntry),
                "Sprites/Default (fileID 10753) was removed from Always Included Shaders");
        }

        // --- A real font must be bundled (not an Editor-only built-in lookup) ---

        [Test]
        public void BundledFont_ExistsAndImportsAsAFont_AtItsPinnedGuid()
        {
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath);
            Assert.That(font, Is.Not.Null,
                $"the bundled UI font is missing or unimportable at {FontAssetPath}");

            Assert.That(AssetDatabase.AssetPathToGUID(FontAssetPath), Is.EqualTo(FontGuid),
                "the bundled font's GUID changed — pin it so references stay stable (#291)");
        }

        [Test]
        public void BundledFontMeta_ShipsFontDataInTheBuild()
        {
            // includeFontData: 1 embeds the glyph data in the player; without it
            // the runtime font falls back to nothing on device (the #291 bug).
            var meta = File.ReadAllText(FontMetaPath);
            Assert.That(meta, Does.Contain("includeFontData: 1"),
                "the bundled font's importer does not embed font data — text would be invisible in the build");
            Assert.That(meta, Does.Contain("guid: " + FontGuid),
                "the bundled font's .meta GUID is not pinned");
        }

        [Test]
        public void BundledFont_LoadsViaResources_SoRuntimeCodeCanUseItInTheBuild()
        {
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var font = Resources.Load<Font>("DejaVuSans");
            Assert.That(font, Is.Not.Null,
                "the bundled font is not reachable via Resources.Load, which is how the " +
                "procedurally-built SettingsPanel binds it in the player (#291)");
        }
    }
}
