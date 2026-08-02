using System;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// #521: the Unity-independent tuning + logic for the lost-item "finder
    /// glow" — a soft pulsing RED halo, a ground contact ring and a subtle
    /// orbiting sparkle attached to the hidden lost quest item so it pops on
    /// any surface (a white ball vanishes on the white sidewalk `#EFE8D8`, a
    /// tennis ball would vanish in the grass `#7ED957`). It's the glow the eye
    /// catches, not the item's own colour. The red comes from
    /// <c>Palette.LostItemGlowHex</c>; the Unity <c>LostItemView</c> is a thin
    /// apply-seam that builds child meshes from these numbers and drives them
    /// with <see cref="PulseScaleAt"/> / <see cref="SparkleAngleAt"/>.
    ///
    /// All dimensions/timings are named constants (#161). The glow is
    /// decoration only — non-interactive, never a tap target (tap-to-collect
    /// stays with <c>LostItemTapZone</c>).
    /// </summary>
    public static class LostItemGlow
    {
        /// <summary>Diameter of the soft halo relative to the item, before the
        /// pulse multiplier is applied. Larger than the item so the glow, not
        /// the item, is what draws the eye.</summary>
        public const float HaloScale = 1.8f;

        /// <summary>Height above the item's own centre the halo sits at, so it
        /// hugs the item rather than the ground.</summary>
        public const float HaloHeight = 0.35f;

        /// <summary>One full breathe (min -> max -> min) of the pulse, in
        /// seconds — a calm, kid-friendly pulse rather than a strobe.</summary>
        public const float PulsePeriodSeconds = 1.6f;

        /// <summary>Halo scale multiplier at the trough of the pulse.</summary>
        public const float PulseScaleMin = 0.85f;

        /// <summary>Halo scale multiplier at the crest of the pulse.</summary>
        public const float PulseScaleMax = 1.25f;

        /// <summary>Diameter of the flat ground contact ring, relative to the
        /// item — a little wider than the halo so it reads as a pool of light
        /// on the surface under the item.</summary>
        public const float GroundRingScale = 2.2f;

        /// <summary>How high off the ground the flat ring floats — just enough
        /// to avoid z-fighting with the surface, not enough to read as
        /// hovering.</summary>
        public const float GroundRingHeight = 0.02f;

        /// <summary>Vertical thickness of the flat ground ring disc — kept tiny
        /// so it reads as a pool of light on the surface, not a puck.</summary>
        public const float GroundRingThickness = 0.05f;

        /// <summary>Diameter of the small orbiting sparkle.</summary>
        public const float SparkleScale = 0.25f;

        /// <summary>How far out from the item centre the sparkle orbits.</summary>
        public const float SparkleOrbitRadius = 0.6f;

        /// <summary>Height above the item centre the sparkle orbits at.</summary>
        public const float SparkleHeight = 0.6f;

        /// <summary>Angular speed of the orbiting sparkle.</summary>
        public const float SparkleOrbitDegreesPerSecond = 120f;

        private const float FullTurnDegrees = 360f;
        private const float HalfPulse = 0.5f;

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

        /// <summary>The halo scale multiplier at <paramref name="elapsedSeconds"/>,
        /// a smooth cosine breathe between <see cref="PulseScaleMin"/> (at t=0
        /// and every whole period) and <see cref="PulseScaleMax"/> (at each
        /// half-period). Deterministic and Unity-free so it's testable in Core
        /// and drivable frame-by-frame by the view.</summary>
        public static float PulseScaleAt(float elapsedSeconds)
        {
            var phase = Mod(elapsedSeconds, PulsePeriodSeconds) / PulsePeriodSeconds;
            // 0 at phase 0/1, 1 at phase 0.5 — a raised cosine.
            var t = HalfPulse * (1.0 - Math.Cos(2.0 * Math.PI * phase));
            return (float)(PulseScaleMin + (PulseScaleMax - PulseScaleMin) * t);
        }

        /// <summary>The sparkle's orbit angle (degrees) at
        /// <paramref name="elapsedSeconds"/>, wrapped to [0, 360).</summary>
        public static float SparkleAngleAt(float elapsedSeconds)
        {
            return Mod(elapsedSeconds * SparkleOrbitDegreesPerSecond, FullTurnDegrees);
        }

        private static float Mod(float value, float modulus)
        {
            var r = value % modulus;
            return r < 0f ? r + modulus : r;
        }
    }
}
