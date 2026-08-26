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

        /// <summary>#675: the flat coin payout for a completed move-in — the
        /// coins a household brings with it when it fills a vacant house. Paid
        /// <b>per household, not per dog</b> (one move-in event, one payout,
        /// whatever the household's size), on the move-in state change itself
        /// rather than on the welcome pop-up, through the same
        /// <see cref="Wallet.Deposit"/> path quest and onboarding payouts use.
        /// Sits here with the other coin sources; the move-in <em>mechanism</em>
        /// (chances, household weights) stays in
        /// <see cref="Doggiehood.Core.Expansion.MoveInNumbers"/>.</summary>
        public static int MoveInReward => TuningConfig.Active.MoveInReward;

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

        /// <summary>The smallest refresh interval the game will honour, in
        /// minutes. #743 guard 2: <see cref="TuningConfig.RefreshIntervalMinutes"/>
        /// sits in a divisor and in a <see cref="TimeSpan"/>, so a zero or
        /// negative override would divide by zero or invert the trickle. The
        /// clamp lives here, at the config edge, so no downstream seam has to
        /// defend itself.</summary>
        public const int MinRefreshIntervalMinutes = 1;

        /// <summary>The smallest pacing window the game will honour, in hours.
        /// #743 guard 2, for the same divisor reason as
        /// <see cref="MinRefreshIntervalMinutes"/>.</summary>
        public const int MinPacingWindowHours = 1;

        /// <summary>How many minutes are in an hour — named rather than an
        /// inline 60 in the per-refresh rate (#161).</summary>
        public const int MinutesPerHour = 60;

        /// <summary>#310/#543/#743: how long between quest-rotation refreshes.
        /// The refresh is a boundary <em>check</em> (never a countdown/expiry —
        /// quests never expire, economy.md #28), computed against a persisted
        /// UTC timestamp so it is immune to device-timezone changes. #543 moved
        /// this off the 8h all-or-nothing batch to an hourly trickle; #743
        /// moved it again to <b>15 minutes</b>, so the same total arrives in
        /// smaller, more frequent steps. The interval is granularity only — the
        /// per-refresh amount is the target spread over
        /// <see cref="PacingWindowHours"/>, so the board still fills in exactly
        /// one pacing window.</summary>
        public static TimeSpan RefreshInterval => TimeSpan.FromMinutes(RefreshIntervalMinutes);

        /// <summary>The <see cref="RefreshInterval"/> span in whole minutes,
        /// named separately so the number itself is a discoverable constant
        /// (#161) rather than buried inside the TimeSpan expression. #743: 15
        /// minutes, clamped to at least
        /// <see cref="MinRefreshIntervalMinutes"/>.</summary>
        public static int RefreshIntervalMinutes =>
            System.Math.Max(MinRefreshIntervalMinutes, TuningConfig.Active.RefreshIntervalMinutes);

        /// <summary>#543: the window (in hours) the population-scaled active-quest
        /// target is spread over, giving the per-refresh trickle amount
        /// <c>target × RefreshIntervalMinutes / (PacingWindowHours × 60)</c> (see
        /// <see cref="Doggiehood.Core.Quests.QuestPacingPolicy.PerRefreshRate"/>).
        /// A named, tunable constant per #161. #624: 4h — a target of 6
        /// trickles 1.5 quests per hour, 12 → three per hour, 5 (floor) →
        /// 1.25 per hour, however finely the interval slices it. Clamped to at
        /// least <see cref="MinPacingWindowHours"/>.</summary>
        public static int PacingWindowHours =>
            System.Math.Max(MinPacingWindowHours, TuningConfig.Active.PacingWindowHours);

        /// <summary>#743: how many <see cref="RefreshInterval"/> boundaries fall
        /// inside one <see cref="PacingWindowHours"/> window — the number of
        /// trickle steps it takes to fill an empty board. 16 at the shipping
        /// 15-minute interval, 4 at the old hourly one; the total delivered is
        /// the same either way. At least one, since both inputs are clamped
        /// positive.</summary>
        public static int RefreshesPerPacingWindow =>
            System.Math.Max(1, PacingWindowHours * MinutesPerHour / RefreshIntervalMinutes);

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
