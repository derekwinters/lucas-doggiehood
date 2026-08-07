using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #615: the onboarding gesture-arrow coach now renders the Kenney "Game
    /// Icons" <c>arrowRight</c> sprite (tinted gold, rotated to the four
    /// directions) instead of a procedurally-drawn chevron. These guard the
    /// imported asset and the runtime tint loader at the serialization level,
    /// per docs/engineering/unity-serialization.md — the same shape as the
    /// #178 lock-icon precedent (a readable Default Texture2D loaded via
    /// <see cref="Resources"/> and recolored by <see cref="TintedIcon"/>).
    /// </summary>
    public class GestureArrowSpriteTests
    {
        private const string ArrowAssetPath =
            "Assets/Art/UI/Onboarding/GestureArrow/Resources/arrowRight.png";
        private const string ArrowGuid = "840118b2a316449da6c55614a6c0adcc";

        [Test]
        public void ArrowTexture_ExistsAtItsPinnedPathAndGuid_AndIsReadable()
        {
            AssetDatabase.ImportAsset(ArrowAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArrowAssetPath);
            Assert.That(texture, Is.Not.Null,
                $"the gesture arrow texture is missing or unimportable at {ArrowAssetPath}");

            Assert.That(AssetDatabase.AssetPathToGUID(ArrowAssetPath), Is.EqualTo(ArrowGuid),
                "the arrow's GUID drifted from its pinned value");

            // TintedIcon.Recolor calls GetPixels32, which requires the source
            // texture be Read/Write enabled — assert it at the .meta level so a
            // bad re-import that drops isReadable can't ship a black arrow.
            var metaYaml = System.IO.File.ReadAllText(ArrowAssetPath + ".meta");
            Assert.That(metaYaml, Does.Match(@"isReadable: 1\b"),
                "the arrow texture must be Read/Write enabled for TintedIcon.Recolor to read its pixels");
        }

        [Test]
        public void TryLoadGestureArrowSprite_ResolvesTheTexture_TintedToTheGestureGoldFill()
        {
            AssetDatabase.ImportAsset(ArrowAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var loaded = OnboardingOverlay.TryLoadGestureArrowSprite(out var arrowSprite);

            Assert.That(loaded, Is.True, "the gesture arrow sprite must resolve from Resources");
            Assert.That(arrowSprite, Is.Not.Null);

            // TintedIcon.Recolor replaces every opaque pixel's RGB with the tint
            // (keeping the source alpha as the arrow's shape). Every opaque pixel
            // should therefore carry the gold GestureFillColor.
            var expected = (Color32)OnboardingOverlay.GestureFillColor;
            var pixels = arrowSprite.texture.GetPixels32();
            var opaqueFound = false;
            foreach (var pixel in pixels)
            {
                if (pixel.a == 0)
                {
                    continue;
                }

                opaqueFound = true;
                Assert.That(pixel.r, Is.EqualTo(expected.r), "opaque pixel red is the gold tint");
                Assert.That(pixel.g, Is.EqualTo(expected.g), "opaque pixel green is the gold tint");
                Assert.That(pixel.b, Is.EqualTo(expected.b), "opaque pixel blue is the gold tint");
            }

            Assert.That(opaqueFound, Is.True, "the arrow sprite has an opaque shape to tint");
        }
    }
}
