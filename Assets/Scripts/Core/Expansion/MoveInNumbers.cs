using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for move-in-system constants (#54). Decided
    /// 2026-07-14 (Derek, in conversation; docs/specs/expansion.md#move-in-system).
    /// As of #620 each value reads from the runtime-overridable
    /// <see cref="TuningConfig.Active"/> so the debug tuning menu (#622) can
    /// adjust them live; the shipping defaults live on
    /// <see cref="TuningConfig"/>.
    /// </summary>
    public static class MoveInNumbers
    {
        /// <summary>Late (settled, large-neighborhood) base move-in chance
        /// rolled on the very next completed quest after a success. #625: the
        /// low end of the population-scaled curve — the effective base rises
        /// toward <see cref="EarlyMoveInChance"/> for small neighborhoods
        /// (see <see cref="ScaledBaseMoveInChance"/>).</summary>
        public static double BaseMoveInChance => TuningConfig.Active.BaseMoveInChance;

        /// <summary>Late (settled) per-quest increment added to the chance for
        /// every completed quest that didn't produce a move-in; reset to base
        /// on a success. #625: the low end of the population-scaled curve
        /// (see <see cref="ScaledMoveInIncrementPerQuest"/>).</summary>
        public static double MoveInChanceIncrementPerQuest => TuningConfig.Active.MoveInChanceIncrementPerQuest;

        /// <summary>#625: early (small-neighborhood) base move-in chance — the
        /// high end of the population-scaled curve.</summary>
        public static double EarlyMoveInChance => TuningConfig.Active.EarlyMoveInChance;

        /// <summary>#625: early (small-neighborhood) per-quest increment — the
        /// high end of the population-scaled curve.</summary>
        public static double EarlyMoveInChanceIncrementPerQuest => TuningConfig.Active.EarlyMoveInChanceIncrementPerQuest;

        /// <summary>#625: at/below this dog count the effective rate is the
        /// early rate (the scaling span's small end).</summary>
        public static int MoveInEarlyPopulation => TuningConfig.Active.MoveInEarlyPopulation;

        /// <summary>#625: at/above this dog count the effective rate settles to
        /// the late rate (the scaling span's large end).</summary>
        public static int MoveInLatePopulation => TuningConfig.Active.MoveInLatePopulation;

        /// <summary>#625: the effective base move-in chance for a neighborhood
        /// of <paramref name="dogCount"/> dogs — the early rate at/below
        /// <see cref="MoveInEarlyPopulation"/>, the late rate at/above
        /// <see cref="MoveInLatePopulation"/>, and a linear interpolation
        /// between, clamped outside the span.</summary>
        public static double ScaledBaseMoveInChance(int dogCount)
        {
            return InterpolateByPopulation(dogCount, EarlyMoveInChance, BaseMoveInChance);
        }

        /// <summary>#625: the effective per-quest increment for a neighborhood
        /// of <paramref name="dogCount"/> dogs, scaled the same way as
        /// <see cref="ScaledBaseMoveInChance"/>.</summary>
        public static double ScaledMoveInIncrementPerQuest(int dogCount)
        {
            return InterpolateByPopulation(dogCount, EarlyMoveInChanceIncrementPerQuest, MoveInChanceIncrementPerQuest);
        }

        private static double InterpolateByPopulation(int dogCount, double earlyValue, double lateValue)
        {
            var early = MoveInEarlyPopulation;
            var late = MoveInLatePopulation;

            if (dogCount <= early)
            {
                return earlyValue;
            }

            if (dogCount >= late)
            {
                return lateValue;
            }

            var t = (dogCount - early) / (double)(late - early);
            return earlyValue + t * (lateValue - earlyValue);
        }

        /// <summary>Relative weight out of 100 for a single-dog household.</summary>
        public static int SingleWeight => TuningConfig.Active.MoveInSingleWeight;

        /// <summary>Relative weight out of 100 for a parent+puppy household
        /// (the pair shares one breed).</summary>
        public static int ParentAndPuppyWeight => TuningConfig.Active.MoveInParentAndPuppyWeight;

        /// <summary>Relative weight out of 100 for a three-dog household
        /// (each dog gets its own independently-rolled breed).</summary>
        public static int ThreeDogWeight => TuningConfig.Active.MoveInThreeDogWeight;

        /// <summary>Chance a household head is drawn from the easter-egg
        /// reserve instead of the general name/breed pools.</summary>
        public static double EasterEggChance => TuningConfig.Active.EasterEggChance;

        /// <summary>Smoothing term in the inverse-count breed weight
        /// (1 / (currentCount + Smoothing)) so a breed with zero current
        /// dogs still gets a finite, positive weight.</summary>
        public static double BreedWeightSmoothing => TuningConfig.Active.BreedWeightSmoothing;
    }
}
