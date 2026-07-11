using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Produces wander movement targets along the street network (#8). Dogs
    /// walk the two street strips, may switch streets when crossing the
    /// intersection, and turn according to their MovementProfile (#89).
    /// Deterministic for a seed. Positions never leave the streets.
    /// </summary>
    public sealed class WanderBehavior
    {
        /// <summary>How far streets extend from the intersection.</summary>
        public const float StreetExtent = 26f;

        private const float StepLength = 3f;

        private readonly Random random;
        private readonly MovementProfile profile;

        // Direction of travel: axis (true = along Z on the NS street,
        // false = along X on the EW street) and sign.
        private bool alongNorthSouth = true;
        private float direction = 1f;

        public WanderBehavior(int seed, MovementProfile profile)
        {
            random = new Random(seed);
            this.profile = profile;
        }

        public GridPoint NextTarget(GridPoint current)
        {
            var atIntersection = Math.Abs(current.X) <= NeighborhoodLayout.StreetWidth / 2f
                && Math.Abs(current.Z) <= NeighborhoodLayout.StreetWidth / 2f;

            if (random.NextDouble() < profile.TurnProbability)
            {
                Turn(atIntersection);
            }

            // Bounce at the end of a street before stepping past it.
            var candidate = (alongNorthSouth ? current.Z : current.X) + direction * StepLength;
            if (Math.Abs(candidate) > StreetExtent)
            {
                direction = -direction;
            }

            return Step(current);
        }

        private void Turn(bool atIntersection)
        {
            if (atIntersection && random.NextDouble() < 0.5)
            {
                // Switch onto the crossing street.
                alongNorthSouth = !alongNorthSouth;
                direction = random.NextDouble() < 0.5 ? 1f : -1f;
            }
            else
            {
                direction = -direction;
            }
        }

        private GridPoint Step(GridPoint current)
        {
            // Keep the cross-axis coordinate pinned to the street centerline
            // so positions always stay inside the street strip.
            return alongNorthSouth
                ? new GridPoint(0f, current.Z + direction * StepLength)
                : new GridPoint(current.X + direction * StepLength, 0f);
        }
    }
}
