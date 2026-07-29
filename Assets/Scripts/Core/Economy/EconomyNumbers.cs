using System;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// The single central home for economy constants (#62). Placeholder
    /// balance numbers — tune here (and only here) once the daily rotation
    /// can be felt in a playable build.
    /// </summary>
    public static class EconomyNumbers
    {
        /// <summary>Flat payout per completed quest, regardless of type.</summary>
        public const int QuestPayout = 10;

        /// <summary>#310: how long between quest-rotation refreshes. The
        /// refresh is a boundary <em>check</em> (never a countdown/expiry —
        /// quests never expire, economy.md #28), computed against a persisted
        /// UTC timestamp so it is immune to device-timezone changes. 8h gives
        /// a few gentle refreshes across a day without flooding a repeat
        /// opener. Tunable placeholder per #62/#161.</summary>
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(RefreshIntervalHours);

        /// <summary>The <see cref="RefreshInterval"/> span in whole hours,
        /// named separately so the number itself is a discoverable constant
        /// (#161) rather than buried inside the TimeSpan expression.</summary>
        public const int RefreshIntervalHours = 8;

        /// <summary>#310: divisor of the population-scaled concurrent-quest
        /// cap — roughly one active quest per this many dogs. See
        /// <see cref="Doggiehood.Core.Quests.QuestPacingPolicy.TargetActiveCount"/>.</summary>
        public const int TargetActiveDivisor = 3;

        /// <summary>#310: minimum aggregate active-quest target — a small
        /// neighborhood still always has a few requests to do.</summary>
        public const int TargetActiveFloor = 3;

        /// <summary>#310: maximum aggregate active-quest target. This ceiling
        /// is the real flood-control dial for playtest tuning — drop it first
        /// if a mid-game map feels busy.</summary>
        public const int TargetActiveCeiling = 12;

        /// <summary>#26/#310: fewest dogs a single refresh batch tops up in
        /// one go (before the aggregate cap and free-dog count clamp it).</summary>
        public const int RotationBatchMin = 2;

        /// <summary>#26/#310: most dogs a single refresh batch tops up in one
        /// go (before the aggregate cap and free-dog count clamp it).</summary>
        public const int RotationBatchMax = 4;
    }
}
