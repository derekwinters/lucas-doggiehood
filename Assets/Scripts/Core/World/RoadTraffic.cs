using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// #600: the along-road membership rule that picks a follower's immediate
    /// leader from the set of active delivery trucks. Following is single-file per
    /// road segment AND per travel direction: a truck is constrained only by the
    /// nearest truck ahead of it on its own segment driving the same way; trucks
    /// on other segments, or oncoming, never constrain it. This is the "owns the
    /// set" seam the Unity tick loop feeds each tick — it holds no state and no
    /// engine types, so all of the decision logic stays in Core.
    /// </summary>
    public static class RoadTraffic
    {
        private const float AheadEpsilon = 0.0001f;

        /// <summary>
        /// Among <paramref name="others"/> sharing the follower's
        /// <paramref name="segment"/> and travel direction, returns the
        /// along-coordinate of the nearest one strictly ahead of
        /// <paramref name="along"/> in the travel direction (the immediate
        /// leader), or null when the follower leads or is alone on its segment.
        /// The follower's own entry may appear in <paramref name="others"/>: it
        /// sits at <paramref name="along"/> (zero distance ahead) and so is never
        /// selected.
        /// </summary>
        public static float? ImmediateLeaderAlong(
            object segment, float travelSign, float along,
            IEnumerable<(object Segment, float TravelSign, float Along)> others)
        {
            var sign = travelSign < 0f ? -1f : 1f;
            float? best = null;
            var bestDistance = float.MaxValue;

            foreach (var other in others)
            {
                if (!Equals(other.Segment, segment))
                {
                    continue;
                }

                var otherSign = other.TravelSign < 0f ? -1f : 1f;
                if (otherSign != sign)
                {
                    continue;
                }

                var distanceAhead = (other.Along - along) * sign;
                if (distanceAhead <= AheadEpsilon)
                {
                    continue;
                }

                if (distanceAhead < bestDistance)
                {
                    bestDistance = distanceAhead;
                    best = other.Along;
                }
            }

            return best;
        }
    }
}
