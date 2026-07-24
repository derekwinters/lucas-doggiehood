using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side rendering for the map-expansion lock indicator (#178):
    /// hovers at Core's <see cref="ExpansionIndicator.Resolve"/> position,
    /// tinted gold when affordable or grey/black when not, and hidden
    /// entirely once no locked zone remains to unlock. No decision logic
    /// here — Core resolves position/affordability fresh every call, this
    /// view only applies the result to a SpriteRenderer, re-reading live
    /// each frame, the same "never cache" contract HudOverlay uses for the
    /// wallet label.
    /// </summary>
    public sealed class ExpansionIndicatorView : MonoBehaviour
    {
        /// <summary>World-Y the indicator hovers at. Purely visual —
        /// "hovering" carries no Core state, just an above-ground height so
        /// the marker reads as floating rather than sitting on the road.</summary>
        public const float HoverHeight = 3f;

        private GameState state;
        private SpriteRenderer spriteRenderer;
        private Sprite affordableSprite;
        private Sprite lockedSprite;

        public void Init(GameState state, Sprite affordableSprite, Sprite lockedSprite)
        {
            this.state = state;
            this.affordableSprite = affordableSprite;
            this.lockedSprite = lockedSprite;
            spriteRenderer = GetComponent<SpriteRenderer>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        /// <summary>
        /// Re-reads <see cref="ExpansionIndicator.Resolve"/> and applies
        /// it: the renderer is disabled entirely when nothing is left to
        /// unlock, otherwise the marker is positioned and tinted per the
        /// current balance/next-cost state. Public so tests can apply it
        /// directly without waiting on a Play-mode frame.
        /// </summary>
        public void Refresh()
        {
            if (state == null)
            {
                return;
            }

            var indicator = ExpansionIndicator.Resolve(state);
            if (indicator == null)
            {
                spriteRenderer.enabled = false;
                return;
            }

            spriteRenderer.enabled = true;
            var position = indicator.Value.Position;
            transform.position = new Vector3(position.X, HoverHeight, position.Z);
            spriteRenderer.sprite = indicator.Value.IsAffordable ? affordableSprite : lockedSprite;
        }
    }
}
