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
    /// <para><b>Cadence</b> (<see cref="ShouldRefresh"/>,
    /// <see cref="ElapsedRefreshIntervals"/>): every
    /// <see cref="EconomyNumbers.RefreshInterval"/> (15 minutes since #743,
    /// hourly before that), measured against a persisted UTC timestamp — since
    /// #704 the instant the board dropped below <see cref="TargetActiveCount"/>
    /// (<see cref="GameState.QuestRefreshTimerStartedUtc"/>), so the wait is
    /// spent waiting for a slot the player actually opened rather than ticking
    /// against a full board. Each refresh a fractional amount
    /// (<see cref="PerRefreshRate"/>, driven by <see cref="AdvanceAccumulator(double, long, GameState, out double)"/>)
    /// trickles in, replacing the old 8h all-or-nothing batch. This is a
    /// boundary <em>check</em>, not a
    /// timer/countdown/expiry — nothing is ever removed and no quest can fail,
    /// so it stays inside the "no timer or fail condition anywhere" constraint
    /// (economy.md #28). UTC-only, so a device-timezone change can neither
    /// double-fire nor stall it.</para>
    ///
    /// <para><b>Window over interval</b> (#743): the pacing window is the
    /// authority and the refresh interval is only granularity. One window's
    /// worth of refreshes accrues exactly <see cref="TargetActiveCount"/>
    /// whatever the interval, which is why time away needs no max-offline
    /// clamp — the accrual reaches the cap on its own and the batch caps hold
    /// from there.</para>
    ///
    /// <para><b>Cap</b> (<see cref="TargetActiveCount"/>): a population-scaled
    /// target so a small neighborhood stays cozy and a large one never floods
    /// — <c>clamp(round(dogCount / divisor), floor, ceiling)</c>.</para>
    /// </summary>
    public sealed class QuestPacingPolicy
    {
        /// <summary>Ceiling on the whole quests a single accumulator advance can
        /// report (#743 guard 3), so an absurdly long absence saturates instead
        /// of overflowing. Far above any reachable board size — the batch caps
        /// clamp the actual add to the headroom regardless.</summary>
        public const int MaxWholeQuestsPerAdvance = int.MaxValue;

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
        /// rotated. The wait is now spent waiting for a slot the player has
        /// actually opened, so a full board can never bank refreshes and an
        /// empty one starts its wait the moment it empties.</para>
        ///
        /// <para>#743: expressed as "at least one whole interval has elapsed"
        /// so this predicate and <see cref="ElapsedRefreshIntervals"/> — which
        /// is what decides how <em>many</em> pay out — can never disagree about
        /// where a boundary falls.</para></summary>
        public bool ShouldRefresh(DateTime nowUtc, GameState state)
        {
            return ElapsedRefreshIntervals(nowUtc, state) >= 1L;
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

        /// <summary>#743: how many whole <see cref="EconomyNumbers.RefreshInterval"/>
        /// boundaries have passed since the board dropped below target
        /// (<see cref="GameState.QuestRefreshTimerStartedUtc"/>) —
        /// <c>floor((nowUtc − timerStart) / interval)</c>. Zero while no clock
        /// is running (a full board is waiting for nothing).
        ///
        /// <para>This is the number that makes time away count: every interval
        /// it reports is paid out, whether the app was open for it or closed.
        /// <paramref name="nowUtc"/> must be a UTC instant — the policy never
        /// reads <c>DateTime.Now</c> or <c>TimeZoneInfo.Local</c>.</para>
        ///
        /// <para><b>Guard 1</b>: if <paramref name="nowUtc"/> lands <em>before</em>
        /// the persisted start (a device clock moved back, a hand-edited save),
        /// the raw quotient goes negative and would <em>subtract</em> from the
        /// accumulator, so it is clamped at zero. A defensive floor, not
        /// clock-tampering hardening.</para></summary>
        public long ElapsedRefreshIntervals(DateTime nowUtc, GameState state)
        {
            if (!state.QuestRefreshTimerStartedUtc.HasValue)
            {
                return 0L;
            }

            var elapsedTicks = (nowUtc - state.QuestRefreshTimerStartedUtc.Value).Ticks;
            if (elapsedTicks <= 0L)
            {
                return 0L;
            }

            return elapsedTicks / EconomyNumbers.RefreshInterval.Ticks;
        }

        /// <summary>One step of the error-diffusion (Bresenham-style)
        /// accumulator — see
        /// <see cref="AdvanceAccumulator(double, long, GameState, out double)"/>,
        /// which this is the single-interval case of.</summary>
        public int AdvanceAccumulator(double accumulator, GameState state, out double remainingAccumulator)
        {
            return AdvanceAccumulator(accumulator, 1L, state, out remainingAccumulator);
        }

        /// <summary>#543/#743: <paramref name="intervals"/> steps of the
        /// error-diffusion (Bresenham-style) accumulator, taken <b>in one
        /// step</b>. Adds <c>intervals × <see cref="PerRefreshRate"/></c> to
        /// <paramref name="accumulator"/>, returns the whole quests those
        /// refreshes are worth (<c>floor</c> of the sum — 0 across a quiet
        /// stretch is expected and fine), and hands back the leftover fraction
        /// (&lt; 1) in <paramref name="remainingAccumulator"/> to carry forward.
        ///
        /// <para><b>Why one step and not a loop.</b> The carried fraction
        /// telescopes: running N single steps from <c>a₀</c> totals
        /// <c>floor(a₀ + N × rate)</c> for any starting fraction, which is
        /// exactly what this computes. So the shortcut is not an approximation
        /// — it is the same number — and it costs the same whether the player
        /// was away four hours or four months (#743). The equivalence is pinned
        /// directly by a test.</para>
        ///
        /// <para><b>Guard 3</b>: a save left for centuries yields an enormous
        /// interval count, so the result saturates at
        /// <see cref="MaxWholeQuestsPerAdvance"/> rather than overflowing
        /// <see cref="int"/> or carrying a non-finite fraction. Saturating is
        /// harmless: the batch caps clamp the actual add to the board's headroom
        /// long before that.</para>
        ///
        /// <para>Pure: no state is read or written here, so the caller
        /// (<see cref="QuestManager.StartNewDay(long, Random)"/>) owns persisting
        /// the remainder.</para></summary>
        public int AdvanceAccumulator(
            double accumulator, long intervals, GameState state, out double remainingAccumulator)
        {
            if (intervals <= 0L)
            {
                remainingAccumulator = accumulator;
                return 0;
            }

            var advanced = accumulator + intervals * PerRefreshRate(state);
            if (!(advanced < MaxWholeQuestsPerAdvance))
            {
                // Also catches NaN/Infinity: neither compares less-than.
                remainingAccumulator = 0d;
                return MaxWholeQuestsPerAdvance;
            }

            var whole = (int)Math.Floor(advanced);
            remainingAccumulator = advanced - whole;
            return whole;
        }

        /// <summary>#743: how long until the next <em>real</em> quest arrives —
        /// <c>ceil((1 − accumulator) / <see cref="PerRefreshRate"/>)</c> refresh
        /// intervals. Null when the board is at
        /// <see cref="TargetActiveCount"/>: nothing is pending, so there is
        /// nothing honest to show.
        ///
        /// <para><b>Why not "time to the next refresh".</b> Most refreshes add
        /// zero quests at a small target (11 of 16 at the floor over a 4h
        /// window) — that is normal accumulator behavior, but a countdown
        /// pointing at the next <em>boundary</em> would hit zero four times an
        /// hour while a quest appeared once. This counts boundaries until the
        /// carried fraction actually tips a whole quest in, so the number never
        /// lies.</para>
        ///
        /// <para>Always at least one whole interval and always a whole multiple
        /// of one, because a quest can only ever arrive on a boundary. This is
        /// game-rules arithmetic, not drawing: #683 paints it and carries no
        /// Core logic of its own. Pure — reads
        /// <see cref="GameState.QuestPacingAccumulator"/> and nothing else, and
        /// never a wall clock.</para></summary>
        public TimeSpan? TimeUntilNextQuest(GameState state)
        {
            if (!IsBoardBelowTarget(state))
            {
                return null;
            }

            var pending = 1d - state.QuestPacingAccumulator;
            var refreshes = (long)Math.Ceiling(pending / PerRefreshRate(state));
            if (refreshes < 1L)
            {
                refreshes = 1L;
            }

            return TimeSpan.FromTicks(EconomyNumbers.RefreshInterval.Ticks * refreshes);
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
