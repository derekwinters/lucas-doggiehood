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

        /// <summary>#310/#543: how long between quest-rotation refreshes. The
        /// refresh is a boundary <em>check</em> (never a countdown/expiry —
        /// quests never expire, economy.md #28), computed against a persisted
        /// UTC timestamp so it is immune to device-timezone changes. #543 moves
        /// this to <b>hourly</b> so quests trickle in each hour rather than
        /// arriving in one 8h all-or-nothing batch; the per-hour amount is the
        /// target spread over <see cref="PacingWindowHours"/>. Tunable
        /// placeholder per #62/#161.</summary>
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(RefreshIntervalHours);

        /// <summary>The <see cref="RefreshInterval"/> span in whole hours,
        /// named separately so the number itself is a discoverable constant
        /// (#161) rather than buried inside the TimeSpan expression. #543: 1h.</summary>
        public const int RefreshIntervalHours = 1;

        /// <summary>#543: the window (in hours) the population-scaled active-quest
        /// target is spread over, giving the per-hour trickle rate
        /// <c>target / PacingWindowHours</c> (see
        /// <see cref="Doggiehood.Core.Quests.QuestPacingPolicy.PerHourRate"/>).
        /// A named, tunable constant per #161: at 6h a target of 6 trickles one
        /// quest per hour, 12 → two per hour, 3 → one every other hour.</summary>
        public const int PacingWindowHours = 6;

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
    }
}
