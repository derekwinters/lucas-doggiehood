using System;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Persistent HUD (#159): the currency chip in the top-right, with the
    /// Settings gear (#219) owning the very corner and the chip nudged just
    /// inboard to its left (settings.md decision ①). The chip now wears the full
    /// Candy Cottage chrome (#65/#296) — a cream pill with a thick Ink outline,
    /// a hard straight-down drop-shadow, and a gold coin token beside the live
    /// tabular balance — restyling the graybox <c>GUI.Box</c> without re-laying
    /// it out (hud.md wireframe #174 keeps the top-right anchor and the shared
    /// <c>CurrencyChip</c> constants).
    ///
    /// Kept on IMGUI (the gear is already IMGUI): the chip's rounded chrome is
    /// drawn procedurally from a single runtime-generated white circle texture
    /// (tinted per layer, capped-and-stretched into a stadium), with no external
    /// raster art. No decision logic here — the balance text comes from Core,
    /// read live off the wallet each frame (never cached). Every geometry/style
    /// value is a named constant (#161), asserted by EditMode tests.
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        // --- CurrencyChip layout constants (shared-components.md #173) ---
        public const float HeightPx = 64f;
        public const float CoinDiameterPx = 44f;
        public const float PaddingLeftPx = 10f;   // coin inset
        public const float PaddingRightPx = 26f;  // number inset
        public const float IconGapPx = 12f;        // coin -> number (mockup gap)
        public const int FontSizePx = 34;          // balance (tabular)

        // --- Shared Candy Cottage baseline (shared-components.md #65) ---
        public const float OutlineThicknessPx = 6f;      // Ink outline on all chrome
        public const float ShadowOffsetPx = 8f;          // hard drop-shadow, straight down, no blur
        public const float PillRadiusPx = 999f;          // full pill (stadium) ends
        public const float CoinOutlineThicknessPx = 4f;  // coin token's ink ring (mockup .coin)

        // Content-width model: with tabular figures every glyph advances the same
        // amount, so the chip width derives deterministically from the label
        // length rather than needing headless font metrics.
        public const float DigitAdvancePx = 22f;

        // --- HUD placement (hud.md #174) ---
        public const float HudEdgeMarginPx = 36f;  // inset from the safe-area top to the chip
        public const float ChipGearGapPx = 16f;    // gap between the chip's right edge and the gear

        // --- Fixed Candy Cottage palette (shared-components.md) ---
        public static readonly Color InkColor = new Color32(0x2E, 0x2A, 0x26, 0xFF);
        public static readonly Color CreamColor = new Color32(0xFF, 0xF3, 0xD9, 0xFF);
        public static readonly Color GoldColor = new Color32(0xFF, 0xC2, 0x3C, 0xFF);

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build — never an editor-only built-in font, which
        /// renders invisible in the player. Same asset the UGUI overlays use.</summary>
        public const string LabelFontResource = "DejaVuSans";

        // Settings gear entry point, from the #218 wireframe constants (unchanged
        // graybox — its restyle is #219's job, not this chip pass).
        private const float GearButtonSizePx = 88f;
        private const float GearMarginPx = 32f;
        private const string GearGlyph = "⚙"; // gear

        // Procedural chrome: one white AA circle, tinted per layer. Used as a
        // full disc (coin) and, cap-and-stretched, as a stadium (pill).
        private const int CircleTextureSize = 128;
        private static Texture2D circleTexture;
        private static GUIStyle labelStyle;

        private GameState state;

        /// <summary>Raised when the HUD gear is tapped — the bootstrap wires
        /// this to open the Settings panel (#219).</summary>
        public event Action GearTapped;

        public void Init(GameState state)
        {
            this.state = state;
        }

        /// <summary>The chip's current balance, straight off the live wallet
        /// (bare tabular number — the coin token supplies the meaning).</summary>
        public string Label
        {
            get { return state == null ? string.Empty : CurrencyChip.Label(state.Wallet.Coins); }
        }

        /// <summary>The Settings gear rect: the top-right corner of the HUD,
        /// inset by <c>GearMarginPx</c> from the raw screen edge (wireframe
        /// decision ① — gear furthest right; kept as-is by #296).</summary>
        public static Rect ComputeGearRect(float screenWidth, float screenHeight)
        {
            return new Rect(
                screenWidth - GearButtonSizePx - GearMarginPx,
                GearMarginPx,
                GearButtonSizePx,
                GearButtonSizePx);
        }

        /// <summary>The chip's total width, derived from its regions (never a
        /// magic number): outline both sides + coin inset + coin + gap + the
        /// tabular number + number inset.</summary>
        public static float ComputeChipWidth(string label)
        {
            var digits = label == null ? 0 : label.Length;
            return 2f * OutlineThicknessPx + PaddingLeftPx + CoinDiameterPx + IconGapPx
                + digits * DigitAdvancePx + PaddingRightPx;
        }

        /// <summary>The currency chip rect. Its top is inset from the
        /// <b>safe-area</b> top by <c>HudEdgeMarginPx</c> (hud.md #174), and its
        /// right edge sits inboard-left of the gear so the gear owns the corner
        /// (decision ①). IMGUI/top-left origin; <paramref name="safeArea"/> is
        /// Unity's bottom-left-origin <c>Screen.safeArea</c>.</summary>
        public static Rect ComputeChipRect(float screenWidth, float screenHeight, Rect safeArea, float chipWidth)
        {
            var gear = ComputeGearRect(screenWidth, screenHeight);
            var topInset = screenHeight - safeArea.yMax;
            var y = topInset + HudEdgeMarginPx;
            var rightEdge = gear.xMin - ChipGearGapPx;
            var x = rightEdge - chipWidth;
            return new Rect(x, y, chipWidth, HeightPx);
        }

        /// <summary>Raises <see cref="GearTapped"/>; the IMGUI gear button
        /// calls this, and tests drive it directly.</summary>
        public void TapGear()
        {
            GearTapped?.Invoke();
        }

        private void OnGUI()
        {
            if (state == null)
            {
                return;
            }

            var label = Label;
            var width = ComputeChipWidth(label);
            var chip = ComputeChipRect(Screen.width, Screen.height, Screen.safeArea, width);
            DrawChip(chip, label);

            if (GUI.Button(ComputeGearRect(Screen.width, Screen.height), GearGlyph))
            {
                TapGear();
            }
        }

        /// <summary>Draws the Candy Cottage chip chrome, back to front: hard
        /// straight-down shadow, Ink outline, cream fill inset by the outline,
        /// the gold coin token (with its ink ring), then the tabular balance.</summary>
        private void DrawChip(Rect chip, string label)
        {
            var shadow = new Rect(chip.x, chip.y + ShadowOffsetPx, chip.width, chip.height);
            DrawStadium(shadow, InkColor);
            DrawStadium(chip, InkColor);

            var fill = new Rect(
                chip.x + OutlineThicknessPx,
                chip.y + OutlineThicknessPx,
                chip.width - 2f * OutlineThicknessPx,
                chip.height - 2f * OutlineThicknessPx);
            DrawStadium(fill, CreamColor);

            var coinX = chip.x + OutlineThicknessPx + PaddingLeftPx;
            var coinY = chip.center.y - CoinDiameterPx / 2f;
            DrawCircle(new Rect(coinX, coinY, CoinDiameterPx, CoinDiameterPx), InkColor);
            var inner = CoinDiameterPx - 2f * CoinOutlineThicknessPx;
            DrawCircle(new Rect(coinX + CoinOutlineThicknessPx, coinY + CoinOutlineThicknessPx, inner, inner), GoldColor);

            var numX = coinX + CoinDiameterPx + IconGapPx;
            var numRight = chip.xMax - OutlineThicknessPx - PaddingRightPx;
            var numRect = new Rect(numX, chip.y, Mathf.Max(0f, numRight - numX), chip.height);
            GUI.Label(numRect, label, LabelStyle());
        }

        private static void DrawStadium(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = color;

            var circle = CircleTexture();
            var diameter = rect.height;
            var halfH = rect.height / 2f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, diameter, diameter), circle);
            GUI.DrawTexture(new Rect(rect.xMax - diameter, rect.y, diameter, diameter), circle);

            var midWidth = rect.width - diameter;
            if (midWidth > 0f)
            {
                GUI.DrawTexture(new Rect(rect.x + halfH, rect.y, midWidth, rect.height), Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        private static void DrawCircle(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, CircleTexture());
            GUI.color = previous;
        }

        private static GUIStyle LabelStyle()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle
                {
                    font = Resources.Load<Font>(LabelFontResource),
                    fontSize = FontSizePx,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
                labelStyle.normal.textColor = InkColor;
            }

            return labelStyle;
        }

        /// <summary>Builds (once) the white anti-aliased circle used to compose
        /// all chip chrome — a full disc for the coin, cap-and-stretched into a
        /// stadium for the pill. Procedural; no external art asset.</summary>
        private static Texture2D CircleTexture()
        {
            if (circleTexture != null)
            {
                return circleTexture;
            }

            var size = CircleTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var coverage = Mathf.Clamp01(radius - distance + 0.5f);
                    var alpha = (byte)Mathf.RoundToInt(coverage * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            circleTexture = texture;
            return circleTexture;
        }
    }
}
