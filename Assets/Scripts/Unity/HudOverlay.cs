using System;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Persistent HUD (#159), with the currency chip in the top-right and the
    /// Settings gear (#219) in the very top-right corner — the gear takes the
    /// corner and the chip sits just inboard to its left, per the approved
    /// settings wireframe (docs/specs/ui/settings.md, decision ①). The full
    /// HUD reconcile (chip styling/exact placement) is the HUD wireframe's job
    /// (#174); this adds only the minimal working gear affordance. Graybox
    /// IMGUI rendering until the Candy Cottage chrome pass (#65); no decision
    /// logic here — the label text comes from Core, read live from the wallet
    /// each frame (never cached).
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        private const float ChipWidth = 140f;
        private const float ChipHeight = 32f;
        private const float ChipMargin = 16f;

        // Settings gear entry point, from the #218 wireframe constants.
        private const float GearButtonSizePx = 88f;
        private const float GearMarginPx = 32f;
        private const string GearGlyph = "⚙"; // gear

        private GameState state;

        /// <summary>Raised when the HUD gear is tapped — the bootstrap wires
        /// this to open the Settings panel (#219).</summary>
        public event Action GearTapped;

        public void Init(GameState state)
        {
            this.state = state;
        }

        /// <summary>The chip's current text, straight off the live wallet.</summary>
        public string Label
        {
            get { return state == null ? string.Empty : CurrencyChip.Label(state.Wallet.Coins); }
        }

        /// <summary>The Settings gear rect: the top-right corner of the HUD,
        /// inset by <c>GearMarginPx</c> (wireframe decision ① — gear furthest
        /// right).</summary>
        public static Rect ComputeGearRect(float screenWidth, float screenHeight)
        {
            return new Rect(
                screenWidth - GearButtonSizePx - GearMarginPx,
                GearMarginPx,
                GearButtonSizePx,
                GearButtonSizePx);
        }

        /// <summary>The currency chip rect: moved just inboard of the gear, to
        /// its left (wireframe decision ①), so the gear owns the corner.</summary>
        public static Rect ComputeChipRect(float screenWidth, float screenHeight)
        {
            var gear = ComputeGearRect(screenWidth, screenHeight);
            return new Rect(gear.xMin - ChipMargin - ChipWidth, ChipMargin, ChipWidth, ChipHeight);
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

            GUI.Box(ComputeChipRect(Screen.width, Screen.height), Label);

            if (GUI.Button(ComputeGearRect(Screen.width, Screen.height), GearGlyph))
            {
                TapGear();
            }
        }
    }
}
