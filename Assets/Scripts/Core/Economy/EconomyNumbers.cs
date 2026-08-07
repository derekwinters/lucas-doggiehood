using System;
using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// The single central home for economy constants (#62). As of #620 each
    /// value is read from the runtime-overridable
    /// <see cref="TuningConfig.Active"/> rather than an inline literal, so the
    /// debug tuning menu (#622) can adjust them live. The shipping defaults
    /// (unchanged here — #620 is plumbing only) live on
    /// <see cref="TuningConfig"/>'s field initializers; this class stays the
    /// documented, discoverable named home for each economy value (#161).
    /// </summary>
    public static class EconomyNumbers
    {
        /// <summary>Flat payout per completed <b>free</b>-type quest
        /// (LostItem / PestControl) — no fronted item cost to reimburse.
        /// #626: paid types instead pay <see cref="PaidQuestPayout"/>.</summary>
        public static int QuestPayout => TuningConfig.Active.QuestPayout;

        /// <summary>#626: the paid-quest payout markup — a paid job reimburses
        /// its fronted item cost times this factor. Slider-tunable via
        /// <see cref="TuningConfig.Active"/>.</summary>
        public static double PaidQuestMarkup => TuningConfig.Active.PaidQuestMarkup;

        /// <summary>#626: the coin payout for completing a <b>paid</b>-type quest
        /// (BuyGift / DecorationRequest / fence) whose fronted item cost is
        /// <paramref name="cost"/>: <c>round(cost × <see cref="PaidQuestMarkup"/>)</c>.
        /// Always exceeds the cost for the shipping markup, so a paid job is
        /// never a net loss — the "getting hired" earner model (economy.md).</summary>
        public static int PaidQuestPayout(int cost)
        {
            return (int)System.Math.Round(cost * PaidQuestMarkup, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>#310/#543: how long between quest-rotation refreshes. The
        /// refresh is a boundary <em>check</em> (never a countdown/expiry —
        /// quests never expire, economy.md #28), computed against a persisted
        /// UTC timestamp so it is immune to device-timezone changes. #543 moves
        /// this to <b>hourly</b> so quests trickle in each hour rather than
        /// arriving in one 8h all-or-nothing batch; the per-hour amount is the
        /// target spread over <see cref="PacingWindowHours"/>.</summary>
        public static TimeSpan RefreshInterval => TimeSpan.FromHours(RefreshIntervalHours);

        /// <summary>The <see cref="RefreshInterval"/> span in whole hours,
        /// named separately so the number itself is a discoverable constant
        /// (#161) rather than buried inside the TimeSpan expression. #543: 1h.</summary>
        public static int RefreshIntervalHours => TuningConfig.Active.RefreshIntervalHours;

        /// <summary>#543: the window (in hours) the population-scaled active-quest
        /// target is spread over, giving the per-hour trickle rate
        /// <c>target / PacingWindowHours</c> (see
        /// <see cref="Doggiehood.Core.Quests.QuestPacingPolicy.PerHourRate"/>).
        /// A named, tunable constant per #161. #624: 4h — a target of 6
        /// trickles 1.5 quests per hour, 12 → three per hour, 5 (floor) →
        /// 1.25 per hour.</summary>
        public static int PacingWindowHours => TuningConfig.Active.PacingWindowHours;

        /// <summary>#310: divisor of the population-scaled concurrent-quest
        /// cap — roughly one active quest per this many dogs. See
        /// <see cref="Doggiehood.Core.Quests.QuestPacingPolicy.TargetActiveCount"/>.</summary>
        public static int TargetActiveDivisor => TuningConfig.Active.TargetActiveDivisor;

        /// <summary>#310: minimum aggregate active-quest target — a small
        /// neighborhood still always has a few requests to do.</summary>
        public static int TargetActiveFloor => TuningConfig.Active.TargetActiveFloor;

        /// <summary>#310: maximum aggregate active-quest target. This ceiling
        /// is the real flood-control dial for playtest tuning — drop it first
        /// if a mid-game map feels busy.</summary>
        public static int TargetActiveCeiling => TuningConfig.Active.TargetActiveCeiling;
    }
}
