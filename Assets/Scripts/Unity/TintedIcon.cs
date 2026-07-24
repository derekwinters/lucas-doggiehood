using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Recolors a monochrome, alpha-masked icon (#178): the staged lock
    /// icon (#183, Assets/Art/UI/ExpansionIndicator/Resources/locked.png)
    /// is stored as a literal black silhouette — RGB (0,0,0), with its
    /// shape carried entirely in the alpha channel. A standard multiply
    /// tint (SpriteRenderer.color, UI Image.color) can never recolor
    /// that — black times any tint stays black — so this builds a new
    /// texture with the same alpha but every pixel's RGB replaced outright
    /// with the requested tint. The source texture's import settings must
    /// mark it Read/Write Enabled (isReadable) for GetPixels32 to succeed.
    /// </summary>
    public static class TintedIcon
    {
        /// <summary>Pixels-per-unit for the generated sprite — matches the
        /// staged icon's own import setting (spritePixelsToUnits: 100 in
        /// locked.png.meta) so its world size is driven purely by the
        /// marker's transform scale, not a mismatched default.</summary>
        public const float SpritePixelsPerUnit = 100f;

        public static Sprite Recolor(Texture2D source, Color tint)
        {
            var tint32 = (Color32)tint;
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(tint32.r, tint32.g, tint32.b, pixels[i].a);
            }

            var recolored = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            recolored.SetPixels32(pixels);
            recolored.Apply();

            return Sprite.Create(
                recolored,
                new Rect(0f, 0f, recolored.width, recolored.height),
                new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit);
        }
    }
}
