using System.Globalization;
using System.Text.RegularExpressions;
using Doggiehood.Core.Art;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Serialization-level guard (#559) that the Main scene's game camera is
    /// authored with the same void-backstop clear settings that
    /// <see cref="CameraRig.ApplyConfiguration"/> applies at runtime (#558):
    /// a SolidColor clear painted the grass green <see cref="Palette.GrassHex"/>.
    ///
    /// The scene originally shipped the Unity default <c>m_ClearFlags: 1</c>
    /// (Skybox) with a blue background colour and no skybox material, so before
    /// <c>CameraRig.Awake</c> ran — and on any build where that runtime override
    /// didn't reach the pixels — the flat blue clear colour showed through below
    /// the grass mesh's near edge as the "blue boundary at the bottom" seam.
    /// Aligning the serialized value removes that latent blue at its source.
    ///
    /// Asserted against the <see cref="Palette.GrassHex"/> constant (not a raw
    /// literal) so the scene can never silently drift from the runtime intent.
    /// Asserted on the YAML text rather than a loaded Camera reference, matching
    /// the order-independent pattern <see cref="AppIconTests"/> uses (see
    /// docs/engineering/unity-serialization.md trap #4).
    /// </summary>
    public class MainSceneCameraTests
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const float ChannelTolerance = 0.001f;

        // Camera is Unity class id 20; capture that object's body up to the
        // next YAML document so the assertions target the game camera alone.
        private static readonly Regex CameraBlock = new Regex(
            @"--- !u!20 &\d+\s*\nCamera:\n((?:.*\n)*?)(?=--- !u!)",
            RegexOptions.None);

        private static readonly Regex BackgroundColor = new Regex(
            @"m_BackGroundColor:\s*\{r:\s*(?<r>[-0-9.eE]+),\s*g:\s*(?<g>[-0-9.eE]+),\s*b:\s*(?<b>[-0-9.eE]+),\s*a:\s*(?<a>[-0-9.eE]+)\}");

        private static string CameraBody()
        {
            var yaml = System.IO.File.ReadAllText(MainScenePath);
            var match = CameraBlock.Match(yaml);
            Assert.That(match.Success, Is.True,
                $"no Camera (!u!20) block found in {MainScenePath}");
            return match.Groups[1].Value;
        }

        [Test]
        public void MainCamera_ClearsWithSolidColor_NotSkybox()
        {
            // CameraClearFlags: Skybox = 1, SolidColor = 2 (verified against the
            // real scene YAML). Pin to the enum value so a bad edit can't ship a
            // Skybox/blue seam silently.
            Assert.That(CameraBody(),
                Does.Contain($"m_ClearFlags: {(int)CameraClearFlags.SolidColor}"),
                "the Main scene camera does not clear with SolidColor — the blue Skybox fallback would show through below the grass mesh at max zoom-out");
        }

        [Test]
        public void MainCamera_BackgroundColor_IsTheGrassVoidBackstop()
        {
            var expected = CoreColors.FromHex(Palette.GrassHex);
            var match = BackgroundColor.Match(CameraBody());
            Assert.That(match.Success, Is.True,
                "no m_BackGroundColor found in the Main scene camera block");

            var actual = new Color(
                float.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture));

            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ChannelTolerance),
                "the Main scene camera's serialized background is not the grass void backstop (red channel)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ChannelTolerance),
                "the Main scene camera's serialized background is not the grass void backstop (green channel)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ChannelTolerance),
                "the Main scene camera's serialized background is not the grass void backstop (blue channel)");
        }
    }
}
