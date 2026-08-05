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
        // #440: the chip height matches the Settings gear (GearButtonSizePx 88),
        // and its interior is scaled by x1.375 (=88/64) so the taller pill stays
        // balanced rather than gaining dead vertical padding.
        public const float HeightPx = 88f;
        public const float CoinDiameterPx = 60f;
        public const float PaddingLeftPx = 14f;   // coin inset
        public const float PaddingRightPx = 36f;  // number inset
        public const float IconGapPx = 17f;        // coin -> number (mockup gap)
        public const int FontSizePx = 46;          // balance (tabular)

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
        // #542: the floating coin-delta label reuses the shared palette's role
        // colors — Leaf (positive/confirm) for a gain, Coral (primary/spend)
        // for a spend — no new colors introduced.
        public static readonly Color LeafColor = new Color32(0x58, 0xC0, 0x6A, 0xFF);
        public static readonly Color CoralColor = new Color32(0xFF, 0x7A, 0x5C, 0xFF);

        // --- Floating coin-delta label (#542, shared-components.md CurrencyChip) ---
        // The rise distance / rise+fade duration / count-up duration are pure
        // animation math and live on the Core CoinChipAnimation type; these two
        // are the label's render layout (font size, and the gap from the chip's
        // bottom edge to where the label starts). Named constants, no inline
        // literals (#161).
        public const int DeltaFontSizePx = 32;
        public const float DeltaOffsetYPx = 12f;

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build — never an editor-only built-in font, which
        /// renders invisible in the player. Same asset the UGUI overlays use.</summary>
        public const string LabelFontResource = "DejaVuSans";

        // Settings gear entry point, from the #218 wireframe constants
        // (settings.md: GearButtonSizePx 88, GearMarginPx 32).
        private const float GearButtonSizePx = 88f;
        private const float GearMarginPx = 32f;

        // --- Procedural gear icon (#370) ---
        // The gear was the last HUD affordance still drawn as a default IMGUI
        // button with a font glyph (⚙, U+2699); the bundled DejaVuSans font has
        // no coverage for it, so on device it fell back to an empty gray box
        // (the #291 font/shader-stripping risk). It now wears the shared Candy
        // Cottage chrome and a procedural ink toothed-disc icon — no font glyph,
        // no raster art — mirroring the coach-bar paw badge (#297). Toothed disc:
        // GearToothCount ink tooth-discs orbiting the hub at GearToothOrbitRadiusPx
        // (poking past the ink body disc), with a cream hub hole. No inline
        // geometry literals (#161).
        public const int GearToothCount = 8;
        public const float GearBodyDiameterPx = 46f;       // central ink cog disc
        public const float GearToothDiameterPx = 14f;      // each radial tooth disc
        public const float GearToothOrbitRadiusPx = 26f;   // tooth center distance from the hub
        public const float GearHubDiameterPx = 18f;        // cream hub hole

        // Procedural chrome is drawn by the shared CandyChrome helper (#297) —
        // the same white-AA-circle routine established here in #296, extracted
        // so the onboarding coach bar draws identical chrome without duplication.
        private static GUIStyle labelStyle;
        private static GUIStyle deltaLabelStyle;

        private GameState state;

        // #542: the currently-playing balance-change animation (null when the
        // chip is at rest), plus the elapsed seconds driving it. Set by the
        // wallet-change handler; ticked in Update; read by OnGUI to draw the
        // count-up value and the floating delta label.
        private Wallet subscribedWallet;
        private CoinChipAnimation animation;
        private float animElapsedSec;

        /// <summary>Raised when the HUD gear is tapped — the bootstrap wires
        /// this to open the Settings panel (#219).</summary>
        public event Action GearTapped;

        public void Init(GameState state)
        {
            if (subscribedWallet != null)
            {
                subscribedWallet.CoinsChanged -= OnCoinsChanged;
            }

            this.state = state;
            subscribedWallet = state == null ? null : state.Wallet;
            if (subscribedWallet != null)
            {
                subscribedWallet.CoinsChanged += OnCoinsChanged;
            }
        }

        private void OnDestroy()
        {
            if (subscribedWallet != null)
            {
                subscribedWallet.CoinsChanged -= OnCoinsChanged;
                subscribedWallet = null;
            }
        }

        /// <summary>The active balance-change animation, or <c>null</c> when the
        /// chip is at rest. #542 — exposed so the wiring can be asserted.</summary>
        public CoinChipAnimation CurrentDelta => animation;

        /// <summary>#542: on a wallet change, start a fresh animation counting
        /// from whatever balance is currently displayed (so a change mid-tween
        /// re-targets rather than snapping) to the new live total, and spawn the
        /// floating delta label for the signed change.</summary>
        private void OnCoinsChanged(int delta)
        {
            var newBalance = state.Wallet.Coins;
            var currentDisplayed = animation == null
                ? newBalance - delta
                : animation.DisplayedBalance(animElapsedSec);
            animation = new CoinChipAnimation(currentDisplayed, newBalance, delta);
            animElapsedSec = 0f;
        }

        private void Update()
        {
            if (animation == null)
            {
                return;
            }

            animElapsedSec += Time.unscaledDeltaTime;
            if (animation.IsFinished(animElapsedSec))
            {
                animation = null;
            }
        }

        /// <summary>The chip's current balance, straight off the live wallet
        /// (bare tabular number — the coin token supplies the meaning).</summary>
        public string Label
        {
            get { return state == null ? string.Empty : CurrencyChip.Label(state.Wallet.Coins); }
        }

        /// <summary>#542: the balance to paint on the chip at
        /// <paramref name="elapsedSec"/> into the current animation — the
        /// count-up tween value while a change is in flight, otherwise the raw
        /// live balance. This is what the chip draws, so the number counts up
        /// instead of snapping to the new total.</summary>
        public string DisplayedLabel(float elapsedSec)
        {
            if (state == null)
            {
                return string.Empty;
            }

            return animation == null
                ? CurrencyChip.Label(state.Wallet.Coins)
                : CurrencyChip.Label(animation.DisplayedBalance(elapsedSec));
        }

        /// <summary>#542: the fixed palette color for a delta role — Leaf for a
        /// gain, Coral for a spend (shared-components.md role-tint mapping).</summary>
        public static Color DeltaColor(CoinDeltaRole role)
        {
            return role == CoinDeltaRole.Gain ? LeafColor : CoralColor;
        }

        /// <summary>#542: the floating delta label's rect for a given rise
        /// offset. It starts <c>DeltaOffsetYPx</c> below the chip's bottom edge,
        /// centred horizontally under the chip, and rises (moves up — decreasing
        /// y in IMGUI's top-left origin) by <paramref name="riseOffsetPx"/> as
        /// the animation plays.</summary>
        public static Rect ComputeDeltaLabelRect(Rect chip, float riseOffsetPx)
        {
            var y = chip.yMax + DeltaOffsetYPx - riseOffsetPx;
            return new Rect(chip.x, y, chip.width, DeltaFontSizePx);
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

        /// <summary>The currency chip rect. Its right edge sits inboard-left of
        /// the gear so the gear owns the corner (decision ①), and it shares the
        /// gear's <b>vertical centreline</b> (#440) so the two read as one clean
        /// row — its <c>y</c> is derived from the gear's on-screen middle and the
        /// chip's own height rather than a separate safe-area top inset (which is
        /// superseded now that the chip matches the gear's height). IMGUI/top-left
        /// origin; <paramref name="safeArea"/> is Unity's bottom-left-origin
        /// <c>Screen.safeArea</c>.</summary>
        public static Rect ComputeChipRect(float screenWidth, float screenHeight, Rect safeArea, float chipWidth)
        {
            var gear = ComputeGearRect(screenWidth, screenHeight);
            var y = gear.center.y - HeightPx / 2f;
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

            var label = DisplayedLabel(animElapsedSec);
            var width = ComputeChipWidth(label);
            var chip = ComputeChipRect(Screen.width, Screen.height, Screen.safeArea, width);
            DrawChip(chip, label);
            DrawDelta(chip);

            var gear = ComputeGearRect(Screen.width, Screen.height);
            DrawGear(gear);
            // Device-safe tap region: a transparent hit target over the drawn
            // gear (GUIStyle.none carries no default skin, no font glyph).
            if (GUI.Button(gear, GUIContent.none, GUIStyle.none))
            {
                TapGear();
            }
        }

        /// <summary>Draws the Candy Cottage gear button, back to front: hard
        /// straight-down shadow, Ink outline disc, cream fill disc, then the
        /// procedural ink toothed-disc gear icon. All chrome is procedural
        /// (CandyChrome) — no font glyph, no external raster art.</summary>
        private void DrawGear(Rect gear)
        {
            var shadow = new Rect(gear.x, gear.y + ShadowOffsetPx, gear.width, gear.height);
            CandyChrome.DrawCircle(shadow, InkColor);
            CandyChrome.DrawCircle(gear, InkColor);

            var fill = new Rect(
                gear.x + OutlineThicknessPx,
                gear.y + OutlineThicknessPx,
                gear.width - 2f * OutlineThicknessPx,
                gear.height - 2f * OutlineThicknessPx);
            CandyChrome.DrawCircle(fill, CreamColor);

            DrawGearIcon(gear.center);
        }

        /// <summary>The procedural ink gear icon: <c>GearToothCount</c> ink
        /// tooth-discs orbiting the hub (poking past the body), a solid ink body
        /// disc fusing them into one cog, then a cream hub hole. No emoji glyph
        /// or raster art, so it renders identically on device.</summary>
        private static void DrawGearIcon(Vector2 center)
        {
            for (var i = 0; i < GearToothCount; i++)
            {
                var angle = (Mathf.PI * 2f) * i / GearToothCount;
                var tx = center.x + Mathf.Cos(angle) * GearToothOrbitRadiusPx;
                var ty = center.y + Mathf.Sin(angle) * GearToothOrbitRadiusPx;
                DrawInkDisc(tx, ty, GearToothDiameterPx);
            }

            DrawInkDisc(center.x, center.y, GearBodyDiameterPx);

            var hub = new Rect(
                center.x - GearHubDiameterPx / 2f,
                center.y - GearHubDiameterPx / 2f,
                GearHubDiameterPx,
                GearHubDiameterPx);
            CandyChrome.DrawCircle(hub, CreamColor);
        }

        private static void DrawInkDisc(float centerX, float centerY, float diameter)
        {
            CandyChrome.DrawCircle(
                new Rect(centerX - diameter / 2f, centerY - diameter / 2f, diameter, diameter),
                InkColor);
        }

        /// <summary>Draws the Candy Cottage chip chrome, back to front: hard
        /// straight-down shadow, Ink outline, cream fill inset by the outline,
        /// the gold coin token (with its ink ring), then the tabular balance.</summary>
        private void DrawChip(Rect chip, string label)
        {
            var shadow = new Rect(chip.x, chip.y + ShadowOffsetPx, chip.width, chip.height);
            CandyChrome.DrawStadium(shadow, InkColor);
            CandyChrome.DrawStadium(chip, InkColor);

            var fill = new Rect(
                chip.x + OutlineThicknessPx,
                chip.y + OutlineThicknessPx,
                chip.width - 2f * OutlineThicknessPx,
                chip.height - 2f * OutlineThicknessPx);
            CandyChrome.DrawStadium(fill, CreamColor);

            var coinX = chip.x + OutlineThicknessPx + PaddingLeftPx;
            var coinY = chip.center.y - CoinDiameterPx / 2f;
            CandyChrome.DrawCircle(new Rect(coinX, coinY, CoinDiameterPx, CoinDiameterPx), InkColor);
            var inner = CoinDiameterPx - 2f * CoinOutlineThicknessPx;
            CandyChrome.DrawCircle(new Rect(coinX + CoinOutlineThicknessPx, coinY + CoinOutlineThicknessPx, inner, inner), GoldColor);

            var numX = coinX + CoinDiameterPx + IconGapPx;
            var numRight = chip.xMax - OutlineThicknessPx - PaddingRightPx;
            var numRect = new Rect(numX, chip.y, Mathf.Max(0f, numRight - numX), chip.height);
            GUI.Label(numRect, label, LabelStyle());
        }

        /// <summary>#542: draws the transient floating delta label below the
        /// chip when a balance-change animation is playing — the signed "+N"/"−N"
        /// in its role color, rising and fading per the Core animation state.
        /// Purely decorative: no hit target, never blocks a tap.</summary>
        private void DrawDelta(Rect chip)
        {
            if (animation == null)
            {
                return;
            }

            var rect = ComputeDeltaLabelRect(chip, animation.RiseOffsetPx(animElapsedSec));
            var style = DeltaLabelStyle();
            style.normal.textColor = DeltaColor(animation.Role);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, animation.Alpha(animElapsedSec));
            GUI.Label(rect, animation.DeltaText, style);
            GUI.color = previous;
        }

        private static GUIStyle DeltaLabelStyle()
        {
            if (deltaLabelStyle == null)
            {
                deltaLabelStyle = new GUIStyle
                {
                    font = Resources.Load<Font>(LabelFontResource),
                    fontSize = DeltaFontSizePx,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }

            return deltaLabelStyle;
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
    }
}
