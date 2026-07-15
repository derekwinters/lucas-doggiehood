using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Persistent HUD (#159), starting with the currency chip in the
    /// top-right corner. Graybox IMGUI rendering until the Candy Cottage
    /// chrome pass (#65); no decision logic here — the label text comes
    /// from Core, read live from the wallet each frame (never cached).
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        private const float ChipWidth = 140f;
        private const float ChipHeight = 32f;
        private const float ChipMargin = 16f;

        private GameState state;

        public void Init(GameState state)
        {
            this.state = state;
        }

        /// <summary>The chip's current text, straight off the live wallet.</summary>
        public string Label
        {
            get { return state == null ? string.Empty : CurrencyChip.Label(state.Wallet.Coins); }
        }

        private void OnGUI()
        {
            if (state == null)
            {
                return;
            }

            GUI.Box(
                new Rect(Screen.width - ChipWidth - ChipMargin, ChipMargin, ChipWidth, ChipHeight),
                Label);
        }
    }
}
