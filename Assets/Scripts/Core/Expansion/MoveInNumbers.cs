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
        /// <summary>Move-in chance rolled on the very next completed quest
        /// after a success (or at neighborhood start).</summary>
        public static double BaseMoveInChance => TuningConfig.Active.BaseMoveInChance;

        /// <summary>Added to the chance for every completed quest that
        /// didn't produce a move-in; reset to zero on a success.</summary>
        public static double MoveInChanceIncrementPerQuest => TuningConfig.Active.MoveInChanceIncrementPerQuest;

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
