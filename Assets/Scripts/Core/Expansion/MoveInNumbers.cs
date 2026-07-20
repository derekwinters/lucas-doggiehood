namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The single central home for move-in-system constants (#54). Decided
    /// 2026-07-14 (Derek, in conversation; docs/specs/expansion.md#move-in-system).
    /// Expect adjustment during playtesting — tune here (and only here).
    /// </summary>
    public static class MoveInNumbers
    {
        /// <summary>Move-in chance rolled on the very next completed quest
        /// after a success (or at neighborhood start).</summary>
        public const double BaseMoveInChance = 0.05;

        /// <summary>Added to the chance for every completed quest that
        /// didn't produce a move-in; reset to zero on a success.</summary>
        public const double MoveInChanceIncrementPerQuest = 0.05;

        /// <summary>Relative weight out of 100 for a single-dog household.</summary>
        public const int SingleWeight = 70;

        /// <summary>Relative weight out of 100 for a parent+puppy household
        /// (the pair shares one breed).</summary>
        public const int ParentAndPuppyWeight = 25;

        /// <summary>Relative weight out of 100 for a three-dog household
        /// (each dog gets its own independently-rolled breed).</summary>
        public const int ThreeDogWeight = 5;

        /// <summary>Chance a household head is drawn from the easter-egg
        /// reserve instead of the general name/breed pools.</summary>
        public const double EasterEggChance = 0.05;

        /// <summary>Smoothing term in the inverse-count breed weight
        /// (1 / (currentCount + Smoothing)) so a breed with zero current
        /// dogs still gets a finite, positive weight.</summary>
        public const double BreedWeightSmoothing = 1.0;
    }
}
