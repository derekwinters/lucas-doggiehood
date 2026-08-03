using System;
using System.Collections.Generic;
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
    /// against a persisted UTC timestamp. Each hour a fractional amount
    /// (<see cref="PerHourRate"/>, driven by <see cref="AdvanceAccumulator"/>)
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
        /// <summary>Whether it is time to add quests. True when no rotation has
        /// happened yet, or at least <see cref="EconomyNumbers.RefreshInterval"/>
        /// has elapsed since the last one. <paramref name="nowUtc"/> must be a
        /// UTC instant; the policy never reads <c>DateTime.Now</c> or
        /// <c>TimeZoneInfo.Local</c>.</summary>
        public bool ShouldRefresh(DateTime nowUtc, GameState state)
        {
            if (!state.LastRotationUtc.HasValue)
            {
                return true;
            }

            return nowUtc - state.LastRotationUtc.Value >= EconomyNumbers.RefreshInterval;
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

        /// <summary>#543: the per-hour quest trickle rate — the population-scaled
        /// <see cref="TargetActiveCount"/> spread over
        /// <see cref="EconomyNumbers.PacingWindowHours"/>
        /// (<c>target / window</c>). So target 6 over a 6h window is 1.0/hr,
        /// 12 → 2.0/hr, 3 → 0.5/hr, 4 → ~0.667/hr. This is a fractional rate;
        /// <see cref="AdvanceAccumulator"/> turns it into whole quests per hour
        /// without ever creating a fractional quest.</summary>
        public double PerHourRate(GameState state)
        {
            return TargetActiveCount(state) / (double)EconomyNumbers.PacingWindowHours;
        }

        /// <summary>#543: one hourly step of the error-diffusion (Bresenham-style)
        /// accumulator. Adds <see cref="PerHourRate"/> to
        /// <paramref name="accumulator"/>, returns the whole quests to add this
        /// hour (<c>floor</c> of the sum — 0 on a quiet hour is expected and
        /// fine), and hands back the leftover fraction (&lt; 1) in
        /// <paramref name="remainingAccumulator"/> to carry to the next hour. The
        /// long-run whole-quest rate equals <see cref="PerHourRate"/>, so a
        /// 0.5/hr target adds one quest every other hour and a 0.667/hr target
        /// adds two every three hours — never a fractional quest. Pure: no state
        /// is read or written here, so the caller
        /// (<see cref="QuestManager.StartNewDay"/>) owns persisting the
        /// remainder.</summary>
        public int AdvanceAccumulator(double accumulator, GameState state, out double remainingAccumulator)
        {
            var advanced = accumulator + PerHourRate(state);
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
