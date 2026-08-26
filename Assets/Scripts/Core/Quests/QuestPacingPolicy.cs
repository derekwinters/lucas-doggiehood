using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// #310: the single dedicated seam that owns both quest-pacing decisions,
    /// so <see cref="QuestManager"/> asks the policy instead of reading raw
    /// constants inline — and future scaling rules (by dog count, by a
    /// progression/"hidden level", by quest-type mix) change here only, never
    /// in the quest engine or the Unity layer.
    ///
    /// <para><b>Cadence</b> (<see cref="ShouldRefresh"/>): every
    /// <see cref="EconomyNumbers.RefreshInterval"/> (hourly, #543), measured
    /// against a persisted UTC timestamp — since #704 the instant the board
    /// dropped below <see cref="TargetActiveCount"/>
    /// (<see cref="GameState.QuestRefreshTimerStartedUtc"/>), so the hour is
    /// spent waiting for a slot the player actually opened rather than ticking
    /// against a full board. Each hour a fractional amount
    /// (<see cref="PerRefreshRate"/>, driven by <see cref="AdvanceAccumulator(double, GameState, out double)"/>)
    /// trickles in, replacing the old 8h all-or-nothing batch. This is a
    /// boundary <em>check</em>, not a
    /// timer/countdown/expiry — nothing is ever removed and no quest can fail,
    /// so it stays inside the "no timer or fail condition anywhere" constraint
    /// (economy.md #28). UTC-only, so a device-timezone change can neither
    /// double-fire nor stall it.</para>
    ///
    /// <para><b>Cap</b> (<see cref="TargetActiveCount"/>): a population-scaled
    /// target so a small neighborhood stays cozy and a large one never floods
    /// — <c>clamp(round(dogCount / divisor), floor, ceiling)</c>.</para>
    /// </summary>
    public sealed class QuestPacingPolicy
    {
        /// <summary>#704: whether the board is waiting on more quests — it
        /// holds fewer than <see cref="TargetActiveCount"/>. This is what starts
        /// (and stops) the refresh clock: a board at the cap is waiting for
        /// nothing, so no clock runs while it is full.</summary>
        public bool IsBoardBelowTarget(GameState state)
        {
            return state.Quests.ActiveQuests.Count() < TargetActiveCount(state);
        }

        /// <summary>Whether it is time to add quests: at least
        /// <see cref="EconomyNumbers.RefreshInterval"/> has elapsed since the
        /// board dropped below target
        /// (<see cref="GameState.QuestRefreshTimerStartedUtc"/>). False while no
        /// clock is running — a full board. <paramref name="nowUtc"/> must be a
        /// UTC instant; the policy never reads <c>DateTime.Now</c> or
        /// <c>TimeZoneInfo.Local</c>.
        ///
        /// <para>#704 changed what starts this clock. It used to measure from
        /// <see cref="GameState.LastRotationUtc"/> — always ticking, whether or
        /// not the board had room, and true outright for a game that had never
        /// rotated. The hour is now spent waiting for a slot the player has
        /// actually opened, so a full board can never bank refreshes and an
        /// empty one starts its hour the moment it empties.</para></summary>
        public bool ShouldRefresh(DateTime nowUtc, GameState state)
        {
            if (!state.QuestRefreshTimerStartedUtc.HasValue)
            {
                return false;
            }

            return nowUtc - state.QuestRefreshTimerStartedUtc.Value >= EconomyNumbers.RefreshInterval;
        }

        /// <summary>How many active quests the neighborhood should hold:
        /// <c>clamp(round(Dogs.Count / <see cref="EconomyNumbers.TargetActiveDivisor"/>),
        /// <see cref="EconomyNumbers.TargetActiveFloor"/>,
        /// <see cref="EconomyNumbers.TargetActiveCeiling"/>)</c>. Takes the whole
        /// <paramref name="state"/> so a future rule can scale on any signal
        /// (progression, quest-type mix) without changing callers.</summary>
        public int TargetActiveCount(GameState state)
        {
            var raw = (int)Math.Round(
                (double)state.Dogs.Count / EconomyNumbers.TargetActiveDivisor,
                MidpointRounding.AwayFromZero);

            if (raw < EconomyNumbers.TargetActiveFloor)
            {
                return EconomyNumbers.TargetActiveFloor;
            }

            if (raw > EconomyNumbers.TargetActiveCeiling)
            {
                return EconomyNumbers.TargetActiveCeiling;
            }

            return raw;
        }

        /// <summary>#543/#743: how much of the board <b>one refresh</b> adds —
        /// the population-scaled <see cref="TargetActiveCount"/> spread over
        /// <see cref="EconomyNumbers.PacingWindowHours"/> and then sliced by the
        /// refresh interval:
        /// <c>target × RefreshIntervalMinutes / (PacingWindowHours × 60)</c>.
        ///
        /// <para>#743 renamed this off "per hour" and put the interval factor
        /// back in. The old <c>PerHourRate</c> returned <c>target / window</c>
        /// yet was added once per refresh <em>boundary</em>, which only read
        /// correctly because the interval happened to be exactly one hour; at
        /// the 15-minute cadence that omission would have paid a full quarter
        /// of the target sixteen times an hour. With the factor restored, one
        /// pacing window's worth of refreshes accrues exactly the target
        /// whatever the interval — the interval decides only how granular the
        /// trickle is.</para>
        ///
        /// <para>Over the shipping 4h window a target of 6 is 0.375 per
        /// 15-minute refresh (1.5/hr), 12 → 0.75 (3.0/hr), and the floor of 5 →
        /// 0.3125 (1.25/hr). This is a fractional amount;
        /// <see cref="AdvanceAccumulator(double, GameState, out double)"/> turns
        /// it into whole quests without ever creating a fractional one.</para></summary>
        public double PerRefreshRate(GameState state)
        {
            return TargetActiveCount(state) * EconomyNumbers.RefreshIntervalMinutes
                / (double)(EconomyNumbers.PacingWindowHours * EconomyNumbers.MinutesPerHour);
        }

        /// <summary>#543: one hourly step of the error-diffusion (Bresenham-style)
        /// accumulator. Adds <see cref="PerRefreshRate"/> to
        /// <paramref name="accumulator"/>, returns the whole quests to add this
        /// hour (<c>floor</c> of the sum — 0 on a quiet hour is expected and
        /// fine), and hands back the leftover fraction (&lt; 1) in
        /// <paramref name="remainingAccumulator"/> to carry to the next hour. The
        /// long-run whole-quest rate equals <see cref="PerRefreshRate"/>, so (with
        /// #624's 4h window) a 1.25/hr floor target adds five quests every four
        /// hours and a 1.5/hr target adds six — never a fractional quest. Pure: no state
        /// is read or written here, so the caller
        /// (<see cref="QuestManager.StartNewDay"/>) owns persisting the
        /// remainder.</summary>
        public int AdvanceAccumulator(double accumulator, GameState state, out double remainingAccumulator)
        {
            var advanced = accumulator + PerRefreshRate(state);
            var whole = (int)Math.Floor(advanced);
            remainingAccumulator = advanced - whole;
            return whole;
        }

        /// <summary>#317: the population-gated purchasable subject pool for a
        /// quest type — every <see cref="ItemCatalog"/> entry tagged
        /// <paramref name="tag"/> whose cost falls in a tier eligible at the
        /// neighborhood's current population (<c>state.Dogs.Count</c>). The
        /// pacing seam owns pool selection so difficulty scaling lives here,
        /// not in the quest engine; the pure classification is
        /// <see cref="QuestCostTiers.EligibleNames"/>. No new persisted state —
        /// population is already known. Find-only (no-cost) entries and the
        /// no-item PestControl type are unaffected, since this filters only
        /// purchasable subjects.</summary>
        public IReadOnlyList<string> EligibleSubjectPool(ItemEligibility tag, GameState state)
        {
            return QuestCostTiers.EligibleNames(ItemCatalog.Items, tag, state.Dogs.Count);
        }
    }
}
