using System;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// #669: how big a red target ring has to be to actually frame the thing it
    /// marks. Engine-free, because this is a rule about the highlight — not a
    /// rendering detail — and because every highlight that attaches a ring to a
    /// world object (the onboarding target house #571, the build-step
    /// foundation #668) has to size it the same way rather than growing its own
    /// numbers.
    ///
    /// <para><b>Invariant — a target ring's inner edge always lies outside the
    /// target's footprint, with a non-zero gap.</b> Two things make that
    /// non-obvious, and both were got wrong before #669:</para>
    /// <list type="number">
    /// <item>The ring has to clear the footprint's <b>diagonal</b>, not its
    /// longest side. The retired rule sized the ring at
    /// <c>1.15 · max(x, z)</c>, but a square house of side <c>w</c> reaches
    /// <c>1.414·w</c> corner to corner — so the ring's outer edge sat ~19%
    /// <i>inside</i> the house's own corners and the house covered it on all
    /// four diagonals by construction. No value below <c>√2</c> can ever
    /// work; sizing from the diagonal removes the trap rather than retuning
    /// past it.</item>
    /// <item>The band is a hollow annulus (#602), so the edge that has to clear
    /// the target is the <b>inner</b> one — the visible near edge — not the
    /// outer one. The gap is therefore applied against the hole, which is
    /// why the containing diameter is divided by
    /// <see cref="LostItemGlow.GroundRingInnerFraction"/>.</item>
    /// </list>
    /// </summary>
    public static class TargetRingGeometry
    {
        /// <summary>How much open ground shows between the target's footprint
        /// and the ring's inner edge, as a fraction of the footprint's
        /// corner-to-centre reach: the hole's radius is this much larger than
        /// bare containment needs, so the ring reads as framing the target
        /// rather than hugging it. Named per #161 — and deliberately expressed
        /// as a CLEARANCE on top of a guaranteed-containing size, not as a
        /// multiplier on the footprint, so no future tuning of this number can
        /// reintroduce a ring the target overlaps: containment holds for every
        /// value &gt;= 0.
        ///
        /// <para>It is bounded ABOVE, though, and that bound is tight. Clearing
        /// the footprint through a hole that is only
        /// <see cref="LostItemGlow.GroundRingInnerFraction"/> of the outer edge
        /// makes the ring roughly <c>1.94×</c> the target's diagonal, so a
        /// max-level house's ring nearly fills its lot; past ~0.07 it crosses
        /// onto the neighbouring lot and stops reading as "this house". 0.05 is
        /// the round value that keeps every lot's max-level ring on its own lot
        /// (pinned by a test) — the corner is the tightest point, while the flat
        /// faces still show metres of open ground.</para></summary>
        public const float FootprintGapFraction = 0.05f;

        /// <summary>The outer diameter a target ring must be drawn at to
        /// contain a <paramref name="footprintX"/> × <paramref name="footprintZ"/>
        /// axis-aligned footprint inside its hole, with
        /// <see cref="FootprintGapFraction"/> of open ground to spare. Feed it
        /// straight to the ring object's diameter-valued scale — the shared
        /// unit-diameter mesh (<c>GroundRingMesh</c>) is built at outer radius
        /// 0.5 for exactly that.</summary>
        public static float OuterDiameter(float footprintX, float footprintZ)
        {
            if (footprintX < 0f)
            {
                throw new ArgumentException("Footprint X must be non-negative.", nameof(footprintX));
            }

            if (footprintZ < 0f)
            {
                throw new ArgumentException("Footprint Z must be non-negative.", nameof(footprintZ));
            }

            // The footprint's corner-to-corner diagonal is the smallest circle
            // diameter that can contain it; the gap clears it, and dividing by
            // the hole ratio pushes that clearance out to the ring's INNER edge.
            var diagonal = (float)Math.Sqrt((footprintX * footprintX) + (footprintZ * footprintZ));
            return diagonal * (1f + FootprintGapFraction) / LostItemGlow.GroundRingInnerFraction;
        }

        /// <summary>The diameter of the hole in a ring drawn at
        /// <paramref name="outerDiameter"/> — the band's visible near edge, and
        /// the edge the containment invariant is stated against.</summary>
        public static float InnerDiameter(float outerDiameter)
        {
            return outerDiameter * LostItemGlow.GroundRingInnerFraction;
        }
    }
}
