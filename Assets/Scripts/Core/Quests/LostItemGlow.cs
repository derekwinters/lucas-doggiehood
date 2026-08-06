namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// #521/#535: the Unity-independent tuning + logic for the lost-item
    /// "finder glow". Derek's revised design (#535) is a flat red GROUND RING
    /// only: a pool-of-light contact ring on the surface beneath the hidden
    /// lost quest item, so the item is easy to spot without changing the item
    /// itself. The item keeps its own mesh, size and colour — the earlier
    /// engulfing halo, size pulse and orbiting sparkle read in playtest as the
    /// item ballooning and turning into "a big red ball" (the lost puppy too),
    /// so they are gone. The red comes from <c>Palette.LostItemGlowHex</c>; the
    /// Unity <c>LostItemView</c> is a thin apply-seam that builds the ring from
    /// these numbers.
    ///
    /// All dimensions are named constants (#161). The ring is decoration only —
    /// non-interactive, never a tap target (tap-to-collect stays with
    /// <c>LostItemTapZone</c>) — and torn down with the item.
    /// </summary>
    public static class LostItemGlow
    {
        /// <summary>Diameter of the flat ground contact ring, relative to the
        /// item — wide enough to read as a pool of light on the surface under
        /// the item, without engulfing the item itself. This is the ring's
        /// OUTER edge.</summary>
        public const float GroundRingScale = 2.2f;

        /// <summary>#602: inner diameter of the hollow ring — the size of the
        /// hole opened in the middle so the highlight reads as a ring OUTLINE
        /// framing the object rather than a filled disc painted over it. The
        /// object and the ground inside this radius stay uncovered. Sits
        /// strictly inside <see cref="GroundRingScale"/>; the same
        /// inner/outer ratio (this ÷ <see cref="GroundRingScale"/>) drives the
        /// onboarding house highlight's hole too, so both rings stay visually
        /// consistent (#571).</summary>
        public const float GroundRingInnerScale = 1.6f;

        /// <summary>How high off the ground the flat ring floats — just enough
        /// to avoid z-fighting with the surface, not enough to read as
        /// hovering.</summary>
        public const float GroundRingHeight = 0.02f;

        /// <summary>Vertical thickness of the flat ground ring disc — kept tiny
        /// so it reads as a pool of light on the surface, not a puck.</summary>
        public const float GroundRingThickness = 0.05f;

        /// <summary>True when the finder glow should be shown for
        /// <paramref name="quest"/>: a lost-item quest whose hidden item is
        /// currently placed. Any other quest type, or a lost-item quest with no
        /// placed item, gets no glow. Null-safe.</summary>
        public static bool ShouldShow(Quest quest)
        {
            return quest != null
                && quest.Type == QuestType.LostItem
                && quest.HiddenItemPosition.HasValue;
        }
    }
}
