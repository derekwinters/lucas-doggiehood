namespace Doggiehood.Core.Interaction
{
    /// <summary>
    /// #670 (R3): the strict priority order the <see cref="InputAuthority"/>
    /// resolves a gesture against at press-down. Lower value = higher priority
    /// = closer to the player's finger. The topmost interested tier wins and
    /// <em>consumes</em> the gesture; nothing below it sees that gesture at all.
    /// </summary>
    public enum InputTier
    {
        /// <summary>An open dialog/overlay and its scrim. Blocks every tier
        /// below it — including camera pan/pinch/twist/scroll, which before
        /// #670 bypassed blocking entirely. Membership of this tier is the
        /// shared <see cref="Cameras.ModalInputGate"/>, not a registration.</summary>
        ModalUi = 0,

        /// <summary>HUD, the IMGUI gear, tuning-menu chrome — UI that is live
        /// but does not black out the world beneath it.</summary>
        NonModalUi = 1,

        /// <summary>Houses, dogs, lost items, bug swarms, expansion locks.</summary>
        World = 2,

        /// <summary>Pan/zoom/rotate. The fallback, offered a gesture only once
        /// everything above has declined it.</summary>
        Camera = 3,
    }
}
